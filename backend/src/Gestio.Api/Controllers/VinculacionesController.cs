using Gestio.Api.Common;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gestio.Api.Controllers;

[ApiController]
[Route("api/vinculaciones")]
public sealed class VinculacionesController(
    VinculacionesService vinculacionesService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VinculacionResponse>>> GetVinculaciones(CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var vinculaciones = await vinculacionesService.ListarAsync(usuario, cancellationToken);
        return Ok(vinculaciones.Select(Mapear).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<VinculacionResponse>> Solicitar(SolicitarVinculacionRequest request, CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var resultado = await vinculacionesService.SolicitarAsync(usuario, request.CodigoAsesoria, cancellationToken);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();

        return CreatedAtAction(nameof(GetVinculaciones), null, Mapear(resultado.Valor!));
    }

    [HttpPost("{id:guid}/aceptar")]
    public async Task<ActionResult<VinculacionResponse>> Aceptar(Guid id, CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var resultado = await vinculacionesService.AceptarAsync(usuario, id, cancellationToken);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();

        return Ok(Mapear(resultado.Valor!));
    }

    [HttpPost("{id:guid}/rechazar")]
    public async Task<ActionResult<VinculacionResponse>> Rechazar(Guid id, CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var resultado = await vinculacionesService.RechazarAsync(usuario, id, cancellationToken);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();

        return Ok(Mapear(resultado.Valor!));
    }

    [HttpPost("{id:guid}/finalizar")]
    public async Task<ActionResult<VinculacionResponse>> Finalizar(Guid id, CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var resultado = await vinculacionesService.FinalizarAsync(usuario, id, cancellationToken);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();

        return Ok(Mapear(resultado.Valor!));
    }

    private static VinculacionResponse Mapear(Vinculacion vinculacion) => new(
        vinculacion.Id,
        vinculacion.AutonomoId,
        vinculacion.Autonomo is null ? null : new AutonomoVinculadoResponse(
            vinculacion.Autonomo.Id,
            vinculacion.Autonomo.Nombre,
            vinculacion.Autonomo.PrimerApellido,
            vinculacion.Autonomo.SegundoApellido,
            vinculacion.Autonomo.Nif),
        vinculacion.CodigoAsesoria,
        vinculacion.Estado.ToString(),
        vinculacion.FechaSolicitud,
        vinculacion.FechaAceptacion,
        vinculacion.AceptadaPorId,
        vinculacion.FechaFinalizacion,
        vinculacion.FinalizadaPorId,
        vinculacion.FechaRechazo,
        vinculacion.RechazadaPorId);
}

public sealed record SolicitarVinculacionRequest(string CodigoAsesoria);

public sealed record VinculacionResponse(
    Guid Id,
    Guid AutonomoId,
    AutonomoVinculadoResponse? Autonomo,
    string CodigoAsesoria,
    string Estado,
    DateTimeOffset FechaSolicitud,
    DateTimeOffset? FechaAceptacion,
    Guid? AceptadaPorId,
    DateTimeOffset? FechaFinalizacion,
    Guid? FinalizadaPorId,
    DateTimeOffset? FechaRechazo,
    Guid? RechazadaPorId);

public sealed record AutonomoVinculadoResponse(Guid Id, string Nombre, string PrimerApellido, string SegundoApellido, string Nif);
