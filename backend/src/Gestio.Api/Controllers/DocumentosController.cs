using Gestio.Api.Common;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Models.Requests;
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

    [HttpGet("{id:guid}/historial-categorias")]
    public async Task<ActionResult<IReadOnlyList<HistorialCategoriaResponse>>> GetHistorialCategorias(Guid id, CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var historial = await documentosService.ObtenerHistorialCategoriasAsync(usuario, id, cancellationToken);
        return Ok(historial
            .Select(entrada => new HistorialCategoriaResponse(
                entrada.Id,
                entrada.DocumentoId,
                entrada.UsuarioId,
                entrada.Usuario.Nombre,
                entrada.CategoriaAnterior.ToString(),
                entrada.CategoriaNueva.ToString(),
                entrada.FechaCambio))
            .ToList());
    }

    [HttpPatch("{id:guid}/categoria")]
    public async Task<ActionResult<DocumentoResponse>> CambiarCategoria(
        Guid id,
        [FromBody] CambiarCategoriaRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var resultado = await documentosService.CambiarCategoriaAsync(usuario, id, request.NuevaCategoria, cancellationToken);
        return resultado.EsCorrecto ? Ok(MapearDocumento(resultado.Valor!)) : resultado.Error!.ToProblem();
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

    [HttpPost("{id:guid}/reintentar")]
    public async Task<ActionResult<DocumentoResponse>> Reintentar(Guid id, CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var resultado = await documentosService.ProcesarAsync(usuario, id, true, CancellationToken.None);
        return resultado.EsCorrecto ? Ok(MapearDocumento(resultado.Valor!)) : resultado.Error!.ToProblem();
    }

    [HttpPost("{id:guid}/corregir")]
    public async Task<ActionResult<DocumentoResponse>> Corregir(
        Guid id,
        [FromBody] ValidarDocumentoRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var resultado = await documentosService.CorregirAsync(usuario, id, request.Campos, cancellationToken);
        return resultado.EsCorrecto ? Ok(MapearDocumento(resultado.Valor!)) : resultado.Error!.ToProblem();
    }

    [HttpPost("{id:guid}/aprobar")]
    public async Task<ActionResult<DocumentoResponse>> Aprobar(Guid id, CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var resultado = await documentosService.AprobarAsync(usuario, id, cancellationToken);
        return resultado.EsCorrecto ? Ok(MapearDocumento(resultado.Valor!)) : resultado.Error!.ToProblem();
    }

    [HttpPost("{id:guid}/aclaracion")]
    public async Task<ActionResult<DocumentoResponse>> SolicitarAclaracion(
        Guid id,
        [FromBody] SolicitarAclaracionRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var resultado = await documentosService.SolicitarAclaracionAsync(usuario, id, request.Pregunta, cancellationToken);
        return resultado.EsCorrecto ? Ok(MapearDocumento(resultado.Valor!)) : resultado.Error!.ToProblem();
    }

    [HttpPost("{id:guid}/nota")]
    public async Task<ActionResult<DocumentoResponse>> EnviarNota(
        Guid id,
        [FromBody] NotaDocumentoRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var resultado = await documentosService.EnviarNotaAsync(usuario, id, request.Nota, cancellationToken);
        return resultado.EsCorrecto ? Ok(MapearDocumento(resultado.Valor!)) : resultado.Error!.ToProblem();
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
        documento.Categoria.ToString(),
        ParseCategoriaPropuesta(documento.ExtraccionIa?.CategoriaSugerida),
        documento.ExtraccionIa?.NivelConfianza?.ToString(),
        documento.ExtraccionIa is null
            ? null
            : new ExtraccionResponse(
                documento.ExtraccionIa.NumeroFactura,
                documento.ExtraccionIa.EmisorNombre,
                documento.ExtraccionIa.EmisorNif,
                documento.ExtraccionIa.FechaEmision,
                documento.ExtraccionIa.BaseImponible,
                documento.ExtraccionIa.TipoIva,
                documento.ExtraccionIa.CuotaIva,
                documento.ExtraccionIa.Total,
                documento.ExtraccionIa.CategoriaSugerida,
                documento.ExtraccionIa.NivelConfianza?.ToString(),
                documento.ExtraccionIa.DatosCrudosJson,
                documento.ExtraccionIa.DesglosesIva,
                documento.ExtraccionIa.Advertencias),
        documento.Anomalias
            .OrderByDescending(anomalia => anomalia.FechaDeteccion)
            .Select(anomalia => new AnomaliaResponse(
                anomalia.Id,
                anomalia.Tipo.ToString(),
                anomalia.Campo,
                anomalia.Descripcion,
                anomalia.Severidad.ToString(),
                anomalia.Resuelta,
                anomalia.FechaDeteccion,
                anomalia.DocumentoRelacionadoId))
            .ToList(),
        documento.Eventos
            .OrderBy(evento => evento.Fecha)
            .Select(evento => new EventoDocumentoResponse(evento.Tipo.ToString(), evento.Fecha, evento.Detalle, ActorDeEvento(evento)))
            .ToList(),
        documento.Intentos
            .OrderBy(i => i.Inicio)
            .Select(i => new IntentoResponse(i.Id, i.Inicio, i.Fin, i.Procesador, i.Version, i.Resultado, i.Error, i.ValoresJson))
            .ToList(),
        documento.HistorialValidacion
            .OrderByDescending(h => h.FechaCambio)
            .Select(h => new HistorialValidacionResponse(
                h.Id,
                h.Campo,
                h.ValorAnterior,
                h.ValorNuevo,
                h.TipoCambio,
                h.UsuarioId,
                h.FechaCambio))
            .ToList(),
        TipoDocumento(documento));

    private static string? ParseCategoriaPropuesta(string? categoriaSugerida)
    {
        if (string.IsNullOrWhiteSpace(categoriaSugerida)) return null;

        return categoriaSugerida.Trim();
    }

    private static string? ActorDeEvento(EventoDocumento evento) =>
        evento.Tipo switch
        {
            TipoEventoDocumento.PROCESAMIENTO_INICIADO
                or TipoEventoDocumento.PROCESAMIENTO_CORRECTO
                or TipoEventoDocumento.PROCESAMIENTO_FALLIDO
                or TipoEventoDocumento.PROCESAMIENTO_COMPLETADO
                or TipoEventoDocumento.PROCESAMIENTO_ERROR
                or TipoEventoDocumento.POSIBLE_DUPLICADO => "IA",
            _ => evento.Usuario is null
                ? null
                : $"{evento.Usuario.Nombre} {evento.Usuario.PrimerApellido}".Trim(),
        };

    private static string TipoDocumento(Documento documento)
    {
        var nifEmisor = documento.ExtraccionIa?.EmisorNif?.Trim();
        var nifPropietario = documento.Usuario.Nif.Trim();

        return !string.IsNullOrEmpty(nifEmisor)
               && string.Equals(nifEmisor, nifPropietario, StringComparison.OrdinalIgnoreCase)
            ? "INGRESO"
            : "GASTO";
    }
}