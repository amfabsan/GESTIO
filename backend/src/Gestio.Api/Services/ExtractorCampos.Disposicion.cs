using System.Text.RegularExpressions;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Interfaces;

namespace Gestio.Api.Services;

public static partial class ExtractorCampos
{
    private sealed record Candidato(string Campo, object Valor, LecturaDocumento Lectura, double? Confianza);

    public static ExtraccionIa Extraer(TextoDocumento documento, Guid documentoId)
    {
        var resultado = Extraer(documento.Texto, documentoId);
        if (documento.Lecturas is not { Count: > 0 }) return resultado;
        var candidatos = new List<Candidato>();
        var cabeceras = new List<Candidato>();
        var impuestos = new List<(IReadOnlyList<DesgloseIva> Filas, LecturaDocumento Lectura, double Confianza)>();
        foreach (var pagina in documento.Lecturas.GroupBy(l => (l.Pagina, l.Geometria)))
        {
            var principal = pagina.Where(l => !l.EsRegion).MaxBy(l => l.Palabras.Sum(p =>
                Math.Max(0, (p.Confianza ?? 0) - 40) * p.Texto.Length));
            if (principal is null) continue;
            foreach (var lectura in pagina)
            {
                var palabras = lectura.EsRegion
                    ? principal.Palabras.Where(p => !Dentro(p, lectura.Region)).Concat(lectura.Palabras).ToArray()
                    : lectura.Palabras;
                var extraccion = Extraer(DisposicionDocumento.Texto(palabras), documentoId);
                var palabrasEmisor = SepararEmisor(palabras);
                var emisor = Extraer(string.Join("\n", LineasDeOrigen(palabrasEmisor).Select(l => l.Texto)), documentoId);
                extraccion.EmisorNombre = emisor.EmisorNombre;
                extraccion.EmisorNif = emisor.EmisorNif;
                if (!lectura.EsRegion && emisor.EmisorNif is not null)
                {
                    var primera = LineasDeOrigen(palabrasEmisor).FirstOrDefault();
                    if (primera is not null && primera.Y < .22 && EsNombre(primera.Texto) &&
                        primera.Palabras.Count(p => p.Texto.Count(char.IsLetter) >= 2) >= 2)
                    {
                        var confianza = primera.Palabras.Min(p => p.Confianza);
                        if (lectura.TextoNativo || confianza >= 85)
                            cabeceras.Add(new(nameof(ExtraccionIa.EmisorNombre), primera.Texto, lectura, confianza));
                    }
                }
                var soportes = new Dictionary<string, IReadOnlyList<PalabraDocumento>>();
                foreach (var (campo, valor) in Valores(extraccion).Where(v => v.Value is not null))
                    soportes[campo] = BuscarSoporte(campo, valor!,
                        campo is nameof(ExtraccionIa.EmisorNombre) or nameof(ExtraccionIa.EmisorNif) ? palabrasEmisor : palabras);
                LeerPosiciones(extraccion, palabras, soportes);
                if (extraccion.DesglosesIva.Count > 0)
                {
                    var valores = extraccion.DesglosesIva.SelectMany(f => new[] { f.BaseImponible, f.TipoIva, f.CuotaIva })
                        .Where(v => v.HasValue).Select(v => v!.Value).ToArray();
                    var soporte = palabras.Where(p => Numero(p) is { } n && valores.Contains(n)).ToArray();
                    var confianza = soporte.Length > 0 ? soporte.Min(p => p.Confianza ?? (lectura.TextoNativo ? 100 : 0)) : 0;
                    if (!lectura.EsRegion || soporte.Any(p => lectura.Palabras.Contains(p)))
                        impuestos.Add((extraccion.DesglosesIva, lectura, confianza));
                }
                foreach (var (campo, valor) in Valores(extraccion).Where(v => v.Value is not null))
                {
                    if (!soportes.TryGetValue(campo, out var soporte) || soporte.Count == 0) continue;
                    if (lectura.EsRegion && !soporte.Any(p => lectura.Palabras.Contains(p))) continue;
                    if (lectura.EsRegion && (campo is nameof(ExtraccionIa.Total) or nameof(ExtraccionIa.BaseImponible) or nameof(ExtraccionIa.CuotaIva)) &&
                        !soporte.Any(p => Regex.IsMatch(p.Texto, @"\d[.,]\d{2}(?:\D|$)|€"))) continue;
                    var puntuaciones = soporte.Where(p => p.Confianza is not null).Select(p => p.Confianza!.Value).ToArray();
                    if (puntuaciones.Length == 0 && !lectura.TextoNativo) continue;
                    candidatos.Add(new Candidato(campo, valor!, lectura, puntuaciones.Length > 0 ? puntuaciones.Min() : null));
                }
            }
        }
        object? Elegir(string campo, object? anterior)
        {
            var lecturas = candidatos.Where(c => c.Campo == campo).ToArray();
            if (campo == nameof(ExtraccionIa.EmisorNif) && lecturas.Any(c => NifValido((string)c.Valor)))
                lecturas = lecturas.Where(c => NifValido((string)c.Valor)).ToArray();
            if (lecturas.Length == 0) return anterior; // Texto nativo de PDF, o campo sin palabras localizables.
            if (campo == nameof(ExtraccionIa.EmisorNombre))
            {
                var legales = lecturas.Where(c => Buscar((string)c.Valor, @"\bS\.?\s*[LA]\.?\s*$").Success).ToArray();
                if (legales.Any(c => c.Lectura.TextoNativo || c.Confianza >= 85) ||
                    legales.Where(c => c.Confianza >= 40).GroupBy(c => Token((string)c.Valor))
                        .Any(g => g.Select(c => c.Lectura.Variante).Distinct().Count() >= 2)) lecturas = legales;
                // Espacios y puntuación no son discrepancias entre nombres. Se devuelve
                // una lectura literal completa; nunca se corrigen letras de un nombre.
                var grupos = lecturas.Where(c => c.Lectura.TextoNativo || c.Confianza >= 40)
                    .GroupBy(c => Token((string)c.Valor)).Where(g => g.Any(c => c.Lectura.TextoNativo || c.Confianza >= 85) ||
                        g.Select(c => c.Lectura.Variante).Distinct().Count() >= 2).ToArray();
                return grupos is [var grupo] ? grupo.OrderByDescending(c => ((string)c.Valor).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length)
                    .ThenByDescending(c => c.Confianza).First().Valor : null;
            }
            // Un error aislado de segmentación (225 frente a 225,70, o un año
            // distinto) no invalida el mismo valor leído por los DOS motores.
            // Si el otro valor también está corroborado, se conserva la ambigüedad.
            var entreMotores = lecturas.Where(c => !c.Lectura.EsRegion && c.Confianza >= 85)
                .GroupBy(c => c.Valor).Where(g => g.Select(c =>
                    c.Lectura.Variante.StartsWith("ppocrv5-latin/", StringComparison.Ordinal) ? "latin" : "tesseract")
                    .Distinct().Count() >= 2 && g.Select(c => c.Lectura.Variante).Distinct().Count() >= 3).ToArray();
            if (entreMotores is [var acuerdoMotores] && !lecturas.Any(c => c.Lectura.TextoNativo && !Equals(c.Valor, acuerdoMotores.Key)) &&
                !lecturas.Where(c => !c.Lectura.EsRegion && c.Confianza >= 85 && !Equals(c.Valor, acuerdoMotores.Key))
                    .GroupBy(c => c.Valor).Any(g => g.Select(c => c.Lectura.Variante).Distinct().Count() >= 2))
                return acuerdoMotores.Key;
            if (campo == nameof(ExtraccionIa.EmisorNif))
            {
                // Los identificadores no son palabras del diccionario: la puntuación
                // puede ser baja aun leyéndose completos. Exigir control válido y
                // coincidencia literal en recortes y preparaciones distintas.
                var coincidentes = lecturas.Where(c => c.Lectura.EsRegion && c.Confianza >= 20 && NifValido((string)c.Valor))
                    .GroupBy(c => c.Valor).Where(g => g.Select(c => c.Lectura.Variante.Split('/')[0]).Distinct().Count() >= 2 &&
                        g.Select(c => c.Lectura.Zona).Distinct().Count() >= 2).ToArray();
                if (coincidentes is [var acuerdo] && !lecturas.Any(c => !Equals(c.Valor, acuerdo.Key) &&
                    (c.Lectura.TextoNativo || c.Confianza >= 85))) return acuerdo.Key;
            }
            var corroboradas = lecturas.Where(c => !c.Lectura.EsRegion && (c.Lectura.TextoNativo || c.Confianza >= 85))
                .GroupBy(c => c.Valor).Where(g => g.Any(c => c.Lectura.TextoNativo) ||
                    g.Select(c => c.Lectura.Variante).Distinct().Count() >= 2).ToArray();
            if (corroboradas is [var corroborada] && !lecturas.Any(c => !Equals(c.Valor, corroborada.Key) &&
                (c.Lectura.TextoNativo || c.Confianza >= 85))) return corroborada.Key;
            var regiones = lecturas.Where(c => c.Lectura.EsRegion && c.Confianza >= 40)
                .GroupBy(c => (c.Lectura.Pagina, c.Lectura.Geometria, c.Lectura.Zona,
                    Preparacion: c.Lectura.Variante.Contains('/') ? c.Lectura.Variante.Split('/')[0] : "")).ToArray();
            var acuerdos = new List<object>();
            foreach (var region in regiones)
            {
                var valores = region.Select(c => c.Valor).Distinct().ToArray();
                if (valores.Length > 1) return null;
                if (region.Select(c => c.Lectura.Variante).Distinct().Count() >= 2) acuerdos.Add(valores[0]);
            }
            var completas = lecturas.Where(c => !c.Lectura.EsRegion && (c.Lectura.TextoNativo || c.Confianza >= 40)).ToArray();
            var identificador = campo is nameof(ExtraccionIa.EmisorNif) or nameof(ExtraccionIa.NumeroFactura);
            if (identificador && completas.Any(c => !c.Lectura.TextoNativo && c.Confianza < 85 &&
                lecturas.Any(r => r.Lectura.EsRegion && !Equals(r.Valor, c.Valor)))) return null;
            if (acuerdos.Count > 0)
            {
                if (acuerdos.Distinct().ToArray() is not [var valor]) return null;
                if (completas.Any(c => !Equals(c.Valor, valor) && (identificador || c.Lectura.TextoNativo || c.Confianza >= 85))) return null;
                return valor;
            }
            var fuertes = completas.Where(c => c.Lectura.TextoNativo || c.Confianza >= 85).ToArray();
            if (fuertes.Select(c => c.Valor).Distinct().ToArray() is [var fiable] &&
                (fuertes.Any(c => c.Lectura.TextoNativo) || completas.Count(c => Equals(c.Valor, fiable)) >= 2))
                return fiable;
            var distintos = completas.Select(c => c.Valor).Distinct().ToArray();
            return distintos is [var unico] &&
                (completas.Select(c => c.Lectura.Variante).Distinct().Count() >= 2 || completas.Any(c => c.Lectura.TextoNativo || c.Confianza >= 85)) ? unico : null;
        }
        resultado.EmisorNombre = (string?)Elegir(nameof(ExtraccionIa.EmisorNombre), resultado.EmisorNombre);
        if (cabeceras.GroupBy(c => Token((string)c.Valor)).ToArray() is [var titulo] &&
            (titulo.Any(c => c.Lectura.TextoNativo) || titulo.Select(c => c.Lectura.Variante).Distinct().Count() >= 2))
            resultado.EmisorNombre = (string)titulo.OrderByDescending(c => c.Confianza).First().Valor;
        resultado.EmisorNif = (string?)Elegir(nameof(ExtraccionIa.EmisorNif), resultado.EmisorNif);
        resultado.NumeroFactura = (string?)Elegir(nameof(ExtraccionIa.NumeroFactura), resultado.NumeroFactura);
        resultado.FechaEmision = (DateOnly?)Elegir(nameof(ExtraccionIa.FechaEmision), resultado.FechaEmision);
        resultado.BaseImponible = (decimal?)Elegir(nameof(ExtraccionIa.BaseImponible), resultado.BaseImponible);
        resultado.TipoIva = (decimal?)Elegir(nameof(ExtraccionIa.TipoIva), resultado.TipoIva);
        resultado.CuotaIva = (decimal?)Elegir(nameof(ExtraccionIa.CuotaIva), resultado.CuotaIva);
        resultado.Total = (decimal?)Elegir(nameof(ExtraccionIa.Total), resultado.Total);
        // Las lecturas parciales de IVA pueden complementarse. Una lectura con una
        // sola fila no contradice a otra que reconoce esa misma fila y una segunda.
        var filas = impuestos.Where(i => i.Confianza >= 40)
            .SelectMany(i => i.Filas.Select(f => (Fila: f, Fuente: i))).GroupBy(i => i.Fila)
            .Where(g => g.Select(i => i.Fuente.Lectura.Variante).Distinct().Count() >= 2 ||
                g.Any(i => i.Fuente.Lectura.TextoNativo || i.Fuente.Confianza >= 85)).ToArray();
        resultado.DesglosesIva = filas.Where(g => !filas.Any(otro => otro.Key.TipoIva == g.Key.TipoIva &&
            otro.Key != g.Key && otro.Key.CuotaIva != g.Key.CuotaIva &&
            !impuestos.Any(i => i.Filas.Contains(g.Key) && i.Filas.Contains(otro.Key))))
            .Select(g => g.Key).Where(f => !filas.Any(g => g.Key != f && f.BaseImponible is null && g.Key.BaseImponible is not null &&
                g.Key.TipoIva == f.TipoIva && g.Key.CuotaIva == f.CuotaIva)).OrderBy(f => f.TipoIva).ToArray();
        if (resultado.DesglosesIva.Count > 1) resultado.TipoIva = resultado.CuotaIva = null;
        Comprobar(resultado);
        return resultado;
    }

    private static bool Dentro(PalabraDocumento palabra, RegionDocumento? region) => region is not null &&
        palabra.CentroX >= region.X && palabra.CentroX <= region.X + region.Ancho &&
        palabra.CentroY >= region.Y && palabra.CentroY <= region.Y + region.Alto;

    private static IReadOnlyList<PalabraDocumento> SepararEmisor(IReadOnlyList<PalabraDocumento> palabras)
    {
        foreach (var linea in DisposicionDocumento.Lineas(palabras))
        {
            var indice = linea.Palabras.ToList().FindIndex(p => Token(p.Texto) is "CLIENTE" or "RECEPTOR" or "DESTINATARIO");
            if (indice < 0) continue;
            if (indice > 0 && Token(linea.Palabras[indice - 1].Texto) == "POR") continue;
            while (indice > 0 && Token(linea.Palabras[indice - 1].Texto) is "DATOS" or "DEL" or "DE" or "LA") indice--;
            var etiqueta = linea.Palabras[indice];
            if (etiqueta.X < .45) return palabras.Where(p => p.CentroY < etiqueta.Y).ToArray();
            var previo = indice > 0 ? linea.Palabras[indice - 1] : null;
            var separacion = previo is not null && etiqueta.X - previo.X - previo.Ancho > .08
                ? (previo.X + previo.Ancho + etiqueta.X) / 2 : etiqueta.X - .08;
            return palabras.Where(p => p.CentroX < separacion || p.Y + p.Alto < etiqueta.Y).ToArray();
        }
        return palabras;
    }

    private static Dictionary<string, object?> Valores(ExtraccionIa e) => new()
    {
        [nameof(e.EmisorNombre)] = e.EmisorNombre, [nameof(e.EmisorNif)] = e.EmisorNif,
        [nameof(e.NumeroFactura)] = e.NumeroFactura, [nameof(e.FechaEmision)] = e.FechaEmision,
        [nameof(e.BaseImponible)] = e.BaseImponible, [nameof(e.TipoIva)] = e.TipoIva,
        [nameof(e.CuotaIva)] = e.CuotaIva, [nameof(e.Total)] = e.Total
    };

    private static string Token(string texto) => Regex.Replace(Normalizar(texto).ToUpperInvariant(), @"[^A-Z0-9]", "");

    private static IReadOnlyList<LineaDocumento> LineasDeOrigen(IReadOnlyList<PalabraDocumento> palabras) =>
        palabras.Count > 0 && palabras.All(p => p.LineaOrigen is not null)
            ? palabras.GroupBy(p => p.LineaOrigen).OrderBy(g => g.Key)
                .Select(g => new LineaDocumento(g.OrderBy(p => p.X).ToArray())).ToArray()
            : DisposicionDocumento.Lineas(palabras);

    private static decimal? Numero(PalabraDocumento p)
    {
        var m = Buscar(p.Texto.Trim(), @"^(" + Dinero + @")\s*(?:%|€|EUR)?[.;]?$" );
        return m.Success ? ConvertirImporte(m.Groups[1].Value) : null;
    }

    private static IReadOnlyList<PalabraDocumento> BuscarSoporte(string campo, object valor, IReadOnlyList<PalabraDocumento> palabras)
    {
        if (valor is decimal d) return palabras.Where(p => Numero(p) == d).ToArray();
        if (valor is DateOnly fecha) return palabras.Where(p => Buscar(Normalizar(p.Texto), "^(?:" + Fecha + "|" + FechaTexto + ")[,;]?$" ).Success &&
            LeerFecha(p.Texto.TrimEnd(',', ';')) == fecha).ToArray();
        var buscado = Token((string)valor);
        if (campo == nameof(ExtraccionIa.EmisorNombre))
        {
            // Las palabras consecutivas de la línea que forman el nombre; no cualquier palabra
            // suelta de la página cuyas letras aparezcan dentro de él (rebajaría su confianza).
            foreach (var linea in LineasDeOrigen(palabras))
            {
                var p = linea.Palabras.Where(x => Token(x.Texto).Length > 0).ToArray();
                for (var i = 0; i < p.Length; i++)
                {
                    var acumulado = "";
                    for (var j = i; j < p.Length && acumulado.Length < buscado.Length; j++)
                        if ((acumulado += Token(p[j].Texto)) == buscado) return p[i..(j + 1)];
                }
            }
            return [];
        }
        return palabras.Where(p => Token(p.Texto) == buscado || campo == nameof(ExtraccionIa.EmisorNif) &&
            Regex.IsMatch(Token(p.Texto), @"^(?:NIF|CIF|NIE)" + Regex.Escape(buscado) + "$" )).ToArray();
    }

    private static void LeerPosiciones(ExtraccionIa e, IReadOnlyList<PalabraDocumento> palabras,
        Dictionary<string, IReadOnlyList<PalabraDocumento>> soportes)
    {
        var lineas = DisposicionDocumento.Lineas(palabras);
        var totales = new List<PalabraDocumento>();
        foreach (var linea in lineas)
        {
            foreach (var etiqueta in linea.Palabras.Where(p => Token(p.Texto) == "TOTAL"))
            {
                var derecha = linea.Palabras.Where(p => p.X > etiqueta.X + etiqueta.Ancho * .8).ToArray();
                if (derecha.Length > 0 && (Token(derecha[0].Texto) == "BASE" ||
                    Token(derecha[0].Texto) == "IVA" && (derecha.Length < 2 || Token(derecha[1].Texto) is not ("INCLUIDO" or "INCLUIDA" or "INC" or "INCL")))) continue;
                if (linea.Palabras.Any(p => Token(p.Texto) is "PVP" or "PRECIO" or "DESCRIPCION")) continue;
                var importes = derecha.Where(p => Numero(p) is not null).ToArray();
                // "109," + ruido + "70€": la cifra llegó partida; quedarse con "70" sería inventar.
                var partida = derecha.Any(p => p.Texto.Any(char.IsDigit) && Numero(p) is null);
                if (importes.Length == 1 && !partida) totales.Add(importes[0]);
            }
        }
        if (totales.Count > 0)
        {
            e.Total = Unico(totales.Select(p => Numero(p)!.Value));
            soportes[nameof(e.Total)] = totales;
        }

        var filasFiscales = new List<(PalabraDocumento Base, PalabraDocumento Tipo, PalabraDocumento Cuota)>();
        foreach (var linea in lineas)
        {
            var b = linea.Palabras.FirstOrDefault(p => Token(p.Texto) is "BASE" or "BASEIMP" or "BASEIMPONIBLE");
            if (b is null) continue;
            var c = linea.Palabras.FirstOrDefault(p => Token(p.Texto) is "CUOTA" or "CUOTAIVA" or "IMPIVA" or "IMPORTEIVA");
            if (c is null)
            {
                c = linea.Palabras.LastOrDefault(p => Token(p.Texto) == "IMP" && linea.Palabras.Any(v =>
                    Token(v.Texto) == "IVA" && v.X > p.X && v.X - p.X < .12));
            }
            if (c is null) continue;
            var t = linea.Palabras.FirstOrDefault(p => p.X > b.X && p.X < c.X &&
                (Token(p.Texto) is "IVA" or "TIPO" or "TIPOIVA" or "XIVA"));
            if (t is null) continue;
            var limiteIzquierdo = b.CentroX - (t.CentroX - b.CentroX) * .8;
            var separacion1 = (b.CentroX + t.CentroX) / 2;
            var separacion2 = (t.CentroX + c.CentroX) / 2;
            var limiteDerecho = c.CentroX + (c.CentroX - t.CentroX) * .8;
            var alto = new[] { b.Alto, t.Alto, c.Alto }.Max();
            foreach (var fila in lineas.Where(l => l.Y > b.Y + alto * .5 && l.Y < b.Y + alto * 6))
            {
                var numeros = fila.Palabras.Where(p => Numero(p) is not null).ToArray();
                var bases = numeros.Where(p => p.CentroX >= limiteIzquierdo && p.CentroX < separacion1).ToArray();
                var tipos = numeros.Where(p => p.CentroX >= separacion1 && p.CentroX < separacion2).ToArray();
                var cuotas = numeros.Where(p => p.CentroX >= separacion2 && p.CentroX <= limiteDerecho).ToArray();
                if (bases.Length == 1 && tipos.Length == 1 && cuotas.Length == 1 && Numero(tipos[0]) is >= 0 and <= 100)
                    filasFiscales.Add((bases[0], tipos[0], cuotas[0]));
            }
        }
        if (filasFiscales.Count > 0)
        {
            e.DesglosesIva = filasFiscales.Select(f => new DesgloseIva(Numero(f.Base), Numero(f.Tipo), Numero(f.Cuota))).Distinct().ToArray();
            // Una base global etiquetada puede coexistir con varias filas; no sumarlas.
            e.TipoIva = e.CuotaIva = null;
            if (filasFiscales.Count == 1)
            {
                var fila = filasFiscales[0];
                e.BaseImponible = Numero(fila.Base); e.TipoIva = Numero(fila.Tipo); e.CuotaIva = Numero(fila.Cuota);
                soportes[nameof(e.BaseImponible)] = [fila.Base];
                soportes[nameof(e.TipoIva)] = [fila.Tipo];
                soportes[nameof(e.CuotaIva)] = [fila.Cuota];
            }
        }

        var numerosFactura = new List<PalabraDocumento>();
        var hayEtiquetaFactura = false;
        foreach (var etiqueta in palabras.Where(p => Token(p.Texto) is "FACT" or "FACTURA" or "FRA" or "TICKET" or
            "TIQUET" or "FACTSIMPLIFICADA" or "FACTURASIMPLIFICADA" or "FRASIMPLIFICADA"))
        {
            hayEtiquetaFactura = true;
            var debajo = palabras.Where(p => p.CentroY >= etiqueta.CentroY - etiqueta.Alto * .4 &&
                p.Y < etiqueta.Y + etiqueta.Alto * 3.5 && p.X >= etiqueta.X - .03 && p.X < etiqueta.X + .4);
            var posibles = new List<PalabraDocumento>();
            foreach (var p in debajo)
            {
                var s = p.Texto.Trim(':', '#', ',', ';');
                if (!s.Any(char.IsDigit) || Buscar(s, "^(?:" + Fecha + "|" + FechaTexto + ")$" ).Success) continue;
                if (Buscar(s, @"^\d+[.,]\d{2}(?:€|EUR)?$").Success) continue;
                if (palabras.Any(m => Token(m.Texto) is "MESA" or "NESA" &&
                    Math.Abs(m.CentroY-p.CentroY) < Math.Max(m.Alto,p.Alto) && m.X < p.X &&
                    p.X-m.X < Math.Abs(p.X-etiqueta.X))) continue;
                // Mayúsculas y dos cifras como mínimo: "Preci0" (Precio mal leído) no es un número.
                if (Regex.IsMatch(s, @"^(?:[A-Z0-9]+[/.-][A-Z0-9/.-]+|[A-Z]+\d[A-Z0-9]*|\d{4,6})$") && s.Count(char.IsDigit) >= 2)
                    posibles.Add(p);
            }
            if (posibles.Count > 0)
            {
                var proxima = posibles.Min(p => Math.Abs(p.CentroY-etiqueta.CentroY));
                numerosFactura.AddRange(posibles.Where(p => Math.Abs(p.CentroY-etiqueta.CentroY) <= proxima + etiqueta.Alto*.5));
            }
        }
        if (numerosFactura.Count > 0)
        {
            e.NumeroFactura = Unico(numerosFactura.Select(p => p.Texto.Trim(':', '#', ',', ';')));
            soportes[nameof(e.NumeroFactura)] = numerosFactura;
        }
        else if (hayEtiquetaFactura)
        {
            e.NumeroFactura = null;
            soportes.Remove(nameof(e.NumeroFactura));
        }
    }
}
