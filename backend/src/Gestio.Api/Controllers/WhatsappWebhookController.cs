using System.Security.Cryptography;
using System.Text;
using Gestio.Api.Common;
using Gestio.Api.Infrastructure.Whatsapp;
using Gestio.Api.Services.Whatsapp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Gestio.Api.Controllers;

// UT27: webhook de la WhatsApp Cloud API. Público para Meta, pero cada notificación tiene que venir firmada con el
// App Secret (requisito 5). No usa X-Usuario-Id: el remitente lo identifica el número verificado, no una cabecera.
[ApiController]
[Route("api/whatsapp/webhook")]
public sealed class WhatsappWebhookController(
    RecepcionWhatsappService recepcion,
    IOptions<WhatsappOptions> options,
    ILogger<WhatsappWebhookController> logger) : ControllerBase
{
    // Una notificación de Meta ocupa unos pocos KB; el límite evita calcular HMAC sobre cuerpos enormes sin firma.
    public const int TamanoMaximoCuerpo = 1024 * 1024;

    // Reto de verificación que Meta hace al registrar la URL del webhook.
    [HttpGet]
    public IActionResult Verificar(
        [FromQuery(Name = "hub.mode")] string? modo,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? reto)
    {
        if (!options.Value.Habilitado) return NotFound();
        if (modo != "subscribe" || reto is null || !IgualesEnTiempoConstante(token, options.Value.TokenVerificacion))
            return StatusCode(StatusCodes.Status403Forbidden);

        return Content(reto, "text/plain");
    }

    [HttpPost]
    public async Task<IActionResult> Recibir(CancellationToken cancellationToken)
    {
        if (!options.Value.Habilitado) return NotFound();
        if (Request.ContentLength > TamanoMaximoCuerpo) return StatusCode(StatusCodes.Status413PayloadTooLarge);

        // Cuerpo en bruto: la firma se calcula sobre los bytes exactos, no sobre un JSON reinterpretado.
        var cuerpo = await LeerCuerpoAsync(cancellationToken);
        if (cuerpo is null) return StatusCode(StatusCodes.Status413PayloadTooLarge);

        if (!FirmaWebhookMeta.EsValida(cuerpo, Request.Headers[FirmaWebhookMeta.Cabecera].ToString(), options.Value.AppSecret))
        {
            logger.LogWarning("Notificación de WhatsApp rechazada: firma ausente o no válida");
            return Unauthorized();
        }

        IReadOnlyList<MensajeEntrante> mensajes;
        IReadOnlyList<EntregaFallida> fallidas;
        try
        {
            mensajes = LectorWebhookMeta.Leer(cuerpo, options.Value.PhoneNumberId);
            fallidas = LectorWebhookMeta.LeerEntregasFallidas(cuerpo, options.Value.PhoneNumberId);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Notificación de WhatsApp firmada pero con JSON no válido");
            return BadRequest();
        }

        // Requisito 8: una respuesta aceptada por Meta que luego no se pudo entregar queda registrada con su motivo.
        foreach (var fallida in fallidas)
            logger.LogWarning("Meta no pudo entregar la respuesta de WhatsApp {MensajeId}: {Codigo} {Titulo} {Detalle}",
                fallida.MensajeId, fallida.Codigo, fallida.Titulo, fallida.Detalle);

        // Cada mensaje por separado: si uno falla no se responde 500, porque Meta reenviaría el lote entero.
        foreach (var mensaje in mensajes)
        {
            try
            {
                await recepcion.RecibirAsync(mensaje, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "No se pudo tratar el mensaje de WhatsApp {MensajeId}", mensaje.MensajeId);
            }
        }

        return Ok();
    }

    private async Task<byte[]?> LeerCuerpoAsync(CancellationToken cancellationToken)
    {
        using var destino = new MemoryStream();
        var bufer = new byte[16384];
        int leidos;
        while ((leidos = await Request.Body.ReadAsync(bufer, cancellationToken)) > 0)
        {
            if (destino.Length + leidos > TamanoMaximoCuerpo) return null;
            destino.Write(bufer, 0, leidos);
        }
        return destino.ToArray();
    }

    // Se comparan los hashes para que la longitud del token tampoco se filtre por el tiempo de respuesta.
    private static bool IgualesEnTiempoConstante(string? recibido, string esperado)
    {
        if (string.IsNullOrEmpty(recibido) || string.IsNullOrEmpty(esperado)) return false;
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(recibido)),
            SHA256.HashData(Encoding.UTF8.GetBytes(esperado)));
    }
}
