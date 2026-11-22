using System.Data;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Gestio.Api.Services;

public sealed class ChecklistObligacionesService(
    ApplicationDbContext context,
    PerfilesFiscalesService perfiles,
    TimeProvider timeProvider)
{
    private static readonly Error Prohibido = new(
        "Checklist.Prohibido", "No tienes permiso para acceder a este checklist.", TipoError.PROHIBIDO);
    private static readonly Error NoEncontrada = new(
        "Checklist.ObligacionNoEncontrada", "No se ha encontrado esta obligación fiscal.", TipoError.NO_ENCONTRADO);
    private static readonly Error ElementoNoEncontrado = new(
        "Checklist.ElementoNoEncontrado", "No se ha encontrado este elemento del checklist.", TipoError.NO_ENCONTRADO);
    private static readonly Error PlantillaNoDisponible = new(
        "Checklist.PlantillaNoDisponible", "No hay una plantilla de demostración para el modelo de esta obligación.", TipoError.NO_DISPONIBLE);
    private static readonly Error ObligacionPresentada = new(
        "Checklist.ObligacionPresentada", "No se puede modificar el checklist de una obligación presentada.", TipoError.CONFLICTO);
    private static readonly Error ConfirmadoPorGestor = new(
        "Checklist.ConfirmadoPorGestor", "Solo el gestor puede devolver a pendiente un elemento confirmado.", TipoError.CONFLICTO);
    private static readonly Error EvidenciaConfirmada = new(
        "Checklist.EvidenciaConfirmada", "Retira primero la evidencia confirmada para devolver este elemento a pendiente.", TipoError.CONFLICTO);

    public async Task<Resultado<ObligacionFiscal>> ConsultarAsync(
        UsuarioActual usuario, Guid autonomoId, Guid obligacionId, CancellationToken ct)
    {
        if (!await PuedeConsultarAsync(usuario, autonomoId, ct)) return Prohibido;
        await using var transaccion = await BloquearAsync(obligacionId, ct);
        var obligacion = await CargarAsync(autonomoId, obligacionId, ct);
        if (obligacion is null) return NoEncontrada;
        if (CatalogoChecklistDemo.Para(obligacion).Count == 0) return PlantillaNoDisponible;

        var cambio = AsegurarElementos(obligacion);
        cambio |= ActualizarEstado(obligacion);
        if (cambio) await context.SaveChangesAsync(ct);
        if (transaccion is not null) await transaccion.CommitAsync(ct);
        return obligacion;
    }

    public async Task AsegurarCalendarioAsync(Guid autonomoId, int ejercicio, CancellationToken ct)
    {
        var obligaciones = await context.Set<ObligacionFiscal>().Include(o => o.Checklist)
            .Where(o => o.AutonomoId == autonomoId && o.Ejercicio == ejercicio).ToListAsync(ct);
        var cambio = false;
        foreach (var obligacion in obligaciones)
        {
            if (CatalogoChecklistDemo.Para(obligacion).Count == 0) continue;
            cambio |= AsegurarElementos(obligacion);
            cambio |= ActualizarEstado(obligacion);
        }
        if (cambio) await context.SaveChangesAsync(ct);
    }

    public async Task<Resultado<ObligacionFiscal>> MarcarAportadoAsync(
        UsuarioActual usuario, Guid autonomoId, Guid obligacionId, Guid elementoId, bool aportado, CancellationToken ct)
    {
        if (usuario.Rol != Rol.AUTONOMO || usuario.Id != autonomoId) return Prohibido;
        await using var transaccion = await BloquearAsync(obligacionId, ct);
        var obligacion = await CargarAsync(autonomoId, obligacionId, ct);
        if (obligacion is null) return NoEncontrada;
        if (CatalogoChecklistDemo.Para(obligacion).Count == 0) return PlantillaNoDisponible;
        var cambio = AsegurarElementos(obligacion);
        if (obligacion.Estado == EstadoObligacion.PRESENTADA) return ObligacionPresentada;

        var elemento = obligacion.Checklist.SingleOrDefault(e => e.Id == elementoId);
        if (elemento is null) return ElementoNoEncontrado;
        if (elemento.Estado == EstadoElementoChecklist.CONFIRMADO) return ConfirmadoPorGestor;

        var nuevoEstado = aportado ? EstadoElementoChecklist.APORTADO : EstadoElementoChecklist.PENDIENTE;
        cambio |= await RegistrarCambioAsync(elemento, nuevoEstado, usuario.Id, OrigenCambioChecklist.AUTONOMO, ct);
        cambio |= ActualizarEstado(obligacion);
        if (cambio) await context.SaveChangesAsync(ct);
        if (transaccion is not null) await transaccion.CommitAsync(ct);
        return obligacion;
    }

    public async Task<Resultado<ObligacionFiscal>> RevisarAsync(
        UsuarioActual usuario, Guid autonomoId, Guid obligacionId, Guid elementoId, bool confirmar, CancellationToken ct)
    {
        if (usuario.Rol != Rol.GESTOR || !await perfiles.PuedeAccederAsync(usuario, autonomoId, true, ct)) return Prohibido;
        await using var transaccion = await BloquearAsync(obligacionId, ct);
        var obligacion = await CargarAsync(autonomoId, obligacionId, ct);
        if (obligacion is null) return NoEncontrada;
        if (CatalogoChecklistDemo.Para(obligacion).Count == 0) return PlantillaNoDisponible;
        var cambio = AsegurarElementos(obligacion);
        if (obligacion.Estado == EstadoObligacion.PRESENTADA) return ObligacionPresentada;

        var elemento = obligacion.Checklist.SingleOrDefault(e => e.Id == elementoId);
        if (elemento is null) return ElementoNoEncontrado;
        if (!confirmar && elemento.VinculosEvidencia.Any(v => v.Estado == EstadoVinculoEvidencia.CONFIRMADA))
            return EvidenciaConfirmada;
        var nuevoEstado = confirmar ? EstadoElementoChecklist.CONFIRMADO : EstadoElementoChecklist.PENDIENTE;
        cambio |= await RegistrarCambioAsync(elemento, nuevoEstado, usuario.Id, OrigenCambioChecklist.GESTOR, ct);
        cambio |= ActualizarEstado(obligacion);
        if (cambio) await context.SaveChangesAsync(ct);
        if (transaccion is not null) await transaccion.CommitAsync(ct);
        return obligacion;
    }

    private async Task<bool> PuedeConsultarAsync(UsuarioActual usuario, Guid autonomoId, CancellationToken ct) =>
        usuario.Rol == Rol.AUTONOMO
            ? usuario.Id == autonomoId
            : usuario.Rol == Rol.GESTOR && await perfiles.PuedeAccederAsync(usuario, autonomoId, false, ct);

    private async Task<IDbContextTransaction?> BloquearAsync(Guid obligacionId, CancellationToken ct)
    {
        if (!context.Database.IsRelational()) return null;
        var transaccion = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM obligaciones_fiscales WHERE \"Id\" = {obligacionId} FOR UPDATE", ct);
            return transaccion;
        }
        catch
        {
            await transaccion.DisposeAsync();
            throw;
        }
    }

    private Task<ObligacionFiscal?> CargarAsync(Guid autonomoId, Guid obligacionId, CancellationToken ct) =>
        context.Set<ObligacionFiscal>().AsSplitQuery()
            .Include(o => o.Checklist).ThenInclude(e => e.UsuarioUltimoCambio)
            .Include(o => o.Checklist).ThenInclude(e => e.Historial).ThenInclude(h => h.Usuario)
            .Include(o => o.Checklist).ThenInclude(e => e.VinculosEvidencia).ThenInclude(v => v.Documento).ThenInclude(d => d.ExtraccionIa)
            .Include(o => o.Checklist).ThenInclude(e => e.VinculosEvidencia).ThenInclude(v => v.ConfirmadaPor)
            .Include(o => o.Checklist).ThenInclude(e => e.VinculosEvidencia).ThenInclude(v => v.Historial).ThenInclude(h => h.Usuario)
            .SingleOrDefaultAsync(o => o.Id == obligacionId && o.AutonomoId == autonomoId, ct);

    private bool AsegurarElementos(ObligacionFiscal obligacion)
    {
        var existentes = obligacion.Checklist.Select(e => e.CodigoPlantilla).ToHashSet(StringComparer.Ordinal);
        var cambio = false;
        foreach (var entrada in CatalogoChecklistDemo.Para(obligacion).Where(e => !existentes.Contains(e.Codigo)))
        {
            var elemento = new ElementoChecklistObligacion
            {
                Id = Guid.NewGuid(), ObligacionFiscalId = obligacion.Id, ObligacionFiscal = obligacion,
                CodigoPlantilla = entrada.Codigo, Descripcion = entrada.Descripcion, Obligatorio = entrada.Obligatorio
            };
            obligacion.Checklist.Add(elemento);
            context.Add(elemento);
            cambio = true;
        }
        return cambio;
    }

    private async Task<bool> RegistrarCambioAsync(
        ElementoChecklistObligacion elemento, EstadoElementoChecklist nuevoEstado, Guid usuarioId,
        OrigenCambioChecklist origen, CancellationToken ct)
    {
        if (elemento.Estado == nuevoEstado) return false;
        var fecha = timeProvider.GetUtcNow();
        var usuario = await context.Usuarios.SingleAsync(u => u.Id == usuarioId, ct);
        var historial = new HistorialElementoChecklist
        {
            Id = Guid.NewGuid(), ElementoChecklistObligacionId = elemento.Id, Elemento = elemento,
            UsuarioId = usuarioId, Usuario = usuario, Fecha = fecha,
            EstadoAnterior = elemento.Estado, EstadoNuevo = nuevoEstado, Origen = origen
        };
        elemento.Estado = nuevoEstado;
        elemento.OrigenUltimoCambio = origen;
        elemento.UsuarioUltimoCambioId = usuarioId;
        elemento.UsuarioUltimoCambio = usuario;
        elemento.FechaUltimoCambio = fecha;
        elemento.Historial.Add(historial);
        context.Add(historial);
        return true;
    }

    internal async Task<bool> RecalcularPorEvidenciasAsync(
        ElementoChecklistObligacion elemento, Guid usuarioId, CancellationToken ct)
    {
        var evidenciaConfirmada = elemento.VinculosEvidencia.Any(v => v.Estado == EstadoVinculoEvidencia.CONFIRMADA);
        if (evidenciaConfirmada)
            return await RegistrarCambioAsync(elemento, EstadoElementoChecklist.CONFIRMADO, usuarioId,
                OrigenCambioChecklist.VINCULACION_EVIDENCIA, ct);

        if (elemento.Estado == EstadoElementoChecklist.CONFIRMADO &&
            elemento.OrigenUltimoCambio == OrigenCambioChecklist.VINCULACION_EVIDENCIA)
            return await RegistrarCambioAsync(elemento, EstadoElementoChecklist.PENDIENTE, usuarioId,
                OrigenCambioChecklist.VINCULACION_EVIDENCIA, ct);

        return false;
    }

    internal static bool ActualizarEstado(ObligacionFiscal obligacion)
    {
        if (obligacion.Estado == EstadoObligacion.PRESENTADA) return false;
        var requeridos = obligacion.Checklist.Where(e => e.Obligatorio).ToList();
        var nuevoEstado = requeridos.Count > 0 && requeridos.All(e => e.Estado == EstadoElementoChecklist.CONFIRMADO)
            ? EstadoObligacion.LISTA
            : EstadoObligacion.INCOMPLETA;
        if (obligacion.Estado == nuevoEstado) return false;
        obligacion.Estado = nuevoEstado;
        return true;
    }
}
