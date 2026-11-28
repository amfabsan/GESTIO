namespace Gestio.Api.Domain.Enums;

// UT27: tipo de mensaje entrante. Solo IMAGEN y DOCUMENTO pueden originar documentos; TEXTO sirve para verificar el número.
public enum TipoMensajeWhatsapp
{
    IMAGEN = 1,
    DOCUMENTO = 2,
    TEXTO = 3,
    OTRO = 4
}
