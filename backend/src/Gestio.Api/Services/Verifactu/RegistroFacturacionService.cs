using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Models.Response;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Services.Verifactu;

/// <summary>
/// Registros de facturación encadenados (simulación académica de VeriFactu, no certificada ni conectada a la AEAT).
/// Crear: solo desde la emisión de una factura (FacturacionService.EmitirAsync). Consultar y verificar: propietario
/// o gestor vinculado. No existe ninguna operación de modificación ni de borrado (req. 7).
/// </summary>
public sealed class RegistroFacturacionService(ApplicationDbContext context)
{
    private static readonly Error Prohibido = new("RegistroFacturacion.Prohibido",
        "No tienes permiso para consultar los registros de facturación de este autónomo.", TipoError.PROHIBIDO);

    /// <summary>
    /// Añade al contexto, SIN guardar, el registro de alta de una factura recién emitida, enlazado con el último
    /// registro del autónomo. Debe llamarse dentro de la transacción que bloquea al autónomo (SELECT … FOR UPDATE
    /// en GeneradorObligaciones.IniciarAsync), para que dos emisiones simultáneas no lean el mismo «último».
    /// El llamador guarda factura y registro en un único SaveChanges.
    /// </summary>
    public async Task<RegistroFacturacion> PrepararAltaAsync(Factura factura, DateTimeOffset instante, CancellationToken ct)
    {
        if (context.Database.IsRelational() && context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "El registro de alta debe prepararse dentro de la transacción que bloquea al autónomo.");
        if (factura.Estado != EstadoFactura.EMITIDA || factura.Numero is null || string.IsNullOrWhiteSpace(factura.EmisorNif) ||
            factura.BaseImponible is null || factura.CuotaIva is null)
            throw new InvalidOperationException("Solo se genera registro de alta para una factura emitida, numerada y con importes calculados.");

        var anterior = await context.RegistrosFacturacion
            .Where(r => r.AutonomoId == factura.AutonomoId)
            .OrderByDescending(r => r.Secuencia)
            .FirstOrDefaultAsync(ct);

        var registro = new RegistroFacturacion
        {
            Id = Guid.NewGuid(),
            AutonomoId = factura.AutonomoId,
            FacturaId = factura.Id,
            Secuencia = (anterior?.Secuencia ?? 0) + 1,
            Tipo = TipoRegistroFacturacion.ALTA,
            IdEmisorFactura = factura.EmisorNif,
            NumSerieFactura = NumSerieFactura(factura.Serie, factura.Numero.Value),
            FechaExpedicionFactura = factura.Fecha,
            TipoFactura = HuellaVerifactu.TipoFacturaCompleta,
            CuotaTotal = factura.CuotaIva.Value,
            // D6: importe total de la factura SIN descontar la retención de IRPF (base + IVA).
            ImporteTotal = factura.BaseImponible.Value + factura.CuotaIva.Value,
            Generado = HuellaVerifactu.TruncarASegundos(instante),
            FechaHoraHusoGenRegistro = HuellaVerifactu.FormatearFechaHoraHuso(instante),
            AnteriorId = anterior?.Id,
            HuellaAnterior = anterior?.Huella,
            Huella = ""
        };
        registro.Huella = HuellaVerifactu.Calcular(DatosHuellaAlta.De(registro));
        context.RegistrosFacturacion.Add(registro);
        return registro;
    }

    /// <summary>Mismo formato que el número visible de la factura (FacturaResponse.NumeroVisible).</summary>
    public static string NumSerieFactura(string serie, int numero) => $"{serie}-{numero:D4}";

    public async Task<Resultado<IReadOnlyList<RegistroFacturacionResponse>>> ListarAsync(
        UsuarioActual usuario, Guid autonomoId, CancellationToken ct)
    {
        if (!await AccesoFacturacion.PuedeConsultarAsync(context, usuario, autonomoId, ct)) return Prohibido;

        var filas = await context.RegistrosFacturacion.AsNoTracking()
            .Where(r => r.AutonomoId == autonomoId)
            .OrderBy(r => r.Secuencia)
            .Select(r => new
            {
                Registro = r,
                Cliente = context.Facturas.Where(f => f.Id == r.FacturaId).Select(f => f.ClienteNombre).FirstOrDefault()
            })
            .ToListAsync(ct);

        var secuenciaPorId = filas.ToDictionary(f => f.Registro.Id, f => f.Registro.Secuencia);
        return filas.Select(f => Mapear(f.Registro, f.Cliente, secuenciaPorId)).ToList();
    }

    public async Task<Resultado<VerificacionCadenaResponse>> VerificarAsync(
        UsuarioActual usuario, Guid autonomoId, CancellationToken ct)
    {
        if (!await AccesoFacturacion.PuedeConsultarAsync(context, usuario, autonomoId, ct)) return Prohibido;

        var registros = await context.RegistrosFacturacion.AsNoTracking()
            .Where(r => r.AutonomoId == autonomoId)
            .ToListAsync(ct);
        var resultado = VerificadorCadena.Verificar(registros);
        var incoherencia = resultado.Incoherencia;
        return new VerificacionCadenaResponse(resultado.Coherente, resultado.Revisados, registros.Count,
            incoherencia?.RegistroId, incoherencia?.Secuencia, incoherencia?.Codigo, incoherencia?.Motivo);
    }

    private static RegistroFacturacionResponse Mapear(
        RegistroFacturacion r, string? cliente, IReadOnlyDictionary<Guid, int> secuenciaPorId) => new(
        r.Id, r.Secuencia, r.Tipo.ToString(), r.FacturaId, cliente, r.IdEmisorFactura, r.NumSerieFactura,
        r.FechaExpedicionFactura, r.TipoFactura, r.CuotaTotal, r.ImporteTotal, r.FechaHoraHusoGenRegistro, r.Generado,
        r.AnteriorId, r.AnteriorId is { } anteriorId && secuenciaPorId.TryGetValue(anteriorId, out var s) ? s : null,
        r.HuellaAnterior, r.Huella, HuellaVerifactu.Canonico(DatosHuellaAlta.De(r)));
}
