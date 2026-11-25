namespace Gestio.Api.Common;

// UT27: configuración del canal de WhatsApp (sección «Whatsapp»). Los secretos del proveedor se añaden en la capa 4 y
// van en `dotnet user-secrets`, nunca en appsettings.json.
public sealed class WhatsappOptions
{
    public const string Seccion = "Whatsapp";

    public bool Habilitado { get; set; }

    // Número del canal de GESTIO tal como se muestra al autónomo (p. ej. «+1 555 012 3456»).
    public string NumeroCanal { get; set; } = "";
}
