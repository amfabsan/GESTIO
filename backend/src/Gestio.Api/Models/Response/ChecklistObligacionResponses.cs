using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Services;

namespace Gestio.Api.Models.Response;

public sealed record CambioChecklistResponse(Guid Id, Guid UsuarioId, string Usuario, DateTimeOffset Fecha,
    string EstadoAnterior, string EstadoNuevo, string Origen);

public sealed record CambioVinculoEvidenciaResponse(Guid Id, string? EstadoAnterior, string EstadoNuevo,
    Guid? UsuarioId, string Usuario, DateTimeOffset Fecha);

public sealed record VinculoEvidenciaChecklistResponse(Guid Id, Guid DocumentoId, string NombreArchivo,
    string CategoriaDocumento, string Estado, DateTimeOffset FechaPropuesta, Guid? ConfirmadaPorId,
    string? ConfirmadaPor, DateTimeOffset? FechaConfirmacion, DateOnly? FechaEmision,
    IReadOnlyList<CambioVinculoEvidenciaResponse> Historial);

public sealed record ElementoChecklistResponse(Guid Id, string CodigoPlantilla, string Descripcion, bool Obligatorio,
    string Estado, string? Origen, Guid? UsuarioUltimoCambioId, string? UsuarioUltimoCambio,
    DateTimeOffset? FechaUltimoCambio, IReadOnlyList<CambioChecklistResponse> Historial,
    IReadOnlyList<VinculoEvidenciaChecklistResponse> Evidencias);

public sealed record ChecklistObligacionResponse(ObligacionFiscalResponse Obligacion, string AvisoFechas,
    string AvisoPlantilla, int ObligatoriosConfirmados, int TotalObligatorios,
    IReadOnlyList<ElementoChecklistResponse> Elementos)
{
    public static ChecklistObligacionResponse Desde(ObligacionFiscal obligacion)
    {
        var obligatorios = obligacion.Checklist.Where(e => e.Obligatorio).ToArray();
        var orden = CatalogoChecklistDemo.Para(obligacion)
            .Select((entrada, indice) => (entrada.Codigo, indice))
            .ToDictionary(x => x.Codigo, x => x.indice, StringComparer.Ordinal);
        var elementos = obligacion.Checklist.OrderBy(e => orden.GetValueOrDefault(e.CodigoPlantilla, int.MaxValue))
            .Select(e => new ElementoChecklistResponse(e.Id, e.CodigoPlantilla, e.Descripcion, e.Obligatorio,
                e.Estado.ToString(), e.OrigenUltimoCambio?.ToString(), e.UsuarioUltimoCambioId,
                Nombre(e.UsuarioUltimoCambio), e.FechaUltimoCambio,
                e.Historial.OrderBy(h => h.Fecha).ThenBy(h => h.Id).Select(h => new CambioChecklistResponse(
                    h.Id, h.UsuarioId, Nombre(h.Usuario) ?? "Usuario desconocido", h.Fecha, h.EstadoAnterior.ToString(), h.EstadoNuevo.ToString(), h.Origen.ToString())).ToArray(),
                e.VinculosEvidencia.Where(v => v.Documento.UsuarioId == obligacion.AutonomoId)
                    .OrderBy(v => v.FechaPropuesta).ThenBy(v => v.Id)
                    .Select(v => new VinculoEvidenciaChecklistResponse(v.Id, v.DocumentoId, v.Documento.NombreArchivo,
                        v.Documento.Categoria.ToString(), v.Estado.ToString(), v.FechaPropuesta, v.ConfirmadaPorId,
                        Nombre(v.ConfirmadaPor), v.FechaConfirmacion, v.Documento.ExtraccionIa?.FechaEmision,
                        v.Historial.OrderBy(h => h.Fecha).ThenBy(h => h.Id)
                            .Select(h => new CambioVinculoEvidenciaResponse(h.Id, h.EstadoAnterior?.ToString(),
                                h.EstadoNuevo.ToString(), h.UsuarioId, Nombre(h.Usuario) ?? "Sistema", h.Fecha)).ToArray())).ToArray()))
            .ToArray();

        return new(ObligacionFiscalResponse.Desde(obligacion), CatalogoObligacionesDemo.Aviso,
            CatalogoChecklistDemo.Aviso,
            obligatorios.Count(e => e.Estado == EstadoElementoChecklist.CONFIRMADO), obligatorios.Length, elementos);
    }

    private static string? Nombre(Usuario? usuario) => usuario is null
        ? null
        : $"{usuario.Nombre} {usuario.PrimerApellido} {usuario.SegundoApellido}".Trim();
}
