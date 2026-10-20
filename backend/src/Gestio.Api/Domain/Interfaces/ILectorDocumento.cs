namespace Gestio.Api.Domain.Interfaces;

// Coordenadas normalizadas a la página (0..1), también para los recortes.
// Confianza es la puntuación de la palabra entregada por el motor, no una confianza de campo.
public sealed record PalabraDocumento(string Texto, double X, double Y, double Ancho, double Alto, double? Confianza,
    int? LineaOrigen = null)
{
    public double CentroX => X + Ancho / 2;
    public double CentroY => Y + Alto / 2;
}

public sealed record RegionDocumento(double X, double Y, double Ancho, double Alto);

public sealed record LecturaDocumento(int Pagina, string Zona, string Variante, bool EsRegion,
    IReadOnlyList<PalabraDocumento> Palabras, RegionDocumento? Region = null, bool TextoNativo = false,
    string Geometria = "original");

public sealed record TextoDocumento(string Texto, string Version, IReadOnlyList<LecturaDocumento>? Lecturas = null);

public interface ILectorDocumento
{
    Task<TextoDocumento> LeerAsync(Stream original, string tipoMime, CancellationToken cancellationToken);
}
