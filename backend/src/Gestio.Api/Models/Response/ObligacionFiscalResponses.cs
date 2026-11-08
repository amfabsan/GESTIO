using Gestio.Api.Domain.Entities;

namespace Gestio.Api.Models.Response;

public sealed record ObligacionFiscalResponse(Guid Id, Guid AutonomoId, string Modelo, string Nombre,
    int Ejercicio, string Periodo, DateOnly FechaPrevista, string Estado)
{
    public static ObligacionFiscalResponse Desde(ObligacionFiscal o) => new(o.Id, o.AutonomoId, o.Modelo,
        o.Nombre, o.Ejercicio, o.Periodo, o.FechaPrevista, o.Estado.ToString());
}

public sealed record CambioObligacionResponse(Guid Id, Guid UsuarioId, string Gestor, DateTimeOffset Fecha, string Descripcion);
public sealed record ObligacionFiscalDetalleResponse(ObligacionFiscalResponse Obligacion, string AvisoFechas, IReadOnlyList<CambioObligacionResponse> Historial);
public sealed record ResumenPerfilFiscalResponse(string RegimenIva, string RegimenIrpf);
public sealed record CalendarioFiscalResponse(int Ejercicio, string NombreAutonomo, bool PerfilConfigurado, ResumenPerfilFiscalResponse? Perfil,
    string AvisoFechas, IReadOnlyList<ObligacionFiscalResponse> Obligaciones);
