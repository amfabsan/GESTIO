namespace Gestio.Api.Domain.Entities;

public sealed class PerfilFiscal
{
    public Guid Id { get; set; }
    public Guid AutonomoId { get; set; }
    public int Ejercicio { get; set; }
    public required string RegimenIva { get; set; }
    public required string RegimenIrpf { get; set; }
    public bool TrabajadoresConRetencion { get; set; }
    public bool LocalAlquiladoConRetencion { get; set; }
    public ICollection<HistorialPerfilFiscal> Historial { get; set; } = [];
}

// Sigue el registro por campo utilizado por los historiales de documentos.
public sealed class HistorialPerfilFiscal
{
    public Guid Id { get; set; }
    public Guid PerfilFiscalId { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public DateTimeOffset FechaCambio { get; set; }
    public required string Campo { get; set; }
    public string? ValorAnterior { get; set; }
    public required string ValorNuevo { get; set; }
}
