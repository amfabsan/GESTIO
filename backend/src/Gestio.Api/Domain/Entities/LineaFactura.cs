namespace Gestio.Api.Domain.Entities;

public sealed class LineaFactura
{
    public Guid Id { get; set; }
    public Guid FacturaId { get; set; }
    public required Factura Factura { get; set; }
    public int Orden { get; set; }
    public string? Descripcion { get; set; }
    public decimal? Cantidad { get; set; }
    public decimal? PrecioUnitario { get; set; }
    public decimal? IvaPorcentaje { get; set; }
}