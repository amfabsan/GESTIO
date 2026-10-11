using System.Security.Cryptography;
using Gestio.Api.Common;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Microsoft.Extensions.Options;

namespace Gestio.Api.Services;

public sealed class DocumentosService(
    ApplicationDbContext context,
    IFileStorageService storage,
    IOptions<DocumentosOptions> options,
    TimeProvider timeProvider,
    ILogger<DocumentosService> logger)
{
    public IQueryable<Documento> ConsultablesPor(UsuarioActual usuario) =>
        usuario.Rol == Rol.GESTOR
            // Provisional hasta PB-04: la vinculación ACTIVA se representa hoy con Usuario.GestorId.
            ? context.Documentos.Where(documento => documento.UsuarioId == usuario.Id || documento.Usuario.GestorId == usuario.Id)
            : context.Documentos.Where(documento => documento.UsuarioId == usuario.Id);

    public Task<Stream?> AbrirOriginalAsync(Documento documento, CancellationToken cancellationToken) =>
        documento.StorageKey is null
            ? Task.FromResult<Stream?>(null)
            : storage.AbrirAsync(documento.StorageKey, cancellationToken);

    public async Task<Resultado<Documento>> SubirAsync(Guid usuarioId, string nombreArchivo, Stream contenido, long tamanoBytes, CancellationToken cancellationToken)
    {
        if (tamanoBytes <= 0) return DocumentoErrores.ArchivoVacio;
        if (tamanoBytes > options.Value.TamanoMaximoBytes) return DocumentoErrores.TamanoExcedido(options.Value.TamanoMaximoBytes);

        var cabecera = new byte[ValidadorArchivo.BytesCabecera];
        var leidos = await contenido.ReadAtLeastAsync(cabecera, cabecera.Length, throwOnEndOfStream: false, cancellationToken);
        var tipoMime = ValidadorArchivo.ObtenerTipoMimeReal(nombreArchivo, cabecera.AsSpan(0, leidos));
        if (tipoMime is null) return DocumentoErrores.TipoNoPermitido;

        contenido.Position = 0;
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(contenido, cancellationToken));
        contenido.Position = 0;

        string storageKey;
        try
        {
            storageKey = await storage.GuardarAsync(contenido, usuarioId, ValidadorArchivo.ExtensionDeAlmacenamiento(tipoMime), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "No se pudo almacenar el archivo subido por {UsuarioId}", usuarioId);
            return DocumentoErrores.AlmacenamientoFallido;
        }

        var nombre = Path.GetFileName(nombreArchivo);
        if (nombre.Length > 255) nombre = nombre[^255..];

        var ahora = timeProvider.GetUtcNow();
        var documento = new Documento
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            NombreArchivo = nombre,
            TipoMime = tipoMime,
            TamanoBytes = tamanoBytes,
            Origen = OrigenDocumento.SUBIDA_WEB,
            StorageKey = storageKey,
            HashSha256 = hash,
            FechaSubida = ahora,
            Estado = EstadoDocumento.SUBIDO
        };
        documento.Eventos.Add(new EventoDocumento(
            usuarioId,
            TipoEventoDocumento.SUBIDA,
            ahora,
            $"Archivo original recibido ({tamanoBytes} bytes)."));

        context.Documentos.Add(documento);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Sin registro en BD no debe quedar un original huérfano.
            await storage.EliminarAsync(storageKey, CancellationToken.None);
            throw;
        }

        return documento;
    }
}