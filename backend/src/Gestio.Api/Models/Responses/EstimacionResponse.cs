namespace Gestio.Api.Models.Responses;

/// <summary>
/// Estimación trimestral orientativa del IVA para un autónomo.
/// No es ni el resultado de una declaración tributaria ni una cifra definitiva.
/// </summary>
public sealed record EstimacionResponse(
    /// <summary>Año fiscal (p. ej. 2026)</summary>
    int Ejercicio,
    /// <summary>Trimestre 1..4</summary>
    int Trimestre,
    /// <summary>Si false, motivoNoDisponible explica por qué</summary>
    bool Disponible,
    /// <summary>Explicación si la estimación no está disponible (ej. perfil no soportado)</summary>
    string? MotivoNoDisponible,
    /// <summary>Suma de IVA de ingresos incluidos en €, redondeado a céntimos</summary>
    decimal IvaIngresos,
    /// <summary>Suma de IVA de gastos marcados para incluir en €, redondeado a céntimos</summary>
    decimal IvaGastos,
    /// <summary>Diferencia orientativa (IvaIngresos - IvaGastos) en €. Negativo = saldo orientativo sin afirmar devolución</summary>
    decimal Diferencia,
    /// <summary>Fecha y hora UTC del último cálculo</summary>
    DateTimeOffset FechaCalculoUtc,
    /// <summary>Documentos validados incluidos en el cálculo, agrupados por tipo</summary>
    IReadOnlyList<EstimacionDocumentoResponse> DocumentosIncluidos,
    /// <summary>Documentos excluidos con el motivo (datos incompletos, anomalías, etc.)</summary>
    IReadOnlyList<ElementoPorRevisarResponse> ElementosPorRevisar);
