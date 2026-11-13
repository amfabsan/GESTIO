using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Domain.Entities;

public sealed class Factura
{
    public Guid Id { get; set; }
    public Guid AutonomoId { get; set; }
    public Guid? ContactoId { get; set; }
    public DateOnly Fecha { get; set; }
    public required string Serie { get; set; }
    public int? Numero { get; set; }
    public EstadoFactura Estado { get; set; } = EstadoFactura.BORRADOR;
    public decimal? RetencionPorcentaje { get; set; }
    public decimal? BaseImponible { get; set; }
    public decimal? CuotaIva { get; set; }
    public decimal? ImporteRetencion { get; set; }
    public decimal? Total { get; set; }
    public string? EmisorNombre { get; set; }
    public string? EmisorNif { get; set; }
    public string? EmisorDireccion { get; set; }
    public string? ClienteNombre { get; set; }
    public string? ClienteNif { get; set; }
    public string? ClienteDireccion { get; set; }
    public DateTimeOffset Creada { get; set; }
    public DateTimeOffset Actualizada { get; set; }
    public DateTimeOffset? Emitida { get; set; }
    public Usuario Autonomo { get; set; } = null!;
    public Contacto? Contacto { get; set; }
    public ICollection<LineaFactura> Lineas { get; set; } = [];
}