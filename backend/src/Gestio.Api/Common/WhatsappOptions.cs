namespace Gestio.Api.Common;

// UT27: configuración del canal de WhatsApp (sección «Whatsapp»). Los valores secretos (TokenAcceso, AppSecret,
// TokenVerificacion) van en `dotnet user-secrets` o en variables de entorno, nunca en appsettings.json.
public sealed class WhatsappOptions
{
    public const string Seccion = "Whatsapp";

    public bool Habilitado { get; set; }

    // Número del canal de GESTIO tal como se muestra al autónomo (p. ej. «+1 555 012 3456»).
    public string NumeroCanal { get; set; } = "";

    // «Phone number ID» del número de WhatsApp Business en Meta. También se usa para descartar los mensajes dirigidos a
    // otro número (requisito 5: solo la integración configurada).
    public string PhoneNumberId { get; set; } = "";

    public string TokenAcceso { get; set; } = "";

    // «App secret» de la app de Meta: firma X-Hub-Signature-256 de cada notificación del webhook.
    public string AppSecret { get; set; } = "";

    // Texto inventado por el desarrollador y registrado en Meta para el reto GET de verificación del webhook.
    public string TokenVerificacion { get; set; } = "";

    public string VersionApi { get; set; } = "v21.0";

    public string UrlGraph { get; set; } = "https://graph.facebook.com";

    // Con el canal habilitado, todo lo necesario tiene que estar: se comprueba al arrancar.
    public static bool EsValida(WhatsappOptions o) =>
        !o.Habilitado ||
        (!string.IsNullOrWhiteSpace(o.NumeroCanal) && !string.IsNullOrWhiteSpace(o.PhoneNumberId) &&
         !string.IsNullOrWhiteSpace(o.TokenAcceso) && !string.IsNullOrWhiteSpace(o.AppSecret) &&
         !string.IsNullOrWhiteSpace(o.TokenVerificacion) && !string.IsNullOrWhiteSpace(o.VersionApi) &&
         Uri.TryCreate(o.UrlGraph, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps);
}
