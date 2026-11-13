using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Gestio.Api.Domain.Entities;

namespace Gestio.Api.Services.Verifactu;

/// <summary>Datos de un registro de alta que entran en la huella, en el orden en que se concatenan.</summary>
public sealed record DatosHuellaAlta(
    string IdEmisorFactura,
    string NumSerieFactura,
    DateOnly FechaExpedicionFactura,
    string TipoFactura,
    decimal CuotaTotal,
    decimal ImporteTotal,
    string? HuellaAnterior,
    string FechaHoraHusoGenRegistro)
{
    public static DatosHuellaAlta De(RegistroFacturacion registro) => new(
        registro.IdEmisorFactura,
        registro.NumSerieFactura,
        registro.FechaExpedicionFactura,
        registro.TipoFactura,
        registro.CuotaTotal,
        registro.ImporteTotal,
        registro.HuellaAnterior,
        registro.FechaHoraHusoGenRegistro);
}

/// <summary>
/// Huella de un registro de alta, imitando el formato que publica la AEAT para VeriFactu
/// (simulación académica: ni certificada ni conectada a la AEAT).
///
/// Representación canónica (siempre igual):
///   IDEmisorFactura={NIF}&amp;NumSerieFactura={serie-número}&amp;FechaExpedicionFactura={dd-MM-yyyy}
///   &amp;TipoFactura={F1}&amp;CuotaTotal={0.00}&amp;ImporteTotal={0.00}&amp;Huella={huella anterior o vacío}
///   &amp;FechaHoraHusoGenRegistro={yyyy-MM-ddTHH:mm:ss±hh:mm en hora de Europe/Madrid}
/// - Orden de campos fijo, separados por '&amp;', sin espacios añadidos.
/// - Importes con exactamente 2 decimales y punto decimal (InvariantCulture); se rechazan importes con más decimales.
/// - Codificación UTF-8; SHA-256; resultado en hexadecimal en mayúsculas (64 caracteres).
/// Contrastado con el ejemplo oficial de la AEAT: ver RegistroEncadenadoHuellaTests.
/// </summary>
public static class HuellaVerifactu
{
    /// <summary>Factura completa (ordinaria). Es el único tipo que emite GESTIO.</summary>
    public const string TipoFacturaCompleta = "F1";

    private static readonly TimeSpan HorarioInvierno = TimeSpan.FromHours(1);
    private static readonly TimeSpan HorarioVerano = TimeSpan.FromHours(2);

    public static string Canonico(DatosHuellaAlta datos) => string.Create(CultureInfo.InvariantCulture,
        $"IDEmisorFactura={datos.IdEmisorFactura}" +
        $"&NumSerieFactura={datos.NumSerieFactura}" +
        $"&FechaExpedicionFactura={datos.FechaExpedicionFactura:dd-MM-yyyy}" +
        $"&TipoFactura={datos.TipoFactura}" +
        $"&CuotaTotal={Importe(datos.CuotaTotal)}" +
        $"&ImporteTotal={Importe(datos.ImporteTotal)}" +
        $"&Huella={datos.HuellaAnterior}" +
        $"&FechaHoraHusoGenRegistro={datos.FechaHoraHusoGenRegistro}");

    public static string Calcular(DatosHuellaAlta datos) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonico(datos))));

    /// <summary>Instante truncado a segundos, en UTC. Es el valor que se guarda en <c>Generado</c>.</summary>
    public static DateTimeOffset TruncarASegundos(DateTimeOffset instante)
    {
        var utc = instante.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
    }

    /// <summary>
    /// Fecha y hora con huso de Europe/Madrid (península), p. ej. 2026-06-16T15:24:01+02:00.
    /// Se calcula con la regla horaria de la UE escrita aquí (verano desde el último domingo de marzo a las 01:00 UTC
    /// hasta el último domingo de octubre a las 01:00 UTC), en lugar de la base de datos de zonas horarias del sistema,
    /// para que la huella no dependa del entorno (la imagen alpine de producción no trae tzdata).
    /// </summary>
    public static string FormatearFechaHoraHuso(DateTimeOffset instante)
    {
        var utc = TruncarASegundos(instante);
        var local = utc.ToOffset(DesfaseMadrid(utc.UtcDateTime));
        return local.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    private static TimeSpan DesfaseMadrid(DateTime utc)
    {
        var inicioVerano = UltimoDomingoALaUnaUtc(utc.Year, 3);
        var finVerano = UltimoDomingoALaUnaUtc(utc.Year, 10);
        return utc >= inicioVerano && utc < finVerano ? HorarioVerano : HorarioInvierno;
    }

    private static DateTime UltimoDomingoALaUnaUtc(int anio, int mes)
    {
        var dia = new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes), 1, 0, 0, DateTimeKind.Utc);
        while (dia.DayOfWeek != DayOfWeek.Sunday) dia = dia.AddDays(-1);
        return dia;
    }

    private static string Importe(decimal importe)
    {
        if (decimal.Round(importe, 2) != importe)
            throw new ArgumentException($"El importe {importe} tiene más de 2 decimales; la huella exige importes redondeados al céntimo.", nameof(importe));
        return importe.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
