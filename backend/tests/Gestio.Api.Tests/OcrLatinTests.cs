using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Services;
using Microsoft.Extensions.Configuration;
using SkiaSharp;

namespace Gestio.Api.Tests;

public sealed class OcrLatinTests
{
    private static PalabraDocumento P(string texto, double x, double y, double confianza = 95, int? linea = null) =>
        new(texto,x,y,.1,.02,confianza,linea);
    private static LecturaDocumento L(string variante, params PalabraDocumento[] palabras) => new(1,"pagina",variante,false,palabras);
    private static ExtraccionIa Extraer(params LecturaDocumento[] lecturas) => ExtractorCampos.Extraer(
        new TextoDocumento(DisposicionDocumento.Texto(lecturas[0].Palabras),"prueba",lecturas),Guid.NewGuid());

    [Fact]
    public void AcuerdoDeDosMotores_NoSeAnulaPorUnaSolaSegmentacionErronea()
    {
        var e=Extraer(L("ppocrv5-latin/color",P("TOTAL",.1,.8),P("225,70",.4,.8)),
            L("ppocrv5-latin/gris",P("TOTAL",.1,.8),P("225,70",.4,.8)),
            L("umbral/psm-3",P("TOTAL",.1,.8),P("225,70",.4,.8)),
            L("rectificada/psm-11",P("TOTAL",.1,.8),P("225",.4,.8)));
        Assert.Equal(225.70m,e.Total);
    }

    [Fact]
    public void CiudadAntesDelNif_NoCompiteConUnaRazonSocialLeidaCompleta()
    {
        var e=ExtractorCampos.Extraer("COMERCIO NORTE S.L.\n12345\nCIUDAD\nNIF: B98979651",Guid.NewGuid());
        Assert.Equal("COMERCIO NORTE S.L.",e.EmisorNombre);
    }

    [Fact]
    public void RazonSocialYCifEnLaMismaLineaDelPie_SeSeparan()
    {
        var e=ExtractorCampos.Extraer("Fecha 01/02/2024\nTOTAL 121,00\nComercio Norte S.L. CIF: B-98979651",Guid.NewGuid());
        Assert.Equal("Comercio Norte S.L.",e.EmisorNombre);
        Assert.Equal("B98979651",e.EmisorNif);
    }

    [Fact]
    public void IdentificadorFiscalEnLineaLegal_ExigeControlValidoSiSuEtiquetaEsIlegible()
    {
        Assert.Equal("B98979651",ExtractorCampos.Extraer("Comercio Norte S.L. CF: B-98979651",Guid.NewGuid()).EmisorNif);
        Assert.Null(ExtractorCampos.Extraer("Comercio Norte S.L. CF: B-98979652",Guid.NewGuid()).EmisorNif);
    }

    [Fact]
    public void LineasDelMotor_NoMezclanNombreConDireccionQueSolapaSuCaja()
    {
        LecturaDocumento Lectura(string variante) => L(variante,
            P("COMERCIO",.1,.04,linea:0),P("NORTE",.25,.03,linea:0),P("S.L.",.4,.02,linea:0),
            P("Calle",.1,.055,linea:1),P("129",.4,.04,linea:1),P("NIF:",.1,.12,linea:2),P("B98979651",.3,.12,linea:2));
        var e=Extraer(Lectura("latin/color"),Lectura("latin/gris"));
        Assert.Equal("COMERCIO NORTE S.L.",e.EmisorNombre);
    }

    [Fact]
    public void NumeroBajoSuColumna_NoSeConfundeConPrecioNiMesa()
    {
        var e=Extraer(L("latin/gris",P("MESA",.1,.2),P("12345",.1,.23),
            P("FACTURA",.5,.2),P("54321",.55,.225),P("8.75",.6,.25)));
        Assert.Equal("54321",e.NumeroFactura);
        Assert.Null(Extraer(L("latin/gris",P("FACTURA",.5,.2),P("8.75",.6,.23))).NumeroFactura);
    }

    [Fact]
    public void FilasDeIvaParciales_SeComplementanSinCalcularBases()
    {
        var e=Extraer(L("latin/color",P("TAX/IVA",.1,.7),P("10%",.4,.7),P("8,00",.7,.7)),
            L("latin/gris",P("TAX/IVA",.1,.7),P("10%",.4,.7),P("8,00",.7,.7),
                P("TAX/IVA",.1,.75),P("21%",.4,.75),P("4,20",.7,.75)));
        Assert.Equal(new[] {new DesgloseIva(null,10,8),new DesgloseIva(null,21,4.2m)},e.DesglosesIva);
        Assert.Null(e.BaseImponible); Assert.Null(e.TipoIva); Assert.Null(e.CuotaIva);
    }

    [Fact]
    public void NombreConEspaciosYPuntuacionDistintos_ConservaUnaLecturaLiteral()
    {
        var e=Extraer(L("latin/color",P("COMERCIONORTE",.1,.1),P("S.L.",.4,.1)),
            L("latin/gris",P("COMERCIO",.1,.1),P("NORTE",.25,.1),P("S.L.",.4,.1)));
        Assert.Equal("COMERCIO NORTE S.L.",e.EmisorNombre);
    }

    [Fact]
    public void DosLecturasCompletasFiables_NoSeAnulanPorUnaRegionDebil()
    {
        var regional=new LecturaDocumento(1,"linea","gris/psm-7",true,
            [P("TOTAL",.1,.8,60),P("28,00",.4,.8,60)],new(0,.79,1,.04));
        var e=Extraer(L("latin/color",P("TOTAL",.1,.8),P("20,00",.4,.8)),
            L("latin/gris",P("TOTAL",.1,.8),P("20,00",.4,.8)),regional);
        Assert.Equal(20,e.Total);
    }

    [Fact]
    public async Task MotorLocal_AbreModelosPublicadosYNoInventaTextoEnImagenBlanca()
    {
        var ruta=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".png");
        try
        {
            using var imagen=new SKBitmap(200,200); imagen.Erase(SKColors.White);
            using(var datos=imagen.Encode(SKEncodedImageFormat.Png,100))
            using(var archivo=File.Create(ruta)) datos.SaveTo(archivo);
            using var motor=new MotorOcrLatin(new ConfigurationBuilder().Build());
            var lectura=await motor.LeerAsync(ruta,1,"blanca","original",CancellationToken.None);
            Assert.Empty(lectura.Palabras);
        }
        finally { File.Delete(ruta); }
    }
}
