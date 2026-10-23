namespace Gestio.Api.Domain.Entities;

public sealed class HistorialValidacion
{
    public Guid Id { get; set; }

    public Guid DocumentoId { get; set; }

    public Documento Documento { get; set; } = null!;

    public Guid UsuarioId { get; set; }

    public string Campo { get; set; } = string.Empty;

    public string? ValorAnterior { get; set; }

    public string? ValorNuevo { get; set; }

    public string TipoCambio { get; set; } = "CORRECCION";

    public DateTimeOffset FechaCambio { get; set; }
}
