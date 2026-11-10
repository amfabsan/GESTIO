namespace Gestio.Api.Services;

public sealed record LineaCalculoFactura(string? Descripcion, decimal? Cantidad, decimal? PrecioUnitario, decimal? IvaPorcentaje);

public sealed record DesgloseIvaFactura(decimal Tipo, decimal Base, decimal Cuota);

public sealed record CalculoFactura(
    decimal BaseImponible,
    IReadOnlyList<DesgloseIvaFactura> DesgloseIva,
    decimal CuotaIva,
    decimal ImporteRetencion,
    decimal Total);

public sealed record ResultadoCalculoFactura(CalculoFactura? Calculo, IReadOnlyList<string> Errores)
{
    public bool EsValido => Calculo is not null && Errores.Count == 0;
}

public static class CalculadoraFactura
{
    public static readonly IReadOnlySet<decimal> TiposIva = new HashSet<decimal> { 0m, 4m, 10m, 21m };

    public static decimal Redondear(decimal importe) => decimal.Round(importe, 2, MidpointRounding.AwayFromZero);

    public static ResultadoCalculoFactura Calcular(
        IReadOnlyCollection<LineaCalculoFactura> lineas,
        decimal? retencionPorcentaje)
    {
        var errores = new List<string>();
        if (lineas.Count == 0)
            errores.Add("Factura.LineasObligatorias");

        if (retencionPorcentaje is < 0m or > 100m)
            errores.Add("Factura.RetencionNoValida");

        var basesPorIva = new Dictionary<decimal, decimal>();
        var todasLasLineasValidas = true;

        foreach (var linea in lineas)
        {
            if (string.IsNullOrWhiteSpace(linea.Descripcion))
            {
                errores.Add("Factura.Linea.DescripcionObligatoria");
                todasLasLineasValidas = false;
            }
            if (linea.Cantidad is null or <= 0m)
            {
                errores.Add("Factura.Linea.CantidadNoValida");
                todasLasLineasValidas = false;
            }
            if (linea.PrecioUnitario is null or < 0m)
            {
                errores.Add("Factura.Linea.PrecioNoValido");
                todasLasLineasValidas = false;
            }
            if (linea.IvaPorcentaje is null || !TiposIva.Contains(linea.IvaPorcentaje.Value))
            {
                errores.Add("Factura.Linea.IvaNoValido");
                todasLasLineasValidas = false;
            }

            if (linea.Cantidad is > 0m && linea.PrecioUnitario is >= 0m && linea.IvaPorcentaje is { } tipoIva && TiposIva.Contains(tipoIva))
            {
                var baseLinea = linea.Cantidad.Value * linea.PrecioUnitario.Value;
                basesPorIva[tipoIva] = basesPorIva.GetValueOrDefault(tipoIva) + baseLinea;
            }
        }

        if (!todasLasLineasValidas || errores.Count > 0)
            return new ResultadoCalculoFactura(null, errores);

        var desglose = basesPorIva
            .OrderBy(x => x.Key)
            .Select(x => new DesgloseIvaFactura(x.Key, Redondear(x.Value), Redondear(Redondear(x.Value) * x.Key / 100m)))
            .ToArray();
        var baseImponible = Redondear(desglose.Sum(x => x.Base));
        var cuotaIva = Redondear(desglose.Sum(x => x.Cuota));
        var importeRetencion = retencionPorcentaje is > 0m
            ? Redondear(baseImponible * retencionPorcentaje.Value / 100m)
            : 0m;
        var total = Redondear(baseImponible + cuotaIva - importeRetencion);

        return new ResultadoCalculoFactura(
            new CalculoFactura(baseImponible, desglose, cuotaIva, importeRetencion, total),
            Array.Empty<string>());
    }
}