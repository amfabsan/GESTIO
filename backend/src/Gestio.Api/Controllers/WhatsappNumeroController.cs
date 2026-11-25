using Gestio.Api.Common;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Whatsapp;
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
    // UT27, capa 5: QR del canal y, con una verificación pendiente, el enlace y el QR que abren el chat con el código.
    private static NumeroWhatsappResponse Mapear(EstadoNumeroWhatsapp estado)
    {
        var enlaceCanal = estado.CanalHabilitado ? QrWhatsapp.EnlaceCanal(estado.NumeroCanal) : null;
        var enlaceCodigo = enlaceCanal is null || estado.CodigoVerificacion is null
            ? null
            : QrWhatsapp.EnlaceCanal(estado.NumeroCanal, estado.CodigoVerificacion);

        return new NumeroWhatsappResponse(
            estado.CanalHabilitado,
            estado.NumeroCanal,
            estado.TelefonoVerificado is not null,
            estado.TelefonoVerificado is null ? null : TelefonoWhatsapp.Enmascarar(estado.TelefonoVerificado),
            estado.FechaVerificacion,
            enlaceCanal,
            enlaceCanal is null ? null : QrWhatsapp.ImagenPngBase64(enlaceCanal),
            estado.CodigoVerificacion is null ? null : new VerificacionPendienteResponse(
                TelefonoWhatsapp.Enmascarar(estado.TelefonoPendiente!),
                estado.CodigoVerificacion,
                estado.CodigoCaducaEn!.Value,
                enlaceCodigo,
                enlaceCodigo is null ? null : QrWhatsapp.ImagenPngBase64(enlaceCodigo)));
    }
}

public sealed record IniciarVerificacionWhatsappRequest(string? Telefono);

public sealed record NumeroWhatsappResponse(
    bool CanalHabilitado,
    string NumeroCanal,
    bool Verificado,
    string? TelefonoVerificado,
    DateTimeOffset? FechaVerificacion,
    string? EnlaceCanal,
    string? QrCanalPngBase64,
    VerificacionPendienteResponse? VerificacionPendiente);

public sealed record VerificacionPendienteResponse(
    string Telefono,
    string Codigo,
    DateTimeOffset CaducaEn,
    string? EnlaceCodigo,
    string? QrCodigoPngBase64);
