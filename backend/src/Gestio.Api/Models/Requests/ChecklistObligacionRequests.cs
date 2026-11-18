namespace Gestio.Api.Models.Requests;

public sealed record CambiarMarcaChecklistRequest(bool Aportado);

public sealed record CambiarRevisionChecklistRequest(bool Confirmar);
