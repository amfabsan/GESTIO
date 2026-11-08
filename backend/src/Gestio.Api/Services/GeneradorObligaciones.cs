using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Gestio.Api.Services;

public sealed class GeneradorObligaciones(ApplicationDbContext context, TimeProvider timeProvider)
{
    // PB-12 y la generación toman el mismo bloqueo antes de leer el perfil. También funciona antes
    // de la primera generación, cuando todavía no existe ninguna fila de obligaciones que bloquear.
    public async Task<IDbContextTransaction?> IniciarAsync(Guid autonomoId, CancellationToken ct)
    {
        if (!context.Database.IsRelational()) return null;
        var transaccion = await context.Database.BeginTransactionAsync(ct);
        try
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM usuarios WHERE \"Id\" = {autonomoId} FOR UPDATE", ct);
            return transaccion;
        }
        catch
        {
            await transaccion.DisposeAsync();
            throw;
        }
    }

    // Solo prepara cambios: el llamador guarda el perfil y las obligaciones de manera atómica.
    public async Task SincronizarAsync(PerfilFiscal perfil, Guid usuarioId, bool soloSiGenerado, CancellationToken ct)
    {
        var existentes = await context.Set<ObligacionFiscal>().Include(o => o.Historial)
            .Where(o => o.AutonomoId == perfil.AutonomoId && o.Ejercicio == perfil.Ejercicio).ToListAsync(ct);
        if (soloSiGenerado && existentes.Count == 0) return;
        var previstas = CatalogoObligacionesDemo.Calcular(perfil);
        foreach (var obligacion in existentes.Where(o => o.Estado != EstadoObligacion.PRESENTADA))
        {
            if (!previstas.Any(p => p.Modelo == obligacion.Modelo && p.Periodo == obligacion.Periodo))
                context.Remove(obligacion);
        }
        foreach (var entrada in previstas)
        {
            var obligacion = existentes.SingleOrDefault(o => o.Modelo == entrada.Modelo && o.Periodo == entrada.Periodo);
            if (obligacion?.Estado == EstadoObligacion.PRESENTADA) continue;
            if (obligacion is null)
            {
                obligacion = new ObligacionFiscal
                {
                    Id = Guid.NewGuid(), AutonomoId = perfil.AutonomoId, Ejercicio = perfil.Ejercicio,
                    Modelo = entrada.Modelo, Nombre = entrada.Nombre, Periodo = entrada.Periodo, FechaPrevista = entrada.FechaPrevista
                };
                context.Add(obligacion);
                Registrar(obligacion, usuarioId, $"Generada con el perfil {perfil.Ejercicio} y el catálogo {CatalogoObligacionesDemo.Version}. Fecha prevista: {entrada.FechaPrevista:yyyy-MM-dd}.");
            }
            else if (obligacion.Nombre != entrada.Nombre || obligacion.FechaPrevista != entrada.FechaPrevista)
            {
                Registrar(obligacion, usuarioId, $"Actualización del catálogo: {obligacion.Nombre} ({obligacion.FechaPrevista:yyyy-MM-dd}) → {entrada.Nombre} ({entrada.FechaPrevista:yyyy-MM-dd}).");
                obligacion.Nombre = entrada.Nombre;
                obligacion.FechaPrevista = entrada.FechaPrevista;
            }
        }
    }

    private void Registrar(ObligacionFiscal obligacion, Guid usuarioId, string descripcion)
    {
        var evento = new HistorialObligacionFiscal
        {
            Id = Guid.NewGuid(), ObligacionFiscalId = obligacion.Id, UsuarioId = usuarioId,
            Fecha = timeProvider.GetUtcNow(), Descripcion = descripcion
        };
        obligacion.Historial.Add(evento);
        context.Add(evento);
    }
}
