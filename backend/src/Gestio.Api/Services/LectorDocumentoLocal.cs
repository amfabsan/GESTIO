using System.Diagnostics;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Gestio.Api.Domain.Interfaces;
using SkiaSharp;

namespace Gestio.Api.Services;

// Los ejecutables pueden ser rutas absolutas; nunca se ejecuta un shell.
public sealed class LectorDocumentoLocal(IConfiguration configuration) : ILectorDocumento
{
    public async Task<TextoDocumento> LeerAsync(Stream original, string tipoMime, CancellationToken cancellationToken)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(TimeSpan.FromMinutes(3));
        var ct = limite.Token;
        var carpeta = Directory.CreateTempSubdirectory("gestio-ocr-").FullName;
        try
        {
            var extension = tipoMime switch
            {
                "application/pdf" => ".pdf", "image/png" => ".png", "image/jpeg" => ".jpg",
                _ => throw new InvalidOperationException("Formato no compatible con la extracción: " + tipoMime)
            };
            var archivo = Path.Combine(carpeta, "original" + extension);
            await using (var salida = File.Create(archivo)) await original.CopyToAsync(salida, ct);
            var version = await Ejecutar("Tesseract", "tesseract", ct, "--version");
            var textos = new List<string>();
            var lecturas = new List<LecturaDocumento>();
            if (tipoMime == "application/pdf")
            {
                var xml = await Ejecutar("PdfToText", "pdftotext", ct, "-bbox-layout", "-enc", "UTF-8", archivo, "-");
                using var lectorXml = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
                    { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 50_000_000 });
                var paginas = XDocument.Load(lectorXml).Descendants().Where(e => e.Name.LocalName == "page").ToArray();
                if (paginas.Length > 50) throw new InvalidOperationException("El PDF supera el límite de 50 páginas para extracción.");
                for (var i = 0; i < paginas.Length; i++)
                {
                    var pagina = paginas[i];
                    static double Atributo(XElement e, string nombre) => double.Parse(e.Attribute(nombre)!.Value, CultureInfo.InvariantCulture);
                    var ancho = Atributo(pagina, "width"); var alto = Atributo(pagina, "height");
                    var palabras = pagina.Descendants().Where(e => e.Name.LocalName == "word")
                        .Select(e => new PalabraDocumento(e.Value, Atributo(e, "xMin") / ancho, Atributo(e, "yMin") / alto,
                            (Atributo(e, "xMax") - Atributo(e, "xMin")) / ancho,
                            (Atributo(e, "yMax") - Atributo(e, "yMin")) / alto, null)).ToArray();
                    var texto = DisposicionDocumento.Texto(palabras);
                    if (EsUtil(texto))
                    {
                        textos.Add(texto);
                        lecturas.Add(new LecturaDocumento(i + 1, "pagina", "texto-pdf", false, palabras, TextoNativo: true));
                        continue;
                    }
                    var prefijo = Path.Combine(carpeta, "pagina");
                    await Ejecutar("PdfToPpm", "pdftoppm", ct, "-f", (i + 1).ToString(), "-l", (i + 1).ToString(), "-scale-to", "3500", "-singlefile", "-png", archivo, prefijo);
                    textos.Add(await Reconocer(prefijo + ".png", i + 1, lecturas, ct));
                }
            }
            else textos.Add(await Reconocer(archivo, 1, lecturas, ct));
            var reconocido = string.Join("\n", textos);
            if (!EsUtil(reconocido)) throw new InvalidOperationException("No se reconoce texto útil. Sube un documento más legible.");
            return new TextoDocumento(reconocido, version.Split('\n')[0].Trim(), lecturas);
        }
        finally { Directory.Delete(carpeta, true); }
    }

    private static bool EsUtil(string texto) => texto.Count(char.IsLetterOrDigit) >= 15 && texto.Any(char.IsLetter);

    private async Task<string> Reconocer(string archivo, int pagina, List<LecturaDocumento> lecturas, CancellationToken ct)
    {
        var variantes = PreprocesadorDocumento.Preparar(archivo, ct);
        var principales = new List<(ImagenOcr Imagen, LecturaDocumento Lectura)>();
        foreach (var variante in variantes)
        {
            using var codec = SKCodec.Create(variante.Ruta);
            foreach (var modo in new[] { "3", "6", "11" })
            {
                var lectura = await LeerZona(variante.Ruta, pagina, "pagina", modo, false, 0, 0,
                    codec.Info.Width, codec.Info.Height, 1, 1, ct, variante.Variante, variante.Geometria);
                lecturas.Add(lectura);
                principales.Add((variante, lectura));
            }
        }
        static double Calidad(LecturaDocumento l) => l.Palabras.Sum(p => Math.Max(0, (p.Confianza ?? 0) - 40) * p.Texto.Length);
        // Releer desde la imagen de la lectura elegida; nunca mezclar coordenadas
        // de la fotografía con las de una página rectificada.
        var seleccion = principales.MaxBy(p => Calidad(p.Lectura));
        foreach (var candidata in principales.GroupBy(p => p.Imagen.Ruta).Select(g => g.MaxBy(p => Calidad(p.Lectura))))
            await Releer(candidata.Imagen, candidata.Lectura, pagina, lecturas, ct);
        return DisposicionDocumento.Texto(seleccion.Lectura.Palabras);
    }

    private async Task Releer(ImagenOcr variante, LecturaDocumento mejorLectura, int pagina,
        List<LecturaDocumento> lecturas, CancellationToken ct)
    {
        var preparado = variante.Ruta;
        using var imagen = SKBitmap.Decode(preparado);

        // Releer sólo cifras dudosas: las tablas con palabras fiables ya conservan su geometría.
        var lineas = DisposicionDocumento.Lineas(mejorLectura.Palabras)
            .Where(l => l.Abajo - l.Y < .065 && l.Palabras.Any(p =>
                p.Texto.Any(char.IsDigit) && p.Confianza < 80) &&
                (l.Y < .30 || l.Y > .60))
            .OrderBy(l => l.Palabras.Where(p => p.Texto.Any(char.IsDigit)).Min(p => p.Confianza))
            .Take(5).ToArray();
        var anclas = lecturas.Where(l => !l.EsRegion && l.Pagina == pagina &&
                l.Geometria == variante.Geometria && l.Variante.StartsWith(variante.Variante + "/", StringComparison.Ordinal))
            .SelectMany(l => l.Palabras).Where(p => p.Alto < .06 && p.Alto > .003 &&
                System.Text.RegularExpressions.Regex.IsMatch(p.Texto, @"^(?:TOTAL|BASE|IVA|CIF|NIF)[:.]?$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .OrderBy(p => p.Texto.StartsWith("TOTAL", StringComparison.OrdinalIgnoreCase) ? 0 :
                p.Texto.StartsWith("BASE", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .GroupBy(p => Math.Round(p.CentroY / .015)).Select(g => g.First()).Take(6)
            .Select(p => new LineaDocumento([new PalabraDocumento("", 0, Math.Max(0,p.Y-p.Alto*.2), 1, p.Alto*1.4, null)]));
        var zonas = anclas.Concat(lineas).Distinct().Take(7)
            .Select((l, i) => (Nombre: $"linea-{i}", Linea: l, Modos: new[] { "7", "13" }))
            .Concat(mejorLectura.Palabras.Where(p => p.Texto.Count(char.IsDigit) >= 4 &&
                (p.Y < .30 || p.Y > .60) && p.Alto < .06 && p.Ancho < .6 && p.Confianza < 90)
                .OrderBy(p => p.Confianza).Take(4).Select((p, i) =>
                    (Nombre: $"palabra-{i}", Linea: new LineaDocumento([p]),
                        Modos: new[] { "8", "13" })))
            .Concat(new[] {
                (Nombre: "cabecera", Linea: new LineaDocumento([new PalabraDocumento("", 0, 0, 1, .28, null)]), Modos: new[] { "6", "11" }),
                (Nombre: "resumen", Linea: new LineaDocumento([new PalabraDocumento("", 0, .65, 1, .35, null)]), Modos: new[] { "6", "11" })
            }).ToArray();
        for (var i = 0; i < zonas.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var l = zonas[i].Linea;
            var margen = Math.Max(.002, (l.Abajo - l.Y) * .12);
            var rectangulo = new SKRectI(
                Math.Max(0, (int)((l.X - margen) * imagen.Width)),
                Math.Max(0, (int)((l.Y - margen) * imagen.Height)),
                Math.Min(imagen.Width, (int)Math.Ceiling((l.Derecha + margen) * imagen.Width)),
                Math.Min(imagen.Height, (int)Math.Ceiling((l.Abajo + margen) * imagen.Height)));
            using var recorte = new SKBitmap();
            if (!imagen.ExtractSubset(recorte, rectangulo)) continue;
            var bloque = zonas[i].Nombre is "cabecera" or "resumen";
            var escala = bloque ? Math.Min(1.5, 3000.0 / recorte.Width)
                : Math.Clamp(Math.Min(80.0 / recorte.Height, 3000.0 / recorte.Width), .25, 3);
            var escalaX = escala * (zonas[i].Nombre.StartsWith("palabra-", StringComparison.Ordinal) ? 1.5 : 1);
            using var ampliado = recorte.Resize(new SKImageInfo((int)(recorte.Width * escalaX), (int)(recorte.Height * escala)),
                new SKSamplingOptions(SKCubicResampler.Mitchell));
            const int borde = 10;
            using var conBorde = new SKBitmap(ampliado.Width + borde * 2, ampliado.Height + borde * 2);
            using (var canvas = new SKCanvas(conBorde))
            {
                canvas.Clear(SKColors.White);
                canvas.DrawBitmap(ampliado, borde, borde);
            }
            var ruta = preparado + $".zona-{i}.png";
            using (var codificada = conBorde.Encode(SKEncodedImageFormat.Png, 100))
            using (var destino = File.Create(ruta)) codificada.SaveTo(destino);
            foreach (var modo in zonas[i].Modos)
            {
                lecturas.Add(await LeerZona(ruta, pagina, zonas[i].Nombre, modo, true,
                    rectangulo.Left - borde / escalaX, rectangulo.Top - borde / escala,
                    imagen.Width, imagen.Height, escalaX, escala, ct, variante.Variante, variante.Geometria));
            }
        }
    }

    private async Task<LecturaDocumento> LeerZona(string archivo, int pagina, string zona, string modo, bool regional,
        double origenX, double origenY, int anchoPagina, int altoPagina, double escalaX, double escalaY, CancellationToken ct,
        string variante = "gris", string geometria = "original")
    {
        var argumentos = new List<string> { archivo, "stdout", "-l", "spa", "--dpi", "300", "--psm", modo,
            "-c", "tessedit_create_tsv=1" };
        var datos = configuration["Ocr:Tessdata"];
        var incluidos = Path.Combine(AppContext.BaseDirectory, "Ocr", "tessdata");
        if (!string.IsNullOrWhiteSpace(datos)) argumentos.AddRange(["--tessdata-dir", Environment.ExpandEnvironmentVariables(datos)]);
        else if (File.Exists(Path.Combine(incluidos, "spa.traineddata"))) argumentos.AddRange(["--tessdata-dir", incluidos]);
        var tsv = await Ejecutar("Tesseract", "tesseract", ct, argumentos.ToArray());
        var palabras = tsv.Split('\n').Skip(1).Select(l => l.TrimEnd('\r').Split('\t', 12))
            .Where(c => c.Length == 12 && c[0] == "5" && !string.IsNullOrWhiteSpace(c[11]))
            .Select(c => new PalabraDocumento(c[11],
                (origenX + int.Parse(c[6]) / escalaX) / anchoPagina,
                (origenY + int.Parse(c[7]) / escalaY) / altoPagina,
                int.Parse(c[8]) / escalaX / anchoPagina, int.Parse(c[9]) / escalaY / altoPagina,
                double.TryParse(c[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var confianza) && confianza >= 0 ? confianza : null))
            .ToArray();
        using var dimensiones = SKCodec.Create(archivo);
        var region = regional ? new RegionDocumento(origenX / anchoPagina, origenY / altoPagina,
            dimensiones.Info.Width / escalaX / anchoPagina, dimensiones.Info.Height / escalaY / altoPagina) : null;
        return new LecturaDocumento(pagina, zona, variante + "/psm-" + modo, regional, palabras, region, Geometria: geometria);
    }

    private async Task<string> Ejecutar(string clave, string defecto, CancellationToken ct, params string[] argumentos)
    {
        var inicio = new ProcessStartInfo(ResolverEjecutable(clave, defecto))
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argumento in argumentos) inicio.ArgumentList.Add(argumento);
        using var proceso = new Process { StartInfo = inicio };
        try { proceso.Start(); }
        catch (System.ComponentModel.Win32Exception ex) { throw new InvalidOperationException($"No está disponible {defecto}. Configura el motor OCR local y el idioma español.", ex); }
        var salida = proceso.StandardOutput.ReadToEndAsync(ct);
        var error = proceso.StandardError.ReadToEndAsync(ct);
        try { await proceso.WaitForExitAsync(ct); }
        catch { if (!proceso.HasExited) proceso.Kill(true); throw; }
        var resultado = await salida;
        await error;
        if (proceso.ExitCode != 0) throw new InvalidOperationException($"{defecto} no pudo leer el documento. Comprueba que no esté dañado o protegido y que esté instalado spa.");
        return resultado;
    }

    private string ResolverEjecutable(string clave, string defecto)
    {
        var configurado = configuration["Ocr:" + clave];
        if (!string.IsNullOrWhiteSpace(configurado)) return Environment.ExpandEnvironmentVariables(configurado);
        if (OperatingSystem.IsWindows())
        {
            // Las instalaciones habituales no siempre están en el PATH del IDE ya abierto.
            var instalado = defecto == "tesseract"
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tesseract.exe")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links", defecto + ".exe");
            if (File.Exists(instalado)) return instalado;
            var rutas = (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "")
                + ";" + Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
            foreach (var carpeta in rutas.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidato = Path.Combine(Environment.ExpandEnvironmentVariables(carpeta.Trim().Trim('"')), defecto + ".exe");
                if (File.Exists(candidato)) return candidato;
            }
        }
        return defecto;
    }
}
