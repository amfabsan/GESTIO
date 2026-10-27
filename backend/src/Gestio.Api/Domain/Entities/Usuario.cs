using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Domain.Entities;

public sealed class Usuario
{
    public Guid Id { get; set; }

    public required string Nombre { get; set; }

    public required string PrimerApellido { get; set; }

    public required string SegundoApellido { get; set; }

    public required string Email { get; set; }

    public required string Nif { get; set; }

    public string? CodigoAsesoria { get; set; }

    // Solo gestores. El administrador es quien acepta, rechaza o finaliza las vinculaciones de su
    // asesoría (PB-04); el nombre es el que ve el autónomo al solicitarla.
    public bool EsAdministradorAsesoria { get; set; }

    public string? NombreAsesoria { get; set; }

    public Guid? GestorId { get; set; }

    public Rol Rol { get; set; }

    public Usuario? Gestor { get; set; }

    public ICollection<Usuario> Clientes { get; } = [];

    public ICollection<Documento> Documentos { get; } = [];

    public ICollection<Vinculacion> VinculacionesComoAutonomo { get; } = [];
}
