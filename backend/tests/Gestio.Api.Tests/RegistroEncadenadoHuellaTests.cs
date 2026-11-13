using System.Globalization;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Services.Verifactu;

namespace Gestio.Api.Tests;

/// <summary>
/// UT22, capa 1: huella SHA-256 y verificación de la cadena (lógica pura).
/// Los valores esperados de huella y de huso horario son literales calculados FUERA de este código
/// (Python hashlib y zoneinfo, y el ejemplo oficial publicado por la AEAT), para que una prueba
/// no repita el mismo error que el código.
/// </summary>
public sealed class RegistroEncadenadoHuellaTests
{
    private static readonly Guid AutonomoId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // Ejemplo oficial de la AEAT (primer registro de alta, sin huella anterior). Huella verificada con Python.
    private static readonly DatosHuellaAlta EjemploAeat = new(
        "89890001K", "12345678/G33", new DateOnly(2024, 1, 1), "F1", 12.35m, 123.45m, null, "2024-01-01T19:20:30+01:00");
    private const string HuellaEjemploAeat = "3C464DAF61ACB827C65FDA19F352A4E3BDC2C640E9E9FC4CC058073F38F12F60";

    // Cadena propia de dos registros. Huellas calculadas con Python sobre el mismo texto canónico.
    private const string HuellaR1 = "055B026B9D7B70CC788473DCF05F73363270B759BCB1B6C763DC038ABD26448F";
    private const string HuellaR2 = "65C130DE93947A7C44530C898F1009E00ECFA0FD2D70F0BD1E5CA853E69A54E9";

    [Fact]
    public void Huella_CoincideConElEjemploOficialDeLaAeat()
    {
        Assert.Equal(
            "IDEmisorFactura=89890001K&NumSerieFactura=12345678/G33&FechaExpedicionFactura=01-01-2024&TipoFactura=F1" +
            "&CuotaTotal=12.35&ImporteTotal=123.45&Huella=&FechaHoraHusoGenRegistro=2024-01-01T19:20:30+01:00",
            HuellaVerifactu.Canonico(EjemploAeat));
        Assert.Equal(HuellaEjemploAeat, HuellaVerifactu.Calcular(EjemploAeat));
    }

    [Fact]
    public void Huella_EncadenaConLaHuellaAnterior_VectoresIndependientes()
    {
        var r1 = Registro(1, null, "F-2026-0001", new DateOnly(2026, 6, 16), 16.25m, 178.75m, Utc("2026-06-16T13:24:01Z"));
        var r2 = Registro(2, r1, "F-2026-0002", new DateOnly(2026, 10, 25), 210m, 1210m, Utc("2026-10-25T01:30:00Z"));

        Assert.Equal(HuellaR1, r1.Huella);
        Assert.Equal(HuellaR1, r2.HuellaAnterior);
        Assert.Contains("&CuotaTotal=210.00&ImporteTotal=1210.00&Huella=" + HuellaR1 + "&", HuellaVerifactu.Canonico(DatosHuellaAlta.De(r2)));
        Assert.Equal(HuellaR2, r2.Huella);
    }

    [Fact]
    public void Huella_NoDependeDeLaCulturaDelHilo()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-ES"); // usaría coma decimal y otro formato de fecha
            Assert.Equal(HuellaEjemploAeat, HuellaVerifactu.Calcular(EjemploAeat));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Huella_RechazaImportesConMasDeDosDecimales()
    {
        var datos = EjemploAeat with { ImporteTotal = 123.455m };
        Assert.Throws<ArgumentException>(() => HuellaVerifactu.Calcular(datos));
    }

    [Theory]
    // Valores esperados obtenidos con Python zoneinfo("Europe/Madrid").
    [InlineData("2024-01-01T18:20:30Z", "2024-01-01T19:20:30+01:00")] // invierno
    [InlineData("2026-06-16T13:24:01Z", "2026-06-16T15:24:01+02:00")] // verano
    [InlineData("2026-10-25T00:30:00Z", "2026-10-25T02:30:00+02:00")] // hora repetida del cambio de octubre, aún en verano
    [InlineData("2026-10-25T01:30:00Z", "2026-10-25T02:30:00+01:00")] // misma hora local, ya en invierno
    [InlineData("2026-03-29T00:59:59Z", "2026-03-29T01:59:59+01:00")] // justo antes del cambio de marzo
    [InlineData("2026-03-29T01:00:00Z", "2026-03-29T03:00:00+02:00")] // justo después del cambio de marzo
    [InlineData("2026-06-16T13:24:01.987Z", "2026-06-16T15:24:01+02:00")] // trunca, no redondea, los milisegundos
    public void FechaHoraHuso_UsaLaHoraDeMadridConSuDesfase(string utc, string esperado)
    {
        Assert.Equal(esperado, HuellaVerifactu.FormatearFechaHoraHuso(Utc(utc)));
    }

    [Fact]
    public void Verificar_CadenaVacia_EsCoherente()
    {
        var resultado = VerificadorCadena.Verificar([]);
        Assert.True(resultado.Coherente);
        Assert.Equal(0, resultado.Revisados);
    }

    [Fact]
    public void Verificar_CadenaCorrecta_EsCoherente()
    {
        var cadena = Cadena(3);
        var resultado = VerificadorCadena.Verificar(cadena.AsEnumerable().Reverse()); // el orden de entrada no importa
        Assert.True(resultado.Coherente);
        Assert.Equal(3, resultado.Revisados);
        Assert.Null(resultado.Incoherencia);
    }

    [Fact]
    public void PA1594_DatoAlterado_SeñalaElRegistroAfectado()
    {
        var cadena = Cadena(3);
        cadena[0].ImporteTotal += 1000m; // mismo caso que el mockup: importe del registro #1 modificado

        var resultado = VerificadorCadena.Verificar(cadena);

        Assert.False(resultado.Coherente);
        Assert.Equal(cadena[0].Id, resultado.Incoherencia!.RegistroId);
        Assert.Equal(1, resultado.Incoherencia.Secuencia);
        Assert.Equal(VerificadorCadena.HuellaNoCoincide, resultado.Incoherencia.Codigo);
        Assert.Equal(1, resultado.Revisados);
    }

    [Theory]
    [InlineData(nameof(RegistroFacturacion.IdEmisorFactura))]
    [InlineData(nameof(RegistroFacturacion.NumSerieFactura))]
    [InlineData(nameof(RegistroFacturacion.FechaExpedicionFactura))]
    [InlineData(nameof(RegistroFacturacion.TipoFactura))]
    [InlineData(nameof(RegistroFacturacion.CuotaTotal))]
    [InlineData(nameof(RegistroFacturacion.FechaHoraHusoGenRegistro))]
    [InlineData(nameof(RegistroFacturacion.Huella))]
    public void PA1594_CualquierCampoDeLaHuellaAlterado_SeDetectaEnEseRegistro(string campo)
    {
        var cadena = Cadena(3);
        var r = cadena[1];
        switch (campo)
        {
            case nameof(RegistroFacturacion.IdEmisorFactura): r.IdEmisorFactura = "00000000T"; break;
            case nameof(RegistroFacturacion.NumSerieFactura): r.NumSerieFactura = "F-2026-9999"; break;
            case nameof(RegistroFacturacion.FechaExpedicionFactura): r.FechaExpedicionFactura = r.FechaExpedicionFactura.AddDays(1); break;
            case nameof(RegistroFacturacion.TipoFactura): r.TipoFactura = "F2"; break;
            case nameof(RegistroFacturacion.CuotaTotal): r.CuotaTotal += 0.01m; break;
            case nameof(RegistroFacturacion.FechaHoraHusoGenRegistro): r.FechaHoraHusoGenRegistro = "2026-06-17T10:00:00+02:00"; break;
            case nameof(RegistroFacturacion.Huella): r.Huella = new string('0', 64); break;
        }

        var resultado = VerificadorCadena.Verificar(cadena);

        Assert.False(resultado.Coherente);
        Assert.Equal(2, resultado.Incoherencia!.Secuencia);
        Assert.Equal(VerificadorCadena.HuellaNoCoincide, resultado.Incoherencia.Codigo);
    }

    [Fact]
    public void PA1594_FechaGeneracionAlterada_SinTocarElTextoHasheado_SeDetecta()
    {
        var cadena = Cadena(2);
        cadena[1].Generado = cadena[1].Generado.AddHours(1);

        var resultado = VerificadorCadena.Verificar(cadena);

        Assert.Equal(2, resultado.Incoherencia!.Secuencia);
        Assert.Equal(VerificadorCadena.FechaGeneracionNoCoincide, resultado.Incoherencia.Codigo);
    }

    [Fact]
    public void PA1595_EnlaceAlRegistroAnteriorAlterado_SeDetecta()
    {
        var cadena = Cadena(3);
        cadena[2].AnteriorId = cadena[0].Id; // #3 apunta a #1 (el enlace no entra en la huella)

        var resultado = VerificadorCadena.Verificar(cadena);

        Assert.Equal(3, resultado.Incoherencia!.Secuencia);
        Assert.Equal(VerificadorCadena.EnlaceNoCoincide, resultado.Incoherencia.Codigo);
    }

    [Fact]
    public void PA1595_InicioDeCadenaConAnterior_SeDetecta()
    {
        var cadena = Cadena(2);
        cadena[0].AnteriorId = Guid.NewGuid();

        var resultado = VerificadorCadena.Verificar(cadena);

        Assert.Equal(1, resultado.Incoherencia!.Secuencia);
        Assert.Equal(VerificadorCadena.EnlaceNoCoincide, resultado.Incoherencia.Codigo);
    }

    [Fact]
    public void PA1595_HuellaAnteriorAlterada_SeDetectaEnEseRegistro()
    {
        var cadena = Cadena(3);
        cadena[1].HuellaAnterior = HuellaR2; // la huella anterior forma parte de la huella propia

        var resultado = VerificadorCadena.Verificar(cadena);

        Assert.Equal(2, resultado.Incoherencia!.Secuencia);
        Assert.Equal(VerificadorCadena.HuellaNoCoincide, resultado.Incoherencia.Codigo);
    }

    [Fact]
    public void PA1595_HuellaAnteriorRecalculadaParaOcultarElCambio_SeDetectaPorElEnlace()
    {
        // Alguien cambia la huella anterior del #2 Y recalcula su huella propia: la huella cuadra,
        // pero ya no coincide con la del #1.
        var cadena = Cadena(2);
        var r2 = cadena[1];
        r2.HuellaAnterior = new string('A', 64);
        r2.Huella = HuellaVerifactu.Calcular(DatosHuellaAlta.De(r2));

        var resultado = VerificadorCadena.Verificar(cadena);

        Assert.Equal(2, resultado.Incoherencia!.Secuencia);
        Assert.Equal(VerificadorCadena.HuellaAnteriorNoCoincide, resultado.Incoherencia.Codigo);
    }

    [Fact]
    public void PA1595_RegistroEliminadoDeLaCadena_SeDetectaElHueco()
    {
        var cadena = Cadena(3);
        cadena.RemoveAt(1);

        var resultado = VerificadorCadena.Verificar(cadena);

        Assert.Equal(3, resultado.Incoherencia!.Secuencia);
        Assert.Equal(VerificadorCadena.SecuenciaRota, resultado.Incoherencia.Codigo);
    }

    [Fact]
    public void PA1595_SecuenciaDuplicada_SeDetecta()
    {
        var cadena = Cadena(3);
        cadena[2].Secuencia = 2; // dos registros con la posición #2

        var resultado = VerificadorCadena.Verificar(cadena);

        Assert.False(resultado.Coherente);
        Assert.Equal(VerificadorCadena.SecuenciaRota, resultado.Incoherencia!.Codigo);
        Assert.Equal(2, resultado.Incoherencia.Secuencia);
    }

    [Fact]
    public void Verificar_InicioDeCadenaConHuellaAnteriorVaciaEnLugarDeNull_SigueSiendoCoherente()
    {
        // Revisión capa 1: si la BD devolviera "" en vez de null, una cadena válida no debe marcarse como incoherente.
        var cadena = Cadena(2);
        cadena[0].HuellaAnterior = "";

        var resultado = VerificadorCadena.Verificar(cadena);

        Assert.True(resultado.Coherente);
    }

    [Fact]
    public void PA1595_SeñalaSoloElPrimerRegistroIncoherente()
    {
        var cadena = Cadena(4);
        cadena[1].ImporteTotal += 1m;
        cadena[3].ImporteTotal += 1m;

        var resultado = VerificadorCadena.Verificar(cadena);

        Assert.Equal(2, resultado.Incoherencia!.Secuencia);
        Assert.Equal(2, resultado.Revisados);
    }

    [Fact]
    public void PA1592_RegistroDeOtroAutonomoMezclado_SeDetecta()
    {
        var cadena = Cadena(2);
        cadena[1].AutonomoId = Guid.NewGuid();

        var resultado = VerificadorCadena.Verificar(cadena);

        Assert.Equal(VerificadorCadena.OtroAutonomo, resultado.Incoherencia!.Codigo);
    }

    private static List<RegistroFacturacion> Cadena(int longitud)
    {
        var cadena = new List<RegistroFacturacion>();
        var inicio = Utc("2026-06-16T13:24:01Z");
        for (var i = 1; i <= longitud; i++)
            cadena.Add(Registro(i, cadena.LastOrDefault(), $"F-2026-{i:D4}", new DateOnly(2026, 6, 16), 21m * i, 121m * i, inicio.AddDays(i - 1)));
        return cadena;
    }

    private static RegistroFacturacion Registro(
        int secuencia, RegistroFacturacion? anterior, string numero, DateOnly fecha, decimal cuota, decimal total, DateTimeOffset instante)
    {
        var registro = new RegistroFacturacion
        {
            Id = Guid.NewGuid(),
            AutonomoId = AutonomoId,
            FacturaId = Guid.NewGuid(),
            Secuencia = secuencia,
            Tipo = TipoRegistroFacturacion.ALTA,
            IdEmisorFactura = "52341234R",
            NumSerieFactura = numero,
            FechaExpedicionFactura = fecha,
            TipoFactura = HuellaVerifactu.TipoFacturaCompleta,
            CuotaTotal = cuota,
            ImporteTotal = total,
            Generado = HuellaVerifactu.TruncarASegundos(instante),
            FechaHoraHusoGenRegistro = HuellaVerifactu.FormatearFechaHoraHuso(instante),
            AnteriorId = anterior?.Id,
            HuellaAnterior = anterior?.Huella,
            Huella = ""
        };
        registro.Huella = HuellaVerifactu.Calcular(DatosHuellaAlta.De(registro));
        return registro;
    }

    private static DateTimeOffset Utc(string iso) =>
        DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
