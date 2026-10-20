using Gestio.Api.Domain.Interfaces;

namespace Gestio.Api.Services;

public sealed record LineaDocumento(IReadOnlyList<PalabraDocumento> Palabras)
{
    public string Texto => string.Join(" ", Palabras.Select(p => p.Texto));
    public double X => Palabras.Min(p => p.X);
    public double Y => Palabras.Min(p => p.Y);
    public double Derecha => Palabras.Max(p => p.X + p.Ancho);
    public double Abajo => Palabras.Max(p => p.Y + p.Alto);
}

public static class DisposicionDocumento
{
    public static IReadOnlyList<LineaDocumento> Lineas(IEnumerable<PalabraDocumento> palabras)
    {
        var filas = new List<List<PalabraDocumento>>();
        foreach (var palabra in palabras.OrderBy(p => p.CentroY))
        {
            var fila = filas.LastOrDefault();
            if (fila is null || Math.Abs(fila.Average(p => p.CentroY) - palabra.CentroY) >
                Math.Max(palabra.Alto, fila.Average(p => p.Alto)) * .55)
            {
                fila = [];
                filas.Add(fila);
            }
            fila.Add(palabra);
        }
        return filas.Select(f => new LineaDocumento(f.OrderBy(p => p.X).ToArray())).ToArray();
    }

    public static string Texto(IEnumerable<PalabraDocumento> palabras) =>
        string.Join("\n", Lineas(palabras).Select(l => l.Texto));
}
