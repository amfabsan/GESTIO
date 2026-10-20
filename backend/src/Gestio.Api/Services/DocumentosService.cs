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
    ILogger<DocumentosService> logger,
    ILectorDocumento lector)
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

        // El original y la carga ya están confirmados. La extracción tiene su propio resultado.
        try { await ProcesarAsync(usuario, documento.Id, false, CancellationToken.None); }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo confirmar el procesamiento de {DocumentoId}; la carga original se conserva", documento.Id);
            context.ChangeTracker.Clear();
        }
        return (await ObtenerAsync(usuario, documento.Id, CancellationToken.None))!;
    }

    public async Task<Resultado<Documento>> ProcesarAsync(UsuarioActual usuario, Guid id, bool reintento, CancellationToken cancellationToken)
    {
        var documento = await ConsultablesPor(usuario).Include(d => d.Eventos).Include(d => d.ExtraccionIa)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (documento is null) return new Error("documento.no_encontrado", "Documento no encontrado.", TipoError.NO_ENCONTRADO);
        if (reintento ? documento.Estado is not (EstadoDocumento.ERROR or EstadoDocumento.PENDIENTE_VALIDACION)
            : documento.Estado != EstadoDocumento.SUBIDO)
            return new Error("documento.estado", "El documento no admite procesamiento en su estado actual.", TipoError.CONFLICTO);

        Stream? contenidoOriginal = null;
        Exception? falloOriginal = null;
        try
        {
            if (documento.StorageKey is not null)
                contenidoOriginal = await storage.AbrirAsync(documento.StorageKey, cancellationToken);
        }
        catch (Exception ex) { falloOriginal = ex; }
        await using var original = contenidoOriginal;

        var intento = new IntentoProcesamiento { DocumentoId = id, Inicio = timeProvider.GetUtcNow() };
        documento.Estado = EstadoDocumento.PROCESANDO;
        var inicio = new EventoDocumento(usuario.Id, TipoEventoDocumento.PROCESAMIENTO_INICIADO, intento.Inicio, "PROCESANDO: inicio de extracción del original.");
        documento.Eventos.Add(inicio);
        context.Add(inicio);
        context.Add(intento);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            return new Error("documento.ocupado", "Otro procesamiento ya ha comenzado. Actualiza el documento.", TipoError.CONFLICTO);
        }

        try
        {
            if (original is null) throw new InvalidOperationException("No se puede acceder al archivo original conservado.", falloOriginal);
            var reconocido = await lector.LeerAsync(original, documento.TipoMime, cancellationToken);
            intento.Version = reconocido.Version;
            var extraccion = ExtractorCampos.Extraer(reconocido, id);
            if (ExtractorCampos.ContarDatos(extraccion) == 0)
                throw new InvalidOperationException("Se ha reconocido texto, pero no se ha podido identificar ningún dato de factura. Prueba con el PDF original o una imagen más nítida y completa.");
            // Guardar una instantánea sin navegaciones para conservar los resultados de cada intento.
            intento.ValoresJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                extraccion.EmisorNombre, extraccion.EmisorNif, extraccion.NumeroFactura,
                extraccion.FechaEmision, extraccion.BaseImponible, extraccion.TipoIva,
                extraccion.CuotaIva, extraccion.Total, extraccion.DesglosesIva, extraccion.Advertencias, Confianza = (decimal?)null
            });
            extraccion.DatosCrudosJson = System.Text.Json.JsonSerializer.Serialize(new { texto = reconocido.Texto,
                lecturas = reconocido.Lecturas, desglosesIva = extraccion.DesglosesIva, advertencias = extraccion.Advertencias });
            if (documento.ExtraccionIa is not null) context.Remove(documento.ExtraccionIa);
            documento.ExtraccionIa = extraccion;
            context.Add(extraccion);
            documento.Estado = EstadoDocumento.PENDIENTE_VALIDACION;
            intento.Resultado = "CORRECTO";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo de extracción de {DocumentoId}", id);
            intento.Error = ex is InvalidOperationException ? ex.Message : ex is OperationCanceledException
                ? "Se agotó el tiempo de extracción. Puedes reintentar." : "No se pudo extraer el documento. Comprueba su legibilidad e inténtalo de nuevo.";
            intento.Resultado = "ERROR";
            documento.Estado = EstadoDocumento.ERROR;
        }
        intento.Fin = timeProvider.GetUtcNow();
        var fin = new EventoDocumento(usuario.Id,
            intento.Error is null ? TipoEventoDocumento.PROCESAMIENTO_CORRECTO : TipoEventoDocumento.PROCESAMIENTO_FALLIDO,
            intento.Fin.Value, intento.Error ?? "PENDIENTE_VALIDACION: datos propuestos disponibles para revisión.");
        documento.Eventos.Add(fin);
        context.Add(fin);
        await context.SaveChangesAsync(CancellationToken.None);
        return (await ObtenerAsync(usuario, id, CancellationToken.None))!;
    }

    // Regla de acceso (RF7) en un único sitio.
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
        .Include(documento => documento.Eventos)
        .Include(documento => documento.Intentos);
}
