using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Domain.Entities;

// UT27: registro de cada mensaje entrante (idempotencia y trazabilidad de fallos, requisitos 5 y 8).
// No guarda teléfonos ni texto del usuario: solo el usuario si el número está vinculado y un código de motivo.
// MediaId, MimeType y NombreArchivo solo se rellenan mientras el archivo está pendiente de procesar en segundo plano
// y se vacían al terminar.
public sealed class MensajeWhatsappRecibido
{
    // Id del mensaje en el proveedor («wamid.…»).
    public required string MensajeId { get; set; }

    public Guid? UsuarioId { get; set; }

    public TipoMensajeWhatsapp Tipo { get; set; }

    public ResultadoMensajeWhatsapp Resultado { get; set; }

    // Código interno (p. ej. «tipo_no_permitido», «almacenamiento_fallido»), nunca texto libre.
    public string? Motivo { get; set; }

    public Guid? DocumentoId { get; set; }

    public string? MediaId { get; set; }

    public string? MimeType { get; set; }

    public string? NombreArchivo { get; set; }

    public int Intentos { get; set; }

    public DateTimeOffset FechaRecepcion { get; set; }

    public DateTimeOffset FechaActualizacion { get; set; }

    public Usuario? Usuario { get; set; }

    public Documento? Documento { get; set; }
}
