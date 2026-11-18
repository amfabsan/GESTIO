namespace Gestio.Api.Models.Responses;

/// <summary>
/// Detalles de un documento incluido en el cálculo de la estimación de IVA.
/// </summary>
public sealed record EstimacionDocumentoResponse(
    Guid DocumentoId,
    string Tipo,
    string? Emisor,
    string? Nif,
    string? NumeroFactura,
    decimal IvaIncluido);
