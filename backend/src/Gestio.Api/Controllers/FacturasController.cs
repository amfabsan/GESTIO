using Gestio.Api.Common;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Models.Request;
using Gestio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gestio.Api.Controllers;

[ApiController]
[Route("api/autonomos/{autonomoId:guid}/facturas")]
public sealed class FacturasController(FacturacionService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(Guid autonomoId, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await service.ListarAsync(usuario, autonomoId, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();
        return Ok(resultado.Valor);
    }

    [HttpGet("{facturaId:guid}")]
    public async Task<IActionResult> Obtener(Guid autonomoId, Guid facturaId, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await service.ConsultarAsync(usuario, autonomoId, facturaId, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();
        return Ok(resultado.Valor);
    }

    [HttpPost]
    public async Task<IActionResult> Crear(Guid autonomoId, GuardarFacturaRequest request, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await service.GuardarBorradorAsync(usuario, autonomoId, null, request, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();
        return CreatedAtAction(nameof(Obtener), new { autonomoId, facturaId = resultado.Valor!.Id }, resultado.Valor);
    }

    [HttpPut("{facturaId:guid}")]
    public async Task<IActionResult> Editar(Guid autonomoId, Guid facturaId, GuardarFacturaRequest request, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await service.GuardarBorradorAsync(usuario, autonomoId, facturaId, request, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();
        return Ok(resultado.Valor);
    }

    [HttpDelete("{facturaId:guid}")]
    public async Task<IActionResult> Eliminar(Guid autonomoId, Guid facturaId, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await service.EliminarBorradorAsync(usuario, autonomoId, facturaId, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();
        return NoContent();
    }

    [HttpPost("{facturaId:guid}/comprobar")]
    public async Task<IActionResult> Comprobar(Guid autonomoId, Guid facturaId, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await service.ComprobarAsync(usuario, autonomoId, facturaId, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();
        return Ok(resultado.Valor);
    }

    [HttpPost("{facturaId:guid}/emitir")]
    public async Task<IActionResult> Emitir(Guid autonomoId, Guid facturaId, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await service.EmitirAsync(usuario, autonomoId, facturaId, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();
        return Ok(resultado.Valor);
    }
}