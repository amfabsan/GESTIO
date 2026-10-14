namespace Gestio.Api.Domain.Errores;

public static class VinculacionErrores
{
    public static readonly Error SoloAutonomoPuedeSolicitar = new(
        "vinculacion.solo_autonomo",
        "Solo un autónomo puede solicitar una vinculación.",
        TipoError.PROHIBIDO);

    public static readonly Error CodigoAsesoriaNoEncontrado = new(
        "vinculacion.codigo_no_encontrado",
        "El código de asesoría no existe.",
        TipoError.NO_ENCONTRADO);

    public static readonly Error SolicitudDuplicada = new(
        "vinculacion.solicitud_duplicada",
        "Ya existe una solicitud pendiente para este autónomo y asesoría.",
        TipoError.CONFLICTO);

    public static readonly Error SoloAdministradorPuedeGestionar = new(
        "vinculacion.solo_administrador",
        "Solo un administrador de la asesoría puede gestionar la vinculación.",
        TipoError.PROHIBIDO);

    public static readonly Error VinculacionNoEncontrada = new(
        "vinculacion.no_encontrada",
        "La vinculación no existe.",
        TipoError.NO_ENCONTRADO);

    public static readonly Error EstadoNoValido = new(
        "vinculacion.estado_no_valido",
        "La vinculación no se encuentra en un estado válido para esta operación.",
        TipoError.VALIDACION);

    public static readonly Error AutonomoYaTieneAsesoria = new(
        "vinculacion.autonomo_ya_vinculado",
        "El autónomo ya tiene otra asesoría activa.",
        TipoError.CONFLICTO);
}
