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
    string Categoria,
    string? CategoriaPropuesta,
    string? NivelConfianzaPropuesta,
    ExtraccionResponse? Extraccion,
    IReadOnlyList<AnomaliaResponse> Anomalias,
    IReadOnlyList<EventoDocumentoResponse> Historial,
    IReadOnlyList<IntentoResponse> Intentos,
    IReadOnlyList<HistorialValidacionResponse>? Validacion, string Tipo);

public sealed record IntentoResponse(Guid Id, DateTimeOffset Inicio, DateTimeOffset? Fin,
    string Procesador, string? Version, string Resultado, string? Error, string? ValoresJson);

public sealed record ExtraccionResponse(
    string? NumeroFactura,
    string? EmisorNombre,
    string? EmisorNif,
    DateOnly? FechaEmision,
    decimal? BaseImponible,
    decimal? TipoIva,
    decimal? CuotaIva,
    decimal? Total,
    string? CategoriaSugerida,
    string? NivelConfianza,
    string? DatosCrudosJson,
    IReadOnlyList<Gestio.Api.Domain.Entities.DesgloseIva> DesglosesIva,
    IReadOnlyList<string> Advertencias);

public sealed record AnomaliaResponse(
    Guid Id,
    string Tipo,
    string? Campo,
    string Descripcion,
    string Severidad,
    bool Resuelta,
    DateTimeOffset FechaDeteccion,Guid? DocumentoRelacionadoId);

public sealed record EventoDocumentoResponse(string Tipo, DateTimeOffset Fecha, string? Detalle, string? Actor);

public sealed record HistorialValidacionResponse(
    Guid Id,
    string Campo,
    string? ValorAnterior,
    string? ValorNuevo,
    string TipoCambio,
    Guid UsuarioId,
    DateTimeOffset FechaCambio);

public sealed record HistorialCategoriaResponse(
    Guid Id,
    Guid DocumentoId,
    Guid ActorId,
    string ActorNombre,
    string CategoriaAnterior,
    string CategoriaNueva,
    DateTimeOffset Fecha);

public sealed record ConfiguracionSubidaResponse(
    long TamanoMaximoBytes,
    IReadOnlyList<TipoArchivoPermitidoResponse> TiposPermitidos);

public sealed record TipoArchivoPermitidoResponse(string Extension, string TipoMime, string FirmaHex);