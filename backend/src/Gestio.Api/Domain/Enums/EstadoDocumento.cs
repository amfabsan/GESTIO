namespace Gestio.Api.Domain.Enums;

public enum EstadoDocumento
{
    SUBIDO = 1,
    EXTRAYENDO_DATOS = 2,
    PENDIENTE_VALIDACION = 3,
    VALIDADO = 4,
    ERROR = 5,
    ACLARACION_SOLICITADA = 6
}
