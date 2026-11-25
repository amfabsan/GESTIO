using Gestio.Api.Domain.Interfaces;

namespace Gestio.Api.Infrastructure.Whatsapp;

// UT27: proveedor cuando el canal no está configurado. No envía ni descarga nada; si por error se llega a usar, falla
// de forma visible en los registros en lugar de simular que todo ha ido bien.
public sealed class ProveedorWhatsappDesactivado(ILogger<ProveedorWhatsappDesactivado> logger) : IProveedorWhatsapp
{
    public Task<MediaWhatsapp> ObtenerMediaAsync(string mediaId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("El canal de WhatsApp no está configurado.");

    public Task<DescargaWhatsapp> DescargarMediaAsync(string mediaId, long maximoBytes, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("El canal de WhatsApp no está configurado.");

    public Task EnviarTextoAsync(string telefono, string texto, CancellationToken cancellationToken)
    {
        logger.LogWarning("Respuesta de WhatsApp no enviada: el canal no está configurado");
        return Task.CompletedTask;
    }
}
