using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Infrastructure.Data.Configurations;
using Gestio.Api.Services.Verifactu;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Gestio.Api.Tests;

/// <summary>
/// UT23 (PB-23), capa 1: el código público que codifica el QR y sus garantías en la BD.
/// Las pruebas [PostgreSqlFact] usan un esquema temporal propio, aplican TODAS las migraciones y lo borran al terminar.
/// Cada prueba de restricción viola UNA sola restricción y comprueba su nombre, para fallar si la restricción desaparece.
/// </summary>
public sealed class CodigoPublicoFacturaTests
{
    private static readonly Guid AutonomoA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // ─── Generador (req. 2: identificador no predecible, sin datos fiscales) ──────

    [Fact]
    public void Generar_Devuelve22CaracteresDelAlfabetoBase62()
    {
        var codigo = CodigoPublicoFactura.Generar();

        Assert.Equal(22, codigo.Length);
        // Comprobación independiente del alfabeto del código de producción: regex escrita a mano.
        Assert.Matches("^[A-Za-z0-9]{22}$", codigo);
        Assert.True(CodigoPublicoFactura.EsFormatoValido(codigo));
    }

    [Fact]
    public void Req2_DiezMilCodigosGenerados_SonTodosDistintos()
    {
        var codigos = Enumerable.Range(0, 10_000).Select(_ => CodigoPublicoFactura.Generar()).ToList();

        Assert.Equal(codigos.Count, codigos.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Generar_UsaLos62CaracteresDelAlfabeto()
    {
        // 220.000 caracteres: si el generador usara solo una parte del alfabeto (p. ej. un error de rango), faltarían letras.
        var usados = Enumerable.Range(0, 10_000).SelectMany(_ => CodigoPublicoFactura.Generar()).ToHashSet();

        Assert.Equal(62, usados.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA")]       // 21: enlace truncado
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAA")]     // 23: carácter añadido
    [InlineData("AAAAAAAAAAAAAAAAAAAA-A")]      // guion (base64url)
    [InlineData("AAAAAAAAAAAAAAAAAAAA_A")]      // guion bajo (base64url)
    [InlineData("AAAAAAAAAAAAAAAAAAAAñA")]      // fuera de ASCII
    [InlineData("AAAAAAAAAAAAAAAAAAAA A")]      // espacio
    [InlineData("AAAAAAAAAA%2FAAAAAAAAA")]      // codificación URL
    [InlineData("../../api/facturas/xxxx")]     // intento de ruta
    public void Req7_EsFormatoValido_RechazaEnlacesMalformados(string? codigo)
    {
        Assert.False(CodigoPublicoFactura.EsFormatoValido(codigo));
    }

    [Fact]
    public void EsFormatoValido_AceptaMayusculasMinusculasYDigitos()
    {
        Assert.True(CodigoPublicoFactura.EsFormatoValido("Vb4nW8cQe1Ts6Yh2AbCd09"));
    }

    // ─── PostgreSQL real: índice único, «solo emitidas» y trigger de inmutabilidad ─

    [PostgreSqlFact]
    public Task PA1608_Bd_DosFacturasNoPuedenCompartirCodigo() => EnEsquemaAsync(async options =>
    {
        var codigo = CodigoPublicoFactura.Generar();
        await InsertarAsync(options, Emitida(1, codigo));

        var error = await InsertarYCapturarAsync(options, Emitida(2, codigo));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal(FacturaConfiguration.IndiceCodigoPublico, error.ConstraintName);
    });

    [PostgreSqlFact]
    public Task D4_VariasFacturasEmitidasSinCodigo_PuedenConvivir() => EnEsquemaAsync(async options =>
    {
        // Facturas emitidas antes de UT23: sin código. El índice filtrado no las trata como duplicadas.
        await InsertarAsync(options, Emitida(1, null));
        await InsertarAsync(options, Emitida(2, null));

        await using var db = new ApplicationDbContext(options);
        Assert.Equal(2, await db.Facturas.CountAsync(f => f.CodigoPublico == null));
    });

    [PostgreSqlFact]
    public Task Req5_Bd_UnBorradorNoPuedeTenerCodigoAlInsertar() => EnEsquemaAsync(async options =>
    {
        var borrador = Emitida(null, CodigoPublicoFactura.Generar());
        borrador.Estado = EstadoFactura.BORRADOR;

        var error = await InsertarYCapturarAsync(options, borrador);

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(FacturaConfiguration.RestriccionCodigoSoloEmitidas, error.ConstraintName);
    });

    [PostgreSqlFact]
    public Task Req5_Bd_UnaFacturaConCodigoNoPuedeVolverABorrador() => EnEsquemaAsync(async options =>
    {
        var factura = Emitida(1, CodigoPublicoFactura.Generar());
        await InsertarAsync(options, factura);
        await using var db = new ApplicationDbContext(options);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Facturas.Where(f => f.Id == factura.Id).ExecuteUpdateAsync(s => s.SetProperty(f => f.Estado, EstadoFactura.BORRADOR)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(FacturaConfiguration.RestriccionCodigoSoloEmitidas, error.ConstraintName);
    });

    [Fact]
    public void Req5_ElCheckDeLaBdAsumeQueEmitidaVale2()
    {
        // El CHECK de la migración tiene el 2 escrito a mano: si alguien renumera el enum, esta prueba avisa.
        Assert.Equal(2, (int)EstadoFactura.EMITIDA);
    }

    [PostgreSqlFact]
    public Task Emision_PasarDeBorradorAEmitidaAsignandoCodigo_SePermite() => EnEsquemaAsync(async options =>
    {
        // Es el UPDATE que hará la emisión (capa 2): el trigger debe dejar pasar NULL -> código.
        var factura = Emitida(null, null);
        factura.Estado = EstadoFactura.BORRADOR;
        await InsertarAsync(options, factura);
        var codigo = CodigoPublicoFactura.Generar();

        await using (var db = new ApplicationDbContext(options))
        {
            var f = await db.Facturas.SingleAsync(x => x.Id == factura.Id);
            f.Estado = EstadoFactura.EMITIDA;
            f.Numero = 1;
            f.CodigoPublico = codigo;
            await db.SaveChangesAsync();
        }

        await using var lectura = new ApplicationDbContext(options);
        Assert.Equal(codigo, (await lectura.Facturas.AsNoTracking().SingleAsync(x => x.Id == factura.Id)).CodigoPublico);
    });

    [PostgreSqlFact]
    public Task D1_Trigger_ImpideCambiarElCodigoConSaveChanges() => EnEsquemaAsync(async options =>
    {
        var original = CodigoPublicoFactura.Generar();
        var factura = Emitida(1, original);
        await InsertarAsync(options, factura);

        await using var db = new ApplicationDbContext(options);
        var f = await db.Facturas.SingleAsync(x => x.Id == factura.Id);
        f.CodigoPublico = CodigoPublicoFactura.Generar();

        var error = Assert.IsType<PostgresException>((await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync())).InnerException);
        Assert.Equal(PostgresErrorCodes.RestrictViolation, error.SqlState);
        await AssertCodigoAsync(options, factura.Id, original);
    });

    [PostgreSqlFact]
    public Task D1_Trigger_ImpideBorrarElCodigoPorSql() => EnEsquemaAsync(async options =>
    {
        var original = CodigoPublicoFactura.Generar();
        var factura = Emitida(1, original);
        await InsertarAsync(options, factura);
        await using var db = new ApplicationDbContext(options);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE facturas SET \"CodigoPublico\" = NULL WHERE \"Id\" = {0}", factura.Id));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, error.SqlState);
        await AssertCodigoAsync(options, factura.Id, original);
    });

    [PostgreSqlFact]
    public Task D1_Trigger_ImpideCambiarElCodigoConExecuteUpdate() => EnEsquemaAsync(async options =>
    {
        var original = CodigoPublicoFactura.Generar();
        var factura = Emitida(1, original);
        await InsertarAsync(options, factura);
        await using var db = new ApplicationDbContext(options);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Facturas.Where(f => f.Id == factura.Id).ExecuteUpdateAsync(s => s.SetProperty(f => f.CodigoPublico, "B".PadRight(22, 'B'))));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, error.SqlState);
        await AssertCodigoAsync(options, factura.Id, original);
    });

    [PostgreSqlFact]
    public Task Trigger_NoBloqueaOtrasActualizacionesDeLaFactura() => EnEsquemaAsync(async options =>
    {
        // El trigger solo vigila "CodigoPublico": otras columnas (p. ej. Actualizada) se siguen pudiendo escribir.
        var original = CodigoPublicoFactura.Generar();
        var factura = Emitida(1, original);
        await InsertarAsync(options, factura);
        await using var db = new ApplicationDbContext(options);

        var filas = await db.Facturas.Where(f => f.Id == factura.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Actualizada, DateTimeOffset.UtcNow));

        Assert.Equal(1, filas);
        await AssertCodigoAsync(options, factura.Id, original);
    });

    // ─── Utilidades ──────────────────────────────────────────────────────────────

    private static Factura Emitida(int? numero, string? codigo)
    {
        var ahora = DateTimeOffset.UtcNow;
        return new Factura
        {
            Id = Guid.NewGuid(), AutonomoId = AutonomoA, Serie = "F-2026", Numero = numero, Fecha = new DateOnly(2026, 6, 16),
            Estado = EstadoFactura.EMITIDA, Creada = ahora, Actualizada = ahora, Emitida = ahora, CodigoPublico = codigo
        };
    }

    private static async Task InsertarAsync(DbContextOptions<ApplicationDbContext> options, Factura factura)
    {
        await using var db = new ApplicationDbContext(options);
        db.Facturas.Add(factura);
        await db.SaveChangesAsync();
    }

    private static async Task<PostgresException> InsertarYCapturarAsync(DbContextOptions<ApplicationDbContext> options, Factura factura)
    {
        await using var db = new ApplicationDbContext(options);
        db.Facturas.Add(factura);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        return Assert.IsType<PostgresException>(error.InnerException);
    }

    private static async Task AssertCodigoAsync(DbContextOptions<ApplicationDbContext> options, Guid facturaId, string esperado)
    {
        await using var db = new ApplicationDbContext(options);
        Assert.Equal(esperado, (await db.Facturas.AsNoTracking().SingleAsync(f => f.Id == facturaId)).CodigoPublico);
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
                db.Usuarios.Add(new Usuario
                {
                    Id = AutonomoA, Nombre = "Autónomo", PrimerApellido = "Uno", SegundoApellido = "Dos",
                    Email = $"{AutonomoA:N}@test", Nif = "52341234R", Rol = Rol.AUTONOMO, DireccionFiscal = "Calle Emisor 1"
                });
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
