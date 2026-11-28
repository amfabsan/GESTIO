using Gestio.Api.Common;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gestio.Api.Services.Whatsapp;

// UT27: recepción de mensajes de WhatsApp (PA 3600-3604, 3606).
//
// RecibirAsync se ejecuta dentro de la petición del webhook y es rápido: reclama el mensaje, identifica el número,
// resuelve los textos y los números no vinculados, y deja los archivos de números vinculados en la cola (D1).
// ProcesarArchivoAsync lo ejecuta ProcesadorMensajesWhatsapp en segundo plano: descarga, guarda por el flujo de PB-05,
// confirma y lanza la extracción.
public sealed class RecepcionWhatsappService(
    ApplicationDbContext context,
    DocumentosService documentosService,
    VerificacionNumeroWhatsappService verificacionService,
    IProveedorWhatsapp proveedor,
    ColaMensajesWhatsapp cola,
    IOptions<DocumentosOptions> documentosOptions,
    TimeProvider timeProvider,
    ILogger<RecepcionWhatsappService> logger)
{
    // D3/D4: un EN_CURSO sin novedades en este tiempo se da por muerto y una nueva entrega lo reprocesa.
    public static readonly TimeSpan EnCursoAbandonado = TimeSpan.FromMinutes(5);

    // Tope para un mensaje que falla siempre de forma inesperada: no se reintenta en cada arranque indefinidamente.
    public const int MaximoIntentos = 5;

    private static readonly Dictionary<string, string> ExtensionPorMime = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = ".pdf",
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png"
    };

    public async Task RecibirAsync(MensajeEntrante mensaje, CancellationToken cancellationToken)
    {
        var ahora = timeProvider.GetUtcNow();

        // 1. Reclamar el mensaje (PA 3603).
        var existente = await context.MensajesWhatsapp.FirstOrDefaultAsync(m => m.MensajeId == mensaje.MensajeId, cancellationToken);
        if (existente is not null)
        {
            // D3: un EN_CURSO abandonado antes de llegar a guardar el archivo (murió entre los dos guardados de esta
            // petición) no tiene nada que reencolar: se descarta y esta entrega se trata como nueva.
            var abandonadoSinArchivo = existente.Resultado == ResultadoMensajeWhatsapp.EN_CURSO && existente.MediaId is null &&
                                       ahora - existente.FechaActualizacion >= EnCursoAbandonado;
            if (!abandonadoSinArchivo)
            {
                await GestionarEntregaRepetidaAsync(existente, ahora, cancellationToken);
                return;
            }

            context.MensajesWhatsapp.Remove(existente);
            await context.SaveChangesAsync(cancellationToken);
        }

        var registro = new MensajeWhatsappRecibido
        {
            MensajeId = mensaje.MensajeId,
            Tipo = mensaje.Tipo,
            Resultado = ResultadoMensajeWhatsapp.EN_CURSO,
            FechaRecepcion = ahora,
            FechaActualizacion = ahora
        };
        context.MensajesWhatsapp.Add(registro);
        try
        {
            // Se guarda ANTES de cualquier otra cosa: la verificación del número guarda con este mismo contexto.
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            context.Entry(registro).State = EntityState.Detached;
            // Otra entrega simultánea del mismo mensaje lo reclamó primero: esta no hace nada ni responde.
            if (await context.MensajesWhatsapp.AsNoTracking().AnyAsync(m => m.MensajeId == mensaje.MensajeId, cancellationToken)) return;

            // Cualquier otro error de base de datos no se calla: queda registrado y sube al webhook.
            logger.LogError(ex, "No se pudo registrar el mensaje de WhatsApp entrante");
            throw;
        }

        // 2. Identificar el número.
        var usuarioId = await UsuarioVerificadoAsync(mensaje.Telefono, cancellationToken);

        // 3. Textos: verificación de un número o ayuda.
        if (mensaje.Tipo == TipoMensajeWhatsapp.TEXTO)
        {
            var verificacion = await verificacionService.VerificarDesdeMensajeAsync(mensaje.Telefono, mensaje.Texto, cancellationToken);
            if (verificacion == ResultadoVerificacionWhatsapp.VERIFICADO)
            {
                await TerminarAsync(registro, ResultadoMensajeWhatsapp.RESPONDIDO, "numero_verificado",
                    await UsuarioVerificadoAsync(mensaje.Telefono, cancellationToken), cancellationToken);
                await ResponderAsync(mensaje.Telefono, TextosWhatsapp.NumeroVerificado, cancellationToken);
                return;
            }

            if (usuarioId is not null)
            {
                var codigoErroneo = verificacion == ResultadoVerificacionWhatsapp.RECHAZADO;
                await TerminarAsync(registro, ResultadoMensajeWhatsapp.RESPONDIDO, codigoErroneo ? "codigo_no_valido" : "ayuda",
                    usuarioId, cancellationToken);
                await ResponderAsync(mensaje.Telefono, codigoErroneo ? TextosWhatsapp.CodigoNoValido : TextosWhatsapp.Ayuda, cancellationToken);
                return;
            }
        }

        // 4. Número no vinculado (PA 3606): mismo texto siempre y el archivo ni se descarga (D5).
        if (usuarioId is null)
        {
            await TerminarAsync(registro, ResultadoMensajeWhatsapp.NO_VINCULADO, "numero_no_vinculado", null, cancellationToken);
            await ResponderAsync(mensaje.Telefono, TextosWhatsapp.NumeroNoVinculado, cancellationToken);
            return;
        }

        // 5. Tipo que no puede ser un documento (audio, ubicación, sticker…).
        if (mensaje.Tipo is not (TipoMensajeWhatsapp.IMAGEN or TipoMensajeWhatsapp.DOCUMENTO) || mensaje.MediaId is null)
        {
            await TerminarAsync(registro, ResultadoMensajeWhatsapp.RECHAZADO, "tipo_mensaje_no_admitido", usuarioId, cancellationToken);
            await ResponderAsync(mensaje.Telefono, TextosWhatsapp.TipoNoAdmitido, cancellationToken);
            return;
        }

        // 6. Archivo de un número vinculado: se guarda lo necesario para procesarlo y se encola.
        registro.UsuarioId = usuarioId;
        registro.MediaId = mensaje.MediaId;
        registro.MimeType = Truncar(mensaje.MimeType, 100);
        registro.NombreArchivo = Truncar(NombreArchivo(mensaje, ahora), 255);
        await context.SaveChangesAsync(cancellationToken);
        cola.Encolar(registro.MensajeId);
    }

    public async Task ProcesarArchivoAsync(string mensajeId, CancellationToken cancellationToken)
    {
        var registro = await context.MensajesWhatsapp.FirstOrDefaultAsync(m => m.MensajeId == mensajeId, cancellationToken);
        if (registro is not { Resultado: ResultadoMensajeWhatsapp.EN_CURSO, UsuarioId: not null, MediaId: not null }) return;

        registro.Intentos++;
        registro.FechaActualizacion = timeProvider.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
        var agotado = registro.Intentos > MaximoIntentos;

        var usuario = await context.Usuarios.AsNoTracking()
            .Where(u => u.Id == registro.UsuarioId)
            .Select(u => new UsuarioActual(u.Id, u.Rol))
            .FirstAsync(cancellationToken);
        var telefono = await context.NumerosWhatsapp.AsNoTracking()
            .Where(n => n.UsuarioId == usuario.Id)
            .Select(n => n.TelefonoVerificado)
            .FirstOrDefaultAsync(cancellationToken);
        var nombre = registro.NombreArchivo ?? "archivo";

        if (agotado && telefono is not null)
        {
            logger.LogError("Mensaje de WhatsApp {MensajeId} abandonado tras {Intentos} intentos", mensajeId, registro.Intentos - 1);
            await FallarAsync(registro, telefono, "intentos_agotados", cancellationToken);
            return;
        }

        // Desvinculado mientras esperaba en la cola: ya no hay a quién asignarlo ni a quién responder.
        if (telefono is null)
        {
            await TerminarAsync(registro, ResultadoMensajeWhatsapp.NO_VINCULADO, "desvinculado_en_cola", registro.UsuarioId, cancellationToken);
            return;
        }

        // Un intento anterior llegó a crear el documento (D3): no se crea otro ni se confirma dos veces.
        if (await DocumentoDelMensajeAsync(mensajeId, cancellationToken) is { } yaCreado)
        {
            registro.DocumentoId = yaCreado;
            await TerminarAsync(registro, ResultadoMensajeWhatsapp.DOCUMENTO_CREADO, null, registro.UsuarioId, cancellationToken);
            await ExtraerSiPendienteAsync(usuario, yaCreado);
            return;
        }

        var maximo = documentosOptions.Value.TamanoMaximoBytes;
        MemoryStream? contenido;
        try
        {
            var media = await proveedor.ObtenerMediaAsync(registro.MediaId, cancellationToken);
            if (media.TamanoBytes > maximo)
            {
                await RechazarAsync(registro, telefono, nombre, DocumentoErrores.TamanoExcedido(maximo), cancellationToken);
                return;
            }

            var descarga = await proveedor.DescargarMediaAsync(registro.MediaId, maximo, cancellationToken);
            if (descarga.Excedido)
            {
                await RechazarAsync(registro, telefono, nombre, DocumentoErrores.TamanoExcedido(maximo), cancellationToken);
                return;
            }
            contenido = descarga.Contenido;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Requisito 8: el fallo de descarga queda registrado y se responde con un error (nunca con confirmación).
            logger.LogError(ex, "No se pudo descargar el adjunto del mensaje de WhatsApp {MensajeId}", mensajeId);
            await FallarAsync(registro, telefono, "descarga_fallida", cancellationToken);
            return;
        }

        await using var archivo = contenido ?? new MemoryStream();
        Resultado<Documento> subida;
        try
        {
            subida = await documentosService.SubirAsync(usuario, nombre, archivo, archivo.Length, cancellationToken,
                OrigenDocumento.WHATSAPP, mensajeId, procesar: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // SubirAsync lanza si falla el guardado en la base de datos (y ya ha borrado el archivo almacenado).
            context.ChangeTracker.Clear();
            registro = await RecargarAsync(mensajeId, cancellationToken);
            if (await DocumentoDelMensajeAsync(mensajeId, cancellationToken) is { } creadoEnParalelo)
            {
                registro.DocumentoId = creadoEnParalelo;
                await TerminarAsync(registro, ResultadoMensajeWhatsapp.DOCUMENTO_CREADO, null, registro.UsuarioId, cancellationToken);
                await ExtraerSiPendienteAsync(usuario, creadoEnParalelo);
                return;
            }

            logger.LogError(ex, "No se pudo guardar el documento del mensaje de WhatsApp {MensajeId}", mensajeId);
            await FallarAsync(registro, telefono, "guardado_fallido", cancellationToken);
            return;
        }

        // SubirAsync puede haber vaciado el seguimiento del contexto (fallo al comprobar duplicados): se relee.
        registro = await RecargarAsync(mensajeId, cancellationToken);

        if (!subida.EsCorrecto)
        {
            if (subida.Error!.Codigo == DocumentoErrores.AlmacenamientoFallido.Codigo)
            {
                // PA 3604: sin almacenamiento no hay documento ni confirmación de recepción.
                logger.LogError("No se pudo almacenar el archivo del mensaje de WhatsApp {MensajeId}", mensajeId);
                await FallarAsync(registro, telefono, "almacenamiento_fallido", cancellationToken);
                return;
            }

            // PA 3602: tipo no admitido, vacío o demasiado grande, con el mismo motivo que da la web.
            await RechazarAsync(registro, telefono, nombre, subida.Error, cancellationToken);
            return;
        }

        var documento = subida.Valor!;
        registro.DocumentoId = documento.Id;
        await TerminarAsync(registro, ResultadoMensajeWhatsapp.DOCUMENTO_CREADO, null, registro.UsuarioId, cancellationToken);

        // Requisito 6: la confirmación sale después de guardar y antes de extraer: «pendiente» es cierto.
        await ResponderAsync(telefono, TextosWhatsapp.Recibido(documento.NombreArchivo), cancellationToken);

        // Mismo procesamiento que una subida web (PA 3601).
        await ExtraerSiPendienteAsync(usuario, documento.Id);
    }

    // Lanza la extracción si el documento sigue en SUBIDO (también cuando un intento anterior lo creó y murió antes).
    private async Task ExtraerSiPendienteAsync(UsuarioActual usuario, Guid documentoId)
    {
        var estado = await context.Documentos.AsNoTracking()
            .Where(d => d.Id == documentoId).Select(d => (EstadoDocumento?)d.Estado).FirstOrDefaultAsync();
        if (estado != EstadoDocumento.SUBIDO) return;

        try
        {
            await documentosService.ProcesarAsync(usuario, documentoId, false, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo procesar {DocumentoId} recibido por WhatsApp; el documento se conserva", documentoId);
            context.ChangeTracker.Clear();
        }
    }

    // Archivos EN_CURSO al arrancar: se vuelven a encolar (D1).
    public async Task<IReadOnlyList<string>> PendientesAsync(CancellationToken cancellationToken) =>
        await context.MensajesWhatsapp.AsNoTracking()
            .Where(m => m.Resultado == ResultadoMensajeWhatsapp.EN_CURSO && m.MediaId != null)
            .OrderBy(m => m.FechaRecepcion)
            .Select(m => m.MensajeId)
            .ToListAsync(cancellationToken);

    private async Task GestionarEntregaRepetidaAsync(MensajeWhatsappRecibido existente, DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        // D3/D4: los terminados se ignoran en silencio; un FALLIDO o un EN_CURSO abandonado con archivo se reencola.
        var reprocesable = existente.MediaId is not null && existente.UsuarioId is not null &&
            (existente.Resultado == ResultadoMensajeWhatsapp.FALLIDO ||
             (existente.Resultado == ResultadoMensajeWhatsapp.EN_CURSO && ahora - existente.FechaActualizacion >= EnCursoAbandonado));
        if (!reprocesable) return;

        existente.Resultado = ResultadoMensajeWhatsapp.EN_CURSO;
        existente.Motivo = null;
        existente.FechaActualizacion = ahora;
        await context.SaveChangesAsync(cancellationToken);
        cola.Encolar(existente.MensajeId);
    }

    private async Task RechazarAsync(MensajeWhatsappRecibido registro, string telefono, string nombre, Error error, CancellationToken cancellationToken)
    {
        await TerminarAsync(registro, ResultadoMensajeWhatsapp.RECHAZADO, error.Codigo, registro.UsuarioId, cancellationToken);
        await ResponderAsync(telefono, TextosWhatsapp.Rechazado(nombre, error.Mensaje), cancellationToken);
    }

    // FALLIDO conserva MediaId para poder reprocesar si el proveedor vuelve a entregar el mensaje (D4).
    private async Task FallarAsync(MensajeWhatsappRecibido registro, string telefono, string motivo, CancellationToken cancellationToken)
    {
        registro.Resultado = ResultadoMensajeWhatsapp.FALLIDO;
        registro.Motivo = motivo;
        registro.FechaActualizacion = timeProvider.GetUtcNow();
        await context.SaveChangesAsync(CancellationToken.None);
        await ResponderAsync(telefono, TextosWhatsapp.ErrorTemporal, cancellationToken);
    }

    // Estados finales: se vacían los datos del archivo (el registro no guarda nada del usuario que no haga falta).
    private async Task TerminarAsync(MensajeWhatsappRecibido registro, ResultadoMensajeWhatsapp resultado, string? motivo, Guid? usuarioId, CancellationToken cancellationToken)
    {
        registro.Resultado = resultado;
        registro.Motivo = motivo;
        registro.UsuarioId = usuarioId;
        registro.MediaId = null;
        registro.MimeType = null;
        registro.NombreArchivo = null;
        registro.FechaActualizacion = timeProvider.GetUtcNow();
        await context.SaveChangesAsync(CancellationToken.None);
    }

    // Un fallo al responder se registra pero no deshace nada: el documento, si se creó, se conserva.
    private async Task ResponderAsync(string telefono, string texto, CancellationToken cancellationToken)
    {
        try
        {
            await proveedor.EnviarTextoAsync(telefono, texto, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "No se pudo enviar la respuesta de WhatsApp");
        }
    }

    private Task<Guid?> UsuarioVerificadoAsync(string telefono, CancellationToken cancellationToken) =>
        context.NumerosWhatsapp.AsNoTracking()
            .Where(n => n.TelefonoVerificado == telefono)
            .Select(n => (Guid?)n.UsuarioId)
            .FirstOrDefaultAsync(cancellationToken);

    private Task<Guid?> DocumentoDelMensajeAsync(string mensajeId, CancellationToken cancellationToken) =>
        context.Documentos.AsNoTracking()
            .Where(d => d.ReferenciaMensaje == mensajeId)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private Task<MensajeWhatsappRecibido> RecargarAsync(string mensajeId, CancellationToken cancellationToken) =>
        context.MensajesWhatsapp.FirstAsync(m => m.MensajeId == mensajeId, cancellationToken);

    // Las fotos llegan sin nombre; un documento sin extensión recibe la de su tipo para que PB-05 pueda validarlo.
    private static string NombreArchivo(MensajeEntrante mensaje, DateTimeOffset ahora)
    {
        var extension = mensaje.MimeType is not null && ExtensionPorMime.TryGetValue(mensaje.MimeType.Split(';')[0].Trim(), out var ext) ? ext : "";
        var nombre = Path.GetFileName(mensaje.NombreArchivo ?? "");
        if (string.IsNullOrWhiteSpace(nombre)) return $"whatsapp-{ahora:yyyyMMdd-HHmmss}{extension}";
        return string.IsNullOrEmpty(Path.GetExtension(nombre)) ? nombre + extension : nombre;
    }

    private static string? Truncar(string? valor, int maximo) =>
        valor is null || valor.Length <= maximo ? valor : valor[^maximo..];
}
