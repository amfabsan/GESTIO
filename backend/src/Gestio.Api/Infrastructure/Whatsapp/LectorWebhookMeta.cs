using System.Text.Json;
using System.Text.RegularExpressions;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Services.Whatsapp;

namespace Gestio.Api.Infrastructure.Whatsapp;

// UT27: traduce una notificación del webhook de la WhatsApp Cloud API a MensajeEntrante.
// Ignora los «statuses» (entregado, leído…), los cambios que no son «messages» y los mensajes dirigidos a un número
// distinto del configurado.
public sealed record EntregaFallida(string MensajeId, int? Codigo, string? Titulo, string? Detalle);

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

    // Requisito 8: Meta acepta una respuesta (200) y avisa después, con un «status» = «failed», si no pudo entregarla.
    // Se extraen solo el id del mensaje y el error; el destinatario (teléfono) no se devuelve para no registrarlo.
    public static IReadOnlyList<EntregaFallida> LeerEntregasFallidas(ReadOnlySpan<byte> cuerpo, string phoneNumberId)
    {
        using var json = JsonDocument.Parse(cuerpo.ToArray());
        var fallidas = new List<EntregaFallida>();
        var raiz = json.RootElement;
        if (Texto(raiz, "object") != "whatsapp_business_account" || Hijo(raiz, "entry") is not { ValueKind: JsonValueKind.Array } entradas)
            return fallidas;

        foreach (var entrada in entradas.EnumerateArray())
        {
            if (Hijo(entrada, "changes") is not { ValueKind: JsonValueKind.Array } cambios) continue;
            foreach (var cambio in cambios.EnumerateArray())
            {
                if (Texto(cambio, "field") != "messages" || Hijo(cambio, "value") is not { ValueKind: JsonValueKind.Object } valor) continue;
                if (Texto(Hijo(valor, "metadata") ?? default, "phone_number_id") != phoneNumberId) continue;
                if (Hijo(valor, "statuses") is not { ValueKind: JsonValueKind.Array } estados) continue;

                foreach (var estado in estados.EnumerateArray())
                {
                    if (Texto(estado, "status") != "failed") continue;
                    var error = Hijo(estado, "errors") is { ValueKind: JsonValueKind.Array } errores && errores.GetArrayLength() > 0
                        ? errores[0]
                        : default;
                    var codigo = Hijo(error, "code") is { ValueKind: JsonValueKind.Number } c ? c.GetInt32() : (int?)null;
                    fallidas.Add(new EntregaFallida(
                        Texto(estado, "id") ?? "?",
                        codigo,
                        Texto(error, "title") ?? Texto(error, "message"),
                        Texto(Hijo(error, "error_data") ?? default, "details")));
                }
            }
        }

        return fallidas;
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
