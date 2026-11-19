namespace Gestio.Api.Models.Responses;

public sealed record PanelGestorResponse(
    string NombreGestor,
    string? NombreAsesoria,
    string? CodigoAsesoria,
    int ClientesActivosCount,
    int SolicitudesPendientesCount,
    int DiasAntelacionObligaciones,
    DateOnly FechaReferencia,
    PanelResumenGlobalesResponse Resumen,
    IReadOnlyList<PanelClienteFilaResponse> Clientes);

public sealed record PanelResumenGlobalesResponse(
    int RequierenAtencion,
    int ObligacionesProximas,
    int ChecklistsIncompletos,
    int DocumentosPorValidar,
    int AnomaliasPendientes);

public sealed record PanelClienteFilaResponse(
    Guid AutonomoId,
    string NombreCompleto,
    string Iniciales,
    string? Actividad,
    string Nif,
    bool RequiereAtencion,
    string? MotivoPrincipal,
    int ObligacionesProximasCount,
    int ChecklistsIncompletosCount,
    int DocumentosPorValidarCount,
    int AnomaliasPendientesCount,
    DateOnly? ProximaObligacionFecha,
    int? DiasHastaProximaObligacion,
    string? ProximaObligacionEtiqueta,
    IReadOnlyList<PanelAvisoOrigenResponse> Avisos);

public sealed record PanelAvisoOrigenResponse(
    string Tipo, // "CHECKLIST" | "DOCUMENTO" | "ANOMALIA"
    string Titulo,
    string Detalle,
    Guid? ObligacionFiscalId,
    Guid? DocumentoId,
    Guid? AnomaliaId,
    int? Ejercicio,
    string? Periodo,
    string? Modelo);

public sealed record PanelClienteDetalleResponse(
    Guid AutonomoId,
    string NombreCompleto,
    string? Actividad,
    string Nif,
    bool RequiereAtencion,
    IReadOnlyList<PanelObligacionProximaDetalleResponse> ObligacionesProximas,
    IReadOnlyList<PanelDocumentoPendienteDetalleResponse> DocumentosPorValidar,
    IReadOnlyList<PanelAnomaliaPendienteDetalleResponse> AnomaliasPendientes);

public sealed record PanelObligacionProximaDetalleResponse(
    Guid ObligacionFiscalId,
    string Modelo,
    string Nombre,
    int Ejercicio,
    string Periodo,
    DateOnly FechaPrevista,
    int DiasRestantes,
    string EstadoObligacion,
    int ObligatoriosConfirmados,
    int ObligatoriosTotales,
    bool ChecklistIncompleto);

public sealed record PanelDocumentoPendienteDetalleResponse(
    Guid DocumentoId,
    string NombreArchivo,
    DateTimeOffset FechaSubida,
    string Estado);

public sealed record PanelAnomaliaPendienteDetalleResponse(
    Guid AnomaliaId,
    Guid DocumentoId,
    string NombreArchivoDocumento,
    string Tipo,
    string Severidad,
    string? Campo,
    string Descripcion,
    DateTimeOffset FechaDeteccion);