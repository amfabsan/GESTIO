namespace Gestio.Api.Domain.Interfaces;

public interface IFileStorageService
{
    Task<string> GuardarAsync(Stream contenido, Guid usuarioId, string extension, CancellationToken cancellationToken);

    Task<Stream?> AbrirAsync(string storageKey, CancellationToken cancellationToken);

    Task EliminarAsync(string storageKey, CancellationToken cancellationToken);
    
}