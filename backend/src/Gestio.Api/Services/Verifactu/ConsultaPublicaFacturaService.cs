using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Services.Verifactu;

/// <summary>
/// Los únicos datos que la página pública del QR puede ver (PB-23 req. 3, decisión D6):
/// emisor (solo nombre), serie y número, fecha de expedición y total. Nada de cliente, líneas, ids ni NIF.
/// </summary>
public sealed record FacturaPublica(string EmisorNombre, string SerieNumero, DateOnly Fecha, decimal Total);

/// <summary>
/// UT23 (PB-23). Consulta sin sesión a partir del código del QR. No recibe usuario: no puede depender de él.
/// Req. 7 / D6: código malformado, inexistente o de una factura no emitida dan el mismo error.
/// </summary>
public sealed class ConsultaPublicaFacturaService(ApplicationDbContext context)
{
    public static readonly Error EnlaceNoValido = new("FacturaPublica.EnlaceNoValido",
        "Enlace no válido. No hay ninguna factura asociada a este código.", TipoError.NO_ENCONTRADO);

    public async Task<Resultado<FacturaPublica>> ConsultarAsync(string? codigo, CancellationToken ct)
    {
        if (!CodigoPublicoFactura.EsFormatoValido(codigo)) return EnlaceNoValido;

        // Proyección explícita: solo salen de la BD los campos permitidos.
        var datos = await context.Facturas.AsNoTracking()
            .Where(f => f.CodigoPublico == codigo && f.Estado == EstadoFactura.EMITIDA)
            .Select(f => new { f.EmisorNombre, f.Serie, f.Numero, f.Fecha, f.Total })
            .SingleOrDefaultAsync(ct);

        // Una factura emitida siempre tiene número, nombre de emisor y total; si faltara alguno, no se enseña nada.
        if (datos is null || datos.Numero is null || datos.EmisorNombre is null || datos.Total is null) return EnlaceNoValido;

        // Mismo formato que FacturaResponse.NumeroVisible (las pruebas comprueban que coinciden).
        return new FacturaPublica(datos.EmisorNombre, $"{datos.Serie}-{datos.Numero.Value:D4}", datos.Fecha, datos.Total.Value);
    }
}
