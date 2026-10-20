using System.Text.RegularExpressions;
using Gestio.Api.Domain.Entities;

namespace Gestio.Api.Services;

public static partial class ExtractorCampos
{
    private static IReadOnlyList<DesgloseIva> LeerDesgloses(string[] lineas)
    {
        var filas = new List<DesgloseIva>();
        foreach (var linea in lineas)
        {
            // IVA 21 % 12,34; TAX/IVA 10% 4,50; TVA (francés). Nunca interpretar la cuota como base.
            var m = Buscar(linea, @"^(?:TAX\s*[/\-]\s*)?(?:IVA|I\.?V\.?A\.?|TVA)\s*[:(]?\s*(\d{1,2}(?:[.,]\d{1,2})?)\s*%\)?\s*[:=|]?\s*(" + Dinero + @")\s*(?:€|EUR)?\s*$");
            if (m.Success && ConvertirImporte(m.Groups[1].Value) is { } tipo &&
                ConvertirImporte(m.Groups[2].Value) is { } cuota)
                filas.Add(new(null, tipo, cuota));
        }
        for (var i = 0; i < lineas.Length; i++)
        {
            var b = Buscar(lineas[i], @"\bBASE(?:\s+IMPONIBLE)?\b");
            var t = Buscar(lineas[i], @"\b(?:TIPO(?:\s+IVA)?|IVA)\b");
            var c = Buscar(lineas[i], @"\b(?:CUOTA(?:\s+IVA)?|IMPORTE\s+IVA)\b");
            if (!b.Success || !t.Success || !c.Success || t.Index == c.Index) continue;
            var columnas = new[] { (Indice: b.Index, Campo: 0), (Indice: t.Index, Campo: 1), (Indice: c.Index, Campo: 2) }.OrderBy(v => v.Indice).ToArray();
            for (var j = i+1; j < Math.Min(i+8, lineas.Length); j++)
            {
                if (Regex.IsMatch(lineas[j], @"^[\s\-_=|.]+$")) continue;
                if (Regex.IsMatch(lineas[j], @"[A-Za-z]")) break;
                var numeros = Regex.Matches(lineas[j], @"(?<!\w)(?:" + Dinero + @")(?!\w)").Select(m => ConvertirImporte(m.Value)).ToArray();
                if (numeros.Length != 3 || numeros.Any(n => n is null)) break;
                var valores = new decimal?[3];
                for (var k = 0; k < 3; k++) valores[columnas[k].Campo] = numeros[k];
                if (valores[1] is >= 0 and <= 100) filas.Add(new(valores[0], valores[1], valores[2]));
            }
        }
        return filas.Distinct().ToArray();
    }

    private static void Comprobar(ExtraccionIa e)
    {
        var avisos = new List<string>();
        if (e.EmisorNif is { } nif && !NifValido(nif))
            avisos.Add("El NIF leído no supera la comprobación del dígito de control. Compruébalo en el original.");
        if (e.DesglosesIva.Any(f => f.BaseImponible is { } b && f.TipoIva is { } t && f.CuotaIva is { } c &&
            Math.Abs(decimal.Round(b*t/100, 2, MidpointRounding.AwayFromZero)-c) > .03m))
            avisos.Add("Hay un desglose de IVA cuyos importes leídos no concuerdan. Compruébalo en el original.");
        var cuotas = e.DesglosesIva.Count > 0 && e.DesglosesIva.All(f => f.CuotaIva is not null)
            ? e.DesglosesIva.Sum(f => f.CuotaIva) : e.CuotaIva;
        if (e.BaseImponible is { } baseLeida && cuotas is { } cuotaLeida && e.Total is { } total &&
            Math.Abs(baseLeida+cuotaLeida-total) > .03m)
            avisos.Add("La base, el IVA y el total leídos no concuerdan; puede haber otros conceptos o un error de lectura.");
        e.Advertencias = avisos;
    }

    // Sólo valida lo leído; no sustituye caracteres ni fabrica un identificador.
    private static bool NifValido(string nif)
    {
        if (Regex.IsMatch(nif, @"^[0-9]{8}[A-Z]$"))
            return "TRWAGMYFPDXBNJZSQVHLCKE"[int.Parse(nif[..8])%23] == nif[8];
        if (Regex.IsMatch(nif, @"^[XYZ][0-9]{7}[A-Z]$"))
            return "TRWAGMYFPDXBNJZSQVHLCKE"[int.Parse("XYZ".IndexOf(nif[0]).ToString()+nif.Substring(1,7))%23] == nif[8];
        if (!Regex.IsMatch(nif, @"^[ABCDEFGHJNPQRSUVW][0-9]{7}[0-9A-J]$")) return false;
        var suma = 0;
        for (var i = 1; i <= 7; i++)
        { var valor = nif[i]-'0'; if (i%2 == 1) { valor *= 2; valor = valor/10+valor%10; } suma += valor; }
        var control = (10-suma%10)%10;
        return nif[8] == ('0'+control) && !"NPQRSW".Contains(nif[0]) ||
            nif[8] == "JABCDEFGHI"[control] && !"ABEH".Contains(nif[0]);
    }
}
