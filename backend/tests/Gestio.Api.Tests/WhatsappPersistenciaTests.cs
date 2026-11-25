using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Gestio.Api.Tests;

/// <summary>
/// UT27, capa 1: restricciones de base de datos del canal de WhatsApp.
/// Usan un esquema temporal propio con TODAS las migraciones aplicadas y lo borran al terminar.
/// Cada prueba viola una sola restricción y comprueba su nombre, para que falle si la restricción desaparece.
/// </summary>
public sealed class WhatsappPersistenciaTests
{
    internal static readonly Guid AutonomoA = Guid.Parse("27272727-0000-0000-0000-00000000000a");
    internal static readonly Guid AutonomoB = Guid.Parse("27272727-0000-0000-0000-00000000000b");

    // PA 3603 / requisito 5: reprocesar el mismo mensaje no puede crear un segundo documento.
    [PostgreSqlFact]
    public Task PA3603_DosDocumentosConLaMismaReferenciaDeMensaje_SeRechazan() => EnEsquemaAsync(async options =>
    {
        await using var db = new ApplicationDbContext(options);
        db.Documentos.Add(Documento(AutonomoA, "wamid.repetido"));
        await db.SaveChangesAsync();

        db.Documentos.Add(Documento(AutonomoA, "wamid.repetido"));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal(DocumentoConfiguration.IndiceReferenciaMensaje, postgres.ConstraintName);
    });

    // Las subidas web no tienen referencia: el índice filtrado no debe impedir varias con NULL.
    [PostgreSqlFact]
    public Task DocumentosSinReferencia_NoChocanEntreSi() => EnEsquemaAsync(async options =>
    {
        await using var db = new ApplicationDbContext(options);
        db.Documentos.AddRange(Documento(AutonomoA, null), Documento(AutonomoA, null));
        Assert.Equal(2, await db.SaveChangesAsync());
    });

    // PA 3605 / requisito 2: un número verificado pertenece a una sola cuenta.
    [PostgreSqlFact]
    public Task PA3605_ElMismoTelefonoVerificadoEnDosCuentas_SeRechaza() => EnEsquemaAsync(async options =>
    {
        await using var db = new ApplicationDbContext(options);
        db.NumerosWhatsapp.Add(new NumeroWhatsapp { Id = Guid.NewGuid(), UsuarioId = AutonomoA, TelefonoVerificado = "34600111222" });
        await db.SaveChangesAsync();

        db.NumerosWhatsapp.Add(new NumeroWhatsapp { Id = Guid.NewGuid(), UsuarioId = AutonomoB, TelefonoVerificado = "34600111222" });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal(NumeroWhatsappConfiguration.IndiceTelefonoVerificado, postgres.ConstraintName);
    });

    // Un número pendiente no bloquea: dos cuentas pueden intentar verificar el mismo número; solo una lo conseguirá.
    [PostgreSqlFact]
    public Task ElMismoTelefonoPendienteEnDosCuentas_SePermite() => EnEsquemaAsync(async options =>
    {
        await using var db = new ApplicationDbContext(options);
        db.NumerosWhatsapp.AddRange(
            Pendiente(AutonomoA, "34600111222", "GESTIO-AAAAAA"),
            Pendiente(AutonomoB, "34600111222", "GESTIO-BBBBBB"));
        Assert.Equal(2, await db.SaveChangesAsync());
    });

    [PostgreSqlFact]
    public Task UnAutonomoSoloTieneUnaFilaDeNumero() => EnEsquemaAsync(async options =>
    {
        await using var db = new ApplicationDbContext(options);
        db.NumerosWhatsapp.Add(new NumeroWhatsapp { Id = Guid.NewGuid(), UsuarioId = AutonomoA });
        await db.SaveChangesAsync();

        db.NumerosWhatsapp.Add(new NumeroWhatsapp { Id = Guid.NewGuid(), UsuarioId = AutonomoA });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(NumeroWhatsappConfiguration.IndiceUnoPorUsuario, postgres.ConstraintName);
    });

    [PostgreSqlFact]
    public Task DosCodigosDeVerificacionIguales_SeRechazan() => EnEsquemaAsync(async options =>
    {
        await using var db = new ApplicationDbContext(options);
        db.NumerosWhatsapp.Add(Pendiente(AutonomoA, "34600111222", "GESTIO-CCCCCC"));
        await db.SaveChangesAsync();

        db.NumerosWhatsapp.Add(Pendiente(AutonomoB, "34600333444", "GESTIO-CCCCCC"));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(NumeroWhatsappConfiguration.IndiceCodigo, postgres.ConstraintName);
    });

    // PA 3603: reclamar un mensaje es insertarlo; una segunda inserción con el mismo id falla por la clave primaria.
    [PostgreSqlFact]
    public Task PA3603_ReclamarDosVecesElMismoMensaje_Falla() => EnEsquemaAsync(async options =>
    {
        await using (var db = new ApplicationDbContext(options))
        {
            db.MensajesWhatsapp.Add(Mensaje("wamid.unico"));
            await db.SaveChangesAsync();
        }

        await using var otro = new ApplicationDbContext(options);
        otro.MensajesWhatsapp.Add(Mensaje("wamid.unico"));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => otro.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("PK_mensajes_whatsapp", postgres.ConstraintName);
    });

    // Un único formato de teléfono: sin él, «+34600111222» y «34600111222» burlarían el índice único.
    [PostgreSqlFact]
    public Task TelefonoConFormatoDistintoDeE164SinMas_SeRechaza() => EnEsquemaAsync(async options =>
    {
        await using var db = new ApplicationDbContext(options);
        db.NumerosWhatsapp.Add(new NumeroWhatsapp { Id = Guid.NewGuid(), UsuarioId = AutonomoA, TelefonoVerificado = "+34600111222" });

        var postgres = await ViolacionAsync(db);
        Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
        Assert.Equal(NumeroWhatsappConfiguration.RestriccionTelefonoVerificado, postgres.ConstraintName);
    });

    [PostgreSqlFact]
    public Task TelefonoPendienteConEspacios_SeRechaza() => EnEsquemaAsync(async options =>
    {
        await using var db = new ApplicationDbContext(options);
        db.NumerosWhatsapp.Add(Pendiente(AutonomoA, "34 600 111 222", "GESTIO-DDDDDD"));

        var postgres = await ViolacionAsync(db);
        Assert.Equal(NumeroWhatsappConfiguration.RestriccionTelefonoPendiente, postgres.ConstraintName);
    });

    // El índice único distingue mayúsculas: el código se guarda siempre en mayúsculas.
    [PostgreSqlFact]
    public Task CodigoEnMinusculas_SeRechaza() => EnEsquemaAsync(async options =>
    {
        await using var db = new ApplicationDbContext(options);
        db.NumerosWhatsapp.Add(Pendiente(AutonomoA, "34600111222", "gestio-abc123"));

        var postgres = await ViolacionAsync(db);
        Assert.Equal(NumeroWhatsappConfiguration.RestriccionCodigo, postgres.ConstraintName);
    });

    [PostgreSqlFact]
    public Task DocumentoDeWhatsappSinReferencia_SeRechaza() => EnEsquemaAsync(async options =>
    {
        await using var db = new ApplicationDbContext(options);
        var documento = Documento(AutonomoA, null);
        documento.Origen = OrigenDocumento.WHATSAPP;
        db.Documentos.Add(documento);

        var postgres = await ViolacionAsync(db);
        Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
        Assert.Equal(DocumentoConfiguration.RestriccionReferenciaSoloWhatsapp, postgres.ConstraintName);
    });

    [PostgreSqlFact]
    public Task DocumentoWebConReferencia_SeRechaza() => EnEsquemaAsync(async options =>
    {
        await using var db = new ApplicationDbContext(options);
        var documento = Documento(AutonomoA, "wamid.web");
        documento.Origen = OrigenDocumento.SUBIDA_WEB;
        db.Documentos.Add(documento);

        var postgres = await ViolacionAsync(db);
        Assert.Equal(DocumentoConfiguration.RestriccionReferenciaSoloWhatsapp, postgres.ConstraintName);
    });

    // D2: un código suelto, sin el número desde el que debe llegar, no puede existir.
    [PostgreSqlFact]
    public Task CodigoSinTelefonoPendiente_SeRechaza() => EnEsquemaAsync(async options =>
    {
        await using var db = new ApplicationDbContext(options);
        db.NumerosWhatsapp.Add(new NumeroWhatsapp
        {
            Id = Guid.NewGuid(), UsuarioId = AutonomoA, CodigoVerificacion = "GESTIO-EEEEEE", CodigoCaducaEn = DateTimeOffset.UtcNow
        });

        var postgres = await ViolacionAsync(db);
        Assert.Equal(NumeroWhatsappConfiguration.RestriccionVerificacionCompleta, postgres.ConstraintName);
    });

    [PostgreSqlFact]
    public Task CodigoSinCaducidad_SeRechaza() => EnEsquemaAsync(async options =>
    {
        await using var db = new ApplicationDbContext(options);
        db.NumerosWhatsapp.Add(new NumeroWhatsapp
        {
            Id = Guid.NewGuid(), UsuarioId = AutonomoA, TelefonoPendiente = "34600111222", CodigoVerificacion = "GESTIO-FFFFFF"
        });

        var postgres = await ViolacionAsync(db);
        Assert.Equal(NumeroWhatsappConfiguration.RestriccionVerificacionCompleta, postgres.ConstraintName);
    });

    private static NumeroWhatsapp Pendiente(Guid usuarioId, string telefono, string codigo) => new()
    {
        Id = Guid.NewGuid(),
        UsuarioId = usuarioId,
        TelefonoPendiente = telefono,
        CodigoVerificacion = codigo,
        CodigoCaducaEn = DateTimeOffset.UtcNow.AddMinutes(15)
    };

    private static async Task<PostgresException> ViolacionAsync(ApplicationDbContext db)
    {
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        return Assert.IsType<PostgresException>(error.InnerException);
    }

    private static Documento Documento(Guid usuarioId, string? referencia) => new()
    {
        Id = Guid.NewGuid(),
        UsuarioId = usuarioId,
        NombreArchivo = "ticket.jpg",
        TipoMime = "image/jpeg",
        TamanoBytes = 10,
        Origen = referencia is null ? OrigenDocumento.SUBIDA_WEB : OrigenDocumento.WHATSAPP,
        ReferenciaMensaje = referencia,
        FechaSubida = DateTimeOffset.UtcNow,
        Estado = EstadoDocumento.SUBIDO
    };

    private static MensajeWhatsappRecibido Mensaje(string id) => new()
    {
        MensajeId = id,
        Tipo = TipoMensajeWhatsapp.IMAGEN,
        Resultado = ResultadoMensajeWhatsapp.EN_CURSO,
        FechaRecepcion = DateTimeOffset.UtcNow,
        FechaActualizacion = DateTimeOffset.UtcNow
    };

    private static Usuario Autonomo(Guid id, string nif) => new()
    {
        Id = id,
        Nombre = "Autónomo",
        PrimerApellido = "UT27",
        SegundoApellido = "Prueba",
        Email = $"{id:N}@gestio.local",
        Nif = nif,
        Rol = Rol.AUTONOMO
    };

    internal static async Task EnEsquemaAsync(Func<DbContextOptions<ApplicationDbContext>, Task> prueba)
    {
        var connectionString = Environment.GetEnvironmentVariable("GESTIO_TEST_POSTGRES")!;
        var schema = $"ut27_test_{Guid.NewGuid():N}";
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
}
