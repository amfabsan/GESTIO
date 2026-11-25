using Gestio.Api.Common;
using Gestio.Api.Controllers;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Services.Whatsapp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gestio.Api.Tests;

/// <summary>UT27, capa 2: vinculación y verificación del número de WhatsApp (PA 3605, requisito 2, D2 y D6).</summary>
public sealed class WhatsappVerificacionTests
{
    private static readonly UsuarioActual Ramon = new(DbInitializer.RamonId, Rol.AUTONOMO);
    private static readonly UsuarioActual Pedro = new(DbInitializer.PedroId, Rol.GESTOR);
    private static readonly Guid LuciaId = Guid.Parse("27272727-0000-0000-0000-0000000000c1");
    private static readonly UsuarioActual Lucia = new(LuciaId, Rol.AUTONOMO);

    private const string MovilRamon = "34600111222";

    // ─── Formato del teléfono y del código ───────────────────────────────────────

    [Theory]
    [InlineData("+34 600 111 222", "34600111222")]
    [InlineData("0034600111222", "34600111222")]
    [InlineData("34-600-111-222", "34600111222")]
    [InlineData("600111222", "34600111222")]
    [InlineData("+1 (555) 012-3456", "15550123456")]
    public void Telefono_SeNormalizaAE164SinMas(string entrada, string esperado) =>
        Assert.Equal(esperado, TelefonoWhatsapp.Normalizar(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1234")]
    [InlineData("912345678")]          // sin prefijo y no es un móvil español: no se adivina el país
    [InlineData("447700900123")]       // británico escrito sin «+»: ambiguo, se pide el prefijo
    [InlineData("34+600111222")]
    [InlineData("+0 600 111 222")]
    [InlineData("+34 600 111 222 333 444")]
    public void Telefono_NoValido_DevuelveNull(string? entrada) => Assert.Null(TelefonoWhatsapp.Normalizar(entrada));

    [Theory]
    [InlineData("34600111222", "+••••••••222")]
    [InlineData("15550123456", "+••••••••456")]
    [InlineData("351912345678", "+•••••••••678")]
    public void Telefono_SeEnmascaraDejandoSoloLasTresUltimasCifras(string telefono, string esperado) =>
        Assert.Equal(esperado, TelefonoWhatsapp.Enmascarar(telefono));

    [Theory]
    [InlineData("GESTIO-ABC234", "GESTIO-ABC234")]
    [InlineData("  gestio-abc234 ", "GESTIO-ABC234")]
    [InlineData("GESTIO ABC 234", "GESTIO-ABC234")]
    [InlineData("gestioabc234", "GESTIO-ABC234")]
    public void Codigo_SeLeeSinDistinguirMayusculasNiEspacios(string mensaje, string esperado) =>
        Assert.Equal(esperado, CodigoVerificacionWhatsapp.Leer(mensaje));

    [Theory]
    [InlineData("Hola, mi código es GESTIO-ABC234")]
    [InlineData("GESTIO-ABC23")]
    [InlineData("GESTIO-ABC2345")]
    [InlineData("hola")]
    [InlineData(null)]
    public void Codigo_ConTextoAlrededorOMalFormado_NoEsCodigo(string? mensaje) =>
        Assert.Null(CodigoVerificacionWhatsapp.Leer(mensaje));

    [Fact]
    public void Codigo_GeneradoCumpleElFormatoGuardadoYNoUsaSimbolosAmbiguos()
    {
        for (var i = 0; i < 200; i++)
        {
            var codigo = CodigoVerificacionWhatsapp.Generar();
            Assert.Matches("^GESTIO-[A-HJ-NP-Z2-9]{6}$", codigo);
        }
    }

    // ─── Iniciar la verificación ─────────────────────────────────────────────────

    [Fact]
    public async Task PA3605_Iniciar_GuardaElNumeroPendienteConUnCodigoQueCaducaA15Minutos()
    {
        var (context, reloj, servicio) = await PrepararAsync(nameof(PA3605_Iniciar_GuardaElNumeroPendienteConUnCodigoQueCaducaA15Minutos));

        var resultado = await servicio.IniciarAsync(Ramon, "+34 600 111 222", CancellationToken.None);

        Assert.True(resultado.EsCorrecto);
        var estado = resultado.Valor!;
        Assert.Null(estado.TelefonoVerificado);
        Assert.Equal(MovilRamon, estado.TelefonoPendiente);
        Assert.Matches("^GESTIO-[A-Z0-9]{6}$", estado.CodigoVerificacion!);
        Assert.Equal(reloj.Ahora.AddMinutes(15), estado.CodigoCaducaEn);
        Assert.Equal("+1 555 000 0000", estado.NumeroCanal);
    }

    [Fact]
    public async Task Iniciar_ComoGestor_SeProhibe()
    {
        var (_, _, servicio) = await PrepararAsync(nameof(Iniciar_ComoGestor_SeProhibe));

        var resultado = await servicio.IniciarAsync(Pedro, "+34 600 111 222", CancellationToken.None);

        Assert.Equal(WhatsappErrores.SoloAutonomo, resultado.Error);
    }

    [Fact]
    public async Task Iniciar_ConElCanalDeshabilitado_NoGeneraCodigo()
    {
        var (context, _, servicio) = await PrepararAsync(nameof(Iniciar_ConElCanalDeshabilitado_NoGeneraCodigo), habilitado: false);

        var resultado = await servicio.IniciarAsync(Ramon, MovilRamon, CancellationToken.None);

        Assert.Equal(WhatsappErrores.CanalNoDisponible, resultado.Error);
        Assert.False(await context.NumerosWhatsapp.AnyAsync());
        // Consultar sí se puede: la web muestra que el canal no está disponible.
        Assert.False((await servicio.ObtenerAsync(Ramon, CancellationToken.None)).Valor!.CanalHabilitado);
    }

    [Fact]
    public async Task Obtener_ComoGestor_SeProhibe()
    {
        var (_, _, servicio) = await PrepararAsync(nameof(Obtener_ComoGestor_SeProhibe));
        Assert.Equal(WhatsappErrores.SoloAutonomo, (await servicio.ObtenerAsync(Pedro, CancellationToken.None)).Error);
    }

    [Fact]
    public async Task Iniciar_ConTelefonoNoValido_NoGuardaNada()
    {
        var (context, _, servicio) = await PrepararAsync(nameof(Iniciar_ConTelefonoNoValido_NoGuardaNada));

        var resultado = await servicio.IniciarAsync(Ramon, "no es un número", CancellationToken.None);

        Assert.Equal(WhatsappErrores.TelefonoNoValido, resultado.Error);
        Assert.False(await context.NumerosWhatsapp.AnyAsync());
    }

    [Fact]
    public async Task Iniciar_ConElNumeroYaVerificado_EsConflicto()
    {
        var (_, _, servicio) = await PrepararAsync(nameof(Iniciar_ConElNumeroYaVerificado_EsConflicto));
        await VerificarAsync(servicio, Ramon, MovilRamon);

        var resultado = await servicio.IniciarAsync(Ramon, MovilRamon, CancellationToken.None);

        Assert.Equal(WhatsappErrores.YaVinculado, resultado.Error);
    }

    // ─── Verificar desde un mensaje ─────────────────────────────────────────────

    [Fact]
    public async Task PA3605_CodigoVigenteDesdeElNumeroEscrito_VerificaYBorraLaVerificacionPendiente()
    {
        var (context, reloj, servicio) = await PrepararAsync(nameof(PA3605_CodigoVigenteDesdeElNumeroEscrito_VerificaYBorraLaVerificacionPendiente));
        var codigo = (await servicio.IniciarAsync(Ramon, MovilRamon, CancellationToken.None)).Valor!.CodigoVerificacion!;

        var resultado = await servicio.VerificarDesdeMensajeAsync(MovilRamon, codigo.ToLowerInvariant(), CancellationToken.None);

        Assert.Equal(ResultadoVerificacionWhatsapp.VERIFICADO, resultado);
        var numero = await context.NumerosWhatsapp.AsNoTracking().SingleAsync();
        Assert.Equal(MovilRamon, numero.TelefonoVerificado);
        Assert.Equal(reloj.Ahora, numero.FechaVerificacion);
        Assert.Null(numero.TelefonoPendiente);
        Assert.Null(numero.CodigoVerificacion);
        Assert.Null(numero.CodigoCaducaEn);
    }

    // D2: quien vea el código en pantalla no puede usarlo desde otro móvil.
    [Fact]
    public async Task PA3605_CodigoEnviadoDesdeOtroNumero_SeRechaza()
    {
        var (context, _, servicio) = await PrepararAsync(nameof(PA3605_CodigoEnviadoDesdeOtroNumero_SeRechaza));
        var codigo = (await servicio.IniciarAsync(Ramon, MovilRamon, CancellationToken.None)).Valor!.CodigoVerificacion!;

        var resultado = await servicio.VerificarDesdeMensajeAsync("34699999999", codigo, CancellationToken.None);

        Assert.Equal(ResultadoVerificacionWhatsapp.RECHAZADO, resultado);
        Assert.Null((await context.NumerosWhatsapp.AsNoTracking().SingleAsync()).TelefonoVerificado);
    }

    [Fact]
    public async Task PA3605_CodigoCaducado_SeRechaza()
    {
        var (context, reloj, servicio) = await PrepararAsync(nameof(PA3605_CodigoCaducado_SeRechaza));
        var codigo = (await servicio.IniciarAsync(Ramon, MovilRamon, CancellationToken.None)).Valor!.CodigoVerificacion!;

        reloj.Avanzar(TimeSpan.FromMinutes(15));
        var resultado = await servicio.VerificarDesdeMensajeAsync(MovilRamon, codigo, CancellationToken.None);

        Assert.Equal(ResultadoVerificacionWhatsapp.RECHAZADO, resultado);
        Assert.Null((await context.NumerosWhatsapp.AsNoTracking().SingleAsync()).TelefonoVerificado);
        // Caducado, el código ya no se muestra en la web.
        Assert.Null((await servicio.ObtenerAsync(Ramon, CancellationToken.None)).Valor!.CodigoVerificacion);
    }

    [Fact]
    public async Task PA3605_CodigoInexistente_SeRechazaIgualQueUnoCaducado()
    {
        var (_, _, servicio) = await PrepararAsync(nameof(PA3605_CodigoInexistente_SeRechazaIgualQueUnoCaducado));
        await servicio.IniciarAsync(Ramon, MovilRamon, CancellationToken.None);

        var resultado = await servicio.VerificarDesdeMensajeAsync(MovilRamon, "GESTIO-ZZZZZZ", CancellationToken.None);

        Assert.Equal(ResultadoVerificacionWhatsapp.RECHAZADO, resultado);
    }

    // Requisito 2: «asociado de forma inequívoca». El número ya verificado en otra cuenta no se puede verificar
    // en una segunda, y el rechazo es el mismo que el de un código erróneo.
    [Fact]
    public async Task PA3605_NumeroYaVerificadoEnOtraCuenta_SeRechazaSinQuitarseloALaPrimera()
    {
        var (context, _, servicio) = await PrepararAsync(nameof(PA3605_NumeroYaVerificadoEnOtraCuenta_SeRechazaSinQuitarseloALaPrimera));
        await VerificarAsync(servicio, Ramon, MovilRamon);
        var codigoLucia = (await servicio.IniciarAsync(Lucia, MovilRamon, CancellationToken.None)).Valor!.CodigoVerificacion!;

        var resultado = await servicio.VerificarDesdeMensajeAsync(MovilRamon, codigoLucia, CancellationToken.None);

        Assert.Equal(ResultadoVerificacionWhatsapp.RECHAZADO, resultado);
        var numeros = await context.NumerosWhatsapp.AsNoTracking().ToListAsync();
        Assert.Equal(MovilRamon, numeros.Single(n => n.UsuarioId == Ramon.Id).TelefonoVerificado);
        Assert.Null(numeros.Single(n => n.UsuarioId == LuciaId).TelefonoVerificado);
    }

    [Fact]
    public async Task TextoQueNoEsUnCodigo_NoEsUnIntentoDeVerificacion()
    {
        var (_, _, servicio) = await PrepararAsync(nameof(TextoQueNoEsUnCodigo_NoEsUnIntentoDeVerificacion));
        await servicio.IniciarAsync(Ramon, MovilRamon, CancellationToken.None);

        Assert.Equal(ResultadoVerificacionWhatsapp.NO_ES_CODIGO,
            await servicio.VerificarDesdeMensajeAsync(MovilRamon, "hola, ¿qué tal?", CancellationToken.None));
    }

    [Fact]
    public async Task Reiniciar_InvalidaElCodigoAnterior()
    {
        var (_, _, servicio) = await PrepararAsync(nameof(Reiniciar_InvalidaElCodigoAnterior));
        var primero = (await servicio.IniciarAsync(Ramon, MovilRamon, CancellationToken.None)).Valor!.CodigoVerificacion!;
        var segundo = (await servicio.IniciarAsync(Ramon, MovilRamon, CancellationToken.None)).Valor!.CodigoVerificacion!;

        Assert.NotEqual(primero, segundo);
        Assert.Equal(ResultadoVerificacionWhatsapp.RECHAZADO,
            await servicio.VerificarDesdeMensajeAsync(MovilRamon, primero, CancellationToken.None));
        Assert.Equal(ResultadoVerificacionWhatsapp.VERIFICADO,
            await servicio.VerificarDesdeMensajeAsync(MovilRamon, segundo, CancellationToken.None));
    }

    // D6/D2: cambiar de número no corta el canal hasta que el nuevo esté verificado.
    [Fact]
    public async Task CambiarDeNumero_ElAnteriorSigueActivoHastaVerificarElNuevo()
    {
        var (context, _, servicio) = await PrepararAsync(nameof(CambiarDeNumero_ElAnteriorSigueActivoHastaVerificarElNuevo));
        await VerificarAsync(servicio, Ramon, MovilRamon);

        var codigo = (await servicio.IniciarAsync(Ramon, "34600333444", CancellationToken.None)).Valor!.CodigoVerificacion!;
        Assert.Equal(MovilRamon, (await context.NumerosWhatsapp.AsNoTracking().SingleAsync()).TelefonoVerificado);

        await servicio.VerificarDesdeMensajeAsync("34600333444", codigo, CancellationToken.None);
        Assert.Equal("34600333444", (await context.NumerosWhatsapp.AsNoTracking().SingleAsync()).TelefonoVerificado);
    }

    // ─── Desvincular (D6) ────────────────────────────────────────────────────────

    [Fact]
    public async Task Desvincular_BorraElNumeroYUnaSegundaVezNoHayNada()
    {
        var (context, _, servicio) = await PrepararAsync(nameof(Desvincular_BorraElNumeroYUnaSegundaVezNoHayNada));
        await VerificarAsync(servicio, Ramon, MovilRamon);

        var resultado = await servicio.DesvincularAsync(Ramon, CancellationToken.None);

        Assert.True(resultado.EsCorrecto);
        Assert.Null(resultado.Valor!.TelefonoVerificado);
        Assert.False(await context.NumerosWhatsapp.AnyAsync());
        Assert.Equal(WhatsappErrores.SinNumeroVinculado, (await servicio.DesvincularAsync(Ramon, CancellationToken.None)).Error);
    }

    [Fact]
    public async Task Desvincular_ComoGestor_SeProhibe()
    {
        var (_, _, servicio) = await PrepararAsync(nameof(Desvincular_ComoGestor_SeProhibe));
        Assert.Equal(WhatsappErrores.SoloAutonomo, (await servicio.DesvincularAsync(Pedro, CancellationToken.None)).Error);
    }

    // ─── API ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Api_DevuelveLosTelefonosEnmascarados()
    {
        var (_, _, servicio) = await PrepararAsync(nameof(Api_DevuelveLosTelefonosEnmascarados));
        await VerificarAsync(servicio, Ramon, MovilRamon);
        await servicio.IniciarAsync(Ramon, "34600333444", CancellationToken.None);
        var controlador = new WhatsappNumeroController(servicio, new UsuarioFijo(Ramon));

        var ok = Assert.IsType<OkObjectResult>((await controlador.Get(CancellationToken.None)).Result);
        var respuesta = Assert.IsType<NumeroWhatsappResponse>(ok.Value);

        Assert.True(respuesta.Verificado);
        Assert.Equal("+••••••••222", respuesta.TelefonoVerificado);
        Assert.Equal("+••••••••444", respuesta.VerificacionPendiente!.Telefono);
        var json = System.Text.Json.JsonSerializer.Serialize(respuesta);
        Assert.DoesNotContain(MovilRamon, json);
        Assert.DoesNotContain("34600333444", json);
    }

    [Fact]
    public async Task Api_SinUsuario_Es401_YComoGestor_Es403()
    {
        var (_, _, servicio) = await PrepararAsync(nameof(Api_SinUsuario_Es401_YComoGestor_Es403));

        var anonimo = new WhatsappNumeroController(servicio, new UsuarioFijo(null));
        Assert.IsType<UnauthorizedResult>((await anonimo.Get(CancellationToken.None)).Result);

        var gestor = new WhatsappNumeroController(servicio, new UsuarioFijo(Pedro));
        var problema = Assert.IsType<ObjectResult>((await gestor.IniciarVerificacion(
            new IniciarVerificacionWhatsappRequest(MovilRamon), CancellationToken.None)).Result);
        Assert.Equal(403, problema.StatusCode);
    }

    // ─── Contra PostgreSQL ───────────────────────────────────────────────────────

    // Requisito 2 con concurrencia real: dos cuentas verifican el mismo número a la vez. Gana una y la otra recibe el
    // rechazo genérico, tanto si la frena la consulta previa como si la frena el índice único (su catch).
    [PostgreSqlFact]
    public Task PA3605_DosCuentasVerificanElMismoNumeroALaVez_SoloUnaLoConsigue() => WhatsappPersistenciaTests.EnEsquemaAsync(async options =>
    {
        var a = new UsuarioActual(WhatsappPersistenciaTests.AutonomoA, Rol.AUTONOMO);
        var b = new UsuarioActual(WhatsappPersistenciaTests.AutonomoB, Rol.AUTONOMO);
        string codigoA, codigoB;
        await using (var db = new ApplicationDbContext(options))
        {
            codigoA = (await ServicioReal(db).IniciarAsync(a, MovilRamon, CancellationToken.None)).Valor!.CodigoVerificacion!;
            codigoB = (await ServicioReal(db).IniciarAsync(b, MovilRamon, CancellationToken.None)).Valor!.CodigoVerificacion!;
        }

        await using var dbA = new ApplicationDbContext(options);
        await using var dbB = new ApplicationDbContext(options);
        var resultados = await Task.WhenAll(
            ServicioReal(dbA).VerificarDesdeMensajeAsync(MovilRamon, codigoA, CancellationToken.None),
            ServicioReal(dbB).VerificarDesdeMensajeAsync(MovilRamon, codigoB, CancellationToken.None));

        Assert.Single(resultados, r => r == ResultadoVerificacionWhatsapp.VERIFICADO);
        Assert.Single(resultados, r => r == ResultadoVerificacionWhatsapp.RECHAZADO);
        await using var comprobacion = new ApplicationDbContext(options);
        Assert.Single(await comprobacion.NumerosWhatsapp.Where(n => n.TelefonoVerificado == MovilRamon).ToListAsync());
    });

    // La consulta de caducidad y las comparaciones de texto se traducen a SQL de verdad (InMemory no lo prueba).
    [PostgreSqlFact]
    public Task PA3605_EnPostgreSql_IniciarVerificarYCaducar() => WhatsappPersistenciaTests.EnEsquemaAsync(async options =>
    {
        var a = new UsuarioActual(WhatsappPersistenciaTests.AutonomoA, Rol.AUTONOMO);
        var reloj = new RelojFijo(DateTimeOffset.UtcNow);
        await using var db = new ApplicationDbContext(options);
        var servicio = ServicioReal(db, reloj);

        var caducado = (await servicio.IniciarAsync(a, MovilRamon, CancellationToken.None)).Valor!.CodigoVerificacion!;
        reloj.Avanzar(TimeSpan.FromMinutes(16));
        Assert.Equal(ResultadoVerificacionWhatsapp.RECHAZADO, await servicio.VerificarDesdeMensajeAsync(MovilRamon, caducado, CancellationToken.None));

        var vigente = (await servicio.IniciarAsync(a, MovilRamon, CancellationToken.None)).Valor!.CodigoVerificacion!;
        Assert.Equal(ResultadoVerificacionWhatsapp.VERIFICADO, await servicio.VerificarDesdeMensajeAsync(MovilRamon, vigente, CancellationToken.None));
    });

    private static VerificacionNumeroWhatsappService ServicioReal(ApplicationDbContext db, TimeProvider? reloj = null) => new(
        db,
        reloj ?? TimeProvider.System,
        Options.Create(new WhatsappOptions { Habilitado = true, NumeroCanal = "+1 555 000 0000" }),
        NullLogger<VerificacionNumeroWhatsappService>.Instance);

    // ─── Utilidades ──────────────────────────────────────────────────────────────

    private static async Task VerificarAsync(VerificacionNumeroWhatsappService servicio, UsuarioActual usuario, string telefono)
    {
        var codigo = (await servicio.IniciarAsync(usuario, telefono, CancellationToken.None)).Valor!.CodigoVerificacion!;
        Assert.Equal(ResultadoVerificacionWhatsapp.VERIFICADO,
            await servicio.VerificarDesdeMensajeAsync(telefono, codigo, CancellationToken.None));
    }

    private static async Task<(ApplicationDbContext, RelojFijo, VerificacionNumeroWhatsappService)> PrepararAsync(string nombre, bool habilitado = true)
    {
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(nombre).Options);
        await DbInitializer.SeedAsync(context);
        context.Usuarios.Add(new Usuario
        {
            Id = LuciaId,
            Nombre = "Lucía",
            PrimerApellido = "Pérez",
            SegundoApellido = "Prueba",
            Email = "lucia.ut27@gestio.local",
            Nif = "12345678Z",
            Rol = Rol.AUTONOMO
        });
        await context.SaveChangesAsync();

        var reloj = new RelojFijo(new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero));
        var servicio = new VerificacionNumeroWhatsappService(
            context,
            reloj,
            Options.Create(new WhatsappOptions { Habilitado = habilitado, NumeroCanal = "+1 555 000 0000" }),
            NullLogger<VerificacionNumeroWhatsappService>.Instance);
        return (context, reloj, servicio);
    }
}

internal sealed class RelojFijo(DateTimeOffset inicio) : TimeProvider
{
    public DateTimeOffset Ahora { get; private set; } = inicio;

    public void Avanzar(TimeSpan intervalo) => Ahora += intervalo;

    public override DateTimeOffset GetUtcNow() => Ahora;
}

file sealed class UsuarioFijo(UsuarioActual? usuario) : ICurrentUserService
{
    public Task<UsuarioActual?> ObtenerAsync(CancellationToken cancellationToken) => Task.FromResult(usuario);
}
