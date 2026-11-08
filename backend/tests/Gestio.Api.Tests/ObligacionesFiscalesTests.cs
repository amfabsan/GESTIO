using Gestio.Api.Controllers;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Tests;

public sealed class ObligacionesFiscalesTests
{
    private readonly Guid autonomo = Guid.NewGuid();
    private readonly Guid otro = Guid.NewGuid();
    private readonly Guid gestor = Guid.NewGuid();
    private readonly Guid ajeno = Guid.NewGuid();
    private static readonly GuardarPerfilFiscalRequest General = new("GENERAL", "ESTIMACION_DIRECTA_SIMPLIFICADA", false, false);

    private async Task<ApplicationDbContext> CrearAsync()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        foreach (var (id, rol, asesoria) in new[] { (autonomo, Rol.AUTONOMO, (string?)null), (otro, Rol.AUTONOMO, null), (gestor, Rol.GESTOR, "ASE-000001"), (ajeno, Rol.GESTOR, "ASE-000002") })
            db.Usuarios.Add(new Usuario { Id = id, Nombre = "Prueba", PrimerApellido = "Uno", SegundoApellido = "Dos", Email = id + "@test", Nif = id.ToString()[..9], Rol = rol, CodigoAsesoria = asesoria });
        db.Vinculaciones.AddRange(new Vinculacion { Id = Guid.NewGuid(), AutonomoId = autonomo, CodigoAsesoria = "ASE-000001", Estado = EstadoVinculacion.ACTIVA },
            new Vinculacion { Id = Guid.NewGuid(), AutonomoId = otro, CodigoAsesoria = "ASE-000001", Estado = EstadoVinculacion.ACTIVA });
        await db.SaveChangesAsync();
        return db;
    }

    private static (PerfilesFiscalesService Perfiles, ObligacionesFiscalesService Obligaciones) Servicios(ApplicationDbContext db)
    {
        var generador = new GeneradorObligaciones(db, TimeProvider.System);
        var perfiles = new PerfilesFiscalesService(db, TimeProvider.System, generador);
        return (perfiles, new ObligacionesFiscalesService(db, perfiles, generador));
    }

    [Theory]
    [InlineData("GENERAL", "ESTIMACION_DIRECTA_NORMAL", "303", "130")]
    [InlineData("SIMPLIFICADO", "ESTIMACION_DIRECTA_SIMPLIFICADA", "303", "130")]
    [InlineData("GENERAL", "ESTIMACION_OBJETIVA", "303", "131")]
    [InlineData("RECARGO_EQUIVALENCIA", "ESTIMACION_OBJETIVA", null, "131")]
    [InlineData("RECARGO_EQUIVALENCIA", "ESTIMACION_DIRECTA_NORMAL", null, "130")]
    public async Task PA1610_CatalogoReducidoYFechasDelEjercicio(string iva, string irpf, string? modeloIva, string modeloIrpf)
    {
        await using var db = await CrearAsync();
        var (perfiles, obligaciones) = Servicios(db);
        Assert.True((await perfiles.GuardarAsync(new(gestor, Rol.GESTOR), autonomo, 2031, General with { RegimenIva = iva, RegimenIrpf = irpf }, default)).EsCorrecto);
        var calendario = (await obligaciones.GenerarAsync(new(autonomo, Rol.AUTONOMO), autonomo, 2031, default)).Valor!;
        Assert.Equal(modeloIva is null ? 4 : 8, calendario.Obligaciones.Count);
        Assert.Equal(new[] { modeloIva, modeloIrpf }.OfType<string>().Order(), calendario.Obligaciones.Select(o => o.Modelo).Distinct().Order());
        foreach (var modelo in calendario.Obligaciones.GroupBy(o => o.Modelo))
        {
            Assert.Equal(new[] { "1T", "2T", "3T", "4T" }, modelo.Select(o => o.Periodo));
            Assert.Equal(new[] { new DateOnly(2031, 4, 25), new DateOnly(2031, 7, 25), new DateOnly(2031, 10, 25), new DateOnly(2032, 1, 25) }, modelo.Select(o => o.FechaPrevista));
            Assert.All(modelo, o => { Assert.Equal("PENDIENTE", o.Estado); Assert.Equal(autonomo, o.AutonomoId); });
        }
    }

    [Fact]
    public async Task PA1612_PA1621_DetalleConHistorialYAvisoYNoEncontrada()
    {
        await using var db = await CrearAsync();
        var (perfiles, obligaciones) = Servicios(db);
        await perfiles.GuardarAsync(new(gestor, Rol.GESTOR), autonomo, 2026, General, default);
        var calendario = (await obligaciones.GenerarAsync(new(autonomo, Rol.AUTONOMO), autonomo, 2026, default)).Valor!;
        var esperado = calendario.Obligaciones.First();
        db.ChangeTracker.Clear();
        var detalle = (await obligaciones.DetalleAsync(new(autonomo, Rol.AUTONOMO), autonomo, esperado.Id, default)).Valor!;
        Assert.Equal(esperado, detalle.Obligacion);
        Assert.Equal("Fechas configuradas para la demostración", detalle.AvisoFechas);
        Assert.Single(detalle.Historial);
        Assert.Equal(autonomo, detalle.Historial[0].UsuarioId);
        Assert.Equal("ObligacionFiscal.NoEncontrada", (await obligaciones.DetalleAsync(new(autonomo, Rol.AUTONOMO), autonomo, Guid.NewGuid(), default)).Error!.Codigo);
    }

    [Fact]
    public async Task PA1613_RepeticionConservaIdsEHistorialEIndiceUnico()
    {
        await using var db = await CrearAsync();
        var (perfiles, obligaciones) = Servicios(db);
        var usuario = new UsuarioActual(autonomo, Rol.AUTONOMO);
        await perfiles.GuardarAsync(new(gestor, Rol.GESTOR), autonomo, 2026, General, default);
        var primero = (await obligaciones.GenerarAsync(usuario, autonomo, 2026, default)).Valor!;
        for (var i = 0; i < 3; i++) Assert.Equal(primero.Obligaciones, (await obligaciones.GenerarAsync(usuario, autonomo, 2026, default)).Valor!.Obligaciones);
        Assert.Equal(8, await db.Set<ObligacionFiscal>().CountAsync());
        Assert.Equal(8, await db.Set<HistorialObligacionFiscal>().CountAsync());
        var indice = db.Model.FindEntityType(typeof(ObligacionFiscal))!.GetIndexes().Single();
        Assert.True(indice.IsUnique);
        Assert.Equal(new[] { "AutonomoId", "Modelo", "Ejercicio", "Periodo" }, indice.Properties.Select(p => p.Name));
    }

    [Fact]
    public async Task PA1614_SinPerfilNoGeneraNiHeredaYValidaEjercicio()
    {
        await using var db = await CrearAsync();
        var (perfiles, obligaciones) = Servicios(db);
        await perfiles.GuardarAsync(new(gestor, Rol.GESTOR), autonomo, 2026, General, default);
        var usuario = new UsuarioActual(autonomo, Rol.AUTONOMO);
        Assert.False((await obligaciones.ConsultarAsync(usuario, autonomo, 2027, default)).Valor!.PerfilConfigurado);
        Assert.Equal("ObligacionFiscal.SinPerfil", (await obligaciones.GenerarAsync(usuario, autonomo, 2027, default)).Error!.Codigo);
        Assert.Empty(await db.Set<ObligacionFiscal>().ToListAsync());
        foreach (var ejercicio in new[] { 0, -1, 9999, int.MaxValue })
        {
            Assert.False((await obligaciones.GenerarAsync(usuario, autonomo, ejercicio, default)).EsCorrecto);
            Assert.False((await perfiles.GuardarAsync(new(gestor, Rol.GESTOR), autonomo, ejercicio, General, default)).EsCorrecto);
        }
    }

    [Fact]
    public async Task PA1615_PA1616_ActualizarPendientesEliminarNoAplicablesYConservarPresentada()
    {
        await using var db = await CrearAsync();
        var (perfiles, obligaciones) = Servicios(db);
        var usuario = new UsuarioActual(autonomo, Rol.AUTONOMO);
        var autorizado = new UsuarioActual(gestor, Rol.GESTOR);
        await perfiles.GuardarAsync(autorizado, autonomo, 2026, General, default);
        await obligaciones.GenerarAsync(usuario, autonomo, 2026, default);
        var presentada = await db.Set<ObligacionFiscal>().SingleAsync(o => o.Modelo == "303" && o.Periodo == "1T");
        presentada.Estado = EstadoObligacion.PRESENTADA; // Precondición PA1616; PB-13 no añade una operación de presentación.
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var anterior = (await obligaciones.DetalleAsync(usuario, autonomo, presentada.Id, default)).Valor!;
        await perfiles.GuardarAsync(autorizado, autonomo, 2026, General with { RegimenIva = "RECARGO_EQUIVALENCIA", RegimenIrpf = "ESTIMACION_OBJETIVA" }, default);
        var nuevas = (await obligaciones.ConsultarAsync(usuario, autonomo, 2026, default)).Valor!.Obligaciones;
        Assert.Equal(5, nuevas.Count);
        Assert.Equal(4, nuevas.Count(o => o.Modelo == "131"));
        Assert.Single(nuevas, o => o.Modelo == "303");
        Assert.DoesNotContain(nuevas, o => o.Modelo == "130");
        var despues = (await obligaciones.DetalleAsync(usuario, autonomo, presentada.Id, default)).Valor!;
        Assert.Equal(anterior.Obligacion, despues.Obligacion);
        Assert.Equal(anterior.Historial, despues.Historial);
        await perfiles.GuardarAsync(autorizado, autonomo, 2026, General, default);
        await obligaciones.GenerarAsync(usuario, autonomo, 2026, default);
        Assert.Equal(8, await db.Set<ObligacionFiscal>().CountAsync());
        Assert.Equal(presentada.Id, (await db.Set<ObligacionFiscal>().SingleAsync(o => o.Modelo == "303" && o.Periodo == "1T")).Id);
        Assert.Equal(anterior.Historial, (await obligaciones.DetalleAsync(usuario, autonomo, presentada.Id, default)).Valor!.Historial);
    }

    [Fact]
    public async Task PA1617_PA1618_PA1620_AislamientoYVinculacionActivaEnTodasLasOperaciones()
    {
        await using var db = await CrearAsync();
        var (perfiles, obligaciones) = Servicios(db);
        foreach (var id in new[] { autonomo, otro })
        {
            await perfiles.GuardarAsync(new(gestor, Rol.GESTOR), id, 2026, General, default);
            await obligaciones.GenerarAsync(new(id, Rol.AUTONOMO), id, 2026, default);
        }
        var lista = (await obligaciones.ConsultarAsync(new(autonomo, Rol.AUTONOMO), autonomo, 2026, default)).Valor!;
        Assert.All(lista.Obligaciones, o => Assert.Equal(autonomo, o.AutonomoId));
        Assert.All((await obligaciones.ConsultarAsync(new(otro, Rol.AUTONOMO), otro, 2026, default)).Valor!.Obligaciones, o => Assert.Equal(otro, o.AutonomoId));
        var idDetalle = lista.Obligaciones[0].Id;
        Assert.True((await obligaciones.DetalleAsync(new(gestor, Rol.GESTOR), autonomo, idDetalle, default)).EsCorrecto);
        foreach (var usuario in new[] { new UsuarioActual(otro, Rol.AUTONOMO), new UsuarioActual(ajeno, Rol.GESTOR) })
        {
            Assert.Equal("ObligacionFiscal.Prohibido", (await obligaciones.ConsultarAsync(usuario, autonomo, 2026, default)).Error!.Codigo);
            Assert.False((await obligaciones.GenerarAsync(usuario, autonomo, 2026, default)).EsCorrecto);
            Assert.False((await obligaciones.DetalleAsync(usuario, autonomo, idDetalle, default)).EsCorrecto);
        }
        Assert.False((await obligaciones.DetalleAsync(new(otro, Rol.AUTONOMO), otro, idDetalle, default)).EsCorrecto);
        foreach (var estado in new[] { EstadoVinculacion.PENDIENTE, EstadoVinculacion.RECHAZADA, EstadoVinculacion.FINALIZADA })
        {
            (await db.Vinculaciones.SingleAsync(v => v.AutonomoId == autonomo)).Estado = estado;
            await db.SaveChangesAsync();
            Assert.False((await obligaciones.ConsultarAsync(new(gestor, Rol.GESTOR), autonomo, 2026, default)).EsCorrecto);
            Assert.False((await obligaciones.GenerarAsync(new(gestor, Rol.GESTOR), autonomo, 2026, default)).EsCorrecto);
            Assert.False((await obligaciones.DetalleAsync(new(gestor, Rol.GESTOR), autonomo, idDetalle, default)).EsCorrecto);
        }
    }

    [Fact]
    public async Task PA1622_CadaEjercicioUsaSuPropioPerfil()
    {
        await using var db = await CrearAsync();
        var (perfiles, obligaciones) = Servicios(db);
        await perfiles.GuardarAsync(new(gestor, Rol.GESTOR), autonomo, 2026, General, default);
        await perfiles.GuardarAsync(new(gestor, Rol.GESTOR), autonomo, 2027, General with { RegimenIva = "RECARGO_EQUIVALENCIA", RegimenIrpf = "ESTIMACION_OBJETIVA" }, default);
        var a = (await obligaciones.GenerarAsync(new(autonomo, Rol.AUTONOMO), autonomo, 2026, default)).Valor!;
        var b = (await obligaciones.GenerarAsync(new(autonomo, Rol.AUTONOMO), autonomo, 2027, default)).Valor!;
        Assert.Equal(8, a.Obligaciones.Count);
        Assert.Equal(4, b.Obligaciones.Count);
        Assert.All(b.Obligaciones, o => { Assert.Equal("131", o.Modelo); Assert.Equal(2027, o.Ejercicio); });
        Assert.Equal(new DateOnly(2028, 1, 25), b.Obligaciones.Last().FechaPrevista);
    }

    private sealed class SinUsuario : ICurrentUserService
    {
        public Task<UsuarioActual?> ObtenerAsync(CancellationToken ct) => Task.FromResult<UsuarioActual?>(null);
    }

    [Fact]
    public async Task Api_SinUsuarioRechazaLasTresOperaciones()
    {
        await using var db = await CrearAsync();
        var controller = new ObligacionesFiscalesController(Servicios(db).Obligaciones, new SinUsuario());
        Assert.IsType<UnauthorizedResult>((await controller.Consultar(autonomo, 2026, default)).Result);
        Assert.IsType<UnauthorizedResult>((await controller.Generar(autonomo, 2026, default)).Result);
        Assert.IsType<UnauthorizedResult>((await controller.Detalle(autonomo, Guid.NewGuid(), default)).Result);
    }
}
