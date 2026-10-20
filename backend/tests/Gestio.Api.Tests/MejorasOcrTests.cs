using System.Text.Json;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Services;
using SkiaSharp;

namespace Gestio.Api.Tests;

public sealed class MejorasOcrTests
{
    [Fact]
    public void NifReleidoConConfianzaBaja_ExigeControlYDosPreparacionesDistintas()
    {
        var original = new LecturaDocumento(1,"pagina","gris/psm-3",false,
            [new("CIF:",.1,.1,.1,.02,95),new("A12345678",.3,.1,.2,.02,15)]);
        LecturaDocumento Region(string zona,string variante,string nif) => new(1,zona,variante,true,
            [new(nif,.3,.1,.2,.02,30)],new(.28,.09,.25,.04));
        var e = ExtractorCampos.Extraer(new TextoDocumento("CIF: A12345678","prueba",[original,
            Region("palabra","gris/psm-8","12345678Z"),Region("linea","umbral/psm-7","12345678Z")]),Guid.NewGuid());
        Assert.Equal("12345678Z",e.EmisorNif);
        var sinControl = ExtractorCampos.Extraer(new TextoDocumento("CIF: A12345678","prueba",[original,
            Region("palabra","gris/psm-8","12345678A"),Region("linea","umbral/psm-7","12345678A")]),Guid.NewGuid());
        Assert.Null(sinControl.EmisorNif);
    }

    [Fact]
    public void TotalFiableConcordante_NoSePierdePorUnaLecturaDebilDistinta()
    {
        LecturaDocumento Lectura(string variante,string total,double confianza) => new(1,"pagina",variante,false,
            [new("TOTAL",.1,.8,.1,.03,95),new(total,.4,.8,.1,.03,confianza)]);
        var e=ExtractorCampos.Extraer(new TextoDocumento("TOTAL 20,00","prueba",[
            Lectura("gris/psm-3","20,00",95),Lectura("gris/psm-6","20,00",80),Lectura("umbral/psm-3","28,00",45)]),Guid.NewGuid());
        Assert.Equal(20m,e.Total);
    }

    [Fact]
    public void NumeroAbreviadoYNifSinEtiqueta_SeLeenLiteralmente()
    {
        var e=ExtractorCampos.Extraer("Comercio\nB98979651\nFac. Simplif: 00000/00001/2026/T002079\nTOTAL IVA INC.....350,00",Guid.NewGuid());
        Assert.Equal("B98979651",e.EmisorNif);
        Assert.Equal("00000/00001/2026/T002079",e.NumeroFactura);
        Assert.Equal(350m,e.Total);
        Assert.Null(e.TipoIva);
    }

    [Fact]
    public void VariosTiposEtiquetados_ConservanCuotasSinInventarBases()
    {
        var e = ExtractorCampos.Extraer("Base imponible 200,00\nTAX/IVA 10% 10,00\nTAX/IVA 21% 21,00\nTOTAL 231,00", Guid.NewGuid());
        Assert.Equal(200m, e.BaseImponible);
        Assert.Null(e.TipoIva); Assert.Null(e.CuotaIva);
        Assert.Equal(new[] { new DesgloseIva(null,10,10), new DesgloseIva(null,21,21) }, e.DesglosesIva);
        Assert.Empty(e.Advertencias);
    }

    [Fact]
    public void TablaConVariosIva_ConservaCadaFilaSinSumarBases()
    {
        var e = ExtractorCampos.Extraer("BASE IVA CUOTA\n100,00 21% 21,00\n50,00 10% 5,00\nTOTAL 176,00", Guid.NewGuid());
        Assert.Null(e.BaseImponible); Assert.Null(e.TipoIva); Assert.Null(e.CuotaIva);
        Assert.Equal(new[] { new DesgloseIva(100,21,21), new DesgloseIva(50,10,5) }, e.DesglosesIva);
    }

    [Fact]
    public void AvisoDeImportesIncoherentes_NoCambiaNiCompletaLoLeido()
    {
        var e = ExtractorCampos.Extraer("Base imponible 100,00\nCuota IVA 21,00\nTotal 130,00", Guid.NewGuid());
        Assert.Equal(130m, e.Total); Assert.Single(e.Advertencias);
        var parcial = ExtractorCampos.Extraer("Base imponible 100,00\nIVA 21% 21,00", Guid.NewGuid());
        Assert.Null(parcial.Total); Assert.Empty(parcial.Advertencias);
    }

    [Fact]
    public void NifIncorrecto_SeSeñalaSinCorregirlo()
    {
        var e = ExtractorCampos.Extraer("Emisor: Empresa SL\nNIF: 12345678A", Guid.NewGuid());
        Assert.Equal("12345678A", e.EmisorNif); Assert.Single(e.Advertencias);
        Assert.Empty(ExtractorCampos.Extraer("NIF: 12345678Z", Guid.NewGuid()).Advertencias);
    }

    [Fact]
    public void DesglosesYAdvertencias_SeRecuperanDelJsonPersistido_YDelHistoricoAntiguo()
    {
        var filas = new[] { new DesgloseIva(null,10,4.25m), new DesgloseIva(100,21,21) };
        var e = new ExtraccionIa { DatosCrudosJson = JsonSerializer.Serialize(new { desglosesIva = filas, advertencias = new[] { "Comprobar" } }) };
        Assert.Equal(filas, e.DesglosesIva); Assert.Equal("Comprobar", Assert.Single(e.Advertencias));
        var antigua = new ExtraccionIa { DatosCrudosJson = "{\"texto\":\"Total 10,00\"}" };
        Assert.Empty(antigua.DesglosesIva); Assert.Empty(antigua.Advertencias);
    }

    [Fact]
    public void PendienteDeCobro_NoReemplazaUnTotalQueNoSeHaPodidoLeer()
    {
        var e = ExtractorCampos.Extraer("TO TAL EUROS: ilegible\nPENDIENTE DE COBRO 45,05", Guid.NewGuid());
        Assert.Null(e.Total);
    }

    [Fact]
    public void RegionesDeOtraGeometria_NoSeSuperponenEnLaFotoOriginal()
    {
        var original = new LecturaDocumento(1,"pagina","gris",false,
            [new("TOTAL",.1,.8,.1,.03,95),new("20,00",.4,.8,.1,.03,95)]);
        var region = new LecturaDocumento(1,"linea","umbral",true,
            [new("99,00",.4,.8,.1,.03,95)],new(.3,.75,.3,.1), Geometria:"corregida");
        var e = ExtractorCampos.Extraer(new TextoDocumento("TOTAL 20,00","prueba",[original,region]),Guid.NewGuid());
        Assert.Equal(20m,e.Total);
    }

    [Fact]
    public void Inclinacion_SeDetectaYDisminuyeAlEnderezar()
    {
        using var imagen = new SKBitmap(600,800);
        using (var canvas = new SKCanvas(imagen))
        using (var negro = new SKPaint { Color = SKColors.Black })
        {
            canvas.Clear(SKColors.White);
            for (var y=100;y<700;y+=35)
            for (var x=80;x<520;x+=18) canvas.DrawRect(x,y,9,12,negro);
        }
        using var torcida = PreprocesadorDocumento.Girar(imagen,4);
        var detectado = PreprocesadorDocumento.DetectarInclinacion(torcida);
        Assert.InRange(detectado,3.5,4.5);
        using var corregida = PreprocesadorDocumento.Girar(torcida,-detectado);
        Assert.InRange(Math.Abs(PreprocesadorDocumento.DetectarInclinacion(corregida)),0,.5);
    }

    [Fact]
    public void Perspectiva_SoloSeRectificaConBordesEstables()
    {
        using var imagen = new SKBitmap(400,600);
        using (var canvas = new SKCanvas(imagen))
        using (var papel = new SKPaint { Color=SKColors.White })
        using (var poligono = new SKPath())
        {
            canvas.Clear(SKColors.DarkGray);
            poligono.MoveTo(30,0); poligono.LineTo(370,0); poligono.LineTo(330,600); poligono.LineTo(70,600); poligono.Close();
            canvas.DrawPath(poligono,papel);
        }
        using var corregida = PreprocesadorDocumento.Rectificar(imagen);
        Assert.True(corregida.GetPixel(10,300).Red > 240);
        using var blanca = new SKBitmap(200,200); blanca.Erase(SKColors.White);
        using var intacta = PreprocesadorDocumento.Rectificar(blanca);
        Assert.Equal(blanca.Bytes,intacta.Bytes);
    }

    [Fact]
    public void Umbral_ConservaTintaSobreFondoConSombra()
    {
        using var imagen = new SKBitmap(200,200);
        using (var canvas = new SKCanvas(imagen))
        using (var tinta = new SKPaint { Color=SKColors.Black })
        { canvas.Clear(new SKColor(170,170,170)); canvas.DrawRect(80,80,8,30,tinta); }
        using var salida = PreprocesadorDocumento.UmbralAdaptativo(imagen);
        Assert.Equal(SKColors.White,salida.GetPixel(20,20)); Assert.Equal(SKColors.Black,salida.GetPixel(83,90));
    }
}
