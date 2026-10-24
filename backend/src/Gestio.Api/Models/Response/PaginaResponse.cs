using Gestio.Api.Common;

namespace Gestio.Api.Models.Responses;

public sealed record PaginaResponse<T>(IReadOnlyList<T> Elementos, int Pagina, int TamanoPagina, int Total, int TotalPaginas)
{
    public static PaginaResponse<T> Desde<TOrigen>(Pagina<TOrigen> pagina, Func<TOrigen, T> mapear) => new(
        pagina.Elementos.Select(mapear).ToList(),
        pagina.NumeroPagina,
        pagina.TamanoPagina,
        pagina.Total,
        pagina.TotalPaginas);
}
