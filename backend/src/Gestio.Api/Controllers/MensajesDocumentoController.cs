using Gestio.Api.Common;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Controllers;

[ApiController]
[Route("api/documentos")]
public sealed class MensajesDocumentoController(
    ApplicationDbContext db, AccesoDocumentos acceso, ICurrentUserService usuarios) : ControllerBase
{
    private const int Limite = 1000;

    public sealed record EnviarRequest(string? Contenido);
    public sealed record LecturaRequest(Guid[] MensajeIds);
    public sealed record MensajeResponse(Guid Id, Guid AutorId, string Autor, string Contenido, DateTimeOffset Fecha);
    public sealed record ConversacionResponse(Guid DocumentoId, string NombreArchivo, MensajeResponse UltimoMensaje, int SinLeer);

    private static readonly System.Linq.Expressions.Expression<Func<MensajeDocumento, MensajeResponse>> Proyeccion =
        m => new MensajeResponse(m.Id, m.AutorId,
            (m.Autor.Nombre + " " + m.Autor.PrimerApellido + " " + m.Autor.SegundoApellido).Trim(),
            m.Contenido, m.Fecha);

    [HttpGet("conversaciones")]
    public async Task<ActionResult> Listar(CancellationToken ct)
    {
        var usuario = await usuarios.ObtenerAsync(ct);
        if (usuario is null) return DocumentoErrores.NoAutenticado.ToProblem();

        var conversaciones = await acceso.ConsultablesPor(usuario)
            .Where(d => db.MensajesDocumento.Any(m => m.DocumentoId == d.Id))
            .Select(d => new ConversacionResponse(d.Id, d.NombreArchivo,
                db.MensajesDocumento.Where(m => m.DocumentoId == d.Id)
                    .OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id)
                    .Select(m => new MensajeResponse(m.Id, m.AutorId,
                        (m.Autor.Nombre + " " + m.Autor.PrimerApellido + " " + m.Autor.SegundoApellido).Trim(),
                        m.Contenido, m.Fecha)).First(),
                db.AvisosMensajeDocumento.Count(a => a.DestinatarioId == usuario.Id &&
                    a.FechaLectura == null && a.Mensaje.DocumentoId == d.Id)))
            .ToListAsync(ct);
        return Ok(conversaciones.OrderByDescending(c => c.UltimoMensaje.Fecha));
    }

    [HttpGet("{documentoId:guid}/mensajes")]
    public async Task<ActionResult> Obtener(Guid documentoId, CancellationToken ct)
    {
        var usuario = await usuarios.ObtenerAsync(ct);
        if (usuario is null) return DocumentoErrores.NoAutenticado.ToProblem();

        var documentos = acceso.ConsultablesPor(usuario).Where(d => d.Id == documentoId);
        if (!await documentos.AnyAsync(ct)) return DocumentoErrores.NoEncontrado.ToProblem();

        var mensajes = db.MensajesDocumento.Where(m => documentos.Any(d => d.Id == m.DocumentoId))
            .OrderBy(m => m.Fecha).ThenBy(m => m.Id);
        return Ok(await mensajes.Select(Proyeccion).ToListAsync(ct));
    }

    [HttpPost("{documentoId:guid}/mensajes")]
    public async Task<ActionResult> Enviar(Guid documentoId, EnviarRequest request, CancellationToken ct)
    {
        var usuario = await usuarios.ObtenerAsync(ct);
        if (usuario is null) return DocumentoErrores.NoAutenticado.ToProblem();

        var documento = await acceso.ConsultablesPor(usuario).AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentoId, ct);
        if (documento is null) return DocumentoErrores.NoEncontrado.ToProblem();

        if (string.IsNullOrWhiteSpace(request.Contenido) || request.Contenido.Length > Limite)
            return Problem(statusCode: 400, detail: "Escribe un mensaje de entre 1 y 1.000 caracteres.");

        var mensaje = new MensajeDocumento
        {
            Id = Guid.NewGuid(),
            DocumentoId = documentoId,
            AutorId = usuario.Id,
            Contenido = request.Contenido.Trim(),
            Fecha = DateTimeOffset.UtcNow
        };
        List<Guid> destinatarios = usuario.Rol == Rol.GESTOR
            ? [documento.UsuarioId]
            : await db.Usuarios.Where(u => u.Rol == Rol.GESTOR && u.CodigoAsesoria != null &&
                db.Vinculaciones.Any(v => v.AutonomoId == documento.UsuarioId &&
                    v.CodigoAsesoria == u.CodigoAsesoria && v.Estado == EstadoVinculacion.ACTIVA))
                .Select(u => u.Id).ToListAsync(ct);

        foreach (var destinatario in destinatarios)
            mensaje.Avisos.Add(new AvisoMensajeDocumento { DestinatarioId = destinatario });

        db.MensajesDocumento.Add(mensaje);
        await db.SaveChangesAsync(ct);

        return StatusCode(201, await db.MensajesDocumento.Where(m => m.Id == mensaje.Id).Select(Proyeccion).SingleAsync(ct));
    }

    [HttpPost("{documentoId:guid}/mensajes/lectura")]
    public async Task<ActionResult> MarcarLeidos(Guid documentoId, LecturaRequest request, CancellationToken ct)
    {
        var usuario = await usuarios.ObtenerAsync(ct);
        if (usuario is null) return DocumentoErrores.NoAutenticado.ToProblem();

        var documentos = acceso.ConsultablesPor(usuario).Where(d => d.Id == documentoId);
        if (!await documentos.AnyAsync(ct)) return DocumentoErrores.NoEncontrado.ToProblem();

        var avisos = await db.AvisosMensajeDocumento.Where(a => a.DestinatarioId == usuario.Id && a.FechaLectura == null &&
                documentos.Any(d => d.Id == a.Mensaje.DocumentoId) && request.MensajeIds.Contains(a.MensajeId))
            .ToListAsync(ct);
        var fecha = DateTimeOffset.UtcNow;
        foreach (var aviso in avisos)
            aviso.FechaLectura = fecha;
        if (avisos.Count > 0) await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
