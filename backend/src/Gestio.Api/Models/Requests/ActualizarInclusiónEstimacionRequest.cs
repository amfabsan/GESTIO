namespace Gestio.Api.Models.Requests;

/// <summary>
/// Solicitud para actualizar la inclusión de un gasto en la estimación de IVA.
/// </summary>
public sealed record ActualizarInclusiónEstimacionRequest(
    /// <summary>Si true, el IVA se resta en la estimación; si false, se excluye</summary>
    bool Incluida);
