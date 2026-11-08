using Gestio.Api.Common;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gestio.Api.Controllers;

[ApiController]
[Route("api/autonomos/{autonomoId:guid}/perfiles-fiscales")]
public sealed class PerfilesFiscalesController(PerfilesFiscalesService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("catalogo")]
    public async Task<IActionResult> Catalogo(Guid autonomoId, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        if (!await service.PuedeAccederAsync(usuario, autonomoId, false, ct))
            return new Gestio.Api.Domain.Errores.Error("PerfilFiscal.Prohibido", "No tienes permiso para acceder a este perfil fiscal.", Gestio.Api.Domain.Errores.TipoError.PROHIBIDO).ToProblem();
        return Ok(new { iva = PerfilesFiscalesService.CatalogoIva, irpf = PerfilesFiscalesService.CatalogoIrpf });
    }

    [HttpGet("{ejercicio:int}")]
    public async Task<IActionResult> Consultar(Guid autonomoId, int ejercicio, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await service.ConsultarAsync(usuario, autonomoId, ejercicio, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();
        return Ok(new { perfil = resultado.Valor is null ? null : Mapear(resultado.Valor) });
    }

    [HttpPut("{ejercicio:int}")]
    public async Task<IActionResult> Guardar(Guid autonomoId, int ejercicio, GuardarPerfilFiscalRequest request, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await service.GuardarAsync(usuario, autonomoId, ejercicio, request, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();
        return Ok(Mapear(resultado.Valor!));
    }

    private static object Mapear(PerfilFiscal p) => new
    {
        p.Id, p.AutonomoId, p.Ejercicio, p.RegimenIva, p.RegimenIrpf,
        p.TrabajadoresConRetencion, p.LocalAlquiladoConRetencion,
        historial = p.Historial.OrderByDescending(h => h.FechaCambio).Select(h => new
        {
            h.Id, h.UsuarioId, gestor = h.Usuario is null ? null : $"{h.Usuario.Nombre} {h.Usuario.PrimerApellido} {h.Usuario.SegundoApellido}".Trim(),
            h.FechaCambio, h.Campo, h.ValorAnterior, h.ValorNuevo
        })
    };
}
