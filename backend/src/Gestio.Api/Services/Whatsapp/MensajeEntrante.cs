using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Services.Whatsapp;

// UT27: mensaje entrante ya traducido del formato del proveedor. Telefono en E.164 sin «+».
public sealed record MensajeEntrante(
    string MensajeId,
    string Telefono,
    TipoMensajeWhatsapp Tipo,
    string? MediaId = null,
    string? MimeType = null,
    string? NombreArchivo = null,
    string? Texto = null);
