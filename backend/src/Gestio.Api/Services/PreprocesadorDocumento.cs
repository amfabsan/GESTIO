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
        using var perspectiva = Rectificar(gris);
        var angulo = DetectarInclinacion(perspectiva);
        using var corregida = Girar(perspectiva, -angulo);
        var geometria = "corregida";
        if (Math.Abs(angulo) >= .25 || !MismosPixeles(gris, perspectiva))
            resultado.Add(Guardar(corregida, archivo, "rectificada", geometria));
        else geometria = "original";
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
