using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Infrastructure.Data.Configurations;
using Gestio.Api.Services.Verifactu;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Gestio.Api.Tests;

/// <summary>
/// UT22, capa 2: persistencia de los registros de facturación.
/// Las pruebas [PostgreSqlFact] usan un esquema temporal propio, aplican TODAS las migraciones y lo borran al terminar.
/// Cada prueba de restricción construye una fila que viola UNA sola restricción y comprueba su nombre,
/// de modo que la prueba falla si esa restricción desaparece. Excepción documentada:
/// Requisito3_ElRegistroUnoNoPuedeTenerAnterior (no se puede aislar; ver la prueba).
/// </summary>
public sealed class RegistroFacturacionPersistenciaTests
{
    private static readonly Guid AutonomoA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AutonomoB = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // ─── Guarda de la aplicación (SaveChanges), sin BD real ───────────────────────

    [Fact]
    public async Task PA1598_GuardaEF_PermiteCrearRegistros()
    {
        await using var db = InMemory();
        db.RegistrosFacturacion.Add(Registro(AutonomoA, 1, null, Guid.NewGuid()));
        Assert.Equal(1, await db.SaveChangesAsync());
    }

    [Fact]
    public async Task PA1598_GuardaEF_ImpideModificarUnRegistro()
    {
        await using var db = InMemory();
        var registro = Registro(AutonomoA, 1, null, Guid.NewGuid());
        db.RegistrosFacturacion.Add(registro);
        await db.SaveChangesAsync();

        registro.ImporteTotal += 1000m;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("modificar", error.Message);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges()); // también por la vía síncrona
    }

    [Fact]
    public async Task PA1598_GuardaEF_ImpideEliminarUnRegistro()
    {
        await using var db = InMemory();
        var registro = Registro(AutonomoA, 1, null, Guid.NewGuid());
        db.RegistrosFacturacion.Add(registro);
        await db.SaveChangesAsync();

        db.RegistrosFacturacion.Remove(registro);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("eliminar", error.Message);
    }

    [Fact]
    public async Task PA1598_GuardaEF_NoGuardaNadaDelLoteSiHayUnRegistroAlterado()
    {
        var nombre = Guid.NewGuid().ToString();
        await using var db = InMemory(nombre);
        var registro = Registro(AutonomoA, 1, null, Guid.NewGuid());
        db.RegistrosFacturacion.Add(registro);
        await db.SaveChangesAsync();

        registro.ImporteTotal += 1m;
        db.RegistrosFacturacion.Add(Registro(AutonomoA, 2, registro, Guid.NewGuid()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await using var otro = InMemory(nombre);
        Assert.Equal(1, await otro.RegistrosFacturacion.CountAsync());
    }

    // ─── PostgreSQL real: trigger de inmutabilidad ────────────────────────────────

    [PostgreSqlFact]
    public Task PA1598_Trigger_ImpideUpdatePorSql() => EnEsquemaAsync(async options =>
    {
        var (r1, _) = await CadenaDeDosAsync(options);
        await using var db = new ApplicationDbContext(options);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE registros_facturacion SET \"ImporteTotal\" = 1 WHERE \"Id\" = {0}", r1.Id));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, error.SqlState);
        Assert.Equal(178.75m, (await db.RegistrosFacturacion.AsNoTracking().SingleAsync(r => r.Id == r1.Id)).ImporteTotal);
    });

    [PostgreSqlFact]
    public Task PA1598_Trigger_ImpideExecuteUpdateQueNoPasaPorSaveChanges() => EnEsquemaAsync(async options =>
    {
        var (r1, _) = await CadenaDeDosAsync(options);
        await using var db = new ApplicationDbContext(options);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.RegistrosFacturacion.Where(r => r.Id == r1.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.Huella, new string('0', 64))));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, error.SqlState);
    });

    [PostgreSqlFact]
    public Task PA1598_Trigger_ImpideDeletePorSqlYExecuteDelete() => EnEsquemaAsync(async options =>
    {
        var (_, r2) = await CadenaDeDosAsync(options);
        await using var db = new ApplicationDbContext(options);

        var porSql = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("DELETE FROM registros_facturacion WHERE \"Id\" = {0}", r2.Id));
        var porEf = await Assert.ThrowsAsync<PostgresException>(() =>
            db.RegistrosFacturacion.Where(r => r.Id == r2.Id).ExecuteDeleteAsync());

        Assert.Equal(PostgresErrorCodes.RestrictViolation, porSql.SqlState);
        Assert.Equal(PostgresErrorCodes.RestrictViolation, porEf.SqlState);
        Assert.Equal(2, await db.RegistrosFacturacion.CountAsync());
    });

    [PostgreSqlFact]
    public Task PA1598_Trigger_ImpideTruncate() => EnEsquemaAsync(async options =>
    {
        await CadenaDeDosAsync(options);
        await using var db = new ApplicationDbContext(options);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("TRUNCATE registros_facturacion"));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, error.SqlState);
        Assert.Equal(2, await db.RegistrosFacturacion.CountAsync());
    });

    // ─── PostgreSQL real: restricciones de la cadena ─────────────────────────────

    [PostgreSqlFact]
    public Task PA1589_UnaFacturaSoloTieneUnRegistroDeAlta() => EnEsquemaAsync(async options =>
    {
        var (r1, r2) = await CadenaDeDosAsync(options);
        // #3 correcto en todo salvo que repite la factura del #1.
        var violacion = await InsertarYCapturarAsync(options, Registro(AutonomoA, 3, r2, r1.FacturaId));
        Assert.Equal(RegistroFacturacionConfiguration.IndiceUnaAltaPorFactura, violacion.ConstraintName);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, violacion.SqlState);
    });

    [PostgreSqlFact]
    public Task PA1590_UnRegistroSoloTieneUnSucesor_LaCadenaNoSeDivide() => EnEsquemaAsync(async options =>
    {
        var (r1, _) = await CadenaDeDosAsync(options);
        var factura = await NuevaFacturaAsync(options, AutonomoA);
        // #3 con factura y posición nuevas, pero apuntando al #1, que ya tiene sucesor (#2).
        var violacion = await InsertarYCapturarAsync(options, Registro(AutonomoA, 3, r1, factura));
        Assert.Equal(RegistroFacturacionConfiguration.IndiceUnSucesor, violacion.ConstraintName);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, violacion.SqlState);
    });

    [PostgreSqlFact]
    public Task PA1590_DosRegistrosNoOcupanLaMismaPosicion() => EnEsquemaAsync(async options =>
    {
        var (_, r2) = await CadenaDeDosAsync(options);
        var factura = await NuevaFacturaAsync(options, AutonomoA);
        // Apunta al #2 (que no tiene sucesor) pero repite la posición 2.
        var violacion = await InsertarYCapturarAsync(options, Registro(AutonomoA, 2, r2, factura));
        Assert.Equal(RegistroFacturacionConfiguration.IndiceSecuencia, violacion.ConstraintName);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, violacion.SqlState);
    });

    [PostgreSqlFact]
    public Task Requisito3_SoloElRegistroUnoEsInicioDeCadena() => EnEsquemaAsync(async options =>
    {
        await CadenaDeDosAsync(options);
        var factura = await NuevaFacturaAsync(options, AutonomoA);

        // Posición 3 sin anterior: sería un segundo inicio de cadena.
        var sinAnterior = await InsertarYCapturarAsync(options, Registro(AutonomoA, 3, null, factura));

        Assert.Equal(RegistroFacturacionConfiguration.RestriccionInicioCadena, sinAnterior.ConstraintName);
        Assert.Equal(PostgresErrorCodes.CheckViolation, sinAnterior.SqlState);
    });

    [PostgreSqlFact]
    public Task Requisito3_ElRegistroUnoNoPuedeTenerAnterior() => EnEsquemaAsync(async options =>
    {
        var (r1, _) = await CadenaDeDosAsync(options);
        var facturaB = await NuevaFacturaAsync(options, AutonomoB);

        // Inicio de la cadena de B que apunta a un registro: no puede tener anterior.
        // No se puede aislar: cualquier anterior posible es de otro autónomo (FK compuesta) o repetiría la posición 1.
        // La prueba se apoya en que PostgreSQL evalúa las CHECK antes que los índices únicos y las FK; si la CHECK
        // desapareciera, saltaría la FK con otro nombre y esta prueba fallaría igualmente.
        var inicioConAnterior = Registro(AutonomoB, 1, null, facturaB);
        inicioConAnterior.AnteriorId = r1.Id;
        inicioConAnterior.HuellaAnterior = r1.Huella;
        var violacion = await InsertarYCapturarAsync(options, inicioConAnterior);

        Assert.Equal(RegistroFacturacionConfiguration.RestriccionInicioCadena, violacion.ConstraintName);
        Assert.Equal(PostgresErrorCodes.CheckViolation, violacion.SqlState);
    });

    [PostgreSqlFact]
    public Task Requisito3_UnSegundoInicioDeCadenaDelMismoAutonomo_SeRechaza() => EnEsquemaAsync(async options =>
    {
        await CadenaDeDosAsync(options);
        var factura = await NuevaFacturaAsync(options, AutonomoA);

        // Lo que pasaría con dos primeras emisiones simultáneas: dos #1 sin anterior. La CHECK lo acepta,
        // así que es el índice (AutonomoId, Secuencia) el que impide la segunda cadena.
        var violacion = await InsertarYCapturarAsync(options, Registro(AutonomoA, 1, null, factura));

        Assert.Equal(RegistroFacturacionConfiguration.IndiceSecuencia, violacion.ConstraintName);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, violacion.SqlState);
    });

    [PostgreSqlFact]
    public Task PA1592_UnRegistroNoPuedeEngancharseALaCadenaDeOtroAutonomo() => EnEsquemaAsync(async options =>
    {
        var (_, r2) = await CadenaDeDosAsync(options);
        var facturaB = await NuevaFacturaAsync(options, AutonomoB);

        // Registro de B que apunta al último de A: ocuparía el único hueco de sucesor de A y bloquearía su cadena.
        var violacion = await InsertarYCapturarAsync(options, Registro(AutonomoB, 2, r2, facturaB));

        Assert.Equal(RegistroFacturacionConfiguration.FkAnteriorMismoAutonomo, violacion.ConstraintName);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, violacion.SqlState);
    });

    [PostgreSqlFact]
    public Task HuellaAnterior_SoloYSiempreQueHayRegistroAnterior() => EnEsquemaAsync(async options =>
    {
        var (_, r2) = await CadenaDeDosAsync(options);
        var factura = await NuevaFacturaAsync(options, AutonomoA);
        var sinHuellaAnterior = Registro(AutonomoA, 3, r2, factura);
        sinHuellaAnterior.HuellaAnterior = null;

        var violacion = await InsertarYCapturarAsync(options, sinHuellaAnterior);

        Assert.Equal(RegistroFacturacionConfiguration.RestriccionHuellaAnterior, violacion.ConstraintName);
        Assert.Equal(PostgresErrorCodes.CheckViolation, violacion.SqlState);
    });

    [PostgreSqlFact]
    public Task FkRestrict_NoSePuedeBorrarUnaFacturaConRegistro() => EnEsquemaAsync(async options =>
    {
        var (r1, _) = await CadenaDeDosAsync(options);
        await using var db = new ApplicationDbContext(options);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("DELETE FROM facturas WHERE \"Id\" = {0}", r1.FacturaId));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal(RegistroFacturacionConfiguration.FkFactura, error.ConstraintName);
    });

    [Fact]
    public void Modelo_CoincideConLaUltimaMigracion()
    {
        // Detecta un snapshot o una migración desincronizados con la configuración (no necesita conexión).
        using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql("Host=localhost;Database=sin_conexion").Options);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [PostgreSqlFact]
    public Task PA1592_CadaAutonomoTieneSuPropiaCadena() => EnEsquemaAsync(async options =>
    {
        await CadenaDeDosAsync(options);
        var facturaB = await NuevaFacturaAsync(options, AutonomoB);
        await using (var db = new ApplicationDbContext(options))
        {
            // B empieza su propia cadena en la posición 1 aunque A ya tenga un #1.
            db.RegistrosFacturacion.Add(Registro(AutonomoB, 1, null, facturaB));
            await db.SaveChangesAsync();
        }

        await using var lectura = new ApplicationDbContext(options);
        var porAutonomo = await lectura.RegistrosFacturacion.GroupBy(r => r.AutonomoId)
            .Select(g => new { g.Key, Total = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Total);
        Assert.Equal(2, porAutonomo[AutonomoA]);
        Assert.Equal(1, porAutonomo[AutonomoB]);
    });

    [PostgreSqlFact]
    public Task Persistencia_LaCadenaLeidaDeLaBdSigueSiendoCoherente() => EnEsquemaAsync(async options =>
    {
        await CadenaDeDosAsync(options);
        await using var db = new ApplicationDbContext(options);

        var registros = await db.RegistrosFacturacion.AsNoTracking().Where(r => r.AutonomoId == AutonomoA).ToListAsync();
        var resultado = VerificadorCadena.Verificar(registros);

        // Comprueba que importes, fechas, null de HuellaAnterior y timestamptz sobreviven al viaje a PostgreSQL.
        Assert.True(resultado.Coherente, resultado.Incoherencia?.Motivo);
        Assert.Equal(2, resultado.Revisados);
    });

    // ─── Utilidades ──────────────────────────────────────────────────────────────

    private static ApplicationDbContext InMemory(string? nombre = null) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(nombre ?? Guid.NewGuid().ToString()).Options);

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
                db.Usuarios.AddRange(Autonomo(AutonomoA, "52341234R"), Autonomo(AutonomoB, "87654321X"));
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

    private static async Task<(RegistroFacturacion R1, RegistroFacturacion R2)> CadenaDeDosAsync(DbContextOptions<ApplicationDbContext> options)
    {
        var f1 = await NuevaFacturaAsync(options, AutonomoA);
        var f2 = await NuevaFacturaAsync(options, AutonomoA);
        var r1 = Registro(AutonomoA, 1, null, f1);
        var r2 = Registro(AutonomoA, 2, r1, f2);
        await using var db = new ApplicationDbContext(options);
        db.RegistrosFacturacion.AddRange(r1, r2);
        await db.SaveChangesAsync();
        return (r1, r2);
    }

    private static async Task<Guid> NuevaFacturaAsync(DbContextOptions<ApplicationDbContext> options, Guid autonomoId)
    {
        await using var db = new ApplicationDbContext(options);
        var ahora = DateTimeOffset.UtcNow;
        var factura = new Factura
        {
            Id = Guid.NewGuid(), AutonomoId = autonomoId, Serie = "F-2026", Fecha = new DateOnly(2026, 6, 16),
            Estado = EstadoFactura.BORRADOR, Creada = ahora, Actualizada = ahora
        };
        db.Facturas.Add(factura);
        await db.SaveChangesAsync();
        return factura.Id;
    }

    private static async Task<PostgresException> InsertarYCapturarAsync(DbContextOptions<ApplicationDbContext> options, RegistroFacturacion registro)
    {
        await using var db = new ApplicationDbContext(options);
        db.RegistrosFacturacion.Add(registro);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        return Assert.IsType<PostgresException>(error.InnerException);
    }

    private static Usuario Autonomo(Guid id, string nif) => new()
    {
        Id = id, Nombre = "Autónomo", PrimerApellido = "Uno", SegundoApellido = "Dos",
        Email = $"{id:N}@test", Nif = nif, Rol = Rol.AUTONOMO, DireccionFiscal = "Calle Emisor 1"
    };

    private static RegistroFacturacion Registro(Guid autonomoId, int secuencia, RegistroFacturacion? anterior, Guid facturaId)
    {
        var instante = new DateTimeOffset(2026, 6, 16, 13, 24, 1, TimeSpan.Zero).AddDays(secuencia);
        var registro = new RegistroFacturacion
        {
            Id = Guid.NewGuid(), AutonomoId = autonomoId, FacturaId = facturaId, Secuencia = secuencia,
            Tipo = TipoRegistroFacturacion.ALTA, IdEmisorFactura = "52341234R", NumSerieFactura = $"F-2026-{secuencia:D4}",
            FechaExpedicionFactura = new DateOnly(2026, 6, 16), TipoFactura = HuellaVerifactu.TipoFacturaCompleta,
            CuotaTotal = 16.25m, ImporteTotal = 178.75m,
            Generado = HuellaVerifactu.TruncarASegundos(instante),
            FechaHoraHusoGenRegistro = HuellaVerifactu.FormatearFechaHoraHuso(instante),
            AnteriorId = anterior?.Id, HuellaAnterior = anterior?.Huella, Huella = ""
        };
        registro.Huella = HuellaVerifactu.Calcular(DatosHuellaAlta.De(registro));
        return registro;
    }
}
