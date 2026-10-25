using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Domain.Entities;

public sealed class HistorialCategoria
{
    public Guid Id { get; set; }

    public Guid DocumentoId { get; set; }

    public Documento Documento { get; set; } = null!;

    public Guid UsuarioId { get; set; }

    public Usuario Usuario { get; set; } = null!;

    public CategoriaDocumento CategoriaAnterior { get; set; }

    public CategoriaDocumento CategoriaNueva { get; set; }

    public DateTimeOffset FechaCambio { get; set; }
}
