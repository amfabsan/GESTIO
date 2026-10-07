using Gestio.Api.Controllers;
using Gestio.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Tests;

public sealed class UsuariosTests
{
    [Fact]
    public async Task SeedAsync_CreaPerfilesConNombreYApellidos_YElEndpointLosDevuelve()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);

        await DbInitializer.SeedAsync(context);

        await DbInitializer.SeedAsync(context);

        Assert.Equal(2, await context.Usuarios.CountAsync());
        Assert.Equal("Ramón", (await context.Usuarios.SingleAsync(usuario => usuario.Id == DbInitializer.RamonId)).Nombre);
        Assert.Equal("García", (await context.Usuarios.SingleAsync(usuario => usuario.Id == DbInitializer.RamonId)).PrimerApellido);
        Assert.Equal("López", (await context.Usuarios.SingleAsync(usuario => usuario.Id == DbInitializer.RamonId)).SegundoApellido);
        Assert.Equal("Pedro", (await context.Usuarios.SingleAsync(usuario => usuario.Id == DbInitializer.PedroId)).Nombre);
        Assert.Equal("Sánchez", (await context.Usuarios.SingleAsync(usuario => usuario.Id == DbInitializer.PedroId)).PrimerApellido);
        Assert.Equal("Martín", (await context.Usuarios.SingleAsync(usuario => usuario.Id == DbInitializer.PedroId)).SegundoApellido);

        var controller = new UsuariosController(context);
        var actionResult = await controller.GetPerfiles(CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var perfiles = Assert.IsAssignableFrom<IReadOnlyList<UsuarioPerfilResponse>>(okResult.Value);

        Assert.Collection(perfiles,
            perfil =>
            {
                Assert.Equal(DbInitializer.RamonId, perfil.Id);
                Assert.Equal("Ramón", perfil.Nombre);
                Assert.Equal("García", perfil.PrimerApellido);
                Assert.Equal("López", perfil.SegundoApellido);
                Assert.Equal("AUTONOMO", perfil.Rol);
            },
            perfil =>
            {
                Assert.Equal(DbInitializer.PedroId, perfil.Id);
                Assert.Equal("Pedro", perfil.Nombre);
                Assert.Equal("Sánchez", perfil.PrimerApellido);
                Assert.Equal("Martín", perfil.SegundoApellido);
                Assert.Equal("GESTOR", perfil.Rol);
            });
    }

    [Fact]
    public async Task SeedAsync_NoSobrescribeUsuariosExistentes()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);

        await DbInitializer.SeedAsync(context);

        var ramon = await context.Usuarios.SingleAsync(usuario => usuario.Id == DbInitializer.RamonId);
        ramon.Nombre = "Nombre editado";
        ramon.PrimerApellido = "Primer apellido editado";
        ramon.SegundoApellido = "Segundo apellido editado";
        await context.SaveChangesAsync();

        await DbInitializer.SeedAsync(context);

        Assert.Equal("Nombre editado", ramon.Nombre);
        Assert.Equal("Primer apellido editado", ramon.PrimerApellido);
        Assert.Equal("Segundo apellido editado", ramon.SegundoApellido);
        Assert.Equal(2, await context.Usuarios.CountAsync());
    }
}
