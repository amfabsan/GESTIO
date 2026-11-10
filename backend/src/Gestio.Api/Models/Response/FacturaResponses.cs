using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Services;

namespace Gestio.Api.Models.Response;

public sealed record DesgloseIvaResponse(decimal Tipo, decimal Base, decimal Cuota);

public sealed record LineaFacturaResponse(
    Guid Id,
    int Orden,
    string? Descripcion,
    decimal? Cantidad,
    decimal? PrecioUnitario,
    decimal? IvaPorcentaje,
    decimal? Base);

public sealed record FacturaResponse(
    Guid Id,
    Guid AutonomoId,
    Guid? ContactoId,
    string Estado,
    string Serie,
    int? Numero,
    string? NumeroVisible,
    DateOnly Fecha,
    decimal? RetencionPorcentaje,
    decimal? BaseImponible,
    IReadOnlyList<DesgloseIvaResponse> DesgloseIva,
    decimal? CuotaIva,
    decimal? ImporteRetencion,
    decimal? Total,
    string? EmisorNombre,
    string? EmisorNif,
    string? EmisorDireccion,
    string? ClienteNombre,
    string? ClienteNif,
    string? ClienteDireccion,
    DateTimeOffset? Emitida,
    IReadOnlyList<LineaFacturaResponse> Lineas)
{
    public static FacturaResponse Desde(Factura factura, CalculoFactura? calculo = null)
    {
        var emitida = factura.Estado == EstadoFactura.EMITIDA;
        var nombreEmisor = emitida
            ? factura.EmisorNombre
            : $"{factura.Autonomo.Nombre} {factura.Autonomo.PrimerApellido} {factura.Autonomo.SegundoApellido}".Trim();
        var nombreCliente = emitida ? factura.ClienteNombre : factura.Contacto?.Nombre;
        var nifCliente = emitida ? factura.ClienteNif : factura.Contacto?.Nif;
        var direccionCliente = emitida ? factura.ClienteDireccion : factura.Contacto?.Direccion;
        return new FacturaResponse(
            factura.Id,
            factura.AutonomoId,
            factura.ContactoId,
            factura.Estado.ToString(),
            factura.Serie,
            factura.Numero,
            factura.Numero is null ? null : $"{factura.Serie}-{factura.Numero.Value:D4}",
            factura.Fecha,
            factura.RetencionPorcentaje,
            calculo?.BaseImponible ?? factura.BaseImponible,
            calculo?.DesgloseIva.Select(x => new DesgloseIvaResponse(x.Tipo, x.Base, x.Cuota)).ToArray() ?? Array.Empty<DesgloseIvaResponse>(),
            calculo?.CuotaIva ?? factura.CuotaIva,
            calculo?.ImporteRetencion ?? factura.ImporteRetencion,
            calculo?.Total ?? factura.Total,
            nombreEmisor,
            emitida ? factura.EmisorNif : factura.Autonomo.Nif,
            emitida ? factura.EmisorDireccion : factura.Autonomo.DireccionFiscal,
            nombreCliente,
            nifCliente,
            direccionCliente,
            factura.Emitida,
            factura.Lineas.OrderBy(x => x.Orden).Select(x => new LineaFacturaResponse(
                x.Id,
                x.Orden,
                x.Descripcion,
                x.Cantidad,
                x.PrecioUnitario,
                x.IvaPorcentaje,
                x.Cantidad is not null && x.PrecioUnitario is not null
                    ? CalculadoraFactura.Redondear(x.Cantidad.Value * x.PrecioUnitario.Value)
                    : null)).ToArray());
    }
}

public sealed record FacturaListaResponse(
    Guid Id,
    string Estado,
    string Serie,
    int? Numero,
    string? NumeroVisible,
    DateOnly Fecha,
    Guid? ContactoId,
    string? ClienteNombre,
    string? ClienteNif,
    string? ClienteDireccion,
    int NumeroLineas,
    decimal? BaseImponible,
    decimal? Total);

public sealed record DireccionContactoFacturaResponse(Guid Id, string Nombre, string Nif, string? Direccion, bool Activo);
