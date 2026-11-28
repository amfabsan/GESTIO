using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Gestio.Api.Common;
using Gestio.Api.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace Gestio.Api.Infrastructure.Whatsapp;

// UT27 (requisito 1): WhatsApp Cloud API de Meta por HTTP, sin dependencias nuevas.
// - Metadatos de un adjunto: GET {graph}/{versión}/{media-id} → url temporal, mime_type, file_size.
// - Descarga: GET de esa url con el mismo token (segundo paso), en memoria y cortando al pasar del máximo.
// - Respuesta: POST {graph}/{versión}/{phone-number-id}/messages (texto libre, gratis dentro de la ventana de 24 h).
public sealed class ProveedorWhatsappMeta(HttpClient http, IOptions<WhatsappOptions> options) : IProveedorWhatsapp
{
    private WhatsappOptions Opciones => options.Value;

    public async Task<MediaWhatsapp> ObtenerMediaAsync(string mediaId, CancellationToken cancellationToken)
    {
        var media = await ObtenerMetadatosAsync(mediaId, cancellationToken);
        return new MediaWhatsapp(media.MimeType, media.TamanoBytes);
    }

    public async Task<DescargaWhatsapp> DescargarMediaAsync(string mediaId, long maximoBytes, CancellationToken cancellationToken)
    {
        // La url de descarga caduca a los pocos minutos: se pide en el momento de descargar.
        var media = await ObtenerMetadatosAsync(mediaId, cancellationToken);
        // El token solo se envía a una URL https devuelta por la propia Graph API.
        if (!Uri.TryCreate(media.Url, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("La Graph API no ha devuelto una URL de descarga https.");

        using var peticion = Peticion(HttpMethod.Get, url);
        using var respuesta = await http.SendAsync(peticion, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await ComprobarAsync(respuesta, "descarga del adjunto", cancellationToken);
        if (respuesta.Content.Headers.ContentLength > maximoBytes) return new DescargaWhatsapp(null, true);

        await using var origen = await respuesta.Content.ReadAsStreamAsync(cancellationToken);
        var destino = new MemoryStream();
        var bufer = new byte[81920];
        int leidos;
        while ((leidos = await origen.ReadAsync(bufer, cancellationToken)) > 0)
        {
            if (destino.Length + leidos > maximoBytes)
            {
                await destino.DisposeAsync();
                return new DescargaWhatsapp(null, true);
            }
            destino.Write(bufer, 0, leidos);
        }

        destino.Position = 0;
        return new DescargaWhatsapp(destino, false);
    }

    public async Task EnviarTextoAsync(string telefono, string texto, CancellationToken cancellationToken)
    {
        using var peticion = Peticion(HttpMethod.Post, Graph($"{Uri.EscapeDataString(Opciones.PhoneNumberId)}/messages"));
        peticion.Content = JsonContent.Create(new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = telefono,
            type = "text",
            text = new { preview_url = false, body = texto }
        });
        using var respuesta = await http.SendAsync(peticion, cancellationToken);
        await ComprobarAsync(respuesta, "envío de la respuesta", cancellationToken);
    }

    private async Task<MetadatosMedia> ObtenerMetadatosAsync(string mediaId, CancellationToken cancellationToken)
    {
        using var peticion = Peticion(HttpMethod.Get, Graph(Uri.EscapeDataString(mediaId)));
        using var respuesta = await http.SendAsync(peticion, cancellationToken);
        await ComprobarAsync(respuesta, "metadatos del adjunto", cancellationToken);
        return await respuesta.Content.ReadFromJsonAsync<MetadatosMedia>(cancellationToken)
               ?? throw new InvalidOperationException("Respuesta vacía de la Graph API.");
    }

    // A diferencia de EnsureSuccessStatusCode, conserva el cuerpo del error de Meta (p. ej. «recipient not in allowed
    // list» o token caducado) para que quede en el registro.
    private static async Task ComprobarAsync(HttpResponseMessage respuesta, string operacion, CancellationToken cancellationToken)
    {
        if (respuesta.IsSuccessStatusCode) return;
        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancellationToken);
        if (cuerpo.Length > 500) cuerpo = cuerpo[..500];
        throw new HttpRequestException($"Graph API ({operacion}): {(int)respuesta.StatusCode} {cuerpo}", null, respuesta.StatusCode);
    }

    private Uri Graph(string ruta) => new($"{Opciones.UrlGraph.TrimEnd('/')}/{Opciones.VersionApi}/{ruta}");

    private HttpRequestMessage Peticion(HttpMethod metodo, Uri url)
    {
        var peticion = new HttpRequestMessage(metodo, url);
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Opciones.TokenAcceso);
        return peticion;
    }

    private sealed record MetadatosMedia(
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("mime_type")] string? MimeType,
        [property: JsonPropertyName("file_size")] long? TamanoBytes);
}
