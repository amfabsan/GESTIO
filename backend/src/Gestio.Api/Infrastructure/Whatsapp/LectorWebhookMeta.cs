using System.Text.Json;
using System.Text.RegularExpressions;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Services.Whatsapp;

namespace Gestio.Api.Infrastructure.Whatsapp;

// UT27: traduce una notificación del webhook de la WhatsApp Cloud API a MensajeEntrante.
// Ignora los «statuses» (entregado, leído…), los cambios que no son «messages» y los mensajes dirigidos a un número
// distinto del configurado.
public static partial class LectorWebhookMeta
{
    [GeneratedRegex("^[1-9][0-9]{7,14}$")]
    private static partial Regex E164SinMas();

    public static IReadOnlyList<MensajeEntrante> Leer(ReadOnlySpan<byte> cuerpo, string phoneNumberId)
    {
        using var json = JsonDocument.Parse(cuerpo.ToArray());
        var mensajes = new List<MensajeEntrante>();
        var raiz = json.RootElement;
        if (Texto(raiz, "object") != "whatsapp_business_account" || !raiz.TryGetProperty("entry", out var entradas) ||
            entradas.ValueKind != JsonValueKind.Array) return mensajes;

        // JSON válido con tipos inesperados (p. ej. «value» como cadena) se ignora en lugar de lanzar.
        foreach (var entrada in entradas.EnumerateArray())
        {
            if (Hijo(entrada, "changes") is not { ValueKind: JsonValueKind.Array } cambios) continue;
            foreach (var cambio in cambios.EnumerateArray())
            {
                if (Texto(cambio, "field") != "messages" || Hijo(cambio, "value") is not { ValueKind: JsonValueKind.Object } valor) continue;
                if (Texto(Hijo(valor, "metadata") ?? default, "phone_number_id") != phoneNumberId) continue;
                if (Hijo(valor, "messages") is not { ValueKind: JsonValueKind.Array } lista) continue;

                foreach (var m in lista.EnumerateArray())
                    if (m.ValueKind == JsonValueKind.Object && Traducir(m) is { } mensaje) mensajes.Add(mensaje);
            }
        }

        return mensajes;
    }

    private static MensajeEntrante? Traducir(JsonElement m)
    {
        var id = Texto(m, "id");
        var desde = Texto(m, "from");
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || desde is null || !E164SinMas().IsMatch(desde)) return null;

        // Una imagen o un documento sin id de adjunto se trata como tipo no admitido.
        return Texto(m, "type") switch
        {
            "image" when Hijo(m, "image") is { } imagen && Texto(imagen, "id") is { } mediaImagen =>
                new MensajeEntrante(id, desde, TipoMensajeWhatsapp.IMAGEN, mediaImagen, Texto(imagen, "mime_type")),
            "document" when Hijo(m, "document") is { } documento && Texto(documento, "id") is { } mediaDocumento =>
                new MensajeEntrante(id, desde, TipoMensajeWhatsapp.DOCUMENTO, mediaDocumento, Texto(documento, "mime_type"),
                    Texto(documento, "filename")),
            "text" when Hijo(m, "text") is { } texto =>
                new MensajeEntrante(id, desde, TipoMensajeWhatsapp.TEXTO, Texto: Texto(texto, "body")),
            _ => new MensajeEntrante(id, desde, TipoMensajeWhatsapp.OTRO)
        };
    }

    private static JsonElement? Hijo(JsonElement elemento, string propiedad) =>
        elemento.ValueKind == JsonValueKind.Object && elemento.TryGetProperty(propiedad, out var valor) ? valor : null;

    private static string? Texto(JsonElement elemento, string propiedad) =>
        elemento.ValueKind == JsonValueKind.Object && elemento.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;
}
