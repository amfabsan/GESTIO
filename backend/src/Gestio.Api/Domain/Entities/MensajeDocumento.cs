namespace Gestio.Api.Domain.Entities;

public sealed class MensajeDocumento
{
    public Guid Id { get; set; }
    public Guid DocumentoId { get; set; }
    public Guid AutorId { get; set; }
    public required string Contenido { get; set; }
    public DateTimeOffset Fecha { get; set; }
    public Usuario Autor { get; set; } = null!;
    public ICollection<AvisoMensajeDocumento> Avisos { get; } = [];
}

public sealed class AvisoMensajeDocumento
{
    public Guid MensajeId { get; set; }
    public Guid DestinatarioId { get; set; }
    public MensajeDocumento Mensaje { get; set; } = null!;
    public DateTimeOffset? FechaLectura { get; set; }
}
