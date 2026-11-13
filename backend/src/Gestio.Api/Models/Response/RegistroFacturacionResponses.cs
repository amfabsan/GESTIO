namespace Gestio.Api.Models.Response;

public sealed record RegistroFacturacionResponse(
    Guid Id,
    int Secuencia,
    string Tipo,
    Guid FacturaId,
    string? ClienteNombre,
    string IdEmisorFactura,
    string NumSerieFactura,
    DateOnly FechaExpedicionFactura,
    string TipoFactura,
    decimal CuotaTotal,
    decimal ImporteTotal,
    string FechaHoraHusoGenRegistro,
    DateTimeOffset Generado,
    Guid? AnteriorId,
    int? SecuenciaAnterior,
    string? HuellaAnterior,
    string Huella,
    string RepresentacionCanonica);

public sealed record VerificacionCadenaResponse(
    bool Coherente,
    int Revisados,
    int TotalRegistros,
    Guid? RegistroIncoherenteId,
    int? SecuenciaIncoherente,
    string? Codigo,
    string? Motivo);
