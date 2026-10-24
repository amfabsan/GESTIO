using Gestio.Api.Common;
using Gestio.Api.Models.Responses;
using Gestio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gestio.Api.Controllers;

// Solo lectura: los eventos los registran los casos de uso del documento, nunca este endpoint.
[ApiController]
[Route("api/documentos/{documentoId:guid}/historial")]
public sealed class HistorialDocumentoController(HistorialDocumentoService historialService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginaResponse<EventoHistorialResponse>>> GetHistorial(
        Guid documentoId,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = 20,
        CancellationToken cancellationToken = default)
    {
        var resultado = await historialService.ObtenerAsync(documentoId, pagina, tamano, cancellationToken);
        return resultado.EsCorrecto
            ? Ok(PaginaResponse<EventoHistorialResponse>.Desde(resultado.Valor!, EventoHistorialResponse.Desde))
            : resultado.Error!.ToProblem();
    }
}
