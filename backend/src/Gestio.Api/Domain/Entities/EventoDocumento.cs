using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Domain.Entities;

// Registro del historial: una vez creado no se modifica; cada cambio del documento añade un evento nuevo.
// Un evento sin usuario es una acción automática del sistema.
public sealed class EventoDocumento
{

    private EventoDocumento() { } // para EF Core

    public EventoDocumento(Guid? usuarioId, TipoEventoDocumento tipo, DateTimeOffset fecha, string? detalle)
    {

        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Tipo = tipo;
        Fecha = fecha;
        Detalle = detalle;

    }

    public static EventoDocumento Correccion(Guid usuarioId, DateTimeOffset fecha, string campo, string? valorAnterior, string? valorNuevo) =>
        new(usuarioId, TipoEventoDocumento.VALIDACION_CORREGIDA, fecha, $"Campo '{campo}' corregido por el gestor.")
        {
            Campo = campo,
            ValorAnterior = valorAnterior,
            ValorNuevo = valorNuevo
        };

    // Mismo formato que una corrección, para que el historial (PB-08) muestre la categoría anterior y la nueva.
    public static EventoDocumento CambioCategoria(Guid usuarioId, DateTimeOffset fecha, CategoriaDocumento anterior, CategoriaDocumento nueva) =>
        new(usuarioId, TipoEventoDocumento.CATEGORIZACION_ACTUALIZADA, fecha, $"Categoría actualizada de {anterior} a {nueva}.")
        {
            Campo = "Categoria",
            ValorAnterior = anterior.ToString(),
            ValorNuevo = nueva.ToString()
        };

    public Guid Id { get; private set; }

    public Guid DocumentoId { get; private set; }

    public Guid? UsuarioId { get; private set; }

    public TipoEventoDocumento Tipo { get; private set; }

    public DateTimeOffset Fecha { get; private set; }

    public string? Detalle { get; private set; }

    // Solo en correcciones: campo afectado y sus valores antes y después del cambio.
    public string? Campo { get; private set; }

    public string? ValorAnterior { get; private set; }

    public string? ValorNuevo { get; private set; }

    public Documento Documento { get; private set; } = null!;

    public Usuario? Usuario { get; private set; }
}