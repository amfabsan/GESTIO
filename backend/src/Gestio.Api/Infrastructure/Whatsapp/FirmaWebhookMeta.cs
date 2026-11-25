using System.Security.Cryptography;
using System.Text;

namespace Gestio.Api.Infrastructure.Whatsapp;

// UT27 (requisito 5): Meta firma cada notificación con HMAC-SHA256 del cuerpo EXACTO usando el App Secret y la envía en
// la cabecera X-Hub-Signature-256 como «sha256=<hex>». Se calcula sobre los bytes recibidos, antes de interpretar el JSON.
public static class FirmaWebhookMeta
{
    public const string Cabecera = "X-Hub-Signature-256";
    private const string Prefijo = "sha256=";

    public static bool EsValida(ReadOnlySpan<byte> cuerpo, string? cabecera, string appSecret)
    {
        if (string.IsNullOrEmpty(appSecret) || cabecera is null || !cabecera.StartsWith(Prefijo, StringComparison.Ordinal)) return false;

        byte[] recibida;
        try
        {
            recibida = Convert.FromHexString(cabecera.AsSpan(Prefijo.Length));
        }
        catch (FormatException)
        {
            return false;
        }

        var esperada = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), cuerpo);
        // Comparación en tiempo constante; con longitudes distintas no hay nada que comparar.
        return recibida.Length == esperada.Length && CryptographicOperations.FixedTimeEquals(recibida, esperada);
    }

    public static string Calcular(ReadOnlySpan<byte> cuerpo, string appSecret) =>
        Prefijo + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), cuerpo));
}
