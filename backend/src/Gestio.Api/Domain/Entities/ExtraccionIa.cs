using Gestio.Api.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Gestio.Api.Domain.Entities;

public sealed class ExtraccionIa
{
    private IReadOnlyList<DesgloseIva>? desgloses;
    private IReadOnlyList<string>? advertencias;
    private IReadOnlyDictionary<string, NivelConfianza>? confianzaCampos;

    // Se conservan con la evidencia y las propuestas en el JSON existente.
    [NotMapped]
    public IReadOnlyList<DesgloseIva> DesglosesIva
    {
        get => desgloses ?? Leer<DesgloseIva[]>("desglosesIva") ?? [];
        set => desgloses = value;
    }

    [NotMapped]
    public IReadOnlyList<string> Advertencias
    {
        get => advertencias ?? Leer<string[]>("advertencias") ?? [];
        set => advertencias = value;
    }

    // Confianza de cada campo propuesto, según la lectura del motor que lo respalda.
    // Un campo que no aparece no tenía puntuación disponible: no se inventa.
    [NotMapped]
    public IReadOnlyDictionary<string, NivelConfianza> ConfianzaCampos
    {
        get => confianzaCampos ?? Leer<Dictionary<string, NivelConfianza>>("confianzaCampos") ?? [];
        set => confianzaCampos = value;
    }

    private T? Leer<T>(string propiedad) where T : class
    {
        if (string.IsNullOrWhiteSpace(DatosCrudosJson)) return null;
        try
        {
            using var json = JsonDocument.Parse(DatosCrudosJson);
            return json.RootElement.TryGetProperty(propiedad, out var valor) ? valor.Deserialize<T>() : null;
        }
        catch (JsonException) { return null; }
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
