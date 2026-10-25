using System.Text.Json;
using Gestio.Api.Common;
using Gestio.Api.Controllers;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Models.Requests;
using Gestio.Api.Models.Responses;
using Gestio.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gestio.Api.Tests;

// PB-10: avisos por datos ausentes o importes incoherentes (PA 1534 y 1535).
public sealed class AnomaliasTests
{
    private static readonly byte[] PdfValido = "%PDF-1.7\nContenido de prueba"u8.ToArray();
    private static readonly UsuarioActual Ramon = new(DbInitializer.RamonId, Rol.AUTONOMO);
    private static readonly UsuarioActual Pedro = new(DbInitializer.PedroId, Rol.GESTOR);

    // Factura completa sin el NIF del emisor: lo que ve el gestor tras el procesamiento.
    private const string FacturaSinNif = "Servicios Delta S.L.\nFactura Nº A-901 Fecha: 03/02/2025\nBASE %IVA IMP.IVA\n80,00 10,00 8,00\nTOTAL EUROS: 88,00";

    // ---- Reglas (requisitos 2 a 5) ----

    [Theory]
    [InlineData("FACTURA Nº 12", true)]
    [InlineData("Factura simplificada Nº 12", false)]
    [InlineData("TICKET 0012\nGracias por su visita", false)]
    [InlineData("Recibo de entrega", false)]
    [InlineData(null, false)]
    public void NifAusente_SoloEnFacturas(string? texto, bool esperaAnomalia)
    {
        var detectadas = DetectorAnomalias.Detectar(Extraccion(texto, nif: null));

        if (esperaAnomalia)
        {
            var anomalia = Assert.Single(detectadas);
            Assert.Equal(TipoAnomalia.NIF_AUSENTE, anomalia.Tipo);
            Assert.Equal(nameof(ExtraccionIa.EmisorNif), anomalia.Campo);
            Assert.Contains("NIF del emisor", anomalia.Descripcion);
        }
        else
        {
            Assert.Empty(detectadas);
        }

        Assert.Empty(DetectorAnomalias.Detectar(Extraccion(texto, nif: "B12345678")));
    }

    // base × tipo / 100 redondeado a céntimos; solo una diferencia SUPERIOR a 0,01 € es anomalía.
    [Theory]
    [InlineData(100.00, 21.0, 21.00, false)]
    [InlineData(100.00, 21.0, 21.01, false)]
    [InlineData(100.00, 21.0, 21.02, true)]
    [InlineData(82.64, 21.0, 17.35, false)]
    [InlineData(10.05, 10.0, 1.01, false)]
    [InlineData(100.00, 21.0, 10.00, true)]
    public void CuotaDeIva_SeComparaConBasePorTipo(double baseImponible, double tipo, double cuota, bool esperaAnomalia)
    {
        var extraccion = Extraccion("FACTURA", baseImponible: (decimal)baseImponible, tipoIva: (decimal)tipo, cuotaIva: (decimal)cuota);

        var detectadas = DetectorAnomalias.Detectar(extraccion);

        Assert.Equal(esperaAnomalia, detectadas.Any(a => a.Campo == nameof(ExtraccionIa.CuotaIva)));
        if (esperaAnomalia)
            Assert.Equal(TipoAnomalia.DESCUADRE_ARITMETICO, detectadas.Single(a => a.Campo == nameof(ExtraccionIa.CuotaIva)).Tipo);
    }

    [Theory]
    [InlineData(121.00, false)]
    [InlineData(121.01, false)]
    [InlineData(120.99, false)]
    [InlineData(121.02, true)]
    [InlineData(130.00, true)]
    public void Total_SeComparaConBaseMasIva(double total, bool esperaAnomalia)
    {
        var extraccion = Extraccion("FACTURA", baseImponible: 100m, cuotaIva: 21m, total: (decimal)total);

        var detectadas = DetectorAnomalias.Detectar(extraccion);

        Assert.Equal(esperaAnomalia, detectadas.Any(a => a.Campo == nameof(ExtraccionIa.Total)));
    }

    [Theory]
    [InlineData("FACTURA\nRetención IRPF 15%: -15,00")]
    [InlineData("FACTURA\nRecargo de equivalencia 5,2%")]
    public void Total_NoSeComprueba_ConConceptosQueAlteranElTotal(string texto)
    {
        var extraccion = Extraccion(texto, baseImponible: 100m, cuotaIva: 21m, total: 106m);

        Assert.Empty(DetectorAnomalias.Detectar(extraccion));
    }

    [Fact]
    public void ConVariasBases_NoSeCompruebanLosImportes()
    {
        var extraccion = Extraccion("FACTURA", baseImponible: 100m, tipoIva: 21m, cuotaIva: 15m, total: 200m);
        extraccion.DesglosesIva = [new DesgloseIva(50m, 21m, 10.5m), new DesgloseIva(50m, 10m, 5m)];

        Assert.Empty(DetectorAnomalias.Detectar(extraccion));
    }

    // Requisito 5: sin todos los importes no hay comprobación, ni se da por buena ni se completa el dato.
    [Fact]
    public void ConImportesIncompletos_NoSeComprueba()
    {
        Assert.Empty(DetectorAnomalias.Detectar(Extraccion("FACTURA", baseImponible: 100m, total: 999m)));
        Assert.Empty(DetectorAnomalias.Detectar(Extraccion("FACTURA", tipoIva: 21m, cuotaIva: 1m, total: 999m)));
        Assert.Empty(DetectorAnomalias.Detectar(Extraccion("FACTURA", baseImponible: 100m, cuotaIva: 21m)));
    }

    // ---- PA 1534: consulta junto al documento y continuidad de la revisión ----

    [Fact]
    public async Task Pa1534_GestorVeLaAnomaliaJuntoAlDocumento_YAlCorregirseQuedaResuelta()
    {
        await using var context = CrearContexto(nameof(Pa1534_GestorVeLaAnomaliaJuntoAlDocumento_YAlCorregirseQuedaResuelta));
        await DbInitializer.SeedAsync(context);
        var id = await SubirProcesado(context, FacturaSinNif);

        // Tras el procesamiento, el gestor ve el aviso con el original y los campos extraídos.
        var documento = await Consultar(context, Pedro, id);
        Assert.True(documento.TieneOriginal);
        Assert.NotNull(documento.Extraccion);
        Assert.Null(documento.Extraccion.EmisorNif);
        var aviso = Assert.Single(documento.Anomalias);
        Assert.Equal(nameof(TipoAnomalia.NIF_AUSENTE), aviso.Tipo);
        Assert.Equal(nameof(ExtraccionIa.EmisorNif), aviso.Campo);
        Assert.False(aviso.Resuelta);
        Assert.False(string.IsNullOrWhiteSpace(aviso.Descripcion));

        // La corrección del campo elimina la causa: deja de estar pendiente pero se conserva.
        var correccion = await CrearServicio(context).CorregirAsync(Pedro, id,
            [new CampoValidacionRequest("EmisorNif", "B76543210")], CancellationToken.None);
        Assert.True(correccion.EsCorrecto, correccion.Error?.Mensaje);
        context.ChangeTracker.Clear();

        var corregido = await Consultar(context, Pedro, id);
        var registro = Assert.Single(corregido.Anomalias);
        Assert.Equal(aviso.Id, registro.Id);
        Assert.True(registro.Resuelta);
        Assert.Equal(aviso.FechaDeteccion, registro.FechaDeteccion);
    }

    [Fact]
    public async Task Pa1534_LaAnomaliaNoCambiaImportesNiImpideRevisar()
    {
        await using var context = CrearContexto(nameof(Pa1534_LaAnomaliaNoCambiaImportesNiImpideRevisar));
        await DbInitializer.SeedAsync(context);
        var servicio = CrearServicio(context);
        var id = await SubirProcesado(context, FacturaSinNif);

        // Cuota 2 céntimos por encima de la calculada: es una anomalía, pero el gestor puede seguir.
        var correccion = await servicio.CorregirAsync(Pedro, id,
        [
            new CampoValidacionRequest("EmisorNif", "B76543210"),
            new CampoValidacionRequest("BaseImponible", "100,00"),
            new CampoValidacionRequest("TipoIva", "21"),
            new CampoValidacionRequest("CuotaIva", "21,02"),
            new CampoValidacionRequest("Total", "121,02")
        ], CancellationToken.None);
        Assert.True(correccion.EsCorrecto, correccion.Error?.Mensaje);
        context.ChangeTracker.Clear();

        var conAviso = await Consultar(context, Pedro, id);
        var descuadre = Assert.Single(conAviso.Anomalias, a => !a.Resuelta);
        Assert.Equal(nameof(TipoAnomalia.DESCUADRE_ARITMETICO), descuadre.Tipo);
        Assert.Equal(nameof(ExtraccionIa.CuotaIva), descuadre.Campo);
        Assert.Contains("21,00 €", descuadre.Descripcion);

        // Los importes quedan tal como los fijó el gestor: la detección no los toca.
        Assert.Equal(21.02m, conAviso.Extraccion!.CuotaIva);
        Assert.Equal(121.02m, conAviso.Extraccion.Total);

        var aprobacion = await servicio.AprobarAsync(Pedro, id, CancellationToken.None);
        Assert.True(aprobacion.EsCorrecto, aprobacion.Error?.Mensaje);
        Assert.Equal(EstadoDocumento.VALIDADO, aprobacion.Valor!.Estado);
        Assert.Contains(aprobacion.Valor.Anomalias, a => a.Id == descuadre.Id && !a.Resuelta);
    }

    [Fact]
    public async Task Pa1534_UnaCorreccionQueIntroduceUnDescuadreLoDetecta_YAlDeshacerloSeResuelve()
    {
        await using var context = CrearContexto(nameof(Pa1534_UnaCorreccionQueIntroduceUnDescuadreLoDetecta_YAlDeshacerloSeResuelve));
        await DbInitializer.SeedAsync(context);
        var servicio = CrearServicio(context);
        var id = await SubirProcesado(context, FacturaSinNif);

        Assert.True((await servicio.CorregirAsync(Pedro, id,
            [new CampoValidacionRequest("EmisorNif", "B76543210"), new CampoValidacionRequest("CuotaIva", "8,02")],
            CancellationToken.None)).EsCorrecto);
        context.ChangeTracker.Clear();
        Assert.Contains((await Consultar(context, Pedro, id)).Anomalias,
            a => !a.Resuelta && a.Campo == nameof(ExtraccionIa.CuotaIva));

        Assert.True((await servicio.CorregirAsync(Pedro, id,
            [new CampoValidacionRequest("CuotaIva", "8,00")], CancellationToken.None)).EsCorrecto);
        context.ChangeTracker.Clear();

        var anomalias = (await Consultar(context, Pedro, id)).Anomalias;
        Assert.All(anomalias, a => Assert.True(a.Resuelta));
        Assert.Contains(anomalias, a => a.Campo == nameof(ExtraccionIa.CuotaIva));
        Assert.Contains(anomalias, a => a.Campo == nameof(ExtraccionIa.EmisorNif));
    }

    // ---- PA 1535: control de acceso a las anomalías ----

    [Fact]
    public async Task Pa1535_SoloPropietarioYGestorConVinculacionActivaVenLasAnomalias()
    {
        await using var context = CrearContexto(nameof(Pa1535_SoloPropietarioYGestorConVinculacionActivaVenLasAnomalias));
        await DbInitializer.SeedAsync(context);
        var id = await SubirProcesado(context, FacturaSinNif);

        var otroAutonomo = new UsuarioActual(Guid.NewGuid(), Rol.AUTONOMO);
        var otroGestor = new UsuarioActual(Guid.NewGuid(), Rol.GESTOR);
        context.Usuarios.AddRange(
            new Usuario { Id = otroAutonomo.Id, Nombre = "Lucía", PrimerApellido = "Pérez", SegundoApellido = "Gil", Email = "lucia@gestio.local", Nif = "11111111H", Rol = Rol.AUTONOMO },
            new Usuario { Id = otroGestor.Id, Nombre = "Marta", PrimerApellido = "Ruiz", SegundoApellido = "Sanz", Email = "marta@gestio.local", Nif = "22222222J", CodigoAsesoria = "ASE-000999", Rol = Rol.GESTOR });
        await context.SaveChangesAsync();

        Assert.NotEmpty((await Consultar(context, Ramon, id)).Anomalias);
        Assert.NotEmpty((await Consultar(context, Pedro, id)).Anomalias);

        await SinAcceso(context, otroAutonomo, id);
        await SinAcceso(context, otroGestor, id);

        // Sin vinculación ACTIVA el gestor deja de verlas, igual que el resto del documento.
        foreach (var estado in new[] { EstadoVinculacion.PENDIENTE, EstadoVinculacion.FINALIZADA })
        {
            var vinculo = await context.Vinculaciones.FirstAsync(v => v.AutonomoId == Ramon.Id);
            vinculo.Estado = estado;
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            await SinAcceso(context, Pedro, id);
        }
    }

    private static async Task SinAcceso(ApplicationDbContext context, UsuarioActual usuario, Guid id)
    {
        var controlador = CrearControlador(context, usuario);
        Assert.IsType<NotFoundResult>((await controlador.GetDocumento(id, CancellationToken.None)).Result);

        var listado = Assert.IsAssignableFrom<IReadOnlyList<DocumentoResponse>>(
            Assert.IsType<OkObjectResult>((await controlador.GetDocumentos(CancellationToken.None)).Result).Value);
        Assert.DoesNotContain(listado, documento => documento.Id == id);
    }

    private static ExtraccionIa Extraccion(
        string? texto,
        string? nif = "B12345678",
        decimal? baseImponible = null,
        decimal? tipoIva = null,
        decimal? cuotaIva = null,
        decimal? total = null) => new()
    {
        EmisorNif = nif,
        BaseImponible = baseImponible,
        TipoIva = tipoIva,
        CuotaIva = cuotaIva,
        Total = total,
        DatosCrudosJson = texto is null ? null : JsonSerializer.Serialize(new { texto })
    };

    private static async Task<Guid> SubirProcesado(ApplicationDbContext context, string texto)
    {
        using var contenido = new MemoryStream(PdfValido);
        var subida = await CrearServicio(context, texto).SubirAsync(Ramon, "factura.pdf", contenido, PdfValido.Length, CancellationToken.None);
        Assert.True(subida.EsCorrecto, subida.Error?.Mensaje);
        Assert.Equal(EstadoDocumento.PENDIENTE_VALIDACION, subida.Valor!.Estado);
        context.ChangeTracker.Clear();
        return subida.Valor.Id;
    }

    private static async Task<DocumentoResponse> Consultar(ApplicationDbContext context, UsuarioActual usuario, Guid id)
    {
        var resultado = await CrearControlador(context, usuario).GetDocumento(id, CancellationToken.None);
        return Assert.IsType<DocumentoResponse>(Assert.IsType<OkObjectResult>(resultado.Result).Value);
    }

    private static ApplicationDbContext CrearContexto(string nombre) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(nombre).Options);

    private static DocumentosService CrearServicio(ApplicationDbContext context, string texto = FacturaSinNif) =>
        new(context, new AlmacenamientoEnMemoria(),
            Options.Create(new DocumentosOptions { TamanoMaximoBytes = 1024 }), TimeProvider.System,
            NullLogger<DocumentosService>.Instance, new LectorFijo(texto), new AccesoDocumentos(context));

    private static DocumentosController CrearControlador(ApplicationDbContext context, UsuarioActual usuario) =>
        new(CrearServicio(context), new UsuarioActualFijo(usuario));
}

// Devuelve siempre el mismo texto reconocido; no verifica el motor OCR.
file sealed class LectorFijo(string texto) : ILectorDocumento
{
    public Task<TextoDocumento> LeerAsync(Stream original, string tipoMime, CancellationToken cancellationToken) =>
        Task.FromResult(new TextoDocumento(texto, "doble-unitario"));
}

file sealed class UsuarioActualFijo(UsuarioActual usuario) : ICurrentUserService
{
    public Task<UsuarioActual?> ObtenerAsync(CancellationToken cancellationToken) => Task.FromResult<UsuarioActual?>(usuario);
}

file sealed class AlmacenamientoEnMemoria : IFileStorageService
{
    private readonly Dictionary<string, byte[]> archivos = [];

    public async Task<string> GuardarAsync(Stream contenido, Guid usuarioId, string extension, CancellationToken cancellationToken)
    {
        using var copia = new MemoryStream();
        await contenido.CopyToAsync(copia, cancellationToken);
        var clave = $"{usuarioId:N}/{Guid.NewGuid():N}{extension}";
        archivos[clave] = copia.ToArray();
        return clave;
    }

    public Task<Stream?> AbrirAsync(string storageKey, CancellationToken cancellationToken) =>
        Task.FromResult<Stream?>(archivos.TryGetValue(storageKey, out var bytes) ? new MemoryStream(bytes) : null);

    public Task EliminarAsync(string storageKey, CancellationToken cancellationToken)
    {
        archivos.Remove(storageKey);
        return Task.CompletedTask;
    }
}
