namespace Gestio.Api.Domain.Errores;

// Resultado de una operación que puede fallar por una regla de negocio conocida.
public sealed class Resultado<T>
{
    private Resultado(T? valor, Error? error)
    {

        Valor = valor;
        Error = error;

    }

    public T? Valor { get; }

    public Error? Error { get; }

    public bool EsCorrecto => Error is null;

    public static Resultado<T> Correcto(T valor) => new(valor, null);

    public static Resultado<T> Fallo(Error error) => new(default, error);

    public static implicit operator Resultado<T>(T valor) => Correcto(valor);

    public static implicit operator Resultado<T>(Error error) => Fallo(error);

}