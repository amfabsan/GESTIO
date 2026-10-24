namespace Gestio.Api.Models.Responses;

public sealed record PaginaResponse<T>(IReadOnlyList<T> Elementos, int Pagina, int TamanoPagina, int Total, int TotalPaginas);
