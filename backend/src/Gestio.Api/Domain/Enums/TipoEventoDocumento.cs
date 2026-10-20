namespace Gestio.Api.Domain.Enums;

public enum TipoEventoDocumento
{
    SUBIDA = 1,
    PROCESAMIENTO_INICIADO = 2,
    PROCESAMIENTO_CORRECTO = 3,
    PROCESAMIENTO_FALLIDO = 4,
    // Nombre conservado en historiales de versiones anteriores.
    PROCESAMIENTO_COMPLETADO = 5,
    PROCESAMIENTO_ERROR = 6
}
