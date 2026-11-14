using System.Security.Cryptography;

namespace Gestio.Api.Services.Verifactu;

/// <summary>
/// UT23 (PB-23), req. 2: identificador público no predecible de una factura emitida.
/// 22 caracteres base62 elegidos con un generador criptográfico (≈131 bits). No contiene ni deriva de datos fiscales.
/// </summary>
public static class CodigoPublicoFactura
{
    public const int Longitud = 22;
    public const string Alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>RandomNumberGenerator.GetString elige cada carácter de forma uniforme (sin sesgo de módulo).</summary>
    public static string Generar() => RandomNumberGenerator.GetString(Alfabeto, Longitud);

    /// <summary>
    /// Comprobación previa a consultar la BD: descarta enlaces truncados, alterados con otros caracteres o demasiado largos.
    /// </summary>
    public static bool EsFormatoValido(string? codigo) =>
        codigo is { Length: Longitud } && codigo.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9');
}
