using Gestio.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
public sealed class UsuariosController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet("perfiles")]
    public async Task<ActionResult<IReadOnlyList<UsuarioPerfilResponse>>> GetPerfiles(CancellationToken cancellationToken)
    {
        var perfiles = await context.Usuarios
            .AsNoTracking()
            .OrderBy(usuario => usuario.Rol)
            .ThenBy(usuario => usuario.Nombre)
            .Select(usuario => new UsuarioPerfilResponse(
                usuario.Id,
                usuario.Nombre,
                usuario.PrimerApellido,
                usuario.SegundoApellido,
                usuario.Rol.ToString()))
            .ToListAsync(cancellationToken);

        return Ok(perfiles);
    }
}

public sealed record UsuarioPerfilResponse(Guid Id, string Nombre, string PrimerApellido, string SegundoApellido, string Rol);
