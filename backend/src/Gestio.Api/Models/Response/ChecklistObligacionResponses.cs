using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Services;

namespace Gestio.Api.Models.Response;

public sealed record CambioChecklistResponse(Guid Id, Guid UsuarioId, string Usuario, DateTimeOffset Fecha,
    string EstadoAnterior, string EstadoNuevo, string Origen);

public sealed record ElementoChecklistResponse(Guid Id, string CodigoPlantilla, string Descripcion, bool Obligatorio,
    string Estado, string? Origen, Guid? UsuarioUltimoCambioId, string? UsuarioUltimoCambio,
    DateTimeOffset? FechaUltimoCambio, IReadOnlyList<CambioChecklistResponse> Historial);

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
                    h.Id, h.UsuarioId, Nombre(h.Usuario), h.Fecha, h.EstadoAnterior.ToString(), h.EstadoNuevo.ToString(), h.Origen.ToString())).ToArray()))
            .ToArray();

        return new(ObligacionFiscalResponse.Desde(obligacion), CatalogoObligacionesDemo.Aviso,
            CatalogoChecklistDemo.Aviso,
            obligatorios.Count(e => e.Estado == EstadoElementoChecklist.CONFIRMADO), obligatorios.Length, elementos);
    }

    private static string? Nombre(Usuario? usuario) => usuario is null
        ? null
        : $"{usuario.Nombre} {usuario.PrimerApellido} {usuario.SegundoApellido}".Trim();
}
