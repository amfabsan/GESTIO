using Gestio.Api.Common;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Models.Responses;
using Gestio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gestio.Api.Controllers;

[ApiController]
[Route("api/documentos")]
public sealed class DocumentosController(DocumentosService documentosService, ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet("configuracion-subida")]
    public ActionResult<ConfiguracionSubidaResponse> GetConfiguracionSubida()
    {
        var reglas = documentosService.ObtenerReglasSubida();

        return Ok(new ConfiguracionSubidaResponse(
            reglas.TamanoMaximoBytes,
            reglas.TiposPermitidos
                .Select(tipo => new TipoArchivoPermitidoResponse(tipo.Extension, tipo.TipoMime, Convert.ToHexString(tipo.Firma)))
                .ToList()));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentoResponse>>> GetDocumentos(CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var documentos = await documentosService.ListarAsync(usuario, cancellationToken);
        return Ok(documentos.Select(MapearDocumento).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentoResponse>> GetDocumento(Guid id, CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        // Si no puede consultarlo se responde 404 para no revelar que el documento existe.
        var documento = await documentosService.ObtenerAsync(usuario, id, cancellationToken);
        return documento is null ? NotFound() : Ok(MapearDocumento(documento));
    }

    [HttpGet("{id:guid}/archivo")]
    public async Task<IActionResult> GetArchivo(Guid id, CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var original = await documentosService.AbrirOriginalAsync(usuario, id, cancellationToken);
        return original is null ? NotFound() : File(original.Contenido, original.TipoMime, original.NombreArchivo);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentoResponse>> SubirDocumento(IFormFile archivo, CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        await using var contenido = archivo.OpenReadStream();
        var resultado = await documentosService.SubirAsync(usuario, archivo.FileName, contenido, archivo.Length, cancellationToken);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();

        var documento = resultado.Valor!;
        return CreatedAtAction(nameof(GetDocumento), new { id = documento.Id }, MapearDocumento(documento));
    }

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