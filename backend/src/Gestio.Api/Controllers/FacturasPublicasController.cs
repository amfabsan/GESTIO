using Gestio.Api.Common;
using Gestio.Api.Models.Response;
using Gestio.Api.Services.Verifactu;
using Microsoft.AspNetCore.Mvc;

namespace Gestio.Api.Controllers;

/// <summary>
/// UT23 (PB-23): página a la que lleva el QR. Sin sesión: no recibe ICurrentUserService ni lee X-Usuario-Id.
/// Req. 6 / prueba 1607: solo GET. No hay ninguna operación de escritura ni acceso a otros datos.
/// Req. 7 / prueba 1604: cualquier enlace no válido da el mismo 404, sin datos.
/// </summary>
[ApiController]
[Route("api/publico/facturas")]
public sealed class FacturasPublicasController(ConsultaPublicaFacturaService consulta) : ControllerBase
{
    // Sin restricción de formato en la ruta: así un código malformado no da un 404 distinto del enrutado,
    // sino exactamente el mismo cuerpo que uno inexistente.
    [HttpGet("{codigo}")]
    public async Task<IActionResult> Consultar(string codigo, CancellationToken ct)
    {
        // Página pública de demostración: que no la guarden cachés ni se envíe el código como Referer a otros sitios.
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Robots-Tag"] = "noindex";

        var resultado = await consulta.ConsultarAsync(codigo, ct);
        if (!resultado.EsCorrecto) return resultado.Error!.ToProblem();
        return Ok(FacturaPublicaResponse.Desde(resultado.Valor!));
    }
}
