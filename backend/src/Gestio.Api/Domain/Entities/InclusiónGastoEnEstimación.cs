namespace Gestio.Api.Domain.Entities;

/// <summary>
/// Marca explícita de inclusión de un gasto en la estimación de IVA.
/// Solo existe si el documento está clasificado como GASTO y en estado VALIDADO.
/// </summary>
public sealed class InclusiónGastoEnEstimación
{
    public Guid Id { get; set; }

    public Guid DocumentoId { get; set; }

    /// <summary>
    /// Si true, el IVA del gasto se resta en la estimación del trimestre.
    /// Si false, el documento aparece en la estimación pero su IVA no se incluye.
    /// </summary>
    public bool Incluida { get; set; }

    public DateTimeOffset FechaCambio { get; set; }

    public Guid ActualizadoPorId { get; set; }

    public Documento Documento { get; set; } = null!;

    public Usuario ActualizadoPor { get; set; } = null!;
}
