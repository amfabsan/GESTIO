using Gestio.Api.Common;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Services.Whatsapp;
using Microsoft.AspNetCore.Mvc;

namespace Gestio.Api.Controllers;

// UT27 (PA 3605): el autónomo vincula, consulta y desvincula su número de WhatsApp.
// Identidad por X-Usuario-Id como el resto de la app (deuda conocida del Sprint 1: no hay login real).
[ApiController]
[Route("api/whatsapp/numero")]
public sealed class WhatsappNumeroController(
    VerificacionNumeroWhatsappService verificacionService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NumeroWhatsappResponse>> Get(CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var resultado = await verificacionService.ObtenerAsync(usuario, cancellationToken);
        return resultado.EsCorrecto ? Ok(Mapear(resultado.Valor!)) : resultado.Error!.ToProblem();
    }

    [HttpPost("verificacion")]
    public async Task<ActionResult<NumeroWhatsappResponse>> IniciarVerificacion(IniciarVerificacionWhatsappRequest request, CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var resultado = await verificacionService.IniciarAsync(usuario, request.Telefono, cancellationToken);
        return resultado.EsCorrecto ? Ok(Mapear(resultado.Valor!)) : resultado.Error!.ToProblem();
    }

    [HttpDelete]
    public async Task<ActionResult<NumeroWhatsappResponse>> Desvincular(CancellationToken cancellationToken)
    {
        var usuario = await currentUserService.ObtenerAsync(cancellationToken);
        if (usuario is null) return Unauthorized();

        var resultado = await verificacionService.DesvincularAsync(usuario, cancellationToken);
        return resultado.EsCorrecto ? Ok(Mapear(resultado.Valor!)) : resultado.Error!.ToProblem();
    }

    // Los teléfonos salen enmascarados: con la identidad por cabecera, quien suplante a un autónomo no ve su número.
    private static NumeroWhatsappResponse Mapear(EstadoNumeroWhatsapp estado) => new(
        estado.CanalHabilitado,
        estado.NumeroCanal,
        estado.TelefonoVerificado is not null,
        estado.TelefonoVerificado is null ? null : TelefonoWhatsapp.Enmascarar(estado.TelefonoVerificado),
        estado.FechaVerificacion,
        estado.CodigoVerificacion is null ? null : new VerificacionPendienteResponse(
            TelefonoWhatsapp.Enmascarar(estado.TelefonoPendiente!),
            estado.CodigoVerificacion,
            estado.CodigoCaducaEn!.Value));
}

public sealed record IniciarVerificacionWhatsappRequest(string? Telefono);

public sealed record NumeroWhatsappResponse(
    bool CanalHabilitado,
    string NumeroCanal,
    bool Verificado,
    string? TelefonoVerificado,
    DateTimeOffset? FechaVerificacion,
    VerificacionPendienteResponse? VerificacionPendiente);

public sealed record VerificacionPendienteResponse(string Telefono, string Codigo, DateTimeOffset CaducaEn);
