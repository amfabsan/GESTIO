using Gestio.Api.Common;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Models.Response;
using Gestio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gestio.Api.Controllers;

[ApiController]
[Route("api/autonomos/{autonomoId:guid}/obligaciones-fiscales")]
public sealed class ObligacionesFiscalesController(ObligacionesFiscalesService servicio, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("{ejercicio:int}")]
    public async Task<ActionResult<CalendarioFiscalResponse>> Consultar(Guid autonomoId, int ejercicio, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await servicio.ConsultarAsync(usuario, autonomoId, ejercicio, ct);
        return resultado.EsCorrecto ? Ok(resultado.Valor) : resultado.Error!.ToProblem();
    }

    [HttpPost("{ejercicio:int}/generar")]
    public async Task<ActionResult<CalendarioFiscalResponse>> Generar(Guid autonomoId, int ejercicio, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await servicio.GenerarAsync(usuario, autonomoId, ejercicio, ct);
        return resultado.EsCorrecto ? Ok(resultado.Valor) : resultado.Error!.ToProblem();
    }

    [HttpGet("detalle/{obligacionId:guid}")]
    public async Task<ActionResult<ObligacionFiscalDetalleResponse>> Detalle(Guid autonomoId, Guid obligacionId, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await servicio.DetalleAsync(usuario, autonomoId, obligacionId, ct);
        return resultado.EsCorrecto ? Ok(resultado.Valor) : resultado.Error!.ToProblem();
    }
}
