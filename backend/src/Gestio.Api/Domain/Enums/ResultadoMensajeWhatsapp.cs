namespace Gestio.Api.Domain.Enums;

// UT27: estado del tratamiento de un mensaje entrante. EN_CURSO y FALLIDO se pueden reprocesar si el proveedor
// vuelve a entregar el mensaje (D3/D4); los demás son finales y una entrega repetida se ignora en silencio.
public enum ResultadoMensajeWhatsapp
{
    EN_CURSO = 1,
    DOCUMENTO_CREADO = 2,
    RECHAZADO = 3,
    NO_VINCULADO = 4,
    FALLIDO = 5,
    // Mensaje de texto de un número (vinculado o en verificación) que no crea documento: código, ayuda.
    RESPONDIDO = 6
}
