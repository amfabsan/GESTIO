using Gestio.Api.Controllers;
using Gestio.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Tests;

public sealed class DocumentosTests
{
    [Fact]
    public async Task SeedAsync_CreaLosDatosDemoSinDuplicarlos_YElEndpointLosDevuelve()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(SeedAsync_CreaLosDatosDemoSinDuplicarlos_YElEndpointLosDevuelve))
            .Options;

        await using var context = new ApplicationDbContext(options);

        await DbInitializer.SeedAsync(context);
        await DbInitializer.SeedAsync(context);

        Assert.Equal(2, await context.Usuarios.CountAsync());
        Assert.Equal(5, await context.Documentos.CountAsync());
        Assert.Equal(2, await context.ExtraccionesIa.CountAsync());
        Assert.Equal(3, await context.Anomalias.CountAsync());

        var controller = new DocumentosController(context);
        var actionResult = await controller.GetDocumentos(CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var documentos = Assert.IsAssignableFrom<IEnumerable<DocumentoResponse>>(okResult.Value);

        Assert.Equal(5, documentos.Count());
        Assert.Contains(documentos, documento => documento.NombreArchivo == "factura-luz-enero.pdf");
        Assert.Contains(documentos, documento => documento.Estado == "Procesando");
        Assert.Contains(documentos, documento => documento.Extraccion is not null);
        Assert.Contains(documentos, documento => documento.Anomalias.Count > 0);
    }
}