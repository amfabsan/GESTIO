using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Infrastructure.Identidad;

// Sprint 1 sin login: el usuario es el perfil elegido en pantalla, que el frontend envía en la cabecera X-Usuario-Id.
// Si en el siguiente sprint se añade login, se sustituye por una implementación basada en JWT sin tocar controladores ni servicios.
public sealed class PerfilSeleccionadoService(IHttpContextAccessor httpContextAccessor, ApplicationDbContext context) : ICurrentUserService
{
    public const string NombreCabecera = "X-Usuario-Id";

    public async Task<UsuarioActual?> ObtenerAsync(CancellationToken cancellationToken)
    {

        var valor = httpContextAccessor.HttpContext?.Request.Headers[NombreCabecera].ToString();
        
        if (!Guid.TryParse(valor, out var usuarioId)) return null;

        return await context.Usuarios
            .AsNoTracking()
            .Where(usuario => usuario.Id == usuarioId)
            .Select(usuario => new UsuarioActual(usuario.Id, usuario.Rol))
            .FirstOrDefaultAsync(cancellationToken);

    }
}