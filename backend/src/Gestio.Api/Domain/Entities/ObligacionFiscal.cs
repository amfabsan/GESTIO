using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Domain.Entities;

public sealed class ObligacionFiscal
{
    public Guid Id { get; set; }
    public Guid AutonomoId { get; set; }
    public required string Modelo { get; set; }
    public required string Nombre { get; set; }
    public int Ejercicio { get; set; }
    public required string Periodo { get; set; }
    public DateOnly FechaPrevista { get; set; }
    public EstadoObligacion Estado { get; set; } = EstadoObligacion.INCOMPLETA;
    public ICollection<HistorialObligacionFiscal> Historial { get; set; } = [];
    public ICollection<ElementoChecklistObligacion> Checklist { get; set; } = [];
}

public sealed class HistorialObligacionFiscal
{
    public Guid Id { get; set; }
    public Guid ObligacionFiscalId { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public DateTimeOffset Fecha { get; set; }
    public required string Descripcion { get; set; }
}
