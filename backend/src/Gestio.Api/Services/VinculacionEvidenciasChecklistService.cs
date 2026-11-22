using System.Data;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Gestio.Api.Services;

public sealed class VinculacionEvidenciasChecklistService(
    ApplicationDbContext context,
    ChecklistObligacionesService checklists,
    PerfilesFiscalesService perfiles,
    TimeProvider timeProvider)
{
    private static readonly Error Prohibido = new(
        "ChecklistEvidencia.Prohibido", "No tienes permiso para acceder a las evidencias de esta obligación.", TipoError.PROHIBIDO);
    private static readonly Error ObligacionNoEncontrada = new(
        "ChecklistEvidencia.ObligacionNoEncontrada", "No se ha encontrado esta obligación fiscal.", TipoError.NO_ENCONTRADO);
    private static readonly Error VinculoNoEncontrado = new(
        "ChecklistEvidencia.NoEncontrado", "No se ha encontrado esta evidencia para la obligación.", TipoError.NO_ENCONTRADO);
    private static readonly Error PlantillaNoDisponible = new(
        "ChecklistEvidencia.PlantillaNoDisponible", "No hay una plantilla de demostración para esta obligación.", TipoError.NO_DISPONIBLE);
    private static readonly Error ObligacionPresentada = new(
        "ChecklistEvidencia.ObligacionPresentada", "No se pueden modificar evidencias de una obligación presentada.", TipoError.CONFLICTO);
    private static readonly Error TransicionNoPermitida = new(
        "ChecklistEvidencia.TransicionNoPermitida", "La evidencia ya no admite esta decisión.", TipoError.CONFLICTO);
    private static readonly Error EvidenciaNoValida = new(
        "ChecklistEvidencia.NoValida", "La evidencia dejó de cumplir las reglas de coincidencia y se ha retirado.", TipoError.CONFLICTO);

    public async Task<Resultado<int>> BuscarParaObligacionAsync(
        UsuarioActual usuario, Guid autonomoId, Guid obligacionId, CancellationToken ct)
    {
        if (usuario.Rol != Rol.GESTOR || !await perfiles.PuedeAccederAsync(usuario, autonomoId, true, ct))
            return Prohibido;

        return await ProponerParaObligacionAsync(autonomoId, obligacionId, usuario.Id, ct);
    }

    public async Task<Resultado<ObligacionFiscal>> CambiarEstadoAsync(
        UsuarioActual usuario, Guid autonomoId, Guid obligacionId, Guid vinculoId,
        EstadoVinculoEvidencia estadoNuevo, CancellationToken ct)
    {
        if (usuario.Rol != Rol.GESTOR || !await perfiles.PuedeAccederAsync(usuario, autonomoId, true, ct))
            return Prohibido;
        if (estadoNuevo is not (EstadoVinculoEvidencia.CONFIRMADA or EstadoVinculoEvidencia.RECHAZADA or EstadoVinculoEvidencia.RETIRADA))
            return TransicionNoPermitida;

        await using var transaccion = await BloquearAsync(obligacionId, ct);
        var obligacion = await context.Set<ObligacionFiscal>().AsSplitQuery()
            .Include(o => o.Checklist).ThenInclude(e => e.UsuarioUltimoCambio)
            .Include(o => o.Checklist).ThenInclude(e => e.Historial).ThenInclude(h => h.Usuario)
            .Include(o => o.Checklist).ThenInclude(e => e.VinculosEvidencia).ThenInclude(v => v.Documento).ThenInclude(d => d.ExtraccionIa)
            .Include(o => o.Checklist).ThenInclude(e => e.VinculosEvidencia).ThenInclude(v => v.ConfirmadaPor)
            .Include(o => o.Checklist).ThenInclude(e => e.VinculosEvidencia).ThenInclude(v => v.Historial).ThenInclude(h => h.Usuario)
            .SingleOrDefaultAsync(o => o.Id == obligacionId && o.AutonomoId == autonomoId, ct);
        if (obligacion is null) return ObligacionNoEncontrada;
        if (CatalogoChecklistDemo.Para(obligacion).Count == 0) return PlantillaNoDisponible;
        if (obligacion.Estado == EstadoObligacion.PRESENTADA) return ObligacionPresentada;

        var elemento = obligacion.Checklist.SingleOrDefault(e => e.VinculosEvidencia.Any(v => v.Id == vinculoId));
        var vinculo = elemento?.VinculosEvidencia.Single(v => v.Id == vinculoId);
        if (elemento is null || vinculo is null || vinculo.Documento.UsuarioId != autonomoId) return VinculoNoEncontrado;
        if (vinculo.Estado == estadoNuevo)
        {
            if (transaccion is not null) await transaccion.CommitAsync(ct);
            return obligacion;
        }

        var transicionPermitida = vinculo.Estado == EstadoVinculoEvidencia.PROPUESTA ||
            vinculo.Estado == EstadoVinculoEvidencia.CONFIRMADA && estadoNuevo == EstadoVinculoEvidencia.RETIRADA;
        if (!transicionPermitida) return TransicionNoPermitida;

        var fecha = timeProvider.GetUtcNow();
        var anterior = vinculo.Estado;
        if (estadoNuevo == EstadoVinculoEvidencia.CONFIRMADA)
        {
            var entrada = CatalogoChecklistDemo.Para(obligacion)
                .SingleOrDefault(e => e.Codigo == elemento.CodigoPlantilla);
            if (entrada is null || !CumpleRegla(obligacion, entrada, vinculo.Documento))
            {
                estadoNuevo = EstadoVinculoEvidencia.RETIRADA;
                vinculo.Estado = estadoNuevo;
                AgregarHistorial(vinculo, anterior, estadoNuevo, usuario.Id, fecha);
                await checklists.RecalcularPorEvidenciasAsync(elemento, usuario.Id, ct);
                ChecklistObligacionesService.ActualizarEstado(obligacion);
                await context.SaveChangesAsync(ct);
                if (transaccion is not null) await transaccion.CommitAsync(ct);
                return EvidenciaNoValida;
            }

            vinculo.ConfirmadaPorId = usuario.Id;
            vinculo.ConfirmadaPor = await context.Usuarios.SingleAsync(u => u.Id == usuario.Id, ct);
            vinculo.FechaConfirmacion = fecha;
        }

        vinculo.Estado = estadoNuevo;
        AgregarHistorial(vinculo, anterior, estadoNuevo, usuario.Id, fecha);
        await checklists.RecalcularPorEvidenciasAsync(elemento, usuario.Id, ct);
        ChecklistObligacionesService.ActualizarEstado(obligacion);
        await context.SaveChangesAsync(ct);
        if (transaccion is not null) await transaccion.CommitAsync(ct);
        return obligacion;
    }

    public async Task<int> ProponerParaDocumentoAsync(Guid documentoId, Guid actorId, CancellationToken ct)
    {
        var documento = await context.Documentos.AsNoTracking()
            .Where(d => d.Id == documentoId && d.Estado == EstadoDocumento.VALIDADO)
            .Select(d => new { d.UsuarioId, FechaEmision = d.ExtraccionIa!.FechaEmision })
            .SingleOrDefaultAsync(ct);
        if (documento?.FechaEmision is not DateOnly fecha || fecha.Year is < 1 or > 9998) return 0;

        return await CrearPropuestasAsync(documento.UsuarioId, fecha.Year, null, documentoId, actorId, ct);
    }

    private async Task<int> ProponerParaObligacionAsync(Guid autonomoId, Guid obligacionId, Guid actorId, CancellationToken ct)
    {
        var obligacion = await context.Set<ObligacionFiscal>().AsNoTracking()
            .Where(o => o.Id == obligacionId && o.AutonomoId == autonomoId && o.Estado != EstadoObligacion.PRESENTADA)
            .Select(o => new { o.Ejercicio })
            .SingleOrDefaultAsync(ct);
        if (obligacion is null) return 0;

        return await CrearPropuestasAsync(autonomoId, obligacion.Ejercicio, obligacionId, null, actorId, ct);
    }

    private async Task<int> CrearPropuestasAsync(
        Guid autonomoId, int ejercicio, Guid? obligacionId, Guid? documentoId, Guid actorId, CancellationToken ct)
    {
        await checklists.AsegurarCalendarioAsync(autonomoId, ejercicio, ct);

        var inicio = new DateOnly(ejercicio, 1, 1);
        var fin = new DateOnly(ejercicio + 1, 1, 1);
        var documentosQuery = context.Documentos.AsNoTracking()
            .Include(d => d.ExtraccionIa)
            .Where(d => d.UsuarioId == autonomoId && d.Estado == EstadoDocumento.VALIDADO &&
                d.ExtraccionIa != null && d.ExtraccionIa.FechaEmision >= inicio && d.ExtraccionIa.FechaEmision < fin);
        if (documentoId.HasValue) documentosQuery = documentosQuery.Where(d => d.Id == documentoId.Value);
        var documentos = await documentosQuery.ToListAsync(ct);
        var obligacionesQuery = context.Set<ObligacionFiscal>()
            .Include(o => o.Checklist).ThenInclude(e => e.VinculosEvidencia).ThenInclude(v => v.Documento).ThenInclude(d => d.ExtraccionIa)
            .Where(o => o.AutonomoId == autonomoId && o.Ejercicio == ejercicio && o.Estado != EstadoObligacion.PRESENTADA);
        if (obligacionId.HasValue) obligacionesQuery = obligacionesQuery.Where(o => o.Id == obligacionId.Value);
        var obligaciones = await obligacionesQuery.ToListAsync(ct);
        var ahora = timeProvider.GetUtcNow();
        var propuestas = new List<VinculoEvidenciaChecklist>();
        var elementosAfectados = new HashSet<ElementoChecklistObligacion>();
        var cambio = false;

        foreach (var obligacion in obligaciones)
        {
            var entradas = CatalogoChecklistDemo.Para(obligacion).ToDictionary(e => e.Codigo, StringComparer.Ordinal);
            foreach (var elemento in obligacion.Checklist)
            {
                if (!entradas.TryGetValue(elemento.CodigoPlantilla, out var entrada)) continue;
                foreach (var vinculo in elemento.VinculosEvidencia.Where(v =>
                    v.Estado is EstadoVinculoEvidencia.PROPUESTA or EstadoVinculoEvidencia.CONFIRMADA))
                {
                    if (CumpleRegla(obligacion, entrada, vinculo.Documento)) continue;
                    var estadoAnterior = vinculo.Estado;
                    vinculo.Estado = EstadoVinculoEvidencia.RETIRADA;
                    AgregarHistorial(vinculo, estadoAnterior, EstadoVinculoEvidencia.RETIRADA, actorId, ahora);
                    elementosAfectados.Add(elemento);
                    cambio = true;
                }

                foreach (var documento in documentos)
                {
                    if (!CumpleRegla(obligacion, entrada, documento) ||
                        elemento.VinculosEvidencia.Any(v => v.DocumentoId == documento.Id))
                        continue;

                    propuestas.Add(new VinculoEvidenciaChecklist
                    {
                        Id = Guid.NewGuid(),
                        DocumentoId = documento.Id,
                        ElementoChecklistObligacionId = elemento.Id,
                        Estado = EstadoVinculoEvidencia.PROPUESTA,
                        FechaPropuesta = ahora,
                        Historial =
                        [
                            new HistorialVinculoEvidencia
                            {
                                Id = Guid.NewGuid(),
                                EstadoAnterior = null,
                                EstadoNuevo = EstadoVinculoEvidencia.PROPUESTA,
                                Fecha = ahora
                            }
                        ]
                    });
                }
            }
        }

        foreach (var elemento in elementosAfectados)
            await checklists.RecalcularPorEvidenciasAsync(elemento, actorId, ct);
        foreach (var obligacion in obligaciones)
            cambio |= ChecklistObligacionesService.ActualizarEstado(obligacion);

        if (propuestas.Count > 0) context.VinculosEvidenciaChecklist.AddRange(propuestas);
        if (!cambio && propuestas.Count == 0) return 0;
        try
        {
            await context.SaveChangesAsync(ct);
            return propuestas.Count;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            context.ChangeTracker.Clear();
            return 0;
        }
    }

    private static bool CumpleRegla(ObligacionFiscal obligacion, CatalogoChecklistDemo.Entrada entrada, Documento documento) =>
        documento.UsuarioId == obligacion.AutonomoId && documento.Estado == EstadoDocumento.VALIDADO &&
        documento.Categoria == entrada.CategoriaEsperada &&
        CatalogoChecklistDemo.PeriodoCoincide(obligacion, documento.ExtraccionIa?.FechaEmision);

    private void AgregarHistorial(
        VinculoEvidenciaChecklist vinculo, EstadoVinculoEvidencia anterior,
        EstadoVinculoEvidencia nuevo, Guid? usuarioId, DateTimeOffset fecha)
    {
        var historial = new HistorialVinculoEvidencia
        {
            Id = Guid.NewGuid(), VinculoEvidenciaChecklistId = vinculo.Id, Vinculo = vinculo,
            EstadoAnterior = anterior, EstadoNuevo = nuevo, UsuarioId = usuarioId, Fecha = fecha
        };
        vinculo.Historial.Add(historial);
        context.Add(historial);
    }

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
}
