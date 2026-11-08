using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Services;
using Gestio.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Tests;

public sealed class PerfilesFiscalesTests
{
    private readonly Guid autonomo = Guid.NewGuid();
    private readonly Guid gestor = Guid.NewGuid();
    private static readonly GuardarPerfilFiscalRequest Valido = new("GENERAL", "ESTIMACION_DIRECTA_SIMPLIFICADA", true, false);

    private async Task<ApplicationDbContext> CrearAsync()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Usuarios.AddRange(new Usuario { Id = autonomo, Nombre = "Cliente", PrimerApellido = "Uno", SegundoApellido = "Dos", Email = "a@test", Nif = "A", Rol = Rol.AUTONOMO },
            new Usuario { Id = gestor, Nombre = "Gestor", PrimerApellido = "Uno", SegundoApellido = "Dos", Email = "g@test", Nif = "G", Rol = Rol.GESTOR, CodigoAsesoria = "ASE" });
        db.Vinculaciones.Add(new Vinculacion { Id = Guid.NewGuid(), AutonomoId = autonomo, CodigoAsesoria = "ASE", Estado = EstadoVinculacion.ACTIVA });
        await db.SaveChangesAsync();
        return db;
    }
    private PerfilesFiscalesService Servicio(ApplicationDbContext db) => new(db, TimeProvider.System, new GeneradorObligaciones(db, TimeProvider.System));

    private sealed class UsuarioFijo(UsuarioActual? usuario) : ICurrentUserService
    {
        public Task<UsuarioActual?> ObtenerAsync(CancellationToken ct) => Task.FromResult(usuario);
    }

    [Fact]
    public async Task Api_ValidaDatosYPermisosConCodigosHttpExistentes()
    {
        await using var db = await CrearAsync();
        var controller = new PerfilesFiscalesController(Servicio(db), new UsuarioFijo(new(gestor, Rol.GESTOR)));
        var incompleto = Assert.IsType<ObjectResult>(await controller.Guardar(autonomo, 2026, new(null, null, null, null), default));
        Assert.Equal(400, incompleto.StatusCode);
        var detalle = Assert.IsType<ProblemDetails>(incompleto.Value).Detail!;
        Assert.Contains("Régimen de IVA", detalle);
        Assert.Contains("Régimen de IRPF", detalle);
        Assert.Contains("Trabajadores con retención", detalle);
        Assert.Contains("Local alquilado con retención", detalle);
        Assert.Equal(400, Assert.IsType<ObjectResult>(await controller.Guardar(autonomo, 2026, Valido with { RegimenIva = "INVALIDO" }, default)).StatusCode);
        Assert.IsType<OkObjectResult>(await controller.Guardar(autonomo, 2026, Valido, default));
        Assert.IsType<OkObjectResult>(await controller.Consultar(autonomo, 2026, default));
        var autonomoController = new PerfilesFiscalesController(Servicio(db), new UsuarioFijo(new(autonomo, Rol.AUTONOMO)));
        Assert.Equal(403, Assert.IsType<ObjectResult>(await autonomoController.Guardar(autonomo, 2026, Valido, default)).StatusCode);
        Assert.IsType<OkObjectResult>(await autonomoController.Consultar(autonomo, 2026, default));
        Assert.Equal(403, Assert.IsType<ObjectResult>(await autonomoController.Consultar(Guid.NewGuid(), 2026, default)).StatusCode);
        var anonimo = new PerfilesFiscalesController(Servicio(db), new UsuarioFijo(null));
        Assert.IsType<UnauthorizedResult>(await anonimo.Guardar(autonomo, 2026, Valido, default));
        Assert.IsType<UnauthorizedResult>(await anonimo.Consultar(autonomo, 2026, default));
    }

    [Fact]
    public async Task PA1664_CrearYRecuperar()
    {
        await using var db = await CrearAsync();
        var servicio = Servicio(db);
        var resultado = await servicio.GuardarAsync(new(gestor, Rol.GESTOR), autonomo, 2026, Valido, default);
        Assert.True(resultado.EsCorrecto);
        db.ChangeTracker.Clear();
        var perfil = (await servicio.ConsultarAsync(new(gestor, Rol.GESTOR), autonomo, 2026, default)).Valor!;
        Assert.Equal(autonomo, perfil.AutonomoId);
        Assert.Equal(2026, perfil.Ejercicio);
        Assert.Equal(Valido.RegimenIva, perfil.RegimenIva);
        Assert.Equal(Valido.RegimenIrpf, perfil.RegimenIrpf);
    }

    [Fact]
    public async Task PA1665_ActualizarSinDuplicadosConHistorialYNoRegistrarSinCambios()
    {
        await using var db = await CrearAsync();
        var servicio = Servicio(db);
        var usuario = new UsuarioActual(gestor, Rol.GESTOR);
        var id = (await servicio.GuardarAsync(usuario, autonomo, 2026, Valido, default)).Valor!.Id;
        var cambio = Valido with { RegimenIva = "SIMPLIFICADO" };
        Assert.True((await servicio.GuardarAsync(usuario, autonomo, 2026, cambio, default)).EsCorrecto);
        db.ChangeTracker.Clear();
        var perfil = (await servicio.ConsultarAsync(usuario, autonomo, 2026, default)).Valor!;
        Assert.Equal(id, perfil.Id);
        Assert.Single(await db.Set<PerfilFiscal>().ToListAsync());
        var evento = Assert.Single(perfil.Historial, h => h.ValorAnterior != null);
        Assert.Equal(gestor, evento.UsuarioId);
        Assert.Equal("GENERAL", evento.ValorAnterior);
        Assert.Equal("SIMPLIFICADO", evento.ValorNuevo);
        Assert.NotEqual(default, evento.FechaCambio);
        await servicio.GuardarAsync(usuario, autonomo, 2026, cambio, default);
        Assert.Equal(5, await db.Set<HistorialPerfilFiscal>().CountAsync());
        var indice = db.Model.FindEntityType(typeof(PerfilFiscal))!.GetIndexes().Single();
        Assert.True(indice.IsUnique);
        Assert.Equal(new[] { "AutonomoId", "Ejercicio" }, indice.Properties.Select(p => p.Name));
    }

    [Fact]
    public async Task PA1666_NoHeredarEjercicios()
    {
        await using var db = await CrearAsync();
        var servicio = Servicio(db);
        var usuario = new UsuarioActual(gestor, Rol.GESTOR);
        await servicio.GuardarAsync(usuario, autonomo, 2026, Valido, default);
        Assert.Null((await servicio.ConsultarAsync(usuario, autonomo, 2027, default)).Valor);
        await servicio.GuardarAsync(usuario, autonomo, 2027, Valido with { RegimenIva = "RECARGO_EQUIVALENCIA" }, default);
        Assert.Equal("GENERAL", (await servicio.ConsultarAsync(usuario, autonomo, 2026, default)).Valor!.RegimenIva);
    }

    [Theory]
    [InlineData(null, null, null, null)]
    [InlineData("GENERAL", "ESTIMACION_DIRECTA_NORMAL", null, false)]
    [InlineData("GENERAL", null, true, false)]
    public async Task PA1667_IncompletoNoPersiste(string? iva, string? irpf, bool? trabajadores, bool? local)
    {
        await using var db = await CrearAsync();
        var servicio = Servicio(db);
        var usuario = new UsuarioActual(gestor, Rol.GESTOR);
        var request = new GuardarPerfilFiscalRequest(iva, irpf, trabajadores, local);
        Assert.Equal("PerfilFiscal.DatosObligatorios", (await servicio.GuardarAsync(usuario, autonomo, 2026, request, default)).Error!.Codigo);
        Assert.Empty(await db.Set<PerfilFiscal>().ToListAsync());
        await servicio.GuardarAsync(usuario, autonomo, 2026, Valido, default);
        Assert.False((await servicio.GuardarAsync(usuario, autonomo, 2026, request, default)).EsCorrecto);
        db.ChangeTracker.Clear();
        Assert.Equal("GENERAL", (await db.Set<PerfilFiscal>().SingleAsync()).RegimenIva);
        Assert.Equal(4, await db.Set<HistorialPerfilFiscal>().CountAsync());
    }

    [Theory]
    [InlineData("INVALIDO", "ESTIMACION_DIRECTA_NORMAL")]
    [InlineData("GENERAL", "INVALIDO")]
    [InlineData("general", "ESTIMACION_DIRECTA_NORMAL")]
    public async Task PA1668_CatalogoCerrado(string iva, string irpf)
    {
        await using var db = await CrearAsync();
        var servicio = Servicio(db);
        var usuario = new UsuarioActual(gestor, Rol.GESTOR);
        var request = Valido with { RegimenIva = iva, RegimenIrpf = irpf };
        Assert.Equal("PerfilFiscal.RegimenNoAdmitido", (await servicio.GuardarAsync(usuario, autonomo, 2026, request, default)).Error!.Codigo);
        Assert.Empty(await db.Set<PerfilFiscal>().ToListAsync());
        await servicio.GuardarAsync(usuario, autonomo, 2026, Valido, default);
        Assert.False((await servicio.GuardarAsync(usuario, autonomo, 2026, request, default)).EsCorrecto);
        db.ChangeTracker.Clear();
        Assert.Equal("GENERAL", (await db.Set<PerfilFiscal>().SingleAsync()).RegimenIva);
        Assert.Equal(4, await db.Set<HistorialPerfilFiscal>().CountAsync());
    }

    [Fact]
    public async Task PA1669_SoloGestorVinculadoEscribeYAutonomoSoloConsulta()
    {
        await using var db = await CrearAsync();
        var servicio = Servicio(db);
        await servicio.GuardarAsync(new(gestor, Rol.GESTOR), autonomo, 2026, Valido, default);
        Assert.True((await servicio.ConsultarAsync(new(autonomo, Rol.AUTONOMO), autonomo, 2026, default)).EsCorrecto);
        foreach (var usuario in new[] { new UsuarioActual(autonomo, Rol.AUTONOMO), new UsuarioActual(Guid.NewGuid(), Rol.GESTOR) })
            Assert.Equal("PerfilFiscal.Prohibido", (await servicio.GuardarAsync(usuario, autonomo, 2026, Valido with { RegimenIva = "SIMPLIFICADO" }, default)).Error!.Codigo);
        foreach (var estado in new[] { EstadoVinculacion.PENDIENTE, EstadoVinculacion.FINALIZADA, EstadoVinculacion.RECHAZADA })
        {
            (await db.Vinculaciones.SingleAsync()).Estado = estado;
            await db.SaveChangesAsync();
            Assert.False((await servicio.GuardarAsync(new(gestor, Rol.GESTOR), autonomo, 2026, Valido, default)).EsCorrecto);
            Assert.False((await servicio.ConsultarAsync(new(gestor, Rol.GESTOR), autonomo, 2026, default)).EsCorrecto);
        }
        Assert.Equal("GENERAL", (await db.Set<PerfilFiscal>().SingleAsync()).RegimenIva);
        Assert.Equal(4, await db.Set<HistorialPerfilFiscal>().CountAsync());
    }
}
