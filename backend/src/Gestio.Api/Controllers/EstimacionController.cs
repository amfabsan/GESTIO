using Gestio.Api.Common;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Models.Responses;
using Gestio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gestio.Api.Controllers;

/// <summary>
/// Consulta la estimación trimestral orientativa del IVA para un autónomo.
/// </summary>
[ApiController]
[Route("api/autonomos/{autonomoId:guid}/estimacion")]
public sealed class EstimacionController(
    EstimacionIvaService estimacionService,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>
    /// Obtiene la estimación de IVA para un trimestre específico.
    /// </summary>
    /// <param name="autonomoId">ID del autónomo propietario de los documentos</param>
    /// <param name="ejercicio">Año fiscal (p. ej. 2026)</param>
    /// <param name="trimestre">Trimestre 1..4</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Estimación con importes, documentos incluidos y elementos por revisar</returns>
    [HttpGet("{ejercicio:int}/{trimestre:int}")]
    public async Task<ActionResult<EstimacionResponse>> Obtener(
        Guid autonomoId,
        int ejercicio,
        int trimestre,
        CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var resultado = await estimacionService.CalcularAsync(usuario, autonomoId, ejercicio, trimestre, cancellationToken);
        return resultado.EsCorrecto ? Ok(resultado.Valor!) : resultado.Error!.ToProblem();
    }
}
