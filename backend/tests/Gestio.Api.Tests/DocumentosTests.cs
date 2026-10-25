using System.Security.Cryptography;
using Gestio.Api.Common;
using Gestio.Api.Controllers;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Gestio.Api.Models.Requests;
using Gestio.Api.Models.Responses;

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

        // El gestor solo ve los de sus clientes con vinculación activa, no los suyos propios.
        var dePedro = await ObtenerListado(CrearControlador(context, Pedro));
        Assert.Equal(3, dePedro.Count);
        Assert.All(dePedro, documento => Assert.Equal("Ramón", documento.Usuario));
        Assert.DoesNotContain(dePedro, documento => documento.NombreArchivo == "factura-proveedor-demo.pdf");
        Assert.Contains(dePedro, documento => documento.NombreArchivo == "factura-luz-enero.pdf");
        Assert.Contains(dePedro, documento => documento.Extraccion is not null);
        Assert.Contains(dePedro, documento => documento.Anomalias.Count > 0);
    }

    [Fact]
    public async Task GetDocumentos_GestorSoloVeDocumentosConVinculacionActiva()
    {
        await using var context = CrearContexto(nameof(GetDocumentos_GestorSoloVeDocumentosConVinculacionActiva));
        await DbInitializer.SeedAsync(context);

        var cliente = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Luis",
            PrimerApellido = "Cliente",
            SegundoApellido = "Demo",
            Email = "luis@gestio.local",
            Nif = "44444444D",
            Rol = Rol.AUTONOMO
        };
        var documento = new Documento
        {
            Id = Guid.NewGuid(),
            UsuarioId = cliente.Id,
            NombreArchivo = "cliente-pendiente.pdf",
            TipoMime = "application/pdf",
            Origen = OrigenDocumento.SUBIDA_WEB,
            FechaSubida = DateTimeOffset.UtcNow,
            Estado = EstadoDocumento.SUBIDO
        };
        var vinculacion = new Vinculacion
        {
            Id = Guid.NewGuid(),
            AutonomoId = cliente.Id,
            CodigoAsesoria = "ASE-000123",
            Estado = EstadoVinculacion.PENDIENTE,
            FechaSolicitud = DateTimeOffset.UtcNow
        };

        context.Usuarios.Add(cliente);
        context.Documentos.Add(documento);
        context.Vinculaciones.Add(vinculacion);
        await context.SaveChangesAsync();

        var sinAcceso = await ObtenerListado(CrearControlador(context, Pedro));
        Assert.DoesNotContain(sinAcceso, item => item.Id == documento.Id);

        vinculacion.Estado = EstadoVinculacion.ACTIVA;
        await context.SaveChangesAsync();

        var conAcceso = await ObtenerListado(CrearControlador(context, Pedro));
        Assert.Contains(conAcceso, item => item.Id == documento.Id);

        vinculacion.Estado = EstadoVinculacion.FINALIZADA;
        await context.SaveChangesAsync();

        var accesoRetirado = await ObtenerListado(CrearControlador(context, Pedro));
        Assert.DoesNotContain(accesoRetirado, item => item.Id == documento.Id);
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

        Assert.Equal(EstadoDocumento.ERROR, documento.Estado);
        Assert.Equal(DbInitializer.RamonId, documento.UsuarioId);
        Assert.Equal("factura.pdf", documento.NombreArchivo);
        Assert.Equal("application/pdf", documento.TipoMime);
        Assert.Equal((long)PdfValido.Length, documento.TamanoBytes);
        Assert.Equal(OrigenDocumento.SUBIDA_WEB, documento.Origen);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(PdfValido)), documento.HashSha256);
        Assert.Equal(PdfValido, storage.Archivos[documento.StorageKey!]);

        var evento = Assert.Single(documento.Eventos, e => e.Tipo == TipoEventoDocumento.SUBIDA);
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

    // PA 126 (UT10) y PA 80 (UT08): la alerta de duplicado es automática, así que su evento no tiene actor.
    [Fact]
    public async Task SubirAsync_Duplicado_RegistraElEventoComoDelSistema()
    {
        await using var context = CrearContexto(nameof(SubirAsync_Duplicado_RegistraElEventoComoDelSistema));
        await DbInitializer.SeedAsync(context);
        var servicio = CrearServicio(context, new AlmacenamientoEnMemoria());

        Guid id = default;
        for (var i = 0; i < 2; i++)
        {
            using var contenido = new MemoryStream(PdfValido);
            id = (await servicio.SubirAsync(Ramon, "factura.pdf", contenido, PdfValido.Length, CancellationToken.None)).Valor!.Id;
        }
        context.ChangeTracker.Clear();

        var evento = await context.EventosDocumento.SingleAsync(e => e.DocumentoId == id && e.Tipo == TipoEventoDocumento.POSIBLE_DUPLICADO);
        Assert.Null(evento.UsuarioId);
    }

    // PA 102 (UT06) / PA 67 (UT07): la confianza de cada campo se guarda con la extracción y llega a la bandeja.
    [Fact]
    public async Task Procesar_GuardaLaConfianzaDeCadaCampo_YLaDevuelveLaApi()
    {
        await using var context = CrearContexto(nameof(Procesar_GuardaLaConfianzaDeCadaCampo_YLaDevuelveLaApi));
        await DbInitializer.SeedAsync(context);
        PalabraDocumento P(string texto, double x, double y) => new(texto, x, y, .08, .02, 95);
        PalabraDocumento[] palabras = [P("BASE", .05, .70), P("IVA", .25, .70), P("CUOTA", .45, .70), P("TOTAL", .7, .70),
            P("92,95", .82, .70), P("84,50", .05, .74), P("10%", .25, .74), P("8,45", .45, .74)];
        var lector = new LectorConLecturas(new TextoDocumento(DisposicionDocumento.Texto(palabras), "motor-prueba",
            [new LecturaDocumento(1, "pagina", "psm-3", false, palabras)]));
        var servicio = new DocumentosService(context, new AlmacenamientoEnMemoria(),
            Options.Create(new DocumentosOptions { TamanoMaximoBytes = 1024 }), TimeProvider.System,
            NullLogger<DocumentosService>.Instance, lector, new AccesoDocumentos(context));
        using var contenido = new MemoryStream(PdfValido);
        var id = (await servicio.SubirAsync(Ramon, "factura.pdf", contenido, PdfValido.Length, CancellationToken.None)).Valor!.Id;
        context.ChangeTracker.Clear();

        var respuesta = await CrearControlador(context, Pedro).GetDocumento(id, CancellationToken.None);
        var documento = Assert.IsType<DocumentoResponse>(Assert.IsType<OkObjectResult>(respuesta.Result).Value);

        Assert.Equal("ALTO", documento.Extraccion!.ConfianzaCampos["Total"]);
        Assert.Equal("ALTO", documento.Extraccion.ConfianzaCampos["CuotaIva"]);
        Assert.False(documento.Extraccion.ConfianzaCampos.ContainsKey("EmisorNif"));
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

    [Fact]
    public async Task ErrorOCR_ConservaOriginal_YReintentoConHistorial_YPermisos()
    {
        await using var context = CrearContexto(nameof(ErrorOCR_ConservaOriginal_YReintentoConHistorial_YPermisos));
        await DbInitializer.SeedAsync(context);
        var storage = new AlmacenamientoEnMemoria();
        var servicio = CrearServicio(context, storage);
        using var contenido = new MemoryStream(PdfValido);
        var subida = await servicio.SubirAsync(Ramon, "original.pdf", contenido, PdfValido.Length, CancellationToken.None);
        var id = subida.Valor!.Id;
        Assert.Equal(EstadoDocumento.ERROR, subida.Valor.Estado);
        Assert.Single(subida.Valor.Intentos);
        Assert.Contains("No está disponible", subida.Valor.Intentos.Single().Error!);
        var ajeno = new UsuarioActual(Guid.NewGuid(), Rol.AUTONOMO);
        Assert.Null(await servicio.AbrirOriginalAsync(ajeno, id, CancellationToken.None));
        Assert.False((await servicio.ProcesarAsync(ajeno, id, true, CancellationToken.None)).EsCorrecto);
        var original = await servicio.AbrirOriginalAsync(Ramon, id, CancellationToken.None);
        Assert.NotNull(original);
        using var copia = new MemoryStream();
        await original.Contenido.CopyToAsync(copia);
        await original.Contenido.DisposeAsync();
        Assert.Equal(PdfValido, copia.ToArray());
        var reintento = await servicio.ProcesarAsync(Ramon, id, true, CancellationToken.None);
        Assert.True(reintento.EsCorrecto);
        context.ChangeTracker.Clear();
        var recargado = await servicio.ObtenerAsync(Ramon, id, CancellationToken.None);
        Assert.Equal(2, recargado!.Intentos.Count);
        Assert.All(recargado.Intentos, i => { Assert.NotNull(i.Fin); Assert.Equal("ERROR", i.Resultado); });
        Assert.Equal(5, recargado.Eventos.Count);
        Assert.Single(storage.Archivos);
        var vinculo = await context.Vinculaciones.FirstAsync(v => v.AutonomoId == Ramon.Id);
        vinculo.Estado = EstadoVinculacion.FINALIZADA;
        await context.SaveChangesAsync();
        Assert.Null(await servicio.AbrirOriginalAsync(Pedro, id, CancellationToken.None));
        Assert.False((await servicio.ProcesarAsync(Pedro, id, true, CancellationToken.None)).EsCorrecto);
    }

    [Fact]
    public async Task ProcesamientoCorrecto_PermiteReextraccionConHistorial_YRechazaValidado()
    {
        await using var context = CrearContexto(nameof(ProcesamientoCorrecto_PermiteReextraccionConHistorial_YRechazaValidado));
        await DbInitializer.SeedAsync(context);
        var storage = new AlmacenamientoEnMemoria();
        // Doble sólo para comprobar la orquestación; no verifica el motor OCR.
        var servicio = new DocumentosService(context, storage,
            Options.Create(new DocumentosOptions { TamanoMaximoBytes = 1024 }), TimeProvider.System,
            NullLogger<DocumentosService>.Instance, new LectorParaPruebaDeFlujo(), new AccesoDocumentos(context));
        using var contenido = new MemoryStream(PdfValido);
        var subida = await servicio.SubirAsync(Ramon, "original.pdf", contenido, PdfValido.Length, CancellationToken.None);
        Assert.True(subida.EsCorrecto);
        Assert.Equal(EstadoDocumento.PENDIENTE_VALIDACION, subida.Valor!.Estado);
        Assert.Equal(12.34m, subida.Valor.ExtraccionIa!.Total);
        Assert.Null(subida.Valor.ExtraccionIa.BaseImponible);
        Assert.Null(subida.Valor.ExtraccionIa.FechaEmision);
        var intento = Assert.Single(subida.Valor.Intentos);
        Assert.Equal("CORRECTO", intento.Resultado);
        Assert.NotNull(intento.ValoresJson);
        Assert.NotNull(intento.Fin);
        Assert.True((await servicio.ProcesarAsync(Ramon, subida.Valor.Id, true, CancellationToken.None)).EsCorrecto);
        context.ChangeTracker.Clear();
        var recargado = await servicio.ObtenerAsync(Ramon, subida.Valor.Id, CancellationToken.None);
        Assert.Equal(12.34m, recargado!.ExtraccionIa!.Total);
        Assert.Equal(2, recargado.Intentos.Count);
        Assert.Equal(5, recargado.Eventos.Count);
        var validado = await context.Documentos.SingleAsync(d => d.Id == recargado.Id);
        validado.Estado = EstadoDocumento.VALIDADO;
        await context.SaveChangesAsync();
        Assert.False((await servicio.ProcesarAsync(Ramon, validado.Id, true, CancellationToken.None)).EsCorrecto);
    }

    [Theory]
    [InlineData("PROCESAMIENTO_COMPLETADO", TipoEventoDocumento.PROCESAMIENTO_COMPLETADO)]
    [InlineData("PROCESAMIENTO_ERROR", TipoEventoDocumento.PROCESAMIENTO_ERROR)]
    public void Historial_LeeTiposPersistidosPorVersionesAnteriores(string almacenado, TipoEventoDocumento esperado)
    {
        using var context = CrearContexto(nameof(Historial_LeeTiposPersistidosPorVersionesAnteriores));
        var propiedad = context.Model.FindEntityType(typeof(EventoDocumento))!
            .FindProperty(nameof(EventoDocumento.Tipo))!;
        var conversor = propiedad.GetTypeMapping().Converter!;
        Assert.Equal(esperado, conversor.ConvertFromProvider(almacenado));
        Assert.Equal(almacenado, conversor.ConvertToProvider(esperado));
    }

    [Fact]
    public async Task TextoReconocidoSinCampos_TerminaEnError_YConservaOriginal()
    {
        await using var context = CrearContexto(nameof(TextoReconocidoSinCampos_TerminaEnError_YConservaOriginal));
        await DbInitializer.SeedAsync(context);
        var storage = new AlmacenamientoEnMemoria();
        var servicio = new DocumentosService(context, storage,
            Options.Create(new DocumentosOptions { TamanoMaximoBytes = 1024 }), TimeProvider.System,
            NullLogger<DocumentosService>.Instance, new LectorParaPruebaDeFlujo("Texto legible sin datos fiscales identificables"), new AccesoDocumentos(context));
        using var contenido = new MemoryStream(PdfValido);
        var subida = await servicio.SubirAsync(Ramon, "documento.pdf", contenido, PdfValido.Length, CancellationToken.None);
        Assert.True(subida.EsCorrecto);
        Assert.Equal(EstadoDocumento.ERROR, subida.Valor!.Estado);
        Assert.Null(subida.Valor.ExtraccionIa);
        Assert.Contains("ningún dato", Assert.Single(subida.Valor.Intentos).Error!);
        Assert.Single(storage.Archivos);
    }

    [Fact]
    public async Task CorregirYAprobar_QuedanEnElHistorialConSuActorYValores()
    {
        await using var context = CrearContexto(nameof(CorregirYAprobar_QuedanEnElHistorialConSuActorYValores));
        await DbInitializer.SeedAsync(context);
        var servicio = new DocumentosService(context, new AlmacenamientoEnMemoria(),
            Options.Create(new DocumentosOptions { TamanoMaximoBytes = 1024 }), TimeProvider.System,
            NullLogger<DocumentosService>.Instance, new LectorParaPruebaDeFlujo(), new AccesoDocumentos(context));
        using var contenido = new MemoryStream(PdfValido);
        var id = (await servicio.SubirAsync(Ramon, "ticket.pdf", contenido, PdfValido.Length, CancellationToken.None)).Valor!.Id;
        context.ChangeTracker.Clear();

        var correccion = await servicio.CorregirAsync(Pedro, id,
        [
            new CampoValidacionRequest("EmisorNombre", "Comercio Corregido SL"),
            new CampoValidacionRequest("EmisorNif", "X8019576M"),
            new CampoValidacionRequest("NumeroFactura", "A19"),
            new CampoValidacionRequest("FechaEmision", "2026-01-19")
        ], CancellationToken.None);
        Assert.True(correccion.EsCorrecto, correccion.Error?.Mensaje);
        Assert.Equal("Ramón", correccion.Valor!.Usuario.Nombre);
        context.ChangeTracker.Clear();
        var aprobacion = await servicio.AprobarAsync(Pedro, id, CancellationToken.None);
        Assert.True(aprobacion.EsCorrecto, aprobacion.Error?.Mensaje);
        Assert.Equal("Ramón", aprobacion.Valor!.Usuario.Nombre);

        context.ChangeTracker.Clear();
        var historial = new HistorialDocumentoService(context, new AccesoDocumentos(context), new UsuarioActualFijo(Ramon));
        var eventos = (await historial.ObtenerAsync(id, 1, 20, CancellationToken.None)).Valor!.Elementos;

        Assert.Equal(
            [TipoEventoDocumento.SUBIDA, TipoEventoDocumento.PROCESAMIENTO_INICIADO, TipoEventoDocumento.PROCESAMIENTO_CORRECTO,
             TipoEventoDocumento.VALIDACION_CORREGIDA, TipoEventoDocumento.VALIDACION_CORREGIDA, TipoEventoDocumento.VALIDACION_CORREGIDA,
             TipoEventoDocumento.VALIDACION_CORREGIDA, TipoEventoDocumento.VALIDACION_APROBADA],
            eventos.Select(evento => evento.Tipo));
        Assert.All(eventos.Where(evento => evento.Tipo is TipoEventoDocumento.PROCESAMIENTO_INICIADO or TipoEventoDocumento.PROCESAMIENTO_CORRECTO),
            evento => Assert.Null(evento.Actor));
        var nif = Assert.Single(eventos, evento => evento.Cambio?.Campo == "EmisorNif");
        Assert.Equal(DbInitializer.PedroId, nif.Actor!.Id);
        Assert.Null(nif.Cambio!.ValorAnterior);
        Assert.Equal("X8019576M", nif.Cambio.ValorNuevo);
    }

    // PA 75-1 (UT07): aprobar con campos obligatorios ausentes o con formato incorrecto
    // devuelve VALIDACION indicando los campos afectados y el documento permanece
    // en PENDIENTE_VALIDACION.
    [Fact]
    public async Task Aprobar_ConDatosInvalidos_DejaPendienteYSeñalaCamposAfectados()
    {
        await using var context = CrearContexto(nameof(Aprobar_ConDatosInvalidos_DejaPendienteYSeñalaCamposAfectados));
        await DbInitializer.SeedAsync(context);
        var servicio = new DocumentosService(context, new AlmacenamientoEnMemoria(),
            Options.Create(new DocumentosOptions { TamanoMaximoBytes = 1024 }), TimeProvider.System,
            NullLogger<DocumentosService>.Instance, new LectorParaPruebaDeFlujo(), new AccesoDocumentos(context));
        using var contenido = new MemoryStream(PdfValido);
        var id = (await servicio.SubirAsync(Ramon, "ticket.pdf", contenido, PdfValido.Length, CancellationToken.None)).Valor!.Id;
        context.ChangeTracker.Clear();

        // El LectorParaPruebaDeFlujo deja la extracción solo con EmisorNombre y Total.
        // Faltan EmisorNif, NumeroFactura y FechaEmision: aprobar debe fallar.
        var vacio = await servicio.AprobarAsync(Pedro, id, CancellationToken.None);
        Assert.False(vacio.EsCorrecto);
        Assert.Equal(TipoError.VALIDACION, vacio.Error!.Tipo);
        Assert.Contains("NIF es obligatorio", vacio.Error.Mensaje);
        Assert.Contains("N.º factura es obligatorio", vacio.Error.Mensaje);
        Assert.Contains("Fecha es obligatorio", vacio.Error.Mensaje);
        Assert.DoesNotContain("Proveedor", vacio.Error.Mensaje);

        context.ChangeTracker.Clear();
        var trasVacio = await context.Documentos.AsNoTracking().SingleAsync(d => d.Id == id);
        Assert.Equal(EstadoDocumento.PENDIENTE_VALIDACION, trasVacio.Estado);

        // NIF con formato incorrecto: CorregirAsync rechaza el formato antes de persistir.
        var formato = await servicio.CorregirAsync(Pedro, id,
        [
            new CampoValidacionRequest("EmisorNif", "ABC"),
            new CampoValidacionRequest("NumeroFactura", "A1"),
            new CampoValidacionRequest("FechaEmision", "2026-01-01")
        ], CancellationToken.None);
        Assert.False(formato.EsCorrecto);
        Assert.Equal(TipoError.VALIDACION, formato.Error!.Tipo);
        Assert.Contains("NIF no tiene un formato válido", formato.Error.Mensaje);

        context.ChangeTracker.Clear();
        var trasFormato = await context.Documentos.AsNoTracking().SingleAsync(d => d.Id == id);
        Assert.Equal(EstadoDocumento.PENDIENTE_VALIDACION, trasFormato.Estado);
    }

    // PA 73-2 / 75-1 (UT07): los importes deben ser coherentes para aprobar.
    // errorEsperado es la etiqueta del campo señalado, o null si el documento se aprueba.
    [Theory]
    [InlineData(100.0, 21.0, 21.0, 121.0, null)]
    [InlineData(82.64, 21.0, 17.35, 99.99, null)]
    [InlineData(100.0, 21.0, 21.0, 106.0, null)]
    [InlineData(100.0, 21.0, null, 121.0, null)]
    [InlineData(99.0, null, null, 109.70, null)]
    [InlineData(100.0, 21.0, 21.0, 130.0, "Total")]
    [InlineData(100.0, null, 21.0, 130.0, "Total")]
    [InlineData(100.0, 21.0, null, 110.0, "Total")]
    [InlineData(100.0, 21.0, 25.0, 125.0, "Cuota de IVA")]
    public async Task Aprobar_ValidaCoherenciaDeImportes(
        double baseImponible, double? tipoIva, double? cuotaIva, double total, string? errorEsperado)
    {
        await using var context = CrearContexto(
            $"{nameof(Aprobar_ValidaCoherenciaDeImportes)}-{baseImponible}-{tipoIva}-{cuotaIva}-{total}");
        await DbInitializer.SeedAsync(context);
        var servicio = new DocumentosService(context, new AlmacenamientoEnMemoria(),
            Options.Create(new DocumentosOptions { TamanoMaximoBytes = 1024 }), TimeProvider.System,
            NullLogger<DocumentosService>.Instance, new LectorParaPruebaDeFlujo(), new AccesoDocumentos(context));
        using var contenido = new MemoryStream(PdfValido);
        var id = (await servicio.SubirAsync(Ramon, "ticket.pdf", contenido, PdfValido.Length, CancellationToken.None)).Valor!.Id;

        // Se fijan los importes directamente, como si los hubiera propuesto la extracción.
        var extraccion = await context.ExtraccionesIa.SingleAsync(e => e.DocumentoId == id);
        extraccion.EmisorNombre = "Comercio SL";
        extraccion.EmisorNif = "X8019576M";
        extraccion.NumeroFactura = "A19";
        extraccion.FechaEmision = new DateOnly(2026, 1, 19);
        extraccion.BaseImponible = (decimal)baseImponible;
        extraccion.TipoIva = (decimal?)tipoIva;
        extraccion.CuotaIva = (decimal?)cuotaIva;
        extraccion.Total = (decimal)total;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var resultado = await servicio.AprobarAsync(Pedro, id, CancellationToken.None);
        context.ChangeTracker.Clear();
        var estado = (await context.Documentos.AsNoTracking().SingleAsync(d => d.Id == id)).Estado;

        if (errorEsperado is null)
        {
            Assert.True(resultado.EsCorrecto, resultado.Error?.Mensaje);
            Assert.Equal(EstadoDocumento.VALIDADO, estado);
        }
        else
        {
            Assert.Equal(TipoError.VALIDACION, resultado.Error!.Tipo);
            Assert.Contains($"{errorEsperado} (", resultado.Error.Mensaje);
            Assert.Equal(EstadoDocumento.PENDIENTE_VALIDACION, estado);
        }
    }

    // PA 75-1 (UT07): una corrección que deja los importes incoherentes no se guarda.
    [Fact]
    public async Task Corregir_ConImportesIncoherentes_NoGuardaCambios()
    {
        await using var context = CrearContexto(nameof(Corregir_ConImportesIncoherentes_NoGuardaCambios));
        await DbInitializer.SeedAsync(context);
        var servicio = new DocumentosService(context, new AlmacenamientoEnMemoria(),
            Options.Create(new DocumentosOptions { TamanoMaximoBytes = 1024 }), TimeProvider.System,
            NullLogger<DocumentosService>.Instance, new LectorParaPruebaDeFlujo(), new AccesoDocumentos(context));
        using var contenido = new MemoryStream(PdfValido);
        var id = (await servicio.SubirAsync(Ramon, "ticket.pdf", contenido, PdfValido.Length, CancellationToken.None)).Valor!.Id;
        context.ChangeTracker.Clear();

        var correccion = await servicio.CorregirAsync(Pedro, id,
        [
            new CampoValidacionRequest("EmisorNif", "X8019576M"),
            new CampoValidacionRequest("NumeroFactura", "A19"),
            new CampoValidacionRequest("FechaEmision", "2026-01-19"),
            new CampoValidacionRequest("BaseImponible", "10,00"),
            new CampoValidacionRequest("CuotaIva", "2,10")
        ], CancellationToken.None);

        Assert.Equal(TipoError.VALIDACION, correccion.Error!.Tipo);
        Assert.Contains("Total (", correccion.Error.Mensaje);

        context.ChangeTracker.Clear();
        var guardada = await context.ExtraccionesIa.AsNoTracking().SingleAsync(e => e.DocumentoId == id);
        Assert.Null(guardada.BaseImponible);
        Assert.Null(guardada.CuotaIva);
        Assert.Equal(12.34m, guardada.Total);
        Assert.Empty(await context.HistorialValidacion.Where(h => h.DocumentoId == id).ToListAsync());
    }

    // PA 77-1 (UT07): intentar aprobar un documento en estados que no son PENDIENTE_VALIDACION
    // se rechaza con EstadoNoPermitidoParaValidacion y el estado del documento no cambia.
    [Theory]
    [InlineData(EstadoDocumento.PROCESANDO)]
    [InlineData(EstadoDocumento.ERROR)]
    [InlineData(EstadoDocumento.SUBIDO)]
    public async Task Aprobar_EstadoNoValido_SeRechazaYNoCambiaEstado(EstadoDocumento estado)
    {
        await using var context = CrearContexto($"{nameof(Aprobar_EstadoNoValido_SeRechazaYNoCambiaEstado)}-{estado}");
        await DbInitializer.SeedAsync(context);
        var servicio = new DocumentosService(context, new AlmacenamientoEnMemoria(),
            Options.Create(new DocumentosOptions { TamanoMaximoBytes = 1024 }), TimeProvider.System,
            NullLogger<DocumentosService>.Instance, new LectorParaPruebaDeFlujo(), new AccesoDocumentos(context));
        using var contenido = new MemoryStream(PdfValido);
        var id = (await servicio.SubirAsync(Ramon, "ticket.pdf", contenido, PdfValido.Length, CancellationToken.None)).Valor!.Id;

        var documento = await context.Documentos.SingleAsync(d => d.Id == id);
        documento.Estado = estado;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var resultado = await servicio.AprobarAsync(Pedro, id, CancellationToken.None);
        Assert.Equal(DocumentoErrores.EstadoNoPermitidoParaValidacion, resultado.Error);

        var sinCambios = await context.Documentos.AsNoTracking().SingleAsync(d => d.Id == id);
        Assert.Equal(estado, sinCambios.Estado);
    }

    // PA 77-1 (UT07, caso VALIDADO): un documento ya validado no puede aprobarse de nuevo.
    [Fact]
    public async Task Aprobar_DocumentoYaValidado_SeRechaza()
    {
        await using var context = CrearContexto(nameof(Aprobar_DocumentoYaValidado_SeRechaza));
        await DbInitializer.SeedAsync(context);
        var servicio = new DocumentosService(context, new AlmacenamientoEnMemoria(),
            Options.Create(new DocumentosOptions { TamanoMaximoBytes = 1024 }), TimeProvider.System,
            NullLogger<DocumentosService>.Instance, new LectorParaPruebaDeFlujo(), new AccesoDocumentos(context));
        using var contenido = new MemoryStream(PdfValido);
        var id = (await servicio.SubirAsync(Ramon, "ticket.pdf", contenido, PdfValido.Length, CancellationToken.None)).Valor!.Id;

        var correccion = await servicio.CorregirAsync(Pedro, id,
        [
            new CampoValidacionRequest("EmisorNombre", "Comercio SL"),
            new CampoValidacionRequest("EmisorNif", "X8019576M"),
            new CampoValidacionRequest("NumeroFactura", "A19"),
            new CampoValidacionRequest("FechaEmision", "2026-01-19")
        ], CancellationToken.None);
        Assert.True(correccion.EsCorrecto, correccion.Error?.Mensaje);
        context.ChangeTracker.Clear();

        var primera = await servicio.AprobarAsync(Pedro, id, CancellationToken.None);
        Assert.True(primera.EsCorrecto, primera.Error?.Mensaje);
        Assert.Equal(EstadoDocumento.VALIDADO, primera.Valor!.Estado);
        context.ChangeTracker.Clear();

        var segunda = await servicio.AprobarAsync(Pedro, id, CancellationToken.None);
        Assert.Equal(DocumentoErrores.DocumentoYaValidado, segunda.Error);
    }

    // PA 76-1 (UT07): un gestor sin vinculación activa con el autónomo no puede
    // consultar ni aprobar sus documentos. Las comprobaciones viven en la API,
    // independientemente de la interfaz.
    [Fact]
    public async Task GestorSinVinculacionActiva_NoPuedeConsultarNiAprobar()
    {
        await using var context = CrearContexto(nameof(GestorSinVinculacionActiva_NoPuedeConsultarNiAprobar));
        await DbInitializer.SeedAsync(context);
        var servicio = new DocumentosService(context, new AlmacenamientoEnMemoria(),
            Options.Create(new DocumentosOptions { TamanoMaximoBytes = 1024 }), TimeProvider.System,
            NullLogger<DocumentosService>.Instance, new LectorParaPruebaDeFlujo(), new AccesoDocumentos(context));
        using var contenido = new MemoryStream(PdfValido);
        var id = (await servicio.SubirAsync(Ramon, "ticket.pdf", contenido, PdfValido.Length, CancellationToken.None)).Valor!.Id;
        context.ChangeTracker.Clear();

        // Se finaliza la vinculación: el acceso debe retirarse de inmediato.
        var vinculo = await context.Vinculaciones.FirstAsync(v => v.AutonomoId == Ramon.Id);
        vinculo.Estado = EstadoVinculacion.FINALIZADA;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.Null(await servicio.ObtenerAsync(Pedro, id, CancellationToken.None));

        var aprobacion = await servicio.AprobarAsync(Pedro, id, CancellationToken.None);
        Assert.False(aprobacion.EsCorrecto);
        Assert.Equal(TipoError.NO_ENCONTRADO, aprobacion.Error!.Tipo);

        var sinCambios = await context.Documentos.AsNoTracking().SingleAsync(d => d.Id == id);
        Assert.Equal(EstadoDocumento.PENDIENTE_VALIDACION, sinCambios.Estado);
    }

    [Fact]
    public async Task Historial_SinPermisoSeRespondeComoNoEncontrado_YSinSesionNoAutenticado()
    {
        await using var context = CrearContexto(nameof(Historial_SinPermisoSeRespondeComoNoEncontrado_YSinSesionNoAutenticado));
        await DbInitializer.SeedAsync(context);
        var documentoId = await context.Documentos.Where(d => d.UsuarioId == Ramon.Id).Select(d => d.Id).FirstAsync();

        Task<Resultado<Pagina<EventoHistorial>>> Consultar(UsuarioActual? usuario) =>
            new HistorialDocumentoService(context, new AccesoDocumentos(context), new UsuarioActualFijo(usuario!))
                .ObtenerAsync(documentoId, 1, 20, CancellationToken.None);

        Assert.True((await Consultar(Pedro)).EsCorrecto);

        var otroAutonomo = await Consultar(new UsuarioActual(Guid.NewGuid(), Rol.AUTONOMO));
        Assert.Equal(DocumentoErrores.NoEncontrado, otroAutonomo.Error);
        Assert.Null(otroAutonomo.Valor);

        var vinculo = await context.Vinculaciones.FirstAsync(v => v.AutonomoId == Ramon.Id);
        vinculo.Estado = EstadoVinculacion.FINALIZADA;
        await context.SaveChangesAsync();
        var gestorSinVinculacionActiva = await Consultar(Pedro);
        Assert.Equal(DocumentoErrores.NoEncontrado, gestorSinVinculacionActiva.Error);
        Assert.Null(gestorSinVinculacionActiva.Valor);

        Assert.Equal(DocumentoErrores.NoAutenticado, (await Consultar(null)).Error);
    }

    // PA 99-1 / 101-1 (UT09): el gestor asigna una categoría a un documento sin clasificar y después
    // la cambia. Cada cambio registra actor, fecha y categorías anterior y nueva, y aparece en el
    // historial del documento (PB-08).
    [Fact]
    public async Task CambiarCategoria_AsignaYCambia_RegistraElCambioEnElHistorial()
    {
        await using var context = CrearContexto(nameof(CambiarCategoria_AsignaYCambia_RegistraElCambioEnElHistorial));
        await DbInitializer.SeedAsync(context);
        var servicio = CrearServicio(context, new AlmacenamientoEnMemoria());
        var id = await FacturaConPropuestaIngreso(context);
        Assert.Equal(CategoriaDocumento.SIN_CLASIFICAR, (await context.Documentos.AsNoTracking().SingleAsync(d => d.Id == id)).Categoria);

        var asignacion = await servicio.CambiarCategoriaAsync(Pedro, id, "INGRESO", CancellationToken.None);
        Assert.True(asignacion.EsCorrecto, asignacion.Error?.Mensaje);
        Assert.Equal(CategoriaDocumento.INGRESO, asignacion.Valor!.Categoria);
        context.ChangeTracker.Clear();

        var cambio = await servicio.CambiarCategoriaAsync(Pedro, id, " gasto ", CancellationToken.None);
        Assert.True(cambio.EsCorrecto, cambio.Error?.Mensaje);
        Assert.Equal(CategoriaDocumento.GASTO, cambio.Valor!.Categoria);
        context.ChangeTracker.Clear();

        var registro = await context.HistorialCategorias.AsNoTracking().Where(h => h.DocumentoId == id).ToListAsync();
        Assert.Equal(2, registro.Count);
        var ultimo = Assert.Single(registro, h => h.CategoriaAnterior == CategoriaDocumento.INGRESO);
        Assert.Equal(CategoriaDocumento.GASTO, ultimo.CategoriaNueva);
        Assert.Equal(DbInitializer.PedroId, ultimo.UsuarioId);
        Assert.NotEqual(default, ultimo.FechaCambio);

        var historial = new HistorialDocumentoService(context, new AccesoDocumentos(context), new UsuarioActualFijo(Ramon));
        var eventos = (await historial.ObtenerAsync(id, 1, 20, CancellationToken.None)).Valor!.Elementos;
        Assert.Equal(2, eventos.Count(evento => evento.Tipo == TipoEventoDocumento.CATEGORIZACION_ACTUALIZADA));
        var evento = Assert.Single(eventos, evento => evento.Cambio?.ValorAnterior == "INGRESO");
        Assert.Equal(TipoEventoDocumento.CATEGORIZACION_ACTUALIZADA, evento.Tipo);
        Assert.Equal("Categoria", evento.Cambio!.Campo);
        Assert.Equal("GASTO", evento.Cambio.ValorNuevo);
        Assert.Equal(DbInitializer.PedroId, evento.Actor!.Id);
    }

    // PA 105-1 (UT09): elegir una categoría distinta de la propuesta no sobrescribe la propuesta
    // ni su confianza, y la corrección de campos (UT07) tampoco puede cambiarla.
    [Fact]
    public async Task CambiarCategoria_DistintaDeLaPropuesta_ConservaPropuestaYConfianza()
    {
        await using var context = CrearContexto(nameof(CambiarCategoria_DistintaDeLaPropuesta_ConservaPropuestaYConfianza));
        await DbInitializer.SeedAsync(context);
        var id = await FacturaConPropuestaIngreso(context);

        var respuesta = await CrearControlador(context, Pedro)
            .CambiarCategoria(id, new CambiarCategoriaRequest("GASTO"), CancellationToken.None);
        var documento = Assert.IsType<DocumentoResponse>(Assert.IsType<OkObjectResult>(respuesta.Result).Value);
        Assert.Equal("GASTO", documento.Categoria);
        Assert.Equal("INGRESO", documento.CategoriaPropuesta);
        Assert.Equal("ALTO", documento.NivelConfianzaPropuesta);
        context.ChangeTracker.Clear();

        // La corrección solo se admite en PENDIENTE_VALIDACION; se fuerza para comprobar el campo.
        var pendiente = await context.Documentos.SingleAsync(d => d.Id == id);
        pendiente.Estado = EstadoDocumento.PENDIENTE_VALIDACION;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var correccion = await CrearServicio(context, new AlmacenamientoEnMemoria())
            .CorregirAsync(Pedro, id, [new CampoValidacionRequest("CategoriaSugerida", "GASTO")], CancellationToken.None);
        Assert.Equal(TipoError.VALIDACION, correccion.Error!.Tipo);
        context.ChangeTracker.Clear();

        var extraccion = await context.ExtraccionesIa.AsNoTracking().SingleAsync(e => e.DocumentoId == id);
        Assert.Equal("INGRESO", extraccion.CategoriaSugerida);
        Assert.Equal(NivelConfianza.ALTO, extraccion.NivelConfianza);
    }

    // PA 109-1 (UT09): solo se admiten INGRESO, GASTO y OTRO. Cualquier otro valor, incluido
    // SIN_CLASIFICAR o un número, se rechaza y la categoría vigente no cambia.
    [Theory]
    [InlineData("SALARIO")]
    [InlineData("SIN_CLASIFICAR")]
    [InlineData("99")]
    [InlineData("0")]
    [InlineData("")]
    [InlineData(null)]
    public async Task CambiarCategoria_FueraDelCatalogo_SeRechazaYNoCambia(string? categoria)
    {
        await using var context = CrearContexto($"{nameof(CambiarCategoria_FueraDelCatalogo_SeRechazaYNoCambia)}-{categoria}");
        await DbInitializer.SeedAsync(context);
        var servicio = CrearServicio(context, new AlmacenamientoEnMemoria());
        var id = await FacturaConPropuestaIngreso(context);
        Assert.True((await servicio.CambiarCategoriaAsync(Pedro, id, "INGRESO", CancellationToken.None)).EsCorrecto);
        context.ChangeTracker.Clear();

        var resultado = await servicio.CambiarCategoriaAsync(Pedro, id, categoria, CancellationToken.None);
        Assert.Equal(TipoError.VALIDACION, resultado.Error!.Tipo);
        context.ChangeTracker.Clear();

        Assert.Equal(CategoriaDocumento.INGRESO, (await context.Documentos.AsNoTracking().SingleAsync(d => d.Id == id)).Categoria);
        Assert.Single(await context.HistorialCategorias.Where(h => h.DocumentoId == id).ToListAsync());
    }

    // PA 110-1 (UT09): guardar la categoría vigente responde con el documento y no registra
    // ningún cambio ficticio.
    [Fact]
    public async Task CambiarCategoria_MismaCategoria_NoRegistraCambio()
    {
        await using var context = CrearContexto(nameof(CambiarCategoria_MismaCategoria_NoRegistraCambio));
        await DbInitializer.SeedAsync(context);
        var id = await FacturaConPropuestaIngreso(context);
        Assert.True((await CrearServicio(context, new AlmacenamientoEnMemoria())
            .CambiarCategoriaAsync(Pedro, id, "GASTO", CancellationToken.None)).EsCorrecto);
        context.ChangeTracker.Clear();

        var respuesta = await CrearControlador(context, Pedro)
            .CambiarCategoria(id, new CambiarCategoriaRequest("GASTO"), CancellationToken.None);
        var documento = Assert.IsType<DocumentoResponse>(Assert.IsType<OkObjectResult>(respuesta.Result).Value);
        Assert.Equal("GASTO", documento.Categoria);
        context.ChangeTracker.Clear();

        Assert.Single(await context.HistorialCategorias.Where(h => h.DocumentoId == id).ToListAsync());
        Assert.Single(await context.EventosDocumento
            .Where(e => e.DocumentoId == id && e.Tipo == TipoEventoDocumento.CATEGORIZACION_ACTUALIZADA)
            .ToListAsync());
    }

    // PA 111-2 (UT09): ni el autónomo ni un gestor sin vinculación activa pueden cambiar la
    // categoría. La categoría vigente no cambia y no se registra ningún evento.
    [Fact]
    public async Task CambiarCategoria_SinPermiso_SeDeniegaYNoRegistraNada()
    {
        await using var context = CrearContexto(nameof(CambiarCategoria_SinPermiso_SeDeniegaYNoRegistraNada));
        await DbInitializer.SeedAsync(context);
        var servicio = CrearServicio(context, new AlmacenamientoEnMemoria());
        var id = await FacturaConPropuestaIngreso(context);
        Assert.True((await servicio.CambiarCategoriaAsync(Pedro, id, "INGRESO", CancellationToken.None)).EsCorrecto);
        context.ChangeTracker.Clear();

        var autonomo = await servicio.CambiarCategoriaAsync(Ramon, id, "GASTO", CancellationToken.None);
        Assert.Equal(TipoError.PROHIBIDO, autonomo.Error!.Tipo);

        var vinculo = await context.Vinculaciones.FirstAsync(v => v.AutonomoId == Ramon.Id);
        vinculo.Estado = EstadoVinculacion.FINALIZADA;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // Sin vinculación activa se responde como si no existiera, igual que en la PA 76-1.
        var gestor = await servicio.CambiarCategoriaAsync(Pedro, id, "GASTO", CancellationToken.None);
        Assert.Equal(TipoError.NO_ENCONTRADO, gestor.Error!.Tipo);
        context.ChangeTracker.Clear();

        Assert.Equal(CategoriaDocumento.INGRESO, (await context.Documentos.AsNoTracking().SingleAsync(d => d.Id == id)).Categoria);
        Assert.Single(await context.HistorialCategorias.Where(h => h.DocumentoId == id).ToListAsync());
        Assert.Single(await context.EventosDocumento
            .Where(e => e.DocumentoId == id && e.Tipo == TipoEventoDocumento.CATEGORIZACION_ACTUALIZADA)
            .ToListAsync());
    }

    // Factura de luz del seed: de Ramón, sin clasificar y con propuesta automática INGRESO (ALTO).
    private static Task<Guid> FacturaConPropuestaIngreso(ApplicationDbContext context) =>
        context.ExtraccionesIa.Where(e => e.CategoriaSugerida == "INGRESO").Select(e => e.DocumentoId).SingleAsync();

    private static ApplicationDbContext CrearContexto(string nombre) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(nombre).Options);

    private static DocumentosService CrearServicio(ApplicationDbContext context, IFileStorageService storage, long tamanoMaximoBytes = 1024) =>
        new(
            context,
            storage,
            Options.Create(new DocumentosOptions { TamanoMaximoBytes = tamanoMaximoBytes }),
            TimeProvider.System,
            NullLogger<DocumentosService>.Instance,
            new LectorDocumentoLocal(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ocr:Tesseract"] = "gestio-test-motor-no-instalado"
            }).Build()),
            new AccesoDocumentos(context));

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
file sealed class LectorParaPruebaDeFlujo(string? texto = null) : ILectorDocumento
{
    public Task<TextoDocumento> LeerAsync(Stream original, string tipoMime, CancellationToken cancellationToken) =>
        Task.FromResult(new TextoDocumento(texto ?? "Emisor: Comercio de prueba\nTotal: 12,34", "doble-unitario"));
}

// Devuelve un reconocimiento con posiciones y puntuaciones, como el motor OCR.
file sealed class LectorConLecturas(TextoDocumento reconocido) : ILectorDocumento
{
    public Task<TextoDocumento> LeerAsync(Stream original, string tipoMime, CancellationToken cancellationToken) =>
        Task.FromResult(reconocido);
}

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
