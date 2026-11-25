using System.Net;
using System.Text;
using System.Text.Json;
using Gestio.Api.Common;
using Gestio.Api.Controllers;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Infrastructure.Whatsapp;
using Gestio.Api.Services.Whatsapp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gestio.Api.Tests;

/// <summary>UT27, capa 4: integración con la WhatsApp Cloud API de Meta (requisitos 1 y 5).</summary>
public sealed class WhatsappMetaTests
{
    private const string Secreto = "secreto-de-prueba";
    private const string PhoneNumberId = "123456789012345";
    private const string MovilRamon = "34600111222";

    // ─── Firma X-Hub-Signature-256 (requisito 5) ─────────────────────────────────

    private static readonly byte[] CuerpoEjemplo = "{\"object\":\"whatsapp_business_account\"}"u8.ToArray();

    [Fact]
    public void Firma_CalculadaConElAppSecret_EsValida()
    {
        // Valor calculado aparte (HMAC-SHA256 del cuerpo con la clave), no con el código de producción.
        var esperada = "sha256=" + Convert.ToHexStringLower(
            new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(Secreto)).ComputeHash(CuerpoEjemplo));

        Assert.True(FirmaWebhookMeta.EsValida(CuerpoEjemplo, esperada, Secreto));
        Assert.True(FirmaWebhookMeta.EsValida(CuerpoEjemplo, esperada.ToUpperInvariant().Replace("SHA256=", "sha256="), Secreto));
    }

    // Vector fijo: HMAC-SHA256 de «{"a":1}» con la clave «clave», calculado fuera de .NET con
    // `printf '{"a":1}' | openssl dgst -sha256 -hmac clave`.
    [Fact]
    public void Firma_VectorConocido_EsValida() =>
        Assert.True(FirmaWebhookMeta.EsValida("{\"a\":1}"u8,
            "sha256=21a69fefed490f3f56de59be0b248591d506dde035781bc6cd160095378c6501", "clave"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha256=")]
    [InlineData("sha256=zz")]
    [InlineData("sha256=abcd")]
    [InlineData("sha1=0000000000000000000000000000000000000000")]
    public void Firma_AusenteMalFormadaODeOtraLongitud_NoEsValida(string? cabecera) =>
        Assert.False(FirmaWebhookMeta.EsValida(CuerpoEjemplo, cabecera, Secreto));

    [Fact]
    public void Firma_ConOtroSecretoOConElCuerpoAlterado_NoEsValida()
    {
        var firma = FirmaWebhookMeta.Calcular(CuerpoEjemplo, Secreto);

        Assert.False(FirmaWebhookMeta.EsValida(CuerpoEjemplo, firma, "otro-secreto"));
        Assert.False(FirmaWebhookMeta.EsValida("{\"object\":\"otro\"}"u8, firma, Secreto));
        Assert.False(FirmaWebhookMeta.EsValida(CuerpoEjemplo, firma, ""));
    }

    // ─── Lectura del JSON de Meta ────────────────────────────────────────────────

    [Fact]
    public void Lector_TraduceImagenDocumentoTextoYOtros()
    {
        var cuerpo = Notificacion(
            new { from = MovilRamon, id = "wamid.img", timestamp = "1", type = "image", image = new { id = "m1", mime_type = "image/jpeg", sha256 = "x" } },
            new { from = MovilRamon, id = "wamid.doc", timestamp = "1", type = "document", document = new { id = "m2", mime_type = "application/pdf", filename = "factura.pdf" } },
            new { from = MovilRamon, id = "wamid.txt", timestamp = "1", type = "text", text = new { body = "GESTIO-ABC234" } },
            new { from = MovilRamon, id = "wamid.aud", timestamp = "1", type = "audio", audio = new { id = "m3" } });

        var mensajes = LectorWebhookMeta.Leer(cuerpo, PhoneNumberId);

        Assert.Equal(
            [
                new MensajeEntrante("wamid.img", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg"),
                new MensajeEntrante("wamid.doc", MovilRamon, TipoMensajeWhatsapp.DOCUMENTO, "m2", "application/pdf", "factura.pdf"),
                new MensajeEntrante("wamid.txt", MovilRamon, TipoMensajeWhatsapp.TEXTO, Texto: "GESTIO-ABC234"),
                new MensajeEntrante("wamid.aud", MovilRamon, TipoMensajeWhatsapp.OTRO)
            ],
            mensajes);
    }

    [Fact]
    public void Lector_IgnoraEstadosDeEntrega()
    {
        var cuerpo = JsonSerializer.SerializeToUtf8Bytes(new
        {
            @object = "whatsapp_business_account",
            entry = new[] { new { id = "1", changes = new[] { new { field = "messages", value = new
            {
                messaging_product = "whatsapp",
                metadata = new { display_phone_number = "15550000000", phone_number_id = PhoneNumberId },
                statuses = new[] { new { id = "wamid.x", status = "delivered", recipient_id = MovilRamon } }
            } } } } }
        });

        Assert.Empty(LectorWebhookMeta.Leer(cuerpo, PhoneNumberId));
    }

    // Requisito 5: un mensaje dirigido a otro número de WhatsApp Business no es de la integración configurada.
    [Fact]
    public void Lector_IgnoraMensajesDeOtroPhoneNumberId()
    {
        var cuerpo = Notificacion(new { from = MovilRamon, id = "wamid.1", type = "text", text = new { body = "hola" } });

        Assert.Empty(LectorWebhookMeta.Leer(cuerpo, "999999999999999"));
    }

    [Theory]
    [InlineData("+34600111222")]
    [InlineData("abc")]
    [InlineData("")]
    public void Lector_DescartaRemitentesConFormatoNoValido(string remitente)
    {
        var cuerpo = Notificacion(new { from = remitente, id = "wamid.1", type = "text", text = new { body = "hola" } });

        Assert.Empty(LectorWebhookMeta.Leer(cuerpo, PhoneNumberId));
    }

    [Fact]
    public void Lector_OtroObjetoOJsonSinMensajes_NoDevuelveNada()
    {
        Assert.Empty(LectorWebhookMeta.Leer("{\"object\":\"page\",\"entry\":[]}"u8, PhoneNumberId));
        Assert.Empty(LectorWebhookMeta.Leer("{}"u8, PhoneNumberId));
    }

    [Fact]
    public void Lector_JsonRoto_Lanza() =>
        Assert.ThrowsAny<JsonException>(() => LectorWebhookMeta.Leer("{no es json"u8, PhoneNumberId));

    // ─── Proveedor contra la Graph API (HTTP falso) ──────────────────────────────

    [Fact]
    public async Task Proveedor_ObtieneMetadatosConElToken()
    {
        var http = new HttpFalso();
        http.Respuestas["https://graph.test/v21.0/m1"] = Json(new { url = "https://cdn.test/m1", mime_type = "image/jpeg", file_size = 2048, id = "m1" });
        var proveedor = Proveedor(http);

        var media = await proveedor.ObtenerMediaAsync("m1", CancellationToken.None);

        Assert.Equal("image/jpeg", media.MimeType);
        Assert.Equal(2048, media.TamanoBytes);
        Assert.Equal("Bearer token-de-prueba", http.Peticiones.Single().Autorizacion);
    }

    [Fact]
    public async Task Proveedor_DescargaEnDosPasosConElTokenEnAmbos()
    {
        var http = new HttpFalso();
        http.Respuestas["https://graph.test/v21.0/m1"] = Json(new { url = "https://cdn.test/m1", mime_type = "image/jpeg", file_size = 4 });
        http.Respuestas["https://cdn.test/m1"] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3, 4]) };

        var descarga = await Proveedor(http).DescargarMediaAsync("m1", 10, CancellationToken.None);

        Assert.False(descarga.Excedido);
        Assert.Equal([1, 2, 3, 4], descarga.Contenido!.ToArray());
        Assert.Equal(["https://graph.test/v21.0/m1", "https://cdn.test/m1"], http.Peticiones.Select(p => p.Url));
        Assert.All(http.Peticiones, p => Assert.Equal("Bearer token-de-prueba", p.Autorizacion));
    }

    [Fact]
    public async Task Proveedor_CortaLaDescargaAlPasarDelMaximo_AunqueNoSeDeclareLaLongitud()
    {
        var http = new HttpFalso();
        http.Respuestas["https://graph.test/v21.0/m1"] = Json(new { url = "https://cdn.test/m1" });
        http.Respuestas["https://cdn.test/m1"] = () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new FlujoSinLongitud(new byte[100]))
        };

        var descarga = await Proveedor(http).DescargarMediaAsync("m1", 50, CancellationToken.None);

        Assert.True(descarga.Excedido);
        Assert.Null(descarga.Contenido);
    }

    [Fact]
    public async Task Proveedor_ContentLengthDeclaradaMayorQueElMaximo_NoLeeElCuerpo()
    {
        var http = new HttpFalso();
        http.Respuestas["https://graph.test/v21.0/m1"] = Json(new { url = "https://cdn.test/m1" });
        http.Respuestas["https://cdn.test/m1"] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[100]) };

        var descarga = await Proveedor(http).DescargarMediaAsync("m1", 50, CancellationToken.None);

        Assert.True(descarga.Excedido);
    }

    // En la demo, el motivo del error de Meta tiene que verse en el registro.
    [Fact]
    public async Task Proveedor_ErrorDeLaGraphApi_ConservaElMotivoDeMeta()
    {
        var http = new HttpFalso();
        http.Respuestas[$"https://graph.test/v21.0/{PhoneNumberId}/messages"] = () => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":{\"message\":\"Recipient phone number not in allowed list\",\"code\":131030}}")
        };

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Proveedor(http).EnviarTextoAsync(MovilRamon, "Hola", CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Contains("not in allowed list", error.Message);
    }

    [Fact]
    public async Task Proveedor_NoEnviaElTokenAUnaUrlQueNoSeaHttps()
    {
        var http = new HttpFalso();
        http.Respuestas["https://graph.test/v21.0/m1"] = Json(new { url = "http://cdn.test/m1" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => Proveedor(http).DescargarMediaAsync("m1", 50, CancellationToken.None));
        Assert.Single(http.Peticiones);
    }

    [Fact]
    public async Task Proveedor_EnviaElTextoAlEndpointDeMensajesDelNumeroConfigurado()
    {
        var http = new HttpFalso();
        http.Respuestas[$"https://graph.test/v21.0/{PhoneNumberId}/messages"] = Json(new { messages = new[] { new { id = "wamid.r" } } });

        await Proveedor(http).EnviarTextoAsync(MovilRamon, "Hola", CancellationToken.None);

        var peticion = http.Peticiones.Single();
        Assert.Equal(HttpMethod.Post, peticion.Metodo);
        Assert.Equal("Bearer token-de-prueba", peticion.Autorizacion);
        using var json = JsonDocument.Parse(peticion.Cuerpo!);
        Assert.Equal("whatsapp", json.RootElement.GetProperty("messaging_product").GetString());
        Assert.Equal(MovilRamon, json.RootElement.GetProperty("to").GetString());
        Assert.Equal("text", json.RootElement.GetProperty("type").GetString());
        Assert.Equal("Hola", json.RootElement.GetProperty("text").GetProperty("body").GetString());
    }

    [Fact]
    public async Task Proveedor_ErrorDeLaGraphApi_Lanza()
    {
        var http = new HttpFalso();
        http.Respuestas[$"https://graph.test/v21.0/{PhoneNumberId}/messages"] = () => new HttpResponseMessage(HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<HttpRequestException>(() => Proveedor(http).EnviarTextoAsync(MovilRamon, "Hola", CancellationToken.None));
    }

    // ─── Opciones ────────────────────────────────────────────────────────────────

    [Fact]
    public void Opciones_DeshabilitadoNoExigeNada_YHabilitadoLoExigeTodo()
    {
        Assert.True(WhatsappOptions.EsValida(new WhatsappOptions()));
        Assert.False(WhatsappOptions.EsValida(new WhatsappOptions { Habilitado = true }));
        Assert.True(WhatsappOptions.EsValida(OpcionesCompletas()));
        var sinSecreto = OpcionesCompletas();
        sinSecreto.AppSecret = "";
        Assert.False(WhatsappOptions.EsValida(sinSecreto));
        var http = OpcionesCompletas();
        http.UrlGraph = "http://graph.facebook.com";
        Assert.False(WhatsappOptions.EsValida(http));
    }

    // ─── Webhook ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_RetoDeVerificacion_DevuelveElRetoSoloConElTokenCorrecto()
    {
        var e = await WhatsappRecepcionTests.Entorno.CrearAsync(nameof(Webhook_RetoDeVerificacion_DevuelveElRetoSoloConElTokenCorrecto));
        var webhook = Webhook(e);

        var ok = Assert.IsType<ContentResult>(webhook.Verificar("subscribe", "token-verificacion", "12345"));
        Assert.Equal("12345", ok.Content);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(webhook.Verificar("subscribe", "otro", "12345")).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(webhook.Verificar("unsubscribe", "token-verificacion", "12345")).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(webhook.Verificar("subscribe", null, "12345")).StatusCode);
    }

    [Fact]
    public async Task Webhook_ConElCanalDeshabilitado_Es404()
    {
        var e = await WhatsappRecepcionTests.Entorno.CrearAsync(nameof(Webhook_ConElCanalDeshabilitado_Es404));
        var webhook = Webhook(e, habilitado: false);

        Assert.IsType<NotFoundResult>(webhook.Verificar("subscribe", "token-verificacion", "1"));
        Assert.IsType<NotFoundResult>(await Enviar(webhook, Notificacion(), firmar: true));
    }

    // Requisito 5: sin firma válida no se procesa nada (ni se responde por WhatsApp).
    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "sha256=0000")]
    [InlineData(true, "otro-secreto")]
    public async Task Webhook_SinFirmaValida_Es401YNoProcesaNada(bool firmar, string? cabeceraOSecreto)
    {
        var e = await WhatsappRecepcionTests.Entorno.CrearAsync($"{nameof(Webhook_SinFirmaValida_Es401YNoProcesaNada)}-{firmar}-{cabeceraOSecreto}");
        var cuerpo = Notificacion(new { from = "34699000111", id = "wamid.1", type = "text", text = new { body = "hola" } });
        var webhook = Webhook(e);

        var resultado = firmar
            ? await Enviar(webhook, cuerpo, firmar: true, secreto: cabeceraOSecreto!)
            : await Enviar(webhook, cuerpo, firmar: false, cabecera: cabeceraOSecreto);

        Assert.IsType<UnauthorizedResult>(resultado);
        Assert.False(await e.Context.MensajesWhatsapp.AnyAsync());
        Assert.Empty(e.Proveedor.Enviados);
    }

    // PA 3600 y 3603 de punta a punta: notificación firmada de Meta → documento; la misma notificación otra vez → nada.
    [Fact]
    public async Task Webhook_NotificacionFirmadaDosVeces_UnDocumentoYUnaConfirmacion()
    {
        var e = await WhatsappRecepcionTests.Entorno.CrearAsync(nameof(Webhook_NotificacionFirmadaDosVeces_UnDocumentoYUnaConfirmacion));
        e.Proveedor.Media["m1"] = ([0xFF, 0xD8, 0xFF, 0xE0, 0x00], "image/jpeg");
        var cuerpo = Notificacion(new { from = MovilRamon, id = "wamid.1", type = "image", image = new { id = "m1", mime_type = "image/jpeg" } });

        for (var i = 0; i < 2; i++)
        {
            Assert.IsType<OkResult>(await Enviar(Webhook(e), cuerpo, firmar: true));
            await e.ProcesarColaAsync();
        }

        var documento = await e.Context.Documentos.AsNoTracking().SingleAsync(d => d.Origen == OrigenDocumento.WHATSAPP);
        Assert.Equal("wamid.1", documento.ReferenciaMensaje);
        Assert.Single(e.Proveedor.Enviados);
    }

    // En un mismo POST, un mensaje que falla no impide tratar los demás y Meta recibe 200 (no reenvía el lote).
    [Fact]
    public async Task Webhook_UnMensajeQueFallaEnElMismoLote_NoImpideTratarLosDemas()
    {
        var e = await WhatsappRecepcionTests.Entorno.CrearAsync(nameof(Webhook_UnMensajeQueFallaEnElMismoLote_NoImpideTratarLosDemas));
        e.Interceptor.FallarAlGuardarMensaje = "wamid.falla";
        var lote = Notificacion(
            new { from = "34699000111", id = "wamid.falla", type = "text", text = new { body = "hola" } },
            new { from = "34699000222", id = "wamid.ok", type = "text", text = new { body = "hola" } });

        Assert.IsType<OkResult>(await Enviar(Webhook(e), lote, firmar: true));

        var enviado = Assert.Single(e.Proveedor.Enviados);
        Assert.Equal("34699000222", enviado.Telefono);
        e.Context.ChangeTracker.Clear();
        Assert.Equal(["wamid.ok"], await e.Context.MensajesWhatsapp.Select(m => m.MensajeId).ToListAsync());
    }

    // JSON válido y firmado, pero con tipos inesperados: no da 500 (Meta reintentaría), se ignora.
    [Fact]
    public async Task Webhook_JsonConTiposInesperados_NoEs500()
    {
        var e = await WhatsappRecepcionTests.Entorno.CrearAsync(nameof(Webhook_JsonConTiposInesperados_NoEs500));
        var raro = "{\"object\":\"whatsapp_business_account\",\"entry\":[\"x\",{\"changes\":[{\"field\":\"messages\",\"value\":\"x\"}]}]}"u8.ToArray();

        Assert.IsType<OkResult>(await Enviar(Webhook(e), raro, firmar: true));
        Assert.Empty(e.Proveedor.Enviados);
    }

    [Fact]
    public void Lector_ImagenSinIdDeAdjunto_EsTipoNoAdmitido()
    {
        var cuerpo = Notificacion(new { from = MovilRamon, id = "wamid.1", type = "image", image = new { mime_type = "image/jpeg" } });

        Assert.Equal(TipoMensajeWhatsapp.OTRO, Assert.Single(LectorWebhookMeta.Leer(cuerpo, PhoneNumberId)).Tipo);
    }

    [Fact]
    public async Task Webhook_ContentLengthDeclaradaMayorQueElMaximo_Es413()
    {
        var e = await WhatsappRecepcionTests.Entorno.CrearAsync(nameof(Webhook_ContentLengthDeclaradaMayorQueElMaximo_Es413));
        var webhook = Webhook(e);
        webhook.ControllerContext.HttpContext.Request.Body = new MemoryStream([]);
        webhook.ControllerContext.HttpContext.Request.ContentLength = WhatsappWebhookController.TamanoMaximoCuerpo + 1;

        Assert.Equal(413, Assert.IsType<StatusCodeResult>(await webhook.Recibir(CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task Webhook_CuerpoDemasiadoGrande_Es413SinCalcularNada()
    {
        var e = await WhatsappRecepcionTests.Entorno.CrearAsync(nameof(Webhook_CuerpoDemasiadoGrande_Es413SinCalcularNada));
        var enorme = new byte[WhatsappWebhookController.TamanoMaximoCuerpo + 1];

        var resultado = await Enviar(Webhook(e), enorme, firmar: false, declararLongitud: false);

        Assert.Equal(413, Assert.IsType<StatusCodeResult>(resultado).StatusCode);
    }

    [Fact]
    public async Task Webhook_JsonRotoPeroFirmado_Es400()
    {
        var e = await WhatsappRecepcionTests.Entorno.CrearAsync(nameof(Webhook_JsonRotoPeroFirmado_Es400));

        Assert.IsType<BadRequestResult>(await Enviar(Webhook(e), "{roto"u8.ToArray(), firmar: true));
    }

    // ─── Utilidades ──────────────────────────────────────────────────────────────

    private static WhatsappOptions OpcionesCompletas() => new()
    {
        Habilitado = true,
        NumeroCanal = "+1 555 000 0000",
        PhoneNumberId = PhoneNumberId,
        TokenAcceso = "token-de-prueba",
        AppSecret = Secreto,
        TokenVerificacion = "token-verificacion",
        UrlGraph = "https://graph.test"
    };

    private static ProveedorWhatsappMeta Proveedor(HttpFalso http) => new(new HttpClient(http), Options.Create(OpcionesCompletas()));

    private static WhatsappWebhookController Webhook(WhatsappRecepcionTests.Entorno e, bool habilitado = true)
    {
        var opciones = OpcionesCompletas();
        opciones.Habilitado = habilitado;
        return new WhatsappWebhookController(e.Recepcion, Options.Create(opciones), NullLogger<WhatsappWebhookController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static Task<IActionResult> Enviar(WhatsappWebhookController webhook, byte[] cuerpo, bool firmar,
        string secreto = Secreto, string? cabecera = null, bool declararLongitud = true)
    {
        var peticion = webhook.ControllerContext.HttpContext.Request;
        peticion.Method = "POST";
        peticion.Body = new MemoryStream(cuerpo);
        if (declararLongitud) peticion.ContentLength = cuerpo.Length;
        var firma = firmar ? FirmaWebhookMeta.Calcular(cuerpo, secreto) : cabecera;
        if (firma is not null) peticion.Headers[FirmaWebhookMeta.Cabecera] = firma;
        return webhook.Recibir(CancellationToken.None);
    }

    // Notificación con la forma real de la WhatsApp Cloud API.
    private static byte[] Notificacion(params object[] mensajes) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        @object = "whatsapp_business_account",
        entry = new[]
        {
            new
            {
                id = "WHATSAPP_BUSINESS_ACCOUNT_ID",
                changes = new[]
                {
                    new
                    {
                        field = "messages",
                        value = new
                        {
                            messaging_product = "whatsapp",
                            metadata = new { display_phone_number = "15550000000", phone_number_id = PhoneNumberId },
                            contacts = new[] { new { profile = new { name = "Ramón" }, wa_id = MovilRamon } },
                            messages = mensajes
                        }
                    }
                }
            }
        }
    });

    private static Func<HttpResponseMessage> Json(object cuerpo) => () => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json")
    };
}

internal sealed class HttpFalso : HttpMessageHandler
{
    public Dictionary<string, Func<HttpResponseMessage>> Respuestas { get; } = [];
    public List<(HttpMethod Metodo, string Url, string? Autorizacion, string? Cuerpo)> Peticiones { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        var cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Peticiones.Add((request.Method, url, request.Headers.Authorization?.ToString(), cuerpo));
        return Respuestas.TryGetValue(url, out var respuesta) ? respuesta() : new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}

// Flujo que no admite Length: simula una descarga sin Content-Length.
internal sealed class FlujoSinLongitud(byte[] datos) : Stream
{
    private readonly MemoryStream interno = new(datos);
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => interno.Read(buffer, offset, Math.Min(count, 16));
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
