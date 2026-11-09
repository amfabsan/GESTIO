namespace Gestio.Api.Domain.Errores;

public static class ContactoErrores
{
    public static readonly Error Prohibido = new("Contacto.Prohibido", "No tienes permiso para acceder a esta agenda.", TipoError.PROHIBIDO);
    public static readonly Error NoEncontrado = new("Contacto.NoEncontrado", "No se ha encontrado el contacto.", TipoError.NO_ENCONTRADO);
    public static readonly Error Duplicado = new("Contacto.NifDuplicado", "Ya existe un contacto con este NIF en la agenda, aunque esté inactivo.", TipoError.CONFLICTO);
    public static readonly Error Referenciado = new("Contacto.Referenciado", "Este contacto no se puede eliminar porque está utilizado. Puedes desactivarlo para que no aparezca en nuevas operaciones.", TipoError.CONFLICTO);
}
