namespace Gestio.Api.Domain.Entities;

// UT27 (PA 3605): número de WhatsApp de un autónomo. Una fila por autónomo.
// Los teléfonos se guardan en E.164 sin «+» (solo dígitos), como los entrega Meta en el campo «from».
public sealed class NumeroWhatsapp
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }

    // Número activo: el único que puede originar documentos. Único entre todas las cuentas.
    public string? TelefonoVerificado { get; set; }

    public DateTimeOffset? FechaVerificacion { get; set; }

    // Verificación en curso (D2): el código solo vale si llega desde este número. Mientras tanto, el número
    // verificado anterior (si lo hay) sigue activo.
    public string? TelefonoPendiente { get; set; }

    public string? CodigoVerificacion { get; set; }

    public DateTimeOffset? CodigoCaducaEn { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
