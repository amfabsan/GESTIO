using System.Text.RegularExpressions;

namespace Gestio.Api.Services.Whatsapp;

// UT27: formato único de teléfono, E.164 sin «+» (solo dígitos), el mismo que entrega Meta en «from» y el que exige
// la restricción CHECK de numeros_whatsapp.
public static partial class TelefonoWhatsapp
{
    [GeneratedRegex("^[1-9][0-9]{7,14}$")]
    private static partial Regex E164SinMas();

    // Acepta «+34 600 123 456», «0034600123456», «34-600-123-456» y, por comodidad, un móvil español de 9 cifras
    // sin prefijo (empieza por 6 o 7), al que se añade 34. Cualquier otro número sin «+» ni «00» se rechaza para no
    // adivinar el país. Devuelve null si no se puede normalizar.
    public static string? Normalizar(string? entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada)) return null;

        var texto = entrada.Trim();
        if (texto.Any(c => !(char.IsAsciiDigit(c) || c is '+' or ' ' or '-' or '(' or ')' or '.'))) return null;
        if (texto.LastIndexOf('+') > 0) return null;

        var digitos = new string(texto.Where(char.IsAsciiDigit).ToArray());
        if (!texto.StartsWith('+'))
        {
            if (digitos.StartsWith("00")) digitos = digitos[2..];
            else if (digitos.Length == 9 && digitos[0] is '6' or '7') digitos = "34" + digitos;
            // Sin «+» ni «00» no se puede saber el país: solo se acepta si ya empieza por el prefijo español.
            else if (!(digitos.Length == 11 && digitos.StartsWith("34"))) return null;
        }

        return E164SinMas().IsMatch(digitos) ? digitos : null;
    }

    // Para mostrar sin exponer el número: solo las 3 últimas cifras, «+••••••••456». No se muestra el prefijo porque
    // sin una tabla de países no se sabe si tiene 1, 2 o 3 cifras, y mostrar 2 fijas engaña (+15… para EE. UU.).
    public static string Enmascarar(string telefono) =>
        telefono.Length <= 3 ? new string('•', telefono.Length) : $"+{new string('•', telefono.Length - 3)}{telefono[^3..]}";
}
