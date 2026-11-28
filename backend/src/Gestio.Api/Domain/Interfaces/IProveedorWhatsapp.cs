namespace Gestio.Api.Domain.Interfaces;

// UT27: lo que GESTIO necesita del proveedor de mensajería (requisito 1). La implementación real (Meta Cloud API)
// llega en la capa 4; las pruebas usan un proveedor falso.
public interface IProveedorWhatsapp
{
    // Metadatos del archivo adjunto, sin descargarlo: permite rechazar por tamaño antes de bajar nada.
    Task<MediaWhatsapp> ObtenerMediaAsync(string mediaId, CancellationToken cancellationToken);

    // Descarga el adjunto en memoria (requisito 8: ninguna copia en disco fuera del almacenamiento de documentos).
    // Si supera maximoBytes se corta la descarga y se devuelve Excedido = true sin contenido.
    Task<DescargaWhatsapp> DescargarMediaAsync(string mediaId, long maximoBytes, CancellationToken cancellationToken);

    Task EnviarTextoAsync(string telefono, string texto, CancellationToken cancellationToken);
}

public sealed record MediaWhatsapp(string? MimeType, long? TamanoBytes);

public sealed record DescargaWhatsapp(MemoryStream? Contenido, bool Excedido);
