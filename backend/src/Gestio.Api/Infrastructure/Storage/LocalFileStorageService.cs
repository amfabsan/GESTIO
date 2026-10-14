using Gestio.Api.Common;
using Gestio.Api.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace Gestio.Api.Infrastructure.Storage;

// Guarda cada original en una carpeta privada por usuario: {usuarioId}/{guid}{extension}.
public sealed class LocalFileStorageService(IOptions<DocumentosOptions> options, IHostEnvironment environment) : IFileStorageService
{
    private readonly string raiz = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.RutaAlmacenamiento));

    public async Task<string> GuardarAsync(Stream contenido, Guid usuarioId, string extension, CancellationToken cancellationToken)
    {
        var storageKey = $"{usuarioId:N}/{Guid.NewGuid():N}{extension}";
        var ruta = ObtenerRuta(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);

        try
        {
            await using var destino = new FileStream(ruta, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await contenido.CopyToAsync(destino, cancellationToken);
        }
        catch
        {
            File.Delete(ruta);
            throw;
        }

        return storageKey;
    }

    public Task<Stream?> AbrirAsync(string storageKey, CancellationToken cancellationToken)
    {
        var ruta = ObtenerRuta(storageKey);
        Stream? archivo = File.Exists(ruta)
            ? new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;

        return Task.FromResult(archivo);
    }

    public Task EliminarAsync(string storageKey, CancellationToken cancellationToken)
    {
        File.Delete(ObtenerRuta(storageKey));
        return Task.CompletedTask;
    }

    private string ObtenerRuta(string storageKey)
    {
        var ruta = Path.GetFullPath(Path.Combine(raiz, storageKey));
        if (!ruta.StartsWith(raiz + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Clave de almacenamiento no válida.");

        return ruta;
    }
}