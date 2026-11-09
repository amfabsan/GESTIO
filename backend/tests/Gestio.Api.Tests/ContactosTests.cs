using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Tests;

public sealed class ContactosTests
{
    [Theory]
    [InlineData("12345678Z")]
    [InlineData("X1234567L")]
    [InlineData("B99286320")]
    [InlineData("Q1234567D")]
    [InlineData(" 1234 5678-z ")]
    public async Task Crear_AceptaDniNieYNifDeEmpresaConControlValido(string nif)
    {
        await using var db = CrearDb();
        var usuario = new UsuarioActual(Guid.Parse("11111111-1111-1111-1111-111111111111"), Rol.AUTONOMO);
        var resultado = await new ContactosService(db).CrearAsync(usuario, usuario.Id,
            new GuardarContactoRequest("CLIENTE", "Contacto", nif, null, null, null), default);

        Assert.True(resultado.EsCorrecto);
        Assert.Equal(ContactosService.NormalizarNif(nif), resultado.Valor!.Nif);
    }

    [Theory]
    [InlineData("12345678A")]
    [InlineData("X1234567A")]
    [InlineData("B99286321")]
    [InlineData("Q12345674")]
    [InlineData("I12345678")]
    [InlineData("1234567Z")]
    public async Task Crear_RechazaIdentificadoresConEstructuraOControlInvalido(string nif)
    {
        await using var db = CrearDb();
        var usuario = new UsuarioActual(Guid.Parse("11111111-1111-1111-1111-111111111111"), Rol.AUTONOMO);
        var resultado = await new ContactosService(db).CrearAsync(usuario, usuario.Id,
            new GuardarContactoRequest("CLIENTE", "Contacto", nif, null, null, null), default);

        Assert.False(resultado.EsCorrecto);
        Assert.Equal("Contacto.NifNoValido", resultado.Error!.Codigo);
        Assert.Empty(await db.Contactos.ToListAsync());
    }

    private static ApplicationDbContext CrearDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
