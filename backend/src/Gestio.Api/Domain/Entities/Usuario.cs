using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Domain.Entities;

public sealed class Usuario
{
    public Guid Id { get; set; }

    public required string Nombre { get; set; }

    public required string Email { get; set; }

    public required string Nif { get; set; }

    public string? CodigoAsesoria { get; set; }

    public Rol Rol { get; set; }

    public ICollection<Documento> Documentos { get; } = [];
}
