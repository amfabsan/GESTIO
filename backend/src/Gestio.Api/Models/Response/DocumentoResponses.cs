namespace Gestio.Api.Models.Responses;

public sealed record DocumentoResponse(
    Guid Id,
    string NombreArchivo,
    string Usuario,
    DateTimeOffset FechaSubida,
    string Estado,
    string TipoMime,
    long TamanoBytes,
    string Origen,
    bool TieneOriginal,
    ExtraccionResponse? Extraccion,
    IReadOnlyList<AnomaliaResponse> Anomalias,
    IReadOnlyList<EventoDocumentoResponse> Historial);

public sealed record ExtraccionResponse(
    string? EmisorNombre,
    string? EmisorNif,
    DateOnly? FechaEmision,
    decimal? BaseImponible,
    decimal? TipoIva,
    decimal? CuotaIva,
    decimal? Total,
    string? CategoriaSugerida,
    string? NivelConfianza,
    string? DatosCrudosJson);

public sealed record AnomaliaResponse(
    string Tipo,
    string Descripcion,
    string Severidad,
    bool Resuelta,
    DateTimeOffset FechaDeteccion);

public sealed record EventoDocumentoResponse(string Tipo, DateTimeOffset Fecha, string? Detalle);

public sealed record ConfiguracionSubidaResponse(
    long TamanoMaximoBytes,
    IReadOnlyList<TipoArchivoPermitidoResponse> TiposPermitidos);

public sealed record TipoArchivoPermitidoResponse(string Extension, string TipoMime, string FirmaHex);