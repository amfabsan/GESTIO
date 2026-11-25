using QRCoder;

namespace Gestio.Api.Infrastructure.Whatsapp;

// UT27, capa 5: QR del canal de WhatsApp para la web (Overlay_WhatsApp del diseño). Se genera en el backend con QRCoder,
// como el de VeriFactu, para no añadir una librería de QR al frontend.
// - Canal: https://wa.me/<número> abre un chat con GESTIO.
// - Verificación: https://wa.me/<número>?text=<código> abre el chat con el código ya escrito.
public static class QrWhatsapp
{
    private const int PixelesPorModulo = 6;

    public static string? EnlaceCanal(string? numeroCanal, string? texto = null)
    {
        var digitos = new string((numeroCanal ?? "").Where(char.IsAsciiDigit).ToArray());
        // wa.me espera el número internacional sin «+» ni «00».
        if (numeroCanal?.TrimStart().StartsWith('+') != true && digitos.StartsWith("00")) digitos = digitos[2..];
        if (digitos.Length < 8) return null;
        return texto is null ? $"https://wa.me/{digitos}" : $"https://wa.me/{digitos}?text={Uri.EscapeDataString(texto)}";
    }

    public static string ImagenPngBase64(string enlace)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(enlace, QRCodeGenerator.ECCLevel.M);
        return Convert.ToBase64String(new PngByteQRCode(datos).GetGraphic(PixelesPorModulo));
    }
}
