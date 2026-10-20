using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Services;

namespace Gestio.Api.Tests;

public sealed class ExtractorDisposicionTests
{
    private static PalabraDocumento P(string texto, double x, double y, double ancho = .08, double confianza = 95) =>
        new(texto, x, y, ancho, .02, confianza);

    private static LecturaDocumento Completa(params PalabraDocumento[] palabras) => new(1, "pagina", "psm-3", false, palabras);
    private static LecturaDocumento Region(string modo, double y, params PalabraDocumento[] palabras) =>
        new(1, "linea", modo, true, palabras, new RegionDocumento(0, y - .005, 1, .035));
    private static TextoDocumento Documento(params LecturaDocumento[] lecturas) =>
        new(DisposicionDocumento.Texto(lecturas[0].Palabras), "motor-prueba", lecturas);

    [Fact]
    public void AsociaColumnas_YTotalEnLaMismaFilaDeLasCabeceras()
    {
        var lectura = Completa(P("BASE", .05, .70), P("IMP", .14, .70), P("IVA", .25, .70),
            P("CUOTA", .45, .70), P("TOTAL", .7, .70), P("92,95", .82, .70),
            P("84,50", .05, .74), P("10%", .25, .74), P("8,45", .45, .74));
        var e = ExtractorCampos.Extraer(Documento(lectura), Guid.NewGuid());
        Assert.Equal(84.50m, e.BaseImponible);
        Assert.Equal(10m, e.TipoIva);
        Assert.Equal(8.45m, e.CuotaIva);
        Assert.Equal(92.95m, e.Total);
        Assert.Null(e.NivelConfianza);
    }

    [Fact]
    public void TextoNativoPdf_ConservaPosicionesSinInventarConfianza()
    {
        var palabras = new[] { P("BASE", .05, .70), P("IVA", .25, .70), P("CUOTA", .45, .70),
            P("TOTAL", .7, .70), P("92,95", .82, .70), P("84,50", .05, .74), P("10%", .25, .74), P("8,45", .45, .74) }
            .Select(p => p with { Confianza = null }).ToArray();
        var lectura = new LecturaDocumento(1, "pagina", "texto-pdf", false, palabras, TextoNativo: true);
        var e = ExtractorCampos.Extraer(Documento(lectura), Guid.NewGuid());
        Assert.Equal(84.50m, e.BaseImponible);
        Assert.Equal(92.95m, e.Total);
        Assert.Null(e.NivelConfianza);
        Assert.All(lectura.Palabras, p => Assert.Null(p.Confianza));
    }

    [Fact]
    public void NumeroDebajoDeFactura_NoConfundeFechaEnOtraColumna()
    {
        var e = ExtractorCampos.Extraer(Documento(Completa(P("FACT.", .05, .10), P("SIMPLIFICADA", .15, .10),
            P("FECHA", .65, .10), P("12/03/2025", .78, .10), P("T029/9412", .05, .13))), Guid.NewGuid());
        Assert.Equal("T029/9412", e.NumeroFactura);
        Assert.Equal(new DateOnly(2025, 3, 12), e.FechaEmision);
    }

    [Fact]
    public void RelecturasConcordantesRecuperanNif_SinSustituirCaracteres()
    {
        var pagina = Completa(P("CIF:", .05, .1), P("876543210", .2, .1));
        Assert.Null(ExtractorCampos.Extraer(Documento(pagina), Guid.NewGuid()).EmisorNif);
        var e = ExtractorCampos.Extraer(Documento(pagina,
            Region("psm-7", .1, P("CIF:", .05, .1), P("B76543210", .2, .1)),
            Region("psm-13", .1, P("CIF:", .05, .1), P("B76543210", .2, .1))), Guid.NewGuid());
        Assert.Equal("B76543210", e.EmisorNif);
    }

    [Fact]
    public void RelecturasContradictorias_DejanCampoNull()
    {
        var e = ExtractorCampos.Extraer(Documento(Completa(P("TOTAL", .5, .8), P("70,00", .8, .8)),
            Region("psm-7", .8, P("TOTAL", .5, .8), P("78,00", .8, .8)),
            Region("psm-13", .8, P("TOTAL", .5, .8), P("79,00", .8, .8))), Guid.NewGuid());
        Assert.Null(e.Total);
    }

    [Fact]
    public void TotalSeLeeDeLaZona_NoSeCalculaConBaseYCuota()
    {
        var e = ExtractorCampos.Extraer(Documento(Completa(P("Base imponible:", .05, .5), P("100,00", .4, .5),
            P("Cuota IVA:", .05, .6), P("21,00", .4, .6), P("TOTAL", .5, .8), P("110,00", .8, .8, confianza: 55)),
            Region("psm-7", .8, P("TOTAL", .5, .8), P("120,00", .8, .8)),
            Region("psm-13", .8, P("TOTAL", .5, .8), P("120,00", .8, .8))), Guid.NewGuid());
        Assert.Equal(120m, e.Total);
    }

    [Fact]
    public void ContradiccionEntreLecturasFiables_NoSeResuelvePorMayoria()
    {
        var e = ExtractorCampos.Extraer(Documento(Completa(P("TOTAL", .5, .8), P("70,00", .8, .8)),
            Region("psm-7", .8, P("TOTAL", .5, .8), P("78,00", .8, .8)),
            Region("psm-13", .8, P("TOTAL", .5, .8), P("78,00", .8, .8))), Guid.NewGuid());
        Assert.Null(e.Total);
    }

    [Fact]
    public void PorClienteNoEsUnBloqueDeDestinatario()
    {
        var e = ExtractorCampos.Extraer(Documento(Completa(P("Por", .05, .5), P("cliente", .15, .5), P("25,00", .5, .5),
            P("Empresa", .05, .7), P("SL", .2, .7), P("NIF:", .05, .75), P("B76543210", .2, .75))), Guid.NewGuid());
        Assert.Equal("B76543210", e.EmisorNif);
    }

    [Fact]
    public void EmisorYClienteEnColumnasParalelas_NoSeMezclan()
    {
        var pagina = Completa(P("Emisor:", .05, .1), P("Empresa", .2, .1), P("SL", .32, .1, .04),
            P("Cliente:", .62, .1), P("ClienteSL", .8, .1),
            P("NIF:", .05, .14), P("B76543210", .2, .14), P("NIF:", .62, .14), P("B00000000", .8, .14));
        var e = ExtractorCampos.Extraer(Documento(pagina), Guid.NewGuid());
        Assert.Equal("Empresa SL", e.EmisorNombre);
        Assert.Equal("B76543210", e.EmisorNif);
    }

    [Fact]
    public void RegionalDelCliente_NoReemplazaElNifDelEmisor()
    {
        var pagina = Completa(P("NIF:", .05, .1), P("B76543210", .2, .1),
            P("Cliente:", .05, .4), P("Persona", .3, .4), P("NIF:", .05, .45), P("B00000000", .2, .45));
        var e = ExtractorCampos.Extraer(Documento(pagina,
            Region("psm-7", .45, P("NIF:", .05, .45), P("B11111111", .2, .45)),
            Region("psm-13", .45, P("NIF:", .05, .45), P("B11111111", .2, .45))), Guid.NewGuid());
        Assert.Equal("B76543210", e.EmisorNif);
    }

    [Fact]
    public void VariasFilasFiscales_NoSeEligeUnaFilaNiSeSuman()
    {
        var e = ExtractorCampos.Extraer(Documento(Completa(P("BASE", .05, .7), P("IVA", .25, .7), P("CUOTA", .45, .7),
            P("100,00", .05, .73), P("21%", .25, .73), P("21,00", .45, .73),
            P("50,00", .05, .76), P("10%", .25, .76), P("5,00", .45, .76))), Guid.NewGuid());
        Assert.Null(e.BaseImponible);
        Assert.Null(e.TipoIva);
        Assert.Null(e.CuotaIva);
    }
}
