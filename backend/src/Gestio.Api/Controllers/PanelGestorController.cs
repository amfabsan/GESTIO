using Gestio.Api.Common;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gestio.Api.Controllers;

[ApiController]
[Route("gestor/panel")]
public sealed class PanelGestorController(
    PanelGestorService service,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ObtenerPanel(
        [FromQuery] bool soloRequiereAtencion = false,
        [FromQuery] string? ordenarPor = null,
        [FromQuery] string? busqueda = null,
        CancellationToken ct = default)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();

        var resultado = await service.ObtenerPanelAsync(usuario, soloRequiereAtencion, ordenarPor, busqueda, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();

        return Ok(resultado.Valor);
    }

    [HttpGet("clientes/{autonomoId:guid}")]
    public async Task<IActionResult> ObtenerDetalleCliente(Guid autonomoId, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();

        var resultado = await service.ObtenerDetalleClienteAsync(usuario, autonomoId, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();

        return Ok(resultado.Valor);
    }
}