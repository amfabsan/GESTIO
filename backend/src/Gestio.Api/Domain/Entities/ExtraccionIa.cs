using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Domain.Entities;

public sealed class ExtraccionIa
{
    public Guid Id { get; set; }

    public Guid DocumentoId { get; set; }

    public string? EmisorNombre { get; set; }

    public string? EmisorNif { get; set; }

    public DateOnly? FechaEmision { get; set; }

    public decimal? BaseImponible { get; set; }

    public decimal? TipoIva { get; set; }

    public decimal? CuotaIva { get; set; }

    public decimal? Total { get; set; }

    public string? CategoriaSugerida { get; set; }

    public NivelConfianza? NivelConfianza { get; set; }

    public string? DatosCrudosJson { get; set; }

    public Documento Documento { get; set; } = null!;
}
