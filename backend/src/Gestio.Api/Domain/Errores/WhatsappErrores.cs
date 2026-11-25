namespace Gestio.Api.Domain.Errores;

public static class WhatsappErrores
{
    public static readonly Error SoloAutonomo = new(
        "whatsapp.solo_autonomo",
        "Solo un autónomo puede vincular un número de WhatsApp.",
        TipoError.PROHIBIDO);

    public static readonly Error TelefonoNoValido = new(
        "whatsapp.telefono_no_valido",
        "El número no es válido. Escríbelo con el prefijo del país, por ejemplo +34 600 123 456.",
        TipoError.VALIDACION);

    public static readonly Error YaVinculado = new(
        "whatsapp.ya_vinculado",
        "Ese número ya está vinculado a tu cuenta.",
        TipoError.CONFLICTO);

    public static readonly Error SinNumeroVinculado = new(
        "whatsapp.sin_numero",
        "No tienes ningún número de WhatsApp vinculado.",
        TipoError.NO_ENCONTRADO);

    public static readonly Error CanalNoDisponible = new(
        "whatsapp.canal_no_disponible",
        "El canal de WhatsApp de GESTIO no está disponible en este momento.",
        TipoError.NO_DISPONIBLE);

    public static readonly Error NoSePudoGenerarCodigo = new(
        "whatsapp.codigo_no_generado",
        "No se ha podido generar el código de verificación. Inténtalo de nuevo.",
        TipoError.NO_DISPONIBLE);
}
