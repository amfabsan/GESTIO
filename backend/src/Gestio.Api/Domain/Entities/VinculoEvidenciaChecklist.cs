using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Domain.Entities;

public sealed class VinculoEvidenciaChecklist
{
    public Guid Id { get; set; }
    public Guid ElementoChecklistObligacionId { get; set; }
    public ElementoChecklistObligacion ElementoChecklistObligacion { get; set; } = null!;
    public Guid DocumentoId { get; set; }
    public Documento Documento { get; set; } = null!;
    public EstadoVinculoEvidencia Estado { get; set; } = EstadoVinculoEvidencia.PROPUESTA;
    public DateTimeOffset FechaPropuesta { get; set; }
    public Guid? ConfirmadaPorId { get; set; }
    public Usuario? ConfirmadaPor { get; set; }
    public DateTimeOffset? FechaConfirmacion { get; set; }
    public ICollection<HistorialVinculoEvidencia> Historial { get; set; } = [];
}

public sealed class HistorialVinculoEvidencia
{
    public Guid Id { get; set; }
    public Guid VinculoEvidenciaChecklistId { get; set; }
    public VinculoEvidenciaChecklist Vinculo { get; set; } = null!;
    public EstadoVinculoEvidencia? EstadoAnterior { get; set; }
    public EstadoVinculoEvidencia EstadoNuevo { get; set; }
    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public DateTimeOffset Fecha { get; set; }
}
