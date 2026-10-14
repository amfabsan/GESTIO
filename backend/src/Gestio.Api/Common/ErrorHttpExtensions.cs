using Gestio.Api.Domain.Errores;
using Microsoft.AspNetCore.Mvc;

namespace Gestio.Api.Common;

// Único punto donde un error de negocio se traduce a una respuesta HTTP (RFC 7807).
public static class ErrorHttpExtensions
{
    public static ObjectResult ToProblem(this Error error)
    {

        var status = error.Tipo switch
        {

            TipoError.VALIDACION => StatusCodes.Status400BadRequest,
            TipoError.NO_ENCONTRADO => StatusCodes.Status404NotFound,
            TipoError.PROHIBIDO => StatusCodes.Status403Forbidden,
            TipoError.TAMANO_EXCEDIDO => StatusCodes.Status413PayloadTooLarge,
            TipoError.TIPO_NO_SOPORTADO => StatusCodes.Status415UnsupportedMediaType,
            TipoError.NO_DISPONIBLE => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError

        };

        var problema = new ProblemDetails
        {

            Status = status,
            Detail = error.Mensaje,
            Extensions = { ["codigo"] = error.Codigo }

        };

        return new ObjectResult(problema) { StatusCode = status };
        
    }
}