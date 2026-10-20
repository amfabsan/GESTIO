using Gestio.Api.Services;

namespace Gestio.Api.Tests;

public sealed class ExtractorCamposTests
{
    [Fact]
    public void DireccionEnCabecera_NoSeProponeComoEmisor()
    {
        var e = ExtractorCampos.Extraer("Ci) Calle del Puerto\nRestaurante\nTel: 900000000\nTOTAL 35,00", Guid.NewGuid());
        Assert.Null(e.EmisorNombre);
    }

    [Fact]
    public void TablaFiscal_FechaEnCabecera_YNombreLegalSinEtiqueta()
    {
        var e = ExtractorCampos.Extraer("Servicios Delta S.L.\nCIF: B76543210\nFactura Nº A-901 Fecha: 03/02/2025\nCliente: Elena\nNIF: 12345678A\nBASE %IVA IMP.IVA\n80,00 10,00 8,00\nTOTAL EUROS: 88,00\nENTREGADO 100,00\nCAMBIO 12,00", Guid.NewGuid());
        Assert.Equal("Servicios Delta S.L.", e.EmisorNombre);
        Assert.Equal("B76543210", e.EmisorNif);
        Assert.Equal("A-901", e.NumeroFactura);
        Assert.Equal(new DateOnly(2025, 2, 3), e.FechaEmision);
        Assert.Equal(80m, e.BaseImponible);
        Assert.Equal(10m, e.TipoIva);
        Assert.Equal(8m, e.CuotaIva);
        Assert.Equal(88m, e.Total);
    }

    [Fact]
    public void ImportesYTotalEnLineasSeparadas_YFechaSinEtiqueta()
    {
        var e = ExtractorCampos.Extraer("Compra 7/4/24, 12:30\nBase imponible\n1.234,56 EUR\nIVA 21%: 259,26\nTOTAL A PAGAR\n1.493,82 €", Guid.NewGuid());
        Assert.Equal(new DateOnly(2024, 4, 7), e.FechaEmision);
        Assert.Equal(1234.56m, e.BaseImponible);
        Assert.Equal(21m, e.TipoIva);
        Assert.Equal(259.26m, e.CuotaIva);
        Assert.Equal(1493.82m, e.Total);
    }

    [Fact]
    public void VariosTiposDeIva_NoSeSumanNiSeEligeUnaFila()
    {
        var e = ExtractorCampos.Extraer("BASE TIPO IVA CUOTA\n100,00 21% 21,00\n50,00 10% 5,00\nTOTAL 176,00", Guid.NewGuid());
        Assert.Null(e.BaseImponible);
        Assert.Null(e.TipoIva);
        Assert.Null(e.CuotaIva);
        Assert.Equal(176m, e.Total);
    }

    [Fact]
    public void NoUsaElClienteComoEmisor_NiVencimientoComoEmision()
    {
        var e = ExtractorCampos.Extraer("FACTURA\nCliente: Empresa Cliente SL\nNIF: B00000000\nFecha vencimiento: 05/12/2026\nTotal: 27,00", Guid.NewGuid());
        Assert.Null(e.EmisorNombre);
        Assert.Null(e.EmisorNif);
        Assert.Null(e.FechaEmision);
    }

    [Fact]
    public void TextoSinCamposNoEsUnaExtraccionUtil()
    {
        Assert.Equal(0, ExtractorCampos.ContarDatos(ExtractorCampos.Extraer("Texto sin información fiscal identificable", Guid.NewGuid())));
    }

    [Fact]
    public void TotalConIva_NoSeConfundeConImportePorCliente()
    {
        var datos = ExtractorCampos.Extraer("TOTAL CON IVA 98,70€\nPor cliente\n32,90€\nTOTAL PENDIENTE 98,70€", Guid.NewGuid());
        Assert.Equal(98.70m, datos.Total);
        Assert.Null(datos.BaseImponible);
        Assert.Null(datos.TipoIva);
        Assert.Null(datos.CuotaIva);
    }

    [Fact]
    public void NormalizaImportes_YNoMezclaCliente()
    {
        var datos = ExtractorCampos.Extraer("Emisor: Comercio SL\nNIF: B12345678\nFactura: F-2026/12\nFecha: 20/10/2026\nBase imponible: 1.000,00\nIVA: 21%\nCuota IVA: 210,00\nTotal: 1.210,00\nCliente: Otro SL\nNIF: B99999999", Guid.NewGuid());
        Assert.Equal("B12345678", datos.EmisorNif);
        Assert.Equal("Comercio SL", datos.EmisorNombre);
        Assert.Equal("F-2026/12", datos.NumeroFactura);
        Assert.Equal(new DateOnly(2026, 10, 20), datos.FechaEmision);
        Assert.Equal(1000m, datos.BaseImponible);
        Assert.Equal(21m, datos.TipoIva);
        Assert.Equal(210m, datos.CuotaIva);
        Assert.Equal(1210m, datos.Total);
        Assert.Null(datos.NivelConfianza);
    }

    [Fact]
    public void AusentesYTotalesAmbiguosQuedanNull()
    {
        var datos = ExtractorCampos.Extraer("Gracias por su compra\nTotal: 12,00\nTotal: 15,00", Guid.NewGuid());
        Assert.Null(datos.Total);
        Assert.Null(datos.BaseImponible);
        Assert.Null(datos.CuotaIva);
        Assert.Null(datos.TipoIva);
        Assert.Null(datos.FechaEmision);
        Assert.Null(datos.EmisorNombre);
        Assert.Null(datos.EmisorNif);
        Assert.Null(datos.NumeroFactura);
    }
}
