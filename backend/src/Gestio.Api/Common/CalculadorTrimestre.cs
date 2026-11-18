namespace Gestio.Api.Common;

/// <summary>
/// Utilidades para trabajar con trimestres fiscales.
/// </summary>
public static class CalculadorTrimestre
{
    /// <summary>
    /// Calcula el trimestre (1-4) a partir de una fecha.
    /// T1: ene-mar (1-3), T2: abr-jun (4-6), T3: jul-sep (7-9), T4: oct-dic (10-12)
    /// </summary>
    public static int ObtenerTrimestre(DateOnly fecha)
    {
        var mes = fecha.Month;
        return (mes - 1) / 3 + 1;
    }

    /// <summary>
    /// Obtiene el primer día del trimestre.
    /// </summary>
    public static DateOnly PrimerDiaDelTrimestre(int ejercicio, int trimestre)
    {
        var mes = (trimestre - 1) * 3 + 1;
        return new DateOnly(ejercicio, mes, 1);
    }

    /// <summary>
    /// Obtiene el último día del trimestre.
    /// </summary>
    public static DateOnly UltimoDiaDelTrimestre(int ejercicio, int trimestre)
    {
        var mesUltimo = trimestre * 3;
        var diasDelMes = DateTime.DaysInMonth(ejercicio, mesUltimo);
        return new DateOnly(ejercicio, mesUltimo, diasDelMes);
    }

    /// <summary>
    /// Verifica si una fecha pertenece al trimestre indicado.
    /// </summary>
    public static bool PerteneceAlTrimestre(DateTimeOffset fecha, int ejercicio, int trimestre)
    {
        var fechaLocal = DateOnly.FromDateTime(fecha.DateTime);
        var inicio = PrimerDiaDelTrimestre(ejercicio, trimestre);
        var fin = UltimoDiaDelTrimestre(ejercicio, trimestre);
        return fechaLocal >= inicio && fechaLocal <= fin;
    }

    /// <summary>
    /// Verifica si una fecha pertenece al trimestre indicado.
    /// Sobrecarga para DateOnly.
    /// </summary>
    public static bool PerteneceAlTrimestre(DateOnly fecha, int ejercicio, int trimestre)
    {
        var inicio = PrimerDiaDelTrimestre(ejercicio, trimestre);
        var fin = UltimoDiaDelTrimestre(ejercicio, trimestre);
        return fecha >= inicio && fecha <= fin;
    }
}
