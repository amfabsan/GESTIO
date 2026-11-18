using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Models.Response;

public sealed record ObligacionFiscalResponse(Guid Id, Guid AutonomoId, string Modelo, string Nombre,
    int Ejercicio, string Periodo, DateOnly FechaPrevista, string Estado,
    int ObligatoriosConfirmados, int TotalObligatorios)
{
    public static ObligacionFiscalResponse Desde(ObligacionFiscal o)
    {
        var obligatorios = o.Checklist.Where(e => e.Obligatorio).ToArray();
        return new(o.Id, o.AutonomoId, o.Modelo, o.Nombre, o.Ejercicio, o.Periodo, o.FechaPrevista,
            o.Estado.ToString(), obligatorios.Count(e => e.Estado == EstadoElementoChecklist.CONFIRMADO), obligatorios.Length);
    }
}

public sealed record CambioObligacionResponse(Guid Id, Guid UsuarioId, string Gestor, DateTimeOffset Fecha, string Descripcion);
public sealed record ObligacionFiscalDetalleResponse(ObligacionFiscalResponse Obligacion, string AvisoFechas, IReadOnlyList<CambioObligacionResponse> Historial);
public sealed record ResumenPerfilFiscalResponse(string RegimenIva, string RegimenIrpf);
public sealed record CalendarioFiscalResponse(int Ejercicio, string NombreAutonomo, bool PerfilConfigurado, ResumenPerfilFiscalResponse? Perfil,
    string AvisoFechas, IReadOnlyList<ObligacionFiscalResponse> Obligaciones);
