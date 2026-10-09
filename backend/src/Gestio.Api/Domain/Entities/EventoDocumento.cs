using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Domain.Entities;

// Registro del historial: una vez creado no se modifica; cada cambio del documento añade un evento nuevo.
public sealed class EventoDocumento
{

    private EventoDocumento() { } // para EF Core

    public EventoDocumento(Guid usuarioId, TipoEventoDocumento tipo, DateTimeOffset fecha, string? detalle)
    {

        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Tipo = tipo;
        Fecha = fecha;
        Detalle = detalle;
        
    }

    public Guid Id { get; private set; }

    public Guid DocumentoId { get; private set; }

    public Guid UsuarioId { get; private set; }

    public TipoEventoDocumento Tipo { get; private set; }

    public DateTimeOffset Fecha { get; private set; }

    public string? Detalle { get; private set; }

    public Documento Documento { get; private set; } = null!;
}