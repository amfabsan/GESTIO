using System.Security.Cryptography;
using Gestio.Api.Common;
using Gestio.Api.Controllers;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Models.Responses;
using Gestio.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gestio.Api.Tests;

public sealed class DocumentosTests
{
    private static readonly byte[] PdfValido = "%PDF-1.7\nContenido de prueba"u8.ToArray();
    private static readonly UsuarioActual Ramon = new(DbInitializer.RamonId, Rol.AUTONOMO);
    private static readonly UsuarioActual Pedro = new(DbInitializer.PedroId, Rol.GESTOR);

    [Fact]
    public async Task GetDocumentos_AutonomoSoloVeLosSuyos_YGestorVeLosDeSusClientes()
    {
        await using var context = CrearContexto(nameof(GetDocumentos_AutonomoSoloVeLosSuyos_YGestorVeLosDeSusClientes));

        await DbInitializer.SeedAsync(context);
        await DbInitializer.SeedAsync(context);

        Assert.Equal(2, await context.Usuarios.CountAsync());
        Assert.Equal(5, await context.Documentos.CountAsync());
        Assert.Equal(2, await context.ExtraccionesIa.CountAsync());
        Assert.Equal(3, await context.Anomalias.CountAsync());

        var deRamon = await ObtenerListado(CrearControlador(context, Ramon));
        Assert.Equal(3, deRamon.Count);
        Assert.All(deRamon, documento => Assert.Equal("Ramón", documento.Usuario));

        var dePedro = await ObtenerListado(CrearControlador(context, Pedro));
        Assert.Equal(5, dePedro.Count);
        Assert.Contains(dePedro, documento => documento.NombreArchivo == "factura-luz-enero.pdf");
        Assert.Contains(dePedro, documento => documento.Extraccion is not null);
        Assert.Contains(dePedro, documento => documento.Anomalias.Count > 0);
    }

    [Fact]
    public async Task GetDocumento_AutonomoAjenoNoPuedeConsultarlo()
    {
        await using var context = CrearContexto(nameof(GetDocumento_AutonomoAjenoNoPuedeConsultarlo));
        await DbInitializer.SeedAsync(context);

        var otroAutonomo = new UsuarioActual(Guid.NewGuid(), Rol.AUTONOMO);
        context.Usuarios.Add(new Usuario
        {
            Id = otroAutonomo.Id,
            Nombre = "Lucía",
            PrimerApellido = "Pérez",
            SegundoApellido = "Gil",
            Email = "lucia@gestio.local",
            Nif = "11111111H",
            Rol = Rol.AUTONOMO
        });
        await context.SaveChangesAsync();

        var documentoDeRamon = await context.Documentos.FirstAsync(documento => documento.UsuarioId == DbInitializer.RamonId);
        var resultado = await CrearControlador(context, otroAutonomo).GetDocumento(documentoDeRamon.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(resultado.Result);
    }

    [Fact]
    public async Task SubirAsync_PdfValido_GuardaOriginalMetadatosYEvento()
    {
        await using var context = CrearContexto(nameof(SubirAsync_PdfValido_GuardaOriginalMetadatosYEvento));
        await DbInitializer.SeedAsync(context);
        var storage = new AlmacenamientoEnMemoria();

        using var contenido = new MemoryStream(PdfValido);
        var resultado = await CrearServicio(context, storage)
            .SubirAsync(Ramon, "factura.pdf", contenido, PdfValido.Length, CancellationToken.None);

        Assert.True(resultado.EsCorrecto);
        var documento = await context.Documentos
            .Include(documento => documento.Eventos)
            .SingleAsync(documento => documento.Id == resultado.Valor!.Id);

        Assert.Equal(EstadoDocumento.SUBIDO, documento.Estado);
        Assert.Equal(DbInitializer.RamonId, documento.UsuarioId);
        Assert.Equal("factura.pdf", documento.NombreArchivo);
        Assert.Equal("application/pdf", documento.TipoMime);
        Assert.Equal((long)PdfValido.Length, documento.TamanoBytes);
        Assert.Equal(OrigenDocumento.SUBIDA_WEB, documento.Origen);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(PdfValido)), documento.HashSha256);
        Assert.Equal(PdfValido, storage.Archivos[documento.StorageKey!]);

        var evento = Assert.Single(documento.Eventos);
        Assert.Equal(TipoEventoDocumento.SUBIDA, evento.Tipo);
        Assert.Equal(DbInitializer.RamonId, evento.UsuarioId);
    }

    [Fact]
    public async Task SubirAsync_Gestor_NoPuedeSubir()
    {
        await using var context = CrearContexto(nameof(SubirAsync_Gestor_NoPuedeSubir));
        var storage = new AlmacenamientoEnMemoria();

        using var contenido = new MemoryStream(PdfValido);
        var resultado = await CrearServicio(context, storage)
            .SubirAsync(Pedro, "factura.pdf", contenido, PdfValido.Length, CancellationToken.None);

        Assert.Equal(DocumentoErrores.SoloAutonomoPuedeSubir, resultado.Error);
        Assert.Empty(storage.Archivos);
        Assert.Equal(0, await context.Documentos.CountAsync());
    }

    [Fact]
    public async Task SubirAsync_MismoArchivoDosVeces_NoSeBloquea()
    {
        await using var context = CrearContexto(nameof(SubirAsync_MismoArchivoDosVeces_NoSeBloquea));
        await DbInitializer.SeedAsync(context);
        var servicio = CrearServicio(context, new AlmacenamientoEnMemoria());

        for (var i = 0; i < 2; i++)
        {
            using var contenido = new MemoryStream(PdfValido);
            var resultado = await servicio.SubirAsync(Ramon, "factura.pdf", contenido, PdfValido.Length, CancellationToken.None);
            Assert.True(resultado.EsCorrecto);
        }

        Assert.Equal(2, await context.Documentos.CountAsync(documento => documento.NombreArchivo == "factura.pdf"));
    }

    [Theory]
    [InlineData("virus.pdf", new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03 })]
    [InlineData("foto.png", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00 })]
    [InlineData("documento.docx", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D })]
    public async Task SubirAsync_TipoRealNoPermitido_NoCreaDocumento(string nombreArchivo, byte[] bytes)
    {
        await using var context = CrearContexto($"{nameof(SubirAsync_TipoRealNoPermitido_NoCreaDocumento)}-{nombreArchivo}");
        var storage = new AlmacenamientoEnMemoria();

        using var contenido = new MemoryStream(bytes);
        var resultado = await CrearServicio(context, storage)
            .SubirAsync(Ramon, nombreArchivo, contenido, bytes.Length, CancellationToken.None);

        Assert.Equal("documento.tipo_no_permitido", resultado.Error?.Codigo);
        Assert.Empty(storage.Archivos);
        Assert.Equal(0, await context.Documentos.CountAsync());
    }

    [Fact]
    public async Task SubirAsync_SuperaTamanoMaximo_NoCreaDocumento()
    {
        await using var context = CrearContexto(nameof(SubirAsync_SuperaTamanoMaximo_NoCreaDocumento));
        var storage = new AlmacenamientoEnMemoria();

        using var contenido = new MemoryStream(PdfValido);
        var resultado = await CrearServicio(context, storage, tamanoMaximoBytes: 10)
            .SubirAsync(Ramon, "factura.pdf", contenido, PdfValido.Length, CancellationToken.None);

        Assert.Equal("documento.tamano_excedido", resultado.Error?.Codigo);
        Assert.Empty(storage.Archivos);
        Assert.Equal(0, await context.Documentos.CountAsync());
    }

    [Fact]
    public async Task SubirAsync_FallaElAlmacenamiento_NoCreaDocumento()
    {
        await using var context = CrearContexto(nameof(SubirAsync_FallaElAlmacenamiento_NoCreaDocumento));
        var storage = new AlmacenamientoEnMemoria { Fallar = true };

        using var contenido = new MemoryStream(PdfValido);
        var resultado = await CrearServicio(context, storage)
            .SubirAsync(Ramon, "factura.pdf", contenido, PdfValido.Length, CancellationToken.None);

        Assert.Equal(DocumentoErrores.AlmacenamientoFallido, resultado.Error);
        Assert.Equal(0, await context.Documentos.CountAsync());
    }

    private static ApplicationDbContext CrearContexto(string nombre) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(nombre).Options);

    private static DocumentosService CrearServicio(ApplicationDbContext context, IFileStorageService storage, long tamanoMaximoBytes = 1024) =>
        new(
            context,
            storage,
            Options.Create(new DocumentosOptions { TamanoMaximoBytes = tamanoMaximoBytes }),
            TimeProvider.System,
            NullLogger<DocumentosService>.Instance);

    private static DocumentosController CrearControlador(ApplicationDbContext context, UsuarioActual usuario) =>
        new(CrearServicio(context, new AlmacenamientoEnMemoria()), new UsuarioActualFijo(usuario));

    private static async Task<IReadOnlyList<DocumentoResponse>> ObtenerListado(DocumentosController controller)
    {
        var resultado = await controller.GetDocumentos(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        return Assert.IsAssignableFrom<IReadOnlyList<DocumentoResponse>>(ok.Value);
    }
}

// Sustituye al perfil seleccionado en los tests: siempre devuelve el usuario indicado.
file sealed class UsuarioActualFijo(UsuarioActual usuario) : ICurrentUserService
{
    public Task<UsuarioActual?> ObtenerAsync(CancellationToken cancellationToken) => Task.FromResult<UsuarioActual?>(usuario);
}

file sealed class AlmacenamientoEnMemoria : IFileStorageService
{
    public Dictionary<string, byte[]> Archivos { get; } = [];

    public bool Fallar { get; init; }

    public async Task<string> GuardarAsync(Stream contenido, Guid usuarioId, string extension, CancellationToken cancellationToken)
    {
        if (Fallar) throw new IOException("Disco lleno");

        using var copia = new MemoryStream();
        await contenido.CopyToAsync(copia, cancellationToken);
        var clave = $"{usuarioId:N}/{Guid.NewGuid():N}{extension}";
        Archivos[clave] = copia.ToArray();
        return clave;
    }

    public Task<Stream?> AbrirAsync(string storageKey, CancellationToken cancellationToken) =>
        Task.FromResult<Stream?>(Archivos.TryGetValue(storageKey, out var bytes) ? new MemoryStream(bytes) : null);

    public Task EliminarAsync(string storageKey, CancellationToken cancellationToken)
    {
        Archivos.Remove(storageKey);
        return Task.CompletedTask;
    }
}