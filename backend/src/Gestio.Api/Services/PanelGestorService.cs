using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Services;

public sealed class PanelGestorService(
    ApplicationDbContext context,
    TimeProvider timeProvider)
{
    public const int DiasAntelacionDefecto = 35;

    private static readonly Error Prohibido = new(
        "PanelGestor.Prohibido",
        "No tienes permiso para acceder al panel de esta asesoría o de este cliente.",
        TipoError.PROHIBIDO);

    private static readonly Error NoEncontrado = new(
        "PanelGestor.NoEncontrado",
        "No se ha encontrado el cliente solicitado en tu asesoría.",
        TipoError.NO_ENCONTRADO);

    public async Task<Resultado<PanelGestorResponse>> ObtenerPanelAsync(
        UsuarioActual usuario,
        bool soloRequiereAtencion = false,
        string? ordenarPor = null,
        string? busqueda = null,
        CancellationToken ct = default)
    {
        if (usuario.Rol != Rol.GESTOR)
            return Prohibido;

        var gestor = await context.Usuarios
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == usuario.Id, ct);

        if (gestor is null || string.IsNullOrWhiteSpace(gestor.CodigoAsesoria))
            return Prohibido;

        var codigoAsesoria = gestor.CodigoAsesoria.Trim();
        var hoy = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var limiteFecha = hoy.AddDays(DiasAntelacionDefecto);

        var vinculacionesAsesoria = await context.Vinculaciones
            .AsNoTracking()
            .Where(v => v.CodigoAsesoria == codigoAsesoria)
            .ToListAsync(ct);

        var activas = vinculacionesAsesoria
            .Where(v => v.Estado == EstadoVinculacion.ACTIVA)
            .ToList();

        var pendientesCount = vinculacionesAsesoria
            .Count(v => v.Estado == EstadoVinculacion.PENDIENTE);

        var autonomoIds = activas.Select(v => v.AutonomoId).Distinct().ToList();
        var nombreGestor = $"{gestor.Nombre} {gestor.PrimerApellido} {gestor.SegundoApellido}".Trim();

        if (autonomoIds.Count == 0)
        {
            return new PanelGestorResponse(
                NombreGestor: nombreGestor,
                NombreAsesoria: gestor.NombreAsesoria,
                CodigoAsesoria: codigoAsesoria,
                ClientesActivosCount: 0,
                SolicitudesPendientesCount: pendientesCount,
                DiasAntelacionObligaciones: DiasAntelacionDefecto,
                FechaReferencia: hoy,
                Resumen: new PanelResumenGlobalesResponse(0, 0, 0, 0, 0),
                Clientes: []);
        }

        var autonomos = await context.Usuarios
            .AsNoTracking()
            .Where(u => autonomoIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        var obligaciones = await context.Set<ObligacionFiscal>()
            .AsNoTracking()
            .Include(o => o.Checklist)
            .Where(o => autonomoIds.Contains(o.AutonomoId))
            .ToListAsync(ct);

        var documentos = await context.Documentos
            .AsNoTracking()
            .Include(d => d.Anomalias)
            .Where(d => autonomoIds.Contains(d.UsuarioId))
            .ToListAsync(ct);

        var filas = new List<PanelClienteFilaResponse>(autonomoIds.Count);

        foreach (var autonomoId in autonomoIds)
        {
            if (!autonomos.TryGetValue(autonomoId, out var autonomo))
                continue;

            var obsCliente = obligaciones
                .Where(o => o.AutonomoId == autonomoId)
                .ToList();

            var obsProximas = obsCliente
                .Where(o => o.FechaPrevista >= hoy && o.FechaPrevista <= limiteFecha)
                .OrderBy(o => o.FechaPrevista)
                .ToList();

            var obsProximasChecklistIncompleto = obsProximas
                .Where(EsChecklistIncompleto)
                .ToList();

            var docsPendientes = documentos
                .Where(d => d.UsuarioId == autonomoId && d.Estado == EstadoDocumento.PENDIENTE_VALIDACION)
                .OrderByDescending(d => d.FechaSubida)
                .ToList();

            var anomaliasPendientes = documentos
                .Where(d => d.UsuarioId == autonomoId)
                .SelectMany(d => d.Anomalias
                    .Where(a => !a.Resuelta)
                    .Select(a => new { Documento = d, Anomalia = a }))
                .OrderByDescending(x => x.Anomalia.FechaDeteccion)
                .ToList();

            var motivos = new List<string>();
            var avisos = new List<PanelAvisoOrigenResponse>();

            foreach (var ob in obsProximasChecklistIncompleto)
            {
                var obligatorios = ob.Checklist.Where(c => c.Obligatorio).ToList();
                var confirmados = obligatorios.Count(c => c.Estado == EstadoElementoChecklist.CONFIRMADO);
                var totalObl = obligatorios.Count;
                var titulo = $"Checklist {ob.Modelo} {ob.Periodo} incompleto · {confirmados}/{totalObl}";
                motivos.Add(titulo);
                avisos.Add(new PanelAvisoOrigenResponse(
                    Tipo: "CHECKLIST",
                    Titulo: titulo,
                    Detalle: ob.Nombre,
                    ObligacionFiscalId: ob.Id,
                    DocumentoId: null,
                    AnomaliaId: null,
                    Ejercicio: ob.Ejercicio,
                    Periodo: ob.Periodo,
                    Modelo: ob.Modelo));
            }

            foreach (var doc in docsPendientes)
            {
                var titulo = $"{doc.NombreArchivo} por validar";
                motivos.Add(titulo);
                avisos.Add(new PanelAvisoOrigenResponse(
                    Tipo: "DOCUMENTO",
                    Titulo: titulo,
                    Detalle: "Pendiente de validación",
                    ObligacionFiscalId: null,
                    DocumentoId: doc.Id,
                    AnomaliaId: null,
                    Ejercicio: null,
                    Periodo: null,
                    Modelo: null));
            }

            foreach (var item in anomaliasPendientes)
            {
                motivos.Add($"Anomalía en {item.Documento.NombreArchivo}: {item.Anomalia.Descripcion}");
                avisos.Add(new PanelAvisoOrigenResponse(
                    Tipo: "ANOMALIA",
                    Titulo: $"Anomalía · {item.Documento.NombreArchivo}",
                    Detalle: item.Anomalia.Descripcion,
                    ObligacionFiscalId: null,
                    DocumentoId: item.Documento.Id,
                    AnomaliaId: item.Anomalia.Id,
                    Ejercicio: null,
                    Periodo: null,
                    Modelo: null));
            }

            var requiereAtencion = obsProximasChecklistIncompleto.Count > 0 ||
                                   docsPendientes.Count > 0 ||
                                   anomaliasPendientes.Count > 0;

            var proximaOb = obsProximas.FirstOrDefault() ??
                             obsCliente.Where(o => o.FechaPrevista >= hoy).OrderBy(o => o.FechaPrevista).FirstOrDefault();

            int? diasHastaProxima = proximaOb is null
                ? null
                : proximaOb.FechaPrevista.DayNumber - hoy.DayNumber;

            string? etiquetaProxima = diasHastaProxima switch
            {
                null => null,
                0 => "hoy",
                1 => "en 1 día",
                _ => $"en {diasHastaProxima} días"
            };

            var nombreCompleto = $"{autonomo.Nombre} {autonomo.PrimerApellido} {autonomo.SegundoApellido}".Trim();

            filas.Add(new PanelClienteFilaResponse(
                AutonomoId: autonomo.Id,
                NombreCompleto: nombreCompleto,
                Iniciales: ObtenerIniciales(autonomo.Nombre, autonomo.PrimerApellido),
                Actividad: null,
                Nif: autonomo.Nif,
                RequiereAtencion: requiereAtencion,
                MotivoPrincipal: motivos.FirstOrDefault(),
                ObligacionesProximasCount: obsProximas.Count,
                ChecklistsIncompletosCount: obsProximasChecklistIncompleto.Count,
                DocumentosPorValidarCount: docsPendientes.Count,
                AnomaliasPendientesCount: anomaliasPendientes.Count,
                ProximaObligacionFecha: proximaOb?.FechaPrevista,
                DiasHastaProximaObligacion: diasHastaProxima,
                ProximaObligacionEtiqueta: etiquetaProxima,
                Avisos: avisos));
        }

        var resumen = new PanelResumenGlobalesResponse(
            RequierenAtencion: filas.Count(f => f.RequiereAtencion),
            ObligacionesProximas: filas.Sum(f => f.ObligacionesProximasCount),
            ChecklistsIncompletos: filas.Sum(f => f.ChecklistsIncompletosCount),
            DocumentosPorValidar: filas.Sum(f => f.DocumentosPorValidarCount),
            AnomaliasPendientes: filas.Sum(f => f.AnomaliasPendientesCount));

        IEnumerable<PanelClienteFilaResponse> resultadoFiltrado = filas;

        if (soloRequiereAtencion)
            resultadoFiltrado = resultadoFiltrado.Where(f => f.RequiereAtencion);

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var term = busqueda.Trim();
            resultadoFiltrado = resultadoFiltrado.Where(f =>
                f.NombreCompleto.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                f.Nif.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (f.Actividad?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                f.Avisos.Any(a => a.Titulo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                   a.Detalle.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        resultadoFiltrado = (ordenarPor?.Trim().ToLowerInvariant()) switch
        {
            "nombre" => resultadoFiltrado.OrderBy(f => f.NombreCompleto, StringComparer.CurrentCultureIgnoreCase),
            _ => resultadoFiltrado
                .OrderBy(f => f.ProximaObligacionFecha ?? DateOnly.MaxValue)
                .ThenByDescending(f => f.RequiereAtencion)
                .ThenBy(f => f.NombreCompleto, StringComparer.CurrentCultureIgnoreCase)
        };

        return new PanelGestorResponse(
            NombreGestor: nombreGestor,
            NombreAsesoria: gestor.NombreAsesoria,
            CodigoAsesoria: codigoAsesoria,
            ClientesActivosCount: autonomoIds.Count,
            SolicitudesPendientesCount: pendientesCount,
            DiasAntelacionObligaciones: DiasAntelacionDefecto,
            FechaReferencia: hoy,
            Resumen: resumen,
            Clientes: resultadoFiltrado.ToArray());
    }

    public async Task<Resultado<PanelClienteDetalleResponse>> ObtenerDetalleClienteAsync(
        UsuarioActual usuario,
        Guid autonomoId,
        CancellationToken ct = default)
    {
        if (usuario.Rol != Rol.GESTOR)
            return Prohibido;

        var gestor = await context.Usuarios
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == usuario.Id, ct);

        if (gestor is null || string.IsNullOrWhiteSpace(gestor.CodigoAsesoria))
            return Prohibido;

        var codigoAsesoria = gestor.CodigoAsesoria.Trim();

        var tieneVinculoActivo = await context.Vinculaciones
            .AsNoTracking()
            .AnyAsync(v => v.AutonomoId == autonomoId &&
                           v.CodigoAsesoria == codigoAsesoria &&
                           v.Estado == EstadoVinculacion.ACTIVA, ct);

        if (!tieneVinculoActivo)
            return Prohibido;

        var autonomo = await context.Usuarios
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == autonomoId, ct);

        if (autonomo is null)
            return NoEncontrado;

        var hoy = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var limiteFecha = hoy.AddDays(DiasAntelacionDefecto);

        var obsProximas = await context.Set<ObligacionFiscal>()
            .AsNoTracking()
            .Include(o => o.Checklist)
            .Where(o => o.AutonomoId == autonomoId && o.FechaPrevista >= hoy && o.FechaPrevista <= limiteFecha)
            .OrderBy(o => o.FechaPrevista)
            .ToListAsync(ct);

        var docsCliente = await context.Documentos
            .AsNoTracking()
            .Include(d => d.Anomalias)
            .Where(d => d.UsuarioId == autonomoId)
            .ToListAsync(ct);

        var detalleObs = obsProximas.Select(o =>
        {
            var obligatorios = o.Checklist.Where(c => c.Obligatorio).ToList();
            var confirmados = obligatorios.Count(c => c.Estado == EstadoElementoChecklist.CONFIRMADO);
            return new PanelObligacionProximaDetalleResponse(
                ObligacionFiscalId: o.Id,
                Modelo: o.Modelo,
                Nombre: o.Nombre,
                Ejercicio: o.Ejercicio,
                Periodo: o.Periodo,
                FechaPrevista: o.FechaPrevista,
                DiasRestantes: o.FechaPrevista.DayNumber - hoy.DayNumber,
                EstadoObligacion: o.Estado.ToString(),
                ObligatoriosConfirmados: confirmados,
                ObligatoriosTotales: obligatorios.Count,
                ChecklistIncompleto: EsChecklistIncompleto(o));
        }).ToList();

        var detalleDocs = docsCliente
            .Where(d => d.Estado == EstadoDocumento.PENDIENTE_VALIDACION)
            .OrderByDescending(d => d.FechaSubida)
            .Select(d => new PanelDocumentoPendienteDetalleResponse(
                DocumentoId: d.Id,
                NombreArchivo: d.NombreArchivo,
                FechaSubida: d.FechaSubida,
                Estado: d.Estado.ToString()))
            .ToList();

        var detalleAnomalias = docsCliente
            .SelectMany(d => d.Anomalias
                .Where(a => !a.Resuelta)
                .Select(a => new PanelAnomaliaPendienteDetalleResponse(
                    AnomaliaId: a.Id,
                    DocumentoId: d.Id,
                    NombreArchivoDocumento: d.NombreArchivo,
                    Tipo: a.Tipo.ToString(),
                    Severidad: a.Severidad.ToString(),
                    Campo: a.Campo,
                    Descripcion: a.Descripcion,
                    FechaDeteccion: a.FechaDeteccion)))
            .OrderByDescending(a => a.FechaDeteccion)
            .ToList();

        var requiereAtencion = detalleObs.Any(o => o.ChecklistIncompleto) ||
                               detalleDocs.Count > 0 ||
                               detalleAnomalias.Count > 0;

        return new PanelClienteDetalleResponse(
            AutonomoId: autonomo.Id,
            NombreCompleto: $"{autonomo.Nombre} {autonomo.PrimerApellido} {autonomo.SegundoApellido}".Trim(),
            Actividad: null,
            Nif: autonomo.Nif,
            RequiereAtencion: requiereAtencion,
            ObligacionesProximas: detalleObs,
            DocumentosPorValidar: detalleDocs,
            AnomaliasPendientes: detalleAnomalias);
    }

    private static bool EsChecklistIncompleto(ObligacionFiscal obligacion)
    {
        if (obligacion.Estado == EstadoObligacion.PRESENTADA)
            return false;

        var obligatorios = obligacion.Checklist.Where(c => c.Obligatorio).ToList();
        if (obligatorios.Count > 0)
            return obligatorios.Any(c => c.Estado != EstadoElementoChecklist.CONFIRMADO);

        return obligacion.Estado == EstadoObligacion.INCOMPLETA;
    }

    private static string ObtenerIniciales(string nombre, string primerApellido)
    {
        var n = string.IsNullOrWhiteSpace(nombre) ? "" : nombre.Trim()[0].ToString().ToUpperInvariant();
        var a = string.IsNullOrWhiteSpace(primerApellido) ? "" : primerApellido.Trim()[0].ToString().ToUpperInvariant();
        return $"{n}{a}";
    }
}