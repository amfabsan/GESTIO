using Gestio.Api.Common;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Services;

public static class CatalogoChecklistDemo
{
    public const string Aviso = "Plantilla de demostración; no constituye una lista oficial de requisitos fiscales.";

    public sealed record Entrada(string Codigo, string Descripcion, bool Obligatorio, CategoriaDocumento CategoriaEsperada);

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<Entrada>> Plantillas =
        new Dictionary<string, IReadOnlyList<Entrada>>(StringComparer.Ordinal)
        {
            ["111"] =
            [
                new("retenciones_trimestre", "Resumen de retenciones del trimestre", true, CategoriaDocumento.GASTO),
                new("justificantes_retenciones", "Justificantes adicionales de retenciones", false, CategoriaDocumento.GASTO)
            ],
            ["115"] =
            [
                new("recibos_alquiler", "Recibos del alquiler del local", true, CategoriaDocumento.GASTO),
                new("contrato_alquiler", "Contrato de arrendamiento vigente", false, CategoriaDocumento.OTRO)
            ],
            ["130"] =
            [
                new("cierre_caja_ventas", "Cierres Z de caja / resumen de ventas", true, CategoriaDocumento.INGRESO),
                new("gastos_personal_seguridad_social", "Gastos de personal y Seguridad Social", true, CategoriaDocumento.GASTO),
                new("cuota_reta", "Recibos de la cuota de autónomo (RETA)", true, CategoriaDocumento.GASTO),
                new("facturas_inversiones", "Facturas de inversiones (amortizaciones)", false, CategoriaDocumento.GASTO)
            ],
            ["131"] =
            [
                new("resumen_modulos", "Resumen de ingresos y módulos del trimestre", true, CategoriaDocumento.INGRESO),
                new("justificantes_gastos", "Justificantes de gastos del trimestre", false, CategoriaDocumento.GASTO)
            ],
            ["303"] =
            [
                new("facturas_emitidas", "Facturas emitidas del trimestre", true, CategoriaDocumento.INGRESO),
                new("facturas_proveedores", "Facturas de proveedores (compras)", true, CategoriaDocumento.GASTO),
                new("recibos_suministros", "Recibos de suministros (luz, agua, gas)", true, CategoriaDocumento.GASTO),
                new("tickets_otros_gastos", "Tickets y otros gastos del trimestre", false, CategoriaDocumento.GASTO)
            ]
        };

    public static IReadOnlyList<Entrada> Para(ObligacionFiscal obligacion) =>
        Plantillas.TryGetValue(obligacion.Modelo, out var entradas) ? entradas : [];

    public static bool PeriodoCoincide(ObligacionFiscal obligacion, DateOnly? fechaEmision)
    {
        if (fechaEmision is null || obligacion.Periodo.Length != 2 || obligacion.Periodo[1] != 'T' ||
            !char.IsAsciiDigit(obligacion.Periodo[0]))
            return false;

        var trimestre = obligacion.Periodo[0] - '0';
        return trimestre is >= 1 and <= 4 &&
            CalculadorTrimestre.PerteneceAlTrimestre(fechaEmision.Value, obligacion.Ejercicio, trimestre);
    }
}
