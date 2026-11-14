namespace Gestio.Api.Common;

/// <summary>UT23 (PB-23), decisión D7: dirección pública a la que lleva el QR.</summary>
public sealed class VerifactuOptions
{
    public const string Seccion = "Verifactu";

    /// <summary>
    /// Origen del frontend tal como lo verá un teléfono (p. ej. http://192.168.1.20:5173). El QR codifica
    /// <c>{UrlPublicaBase}/f/{código}</c>. Se recalcula en cada consulta: lo que no cambia es el código.
    /// </summary>
    public string UrlPublicaBase { get; set; } = "";

    /// <summary>Solo http/https, absoluta y sin query ni fragmento: el QR no debe llevar nada más que el código.</summary>
    public static bool EsUrlBaseValida(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && string.IsNullOrEmpty(uri.UserInfo);
}
