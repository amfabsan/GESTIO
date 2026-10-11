using Gestio.Api.Common;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gestio.Api.Controllers;

[ApiController]
[Route("api/documentos")]
public sealed class DocumentosController(
    DocumentosService documentosService,
    ICurrentUserService currentUserService,
    IOptions<DocumentosOptions> options) : ControllerBase
{
    [HttpGet("configuracion-subida")]
    public ActionResult<ConfiguracionSubidaResponse> GetConfiguracionSubida() =>
        Ok(new ConfiguracionSubidaResponse(
            options.Value.TamanoMaximoBytes,
            ValidadorArchivo.ExtensionesPermitidas,
            ValidadorArchivo.TiposMimePermitidos));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentoResponse>>> GetDocumentos(CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var documentos = await ConDetalle(documentosService.ConsultablesPor(usuario))
            .OrderByDescending(documento => documento.FechaSubida)
            .ToListAsync(cancellationToken);

        return Ok(documentos.Select(MapearDocumento).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentoResponse>> GetDocumento(Guid id, CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        // Si no puede consultarlo se responde 404 para no revelar que el documento existe.
        var documento = await ConDetalle(documentosService.ConsultablesPor(usuario))
            .FirstOrDefaultAsync(documento => documento.Id == id, cancellationToken);

        return documento is null ? NotFound() : Ok(MapearDocumento(documento));
    }

    [HttpGet("{id:guid}/archivo")]
    public async Task<IActionResult> GetArchivo(Guid id, CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var documento = await documentosService.ConsultablesPor(usuario)
            .AsNoTracking()
            .FirstOrDefaultAsync(documento => documento.Id == id, cancellationToken);
        if (documento is null) return NotFound();

        var archivo = await documentosService.AbrirOriginalAsync(documento, cancellationToken);
        return archivo is null ? NotFound() : File(archivo, documento.TipoMime, documento.NombreArchivo);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentoResponse>> SubirDocumento(IFormFile archivo, CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();
        if (usuario.Rol != Rol.AUTONOMO) return DocumentoErrores.SoloAutonomoPuedeSubir.ToProblem();

        await using var contenido = archivo.OpenReadStream();
        var resultado = await documentosService.SubirAsync(usuario.Id, archivo.FileName, contenido, archivo.Length, cancellationToken);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();

        var documento = await ConDetalle(documentosService.ConsultablesPor(usuario))
            .FirstAsync(documento => documento.Id == resultado.Valor!.Id, cancellationToken);

        return CreatedAtAction(nameof(GetDocumento), new { id = documento.Id }, MapearDocumento(documento));
    }

    private static IQueryable<Documento> ConDetalle(IQueryable<Documento> consulta) => consulta
        .AsNoTracking()
        .AsSplitQuery()
        .Include(documento => documento.Usuario)
        .Include(documento => documento.ExtraccionIa)
        .Include(documento => documento.Anomalias)
        .Include(documento => documento.Eventos);

    private static DocumentoResponse MapearDocumento(Documento documento) => new(
        documento.Id,
        documento.NombreArchivo,
        documento.Usuario.Nombre,
        documento.FechaSubida,
        documento.Estado.ToString(),
        documento.TipoMime,
        documento.TamanoBytes,
        documento.Origen.ToString(),
        documento.StorageKey is not null,
        documento.ExtraccionIa is null
            ? null
            : new ExtraccionResponse(
                documento.ExtraccionIa.EmisorNombre,
                documento.ExtraccionIa.EmisorNif,
                documento.ExtraccionIa.FechaEmision,
                documento.ExtraccionIa.BaseImponible,
                documento.ExtraccionIa.TipoIva,
                documento.ExtraccionIa.CuotaIva,
                documento.ExtraccionIa.Total,
                documento.ExtraccionIa.CategoriaSugerida,
                documento.ExtraccionIa.NivelConfianza?.ToString(),
                documento.ExtraccionIa.DatosCrudosJson),
        documento.Anomalias
            .OrderByDescending(anomalia => anomalia.FechaDeteccion)
            .Select(anomalia => new AnomaliaResponse(
                anomalia.Tipo.ToString(),
                anomalia.Descripcion,
                anomalia.Severidad.ToString(),
                anomalia.Resuelta,
                anomalia.FechaDeteccion))
            .ToList(),
        documento.Eventos
            .OrderBy(evento => evento.Fecha)
            .Select(evento => new EventoDocumentoResponse(evento.Tipo.ToString(), evento.Fecha, evento.Detalle))
            .ToList());
}

public sealed record DocumentoResponse(
    Guid Id,
    string NombreArchivo,
    string Usuario,
    DateTimeOffset FechaSubida,
    string Estado,
    string TipoMime,
    long TamanoBytes,
    string Origen,
    bool TieneOriginal,
    ExtraccionResponse? Extraccion,
    IReadOnlyList<AnomaliaResponse> Anomalias,
    IReadOnlyList<EventoDocumentoResponse> Historial);

public sealed record ExtraccionResponse(
    string? EmisorNombre,
    string? EmisorNif,
    DateOnly? FechaEmision,
    decimal? BaseImponible,
    decimal? TipoIva,
    decimal? CuotaIva,
    decimal? Total,
    string? CategoriaSugerida,
    string? NivelConfianza,
    string? DatosCrudosJson);

public sealed record AnomaliaResponse(
    string Tipo,
    string Descripcion,
    string Severidad,
    bool Resuelta,
    DateTimeOffset FechaDeteccion);

public sealed record EventoDocumentoResponse(string Tipo, DateTimeOffset Fecha, string? Detalle);

public sealed record ConfiguracionSubidaResponse(
    long TamanoMaximoBytes,
    IReadOnlyCollection<string> Extensiones,
    IReadOnlyCollection<string> TiposMime);