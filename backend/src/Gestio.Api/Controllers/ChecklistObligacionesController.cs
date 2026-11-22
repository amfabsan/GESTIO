using Gestio.Api.Common;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Models.Requests;
using Gestio.Api.Models.Response;
using Gestio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gestio.Api.Controllers;

[ApiController]
[Route("api/autonomos/{autonomoId:guid}/obligaciones-fiscales/{obligacionId:guid}/checklist")]
public sealed class ChecklistObligacionesController(
    ChecklistObligacionesService servicio,
    VinculacionEvidenciasChecklistService vinculacionEvidencias,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ChecklistObligacionResponse>> Consultar(
        Guid autonomoId, Guid obligacionId, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await servicio.ConsultarAsync(usuario, autonomoId, obligacionId, ct);
        return resultado.EsCorrecto ? Ok(ChecklistObligacionResponse.Desde(resultado.Valor!)) : resultado.Error!.ToProblem();
    }

    [HttpPatch("elementos/{elementoId:guid}/marca")]
    public async Task<ActionResult<ChecklistObligacionResponse>> MarcarAportado(
        Guid autonomoId, Guid obligacionId, Guid elementoId,
        [FromBody] CambiarMarcaChecklistRequest request, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await servicio.MarcarAportadoAsync(usuario, autonomoId, obligacionId, elementoId, request.Aportado, ct);
        return resultado.EsCorrecto ? Ok(ChecklistObligacionResponse.Desde(resultado.Valor!)) : resultado.Error!.ToProblem();
    }

    [HttpPatch("elementos/{elementoId:guid}/revision")]
    public async Task<ActionResult<ChecklistObligacionResponse>> Revisar(
        Guid autonomoId, Guid obligacionId, Guid elementoId,
        [FromBody] CambiarRevisionChecklistRequest request, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await servicio.RevisarAsync(usuario, autonomoId, obligacionId, elementoId, request.Confirmar, ct);
        return resultado.EsCorrecto ? Ok(ChecklistObligacionResponse.Desde(resultado.Valor!)) : resultado.Error!.ToProblem();
    }

    [HttpPost("evidencias/buscar")]
    public async Task<ActionResult<int>> BuscarEvidencias(Guid autonomoId, Guid obligacionId, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await vinculacionEvidencias.BuscarParaObligacionAsync(usuario, autonomoId, obligacionId, ct);
        return resultado.EsCorrecto ? Ok(resultado.Valor) : resultado.Error!.ToProblem();
    }

    [HttpPost("evidencias/{vinculoId:guid}/confirmar")]
    public Task<ActionResult<ChecklistObligacionResponse>> ConfirmarEvidencia(
        Guid autonomoId, Guid obligacionId, Guid vinculoId, CancellationToken ct) =>
        CambiarEvidenciaAsync(autonomoId, obligacionId, vinculoId, EstadoVinculoEvidencia.CONFIRMADA, ct);

    [HttpPost("evidencias/{vinculoId:guid}/rechazar")]
    public Task<ActionResult<ChecklistObligacionResponse>> RechazarEvidencia(
        Guid autonomoId, Guid obligacionId, Guid vinculoId, CancellationToken ct) =>
        CambiarEvidenciaAsync(autonomoId, obligacionId, vinculoId, EstadoVinculoEvidencia.RECHAZADA, ct);

    [HttpPost("evidencias/{vinculoId:guid}/retirar")]
    public Task<ActionResult<ChecklistObligacionResponse>> RetirarEvidencia(
        Guid autonomoId, Guid obligacionId, Guid vinculoId, CancellationToken ct) =>
        CambiarEvidenciaAsync(autonomoId, obligacionId, vinculoId, EstadoVinculoEvidencia.RETIRADA, ct);

    private async Task<ActionResult<ChecklistObligacionResponse>> CambiarEvidenciaAsync(
        Guid autonomoId, Guid obligacionId, Guid vinculoId, EstadoVinculoEvidencia estado, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await vinculacionEvidencias.CambiarEstadoAsync(usuario, autonomoId, obligacionId, vinculoId, estado, ct);
        return resultado.EsCorrecto
            ? Ok(ChecklistObligacionResponse.Desde(resultado.Valor!))
            : resultado.Error!.ToProblem();
    }
}
