using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Domain.Entities;

/// <summary>
/// Registro de facturación de alta encadenado (simulación académica de VeriFactu, no certificada ni conectada a la AEAT).
/// Guarda una instantánea de los datos que entran en la huella para poder recalcularla sin depender de la factura.
/// Inmutable una vez creado.
/// </summary>
public sealed class RegistroFacturacion
{
    public Guid Id { get; set; }
    public Guid AutonomoId { get; set; }
    public Guid FacturaId { get; set; }

    /// <summary>Posición en la cadena del autónomo: 1 es el inicio de cadena.</summary>
    public int Secuencia { get; set; }

    public TipoRegistroFacturacion Tipo { get; set; } = TipoRegistroFacturacion.ALTA;

    // Datos de la factura que entran en la huella (instantánea en el momento de generar el registro).
    public required string IdEmisorFactura { get; set; }
    public required string NumSerieFactura { get; set; }
    public DateOnly FechaExpedicionFactura { get; set; }
    public required string TipoFactura { get; set; }
    public decimal CuotaTotal { get; set; }
    public decimal ImporteTotal { get; set; }

    /// <summary>Texto exacto que entra en la huella: hora de Europe/Madrid con su desfase, p. ej. 2026-06-16T15:24:01+02:00.</summary>
    public required string FechaHoraHusoGenRegistro { get; set; }

    /// <summary>Mismo instante que <see cref="FechaHoraHusoGenRegistro"/>, truncado a segundos.</summary>
    public DateTimeOffset Generado { get; set; }

    public Guid? AnteriorId { get; set; }
    public string? HuellaAnterior { get; set; }
    public required string Huella { get; set; }
}
