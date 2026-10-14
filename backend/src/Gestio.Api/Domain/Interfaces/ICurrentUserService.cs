using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Domain.Interfaces;

public interface ICurrentUserService
{
    Task<UsuarioActual?> ObtenerAsync(CancellationToken cancellationToken);
}

public sealed record UsuarioActual(Guid Id, Rol Rol);