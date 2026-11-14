namespace Gestio.Api.Models.Responses;

/// <summary>
/// Elemento excluido del cálculo de estimación con su motivo.
/// </summary>
public sealed record ElementoPorRevisarResponse(
    Guid DocumentoId,
    string Motivo,
    string Severidad);
