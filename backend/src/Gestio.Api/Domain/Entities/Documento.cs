using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Domain.Entities;

public sealed class Documento
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }

    public required string NombreArchivo { get; set; }

    public required string TipoMime { get; set; }

    public long TamanoBytes { get; set; }

    public OrigenDocumento Origen { get; set; }

    public string? StorageKey { get; set; }

    public string? HashSha256 { get; set; }

    public DateTimeOffset FechaSubida { get; set; }

    public EstadoDocumento Estado { get; set; }

    public Usuario Usuario { get; set; } = null!;

    public ExtraccionIa? ExtraccionIa { get; set; }

    public ICollection<Anomalia> Anomalias { get; } = [];

    public ICollection<EventoDocumento> Eventos { get; } = [];
    
}