using Gestio.Api.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Gestio.Api.Domain.Entities;

public sealed class ExtraccionIa
{
    private IReadOnlyList<DesgloseIva>? desgloses;
    private IReadOnlyList<string>? advertencias;

    // Se conservan con la evidencia y las propuestas en el JSON existente.
    [NotMapped]
    public IReadOnlyList<DesgloseIva> DesglosesIva
    {
        get => desgloses ?? LeerLista<DesgloseIva>("desglosesIva");
        set => desgloses = value;
    }

    [NotMapped]
    public IReadOnlyList<string> Advertencias
    {
        get => advertencias ?? LeerLista<string>("advertencias");
        set => advertencias = value;
    }

    private IReadOnlyList<T> LeerLista<T>(string propiedad)
    {
        if (string.IsNullOrWhiteSpace(DatosCrudosJson)) return [];
        try
        {
            using var json = JsonDocument.Parse(DatosCrudosJson);
            return json.RootElement.TryGetProperty(propiedad, out var lista)
                ? lista.Deserialize<T[]>() ?? [] : [];
        }
        catch (JsonException) { return []; }
    }
    public Guid Id { get; set; }

    public Guid DocumentoId { get; set; }

    public string? NumeroFactura { get; set; }

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

public sealed record DesgloseIva(decimal? BaseImponible, decimal? TipoIva, decimal? CuotaIva);
