namespace Gestio.Api.Models.Responses;

public sealed record EventoHistorialResponse(
    Guid Id,
    string Tipo,
    DateTimeOffset Fecha,
    string? Detalle,
    ActorEventoResponse? Actor,
    CambioCampoResponse? Cambio);

public sealed record ActorEventoResponse(Guid Id, string Nombre, string PrimerApellido, string SegundoApellido);

public sealed record CambioCampoResponse(string Campo, string? ValorAnterior, string? ValorNuevo);
