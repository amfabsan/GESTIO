using Gestio.Api.Domain.Interfaces;
using SessionOptions = Microsoft.ML.OnnxRuntime.SessionOptions;
using RapidOcrNet;
using SkiaSharp;

namespace Gestio.Api.Services;

// Complemento local de Tesseract: PP-OCRv5 latino, CPU y modelos del paquete.
// Una sesión compartida evita cargar modelos para cada página o petición.
public sealed class MotorOcrLatin(IConfiguration configuration) : IDisposable
{
    private readonly SemaphoreSlim acceso = new(1, 1);
    private RapidOcr? motor;

    public async Task<LecturaDocumento> LeerAsync(string ruta, int pagina, string variante,
        string geometria, CancellationToken ct)
    {
        await acceso.WaitAsync(ct);
        try
        {
            if (motor is null)
            {
                var carpeta = configuration["Ocr:ModelosLatin"] ?? Path.Combine(AppContext.BaseDirectory, "models", "v5");
                var nuevo = new RapidOcr();
                try
                {
                    using var opciones = new SessionOptions { IntraOpNumThreads = 2, InterOpNumThreads = 1 };
                    nuevo.InitModels(Path.Combine(carpeta, "ch_PP-OCRv5_mobile_det.onnx"),
                        Path.Combine(carpeta, "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx"),
                        Path.Combine(carpeta, "latin_PP-OCRv5_rec_mobile_infer.onnx"),
                        Path.Combine(carpeta, "ppocrv5_latin_dict.txt"), opciones);
                    motor = nuevo;
                }
                catch { nuevo.Dispose(); throw; }
            }
            using var imagen = SKBitmap.Decode(ruta) ?? throw new InvalidOperationException("No se puede leer la imagen para OCR latino.");
            var resultado = await motor.DetectAsync(imagen, RapidOcrOptions.PythonCompat with
                { ReturnWordBox = true, DoAngle = false }, null, ct);
            var palabras = new List<PalabraDocumento>();
            var linea = 0;
            foreach (var bloque in resultado.TextBlocks)
            {
            foreach (var palabra in bloque.WordResults ?? [])
            {
                var puntos = palabra.BoxPoints;
                var x = puntos.Min(p => p.X); var y = puntos.Min(p => p.Y);
                // El motor proporciona 0..1; se conserva su puntuación en la escala
                // 0..100 usada por las lecturas. No es una confianza de campo.
                palabras.Add(new(palabra.Text, (double)x / imagen.Width, (double)y / imagen.Height,
                    (double)(puntos.Max(p => p.X) - x) / imagen.Width, (double)(puntos.Max(p => p.Y) - y) / imagen.Height,
                    palabra.Score * 100d, linea));
            }
            linea++;
            }
            return new(pagina, "pagina", "ppocrv5-latin/" + variante, false, palabras, Geometria: geometria);
        }
        finally { acceso.Release(); }
    }

    public void Dispose() { motor?.Dispose(); acceso.Dispose(); }
}
