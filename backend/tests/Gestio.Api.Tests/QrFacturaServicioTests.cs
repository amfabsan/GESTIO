using Gestio.Api.Common;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Infrastructure.VeriFactu;
using Gestio.Api.Models.Request;
using Gestio.Api.Models.Response;
using Gestio.Api.Services;
using Gestio.Api.Services.Verifactu;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Gestio.Api.Tests;

/// <summary>
/// UT23 (PB-23), capa 2: asignación del código al emitir, construcción del QR y consulta pública.
/// Los criterios 1601-1609 completos (endpoint, PDF, móvil) se cierran en las capas 3 y 4; aquí se prueba el servicio.
/// </summary>
public sealed class QrFacturaServicioTests
{
    private static readonly Guid AutonomoId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ContactoId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateOnly Fecha = new(2026, 6, 5);
    private static readonly IReadOnlyList<GuardarLineaFacturaRequest> Lineas = [new("Servicio de catering", 1m, 2545.45m, 10m)];
    private const string Base = "http://192.168.1.20:5173";

    // ─── Emisión: el código nace con la factura emitida ───────────────────────────

    [Fact]
    public async Task Req1_AlEmitir_LaFacturaRecibeUnCodigoPublicoValido()
    {
        await using var db = await CrearDbAsync();
        var emitida = await EmitirNuevaAsync(db);

        var codigo = await CodigoDeAsync(db, emitida.Id);

        Assert.Matches("^[A-Za-z0-9]{22}$", codigo);
    }

    [Fact]
    public async Task PA1603_UnBorradorNoTieneCodigoNiQr()
    {
        await using var db = await CrearDbAsync();
        var borrador = await BorradorAsync(db);

        Assert.Null(await CodigoDeAsync(db, borrador));
        Assert.Null(Qr().Para(null));
    }

    [Fact]
    public async Task PA1601_ReemitirYConsultarDeNuevo_ConservanElMismoCodigoYLaMismaUrl()
    {
        await using var db = await CrearDbAsync();
        var id = await BorradorAsync(db);
        await Servicio(db).EmitirAsync(Propietario, AutonomoId, id, default);
        var primero = await CodigoDeAsync(db, id);
        var qrPrimero = Qr().Para(primero)!;

        // Emitir otra vez es idempotente (UT21) y consultar no regenera nada.
        var repetida = await Servicio(db).EmitirAsync(Propietario, AutonomoId, id, default);
        await Servicio(db).ConsultarAsync(Propietario, AutonomoId, id, default);
        var segundo = await CodigoDeAsync(db, id);
        var qrSegundo = Qr().Para(segundo)!;

        Assert.True(repetida.EsCorrecto);
        Assert.Equal(primero, segundo);
        Assert.Equal(qrPrimero.Url, qrSegundo.Url);
    }

    [Fact]
    public async Task PA1608_DosFacturasEmitidas_TienenCodigosYUrlsDistintos()
    {
        await using var db = await CrearDbAsync();
        var una = await EmitirNuevaAsync(db);
        var otra = await EmitirNuevaAsync(db);

        var codigoUna = await CodigoDeAsync(db, una.Id);
        var codigoOtra = await CodigoDeAsync(db, otra.Id);

        Assert.NotEqual(codigoUna, codigoOtra);
        Assert.NotEqual(Qr().Para(codigoUna)!.Url, Qr().Para(codigoOtra)!.Url);
    }

    // ─── Contenido del QR (req. 2, prueba 1609) ───────────────────────────────────

    [Fact]
    public async Task PA1609_ElQrCodificaSoloLaBaseYElCodigo_SinDatosFiscales()
    {
        await using var db = await CrearDbAsync();
        var emitida = await EmitirNuevaAsync(db);
        var codigo = (await CodigoDeAsync(db, emitida.Id))!;

        var url = Qr().Para(codigo)!.Url;

        // Igualdad exacta escrita a mano: si se añadiera cualquier parámetro, falla.
        Assert.Equal($"http://192.168.1.20:5173/f/{codigo}", url);
        foreach (var dato in new[] { "87654321X", "12345678Z", emitida.NumeroVisible!, "2800", "2026-06-05", "05-06-2026", "Cliente" })
            Assert.DoesNotContain(dato, url, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("http://192.168.1.20:5173/")]
    [InlineData("http://192.168.1.20:5173")]
    [InlineData("  http://192.168.1.20:5173/  ")] // espacios copiados al configurar
    public void ConstruirUrl_NoDuplicaLaBarraFinal(string baseConfigurada)
    {
        var codigo = CodigoPublicoFactura.Generar();

        Assert.Equal($"http://192.168.1.20:5173/f/{codigo}", Qr(baseConfigurada).ConstruirUrl(codigo));
    }

    [Fact]
    public void ConstruirUrl_RechazaUnCodigoMalformado()
    {
        Assert.Throws<ArgumentException>(() => Qr().ConstruirUrl("../../api/facturas"));
    }

    [Fact]
    public void Para_DevuelveUnPngYEsDeterminista()
    {
        var codigo = CodigoPublicoFactura.Generar();

        var a = Qr().Para(codigo)!;
        var b = Qr().Para(codigo)!;
        var bytes = Convert.FromBase64String(a.ImagenPngBase64);

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes[..4]); // cabecera PNG
        Assert.Equal(Qr().ConstruirUrl(codigo), a.Url);
        Assert.Equal(a.ImagenPngBase64, b.ImagenPngBase64);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/f")]                         // relativa
    [InlineData("ftp://192.168.1.20")]         // esquema no http(s)
    [InlineData("http://192.168.1.20/?nif=1")] // query: el QR llevaría algo más que el código
    [InlineData("http://192.168.1.20/#x")]     // fragmento
    [InlineData("http://u:p@192.168.1.20")]    // credenciales en la URL
    public void D7_UnaUrlBaseNoValida_SeRechazaAlCrearElServicio(string baseConfigurada)
    {
        Assert.Throws<InvalidOperationException>(() => Qr(baseConfigurada));
    }

    // ─── Consulta pública (req. 3, 6 y 7) ─────────────────────────────────────────

    [Fact]
    public async Task PA1605_LaConsultaPublica_DevuelveLosDatosDeLaFacturaEmitida()
    {
        await using var db = await CrearDbAsync();
        var emitida = await EmitirNuevaAsync(db);
        var codigo = await CodigoDeAsync(db, emitida.Id);

        var publica = await new ConsultaPublicaFacturaService(db).ConsultarAsync(codigo, default);

        Assert.True(publica.EsCorrecto);
        Assert.Equal(emitida.EmisorNombre, publica.Valor!.EmisorNombre);
        Assert.Equal(emitida.NumeroVisible, publica.Valor.SerieNumero);
        Assert.Equal("F-2026-0001", publica.Valor.SerieNumero);
        Assert.Equal(Fecha, publica.Valor.Fecha);
        Assert.Equal(emitida.Total, publica.Valor.Total);
        Assert.Equal(2800.00m, publica.Valor.Total); // 2545,45 + 10 % IVA, calculado a mano (mockup)
    }

    [Fact]
    public async Task PA1604_CodigoInexistenteModificadoOMalformado_DanElMismoErrorSinDatos()
    {
        await using var db = await CrearDbAsync();
        var emitida = await EmitirNuevaAsync(db);
        var real = (await CodigoDeAsync(db, emitida.Id))!;
        var modificado = (real[0] == 'A' ? 'B' : 'A') + real[1..];
        var servicio = new ConsultaPublicaFacturaService(db);

        foreach (var codigo in new[] { modificado, CodigoPublicoFactura.Generar(), real[..21], real + "A", "", null, "../../x" })
        {
            var resultado = await servicio.ConsultarAsync(codigo, default);
            Assert.False(resultado.EsCorrecto);
            Assert.Null(resultado.Valor);
            Assert.Equal(ConsultaPublicaFacturaService.EnlaceNoValido, resultado.Error);
        }
    }

    [Fact]
    public async Task PA1604_UnCodigoEnUnaFacturaNoEmitida_NoSeEnseña()
    {
        // En PostgreSQL lo impide el CHECK de la capa 1; aquí se prueba que el servicio no confía solo en eso.
        await using var db = await CrearDbAsync();
        var id = await BorradorAsync(db);
        var codigo = CodigoPublicoFactura.Generar();
        var borrador = await db.Facturas.SingleAsync(f => f.Id == id);
        borrador.CodigoPublico = codigo;
        await db.SaveChangesAsync();

        var resultado = await new ConsultaPublicaFacturaService(db).ConsultarAsync(codigo, default);

        Assert.Equal(ConsultaPublicaFacturaService.EnlaceNoValido, resultado.Error);
    }

    [Fact]
    public void PA1607_LaFacturaPublica_TieneExactamenteLosCuatroCamposPermitidos()
    {
        var campos = typeof(FacturaPublica).GetProperties().Select(p => p.Name).Order().ToArray();

        Assert.Equal(["EmisorNombre", "Fecha", "SerieNumero", "Total"], campos);
    }

    [Fact]
    public void PA1605_LaConsultaPublica_NoDependeDelUsuario()
    {
        // Sin sesión: el servicio no puede recibir ICurrentUserService ni un UsuarioActual.
        var parametros = typeof(ConsultaPublicaFacturaService).GetConstructors().Single().GetParameters()
            .Concat(typeof(ConsultaPublicaFacturaService).GetMethod(nameof(ConsultaPublicaFacturaService.ConsultarAsync))!.GetParameters())
            .Select(p => p.ParameterType).ToArray();

        Assert.DoesNotContain(typeof(ICurrentUserService), parametros);
        Assert.DoesNotContain(typeof(UsuarioActual), parametros);
    }

    // ─── PostgreSQL real: colisión del código y reversión ─────────────────────────

    [PostgreSqlFact]
    public Task Colision_DelCodigo_SeInformaComoTalYRevierteLaEmision() => EnEsquemaAsync(async options =>
    {
        Guid id;
        await using (var db = new ApplicationDbContext(options))
        {
            await EmitirNuevaAsync(db); // ya existe una factura con código
            id = await BorradorAsync(db);
            // Una colisión real es inalcanzable (≈2^-131). Se provoca de verdad: al emitir, el trigger de prueba sustituye
            // el código nuevo por el de la factura existente, y es el índice REAL de la migración el que lo rechaza.
            // ("colision_codigo" se ejecuta antes que "facturas_codigo_publico_inmutable" por orden alfabético.)
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION colision_codigo() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF OLD."CodigoPublico" IS NULL AND NEW."CodigoPublico" IS NOT NULL THEN
                        NEW."CodigoPublico" := (SELECT "CodigoPublico" FROM facturas WHERE "CodigoPublico" IS NOT NULL LIMIT 1);
                    END IF;
                    RETURN NEW;
                END; $$;
                CREATE TRIGGER colision_codigo BEFORE UPDATE ON facturas FOR EACH ROW EXECUTE FUNCTION colision_codigo();
                """);
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var resultado = await Servicio(db).EmitirAsync(Propietario, AutonomoId, id, default);
            Assert.Equal("Factura.CodigoPublicoEnConflicto", resultado.Error!.Codigo);
        }

        await using var lectura = new ApplicationDbContext(options);
        var factura = await lectura.Facturas.AsNoTracking().SingleAsync(f => f.Id == id);
        Assert.Equal(EstadoFactura.BORRADOR, factura.Estado);
        Assert.Null(factura.CodigoPublico);
        Assert.Null(factura.Numero);
        Assert.Equal(1, await lectura.RegistrosFacturacion.CountAsync()); // solo el de la primera factura
    });

    [PostgreSqlFact]
    public Task PA1605_EnPostgreSql_EmitirYConsultarPublicamente() => EnEsquemaAsync(async options =>
    {
        FacturaResponse emitida;
        await using (var db = new ApplicationDbContext(options))
            emitida = await EmitirNuevaAsync(db);

        await using var lectura = new ApplicationDbContext(options);
        var codigo = await CodigoDeAsync(lectura, emitida.Id);
        var publica = await new ConsultaPublicaFacturaService(lectura).ConsultarAsync(codigo, default);

        Assert.Equal(emitida.NumeroVisible, publica.Valor!.SerieNumero);
        Assert.Equal(emitida.Total, publica.Valor.Total);
        Assert.Equal(1, await lectura.RegistrosFacturacion.CountAsync(r => r.FacturaId == emitida.Id));
    });

    // ─── Utilidades ──────────────────────────────────────────────────────────────

    private static UsuarioActual Propietario => new(AutonomoId, Rol.AUTONOMO);

    private static QrFacturaService Qr(string baseConfigurada = Base) =>
        new(Options.Create(new VerifactuOptions { UrlPublicaBase = baseConfigurada }));

    private static FacturacionService Servicio(ApplicationDbContext db) =>
        new(db, new GeneradorObligaciones(db, TimeProvider.System), TimeProvider.System, new RegistroFacturacionService(db),
            NullLogger<FacturacionService>.Instance);

    private static async Task<string?> CodigoDeAsync(ApplicationDbContext db, Guid facturaId) =>
        await db.Facturas.AsNoTracking().Where(f => f.Id == facturaId).Select(f => f.CodigoPublico).SingleAsync();

    private static async Task<Guid> BorradorAsync(ApplicationDbContext db)
    {
        var borrador = await Servicio(db).GuardarBorradorAsync(Propietario, AutonomoId, null,
            new GuardarFacturaRequest(ContactoId, Fecha, null, Lineas), default);
        Assert.True(borrador.EsCorrecto, borrador.Error?.Mensaje);
        return borrador.Valor!.Id;
    }

    private static async Task<FacturaResponse> EmitirNuevaAsync(ApplicationDbContext db)
    {
        var id = await BorradorAsync(db);
        var emitida = await Servicio(db).EmitirAsync(Propietario, AutonomoId, id, default);
        Assert.True(emitida.EsCorrecto, emitida.Error?.Mensaje);
        return emitida.Valor!;
    }

    private static void Sembrar(ApplicationDbContext db)
    {
        db.Usuarios.Add(new Usuario
        {
            Id = AutonomoId, Nombre = "Ramón", PrimerApellido = "García", SegundoApellido = "Pérez", Email = "a@test",
            Nif = "87654321X", Rol = Rol.AUTONOMO, DireccionFiscal = "C/ Major 12"
        });
        db.Contactos.Add(new Contacto
        {
            Id = ContactoId, AutonomoId = AutonomoId, Clasificacion = "CLIENTE", Nombre = "Cliente Cerámicas",
            Nif = "12345678Z", Direccion = "Pol. Ind. El Collet", Activo = true
        });
    }

    private static async Task<ApplicationDbContext> CrearDbAsync()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Sembrar(db);
        await db.SaveChangesAsync();
        return db;
    }

    private static async Task EnEsquemaAsync(Func<DbContextOptions<ApplicationDbContext>, Task> prueba)
    {
        var connectionString = Environment.GetEnvironmentVariable("GESTIO_TEST_POSTGRES")!;
        var schema = $"ut23_test_{Guid.NewGuid():N}";
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
                Sembrar(db);
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
}
