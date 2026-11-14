using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Models.Request;
using Gestio.Api.Models.Response;
using Gestio.Api.Services;
using Gestio.Api.Services.Verifactu;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Gestio.Api.Tests;

/// <summary>
/// UT22, capa 3: creación del registro al emitir (PB-21), consulta, verificación y autorización.
/// InMemory NO tiene transacciones ni bloqueos: la atomicidad, la reversión y la concurrencia solo se demuestran
/// en las pruebas [PostgreSqlFact], que usan un esquema temporal propio con todas las migraciones.
/// </summary>
public sealed class RegistroFacturacionServicioTests
{
    private static readonly Guid AutonomoA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AutonomoB = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid GestorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ContactoA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ContactoB = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateOnly Fecha = new(2026, 6, 16);
    // 100 € al 21 %: base 100,00, IVA 21,00. Valores esperados escritos a mano, no calculados con el código.
    private static readonly IReadOnlyList<GuardarLineaFacturaRequest> Linea = [new("Servicio profesional", 1m, 100m, 21m)];
    private static readonly DateTimeOffset Ahora = new(2026, 6, 16, 13, 24, 1, 987, TimeSpan.Zero);

    // ─── InMemory: flujo funcional ───────────────────────────────────────────────

    [Fact]
    public async Task Criterio1_PrimeraFacturaEmitida_CreaUnUnicoRegistroComoInicioDeCadena()
    {
        await using var db = await CrearDbAsync();
        var emitida = await EmitirNuevaAsync(db, AutonomoA, ContactoA);

        var registro = Assert.Single(await db.RegistrosFacturacion.AsNoTracking().ToListAsync());
        Assert.Equal(emitida.Id, registro.FacturaId);
        Assert.Equal(AutonomoA, registro.AutonomoId);
        Assert.Equal(TipoRegistroFacturacion.ALTA, registro.Tipo);
        Assert.Equal(1, registro.Secuencia);
        Assert.Null(registro.AnteriorId);
        Assert.Null(registro.HuellaAnterior);
        Assert.Equal("87654321X", registro.IdEmisorFactura);
        Assert.Equal(emitida.NumeroVisible, registro.NumSerieFactura); // mismo texto que ve el usuario
        Assert.Equal("F1", registro.TipoFactura);
        Assert.Equal(21.00m, registro.CuotaTotal);
        Assert.Equal(121.00m, registro.ImporteTotal);
        Assert.Equal(Fecha, registro.FechaExpedicionFactura);
        // Mismo instante que la emisión, truncado a segundos y en hora de Madrid (verano: +02:00).
        Assert.Equal(new DateTimeOffset(2026, 6, 16, 13, 24, 1, TimeSpan.Zero), registro.Generado);
        Assert.Equal("2026-06-16T15:24:01+02:00", registro.FechaHoraHusoGenRegistro);
        Assert.Equal(Ahora, emitida.Emitida);
        Assert.Matches("^[0-9A-F]{64}$", registro.Huella);
    }

    [Fact]
    public async Task Criterio2_DosFacturasSucesivas_ElSegundoApuntaAlPrimero_YLaCadenaEsCoherente()
    {
        await using var db = await CrearDbAsync();
        await EmitirNuevaAsync(db, AutonomoA, ContactoA);
        await EmitirNuevaAsync(db, AutonomoA, ContactoA);

        var registros = await db.RegistrosFacturacion.AsNoTracking().OrderBy(r => r.Secuencia).ToListAsync();
        Assert.Equal(2, registros.Count);
        Assert.Equal(registros[0].Id, registros[1].AnteriorId);
        Assert.Equal(registros[0].Huella, registros[1].HuellaAnterior);

        var verificacion = (await Registros(db).VerificarAsync(Propietario(AutonomoA), AutonomoA, default)).Valor!;
        Assert.True(verificacion.Coherente);
        Assert.Equal(2, verificacion.Revisados);
        Assert.Equal(2, verificacion.TotalRegistros);
    }

    [Fact]
    public async Task PA1589_ReintentarLaEmision_NoCreaUnSegundoRegistro()
    {
        await using var db = await CrearDbAsync();
        var borrador = await BorradorAsync(db, AutonomoA, ContactoA);
        var primera = await Facturacion(db).EmitirAsync(Propietario(AutonomoA), AutonomoA, borrador, default);
        var reintento = await Facturacion(db).EmitirAsync(Propietario(AutonomoA), AutonomoA, borrador, default);

        Assert.True(primera.EsCorrecto);
        Assert.True(reintento.EsCorrecto);
        Assert.Equal(primera.Valor!.Numero, reintento.Valor!.Numero);
        Assert.Single(await db.RegistrosFacturacion.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task D6_ImporteTotalNoDescuentaLaRetencion()
    {
        await using var db = await CrearDbAsync();
        // Base 100, IVA 21, retención 15 % (15): la factura cobra 106, pero el ImporteTotal del registro es 121.
        var emitida = await EmitirNuevaAsync(db, AutonomoA, ContactoA, retencion: 15m);

        var registro = await db.RegistrosFacturacion.AsNoTracking().SingleAsync();
        Assert.Equal(106.00m, emitida.Total);
        Assert.Equal(121.00m, registro.ImporteTotal);
        Assert.Equal(21.00m, registro.CuotaTotal);
    }

    [Fact]
    public async Task PA1592_FacturasDeDosAutonomos_FormanCadenasIndependientes()
    {
        await using var db = await CrearDbAsync();
        await EmitirNuevaAsync(db, AutonomoA, ContactoA);
        await EmitirNuevaAsync(db, AutonomoB, ContactoB);
        await EmitirNuevaAsync(db, AutonomoA, ContactoA);

        var deA = (await Registros(db).ListarAsync(Propietario(AutonomoA), AutonomoA, default)).Valor!;
        var deB = (await Registros(db).ListarAsync(Propietario(AutonomoB), AutonomoB, default)).Valor!;

        Assert.Equal([1, 2], deA.Select(r => r.Secuencia));
        Assert.Equal(deA[0].Id, deA[1].AnteriorId);
        var unicoDeB = Assert.Single(deB);
        Assert.Equal(1, unicoDeB.Secuencia);
        Assert.Null(unicoDeB.AnteriorId);
        Assert.Equal("12345678Z", unicoDeB.IdEmisorFactura);
        Assert.True((await Registros(db).VerificarAsync(Propietario(AutonomoA), AutonomoA, default)).Valor!.Coherente);
        Assert.True((await Registros(db).VerificarAsync(Propietario(AutonomoB), AutonomoB, default)).Valor!.Coherente);
    }

    [Fact]
    public async Task PA1593_SinAutorizacion_NoSePuedenConsultarNiVerificarLosRegistrosDeOtroAutonomo()
    {
        await using var db = await CrearDbAsync(conGestorVinculadoA: true);
        await EmitirNuevaAsync(db, AutonomoA, ContactoA);
        var servicio = Registros(db);

        var otroAutonomo = Propietario(AutonomoB);
        var gestorNoVinculado = new UsuarioActual(Guid.NewGuid(), Rol.GESTOR);
        foreach (var intruso in new[] { otroAutonomo, gestorNoVinculado })
        {
            Assert.Equal(TipoError.PROHIBIDO, (await servicio.ListarAsync(intruso, AutonomoA, default)).Error!.Tipo);
            Assert.Equal(TipoError.PROHIBIDO, (await servicio.VerificarAsync(intruso, AutonomoA, default)).Error!.Tipo);
        }

        // D4: el gestor con vinculación ACTIVA puede consultar y verificar (solo lectura).
        var gestor = new UsuarioActual(GestorId, Rol.GESTOR);
        Assert.Single((await servicio.ListarAsync(gestor, AutonomoA, default)).Valor!);
        Assert.True((await servicio.VerificarAsync(gestor, AutonomoA, default)).Valor!.Coherente);
    }

    [Fact]
    public async Task Listar_DevuelveLaCadenaEnOrdenConSuEnlaceYSuRepresentacionCanonica()
    {
        await using var db = await CrearDbAsync();
        await EmitirNuevaAsync(db, AutonomoA, ContactoA);
        await EmitirNuevaAsync(db, AutonomoA, ContactoA);

        var lista = (await Registros(db).ListarAsync(Propietario(AutonomoA), AutonomoA, default)).Valor!;

        Assert.Equal([1, 2], lista.Select(r => r.Secuencia));
        Assert.Null(lista[0].SecuenciaAnterior);
        Assert.Equal(1, lista[1].SecuenciaAnterior);
        Assert.Equal("Cliente A", lista[0].ClienteNombre);
        Assert.Equal(
            "IDEmisorFactura=87654321X&NumSerieFactura=F-2026-0001&FechaExpedicionFactura=16-06-2026&TipoFactura=F1" +
            "&CuotaTotal=21.00&ImporteTotal=121.00&Huella=&FechaHoraHusoGenRegistro=2026-06-16T15:24:01+02:00",
            lista[0].RepresentacionCanonica);
        Assert.Contains("&Huella=" + lista[0].Huella + "&", lista[1].RepresentacionCanonica);
        // Huella de ese texto canónico calculada FUERA del código (Python hashlib), para que esta capa no sea circular.
        Assert.Equal("EBA5AF175D47D34EB6DDFDC13D271987412B34D7FE0EC63289A7FEB0D0E6395C", lista[0].Huella);
    }

    [Theory]
    [InlineData("otra-asesoria")]
    [InlineData("pendiente")]
    [InlineData("finalizada")]
    [InlineData("vinculado-a-otro")]
    public async Task PA1593_GestorSinVinculacionActivaConEseAutonomo_NoPuedeConsultar(string caso)
    {
        await using var db = await CrearDbAsync();
        db.Usuarios.Add(new Usuario { Id = GestorId, Nombre = "Gestor", PrimerApellido = "G", SegundoApellido = "G", Email = "g@test",
            Nif = "GESTOR", Rol = Rol.GESTOR, CodigoAsesoria = "ASE-123456" });
        var (autonomo, codigo, estado) = caso switch
        {
            "otra-asesoria" => (AutonomoA, "ASE-999999", EstadoVinculacion.ACTIVA),   // A está vinculado, pero a otra asesoría
            "pendiente" => (AutonomoA, "ASE-123456", EstadoVinculacion.PENDIENTE),
            "finalizada" => (AutonomoA, "ASE-123456", EstadoVinculacion.FINALIZADA),
            _ => (AutonomoB, "ASE-123456", EstadoVinculacion.ACTIVA)                   // el gestor lleva a B, no a A
        };
        db.Vinculaciones.Add(new Vinculacion { Id = Guid.NewGuid(), AutonomoId = autonomo, CodigoAsesoria = codigo,
            Estado = estado, FechaSolicitud = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        await EmitirNuevaAsync(db, AutonomoA, ContactoA);

        var gestor = new UsuarioActual(GestorId, Rol.GESTOR);
        Assert.Equal(TipoError.PROHIBIDO, (await Registros(db).ListarAsync(gestor, AutonomoA, default)).Error!.Tipo);
        Assert.Equal(TipoError.PROHIBIDO, (await Registros(db).VerificarAsync(gestor, AutonomoA, default)).Error!.Tipo);
    }

    [Fact]
    public async Task Verificar_SinRegistros_EsCoherente()
    {
        await using var db = await CrearDbAsync();
        var verificacion = (await Registros(db).VerificarAsync(Propietario(AutonomoA), AutonomoA, default)).Valor!;
        Assert.True(verificacion.Coherente);
        Assert.Equal(0, verificacion.TotalRegistros);
    }

    [Fact]
    public async Task PrepararAlta_RechazaUnaFacturaQueNoEstaEmitida()
    {
        await using var db = await CrearDbAsync();
        var factura = new Factura { Id = Guid.NewGuid(), AutonomoId = AutonomoA, Serie = "F-2026", Fecha = Fecha };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Registros(db).PrepararAltaAsync(factura, Ahora, default));
    }

    // ─── PostgreSQL real: transacción, reversión y concurrencia ─────────────────

    [PostgreSqlFact]
    public Task PA1590_EmisionesSimultaneasDelMismoAutonomo_FormanUnaSecuenciaLineal() => EnEsquemaAsync(async options =>
    {
        var borradores = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            await using var db = new ApplicationDbContext(options);
            borradores.Add(await BorradorAsync(db, AutonomoA, ContactoA));
        }

        var resultados = await Task.WhenAll(borradores.Select(async id =>
        {
            await using var db = new ApplicationDbContext(options);
            return await Facturacion(db).EmitirAsync(Propietario(AutonomoA), AutonomoA, id, default);
        }));

        Assert.All(resultados, r => Assert.True(r.EsCorrecto, r.Error?.Mensaje));
        await using var lectura = new ApplicationDbContext(options);
        var registros = await lectura.RegistrosFacturacion.AsNoTracking().OrderBy(r => r.Secuencia).ToListAsync();
        Assert.Equal([1, 2, 3, 4, 5], registros.Select(r => r.Secuencia));
        for (var i = 1; i < registros.Count; i++)
            Assert.Equal(registros[i - 1].Id, registros[i].AnteriorId); // nadie apunta dos veces al mismo anterior
        Assert.True(VerificadorCadena.Verificar(registros).Coherente);
    });

    [PostgreSqlFact]
    public Task PA1589_DosConfirmacionesSimultaneasDeLaMismaFactura_UnSoloRegistro() => EnEsquemaAsync(async options =>
    {
        Guid borrador;
        await using (var db = new ApplicationDbContext(options)) borrador = await BorradorAsync(db, AutonomoA, ContactoA);

        var resultados = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var db = new ApplicationDbContext(options);
            return await Facturacion(db).EmitirAsync(Propietario(AutonomoA), AutonomoA, borrador, default);
        }));

        Assert.All(resultados, r => Assert.True(r.EsCorrecto, r.Error?.Mensaje));
        await using var lectura = new ApplicationDbContext(options);
        Assert.Equal(1, await lectura.RegistrosFacturacion.CountAsync());
    });

    [PostgreSqlFact]
    public Task PA1591_SiFallaLaCreacionDelRegistro_SeRevierteLaEmisionYSeInforma() => EnEsquemaAsync(async options =>
    {
        Guid borrador;
        await using (var db = new ApplicationDbContext(options))
        {
            borrador = await BorradorAsync(db, AutonomoA, ContactoA);
            // Fallo simulado en la BD al insertar el registro (lo mismo que hará la guía de prueba manual).
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION fallo_simulado() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'fallo simulado al crear el registro'; END; $$;
                CREATE TRIGGER fallo_simulado BEFORE INSERT ON registros_facturacion
                FOR EACH ROW EXECUTE FUNCTION fallo_simulado();
                """);
        }

        Resultado<FacturaResponse> resultado;
        await using (var db = new ApplicationDbContext(options))
            resultado = await Facturacion(db).EmitirAsync(Propietario(AutonomoA), AutonomoA, borrador, default);

        Assert.False(resultado.EsCorrecto);
        Assert.Equal("Factura.EmisionRevertida", resultado.Error!.Codigo);
        Assert.Contains("sigue como borrador", resultado.Error.Mensaje);
        await using (var lectura = new ApplicationDbContext(options))
        {
            var factura = await lectura.Facturas.AsNoTracking().SingleAsync(f => f.Id == borrador);
            Assert.Equal(EstadoFactura.BORRADOR, factura.Estado);
            Assert.Null(factura.Numero);
            Assert.Null(factura.Emitida);
            Assert.Equal(0, await lectura.RegistrosFacturacion.CountAsync());
            await lectura.Database.ExecuteSqlRawAsync("DROP TRIGGER fallo_simulado ON registros_facturacion");
        }

        // Sin el fallo, la misma factura se emite con el número 1: el intento fallido no consumió número.
        await using var reintento = new ApplicationDbContext(options);
        var emitida = await Facturacion(reintento).EmitirAsync(Propietario(AutonomoA), AutonomoA, borrador, default);
        Assert.True(emitida.EsCorrecto, emitida.Error?.Mensaje);
        Assert.Equal(1, emitida.Valor!.Numero);
    });

    [PostgreSqlFact]
    public Task PA1591_TrasUnFallo_UnSaveChangesPosteriorNoGuardaLaEmision() => EnEsquemaAsync(async options =>
    {
        Guid borrador;
        await using (var db = new ApplicationDbContext(options))
        {
            borrador = await BorradorAsync(db, AutonomoA, ContactoA);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION fallo_simulado() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'fallo simulado'; END; $$;
                CREATE TRIGGER fallo_simulado BEFORE INSERT ON registros_facturacion
                FOR EACH ROW EXECUTE FUNCTION fallo_simulado();
                """);
        }

        await using (var db = new ApplicationDbContext(options))
        {
            Assert.False((await Facturacion(db).EmitirAsync(Propietario(AutonomoA), AutonomoA, borrador, default)).EsCorrecto);
            await db.SaveChangesAsync(); // otro código de la misma petición guarda después
        }

        await using var lectura = new ApplicationDbContext(options);
        Assert.Equal(EstadoFactura.BORRADOR, (await lectura.Facturas.AsNoTracking().SingleAsync(f => f.Id == borrador)).Estado);
    });

    [PostgreSqlFact]
    public Task PA1591_ViolacionDeUnicidadDelRegistro_RevierteLaEmisionConSuPropioError() => EnEsquemaAsync(async options =>
    {
        Guid borrador;
        await using (var db = new ApplicationDbContext(options))
        {
            borrador = await BorradorAsync(db, AutonomoA, ContactoA);
            // Estado imposible por la aplicación: ya existe un registro para esa factura (borrador). Al emitir,
            // el nuevo registro choca con UX_registros_facturacion_factura, una violación de unicidad REAL.
            db.RegistrosFacturacion.Add(new RegistroFacturacion
            {
                Id = Guid.NewGuid(), AutonomoId = AutonomoA, FacturaId = borrador, Secuencia = 1, IdEmisorFactura = "87654321X",
                NumSerieFactura = "X", FechaExpedicionFactura = Fecha, TipoFactura = "F1", CuotaTotal = 0m, ImporteTotal = 0m,
                FechaHoraHusoGenRegistro = "2026-06-16T15:24:01+02:00", Generado = Ahora, Huella = new string('0', 64)
            });
            await db.SaveChangesAsync();
        }

        Resultado<FacturaResponse> resultado;
        await using (var db = new ApplicationDbContext(options))
            resultado = await Facturacion(db).EmitirAsync(Propietario(AutonomoA), AutonomoA, borrador, default);

        Assert.Equal("Factura.EmisionRevertida", resultado.Error!.Codigo); // no «NumeroEnConflicto»
        await using var lectura = new ApplicationDbContext(options);
        var factura = await lectura.Facturas.AsNoTracking().SingleAsync(f => f.Id == borrador);
        Assert.Equal(EstadoFactura.BORRADOR, factura.Estado);
        Assert.Null(factura.Numero);
        Assert.Equal(1, await lectura.RegistrosFacturacion.CountAsync());
    });

    [PostgreSqlFact]
    public Task UnaViolacionRealDelIndiceDeNumeracion_InformaTableNameFacturas() => EnEsquemaAsync(async options =>
    {
        // Comprueba la premisa del filtro TableName: "facturas" de EmitirAsync con una violación real.
        await using var db = new ApplicationDbContext(options);
        const string insertar = """
            INSERT INTO facturas ("Id","AutonomoId","Serie","Numero","Fecha","Estado","Creada","Actualizada")
            VALUES (gen_random_uuid(), {0}, 'F-2026', 7, DATE '2026-06-16', 2, now(), now())
            """;
        await db.Database.ExecuteSqlRawAsync(insertar, AutonomoA);

        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(insertar, AutonomoA));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("facturas", error.TableName);
    });

    [PostgreSqlFact]
    public Task ConflictoDeNumeracion_SeInformaComoNumeroEnConflicto_YNoComoFalloDelRegistro() => EnEsquemaAsync(async options =>
    {
        Guid borrador;
        await using (var db = new ApplicationDbContext(options))
        {
            borrador = await BorradorAsync(db, AutonomoA, ContactoA);
            // Con el bloqueo, un conflicto de número real no se puede provocar desde la app; se simula con la misma
            // forma de error que da PostgreSQL (unique_violation sobre la tabla facturas).
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION conflicto_numero() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'numero duplicado' USING ERRCODE = 'unique_violation', TABLE = 'facturas'; END; $$;
                CREATE TRIGGER conflicto_numero BEFORE UPDATE ON facturas FOR EACH ROW EXECUTE FUNCTION conflicto_numero();
                """);
        }

        Resultado<FacturaResponse> resultado;
        await using (var db = new ApplicationDbContext(options))
            resultado = await Facturacion(db).EmitirAsync(Propietario(AutonomoA), AutonomoA, borrador, default);

        Assert.Equal("Factura.NumeroEnConflicto", resultado.Error!.Codigo);
        await using var lectura = new ApplicationDbContext(options);
        Assert.Equal(0, await lectura.RegistrosFacturacion.CountAsync());
    });

    [PostgreSqlFact]
    public Task PrepararAlta_FueraDeLaTransaccionConBloqueo_SeRechaza() => EnEsquemaAsync(async options =>
    {
        await using var db = new ApplicationDbContext(options);
        var factura = new Factura
        {
            Id = Guid.NewGuid(), AutonomoId = AutonomoA, Serie = "F-2026", Fecha = Fecha, Numero = 1,
            Estado = EstadoFactura.EMITIDA, EmisorNif = "87654321X", BaseImponible = 100m, CuotaIva = 21m
        };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Registros(db).PrepararAltaAsync(factura, Ahora, default));
        Assert.Contains("transacción", error.Message);
    });

    [PostgreSqlFact]
    public Task PA1594_EnUnEntornoDePruebas_UnDatoModificadoEnLaBd_SeDetectaYSeSeñalaElRegistro() => EnEsquemaAsync(async options =>
    {
        for (var i = 0; i < 3; i++)
        {
            await using var db = new ApplicationDbContext(options);
            await EmitirNuevaAsync(db, AutonomoA, ContactoA);
        }

        await using var prueba = new ApplicationDbContext(options);
        // Solo en pruebas: se desactiva el trigger de inmutabilidad para simular una manipulación directa.
        await prueba.Database.ExecuteSqlRawAsync("""
            ALTER TABLE registros_facturacion DISABLE TRIGGER registros_facturacion_sin_cambios;
            UPDATE registros_facturacion SET "ImporteTotal" = "ImporteTotal" + 1000 WHERE "Secuencia" = 1;
            ALTER TABLE registros_facturacion ENABLE TRIGGER registros_facturacion_sin_cambios;
            """);

        var verificacion = (await Registros(prueba).VerificarAsync(Propietario(AutonomoA), AutonomoA, default)).Valor!;

        Assert.False(verificacion.Coherente);
        Assert.Equal(1, verificacion.SecuenciaIncoherente);
        Assert.Equal(VerificadorCadena.HuellaNoCoincide, verificacion.Codigo);
        Assert.Equal(3, verificacion.TotalRegistros);
    });

    // ─── Utilidades ──────────────────────────────────────────────────────────────

    private static UsuarioActual Propietario(Guid autonomoId) => new(autonomoId, Rol.AUTONOMO);

    private static RegistroFacturacionService Registros(ApplicationDbContext db) => new(db);

    private static FacturacionService Facturacion(ApplicationDbContext db) =>
        new(db, new GeneradorObligaciones(db, TimeProvider.System), new RelojFijo(Ahora), new RegistroFacturacionService(db), QrDePrueba.Servicio(),
            NullLogger<FacturacionService>.Instance);

    private static async Task<Guid> BorradorAsync(ApplicationDbContext db, Guid autonomoId, Guid contactoId, decimal? retencion = null)
    {
        var borrador = await Facturacion(db).GuardarBorradorAsync(Propietario(autonomoId), autonomoId, null,
            new GuardarFacturaRequest(contactoId, Fecha, retencion, Linea), default);
        Assert.True(borrador.EsCorrecto, borrador.Error?.Mensaje);
        return borrador.Valor!.Id;
    }

    private static async Task<FacturaResponse> EmitirNuevaAsync(ApplicationDbContext db, Guid autonomoId, Guid contactoId, decimal? retencion = null)
    {
        var id = await BorradorAsync(db, autonomoId, contactoId, retencion);
        var emitida = await Facturacion(db).EmitirAsync(Propietario(autonomoId), autonomoId, id, default);
        Assert.True(emitida.EsCorrecto, emitida.Error?.Mensaje);
        return emitida.Valor!;
    }

    private static void Sembrar(ApplicationDbContext db, bool conGestorVinculadoA)
    {
        db.Usuarios.AddRange(
            new Usuario { Id = AutonomoA, Nombre = "Autónomo", PrimerApellido = "A", SegundoApellido = "Uno", Email = "a@test",
                Nif = "87654321X", Rol = Rol.AUTONOMO, DireccionFiscal = "Calle Emisor A" },
            new Usuario { Id = AutonomoB, Nombre = "Autónomo", PrimerApellido = "B", SegundoApellido = "Dos", Email = "b@test",
                Nif = "12345678Z", Rol = Rol.AUTONOMO, DireccionFiscal = "Calle Emisor B" });
        db.Contactos.AddRange(
            new Contacto { Id = ContactoA, AutonomoId = AutonomoA, Clasificacion = "CLIENTE", Nombre = "Cliente A", Nif = "11111111H",
                Direccion = "Calle Cliente A", Activo = true },
            new Contacto { Id = ContactoB, AutonomoId = AutonomoB, Clasificacion = "CLIENTE", Nombre = "Cliente B", Nif = "22222222J",
                Direccion = "Calle Cliente B", Activo = true });
        if (conGestorVinculadoA)
        {
            db.Usuarios.Add(new Usuario { Id = GestorId, Nombre = "Gestor", PrimerApellido = "G", SegundoApellido = "G", Email = "g@test",
                Nif = "GESTOR", Rol = Rol.GESTOR, CodigoAsesoria = "ASE-123456" });
            db.Vinculaciones.Add(new Vinculacion { Id = Guid.NewGuid(), AutonomoId = AutonomoA, CodigoAsesoria = "ASE-123456",
                Estado = EstadoVinculacion.ACTIVA, FechaSolicitud = DateTimeOffset.UtcNow });
        }
    }

    private static async Task<ApplicationDbContext> CrearDbAsync(bool conGestorVinculadoA = false)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Sembrar(db, conGestorVinculadoA);
        await db.SaveChangesAsync();
        return db;
    }

    private static async Task EnEsquemaAsync(Func<DbContextOptions<ApplicationDbContext>, Task> prueba)
    {
        var connectionString = Environment.GetEnvironmentVariable("GESTIO_TEST_POSTGRES")!;
        var schema = $"ut22_test_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            var schemaConnection = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(schemaConnection).Options;
            await using (var db = new ApplicationDbContext(options))
            {
                await db.Database.MigrateAsync();
                Sembrar(db, conGestorVinculadoA: false);
                await db.SaveChangesAsync();
            }
            await prueba(options);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
