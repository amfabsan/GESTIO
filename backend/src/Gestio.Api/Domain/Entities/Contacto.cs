namespace Gestio.Api.Domain.Entities;

public sealed class Contacto
{
    public Guid Id { get; set; }

    public Guid AutonomoId { get; set; }

    public required string Clasificacion { get; set; }

    public required string Nombre { get; set; }

    public required string Nif { get; set; }

    public string? Email { get; set; }

    public string? Telefono { get; set; }

    public string? Direccion { get; set; }

    public bool Activo { get; set; } = true;

    public Usuario Autonomo { get; set; } = null!;
}
