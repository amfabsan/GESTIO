using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Tests;

public sealed class VinculacionesTests
{
    private static readonly Guid AutonomoId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid AdministradorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid OtraAdministradoraId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task SolicitarAsync_CodigoValido_CreaSolicitudPendiente()
    {
        await using var context = CrearContexto();
        await AñadirUsuariosAsync(context);

        var resultado = await CrearServicio(context).SolicitarAsync(
            new(AutonomoId, Rol.AUTONOMO),
            " ase-000123 ",
            CancellationToken.None);

        Assert.True(resultado.EsCorrecto);
        Assert.Equal(EstadoVinculacion.PENDIENTE, resultado.Valor!.Estado);
        Assert.Equal("ASE-000123", resultado.Valor.CodigoAsesoria);
    }

    [Fact]
    public async Task SolicitarAsync_CodigoDesconocido_NoCreaVinculacion()
    {
        await using var context = CrearContexto();
        await AñadirUsuariosAsync(context);

        var resultado = await CrearServicio(context).SolicitarAsync(
            new(AutonomoId, Rol.AUTONOMO),
            "ASE-999999",
            CancellationToken.None);

        Assert.Equal(VinculacionErrores.CodigoAsesoriaNoEncontrado, resultado.Error);
        Assert.Empty(await context.Vinculaciones.ToListAsync());
    }

    [Fact]
    public async Task SolicitarAsync_MismaParejaConSolicitudPendiente_DevuelveConflicto()
    {
        await using var context = CrearContexto();
        await AñadirUsuariosAsync(context);
        var servicio = CrearServicio(context);
        var usuario = new UsuarioActual(AutonomoId, Rol.AUTONOMO);

        Assert.True((await servicio.SolicitarAsync(usuario, "ASE-000123", CancellationToken.None)).EsCorrecto);
        var repetida = await servicio.SolicitarAsync(usuario, "ASE-000123", CancellationToken.None);

        Assert.Equal(VinculacionErrores.SolicitudDuplicada, repetida.Error);
        Assert.Equal(1, await context.Vinculaciones.CountAsync());
    }

    [Fact]
    public async Task AceptarAsync_AdministradorDeLaAsesoria_ActivaLaSolicitud()
    {
        await using var context = CrearContexto();
        await AñadirUsuariosAsync(context);
        var servicio = CrearServicio(context);
        var solicitud = (await servicio.SolicitarAsync(
            new(AutonomoId, Rol.AUTONOMO),
            "ASE-000123",
            CancellationToken.None)).Valor!;

        var resultado = await servicio.AceptarAsync(
            new(AdministradorId, Rol.GESTOR),
            solicitud.Id,
            CancellationToken.None);

        Assert.True(resultado.EsCorrecto);
        Assert.Equal(EstadoVinculacion.ACTIVA, resultado.Valor!.Estado);
        Assert.Equal(AdministradorId, resultado.Valor.AceptadaPorId);
    }

    [Fact]
    public async Task AceptarAsync_OtroGestor_NoPuedeAceptar()
    {
        await using var context = CrearContexto();
        await AñadirUsuariosAsync(context);
        var servicio = CrearServicio(context);
        var solicitud = (await servicio.SolicitarAsync(
            new(AutonomoId, Rol.AUTONOMO),
            "ASE-000123",
            CancellationToken.None)).Valor!;

        var resultado = await servicio.AceptarAsync(
            new(OtraAdministradoraId, Rol.GESTOR),
            solicitud.Id,
            CancellationToken.None);

        Assert.Equal(VinculacionErrores.SoloAdministradorPuedeGestionar, resultado.Error);
        Assert.Equal(EstadoVinculacion.PENDIENTE, (await context.Vinculaciones.SingleAsync()).Estado);
    }

    [Fact]
    public async Task AceptarAsync_AutonomoConOtraActiva_DevuelveConflicto()
    {
        await using var context = CrearContexto();
        await AñadirUsuariosAsync(context);
        context.Vinculaciones.Add(new Vinculacion
        {
            Id = Guid.NewGuid(),
            AutonomoId = AutonomoId,
            CodigoAsesoria = "ASE-999998",
            Estado = EstadoVinculacion.ACTIVA,
            FechaSolicitud = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        var servicio = CrearServicio(context);
        var solicitud = (await servicio.SolicitarAsync(
            new(AutonomoId, Rol.AUTONOMO),
            "ASE-000123",
            CancellationToken.None)).Valor!;

        var resultado = await servicio.AceptarAsync(
            new(AdministradorId, Rol.GESTOR),
            solicitud.Id,
            CancellationToken.None);

        Assert.Equal(VinculacionErrores.AutonomoYaTieneAsesoria, resultado.Error);
        Assert.Equal(EstadoVinculacion.PENDIENTE, (await context.Vinculaciones.SingleAsync(v => v.Id == solicitud.Id)).Estado);
    }

    [Fact]
    public async Task FinalizarAsync_Administrador_ConservaLaVinculacionComoFinalizada()
    {
        await using var context = CrearContexto();
        await AñadirUsuariosAsync(context);
        var servicio = CrearServicio(context);
        var solicitud = (await servicio.SolicitarAsync(
            new(AutonomoId, Rol.AUTONOMO),
            "ASE-000123",
            CancellationToken.None)).Valor!;
        await servicio.AceptarAsync(new(AdministradorId, Rol.GESTOR), solicitud.Id, CancellationToken.None);

        var resultado = await servicio.FinalizarAsync(
            new(AdministradorId, Rol.GESTOR),
            solicitud.Id,
            CancellationToken.None);

        Assert.True(resultado.EsCorrecto);
        Assert.Equal(EstadoVinculacion.FINALIZADA, resultado.Valor!.Estado);
        Assert.NotNull(resultado.Valor.FechaFinalizacion);
        Assert.Equal(1, await context.Vinculaciones.CountAsync());
    }

    private static VinculacionesService CrearServicio(ApplicationDbContext context) =>
        new(context, TimeProvider.System);

    private static ApplicationDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task AñadirUsuariosAsync(ApplicationDbContext context)
    {
        context.Usuarios.AddRange(
            new Usuario
            {
                Id = AutonomoId,
                Nombre = "Ana",
                PrimerApellido = "Autónoma",
                SegundoApellido = "Demo",
                Email = "ana@gestio.local",
                Nif = "11111111A",
                Rol = Rol.AUTONOMO
            },
            new Usuario
            {
                Id = AdministradorId,
                Nombre = "Administrador",
                PrimerApellido = "Principal",
                SegundoApellido = "Demo",
                Email = "admin@gestio.local",
                Nif = "22222222B",
                CodigoAsesoria = "ASE-000123",
                Rol = Rol.GESTOR
            },
            new Usuario
            {
                Id = OtraAdministradoraId,
                Nombre = "Administradora",
                PrimerApellido = "Otra",
                SegundoApellido = "Demo",
                Email = "otra@gestio.local",
                Nif = "33333333C",
                CodigoAsesoria = "ASE-000456",
                Rol = Rol.GESTOR
            });
        await context.SaveChangesAsync();
    }
}
