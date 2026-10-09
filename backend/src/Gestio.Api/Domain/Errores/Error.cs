namespace Gestio.Api.Domain.Errores;

public sealed record Error(string Codigo, string Mensaje, TipoError Tipo);