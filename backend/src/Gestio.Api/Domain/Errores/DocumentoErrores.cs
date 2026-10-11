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
}