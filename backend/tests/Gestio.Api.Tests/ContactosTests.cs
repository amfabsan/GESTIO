using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Tests;

public sealed class ContactosTests
{
    private static readonly Guid AutonomoId = Guid.Parse("11111111-1111-1111-1111-111111111111");

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

    [Theory]
    [InlineData("612345678")]
    [InlineData("712345678")]
    [InlineData("812345678")]
    [InlineData("912345678")]
    [InlineData("612 345 678")]
    [InlineData("+34 612 345 678")]
    [InlineData("+34 (91) 234-56-78")]
    public async Task Crear_AceptaTelefonoEspanolValido(string telefono)
    {
        await using var db = CrearDb();
        var usuario = new UsuarioActual(AutonomoId, Rol.AUTONOMO);
        var resultado = await new ContactosService(db).CrearAsync(usuario, AutonomoId,
            new GuardarContactoRequest("CLIENTE", "Contacto", "12345678Z", null, telefono, null), default);

        Assert.True(resultado.EsCorrecto);
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("512345678")]
    [InlineData("61234567")]
    [InlineData("6123456789")]
    [InlineData("0034612345678")]
    [InlineData("+34 512345678")]
    [InlineData("+34 61234567")]
    [InlineData("teléfono")]
    public async Task Crear_RechazaTelefonoEspanolInvalido(string telefono)
    {
        await using var db = CrearDb();
        var usuario = new UsuarioActual(AutonomoId, Rol.AUTONOMO);
        var resultado = await new ContactosService(db).CrearAsync(usuario, AutonomoId,
            new GuardarContactoRequest("CLIENTE", "Contacto", "12345678Z", null, telefono, null), default);

        Assert.False(resultado.EsCorrecto);
        Assert.Equal("Contacto.TelefonoNoValido", resultado.Error!.Codigo);
        Assert.Empty(await db.Contactos.ToListAsync());
    }

    [Fact]
    public async Task Eliminar_RechazaContactoUsadoPorFactura()
    {
        await using var db = CrearDb();
        var contacto = CrearContacto();
        db.Contactos.Add(contacto);
        db.Facturas.Add(new Factura
        {
            Id = Guid.NewGuid(), AutonomoId = AutonomoId, ContactoId = contacto.Id, Serie = "F-2026"
        });
        await db.SaveChangesAsync();

        var resultado = await new ContactosService(db).EliminarAsync(new(AutonomoId, Rol.AUTONOMO), AutonomoId, contacto.Id, default);

        Assert.False(resultado.EsCorrecto);
        Assert.Equal("Contacto.Referenciado", resultado.Error!.Codigo);
        Assert.True(await db.Contactos.AnyAsync(c => c.Id == contacto.Id));
    }

    [Fact]
    public async Task Eliminar_PermiteContactoSinFacturas()
    {
        await using var db = CrearDb();
        var contacto = CrearContacto();
        db.Contactos.Add(contacto);
        await db.SaveChangesAsync();

        var resultado = await new ContactosService(db).EliminarAsync(new(AutonomoId, Rol.AUTONOMO), AutonomoId, contacto.Id, default);

        Assert.True(resultado.EsCorrecto);
        Assert.True(resultado.Valor);
        Assert.False(await db.Contactos.AnyAsync(c => c.Id == contacto.Id));
    }

    private static Contacto CrearContacto() => new()
    {
        Id = Guid.NewGuid(),
        AutonomoId = AutonomoId,
        Clasificacion = "CLIENTE",
        Nombre = "Contacto",
        Nif = "12345678Z"
    };

    private static ApplicationDbContext CrearDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
