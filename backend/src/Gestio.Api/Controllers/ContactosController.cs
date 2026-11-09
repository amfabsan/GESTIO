using Gestio.Api.Common;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gestio.Api.Controllers;

[ApiController]
[Route("api/autonomos/{autonomoId:guid}/contactos")]
public sealed class ContactosController(ContactosService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(Guid autonomoId, [FromQuery] string? busqueda, [FromQuery] string? tipo,
        [FromQuery] string? estado, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await service.ListarAsync(usuario, autonomoId, busqueda, tipo, estado, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();
        return Ok(resultado.Valor!.Select(Mapear));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtener(Guid autonomoId, Guid id, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await service.ConsultarAsync(usuario, autonomoId, id, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();
        return Ok(Mapear(resultado.Valor!));
    }

    [HttpPost]
    public async Task<IActionResult> Crear(Guid autonomoId, GuardarContactoRequest request, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await service.CrearAsync(usuario, autonomoId, request, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();
        return CreatedAtAction(nameof(Obtener), new { autonomoId, id = resultado.Valor!.Id }, Mapear(resultado.Valor));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid autonomoId, Guid id, GuardarContactoRequest request, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await service.EditarAsync(usuario, autonomoId, id, request, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();
        return Ok(Mapear(resultado.Valor!));
    }

    [HttpPost("{id:guid}/activar")]
    public Task<IActionResult> Activar(Guid autonomoId, Guid id, CancellationToken ct) => CambiarEstado(autonomoId, id, true, ct);

    [HttpPost("{id:guid}/desactivar")]
    public Task<IActionResult> Desactivar(Guid autonomoId, Guid id, CancellationToken ct) => CambiarEstado(autonomoId, id, false, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid autonomoId, Guid id, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await service.EliminarAsync(usuario, autonomoId, id, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();
        return NoContent();
    }

    private async Task<IActionResult> CambiarEstado(Guid autonomoId, Guid id, bool activo, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await service.CambiarEstadoAsync(usuario, autonomoId, id, activo, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();
        return Ok(Mapear(resultado.Valor!));
    }

    private static ContactoResponse Mapear(Contacto contacto) => new(
        contacto.Id, contacto.AutonomoId, contacto.Clasificacion, contacto.Nombre, contacto.Nif,
        contacto.Email, contacto.Telefono, contacto.Direccion, contacto.Activo);
}

public sealed record ContactoResponse(Guid Id, Guid AutonomoId, string Clasificacion, string Nombre, string Nif,
    string? Email, string? Telefono, string? Direccion, bool Activo);
