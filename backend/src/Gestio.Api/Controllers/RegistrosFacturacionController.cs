using Gestio.Api.Common;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Models.Response;
using Gestio.Api.Services.Verifactu;
using Microsoft.AspNetCore.Mvc;

namespace Gestio.Api.Controllers;

/// <summary>
/// UT22: consulta y verificación de la cadena de registros de facturación (simulación académica de VeriFactu).
/// Solo lectura a propósito (req. 7 / PA 1598): los registros se crean al emitir una factura y no hay
/// ninguna ruta para modificarlos ni eliminarlos. Una prueba lo comprueba sobre todas las rutas de la API.
/// </summary>
[ApiController]
[Route("api/autonomos/{autonomoId:guid}/registros-facturacion")]
public sealed class RegistrosFacturacionController(RegistroFacturacionService servicio, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RegistroFacturacionResponse>>> Listar(Guid autonomoId, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await servicio.ListarAsync(usuario, autonomoId, ct);
        return resultado.EsCorrecto ? Ok(resultado.Valor) : resultado.Error!.ToProblem();
    }

    [HttpGet("verificacion")]
    public async Task<ActionResult<VerificacionCadenaResponse>> Verificar(Guid autonomoId, CancellationToken ct)
    {
        var usuario = await currentUser.ObtenerAsync(ct);
        if (usuario is null) return Unauthorized();
        var resultado = await servicio.VerificarAsync(usuario, autonomoId, ct);
        return resultado.EsCorrecto ? Ok(resultado.Valor) : resultado.Error!.ToProblem();
    }
}
