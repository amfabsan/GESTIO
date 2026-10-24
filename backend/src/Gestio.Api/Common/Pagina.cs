namespace Gestio.Api.Common;

// Porción de un listado largo junto con lo necesario para pedir las siguientes.
public sealed record Pagina<T>(IReadOnlyList<T> Elementos, int NumeroPagina, int TamanoPagina, int Total)
{
    public int TotalPaginas => (int)Math.Ceiling(Total / (double)TamanoPagina);
}
