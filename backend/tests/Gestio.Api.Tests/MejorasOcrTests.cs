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

    [Fact]
    public void FotoLejana_SeDetectanLasEsquinasDelPapelYSeRecorta()
    {
        using var imagen = new SKBitmap(1200,900);
        SKPoint[] papel = [new(350,200), new(830,240), new(800,760), new(330,720)];
        using (var canvas = new SKCanvas(imagen))
        using (var blanco = new SKPaint { Color=SKColors.White })
        using (var negro = new SKPaint { Color=SKColors.Black })
        using (var poligono = new SKPath())
        {
            canvas.Clear(new SKColor(120,80,50));
            poligono.AddPoly(papel); canvas.DrawPath(poligono,blanco);
            for (var y=300;y<650;y+=30) canvas.DrawRect(420,y,300,10,negro);
        }
        var esquinas = PreprocesadorDocumento.DetectarDocumento(imagen);
        Assert.NotNull(esquinas);
        for (var i=0;i<4;i++) Assert.InRange(SKPoint.Distance(esquinas![i],papel[i]),0,15);
        using var recorte = PreprocesadorDocumento.Recortar(imagen,esquinas!,out var ampliacion);
        Assert.True(ampliacion > 1);
        // Tras recortar no queda fondo marrón en el interior cercano a las esquinas.
        Assert.True(recorte.GetPixel(recorte.Width/20,recorte.Height/20).Blue > 200);
        Assert.True(recorte.GetPixel(recorte.Width*19/20,recorte.Height*19/20).Blue > 200);
    }

    [Fact]
    public void PapelQueLlenaLaFotoOSaleDelEncuadre_NoSeRecorta()
    {
        using var blanca = new SKBitmap(800,600); blanca.Erase(SKColors.White);
        Assert.Null(PreprocesadorDocumento.DetectarDocumento(blanca));
        using var cortada = new SKBitmap(1200,900);
        using (var canvas = new SKCanvas(cortada))
        using (var blanco = new SKPaint { Color=SKColors.White })
        { canvas.Clear(new SKColor(30,30,30)); canvas.DrawRect(400,150,400,750,blanco); }
        Assert.Null(PreprocesadorDocumento.DetectarDocumento(cortada));
    }

    [Fact]
    public void Iluminacion_EliminaLaSombraDeUnPliegue()
    {
        using var imagen = new SKBitmap(600,800);
        using (var canvas = new SKCanvas(imagen))
        using (var sombra = new SKPaint { Color=new SKColor(110,110,110) })
        using (var tinta = new SKPaint { Color=SKColors.Black })
        using (var tintaEnSombra = new SKPaint { Color=new SKColor(40,40,40) })
        {
            canvas.Clear(new SKColor(235,235,235));
            canvas.DrawRect(0,400,600,400,sombra);
            canvas.DrawRect(100,200,200,8,tinta); canvas.DrawRect(100,600,200,8,tintaEnSombra);
        }
        using var salida = PreprocesadorDocumento.NormalizarIluminacion(imagen,out var variacion);
        Assert.True(variacion >= 35);
        Assert.True(salida.GetPixel(500,200).Red > 230); Assert.True(salida.GetPixel(500,700).Red > 230);
        Assert.True(salida.GetPixel(200,604).Red < 128);
    }

    [Theory]
    [InlineData("Fecha\n|Sáb. 04/Mar/2017 | 106 | 4 |",2017,3,4)]
    [InlineData("Fecha: 4 de marzo de 2017",2017,3,4)]
    [InlineData("Emitida el 15-DIC-19",2019,12,15)]
    public void FechaConMesEnLetra_SeLee(string texto,int anio,int mes,int dia)
    {
        Assert.Equal(new DateOnly(anio,mes,dia),ExtractorCampos.Extraer(texto,Guid.NewGuid()).FechaEmision);
    }

    [Fact]
    public void DireccionConNombreDeMes_NoEsUnaFecha()
    {
        Assert.Null(ExtractorCampos.Extraer("DEPORTES ALVARADO\nC/ MAR, 27\nVALENCIA",Guid.NewGuid()).FechaEmision);
    }

    [Fact]
    public void FilaFiscalConCabeceraIlegible_SeReconocePorCoherencia()
    {
        var e = ExtractorCampos.Extraer("Mare nt % hrs bra + Tan Total Foctra\n123.83 10.00 12.38 136.21",Guid.NewGuid());
        Assert.Equal(123.83m,e.BaseImponible); Assert.Equal(10m,e.TipoIva); Assert.Equal(12.38m,e.CuotaIva); Assert.Equal(136.21m,e.Total);
        Assert.Equal("10",e.TipoIva!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void FilaFiscalConCuotaMalLeida_ConservaBaseTipoYTotalSinInventarLaCuota()
    {
        var e = ExtractorCampos.Extraer("123.83 10.00 12.30 136.21",Guid.NewGuid());
        Assert.Equal(123.83m,e.BaseImponible); Assert.Equal(10m,e.TipoIva); Assert.Null(e.CuotaIva); Assert.Equal(136.21m,e.Total);
        // Un céntimo de diferencia (12,39 por 12,38) ya no es coherente: la cuota no se acepta.
        var dudosa = ExtractorCampos.Extraer("123.83 10.00 12.39 136.21",Guid.NewGuid());
        Assert.Equal(123.83m,dudosa.BaseImponible); Assert.Null(dudosa.CuotaIva); Assert.Equal(136.21m,dudosa.Total);
    }

    [Fact]
    public void LineasDeProducto_NoSeConfundenConUnaFilaFiscal()
    {
        var e = ExtractorCampos.Extraer("4 Solen de Cabras 2.60 10.40\n3 Cerveza Mahou 2.70 8.10\n1 Agua 10 11",Guid.NewGuid());
        Assert.Null(e.BaseImponible); Assert.Null(e.TipoIva); Assert.Null(e.CuotaIva); Assert.Null(e.Total);
    }

    [Fact]
    public void NombreSinEtiqueta_EsLaLineaAnteriorALaDireccionAunqueHayaRuidoEncima()
    {
        var e = ExtractorCampos.Extraer("QR tributario:\nVERISFACTU\nERIAFA\nDEPORTES ALVARADO ,\n96 3923034\nC/ MAR, 27\nVALENCIA\nB98979651",Guid.NewGuid());
        Assert.Equal("DEPORTES ALVARADO",e.EmisorNombre);
        Assert.Equal("B98979651",e.EmisorNif);
    }

    [Fact]
    public void DireccionPartidaEnDosLineas_NoSeTomaComoNombre()
    {
        var e = ExtractorCampos.Extraer("JUAN Y ANDREA ho. S.L. 12\nVENDA DE SES SALINES,\n07860 FORMENTERA",Guid.NewGuid());
        Assert.Null(e.EmisorNombre);
    }

    [Fact]
    public void CifConGuionTrasLaLetra_SeLee()
    {
        Assert.Equal("B81205791",ExtractorCampos.Extraer("Rodimar Hostelería S.L. CIF: B-81205791",Guid.NewGuid()).EmisorNif);
    }

    [Fact]
    public void TvaSeReconoceComoEtiquetaDeIva()
    {
        Assert.Equal(new[] { new DesgloseIva(null,10,29.21m) },ExtractorCampos.Extraer("TAX/TVA 10% 29,21",Guid.NewGuid()).DesglosesIva);
    }

    [Fact]
    public void ConfianzaDelNombre_SeMideConSusPalabrasYNoConPalabrasSueltas()
    {
        // "DE" con confianza 5 en otra línea no debe rebajar la del nombre leído al 96 %.
        LecturaDocumento Lectura(string variante) => new(1,"pagina",variante,false,
            [new("DEPORTES",.3,.1,.2,.02,96),new("ALVARADO",.52,.1,.2,.02,97),
             new("96",.3,.13,.05,.02,95),new("3923034",.36,.13,.15,.02,95),new("DE",.3,.6,.05,.02,5)]);
        var e = ExtractorCampos.Extraer(new TextoDocumento("DEPORTES ALVARADO\n96 3923034\nDE","prueba",
            [Lectura("gris/psm-3"),Lectura("umbral/psm-6")]),Guid.NewGuid());
        Assert.Equal("DEPORTES ALVARADO",e.EmisorNombre);
    }

    [Fact]
    public void TotalConLaCifraPartida_NoAportaUnTotalFalso()
    {
        // Una lectura parte "109,70€" en "109," + ruido + "70€": no debe proponer 70 y anular las demás.
        LecturaDocumento Buena(string variante) => new(1,"pagina",variante,false,
            [new("TOTAL",.1,.8,.1,.03,97),new("CON",.22,.8,.06,.03,96),new("IVA",.3,.8,.06,.03,96),new("109,70€",.6,.8,.15,.03,93)]);
        var partida = new LecturaDocumento(1,"pagina","umbral/psm-11",false,
            [new("TOTAL",.1,.8,.1,.03,97),new("PENDIENTE",.22,.8,.2,.03,97),new("109,",.6,.8,.06,.03,97),
             new("Arado",.67,.8,.06,.03,0),new("70€",.74,.8,.06,.03,97)]);
        var e = ExtractorCampos.Extraer(new TextoDocumento("TOTAL CON IVA 109,70€","prueba",
            [Buena("gris/psm-6"),Buena("rectificada/psm-6"),partida]),Guid.NewGuid());
        Assert.Equal(109.70m,e.Total);
    }

    [Fact]
    public void PalabraMalLeidaBajoFactura_NoSeTomaComoNumero()
    {
        LecturaDocumento Lectura(string debajo) => new(1,"pagina","gris/psm-3",false,
            [new("FACTURA",.3,.3,.2,.02,95),new(debajo,.3,.33,.1,.02,95)]);
        Assert.Null(ExtractorCampos.Extraer(new TextoDocumento("FACTURA\nPreci0","prueba",[Lectura("Preci0")]),Guid.NewGuid()).NumeroFactura);
        Assert.Equal("A19",ExtractorCampos.Extraer(new TextoDocumento("FACTURA\nA19","prueba",[Lectura("A19")]),Guid.NewGuid()).NumeroFactura);
    }
}
