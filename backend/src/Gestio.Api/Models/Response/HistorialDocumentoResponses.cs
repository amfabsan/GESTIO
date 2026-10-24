using Gestio.Api.Services;

namespace Gestio.Api.Models.Responses;

public sealed record EventoHistorialResponse(
    Guid Id,
    string Tipo,
    DateTimeOffset Fecha,
    string? Detalle,
    ActorEventoResponse? Actor,
    CambioCampoResponse? Cambio)
{
    public static EventoHistorialResponse Desde(EventoHistorial evento) => new(
        evento.Id,
        evento.Tipo.ToString(),
        evento.Fecha,
        evento.Detalle,
        evento.Actor is null ? null : ActorEventoResponse.Desde(evento.Actor),
        evento.Cambio is null ? null : CambioCampoResponse.Desde(evento.Cambio));
}

public sealed record ActorEventoResponse(Guid Id, string Nombre, string PrimerApellido, string SegundoApellido)
{
    public static ActorEventoResponse Desde(ActorEvento actor) =>
        new(actor.Id, actor.Nombre, actor.PrimerApellido, actor.SegundoApellido);
}

public sealed record CambioCampoResponse(string Campo, string? ValorAnterior, string? ValorNuevo)
{
    public static CambioCampoResponse Desde(CambioCampo cambio) =>
        new(cambio.Campo, cambio.ValorAnterior, cambio.ValorNuevo);
}
