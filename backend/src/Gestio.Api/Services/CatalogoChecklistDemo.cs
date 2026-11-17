using Gestio.Api.Domain.Entities;

namespace Gestio.Api.Services;

public static class CatalogoChecklistDemo
{
    public const string Aviso = "Plantilla de demostración; no constituye una lista oficial de requisitos fiscales.";

    public sealed record Entrada(string Codigo, string Descripcion, bool Obligatorio);

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<Entrada>> Plantillas =
        new Dictionary<string, IReadOnlyList<Entrada>>(StringComparer.Ordinal)
        {
            ["111"] =
            [
                new("retenciones_trimestre", "Resumen de retenciones del trimestre", true),
                new("justificantes_retenciones", "Justificantes adicionales de retenciones", false)
            ],
            ["115"] =
            [
                new("recibos_alquiler", "Recibos del alquiler del local", true),
                new("contrato_alquiler", "Contrato de arrendamiento vigente", false)
            ],
            ["130"] =
            [
                new("cierre_caja_ventas", "Cierres Z de caja / resumen de ventas", true),
                new("gastos_personal_seguridad_social", "Gastos de personal y Seguridad Social", true),
                new("cuota_reta", "Recibos de la cuota de autónomo (RETA)", true),
                new("facturas_inversiones", "Facturas de inversiones (amortizaciones)", false)
            ],
            ["131"] =
            [
                new("resumen_modulos", "Resumen de ingresos y módulos del trimestre", true),
                new("justificantes_gastos", "Justificantes de gastos del trimestre", false)
            ],
            ["303"] =
            [
                new("facturas_emitidas", "Facturas emitidas del trimestre", true),
                new("facturas_proveedores", "Facturas de proveedores (compras)", true),
                new("recibos_suministros", "Recibos de suministros (luz, agua, gas)", true),
                new("tickets_otros_gastos", "Tickets y otros gastos del trimestre", false)
            ]
        };

    public static IReadOnlyList<Entrada> Para(ObligacionFiscal obligacion) =>
        Plantillas.TryGetValue(obligacion.Modelo, out var entradas) ? entradas : [];
}
