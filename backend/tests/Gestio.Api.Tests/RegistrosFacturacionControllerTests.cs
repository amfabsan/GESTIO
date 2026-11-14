using System.Reflection;
using Gestio.Api.Controllers;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Models.Request;
using Gestio.Api.Models.Response;
using Gestio.Api.Services;
using Gestio.Api.Services.Verifactu;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gestio.Api.Tests;

/// <summary>UT22, capa 4: API de consulta y verificación de registros de facturación.</summary>
public sealed class RegistrosFacturacionControllerTests
{
    private static readonly Guid AutonomoA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AutonomoB = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ContactoA = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task SinUsuarioAutenticado_LasDosRutasDevuelven401()
    {
        await using var db = await CrearDbAsync();
        var controller = new RegistrosFacturacionController(new RegistroFacturacionService(db), new UsuarioFijo(null));

        Assert.IsType<UnauthorizedResult>((await controller.Listar(AutonomoA, default)).Result);
        Assert.IsType<UnauthorizedResult>((await controller.Verificar(AutonomoA, default)).Result);
    }

    [Fact]
    public async Task Propietario_ListaSuCadenaYLaVerifica_200()
    {
        await using var db = await CrearDbAsync();
        await EmitirAsync(db);
        await EmitirAsync(db);
        var controller = Controller(db, new UsuarioActual(AutonomoA, Rol.AUTONOMO));

        var lista = Assert.IsAssignableFrom<IReadOnlyList<RegistroFacturacionResponse>>(
            Assert.IsType<OkObjectResult>((await controller.Listar(AutonomoA, default)).Result).Value);
        var verificacion = Assert.IsType<VerificacionCadenaResponse>(
            Assert.IsType<OkObjectResult>((await controller.Verificar(AutonomoA, default)).Result).Value);

        Assert.Equal([1, 2], lista.Select(r => r.Secuencia));
        Assert.True(verificacion.Coherente);
        Assert.Equal(2, verificacion.TotalRegistros);
    }

    [Fact]
    public async Task PA1593_OtroAutonomo_Recibe403ConProblemDetails()
    {
        await using var db = await CrearDbAsync();
        await EmitirAsync(db);
        var controller = Controller(db, new UsuarioActual(AutonomoB, Rol.AUTONOMO));

        foreach (var resultado in new[] { (await controller.Listar(AutonomoA, default)).Result, (await controller.Verificar(AutonomoA, default)).Result })
        {
            var objeto = Assert.IsType<ObjectResult>(resultado);
            Assert.Equal(403, objeto.StatusCode);
            var problema = Assert.IsType<ProblemDetails>(objeto.Value);
            Assert.Equal("RegistroFacturacion.Prohibido", problema.Extensions["codigo"]);
        }
    }

    [Fact]
    public async Task D4_GestorConVinculacionActiva_Recibe200EnLasDosRutas()
    {
        await using var db = await CrearDbAsync();
        var gestorId = Guid.NewGuid();
        db.Usuarios.Add(new Usuario { Id = gestorId, Nombre = "Gestor", PrimerApellido = "G", SegundoApellido = "G", Email = "g@test",
            Nif = "GESTOR", Rol = Rol.GESTOR, CodigoAsesoria = "ASE-123456" });
        db.Vinculaciones.Add(new Vinculacion { Id = Guid.NewGuid(), AutonomoId = AutonomoA, CodigoAsesoria = "ASE-123456",
            Estado = EstadoVinculacion.ACTIVA, FechaSolicitud = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        await EmitirAsync(db);
        var controller = Controller(db, new UsuarioActual(gestorId, Rol.GESTOR));

        Assert.IsType<OkObjectResult>((await controller.Listar(AutonomoA, default)).Result);
        Assert.IsType<OkObjectResult>((await controller.Verificar(AutonomoA, default)).Result);
    }

    [Fact]
    public void PA1598_NingunaRutaDeLaApiPermiteModificarOEliminarRegistros()
    {
        // Recorre TODOS los controladores: cualquier ruta futura bajo /registros-facturacion, esté donde esté,
        // tiene que ser GET. Esta lista es exactamente lo que expone la API hoy.
        var rutas = typeof(RegistrosFacturacionController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(Rutas)
            .Where(r => r.Plantilla.Contains("registros-facturacion", StringComparison.OrdinalIgnoreCase))
            .Select(r => $"{r.Verbo} {r.Plantilla}")
            .Order()
            .ToList();

        Assert.Equal(
        [
            "GET api/autonomos/{autonomoId:guid}/registros-facturacion",
            "GET api/autonomos/{autonomoId:guid}/registros-facturacion/verificacion"
        ], rutas);
    }

    [Fact]
    public void PA1598_ElServicioNoOfreceOperacionesDeModificacionNiBorrado()
    {
        var publicos = typeof(RegistroFacturacionService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name).Order().ToList();
        Assert.Equal(["ListarAsync", "NumSerieFactura", "PrepararAltaAsync", "VerificarAsync"], publicos);
    }

    // Modelo de las rutas de atributo de MVC: usa las mismas interfaces que el framework
    // (IActionHttpMethodProvider para verbos, IRouteTemplateProvider para plantillas), así que cubre
    // [HttpX], [AcceptVerbs] y [Route] en la acción. Una acción pública sin verbo acepta CUALQUIERA ("ANY").
    // Incluye métodos heredados salvo los de ControllerBase/Controller. No cubre endpoints mínimos (MapX en Program.cs).
    private static IEnumerable<(string Verbo, string Plantilla)> Rutas(Type controlador)
    {
        var bases = controlador.GetCustomAttributes(true).OfType<IRouteTemplateProvider>().Select(r => r.Template ?? "").DefaultIfEmpty("");
        var acciones = controlador.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object) &&
                        m.DeclaringType != typeof(ControllerBase) && m.DeclaringType != typeof(Controller) &&
                        m.GetCustomAttribute<NonActionAttribute>() is null);
        foreach (var metodo in acciones)
        {
            var atributos = metodo.GetCustomAttributes(true);
            var verbos = atributos.OfType<IActionHttpMethodProvider>().SelectMany(p => p.HttpMethods).DefaultIfEmpty("ANY").ToList();
            var plantillas = atributos.OfType<IRouteTemplateProvider>().Select(p => p.Template).Where(t => t is not null).DefaultIfEmpty("").ToList();
            foreach (var baseRuta in bases)
            foreach (var plantilla in plantillas)
            foreach (var verbo in verbos)
                yield return (verbo, Combinar(baseRuta, plantilla!));
        }
    }

    private static string Combinar(string baseRuta, string plantilla) =>
        plantilla.StartsWith('/') || plantilla.StartsWith("~/") ? plantilla.TrimStart('~', '/')
        : string.IsNullOrEmpty(plantilla) ? baseRuta
        : $"{baseRuta}/{plantilla}";

    private static RegistrosFacturacionController Controller(ApplicationDbContext db, UsuarioActual usuario) =>
        new(new RegistroFacturacionService(db), new UsuarioFijo(usuario));

    private static async Task EmitirAsync(ApplicationDbContext db)
    {
        var facturacion = new FacturacionService(db, new GeneradorObligaciones(db, TimeProvider.System), TimeProvider.System,
            new RegistroFacturacionService(db), QrDePrueba.Servicio(), NullLogger<FacturacionService>.Instance);
        var propietario = new UsuarioActual(AutonomoA, Rol.AUTONOMO);
        var borrador = await facturacion.GuardarBorradorAsync(propietario, AutonomoA, null,
            new GuardarFacturaRequest(ContactoA, new DateOnly(2026, 6, 16), null, [new("Servicio", 1m, 100m, 21m)]), default);
        Assert.True((await facturacion.EmitirAsync(propietario, AutonomoA, borrador.Valor!.Id, default)).EsCorrecto);
    }

    private static async Task<ApplicationDbContext> CrearDbAsync()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Usuarios.Add(new Usuario { Id = AutonomoA, Nombre = "Autónomo", PrimerApellido = "A", SegundoApellido = "Uno",
            Email = "a@test", Nif = "87654321X", Rol = Rol.AUTONOMO, DireccionFiscal = "Calle Emisor A" });
        db.Contactos.Add(new Contacto { Id = ContactoA, AutonomoId = AutonomoA, Clasificacion = "CLIENTE", Nombre = "Cliente A",
            Nif = "11111111H", Direccion = "Calle Cliente A", Activo = true });
        await db.SaveChangesAsync();
        return db;
    }

    private sealed class UsuarioFijo(UsuarioActual? usuario) : ICurrentUserService
    {
        public Task<UsuarioActual?> ObtenerAsync(CancellationToken cancellationToken) => Task.FromResult(usuario);
    }
}
