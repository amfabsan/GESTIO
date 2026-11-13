namespace Gestio.Api.Models.Request;

public sealed record GuardarFacturaRequest(
    Guid? ContactoId,
    DateOnly Fecha,
    decimal? RetencionPorcentaje,
    IReadOnlyList<GuardarLineaFacturaRequest>? Lineas);

public sealed record GuardarLineaFacturaRequest(
    string? Descripcion,
    decimal? Cantidad,
    decimal? PrecioUnitario,
    decimal? IvaPorcentaje);