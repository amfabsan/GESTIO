using System.Reflection;
using System.Text.Json;
using Gestio.Api.Controllers;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Models.Request;
using Gestio.Api.Models.Response;
using Gestio.Api.Services;
using Gestio.Api.Services.Verifactu;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gestio.Api.Tests;

/// <summary>UT23 (PB-23), capa 3: API pública del QR y QR en el detalle de la factura.</summary>
public sealed class FacturasPublicasControllerTests
{
    private static readonly Guid AutonomoId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ContactoId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly UsuarioActual Propietario = new(AutonomoId, Rol.AUTONOMO);

    // ─── Endpoint público ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PA1605_SinSesion_DevuelveLosDatosMinimosYElAviso()
    {
        await using var db = await CrearDbAsync();
        var (_, codigo) = await EmitirAsync(db);

        var resultado = await Publico(db).Consultar(codigo, default);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var factura = Assert.IsType<FacturaPublicaResponse>(ok.Value);
        Assert.Equal("Ramón García Pérez", factura.EmisorNombre);
        Assert.Equal("F-2026-0001", factura.SerieNumero);
        Assert.Equal(new DateOnly(2026, 6, 5), factura.Fecha);
        Assert.Equal(2800.00m, factura.Total);
        Assert.Equal("Demostración académica: factura no cotejable en la AEAT", factura.Aviso); // texto literal del req. 3
    }

    [Fact]
    public async Task PA1607_ElJsonPublico_SoloTieneLosCincoCamposPermitidos()
    {
        await using var db = await CrearDbAsync();
        var (_, codigo) = await EmitirAsync(db);

        var ok = Assert.IsType<OkObjectResult>(await Publico(db).Consultar(codigo, default));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value, JsonSerializerOptions.Web));

        var claves = json.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(["aviso", "emisorNombre", "fecha", "serieNumero", "total"], claves);
        foreach (var privado in new[] { "87654321X", "12345678Z", "Cerámicas", "Collet", "Catering", AutonomoId.ToString() })
            Assert.DoesNotContain(privado, TextoSinEscapar(ok.Value), StringComparison.OrdinalIgnoreCase);
    }

    // El serializador por defecto escapa «á» como á: buscar «Cerámicas» en ese texto nunca fallaría.
    private static string TextoSinEscapar(object? valor) => JsonSerializer.Serialize(valor,
        new JsonSerializerOptions(JsonSerializerOptions.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    [Fact]
    public async Task PA1604_EnlacesNoValidos_DanEl404IdenticoYSinDatos()
    {
        await using var db = await CrearDbAsync();
        var (_, real) = await EmitirAsync(db);
        var modificado = (real[^1] == 'A' ? 'B' : 'A') + "";
        modificado = real[..^1] + modificado;
        var cuerpos = new List<string>();

        // El de un borrador: en PostgreSQL el CHECK de la capa 1 lo impide; aquí se fuerza para probar el filtro del servicio.
        var borrador = await Facturacion(db).GuardarBorradorAsync(Propietario, AutonomoId, null, Request, default);
        var deBorrador = CodigoPublicoFactura.Generar();
        (await db.Facturas.SingleAsync(f => f.Id == borrador.Valor!.Id)).CodigoPublico = deBorrador;
        await db.SaveChangesAsync();

        foreach (var codigo in new[] { modificado, CodigoPublicoFactura.Generar(), deBorrador, real[..10], real + "x", "%27%20OR%201=1", "../registros" })
        {
            var problema = Assert.IsType<ObjectResult>(await Publico(db).Consultar(codigo, default));
            Assert.Equal(StatusCodes.Status404NotFound, problema.StatusCode);
            var cuerpo = TextoSinEscapar(problema.Value);
            Assert.DoesNotContain("F-2026", cuerpo);
            Assert.DoesNotContain("Ramón", cuerpo);
            cuerpos.Add(cuerpo);
        }

        Assert.Single(cuerpos.Distinct());
    }

    [Fact]
    public void PA1607_ElControladorPublico_SoloTieneUnaAccionGet()
    {
        // Todo método público declarado es una acción: uno sin atributo de verbo aceptaría cualquier verbo, así que
        // se exige que haya exactamente uno y que solo admita GET.
        var acciones = typeof(FacturasPublicasController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .ToArray();

        var accion = Assert.Single(acciones);
        Assert.Equal(["GET"], accion.GetCustomAttributes<HttpMethodAttribute>().SelectMany(a => a.HttpMethods).ToArray());
    }

    [Fact]
    public void PA1605_ElControladorPublico_NoDependeDelUsuario()
    {
        var parametros = typeof(FacturasPublicasController).GetConstructors().Single().GetParameters().Select(p => p.ParameterType);

        Assert.DoesNotContain(typeof(ICurrentUserService), parametros);
        Assert.Equal("api/publico/facturas", typeof(FacturasPublicasController).GetCustomAttribute<RouteAttribute>()!.Template);
    }

    [Fact]
    public async Task LaRespuestaPublica_NoSeCacheaNiEnviaReferer()
    {
        await using var db = await CrearDbAsync();
        var (_, codigo) = await EmitirAsync(db);
        var controller = Publico(db);

        await controller.Consultar(codigo, default);

        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal("no-referrer", controller.Response.Headers["Referrer-Policy"].ToString());
    }

    // ─── QR en el detalle de la factura (con sesión) ──────────────────────────────

    [Fact]
    public async Task PA1602_ElDetalleDeUnaFacturaEmitida_TraeElQrConLaUrlPublica()
    {
        await using var db = await CrearDbAsync();
        var (id, codigo) = await EmitirAsync(db);

        var factura = await DetalleAsync(db, id);

        Assert.NotNull(factura.Qr);
        Assert.Equal($"http://192.168.1.20:5173/f/{codigo}", factura.Qr!.Url);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, Convert.FromBase64String(factura.Qr.ImagenPngBase64)[..4]);
    }

    [Fact]
    public async Task PA1601_ConsultasSucesivasDelDetalle_DevuelvenElMismoQr()
    {
        await using var db = await CrearDbAsync();
        var (id, _) = await EmitirAsync(db);

        var primera = await DetalleAsync(db, id);
        var segunda = await DetalleAsync(db, id);

        Assert.Equal(primera.Qr!.Url, segunda.Qr!.Url);
        Assert.Equal(primera.Qr.ImagenPngBase64, segunda.Qr.ImagenPngBase64);
    }

    [Fact]
    public async Task PA1603_ElDetalleDeUnBorrador_NoTraeQr()
    {
        await using var db = await CrearDbAsync();
        var borrador = await Facturacion(db).GuardarBorradorAsync(Propietario, AutonomoId, null, Request, default);

        var factura = await DetalleAsync(db, borrador.Valor!.Id);

        Assert.Equal("BORRADOR", factura.Estado);
        Assert.Null(factura.Qr);
    }

    [Fact]
    public async Task D4_UnaFacturaEmitidaAntesDeUT23_SeConsultaSinQr()
    {
        await using var db = await CrearDbAsync();
        var ahora = DateTimeOffset.UtcNow;
        var antigua = new Factura
        {
            Id = Guid.NewGuid(), AutonomoId = AutonomoId, Serie = "F-2026", Numero = 9, Fecha = new DateOnly(2026, 5, 1),
            Estado = EstadoFactura.EMITIDA, Creada = ahora, Actualizada = ahora, Emitida = ahora,
            EmisorNombre = "Ramón García Pérez", BaseImponible = 0m, CuotaIva = 0m, Total = 0m
        };
        db.Facturas.Add(antigua);
        await db.SaveChangesAsync();

        var factura = await DetalleAsync(db, antigua.Id);

        Assert.Equal("EMITIDA", factura.Estado);
        Assert.Null(factura.Qr);
    }

    [Fact]
    public void D2_ElListadoNoLlevaQr()
    {
        Assert.DoesNotContain(typeof(FacturaListaResponse).GetProperties(), p => p.Name.Contains("Qr", StringComparison.OrdinalIgnoreCase));
    }

    // ─── Utilidades ──────────────────────────────────────────────────────────────

    private static readonly GuardarFacturaRequest Request =
        new(ContactoId, new DateOnly(2026, 6, 5), null, [new("Servicio de catering", 1m, 2545.45m, 10m)]);

    private static FacturasPublicasController Publico(ApplicationDbContext db) =>
        new(new ConsultaPublicaFacturaService(db)) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static FacturacionService Facturacion(ApplicationDbContext db) =>
        new(db, new GeneradorObligaciones(db, TimeProvider.System), TimeProvider.System, new RegistroFacturacionService(db),
            QrDePrueba.Servicio(), NullLogger<FacturacionService>.Instance);

    private static async Task<FacturaResponse> DetalleAsync(ApplicationDbContext db, Guid facturaId)
    {
        var controller = new FacturasController(Facturacion(db), new UsuarioFijo(Propietario));
        var ok = Assert.IsType<OkObjectResult>(await controller.Obtener(AutonomoId, facturaId, default));
        return Assert.IsType<FacturaResponse>(ok.Value);
    }

    private static async Task<(Guid Id, string Codigo)> EmitirAsync(ApplicationDbContext db)
    {
        var borrador = await Facturacion(db).GuardarBorradorAsync(Propietario, AutonomoId, null, Request, default);
        var emitida = await Facturacion(db).EmitirAsync(Propietario, AutonomoId, borrador.Valor!.Id, default);
        Assert.True(emitida.EsCorrecto, emitida.Error?.Mensaje);
        var codigo = await db.Facturas.AsNoTracking().Where(f => f.Id == emitida.Valor!.Id).Select(f => f.CodigoPublico).SingleAsync();
        return (emitida.Valor!.Id, codigo!);
    }

    private static async Task<ApplicationDbContext> CrearDbAsync()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Usuarios.Add(new Usuario
        {
            Id = AutonomoId, Nombre = "Ramón", PrimerApellido = "García", SegundoApellido = "Pérez", Email = "a@test",
            Nif = "87654321X", Rol = Rol.AUTONOMO, DireccionFiscal = "C/ Major 12"
        });
        db.Contactos.Add(new Contacto
        {
            Id = ContactoId, AutonomoId = AutonomoId, Clasificacion = "CLIENTE", Nombre = "Cerámicas Gómez",
            Nif = "12345678Z", Direccion = "Pol. Ind. El Collet", Activo = true
        });
        await db.SaveChangesAsync();
        return db;
    }

    private sealed class UsuarioFijo(UsuarioActual? usuario) : ICurrentUserService
    {
        public Task<UsuarioActual?> ObtenerAsync(CancellationToken cancellationToken) => Task.FromResult(usuario);
    }
}
