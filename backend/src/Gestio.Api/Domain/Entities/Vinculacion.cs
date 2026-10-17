using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Domain.Entities;

public sealed class Vinculacion
{
    public Guid Id { get; set; }

    public Guid AutonomoId { get; set; }

    public required string CodigoAsesoria { get; set; }

    public EstadoVinculacion Estado { get; set; }

    public DateTimeOffset FechaSolicitud { get; set; }

    public DateTimeOffset? FechaAceptacion { get; set; }

    public Guid? AceptadaPorId { get; set; }

    public DateTimeOffset? FechaFinalizacion { get; set; }

    public Guid? FinalizadaPorId { get; set; }

    public DateTimeOffset? FechaRechazo { get; set; }

    public Guid? RechazadaPorId { get; set; }

    public Usuario Autonomo { get; set; } = null!;
}
