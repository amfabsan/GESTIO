using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Domain.Entities;

public sealed class ElementoChecklistObligacion
{
    public Guid Id { get; set; }
    public Guid ObligacionFiscalId { get; set; }
    public ObligacionFiscal ObligacionFiscal { get; set; } = null!;
    public required string CodigoPlantilla { get; set; }
    public required string Descripcion { get; set; }
    public bool Obligatorio { get; set; }
    public EstadoElementoChecklist Estado { get; set; } = EstadoElementoChecklist.PENDIENTE;
    public OrigenCambioChecklist? OrigenUltimoCambio { get; set; }
    public Guid? UsuarioUltimoCambioId { get; set; }
    public Usuario? UsuarioUltimoCambio { get; set; }
    public DateTimeOffset? FechaUltimoCambio { get; set; }
    public ICollection<HistorialElementoChecklist> Historial { get; set; } = [];
    public ICollection<VinculoEvidenciaChecklist> VinculosEvidencia { get; set; } = [];
}

public sealed class HistorialElementoChecklist
{
    public Guid Id { get; set; }
    public Guid ElementoChecklistObligacionId { get; set; }
    public ElementoChecklistObligacion Elemento { get; set; } = null!;
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public DateTimeOffset Fecha { get; set; }
    public EstadoElementoChecklist EstadoAnterior { get; set; }
    public EstadoElementoChecklist EstadoNuevo { get; set; }
    public OrigenCambioChecklist Origen { get; set; }
}
