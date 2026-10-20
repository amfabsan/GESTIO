using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Interfaces;

namespace Gestio.Api.Services;

// Reglas sobre texto leído: no dependen del archivo, de sus metadatos ni de valores esperados.
public static partial class ExtractorCampos
{
    private const string Dinero = @"-?\d+(?:[. ]\d{3})*,\d{2}|-?\d+\.\d{2}|-?\d+";
    private const string Fecha = @"\b(?:\d{4}[-/]\d{1,2}[-/]\d{1,2}|\d{1,2}[/.-]\d{1,2}[/.-](?:\d{4}|\d{2}))\b";
    // 04/Mar/2017, 4-MAR-17, 4 de marzo de 2017 (abreviaturas en español e inglés).
    private const string Meses = "ene|feb|mar|abr|may|jun|jul|ago|sep|set|oct|nov|dic|jan|apr|aug|dec";
    private const string FechaTexto = @"\b\d{1,2}(?:\s*[/.\-]\s*|\s+de\s+|\s+)(?:" + Meses + @")[a-z]*\.?(?:\s*[/.\-]\s*|\s+de\s+|\s+)(?:\d{4}|\d{2})\b";
    private const string Direccion = @"^(?:c/|cl[. ]|calle|avda?\b|avenida|plaza|pza\b|paseo|ctra\b)|\b\d{5}\b|\b(?:tel\.?|telf\.?|telefono)\b|^\+?(?:\d[ .]?){9,12}$";
    // Tipos de IVA, IGIC e IPSI vigentes: sirven para reconocer una fila fiscal, nunca para calcularla.
    private static readonly decimal[] TiposConocidos = [2m, 3m, 4m, 5m, 7m, 7.5m, 8m, 9.5m, 10m, 13.5m, 15m, 20m, 21m];
    private static Match Buscar(string texto, string patron) => Regex.Match(texto, patron, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static string Normalizar(string texto) => new(texto.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    private static T? Unico<T>(IEnumerable<T> valores) where T : struct =>
        valores.Distinct().Take(2).ToArray() is [var valor] ? valor : null;
    private static string? Unico(IEnumerable<string> valores) => valores.Select(v => v.Trim()).Where(v => v.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray() is [var valor] ? valor : null;

    public static int ContarDatos(ExtraccionIa e) => new object?[] { e.EmisorNombre, e.EmisorNif, e.NumeroFactura,
        e.FechaEmision, e.BaseImponible, e.TipoIva, e.CuotaIva, e.Total }.Count(v => v is not null) + e.DesglosesIva.Count;

    public static ExtraccionIa Extraer(string texto, Guid documentoId)
    {
        var originales = texto.Replace('\u00a0', ' ').Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
        var lineas = originales.Select(Normalizar).ToArray();
        var emisor = originales.TakeWhile(l => !Buscar(Normalizar(l),
            @"\b(?:cliente|receptor|destinatario|facturar a)\s*[:\-]|^(?:datos (?:del? )?cliente|cliente|receptor|destinatario)\b").Success).ToArray();

        IEnumerable<decimal> Importes(string etiquetas, bool quitarPorcentaje = false)
        {
            for (var i = 0; i < lineas.Length; i++)
            {
                var m = Buscar(lineas[i], @"^(?:" + etiquetas + @")\b\s*[:=€]?\s*(.*)$");
                if (!m.Success) continue;
                var resto = m.Groups[1].Value.Trim();
                if (quitarPorcentaje) resto = Regex.Replace(resto, @"\(?\d+(?:[.,]\d+)?\s*%\)?\s*[:=]?", "");
                if (resto.Length == 0 && i + 1 < lineas.Length) resto = lineas[i + 1];
                var cantidad = Buscar(resto, @"^[.\s:_=€]*(?:-\s+)*(" + Dinero + @")\s*(?:€|EUR)?(?:\s|$|[.;])");
                if (cantidad.Success && ConvertirImporte(cantidad.Groups[1].Value) is { } valor) yield return valor;
            }
        }

        var nombresExplicitos = emisor.Select(l => Buscar(l, @"^(?:emisor|raz[oó]n social|proveedor|empresa)\s*:\s*(.{2,200})$"))
            .Where(m => m.Success).Select(m => m.Groups[1].Value).ToArray();
        var nombre = Unico(nombresExplicitos);
        if (nombre is null && nombresExplicitos.Length == 0)
        {
            // Sólo nombres legales o contiguos al NIF, antes del bloque de cliente.
            var candidatos = new List<string>();
            for (var i = 0; i < emisor.Length; i++)
            {
                var l = emisor[i];
                if (Buscar(Normalizar(l), @"\b(?:S\.?\s*L\.?|S\.?\s*A\.?)\s*$").Success && EsNombre(l)) candidatos.Add(l);
                if (Buscar(l, @"\b(?:N\.?I\.?F\.?|C\.?I\.?F\.?)\s*:").Success && i > 0 && EsNombre(emisor[i - 1]))
                    candidatos.Add(emisor[i - 1]);
            }
            nombre = Unico(candidatos);
            if (nombre is null && emisor.Length > 0 && EsNombre(emisor[0]) &&
                emisor[0].Count(char.IsUpper) >= emisor[0].Count(char.IsLetter) * .8 && emisor.Take(6).Any(l =>
                Buscar(Normalizar(l), @"\b(?:nif|cif|telefono|tel\.?|calle|avenida|restaurante)\b|^(?:c/|cl[. ])").Success))
                nombre = emisor[0];
            // Nombre comercial sin etiqueta y no en la primera línea (QR, logotipo o rótulos
            // encima): la línea en mayúsculas más cercana por encima de la primera dirección,
            // código postal o teléfono. Lo de más arriba suele ser ruido del QR o del logotipo;
            // "palabra," al final es una dirección que continúa en la línea siguiente.
            // Tres letras iguales seguidas son filas de asteriscos o guiones mal leídas.
            var primeraDireccion = Array.FindIndex(emisor.Take(15).ToArray(), l => Buscar(Normalizar(l), Direccion).Success);
            nombre ??= primeraDireccion <= 0 ? null : emisor[Math.Max(0, primeraDireccion - 3)..primeraDireccion].Reverse()
                .Where(l => !Regex.IsMatch(l, @"\p{L},\s*$"))
                .Select(l => Regex.Replace(l, @"[\s,;:\-]+$|\s+\.$", ""))
                .FirstOrDefault(l => EsNombre(l) && !l.Any(char.IsLower) && l.Count(char.IsLetter) >= 5 &&
                    !Regex.IsMatch(l, @"[^\p{L}\s.,&'\-]|(\p{L})\1\1"));
        }

        var nifs = emisor.SelectMany(l => Regex.Matches(l,
            @"\b(?:N\.?I\.?F\.?|C\.?I\.?F\.?|NIE)(?:\s+(?:del?\s+)?emisor)?[\s:.\-]*([A-Z][\s.\-]*\d(?:[ .-]?\d){6}\s*[A-Z0-9]|\d(?:[ .-]?\d){7}\s*[A-Z])\b", RegexOptions.IgnoreCase))
            .Select(m => Regex.Replace(m.Groups[1].Value, @"[ .-]", "").ToUpperInvariant()).ToList();
        if (nifs.Count == 0)
            nifs.AddRange(emisor.Take(14).Where(l => Buscar(l, @"^[A-Z0-9]{9}$").Success && NifValido(l.ToUpperInvariant()))
                .Select(l => l.ToUpperInvariant()));
        var numeros = lineas.Select(l => Buscar(l,
            @"\b(?:n(?:umero|[º°o.]|ro\.)\s*(?:de\s*)?(?:factura|ticket)|(?:factura(?: simplificada)?|fac\.?\s*simplif\.?|ticket|tiquet)(?:\s*(?:n(?:umero|[º°o.]|ro\.)|num\.))?)\s*[:#-]?\s*([A-Z0-9][A-Z0-9/.-]*)\b"))
            .Where(m => m.Success && m.Groups[1].Value.Any(char.IsDigit)).Select(m => m.Groups[1].Value);

        var fechasExplicitas = new List<DateOnly>();
        var fechasSinEtiqueta = new List<DateOnly>();
        foreach (var l in lineas)
        {
            if (Buscar(l, @"\b(?:vencimiento|pago|pedido|entrega)\s*:").Success &&
                !Buscar(l, @"fecha (?:de )?(?:emision|factura)").Success) continue;
            var explicita = Buscar(l, @"fecha(?:\s+(?:de\s+)?(?:emision|factura|expedicion))?\s*[:. -]*\s*(" + Fecha + "|" + FechaTexto + ")");
            if (explicita.Success && LeerFecha(explicita.Groups[1].Value) is { } fecha) fechasExplicitas.Add(fecha);
            else foreach (Match m in Regex.Matches(l, Fecha + "|" + FechaTexto, RegexOptions.IgnoreCase))
                if (LeerFecha(m.Value) is { } f) fechasSinEtiqueta.Add(f);
        }

        decimal? baseTabla = null, tipoTabla = null, cuotaTabla = null;
        var tablas = new List<(decimal Base, decimal Tipo, decimal Cuota)>();
        var variasFilas = false;
        for (var i = 0; i + 1 < lineas.Length; i++)
        {
            var cabecera = lineas[i];
            var b = Buscar(cabecera, @"\bbase\b");
            var t = Buscar(cabecera, @"%|\btipo\b|\bx\s*iva\b");
            var c = Buscar(cabecera, @"cuota|imp(?:orte)?\.?\s*(?:iva|i\.?v\.?a\.?)");
            if (!b.Success || !t.Success || !c.Success) continue;
            var posiciones = new[] { (Campo: "base", Pos: b.Index), (Campo: "tipo", Pos: t.Index),
                (Campo: "cuota", Pos: c.Index) }.OrderBy(p => p.Pos).ToArray();
            var filas = new List<decimal[]>();
            for (var j = i + 1; j < Math.Min(i + 6, lineas.Length); j++)
            {
                if (Regex.IsMatch(lineas[j], @"^[\s\-_=|.]+$")) continue;
                var valores = Regex.Matches(lineas[j], @"(?<!\w)(?:" + Dinero + @")(?!\w)")
                    .Select(m => ConvertirImporte(m.Value)).ToArray();
                if (valores.Length != 3 || valores.Any(v => v is null) || Regex.IsMatch(lineas[j], @"[A-Za-z]")) break;
                filas.Add(valores.Select(v => v!.Value).ToArray());
            }
            // No sumar varios tipos de IVA ni elegir una fila arbitraria para un modelo escalar.
            if (filas.Count != 1) { variasFilas |= filas.Count > 1; continue; }
            decimal baseFila = 0, tipoFila = 0, cuotaFila = 0;
            for (var col = 0; col < 3; col++)
                switch (posiciones[col].Campo)
                {
                    case "base": baseFila = filas[0][col]; break;
                    case "tipo": tipoFila = filas[0][col]; break;
                    case "cuota": cuotaFila = filas[0][col]; break;
                }
            tablas.Add((baseFila, tipoFila, cuotaFila));
        }
        if (!variasFilas && tablas.Distinct().ToArray() is [var tabla])
            (baseTabla, tipoTabla, cuotaTabla) = (tabla.Base, tabla.Tipo, tabla.Cuota);
        // Cabecera fiscal ilegible (letra pequeña) con cifras legibles: la fila se reconoce
        // por su coherencia interna exacta (base × tipo = cuota redondeada o truncada, base +
        // cuota = total). No se calcula ninguna cifra: sólo se identifica qué es cada una de
        // las leídas. Sin tolerancia, un dígito mal leído no pasa la prueba; si sólo falla la
        // cuota, base × (1 + tipo) = total sigue identificando base, tipo y total.
        var coherentes = new List<(decimal Base, decimal Tipo, decimal? Cuota, decimal? Total)>();
        foreach (var l in lineas)
        {
            var cifras = Regex.Matches(l, @"(?<![\w.,])(?:" + Dinero + @")(?!\w)");
            var n = cifras.Select(m => ConvertirImporte(m.Value)).ToArray();
            for (var k = 0; k + 2 < n.Length; k++)
            {
                if (n[k] is not { } b || n[k + 1] is not { } t || b <= 0 || !TiposConocidos.Contains(t) ||
                    !Regex.IsMatch(cifras[k].Value, @"[.,]\d{2}$")) continue;
                var producto = b * t / 100;
                var esperadas = new[] { decimal.Round(producto, 2, MidpointRounding.AwayFromZero), decimal.Truncate(producto * 100) / 100 };
                bool EsTotal(decimal? x) => x is { } v && esperadas.Any(c => b + c == v);
                var siguiente = k + 3 < n.Length ? n[k + 3] : null;
                t /= 1.0000000000m; // "10.00" se guarda como 10, igual que "10%"
                // Si la base se obtuvo dividiendo el total, la cuota es la diferencia y puede
                // separarse un céntimo del producto: sólo se acepta si el total lo confirma.
                if (n[k + 2] is { } c && (esperadas.Contains(c) || siguiente == b + c && Math.Abs(c - producto) < .01m))
                    coherentes.Add((b, t, c, siguiente == b + c ? siguiente : null));
                else if (EsTotal(n[k + 2])) coherentes.Add((b, t, null, n[k + 2]));
                else if (EsTotal(siguiente)) coherentes.Add((b, t, null, siguiente)); // cuota ilegible entre medias
            }
        }
        (decimal Base, decimal Tipo, decimal? Cuota, decimal? Total)? coherente = coherentes.Distinct().ToArray() is [var fila] ? fila : null;
        var tipos = lineas.SelectMany(l => Regex.Matches(l,
            @"\b(?:tipo\s+(?:de\s+)?iva|iva|tva)\s*[:(]?\s*(\d{1,2}(?:[.,]\d{1,2})?)\s*%", RegexOptions.IgnoreCase))
            .Select(m => ConvertirImporte(m.Groups[1].Value)).Where(v => v is not null).Select(v => v!.Value).ToArray();
        var totales = Importes(@"total(?:\s+(?:euros|eur|factura|a pagar|con iva|iva (?:inc\.?|incl(?:uido|uida|\.)?)|general))?|tot\.?|importe total|a pagar").ToArray();
        var bases = Importes(@"base imponible|total base imponible|subtotal").ToArray();
        var cuotas = Importes(@"cuota (?:de )?iva|importe (?:de )?iva|total iva|^iva", true).ToArray();
        var resultado = new ExtraccionIa
        {
            Id = Guid.NewGuid(), DocumentoId = documentoId, EmisorNombre = nombre, EmisorNif = Unico(nifs),
            NumeroFactura = Unico(numeros),
            FechaEmision = fechasExplicitas.Count > 0 ? Unico(fechasExplicitas) : Unico(fechasSinEtiqueta),
            BaseImponible = bases.Length > 0 ? Unico(bases) : baseTabla ?? coherente?.Base,
            TipoIva = tipos.Length > 0 ? Unico(tipos) : tipoTabla ?? coherente?.Tipo,
            CuotaIva = cuotas.Length > 0 ? Unico(cuotas) : cuotaTabla ?? coherente?.Cuota,
            Total = totales.Length > 0 ? Unico(totales) : coherente?.Total ?? (lineas.Any(l => Buscar(l, @"\bTO\s*TAL\b").Success)
                ? null : Unico(Importes(@"total pendiente|pendiente de cobro")))
        };
        resultado.DesglosesIva = LeerDesgloses(lineas);
        if (resultado.DesglosesIva.Count > 1) resultado.TipoIva = resultado.CuotaIva = null;
        Comprobar(resultado);
        return resultado;
    }

    private static bool EsNombre(string l) => l.Length is >= 3 and <= 200 && l.Count(char.IsLetter) >= 3 &&
        !l.Any(char.IsDigit) && !Buscar(Normalizar(l),
            @"\b(?:factura|ticket|documento|gracias|cliente|base|total|producto|descripcion|fecha|nif|cif|telefono|www|http|iva|calle|avenida|espana|spain)\b|^(?:c[/il.)]+\s|cl\b|av\b)").Success;

    private static decimal? ConvertirImporte(string valor)
    {
        var limpio = valor.Replace(" ", "");
        if (limpio.Contains(',')) limpio = limpio.Replace(".", "").Replace(',', '.');
        return decimal.TryParse(limpio, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out var d) && Math.Abs(d) < 10000000000000000m ? d : null;
    }

    private static DateOnly? LeerFecha(string valor) =>
        Regex.IsMatch(valor, @"^[\d/.\-]+$") ? ConvertirFecha(valor) : ConvertirFechaTexto(valor);

    private static DateOnly? ConvertirFechaTexto(string valor)
    {
        var m = Buscar(Normalizar(valor), @"(\d{1,2})\D+?(" + Meses + @")[a-z]*\.?\D+?(\d{4}|\d{2})\b");
        if (!m.Success) return null;
        var mes = m.Groups[2].Value.ToLowerInvariant() switch
        {
            "ene" or "jan" => 1, "feb" => 2, "mar" => 3, "abr" or "apr" => 4, "may" => 5, "jun" => 6,
            "jul" => 7, "ago" or "aug" => 8, "sep" or "set" => 9, "oct" => 10, "nov" => 11, _ => 12
        };
        var anio = int.Parse(m.Groups[3].Value);
        if (anio < 100) anio += anio < 70 ? 2000 : 1900;
        try { return new DateOnly(anio, mes, int.Parse(m.Groups[1].Value)); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static DateOnly? ConvertirFecha(string valor)
    {
        var partes = Regex.Split(valor, @"[/.-]").Select(int.Parse).ToArray();
        if (partes.Length != 3) return null;
        var iso = partes[0] > 31;
        var anio = iso ? partes[0] : partes[2];
        if (anio < 100) anio += anio < 70 ? 2000 : 1900;
        try { return new DateOnly(anio, partes[1], iso ? partes[2] : partes[0]); }
        catch (ArgumentOutOfRangeException) { return null; }
    }
}

