using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Gestio.Api.Services.Whatsapp;

// UT27 (D2): código que el autónomo envía por WhatsApp para verificar su número: «GESTIO-XXXXXX».
public static partial class CodigoVerificacionWhatsapp
{
    public const string Prefijo = "GESTIO-";

    // Sin 0/O ni 1/I para que no se confundan al teclearlos: 32 símbolos, 32^6 ≈ 1.070 millones de códigos.
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(15);

    [GeneratedRegex("^GESTIO-?([A-Z0-9]{6})$")]
    private static partial Regex Formato();

    public static string Generar() => Prefijo + RandomNumberGenerator.GetString(Alfabeto, 6);

    // El mensaje completo tiene que ser el código (sin texto alrededor). Se ignoran mayúsculas, espacios y el guion.
    // Devuelve el código en su forma guardada o null si el mensaje no es un código.
    public static string? Leer(string? mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje)) return null;

        var compacto = new string(mensaje.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        var coincidencia = Formato().Match(compacto);
        return coincidencia.Success ? Prefijo + coincidencia.Groups[1].Value : null;
    }
}
