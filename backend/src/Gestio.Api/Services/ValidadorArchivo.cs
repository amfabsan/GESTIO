namespace Gestio.Api.Services;

public sealed record TipoArchivoPermitido(string Extension, string TipoMime, byte[] Firma);

// Comprueba el tipo real del archivo por su firma (magic bytes); cambiar la extensión no basta para colarlo.
public static class ValidadorArchivo
{
    private static readonly Dictionary<string, string> MimePorExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png"
    };

    private static readonly Dictionary<string, byte[]> FirmaPorMime = new()
    {
        ["application/pdf"] = "%PDF-"u8.ToArray(),
        ["image/jpeg"] = [0xFF, 0xD8, 0xFF],
        ["image/png"] = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]
    };

    public static readonly IReadOnlyList<TipoArchivoPermitido> TiposPermitidos = MimePorExtension
        .Select(par => new TipoArchivoPermitido(par.Key, par.Value, FirmaPorMime[par.Value]))
        .ToList();

    // Bytes que hay que leer del archivo para poder comparar la firma más larga.
    public static readonly int BytesCabecera = FirmaPorMime.Values.Max(firma => firma.Length);

    // Devuelve el tipo MIME real si la extensión está permitida y el contenido coincide con ella; si no, null.
    public static string? ObtenerTipoMimeReal(string nombreArchivo, ReadOnlySpan<byte> cabecera)
    {
        if (!MimePorExtension.TryGetValue(Path.GetExtension(nombreArchivo), out var tipoMime)) return null;

        return cabecera.StartsWith(FirmaPorMime[tipoMime]) ? tipoMime : null;
    }

    public static string ExtensionDeAlmacenamiento(string tipoMime) => tipoMime switch
    {
        "application/pdf" => ".pdf",
        "image/jpeg" => ".jpg",
        _ => ".png"
    };
}