using SkiaSharp;

namespace Gestio.Api.Services;

public sealed record ImagenOcr(string Ruta, string Variante, string Geometria);

// Sólo trabaja sobre copias temporales. No aumenta artificialmente la confianza del OCR.
public static class PreprocesadorDocumento
{
    public static IReadOnlyList<ImagenOcr> Preparar(string archivo, CancellationToken ct)
    {
        using var codec = SKCodec.Create(archivo) ?? throw new InvalidOperationException("No se puede decodificar la imagen original.");
        if ((long)codec.Info.Width * codec.Info.Height > 40_000_000)
            throw new InvalidOperationException("La imagen supera el límite de 40 megapíxeles para OCR.");
        using var original = SKBitmap.Decode(codec) ?? throw new InvalidOperationException("No se puede leer la imagen original.");
        var escala = Math.Min(Math.Clamp(1800.0 / original.Width, 1, 5), 3500.0 / Math.Max(original.Width, original.Height));
        using var redimensionada = original.Resize(new SKImageInfo(Math.Max(1, (int)(original.Width * escala)),
            Math.Max(1, (int)(original.Height * escala))), new SKSamplingOptions(SKCubicResampler.Mitchell));
        // Aplicar EXIF antes de detectar geometría; las fotos móviles pueden venir giradas.
        using var orientada = Orientar(redimensionada, codec.EncodedOrigin);
        using var gris = new SKBitmap(orientada.Width, orientada.Height);
        using (var canvas = new SKCanvas(gris))
        using (var pintura = new SKPaint { ColorFilter = SKColorFilter.CreateColorMatrix([
            .2126f,.7152f,.0722f,0,0, .2126f,.7152f,.0722f,0,0, .2126f,.7152f,.0722f,0,0, 0,0,0,1,0]) })
        { canvas.Clear(SKColors.White); canvas.DrawBitmap(orientada, 0, 0, pintura); }
        var resultado = new List<ImagenOcr> { Guardar(gris, archivo, "gris", "original") };
        ct.ThrowIfCancellationRequested();

        // Foto lejana: el papel ocupa sólo parte de la imagen. Se recorta y endereza
        // el cuadrilátero del papel para que el texto gane resolución y las zonas
        // de cabecera/resumen de la relectura caigan sobre la factura y no sobre la mesa.
        if (DetectarDocumento(orientada) is { } esquinas)
        {
            using var documento = Recortar(gris, esquinas, out var ampliacion);
            ct.ThrowIfCancellationRequested();
            using var iluminado = NormalizarIluminacion(documento, out _);
            using var enfocado = ampliacion > 1.3 ? Enfocar(iluminado) : iluminado.Copy();
            var anguloDocumento = DetectarInclinacion(enfocado);
            using var recto = Math.Abs(anguloDocumento) >= .25 ? Girar(enfocado, -anguloDocumento) : enfocado.Copy();
            resultado.Add(Guardar(recto, archivo, "documento", "documento"));
            ct.ThrowIfCancellationRequested();
            using var umbralDocumento = UmbralAdaptativo(recto);
            resultado.Add(Guardar(umbralDocumento, archivo, "documento-umbral", "documento"));
            return resultado;
        }

        using var perspectiva = Rectificar(gris);
        var angulo = DetectarInclinacion(perspectiva);
        using var corregida = Girar(perspectiva, -angulo);
        var cambioGeometrico = Math.Abs(angulo) >= .25 || !MismosPixeles(gris, perspectiva);
        var geometria = cambioGeometrico ? "corregida" : "original";
        // Pliegues y luz desigual: sólo se corrige la iluminación cuando hay sombras
        // claras, para no alterar las fotos que ya se leían bien.
        using var iluminada = NormalizarIluminacion(corregida, out var variacion);
        var sombras = variacion >= 35;
        if (cambioGeometrico || sombras)
            resultado.Add(Guardar(sombras ? iluminada : corregida, archivo, "rectificada", geometria));
        ct.ThrowIfCancellationRequested();
        using var umbral = UmbralAdaptativo(corregida);
        resultado.Add(Guardar(umbral, archivo, "umbral", geometria));
        return resultado;
    }

    private static ImagenOcr Guardar(SKBitmap imagen, string archivo, string variante, string geometria)
    {
        var ruta = archivo + "." + variante + ".png";
        using var datos = imagen.Encode(SKEncodedImageFormat.Png, 100);
        using var salida = File.Create(ruta);
        datos.SaveTo(salida);
        return new(ruta, variante, geometria);
    }

    private static SKBitmap Orientar(SKBitmap b, SKEncodedOrigin origen)
    {
        var intercambiar = (int)origen >= 5;
        var salida = new SKBitmap(intercambiar ? b.Height : b.Width, intercambiar ? b.Width : b.Height);
        var entrada = b.Pixels; var pixeles = new SKColor[entrada.Length];
        for (var y = 0; y < b.Height; y++)
        for (var x = 0; x < b.Width; x++)
        {
            var (dx, dy) = (int)origen switch
            {
                2 => (b.Width - 1 - x, y), 3 => (b.Width - 1 - x, b.Height - 1 - y),
                4 => (x, b.Height - 1 - y), 5 => (y, x), 6 => (b.Height - 1 - y, x),
                7 => (b.Height - 1 - y, b.Width - 1 - x), 8 => (y, b.Width - 1 - x), _ => (x, y)
            };
            pixeles[dy * salida.Width + dx] = entrada[y * b.Width + x];
        }
        salida.Pixels = pixeles; return salida;
    }

    // Busca el papel como la mayor región clara sobre un fondo más oscuro. Devuelve las esquinas
    // (sup-izq, sup-der, inf-der, inf-izq) en píxeles de la imagen, o null si el papel
    // ya llena la foto o su contorno no es un cuadrilátero fiable (mano, esquina tapada...).
    public static SKPoint[]? DetectarDocumento(SKBitmap imagen)
    {
        var f = Math.Min(1, 480.0 / Math.Max(imagen.Width, imagen.Height));
        using var mini = imagen.Resize(new SKImageInfo(Math.Max(1, (int)(imagen.Width * f)),
            Math.Max(1, (int)(imagen.Height * f))), new SKSamplingOptions(SKFilterMode.Linear));
        var w = mini.Width; var h = mini.Height; var n = w * h;
        // Luminancia: con luz cálida el canal azul del papel cae casi al nivel de la mesa.
        var valores = mini.Pixels.Select(p => (byte)(.2126 * p.Red + .7152 * p.Green + .0722 * p.Blue)).ToArray();
        // Corte por debajo de Otsu: la sombra de un pliegue oscurece el papel y no
        // debe partirlo en dos; sigue siendo bastante más claro que el fondo.
        var (mediaFondo, mediaPapel) = Otsu(valores);
        var umbral = mediaFondo + (mediaPapel - mediaFondo) * .2;
        var etiquetas = new int[n]; var mayor = 0; var areaMayor = 0; var cola = new Queue<int>();
        for (int i = 0, etiqueta = 0; i < n; i++)
        {
            if (valores[i] <= umbral || etiquetas[i] != 0) continue;
            etiqueta++; var area = 0; etiquetas[i] = etiqueta; cola.Enqueue(i);
            while (cola.TryDequeue(out var p))
            {
                area++;
                foreach (var q in Vecinos(p, w, h))
                    if (valores[q] > umbral && etiquetas[q] == 0) { etiquetas[q] = etiqueta; cola.Enqueue(q); }
            }
            if (area > areaMayor) { areaMayor = area; mayor = etiqueta; }
        }
        if (mayor == 0) return null;
        // Rellenar huecos (texto, logos, tablas): lo no alcanzable desde el borde es papel.
        var fuera = new bool[n];
        void Sembrar(int p) { if (!fuera[p] && etiquetas[p] != mayor) { fuera[p] = true; cola.Enqueue(p); } }
        for (var x = 0; x < w; x++) { Sembrar(x); Sembrar((h - 1) * w + x); }
        for (var y = 0; y < h; y++) { Sembrar(y * w); Sembrar(y * w + w - 1); }
        while (cola.TryDequeue(out var p))
            foreach (var q in Vecinos(p, w, h)) Sembrar(q);
        var areaPapel = fuera.Count(v => !v);
        // Sólo actuar si el papel es claramente menor que la foto: las fotos cercanas
        // y los escaneos siguen el flujo anterior sin cambios.
        if (areaPapel < n * .06 || areaPapel > n * .85) return null;
        // Papel cortado por el encuadre: sus "esquinas" serían el borde de la foto.
        var enBorde = 0;
        for (var x = 0; x < w; x++) enBorde += (etiquetas[x] == mayor ? 1 : 0) + (etiquetas[(h - 1) * w + x] == mayor ? 1 : 0);
        for (var y = 0; y < h; y++) enBorde += (etiquetas[y * w] == mayor ? 1 : 0) + (etiquetas[y * w + w - 1] == mayor ? 1 : 0);
        if (enBorde > (w + h) * 2 * .02) return null;
        // Envolvente convexa del papel y el cuadrilátero inscrito de mayor área: los
        // bordes irregulares (sombras, esquinas dobladas) no desplazan las esquinas.
        var contorno = new List<SKPoint>();
        for (var y = 0; y < h; y++)
        {
            var fila = Enumerable.Range(y * w, w).Where(p => etiquetas[p] == mayor).ToArray();
            if (fila.Length > 0) { contorno.Add(new(fila[0] % w, y)); contorno.Add(new(fila[^1] % w, y)); }
        }
        var envolvente = Envolvente(contorno);
        var m = envolvente.Count;
        if (m < 4) return null;
        double Triangulo(int a, int b, int c) => Math.Abs(
            (envolvente[b].X - envolvente[a].X) * (envolvente[c].Y - envolvente[a].Y) -
            (envolvente[b].Y - envolvente[a].Y) * (envolvente[c].X - envolvente[a].X)) / 2;
        var areaCuadrilatero = 0.0; int[] indices = [0, 1, 2, 3];
        for (var a = 0; a < m - 3; a++)
        for (var c = a + 2; c < m - 1; c++)
        {
            var b = Enumerable.Range(a + 1, c - a - 1).MaxBy(i => Triangulo(a, i, c));
            var d = Enumerable.Range(c + 1, m - c - 1).MaxBy(i => Triangulo(c, i, a));
            var area = Triangulo(a, b, c) + Triangulo(c, d, a);
            if (area > areaCuadrilatero) { areaCuadrilatero = area; indices = [a, b, c, d]; }
        }
        var esquinas = indices.Select(i => envolvente[i]).ToArray();
        if (AreaConSigno(esquinas) < 0) Array.Reverse(esquinas);
        var inicio = Array.IndexOf(esquinas, esquinas.MinBy(p => p.X + p.Y));
        esquinas = Enumerable.Range(0, 4).Select(i => esquinas[(inicio + i) % 4]).ToArray();
        var areaEnvolvente = Math.Abs(AreaConSigno(envolvente));
        var lados = Enumerable.Range(0, 4).Select(i => SKPoint.Distance(esquinas[i], esquinas[(i + 1) % 4])).ToArray();
        // El papel debe parecer un cuadrilátero: cuatro esquinas cubren casi toda la
        // envolvente y la región rellena ocupa casi toda ella (no una mano o un objeto).
        if (areaCuadrilatero < areaEnvolvente * .85 || areaPapel < areaEnvolvente * .85 ||
            lados.Min() < Math.Min(w, h) * .2) return null;
        var centro = new SKPoint(esquinas.Average(p => p.X), esquinas.Average(p => p.Y));
        int Valor(double x, double y) => valores[Math.Clamp((int)y, 0, h - 1) * w + Math.Clamp((int)x, 0, w - 1)];
        // Ajustar cada lado al borde real del papel (el umbral deja bordes irregulares en
        // zonas oscuras) y exigir que exista: si la sombra de un pliegue o de la mano parte
        // el papel, ese "lado" queda en mitad del papel y no se recorta.
        var lineas = new (SKPoint A, SKPoint B)[4];
        for (var i = 0; i < 4; i++)
        {
            var a = esquinas[i]; var b = esquinas[(i + 1) % 4];
            var medio = new SKPoint((a.X + b.X) / 2, (a.Y + b.Y) / 2);
            var largo = SKPoint.Distance(a, b);
            var (nx, ny) = (-(b.Y - a.Y) / largo, (b.X - a.X) / largo);
            if (nx * (centro.X - medio.X) + ny * (centro.Y - medio.Y) > 0) (nx, ny) = (-nx, -ny);
            // tolerante: dentro se toma el punto más claro cercano (papel entre letras pegadas al borde).
            IEnumerable<int> Saltos(int desplazamiento, bool tolerante) => Enumerable.Range(1, 19).Select(k =>
            {
                var x = a.X + (b.X - a.X) * k / 20.0 + nx * desplazamiento;
                var y = a.Y + (b.Y - a.Y) * k / 20.0 + ny * desplazamiento;
                var dentro = tolerante ? Enumerable.Range(2, 5).Max(d => Valor(x - nx * d, y - ny * d)) : Valor(x - nx * 2, y - ny * 2);
                return dentro - Valor(x + nx * 3, y + ny * 3);
            });
            // El borde del papel es la transición más exterior; las líneas de texto paralelas quedan dentro.
            var exterior = Enumerable.Range(-4, 29).LastOrDefault(d => Saltos(d, true).Count(s => s > 15) >= 14, int.MinValue);
            if (exterior == int.MinValue) return null;
            // La comparación tolerante rebasa el borde unos píxeles: afinar al salto más nítido.
            var ajuste = Enumerable.Range(exterior - 5, 6).MaxBy(d => Saltos(d, false).Sum());
            lineas[i] = (new(a.X + nx * ajuste, a.Y + ny * ajuste), new(b.X + nx * ajuste, b.Y + ny * ajuste));
        }
        esquinas = Enumerable.Range(0, 4).Select(i => Corte(lineas[(i + 3) % 4], lineas[i])).ToArray();
        centro = new SKPoint(esquinas.Average(p => p.X), esquinas.Average(p => p.Y));
        // Ampliar un poco hacia fuera para no cortar texto pegado al borde.
        return esquinas.Select(p => new SKPoint(
            (float)Math.Clamp((centro.X + (p.X - centro.X) * 1.02) / f, 0, imagen.Width - 1),
            (float)Math.Clamp((centro.Y + (p.Y - centro.Y) * 1.02) / f, 0, imagen.Height - 1))).ToArray();
    }

    private static IEnumerable<int> Vecinos(int p, int w, int h)
    {
        var x = p % w;
        if (x > 0) yield return p - 1;
        if (x < w - 1) yield return p + 1;
        if (p >= w) yield return p - w;
        if (p < w * (h - 1)) yield return p + w;
    }

    // Devuelve la media de cada clase con el umbral de Otsu (fondo oscuro, papel claro).
    private static (double Fondo, double Papel) Otsu(byte[] valores)
    {
        var histograma = new long[256];
        foreach (var v in valores) histograma[v]++;
        double total = valores.Length, suma = 0, sumaFondo = 0, pesoFondo = 0, mejor = -1;
        (double, double) medias = (0, 255);
        for (var i = 0; i < 256; i++) suma += i * histograma[i];
        for (var i = 0; i < 256; i++)
        {
            pesoFondo += histograma[i];
            if (pesoFondo == 0) continue;
            var pesoFrente = total - pesoFondo;
            if (pesoFrente == 0) break;
            sumaFondo += i * histograma[i];
            var (fondo, papel) = (sumaFondo / pesoFondo, (suma - sumaFondo) / pesoFrente);
            var entreClases = pesoFondo * pesoFrente * (papel - fondo) * (papel - fondo);
            if (entreClases > mejor) { mejor = entreClases; medias = (fondo, papel); }
        }
        return medias;
    }

    private static SKPoint Corte((SKPoint A, SKPoint B) l, (SKPoint A, SKPoint B) m)
    {
        double rx = l.B.X - l.A.X, ry = l.B.Y - l.A.Y, sx = m.B.X - m.A.X, sy = m.B.Y - m.A.Y;
        var t = ((m.A.X - l.A.X) * sy - (m.A.Y - l.A.Y) * sx) / (rx * sy - ry * sx);
        return new((float)(l.A.X + rx * t), (float)(l.A.Y + ry * t));
    }

    // Cadena monótona de Andrew.
    private static List<SKPoint> Envolvente(IEnumerable<SKPoint> puntos)
    {
        var p = puntos.Distinct().OrderBy(q => q.X).ThenBy(q => q.Y).ToArray();
        if (p.Length < 3) return [.. p];
        static double Giro(SKPoint o, SKPoint a, SKPoint b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var e = new SKPoint[p.Length * 2]; var k = 0;
        for (var i = 0; i < p.Length; i++)
        { while (k >= 2 && Giro(e[k - 2], e[k - 1], p[i]) <= 0) k--; e[k++] = p[i]; }
        for (int i = p.Length - 2, t = k + 1; i >= 0; i--)
        { while (k >= t && Giro(e[k - 2], e[k - 1], p[i]) <= 0) k--; e[k++] = p[i]; }
        return e.Take(k - 1).ToList();
    }

    // Positiva cuando los puntos van en sentido horario en la imagen (eje Y hacia abajo).
    private static double AreaConSigno(IReadOnlyList<SKPoint> p) => Enumerable.Range(0, p.Count).Sum(i =>
        (double)p[i].X * p[(i + 1) % p.Count].Y - (double)p[(i + 1) % p.Count].X * p[i].Y) / 2;

    // Lleva el cuadrilátero del papel a un rectángulo con una homografía completa
    // (corrige la perspectiva en ambos ejes, no sólo la lateral) y lo amplía si hace falta.
    public static SKBitmap Recortar(SKBitmap imagen, SKPoint[] esquinas, out double ampliacion)
    {
        var ancho = Math.Max(SKPoint.Distance(esquinas[0], esquinas[1]), SKPoint.Distance(esquinas[3], esquinas[2]));
        var alto = Math.Max(SKPoint.Distance(esquinas[0], esquinas[3]), SKPoint.Distance(esquinas[1], esquinas[2]));
        ampliacion = Math.Min(2.5, 3000.0 / Math.Max(ancho, alto));
        var w = Math.Max(1, (int)(ancho * ampliacion)); var h = Math.Max(1, (int)(alto * ampliacion));
        var salida = new SKBitmap(w, h);
        using var canvas = new SKCanvas(salida);
        canvas.Clear(SKColors.White);
        canvas.SetMatrix(Homografia(esquinas, [new(0, 0), new(w, 0), new(w, h), new(0, h)]));
        using var fuente = SKImage.FromBitmap(imagen);
        canvas.DrawImage(fuente, 0, 0, new SKSamplingOptions(SKCubicResampler.Mitchell));
        return salida;
    }

    private static SKMatrix Homografia(SKPoint[] origen, SKPoint[] destino)
    {
        // u = (h0·x + h1·y + h2) / (h6·x + h7·y + 1); v análogo con h3..h5.
        var a = new double[8, 9];
        for (var i = 0; i < 4; i++)
        {
            double x = origen[i].X, y = origen[i].Y, u = destino[i].X, v = destino[i].Y;
            a[2*i,0] = x; a[2*i,1] = y; a[2*i,2] = 1; a[2*i,6] = -u*x; a[2*i,7] = -u*y; a[2*i,8] = u;
            a[2*i+1,3] = x; a[2*i+1,4] = y; a[2*i+1,5] = 1; a[2*i+1,6] = -v*x; a[2*i+1,7] = -v*y; a[2*i+1,8] = v;
        }
        for (var c = 0; c < 8; c++)
        {
            var pivote = Enumerable.Range(c, 8 - c).MaxBy(f => Math.Abs(a[f, c]));
            for (var k = 0; k < 9; k++) (a[c, k], a[pivote, k]) = (a[pivote, k], a[c, k]);
            for (var f = 0; f < 8; f++)
            {
                if (f == c) continue;
                var m = a[f, c] / a[c, c];
                for (var k = c; k < 9; k++) a[f, k] -= m * a[c, k];
            }
        }
        float H(int i) => (float)(a[i, 8] / a[i, i]);
        return new SKMatrix(H(0), H(1), H(2), H(3), H(4), H(5), H(6), H(7), 1);
    }

    // Estima el brillo del papel sin tinta a baja resolución y divide por él:
    // elimina la sombra de pliegues y la luz desigual. variacion (p95 - p5 del fondo)
    // indica cuánto cambia la iluminación para decidir si merece la pena aplicarlo.
    public static SKBitmap NormalizarIluminacion(SKBitmap gris, out int variacion)
    {
        var f = Math.Min(1, 160.0 / Math.Max(gris.Width, gris.Height));
        using var mini = gris.Resize(new SKImageInfo(Math.Max(1, (int)(gris.Width * f)),
            Math.Max(1, (int)(gris.Height * f))), new SKSamplingOptions(SKFilterMode.Linear));
        var w = mini.Width; var h = mini.Height;
        var fondo = mini.Pixels.Select(p => p.Red).ToArray();
        // Máximo local repetido: las letras desaparecen y queda el papel.
        for (var paso = 0; paso < 3; paso++)
        {
            var previo = fondo; fondo = new byte[previo.Length];
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                byte maximo = 0;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                    maximo = Math.Max(maximo, previo[Math.Clamp(y + dy, 0, h - 1) * w + Math.Clamp(x + dx, 0, w - 1)]);
                fondo[y * w + x] = maximo;
            }
        }
        var ordenado = fondo.Order().ToArray();
        variacion = ordenado[ordenado.Length * 95 / 100] - ordenado[ordenado.Length * 5 / 100];
        using var fondoMini = new SKBitmap(w, h);
        fondoMini.Pixels = fondo.Select(v => new SKColor(v, v, v)).ToArray();
        using var suave = Desenfocar(fondoMini, 3);
        using var luz = suave.Resize(new SKImageInfo(gris.Width, gris.Height), new SKSamplingOptions(SKFilterMode.Linear));
        var entrada = gris.Pixels; var brillo = luz.Pixels; var destino = new SKColor[entrada.Length];
        for (var i = 0; i < entrada.Length; i++)
        {
            // El mínimo evita disparar el ruido en zonas casi negras (fondo, logos).
            var v = (byte)Math.Clamp(entrada[i].Red * 255.0 / Math.Max(60, (int)brillo[i].Red), 0, 255);
            destino[i] = new SKColor(v, v, v);
        }
        var salida = new SKBitmap(gris.Width, gris.Height); salida.Pixels = destino; return salida;
    }

    // Máscara de enfoque suave: las fotos lejanas llegan blandas tras ampliarlas.
    private static SKBitmap Enfocar(SKBitmap gris)
    {
        using var suave = Desenfocar(gris, 1.5f);
        var a = gris.Pixels; var b = suave.Pixels; var destino = new SKColor[a.Length];
        for (var i = 0; i < a.Length; i++)
        {
            var v = (byte)Math.Clamp(a[i].Red + .7 * (a[i].Red - b[i].Red), 0, 255);
            destino[i] = new SKColor(v, v, v);
        }
        var salida = new SKBitmap(gris.Width, gris.Height); salida.Pixels = destino; return salida;
    }

    private static SKBitmap Desenfocar(SKBitmap imagen, float sigma)
    {
        var salida = new SKBitmap(imagen.Width, imagen.Height);
        using var canvas = new SKCanvas(salida);
        using var pintura = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(sigma, sigma, SKShaderTileMode.Clamp) };
        canvas.Clear(SKColors.White);
        canvas.DrawBitmap(imagen, 0, 0, pintura);
        return salida;
    }

    public static double DetectarInclinacion(SKBitmap imagen)
    {
        var escala = Math.Min(1, 700.0 / imagen.Width);
        using var mini = imagen.Resize(new SKImageInfo(Math.Max(1, (int)(imagen.Width * escala)),
            Math.Max(1, (int)(imagen.Height * escala))), new SKSamplingOptions(SKFilterMode.Linear));
        var puntos = new List<(int X, int Y)>();
        using var binaria = UmbralAdaptativo(mini);
        var pixeles = binaria.Pixels;
        for (var y = mini.Height / 12; y < mini.Height * 11 / 12; y += 2)
        for (var x = mini.Width / 10; x < mini.Width * 9 / 10; x += 2)
            if (pixeles[y * mini.Width + x].Red < 110) puntos.Add((x, y));
        if (puntos.Count < 30) return 0;
        double Puntuar(double grados)
        {
            var pendiente = Math.Tan(grados * Math.PI / 180);
            var filas = new int[mini.Height + mini.Width];
            foreach (var (x, y) in puntos)
            {
                var fila = (int)Math.Round(y - pendiente * x) + mini.Width / 2;
                if (fila >= 0 && fila < filas.Length) filas[fila]++;
            }
            return filas.Sum(n => (double)n * n);
        }
        var baseScore = Puntuar(0); var mejor = baseScore; double angulo = 0;
        for (var a = -8.0; a <= 8; a += .25)
        { var score = Puntuar(a); if (score > mejor) { mejor = score; angulo = a; } }
        // El margen evita girar documentos con ruido o sin líneas de texto claras.
        return mejor > baseScore * 1.02 ? angulo : 0;
    }

    public static SKBitmap Girar(SKBitmap imagen, double grados)
    {
        var radianes = grados * Math.PI / 180;
        var ancho = (int)Math.Ceiling(imagen.Width*Math.Abs(Math.Cos(radianes))+imagen.Height*Math.Abs(Math.Sin(radianes)));
        var alto = (int)Math.Ceiling(imagen.Height*Math.Abs(Math.Cos(radianes))+imagen.Width*Math.Abs(Math.Sin(radianes)));
        var salida = new SKBitmap(ancho, alto);
        using var canvas = new SKCanvas(salida);
        canvas.Clear(SKColors.White);
        canvas.Translate(ancho/2f,alto/2f);
        canvas.RotateDegrees((float)grados);
        canvas.DrawBitmap(imagen, -imagen.Width/2f, -imagen.Height/2f);
        return salida;
    }

    // Detecta bordes laterales del papel con contraste hacia dentro. Si los bordes
    // no son estables no deforma la foto; no se intenta adivinar esquinas ocultas.
    public static SKBitmap Rectificar(SKBitmap imagen)
    {
        using var mini = imagen.Resize(new SKImageInfo(400, Math.Max(1, imagen.Height * 400 / imagen.Width)),
            new SKSamplingOptions(SKFilterMode.Linear));
        var px = mini.Pixels; var w = mini.Width; var h = mini.Height;
        (double A, double B)? Borde(bool izquierdo)
        {
            var puntos = new List<(double Y, double X)>();
            for (var y = h / 10; y < h * 9 / 10; y += Math.Max(1, h / 70))
            {
                double mejor = 22; var posicion = -1;
                for (var x = izquierdo ? 4 : w * 2 / 3; x < (izquierdo ? w / 3 : w - 4); x++)
                {
                    double fuera = 0, dentro = 0;
                    for (var d = 2; d <= 4; d++)
                    { fuera += px[y*w + x + (izquierdo ? -d : d)].Red; dentro += px[y*w + x + (izquierdo ? d : -d)].Red; }
                    var contraste = (dentro - fuera) / 3;
                    if (contraste > mejor && dentro / 3 > 150) { mejor = contraste; posicion = x; }
                }
                if (posicion >= 0) puntos.Add((y, posicion));
            }
            if (puntos.Count < 30) return null;
            // RANSAC acotado: letras y sombras no deben convertirse en bordes del papel.
            var mejores = Array.Empty<(double Y, double X)>();
            for (var i = 0; i < puntos.Count; i += 3)
            for (var j = i + 10; j < puntos.Count; j += 3)
            {
                var a = (puntos[j].X - puntos[i].X) / (puntos[j].Y - puntos[i].Y);
                if (Math.Abs(a) > .3) continue;
                var b = puntos[i].X - a * puntos[i].Y;
                var consenso = puntos.Where(p => Math.Abs(p.X - a*p.Y - b) < 3).ToArray();
                if (consenso.Length > mejores.Length) mejores = consenso;
            }
            if (mejores.Length < 30 || mejores.Length < puntos.Count * .75 ||
                mejores.Max(p => p.Y) - mejores.Min(p => p.Y) < h * .6) return null;
            var my = mejores.Average(p => p.Y); var mx = mejores.Average(p => p.X);
            var pendiente = mejores.Sum(p => (p.Y-my)*(p.X-mx)) / mejores.Sum(p => (p.Y-my)*(p.Y-my));
            return (pendiente, mx - pendiente*my);
        }
        var izq = Borde(true); var der = Borde(false);
        if (izq is null || der is null) return imagen.Copy();
        var l0 = izq.Value.B; var l1 = izq.Value.A*(h-1) + l0;
        var r0 = der.Value.B; var r1 = der.Value.A*(h-1) + r0;
        if (Math.Min(r0-l0, r1-l1) < w*.5 || l0 < 0 || l1 < 0 || r0 >= w || r1 >= w ||
            Math.Abs((r1-l1)-(r0-l0)) < w*.025) return imagen.Copy();
        // Homografía de un trapecio de lados rectos a un rectángulo.
        var factor = (r0-l0)/(r1-l1);
        var entrada = imagen.Pixels; var salida = new SKBitmap(imagen.Width, imagen.Height);
        var destino = new SKColor[entrada.Length];
        for (var y = 0; y < imagen.Height; y++)
        {
            var v = (double)y/(imagen.Height-1);
            var t = factor*v/(1+(factor-1)*v);
            var izquierda = (l0+(l1-l0)*t)/w*imagen.Width;
            var derecha = (r0+(r1-r0)*t)/w*imagen.Width;
            for (var x = 0; x < imagen.Width; x++)
                destino[y*imagen.Width+x] = Muestrear(entrada, imagen.Width, imagen.Height,
                    izquierda+(derecha-izquierda)*x/(imagen.Width-1), t*(imagen.Height-1));
        }
        salida.Pixels = destino; return salida;
    }

    private static SKColor Muestrear(SKColor[] pixeles, int w, int h, double x, double y)
    {
        var ix = Math.Clamp((int)x, 0, w-1); var iy = Math.Clamp((int)y, 0, h-1);
        var dx = x-ix; var dy = y-iy;
        var valor = pixeles[iy*w+ix].Red*(1-dx)*(1-dy) + pixeles[iy*w+Math.Min(w-1,ix+1)].Red*dx*(1-dy)
            + pixeles[Math.Min(h-1,iy+1)*w+ix].Red*(1-dx)*dy
            + pixeles[Math.Min(h-1,iy+1)*w+Math.Min(w-1,ix+1)].Red*dx*dy;
        var gris = (byte)Math.Clamp(valor, 0, 255); return new(gris, gris, gris);
    }

    public static SKBitmap UmbralAdaptativo(SKBitmap imagen)
    {
        var w = imagen.Width; var h = imagen.Height; var entrada = imagen.Pixels;
        var integral = new long[(w+1)*(h+1)];
        for (var y = 0; y < h; y++)
        { long suma = 0; for (var x = 0; x < w; x++) { suma += entrada[y*w+x].Red; integral[(y+1)*(w+1)+x+1] = integral[y*(w+1)+x+1]+suma; } }
        var radio = Math.Max(12, w/40); var destino = new SKColor[entrada.Length];
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var x0 = Math.Max(0,x-radio); var x1 = Math.Min(w,x+radio+1);
            var y0 = Math.Max(0,y-radio); var y1 = Math.Min(h,y+radio+1);
            var media = (double)(integral[y1*(w+1)+x1]-integral[y0*(w+1)+x1]-integral[y1*(w+1)+x0]+integral[y0*(w+1)+x0])/((x1-x0)*(y1-y0));
            destino[y*w+x] = entrada[y*w+x].Red < media-12 ? SKColors.Black : SKColors.White;
        }
        var salida = new SKBitmap(w,h); salida.Pixels = destino; return salida;
    }

    private static bool MismosPixeles(SKBitmap a, SKBitmap b) => a.Bytes.AsSpan().SequenceEqual(b.Bytes);
}
