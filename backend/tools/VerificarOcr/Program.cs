using System.Diagnostics;
using System.Text.Json;
using Gestio.Api.Services;
using Microsoft.Extensions.Configuration;

// Manifiesto privado: [{"Archivo":"ruta absoluta", "TipoMime":"image/jpeg",
// "Esperado":{"Total":123.45,"EmisorNif":null}}]. Los valores esperados sólo
// se comparan DESPUÉS del OCR; nunca se entregan al lector ni al extractor.
if (args.Length < 2) { Console.Error.WriteLine("Uso: VerificarOcr manifiesto.json carpetaResultados [--renombrar]"); return 2; }
var carpeta = Path.GetFullPath(args[1]); Directory.CreateDirectory(carpeta);
using var manifiesto = JsonDocument.Parse(await File.ReadAllTextAsync(args[0]));
var configuracion = new ConfigurationBuilder().AddEnvironmentVariables().Build();
using var motorLatin = new MotorOcrLatin(configuracion);
var lector = new LectorDocumentoLocal(configuracion, motorLatin);
var errores = 0;
foreach (var caso in manifiesto.RootElement.EnumerateArray())
{
    var archivo = Path.GetFullPath(caso.GetProperty("Archivo").GetString()!);
    var nombre = Path.GetFileName(archivo); var reloj = Stopwatch.StartNew();
    try
    {
        await using var stream = File.OpenRead(archivo);
        var texto = await lector.LeerAsync(stream, caso.GetProperty("TipoMime").GetString()!, CancellationToken.None);
        var e = ExtractorCampos.Extraer(texto, Guid.NewGuid());
        var campos = JsonSerializer.SerializeToElement(new { e.EmisorNombre, e.EmisorNif, e.NumeroFactura,
            e.FechaEmision, e.BaseImponible, e.TipoIva, e.CuotaIva, e.Total, e.DesglosesIva, e.Advertencias });
        var fallos = new List<string>();
        foreach (var esperado in caso.GetProperty("Esperado").EnumerateObject())
            if (!campos.TryGetProperty(esperado.Name, out var actual) || !JsonElement.DeepEquals(esperado.Value, actual))
                fallos.Add($"{esperado.Name}: esperado {esperado.Value.GetRawText()}, obtenido {(actual.ValueKind == JsonValueKind.Undefined ? "campo desconocido" : actual.GetRawText())}");
        if (args.Contains("--renombrar"))
        {
            var copia = Path.Combine(carpeta, Guid.NewGuid().ToString("N")+Path.GetExtension(archivo));
            try
            {
                File.Copy(archivo, copia);
                await using var renombrado = File.OpenRead(copia);
                var otro = ExtractorCampos.Extraer(await lector.LeerAsync(renombrado,
                    caso.GetProperty("TipoMime").GetString()!, CancellationToken.None), e.DocumentoId);
                var valoresOtro = JsonSerializer.SerializeToElement(new { otro.EmisorNombre, otro.EmisorNif, otro.NumeroFactura,
                    otro.FechaEmision, otro.BaseImponible, otro.TipoIva, otro.CuotaIva, otro.Total,
                    otro.DesglosesIva, otro.Advertencias });
                if (!JsonElement.DeepEquals(campos, valoresOtro)) fallos.Add("Renombrar cambió los datos extraídos.");
            }
            finally { File.Delete(copia); }
        }
        var informe = new { Archivo = nombre, Segundos = reloj.Elapsed.TotalSeconds, Campos = campos, Fallos = fallos, Texto = texto };
        await File.WriteAllTextAsync(Path.Combine(carpeta, nombre+".json"), JsonSerializer.Serialize(informe, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(new { Archivo = nombre, Campos = campos, Fallos = fallos, Segundos = reloj.Elapsed.TotalSeconds }));
        errores += fallos.Count;
    }
    catch (Exception ex) { errores++; Console.Error.WriteLine($"{nombre}: {ex.Message}"); }
}
return errores == 0 ? 0 : 1;
