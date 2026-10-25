using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Services;

public sealed record AnomaliaDetectada(TipoAnomalia Tipo, string Campo, string Descripcion);

// Reglas explicables (PB-10) sobre los valores vigentes de la extracción. Solo avisan de qué dato
// revisar: no corrigen importes ni deciden si la factura es fiscalmente válida.
public static class DetectorAnomalias
{
    private enum TipoComprobante { FACTURA, TICKET, OTRO }

    // Diferencia máxima admitida entre el importe calculado y el consignado.
    private const decimal Tolerancia = 0.01m;

    // Datos exigidos según el tipo de documento. A un ticket o factura simplificada no se le
    // exige el NIF del emisor; basta con cambiarlo aquí para hacerlo obligatorio.
    private static readonly Dictionary<TipoComprobante, bool> NifEmisorRequerido = new()
    {
        [TipoComprobante.FACTURA] = true,
        [TipoComprobante.TICKET] = false,
        [TipoComprobante.OTRO] = false,
    };

    // Tipos que gestionan estas reglas: el resto (duplicados, baja confianza…) tiene su propio origen.
    public static bool EsDeEstasReglas(TipoAnomalia tipo) =>
        tipo is TipoAnomalia.NIF_AUSENTE or TipoAnomalia.DESCUADRE_ARITMETICO;

    public static IReadOnlyList<AnomaliaDetectada> Detectar(ExtraccionIa extraccion)
    {
        var texto = TextoReconocido(extraccion);
        var detectadas = new List<AnomaliaDetectada>();

        if (string.IsNullOrWhiteSpace(extraccion.EmisorNif) && NifEmisorRequerido[Clasificar(texto)])
            detectadas.Add(new(
                TipoAnomalia.NIF_AUSENTE,
                nameof(ExtraccionIa.EmisorNif),
                "Falta el NIF del emisor, que es obligatorio en una factura. Compruébalo en el documento original."));

        // Si falta algún importe no se comprueba nada: ni se da por correcto ni se inventa el dato que falta.
        // Con varias bases, el tipo de IVA no describe todo el documento.
        var variasBases = extraccion.DesglosesIva.Count > 1;

        if (extraccion.BaseImponible is { } b && extraccion.TipoIva is { } t && extraccion.CuotaIva is { } cuota && !variasBases)
        {
            var calculada = Math.Round(b * t / 100m, 2, MidpointRounding.AwayFromZero);
            if (Math.Abs(calculada - cuota) > Tolerancia)
                detectadas.Add(new(
                    TipoAnomalia.DESCUADRE_ARITMETICO,
                    nameof(ExtraccionIa.CuotaIva),
                    $"La cuota de IVA ({Euros(cuota)}) no coincide con el {t:0.##} % de la base imponible ({Euros(b)}), que da {Euros(calculada)}."));
        }

        // Retenciones, recargos o varias bases cambian el total: la suma simple no sirve para comprobarlo.
        if (extraccion.BaseImponible is { } bt && extraccion.CuotaIva is { } ct && extraccion.Total is { } total &&
            !variasBases && !TieneOtrosConceptos(texto) && Math.Abs(bt + ct - total) > Tolerancia)
            detectadas.Add(new(
                TipoAnomalia.DESCUADRE_ARITMETICO,
                nameof(ExtraccionIa.Total),
                $"El total ({Euros(total)}) no coincide con la base imponible más la cuota de IVA ({Euros(bt)} + {Euros(ct)} = {Euros(bt + ct)})."));

        return detectadas;
    }

    // Un ticket o factura simplificada puede llevar también la palabra «factura», por eso se mira antes.
    private static TipoComprobante Clasificar(string texto) =>
        Regex.IsMatch(texto, @"\b(?:factura simplificada|fra\.? simplificada|fac\.? simplif|ticket|tiquet)")
            ? TipoComprobante.TICKET
            : Regex.IsMatch(texto, @"\bfactura\b") ? TipoComprobante.FACTURA : TipoComprobante.OTRO;

    private static bool TieneOtrosConceptos(string texto) =>
        Regex.IsMatch(texto, @"\b(?:retencion|irpf|recargo de equivalencia)\b");

    // Texto leído del original, en minúsculas y sin tildes, que el procesamiento guarda en DatosCrudosJson.
    private static string TextoReconocido(ExtraccionIa extraccion)
    {
        if (string.IsNullOrWhiteSpace(extraccion.DatosCrudosJson)) return string.Empty;
        try
        {
            using var json = JsonDocument.Parse(extraccion.DatosCrudosJson);
            return json.RootElement.TryGetProperty("texto", out var texto) && texto.ValueKind == JsonValueKind.String
                ? Normalizar(texto.GetString()!)
                : string.Empty;
        }
        catch (JsonException) { return string.Empty; }
    }

    private static string Normalizar(string texto) => new(texto.ToLowerInvariant().Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());

    private static string Euros(decimal importe) =>
        importe.ToString("N2", CultureInfo.GetCultureInfo("es-ES")) + " €";
}
