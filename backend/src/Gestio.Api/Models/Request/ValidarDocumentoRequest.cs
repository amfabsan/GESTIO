namespace Gestio.Api.Models.Requests;

public sealed record ValidarDocumentoRequest(
    IReadOnlyList<CampoValidacionRequest> Campos);

public sealed record CampoValidacionRequest(
    string Campo,
    string? Valor);
