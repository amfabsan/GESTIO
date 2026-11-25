using Gestio.Api.Controllers;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Tests;

public sealed class MensajesDocumentoTests
{
    [Fact]
    public async Task ConversacionPersiste_AvisaAlDestinatario_YNoModificaLaValidacion()
    {
        var opciones = Opciones();
        var (propietario, gestor, ajeno, documento) = await PrepararAsync(opciones);
        MensajesDocumentoController.MensajeResponse pregunta;

        await using (var db = new ApplicationDbContext(opciones))
        {
            var respuesta = Assert.IsType<ObjectResult>(await Controlador(db, propietario)
                .Enviar(documento, new("¿Puedes revisar el justificante?"), default));
            Assert.Equal(201, respuesta.StatusCode);
            pregunta = Assert.IsType<MensajesDocumentoController.MensajeResponse>(respuesta.Value);
            Assert.Equal(propietario.Id, pregunta.AutorId);
            Assert.StartsWith("Ramón", pregunta.Autor);
            Assert.NotEqual(default, pregunta.Fecha);
        }

        await using (var db = new ApplicationDbContext(opciones))
        {
            var controlador = Controlador(db, gestor);
            var aviso = Assert.Single(Conversaciones(await controlador.Listar(default)));
            Assert.Equal(documento, aviso.DocumentoId);
            Assert.Equal(1, aviso.SinLeer);
            Assert.Equal(pregunta, Assert.Single(Mensajes(await controlador.Obtener(documento, default))));
            Assert.IsType<NoContentResult>(await controlador.MarcarLeidos(documento, new([pregunta.Id]), default));
            Assert.Equal(0, Assert.Single(Conversaciones(await controlador.Listar(default))).SinLeer);
            Assert.Equal(201, Assert.IsType<ObjectResult>(await controlador.Enviar(documento, new("Sí, está correcto."), default)).StatusCode);

            Assert.Empty(Conversaciones(await Controlador(db, ajeno).Listar(default)));
            Assert.Equal(404, Assert.IsType<ObjectResult>(await Controlador(db, ajeno).Obtener(documento, default)).StatusCode);
        }

        await using (var db = new ApplicationDbContext(opciones))
        {
            var controlador = Controlador(db, propietario);
            Assert.Equal(1, Assert.Single(Conversaciones(await controlador.Listar(default))).SinLeer);
            var hilo = Mensajes(await controlador.Obtener(documento, default)).ToArray();
            Assert.Equal(2, hilo.Length);
            Assert.Equal(pregunta, hilo[0]);
            Assert.Equal(gestor.Id, hilo[1].AutorId);
            Assert.True(hilo[0].Fecha <= hilo[1].Fecha);
            Assert.Equal(EstadoDocumento.PENDIENTE_VALIDACION, (await db.Documentos.SingleAsync()).Estado);
            Assert.Empty(await db.EventosDocumento.ToListAsync());

            (await db.Vinculaciones.SingleAsync()).Estado = EstadoVinculacion.FINALIZADA;
            await db.SaveChangesAsync();
            var sinVinculo = Controlador(db, gestor);
            Assert.Empty(Conversaciones(await sinVinculo.Listar(default)));
            Assert.Equal(404, Assert.IsType<ObjectResult>(await sinVinculo.Obtener(documento, default)).StatusCode);
            Assert.Equal(404, Assert.IsType<ObjectResult>(await sinVinculo.Enviar(documento, new("Otro mensaje"), default)).StatusCode);
            Assert.Equal(2, Mensajes(await controlador.Obtener(documento, default)).Count());
        }
    }

    [Fact]
    public async Task RechazaBlancosYLongitudExcesiva_YAceptaElLimite()
    {
        var opciones = Opciones();
        var (propietario, _, _, documento) = await PrepararAsync(opciones);
        await using var db = new ApplicationDbContext(opciones);
        var controlador = Controlador(db, propietario);

        foreach (var contenido in new[] { null, "", " \t\n ", new string('x', 1001) })
            Assert.Equal(400, Assert.IsType<ObjectResult>(await controlador.Enviar(documento, new(contenido), default)).StatusCode);

        Assert.Empty(await db.MensajesDocumento.ToListAsync());
        Assert.Empty(await db.AvisosMensajeDocumento.ToListAsync());
        Assert.Equal(201, Assert.IsType<ObjectResult>(await controlador.Enviar(documento, new(new string('x', 1000)), default)).StatusCode);
    }

    private static IEnumerable<MensajesDocumentoController.MensajeResponse> Mensajes(ActionResult resultado) =>
        Assert.IsAssignableFrom<IEnumerable<MensajesDocumentoController.MensajeResponse>>(Assert.IsType<OkObjectResult>(resultado).Value);

    private static IEnumerable<MensajesDocumentoController.ConversacionResponse> Conversaciones(ActionResult resultado) =>
        Assert.IsAssignableFrom<IEnumerable<MensajesDocumentoController.ConversacionResponse>>(Assert.IsType<OkObjectResult>(resultado).Value);

    private static MensajesDocumentoController Controlador(ApplicationDbContext db, UsuarioActual usuario) =>
        new(db, new AccesoDocumentos(db), new UsuarioFijo(usuario))
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };

    private static DbContextOptions<ApplicationDbContext> Opciones() =>
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private static async Task<(UsuarioActual Propietario, UsuarioActual Gestor, UsuarioActual Ajeno, Guid Documento)> PrepararAsync(
        DbContextOptions<ApplicationDbContext> opciones)
    {
        await using var db = new ApplicationDbContext(opciones);
        var propietario = CrearUsuario("Ramón", Rol.AUTONOMO);
        var gestor = CrearUsuario("Pedro", Rol.GESTOR, "ASESORIA-1");
        var ajeno = CrearUsuario("Ana", Rol.GESTOR, "ASESORIA-2");
        db.Usuarios.AddRange(propietario, gestor, ajeno);
        db.Vinculaciones.Add(new Vinculacion
        {
            AutonomoId = propietario.Id, CodigoAsesoria = gestor.CodigoAsesoria!, Estado = EstadoVinculacion.ACTIVA
        });
        var documento = new Documento
        {
            Id = Guid.NewGuid(), UsuarioId = propietario.Id, NombreArchivo = "justificante.pdf",
            TipoMime = "application/pdf", Estado = EstadoDocumento.PENDIENTE_VALIDACION
        };
        db.Documentos.Add(documento);
        await db.SaveChangesAsync();

        return (new(propietario.Id, propietario.Rol), new(gestor.Id, gestor.Rol), new(ajeno.Id, ajeno.Rol), documento.Id);
    }

    private static Usuario CrearUsuario(string nombre, Rol rol, string? asesoria = null) => new()
    {
        Id = Guid.NewGuid(), Nombre = nombre, PrimerApellido = "Pérez", SegundoApellido = "",
        Email = nombre + "@example.test", Nif = nombre, Rol = rol, CodigoAsesoria = asesoria
    };

    private sealed class UsuarioFijo(UsuarioActual usuario) : ICurrentUserService
    {
        public Task<UsuarioActual?> ObtenerAsync(CancellationToken cancellationToken) => Task.FromResult<UsuarioActual?>(usuario);
    }
}
