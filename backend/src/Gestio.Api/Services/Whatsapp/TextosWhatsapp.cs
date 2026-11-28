namespace Gestio.Api.Services.Whatsapp;

// UT27: respuestas que GESTIO envía por WhatsApp. Reunidas aquí para revisar de un vistazo que ninguna revela datos de
// otras cuentas (requisito 7) ni afirma que un documento esté validado (requisito 6).
public static class TextosWhatsapp
{
    // Requisito 7: la ÚNICA respuesta a un número no vinculado, sea cual sea el mensaje (archivo, texto o código erróneo).
    public const string NumeroNoVinculado =
        "Hola. Este número no está vinculado a ninguna cuenta de GESTIO, así que no hemos guardado nada. " +
        "Para enviar documentos, entra en la web de GESTIO, vincula tu número en «WhatsApp» y envía aquí el código que te mostramos.";

    public const string NumeroVerificado =
        "Número vinculado correctamente. Ya puedes enviar fotos (JPG o PNG) o PDF de tus facturas y tickets.";

    public const string CodigoNoValido =
        "El código no es válido o ha caducado. Genera uno nuevo desde la web de GESTIO y envíalo desde este número.";

    public const string Ayuda =
        "Envía una foto (JPG o PNG) o un PDF de tu factura o ticket y lo añadiremos a tu buzón de GESTIO.";

    public const string TipoNoAdmitido =
        "Solo podemos recibir fotos (JPG o PNG) y documentos PDF. Este mensaje no se ha añadido a tu buzón.";

    public const string ErrorTemporal =
        "No hemos podido guardar tu archivo por un problema temporal, así que no se ha añadido a tu buzón. " +
        "Vuelve a enviarlo dentro de unos minutos.";

    // Requisito 6: recibido y pendiente; nunca «validado».
    public static string Recibido(string nombreArchivo) =>
        $"Hemos recibido «{nombreArchivo}». Queda pendiente de procesamiento y revisión; puedes seguir su estado en tu buzón de GESTIO.";

    public static string Rechazado(string nombreArchivo, string motivo) =>
        $"No hemos podido añadir «{nombreArchivo}» a tu buzón. {motivo}";
}
