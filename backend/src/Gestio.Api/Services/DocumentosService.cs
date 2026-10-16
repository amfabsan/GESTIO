using System.Security.Cryptography;
using Gestio.Api.Common;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gestio.Api.Services;

public sealed record ReglasSubida(long TamanoMaximoBytes, IReadOnlyList<TipoArchivoPermitido> TiposPermitidos);

public sealed record ArchivoOriginal(Stream Contenido, string TipoMime, string NombreArchivo);

public sealed class DocumentosService(
    ApplicationDbContext context,
    IFileStorageService storage,
    IOptions<DocumentosOptions> options,
    TimeProvider timeProvider,
    ILogger<DocumentosService> logger)
{
    public ReglasSubida ObtenerReglasSubida() => new(options.Value.TamanoMaximoBytes, ValidadorArchivo.TiposPermitidos);

    public async Task<IReadOnlyList<Documento>> ListarAsync(UsuarioActual usuario, CancellationToken cancellationToken) =>
        await ConDetalle(ConsultablesPor(usuario))
            .OrderByDescending(documento => documento.FechaSubida)
            .ToListAsync(cancellationToken);

    public Task<Documento?> ObtenerAsync(UsuarioActual usuario, Guid id, CancellationToken cancellationToken) =>
        ConDetalle(ConsultablesPor(usuario))
            .FirstOrDefaultAsync(documento => documento.Id == id, cancellationToken);

    public async Task<ArchivoOriginal?> AbrirOriginalAsync(UsuarioActual usuario, Guid id, CancellationToken cancellationToken)
    {
        var documento = await ConsultablesPor(usuario)
            .AsNoTracking()
            .FirstOrDefaultAsync(documento => documento.Id == id, cancellationToken);
        if (documento?.StorageKey is null) return null;

        var contenido = await storage.AbrirAsync(documento.StorageKey, cancellationToken);
        return contenido is null ? null : new ArchivoOriginal(contenido, documento.TipoMime, documento.NombreArchivo);
    }

    public async Task<Resultado<Documento>> SubirAsync(UsuarioActual usuario, string nombreArchivo, Stream contenido, long tamanoBytes, CancellationToken cancellationToken)
    {
        if (usuario.Rol != Rol.AUTONOMO) return DocumentoErrores.SoloAutonomoPuedeSubir;
        if (tamanoBytes <= 0) return DocumentoErrores.ArchivoVacio;
        if (tamanoBytes > options.Value.TamanoMaximoBytes) return DocumentoErrores.TamanoExcedido(options.Value.TamanoMaximoBytes);

        var cabecera = new byte[ValidadorArchivo.BytesCabecera];
        var leidos = await contenido.ReadAtLeastAsync(cabecera, cabecera.Length, throwOnEndOfStream: false, cancellationToken);
        var tipoMime = ValidadorArchivo.ObtenerTipoMimeReal(nombreArchivo, cabecera.AsSpan(0, leidos));
        if (tipoMime is null) return DocumentoErrores.TipoNoPermitido(ValidadorArchivo.TiposPermitidos.Select(tipo => tipo.Extension));

        contenido.Position = 0;
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(contenido, cancellationToken));
        contenido.Position = 0;

        string storageKey;
        try
        {
            storageKey = await storage.GuardarAsync(contenido, usuario.Id, ValidadorArchivo.ExtensionDeAlmacenamiento(tipoMime), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "No se pudo almacenar el archivo subido por {UsuarioId}", usuario.Id);
            return DocumentoErrores.AlmacenamientoFallido;
        }

        var nombre = Path.GetFileName(nombreArchivo);
        if (nombre.Length > 255) nombre = nombre[^255..];

        var ahora = timeProvider.GetUtcNow();
        var documento = new Documento
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
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
            usuario.Id,
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

        return (await ObtenerAsync(usuario, documento.Id, cancellationToken))!;
    }

    // Regla de acceso (RF7) en un único sitio. Provisional hasta PB-04: la vinculación ACTIVA se representa hoy con Usuario.GestorId.
    private IQueryable<Documento> ConsultablesPor(UsuarioActual usuario) =>
        usuario.Rol == Rol.GESTOR
            ? context.Documentos.Where(documento =>
                documento.UsuarioId == usuario.Id ||
                context.Vinculaciones.Any(vinculacion =>
                    vinculacion.AutonomoId == documento.UsuarioId &&
                    vinculacion.CodigoAsesoria == context.Usuarios
                        .Where(administrador => administrador.Id == usuario.Id)
                        .Select(administrador => administrador.CodigoAsesoria)
                        .FirstOrDefault() &&
                    vinculacion.Estado == EstadoVinculacion.ACTIVA))
            : context.Documentos.Where(documento => documento.UsuarioId == usuario.Id);

    private static IQueryable<Documento> ConDetalle(IQueryable<Documento> consulta) => consulta
        .AsNoTracking()
        .AsSplitQuery()
        .Include(documento => documento.Usuario)
        .Include(documento => documento.ExtraccionIa)
        .Include(documento => documento.Anomalias)
        .Include(documento => documento.Eventos);
}