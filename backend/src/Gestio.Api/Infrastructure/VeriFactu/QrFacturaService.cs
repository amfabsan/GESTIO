using Gestio.Api.Common;
using Gestio.Api.Services.Verifactu;
using Microsoft.Extensions.Options;
using QRCoder;

namespace Gestio.Api.Infrastructure.VeriFactu;

/// <summary>QR de una factura emitida: la URL que codifica y la imagen PNG en base64.</summary>
public sealed record QrFactura(string Url, string ImagenPngBase64);

/// <summary>
/// UT23 (PB-23). El QR se genera solo en el backend (CLAUDE.md, decisión D2) con QRCoder.
/// Req. 2 / prueba 1609: el contenido es exclusivamente <c>{UrlPublicaBase}/f/{código}</c>, sin datos fiscales.
/// </summary>
public sealed class QrFacturaService
{
    // Nivel M (≈15 % de redundancia), el mismo que usa el prototipo; 8 píxeles por módulo para que se lea impreso.
    private const QRCodeGenerator.ECCLevel NivelCorreccion = QRCodeGenerator.ECCLevel.M;
    private const int PixelesPorModulo = 8;

    private readonly string urlBase;

    public QrFacturaService(IOptions<VerifactuOptions> opciones)
    {
        // Uri.TryCreate tolera espacios en los extremos; se quitan para que no acaben dentro del QR.
        var url = opciones.Value.UrlPublicaBase?.Trim();
        if (!VerifactuOptions.EsUrlBaseValida(url))
            throw new InvalidOperationException(
                $"Configuración {VerifactuOptions.Seccion}:UrlPublicaBase no válida: debe ser una URL http(s) absoluta sin query ni fragmento.");
        urlBase = url!.TrimEnd('/');
    }

    /// <summary>Contenido del QR. Separado de la imagen para poder probar exactamente qué se codifica.</summary>
    public string ConstruirUrl(string codigoPublico)
    {
        if (!CodigoPublicoFactura.EsFormatoValido(codigoPublico))
            throw new ArgumentException("Código público con formato no válido.", nameof(codigoPublico));
        return $"{urlBase}/f/{codigoPublico}";
    }

    /// <summary>QR de la factura, o null si no tiene código (borrador o factura emitida antes de UT23).</summary>
    public QrFactura? Para(string? codigoPublico)
    {
        if (codigoPublico is null) return null;
        var url = ConstruirUrl(codigoPublico);
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(url, NivelCorreccion);
        var png = new PngByteQRCode(datos).GetGraphic(PixelesPorModulo);
        return new QrFactura(url, Convert.ToBase64String(png));
    }
}
