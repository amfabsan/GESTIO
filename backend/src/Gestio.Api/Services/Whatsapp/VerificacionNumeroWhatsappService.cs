using Gestio.Api.Common;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gestio.Api.Services.Whatsapp;

public sealed record EstadoNumeroWhatsapp(
    bool CanalHabilitado,
    string NumeroCanal,
    string? TelefonoVerificado,
    DateTimeOffset? FechaVerificacion,
    string? TelefonoPendiente,
    string? CodigoVerificacion,
    DateTimeOffset? CodigoCaducaEn);

public enum ResultadoVerificacionWhatsapp
{
    // El texto no tiene forma de código: no es un intento de verificación.
    NO_ES_CODIGO,
    VERIFICADO,
    // Código inexistente, caducado, enviado desde otro número o número ya verificado en otra cuenta. Todos dan la
    // misma respuesta al remitente para no revelar nada.
    RECHAZADO
}

// UT27 (PA 3605, requisito 2): vincular y verificar el número de WhatsApp de un autónomo.
public sealed class VerificacionNumeroWhatsappService(
    ApplicationDbContext context,
    TimeProvider timeProvider,
    IOptions<WhatsappOptions> options,
    ILogger<VerificacionNumeroWhatsappService> logger)
{
    private const int IntentosGeneracionCodigo = 3;

    public async Task<Resultado<EstadoNumeroWhatsapp>> ObtenerAsync(UsuarioActual usuario, CancellationToken cancellationToken)
    {
        if (usuario.Rol != Rol.AUTONOMO) return WhatsappErrores.SoloAutonomo;

        var numero = await context.NumerosWhatsapp.AsNoTracking()
            .FirstOrDefaultAsync(n => n.UsuarioId == usuario.Id, cancellationToken);
        return Estado(numero);
    }

    // D2: el autónomo escribe su número; el código solo valdrá si llega desde ese número. El número verificado
    // anterior, si lo hay, sigue activo hasta que se complete la nueva verificación.
    public async Task<Resultado<EstadoNumeroWhatsapp>> IniciarAsync(UsuarioActual usuario, string? telefono, CancellationToken cancellationToken)
    {
        if (usuario.Rol != Rol.AUTONOMO) return WhatsappErrores.SoloAutonomo;
        // Sin canal no hay a dónde enviar el código: no se genera uno que caducaría sin poder usarse.
        if (!options.Value.Habilitado) return WhatsappErrores.CanalNoDisponible;

        var normalizado = TelefonoWhatsapp.Normalizar(telefono);
        if (normalizado is null) return WhatsappErrores.TelefonoNoValido;

        var numero = await context.NumerosWhatsapp.FirstOrDefaultAsync(n => n.UsuarioId == usuario.Id, cancellationToken);
        if (numero?.TelefonoVerificado == normalizado) return WhatsappErrores.YaVinculado;

        if (numero is null)
        {
            numero = new NumeroWhatsapp { Id = Guid.NewGuid(), UsuarioId = usuario.Id };
            context.NumerosWhatsapp.Add(numero);
        }

        // Si el número ya está verificado en otra cuenta no se dice aquí: la verificación fallará con el mensaje
        // genérico y la web no sirve para averiguar qué números tienen cuenta.
        numero.TelefonoPendiente = normalizado;
        numero.CodigoCaducaEn = timeProvider.GetUtcNow() + CodigoVerificacionWhatsapp.Vigencia;

        for (var intento = 1; ; intento++)
        {
            numero.CodigoVerificacion = CodigoVerificacionWhatsapp.Generar();
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return Estado(numero);
            }
            catch (DbUpdateException ex) when (intento < IntentosGeneracionCodigo &&
                                               Restriccion(ex) == NumeroWhatsappConfiguration.IndiceCodigo)
            {
                // Colisión improbable con otro código guardado: se genera otro.
                logger.LogWarning(ex, "Colisión al guardar el código de verificación de WhatsApp; se reintenta");
            }
            catch (DbUpdateException ex)
            {
                // Agotados los reintentos, o choque con otra restricción (p. ej. dos peticiones simultáneas del mismo
                // autónomo creando su fila): se saca esta entidad del contexto y se pide repetir.
                logger.LogError(ex, "No se pudo guardar el código de verificación de WhatsApp de {UsuarioId}", usuario.Id);
                context.Entry(numero).State = EntityState.Detached;
                return WhatsappErrores.NoSePudoGenerarCodigo;
            }
        }
    }

    // D6: corta el canal. Borra el número verificado y cualquier verificación en curso.
    public async Task<Resultado<EstadoNumeroWhatsapp>> DesvincularAsync(UsuarioActual usuario, CancellationToken cancellationToken)
    {
        if (usuario.Rol != Rol.AUTONOMO) return WhatsappErrores.SoloAutonomo;

        var numero = await context.NumerosWhatsapp.FirstOrDefaultAsync(n => n.UsuarioId == usuario.Id, cancellationToken);
        if (numero is null || (numero.TelefonoVerificado is null && numero.TelefonoPendiente is null))
            return WhatsappErrores.SinNumeroVinculado;

        context.NumerosWhatsapp.Remove(numero);
        await context.SaveChangesAsync(cancellationToken);
        return Estado(null);
    }

    // Llamado al recibir un texto por WhatsApp. El remitente lo autentica el proveedor; el código prueba que quien
    // lo envía tiene la sesión del autónomo, y que llegue desde TelefonoPendiente prueba que es el número que escribió.
    public async Task<ResultadoVerificacionWhatsapp> VerificarDesdeMensajeAsync(string telefonoRemitente, string? texto, CancellationToken cancellationToken)
    {
        var codigo = CodigoVerificacionWhatsapp.Leer(texto);
        if (codigo is null) return ResultadoVerificacionWhatsapp.NO_ES_CODIGO;

        var ahora = timeProvider.GetUtcNow();
        var numero = await context.NumerosWhatsapp.FirstOrDefaultAsync(n =>
            n.CodigoVerificacion == codigo &&
            n.TelefonoPendiente == telefonoRemitente &&
            n.CodigoCaducaEn > ahora, cancellationToken);
        if (numero is null) return ResultadoVerificacionWhatsapp.RECHAZADO;

        var enOtraCuenta = await context.NumerosWhatsapp.AnyAsync(n =>
            n.UsuarioId != numero.UsuarioId && n.TelefonoVerificado == telefonoRemitente, cancellationToken);
        if (enOtraCuenta) return ResultadoVerificacionWhatsapp.RECHAZADO;

        numero.TelefonoVerificado = telefonoRemitente;
        numero.FechaVerificacion = ahora;
        numero.TelefonoPendiente = null;
        numero.CodigoVerificacion = null;
        numero.CodigoCaducaEn = null;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Otra cuenta ha verificado el mismo número a la vez: el índice único decide.
            logger.LogWarning(ex, "Verificación de WhatsApp rechazada por el índice único para {UsuarioId}", numero.UsuarioId);
            // Solo se descarta esta entidad: el contexto lo comparte el llamador (la recepción del mensaje, capa 3)
            // y vaciarlo entero le quitaría sus propios cambios pendientes.
            context.Entry(numero).State = EntityState.Detached;
            return ResultadoVerificacionWhatsapp.RECHAZADO;
        }

        logger.LogInformation("Número de WhatsApp verificado para {UsuarioId}", numero.UsuarioId);
        return ResultadoVerificacionWhatsapp.VERIFICADO;
    }

    private static string? Restriccion(DbUpdateException ex) => (ex.InnerException as Npgsql.PostgresException)?.ConstraintName;

    private EstadoNumeroWhatsapp Estado(NumeroWhatsapp? numero)
    {
        var vigente = numero?.CodigoCaducaEn > timeProvider.GetUtcNow();
        return new EstadoNumeroWhatsapp(
            options.Value.Habilitado,
            options.Value.NumeroCanal,
            numero?.TelefonoVerificado,
            numero?.FechaVerificacion,
            vigente ? numero!.TelefonoPendiente : null,
            vigente ? numero!.CodigoVerificacion : null,
            vigente ? numero!.CodigoCaducaEn : null);
    }
}
