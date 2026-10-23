namespace Gestio.Api.Domain.Errores;

public static class DocumentoErrores
{
    public static readonly Error ArchivoVacio = new(
        "documento.archivo_vacio",
        "El archivo está vacío.",
        TipoError.VALIDACION);

    public static Error TamanoExcedido(long tamanoMaximoBytes) => new(
        "documento.tamano_excedido",
        $"El archivo supera el tamaño máximo permitido ({tamanoMaximoBytes / 1024 / 1024} MB).",
        TipoError.TAMANO_EXCEDIDO);

    public static Error TipoNoPermitido(IEnumerable<string> extensionesPermitidas) => new(
        "documento.tipo_no_permitido",
        $"Solo se admiten archivos {string.Join(", ", extensionesPermitidas.Select(extension => extension.TrimStart('.').ToUpperInvariant()))}.",
        TipoError.TIPO_NO_SOPORTADO);

    public static readonly Error AlmacenamientoFallido = new(
        "documento.almacenamiento_fallido",
        "No se ha podido guardar el archivo. El documento no se ha creado; inténtalo de nuevo.",
        TipoError.NO_DISPONIBLE);

    public static readonly Error SoloAutonomoPuedeSubir = new(
        "documento.solo_autonomo",
        "Solo un autónomo puede subir documentos a su espacio.",
        TipoError.PROHIBIDO);

    public static readonly Error ValidacionCamposObligatorios = new(
        "documento.validacion.campos_obligatorios",
        "Faltan campos obligatorios o tienen un formato incorrecto.",
        TipoError.VALIDACION);

    public static readonly Error ValidacionFormatoInvalido = new(
        "documento.validacion.formato_invalido",
        "Hay valores del documento con un formato no válido.",
        TipoError.VALIDACION);

    public static readonly Error DocumentoYaValidado = new(
        "documento.validacion.documento_ya_validado",
        "Este documento ya está validado y no puede aprobarse de nuevo.",
        TipoError.CONFLICTO);

    public static readonly Error EstadoNoPermitidoParaValidacion = new(
        "documento.validacion.estado_no_permitido",
        "El documento no está en un estado válido para ser validado.",
        TipoError.CONFLICTO);

    public static readonly Error SinVinculacionActiva = new(
        "documento.validacion.sin_vinculacion_activa",
        "No tienes acceso para validar este documento porque la vinculación no está activa.",
        TipoError.PROHIBIDO);

    public static readonly Error SinPermisoValidacion = new(
        "documento.validacion.sin_permiso",
        "Solo el gestor de la asesoría puede validar este documento.",
        TipoError.PROHIBIDO);

    public static readonly Error CampoConValorInvalido = new(
        "documento.validacion.campo_incorrecto",
        "El valor corregido no cumple la validación del campo.",
        TipoError.VALIDACION);

    public static readonly Error ValidacionSinCambios = new(
        "documento.validacion.sin_cambios",
        "No se registraron cambios en la validación.",
        TipoError.VALIDACION);

    public static readonly Error ErrorGuardandoValidacion = new(
        "documento.validacion.error_guardado",
        "No se pudo guardar la corrección o la aprobación del documento.",
        TipoError.NO_DISPONIBLE);
}