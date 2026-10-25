using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Domain.Entities;

public sealed class Anomalia
{
    public Guid Id { get; set; }

    public Guid DocumentoId { get; set; }

    public Guid? DocumentoRelacionadoId { get; set; }

    public TipoAnomalia Tipo { get; set; }

    // Campo de la extracción que debe revisar el gestor (p. ej. "EmisorNif", "Total"), si la regla señala uno.
    public string? Campo { get; set; }

    public required string Descripcion { get; set; }

    public SeveridadAnomalia Severidad { get; set; }

    public bool Resuelta { get; set; }

    public DateTimeOffset FechaDeteccion { get; set; }

    public Documento Documento { get; set; } = null!;
}
