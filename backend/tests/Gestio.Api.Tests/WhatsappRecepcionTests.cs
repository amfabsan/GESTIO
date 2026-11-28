using Gestio.Api.Common;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Services;
using Gestio.Api.Services.Whatsapp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gestio.Api.Tests;

/// <summary>UT27, capa 3: recepción de documentos por WhatsApp (PA 3600-3604, 3606, 3607).</summary>
public sealed class WhatsappRecepcionTests
{
    private const string MovilRamon = "34600111222";
    private const string MovilDesconocido = "34699000111";
    private const string Factura = "Servicios Delta S.L.\nFactura Nº A-901 Fecha: 03/02/2025\nBASE %IVA IMP.IVA\n80,00 10,00 8,00\nTOTAL EUROS: 88,00";

    private static readonly byte[] Jpg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
    private static readonly byte[] Pdf = "%PDF-1.7\nFactura de prueba"u8.ToArray();
    private static readonly byte[] Docx = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00];
    private static readonly UsuarioActual Ramon = new(DbInitializer.RamonId, Rol.AUTONOMO);
    private static readonly UsuarioActual Pedro = new(DbInitializer.PedroId, Rol.GESTOR);

    // ─── PA 3600 · Foto de un ticket ─────────────────────────────────────────────

    [Fact]
    public async Task PA3600_FotoJpgDesdeNumeroVerificado_CreaDocumentoSuyoConOrigenWhatsappYConfirma()
    {
        var e = await Entorno.CrearAsync(nameof(PA3600_FotoJpgDesdeNumeroVerificado_CreaDocumentoSuyoConOrigenWhatsappYConfirma));
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg"));

        var documento = await e.Context.Documentos.AsNoTracking().SingleAsync(d => d.Origen == OrigenDocumento.WHATSAPP);
        Assert.Equal(Ramon.Id, documento.UsuarioId);
        Assert.Equal("wamid.1", documento.ReferenciaMensaje);
        Assert.Equal("image/jpeg", documento.TipoMime);
        Assert.EndsWith(".jpg", documento.NombreArchivo);
        Assert.Equal(e.Reloj.Ahora, documento.FechaSubida);

        var enviado = Assert.Single(e.Proveedor.Enviados);
        Assert.Equal(MovilRamon, enviado.Telefono);
        Assert.Contains("Hemos recibido", enviado.Texto);
        Assert.Contains("pendiente de procesamiento y revisión", enviado.Texto);
        Assert.DoesNotContain("validad", enviado.Texto, StringComparison.OrdinalIgnoreCase);

        var registro = await e.Context.MensajesWhatsapp.AsNoTracking().SingleAsync();
        Assert.Equal(ResultadoMensajeWhatsapp.DOCUMENTO_CREADO, registro.Resultado);
        Assert.Equal(documento.Id, registro.DocumentoId);
        Assert.Null(registro.MediaId);
        Assert.Null(registro.NombreArchivo);
    }

    // Requisito 6: la confirmación sale después de guardar y antes de la extracción.
    [Fact]
    public async Task PA3600_LaConfirmacionSeEnviaConElDocumentoGuardadoYAntesDeExtraer()
    {
        var e = await Entorno.CrearAsync(nameof(PA3600_LaConfirmacionSeEnviaConElDocumentoGuardadoYAntesDeExtraer));
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");
        EstadoDocumento? estadoAlConfirmar = null;
        e.Proveedor.AlEnviar = () =>
        {
            using var otro = new ApplicationDbContext(e.Opciones);
            estadoAlConfirmar = otro.Documentos.Single(d => d.Origen == OrigenDocumento.WHATSAPP).Estado;
        };

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg"));

        Assert.Equal(EstadoDocumento.SUBIDO, estadoAlConfirmar);
        Assert.Equal(EstadoDocumento.PENDIENTE_VALIDACION, (await e.Context.Documentos.AsNoTracking().SingleAsync(d => d.Origen == OrigenDocumento.WHATSAPP)).Estado);
    }

    // ─── PA 3601 · PDF con el mismo flujo que la web ─────────────────────────────

    [Fact]
    public async Task PA3601_PdfValido_SigueLosMismosEstadosYEventosQueUnaSubidaWeb()
    {
        var e = await Entorno.CrearAsync(nameof(PA3601_PdfValido_SigueLosMismosEstadosYEventosQueUnaSubidaWeb));
        e.Proveedor.Media["m1"] = (Pdf, "application/pdf");

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.DOCUMENTO, "m1", "application/pdf", "factura-luz.pdf"));
        using (var web = new MemoryStream(Pdf.Concat("web"u8.ToArray()).ToArray()))
            Assert.True((await e.Documentos.SubirAsync(Ramon, "factura-web.pdf", web, web.Length, CancellationToken.None)).EsCorrecto);

        var documentos = await e.Context.Documentos.AsNoTracking().Include(d => d.Eventos).ToListAsync();
        var porWhatsapp = documentos.Single(d => d.Origen == OrigenDocumento.WHATSAPP);
        var porWeb = documentos.Single(d => d.Origen == OrigenDocumento.SUBIDA_WEB && d.NombreArchivo == "factura-web.pdf");

        Assert.Equal("factura-luz.pdf", porWhatsapp.NombreArchivo);
        Assert.Equal(porWeb.Estado, porWhatsapp.Estado);
        Assert.Equal(EstadoDocumento.PENDIENTE_VALIDACION, porWhatsapp.Estado);
        Assert.Equal(Tipos(porWeb), Tipos(porWhatsapp));
        Assert.Contains(porWhatsapp.Eventos, ev => ev.Tipo == TipoEventoDocumento.SUBIDA && ev.Detalle!.Contains("por WhatsApp"));

        static TipoEventoDocumento[] Tipos(Documento d) => d.Eventos.OrderBy(ev => ev.Fecha).ThenBy(ev => ev.Tipo).Select(ev => ev.Tipo).ToArray();
    }

    [Fact]
    public async Task DocumentoSinExtension_RecibeLaDeSuTipo()
    {
        var e = await Entorno.CrearAsync(nameof(DocumentoSinExtension_RecibeLaDeSuTipo));
        e.Proveedor.Media["m1"] = (Pdf, "application/pdf");

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.DOCUMENTO, "m1", "application/pdf", "factura"));

        Assert.Equal("factura.pdf", (await e.Context.Documentos.AsNoTracking().SingleAsync(d => d.Origen == OrigenDocumento.WHATSAPP)).NombreArchivo);
    }

    // ─── PA 3602 · Formato no admitido o tamaño excesivo ─────────────────────────

    [Fact]
    public async Task PA3602_FormatoNoAdmitido_NoCreaDocumentoYExplicaElMotivo()
    {
        var e = await Entorno.CrearAsync(nameof(PA3602_FormatoNoAdmitido_NoCreaDocumentoYExplicaElMotivo));
        e.Proveedor.Media["m1"] = (Docx, "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.DOCUMENTO, "m1",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "presupuesto.docx"));

        Assert.False(await e.Context.Documentos.AnyAsync(d => d.Origen == OrigenDocumento.WHATSAPP));
        var enviado = Assert.Single(e.Proveedor.Enviados).Texto;
        Assert.Contains("presupuesto.docx", enviado);
        Assert.Contains("Solo se admiten archivos PDF, JPG, JPEG, PNG", enviado);
        var registro = await e.Context.MensajesWhatsapp.AsNoTracking().SingleAsync();
        Assert.Equal(ResultadoMensajeWhatsapp.RECHAZADO, registro.Resultado);
        Assert.Equal("documento.tipo_no_permitido", registro.Motivo);
    }

    // Cambiar la extensión no basta: la firma del contenido manda, igual que en la web.
    [Fact]
    public async Task PA3602_ContenidoQueNoCoincideConLaExtension_SeRechaza()
    {
        var e = await Entorno.CrearAsync(nameof(PA3602_ContenidoQueNoCoincideConLaExtension_SeRechaza));
        e.Proveedor.Media["m1"] = (Docx, "application/pdf");

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.DOCUMENTO, "m1", "application/pdf", "falso.pdf"));

        Assert.False(await e.Context.Documentos.AnyAsync(d => d.Origen == OrigenDocumento.WHATSAPP));
        Assert.Contains("Solo se admiten", Assert.Single(e.Proveedor.Enviados).Texto);
    }

    [Fact]
    public async Task PA3602_DemasiadoGrandeSegunLosMetadatos_NoSeDescargaNiCreaDocumento()
    {
        var e = await Entorno.CrearAsync(nameof(PA3602_DemasiadoGrandeSegunLosMetadatos_NoSeDescargaNiCreaDocumento), tamanoMaximo: 1024);
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");
        e.Proveedor.TamanoDeclarado = 5_000;

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg"));

        Assert.Equal(0, e.Proveedor.Descargas);
        Assert.False(await e.Context.Documentos.AnyAsync(d => d.Origen == OrigenDocumento.WHATSAPP));
        Assert.Contains("supera el tamaño máximo permitido", Assert.Single(e.Proveedor.Enviados).Texto);
        Assert.Equal("documento.tamano_excedido", (await e.Context.MensajesWhatsapp.AsNoTracking().SingleAsync()).Motivo);
    }

    // Si los metadatos no lo dicen (o mienten), la descarga se corta al pasar del máximo.
    [Fact]
    public async Task PA3602_DemasiadoGrandeAlDescargar_NoCreaDocumento()
    {
        var e = await Entorno.CrearAsync(nameof(PA3602_DemasiadoGrandeAlDescargar_NoCreaDocumento), tamanoMaximo: 10);
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");
        e.Proveedor.TamanoDeclarado = null;

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg"));

        Assert.False(await e.Context.Documentos.AnyAsync(d => d.Origen == OrigenDocumento.WHATSAPP));
        Assert.Contains("supera el tamaño máximo permitido", Assert.Single(e.Proveedor.Enviados).Texto);
    }

    [Fact]
    public async Task MensajeQueNoEsArchivoNiTexto_DeNumeroVinculado_RecibeElMotivo()
    {
        var e = await Entorno.CrearAsync(nameof(MensajeQueNoEsArchivoNiTexto_DeNumeroVinculado_RecibeElMotivo));

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.OTRO));

        Assert.Equal(TextosWhatsapp.TipoNoAdmitido, Assert.Single(e.Proveedor.Enviados).Texto);
        Assert.False(await e.Context.Documentos.AnyAsync(d => d.Origen == OrigenDocumento.WHATSAPP));
    }

    [Fact]
    public async Task TextoDeNumeroVinculado_RecibeLaAyuda()
    {
        var e = await Entorno.CrearAsync(nameof(TextoDeNumeroVinculado_RecibeLaAyuda));

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.TEXTO, Texto: "hola"));

        Assert.Equal(TextosWhatsapp.Ayuda, Assert.Single(e.Proveedor.Enviados).Texto);
        Assert.Equal(ResultadoMensajeWhatsapp.RESPONDIDO, (await e.Context.MensajesWhatsapp.AsNoTracking().SingleAsync()).Resultado);
    }

    // ─── PA 3603 · Entregas repetidas ────────────────────────────────────────────

    [Fact]
    public async Task PA3603_ElMismoEventoDosVeces_UnSoloDocumentoYUnaSolaConfirmacion()
    {
        var e = await Entorno.CrearAsync(nameof(PA3603_ElMismoEventoDosVeces_UnSoloDocumentoYUnaSolaConfirmacion));
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");
        var mensaje = new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg");

        await e.RecibirYProcesarAsync(mensaje);
        await e.RecibirYProcesarAsync(mensaje);

        Assert.Single(await e.Context.Documentos.Where(d => d.Origen == OrigenDocumento.WHATSAPP).ToListAsync());
        Assert.Single(e.Proveedor.Enviados);
    }

    // Segunda entrega mientras la primera aún está en la cola: tampoco se encola dos veces.
    [Fact]
    public async Task PA3603_SegundaEntregaConLaPrimeraEnCurso_NoSeEncolaOtraVez()
    {
        var e = await Entorno.CrearAsync(nameof(PA3603_SegundaEntregaConLaPrimeraEnCurso_NoSeEncolaOtraVez));
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");
        var mensaje = new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg");

        await e.Recepcion.RecibirAsync(mensaje, CancellationToken.None);
        await e.Recepcion.RecibirAsync(mensaje, CancellationToken.None);

        Assert.Equal(["wamid.1"], e.VaciarCola());
    }

    [Fact]
    public async Task PA3603_TextoRepetido_NoSeRespondeDosVeces()
    {
        var e = await Entorno.CrearAsync(nameof(PA3603_TextoRepetido_NoSeRespondeDosVeces));
        var mensaje = new MensajeEntrante("wamid.1", MovilDesconocido, TipoMensajeWhatsapp.TEXTO, Texto: "hola");

        await e.RecibirYProcesarAsync(mensaje);
        await e.RecibirYProcesarAsync(mensaje);

        Assert.Single(e.Proveedor.Enviados);
    }

    // D3/D4: un FALLIDO se reprocesa si el proveedor lo vuelve a entregar.
    [Fact]
    public async Task FalloTemporal_YNuevaEntregaDelMismoMensaje_SeReprocesaYConfirmaUnaVez()
    {
        var e = await Entorno.CrearAsync(nameof(FalloTemporal_YNuevaEntregaDelMismoMensaje_SeReprocesaYConfirmaUnaVez));
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");
        e.Proveedor.FallarDescarga = true;
        var mensaje = new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg");

        await e.RecibirYProcesarAsync(mensaje);
        Assert.Equal(ResultadoMensajeWhatsapp.FALLIDO, (await e.Context.MensajesWhatsapp.AsNoTracking().SingleAsync()).Resultado);

        e.Proveedor.FallarDescarga = false;
        await e.RecibirYProcesarAsync(mensaje);

        Assert.Single(await e.Context.Documentos.Where(d => d.Origen == OrigenDocumento.WHATSAPP).ToListAsync());
        Assert.Equal([TextosWhatsapp.ErrorTemporal], e.Proveedor.Enviados.Take(1).Select(m => m.Texto));
        Assert.Single(e.Proveedor.Enviados, m => m.Texto.StartsWith("Hemos recibido"));
        Assert.Equal(2, (await e.Context.MensajesWhatsapp.AsNoTracking().SingleAsync()).Intentos);
    }

    // D3: un EN_CURSO reciente no se toca; uno abandonado (más de 5 min) se reencola.
    [Fact]
    public async Task EnCursoAbandonado_SeReencolaConUnaNuevaEntrega()
    {
        var e = await Entorno.CrearAsync(nameof(EnCursoAbandonado_SeReencolaConUnaNuevaEntrega));
        var mensaje = new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg");
        await e.Recepcion.RecibirAsync(mensaje, CancellationToken.None);
        e.VaciarCola();

        e.Reloj.Avanzar(TimeSpan.FromMinutes(4));
        await e.Recepcion.RecibirAsync(mensaje, CancellationToken.None);
        Assert.Empty(e.VaciarCola());

        e.Reloj.Avanzar(TimeSpan.FromMinutes(2));
        await e.Recepcion.RecibirAsync(mensaje, CancellationToken.None);
        Assert.Equal(["wamid.1"], e.VaciarCola());
    }

    // Si un intento anterior llegó a crear el documento, el reproceso no crea otro ni vuelve a confirmar.
    [Fact]
    public async Task Reproceso_ConElDocumentoYaCreado_NoDuplicaNiConfirma()
    {
        var e = await Entorno.CrearAsync(nameof(Reproceso_ConElDocumentoYaCreado_NoDuplicaNiConfirma));
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");
        await e.Recepcion.RecibirAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg"), CancellationToken.None);
        using (var mismo = new MemoryStream(Jpg))
            await e.Documentos.SubirAsync(Ramon, "x.jpg", mismo, mismo.Length, CancellationToken.None, OrigenDocumento.WHATSAPP, "wamid.1", procesar: false);
        e.Context.ChangeTracker.Clear();

        await e.ProcesarColaAsync();

        var documento = Assert.Single(await e.Context.Documentos.Where(d => d.ReferenciaMensaje == "wamid.1").ToListAsync());
        Assert.Equal(EstadoDocumento.PENDIENTE_VALIDACION, documento.Estado);
        Assert.Empty(e.Proveedor.Enviados);
        Assert.Equal(ResultadoMensajeWhatsapp.DOCUMENTO_CREADO, (await e.Context.MensajesWhatsapp.AsNoTracking().SingleAsync()).Resultado);
    }

    // ─── PA 3604 · Fallo de almacenamiento ───────────────────────────────────────

    [Fact]
    public async Task PA3604_FallaElAlmacenamiento_NoHayDocumentoNiConfirmacionDeRecepcion()
    {
        var e = await Entorno.CrearAsync(nameof(PA3604_FallaElAlmacenamiento_NoHayDocumentoNiConfirmacionDeRecepcion), almacenamientoFalla: true);
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg"));

        Assert.False(await e.Context.Documentos.AnyAsync(d => d.Origen == OrigenDocumento.WHATSAPP));
        var enviado = Assert.Single(e.Proveedor.Enviados).Texto;
        Assert.Equal(TextosWhatsapp.ErrorTemporal, enviado);
        Assert.DoesNotContain("Hemos recibido", enviado);
        var registro = await e.Context.MensajesWhatsapp.AsNoTracking().SingleAsync();
        Assert.Equal(ResultadoMensajeWhatsapp.FALLIDO, registro.Resultado);
        Assert.Equal("almacenamiento_fallido", registro.Motivo);
        Assert.Contains(e.Logs, l => l.Nivel == LogLevel.Error && l.Texto.Contains("almacenar"));
    }

    // El otro camino de fallo de guardado: la base de datos (SubirAsync lanza en lugar de devolver un error).
    [Fact]
    public async Task PA3604_FallaLaBaseDeDatosAlGuardar_NoHayConfirmacionDeRecepcion()
    {
        var e = await Entorno.CrearAsync(nameof(PA3604_FallaLaBaseDeDatosAlGuardar_NoHayConfirmacionDeRecepcion));
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");
        await e.Recepcion.RecibirAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg"), CancellationToken.None);
        e.Interceptor.FallarAlGuardarDocumentos = true;

        await e.ProcesarColaAsync();

        e.Interceptor.FallarAlGuardarDocumentos = false;
        Assert.False(await e.Context.Documentos.AnyAsync(d => d.Origen == OrigenDocumento.WHATSAPP));
        Assert.Empty(e.Almacenamiento.Archivos);
        Assert.Equal(TextosWhatsapp.ErrorTemporal, Assert.Single(e.Proveedor.Enviados).Texto);
        Assert.Equal("guardado_fallido", (await e.Context.MensajesWhatsapp.AsNoTracking().SingleAsync()).Motivo);
    }

    [Fact]
    public async Task FallaLaDescarga_SeRegistraYSeRespondeConError()
    {
        var e = await Entorno.CrearAsync(nameof(FallaLaDescarga_SeRegistraYSeRespondeConError));
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");
        e.Proveedor.FallarDescarga = true;

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg"));

        Assert.Equal(TextosWhatsapp.ErrorTemporal, Assert.Single(e.Proveedor.Enviados).Texto);
        Assert.Equal("descarga_fallida", (await e.Context.MensajesWhatsapp.AsNoTracking().SingleAsync()).Motivo);
        Assert.Contains(e.Logs, l => l.Nivel == LogLevel.Error);
    }

    // Un fallo al enviar la confirmación no deshace el documento.
    [Fact]
    public async Task FallaElEnvioDeLaConfirmacion_ElDocumentoSeConserva()
    {
        var e = await Entorno.CrearAsync(nameof(FallaElEnvioDeLaConfirmacion_ElDocumentoSeConserva));
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");
        e.Proveedor.FallarEnvio = true;

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg"));

        Assert.Single(await e.Context.Documentos.Where(d => d.Origen == OrigenDocumento.WHATSAPP).ToListAsync());
        Assert.Equal(ResultadoMensajeWhatsapp.DOCUMENTO_CREADO, (await e.Context.MensajesWhatsapp.AsNoTracking().SingleAsync()).Resultado);
    }

    // D3: un EN_CURSO que murió antes de guardar su archivo no bloquea el mensaje para siempre.
    [Fact]
    public async Task EnCursoAbandonadoSinArchivo_UnaNuevaEntregaLoTrataComoNuevo()
    {
        var e = await Entorno.CrearAsync(nameof(EnCursoAbandonadoSinArchivo_UnaNuevaEntregaLoTrataComoNuevo));
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");
        e.Context.MensajesWhatsapp.Add(new MensajeWhatsappRecibido
        {
            MensajeId = "wamid.1", Tipo = TipoMensajeWhatsapp.IMAGEN, Resultado = ResultadoMensajeWhatsapp.EN_CURSO,
            FechaRecepcion = e.Reloj.Ahora.AddMinutes(-10), FechaActualizacion = e.Reloj.Ahora.AddMinutes(-10)
        });
        await e.Context.SaveChangesAsync();
        e.Context.ChangeTracker.Clear();

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg"));

        Assert.Single(await e.Context.Documentos.Where(d => d.ReferenciaMensaje == "wamid.1").ToListAsync());
        Assert.Single(e.Proveedor.Enviados, m => m.Texto.StartsWith("Hemos recibido"));
    }

    [Fact]
    public async Task EnCursoRecienteSinArchivo_NoSeTocaNiSeResponde()
    {
        var e = await Entorno.CrearAsync(nameof(EnCursoRecienteSinArchivo_NoSeTocaNiSeResponde));
        e.Context.MensajesWhatsapp.Add(new MensajeWhatsappRecibido
        {
            MensajeId = "wamid.1", Tipo = TipoMensajeWhatsapp.TEXTO, Resultado = ResultadoMensajeWhatsapp.EN_CURSO,
            FechaRecepcion = e.Reloj.Ahora.AddMinutes(-1), FechaActualizacion = e.Reloj.Ahora.AddMinutes(-1)
        });
        await e.Context.SaveChangesAsync();
        e.Context.ChangeTracker.Clear();

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilDesconocido, TipoMensajeWhatsapp.TEXTO, Texto: "hola"));

        Assert.Empty(e.Proveedor.Enviados);
    }

    // La reclamación solo calla el duplicado: otro error de base de datos se registra y sube al webhook.
    [Fact]
    public async Task ErrorDeBaseDeDatosAlReclamar_SeRegistraYSePropaga()
    {
        var e = await Entorno.CrearAsync(nameof(ErrorDeBaseDeDatosAlReclamar_SeRegistraYSePropaga));
        e.Interceptor.FallarAlGuardarMensajes = true;

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            e.Recepcion.RecibirAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.TEXTO, Texto: "hola"), CancellationToken.None));

        Assert.Contains(e.Logs, l => l.Nivel == LogLevel.Error);
        Assert.Empty(e.Proveedor.Enviados);
    }

    // Un mensaje que falla siempre de forma inesperada no se reintenta indefinidamente.
    [Fact]
    public async Task IntentosAgotados_SeAbandonaConErrorYSinDescargar()
    {
        var e = await Entorno.CrearAsync(nameof(IntentosAgotados_SeAbandonaConErrorYSinDescargar));
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");
        e.Context.MensajesWhatsapp.Add(new MensajeWhatsappRecibido
        {
            MensajeId = "wamid.1", UsuarioId = Ramon.Id, Tipo = TipoMensajeWhatsapp.IMAGEN, Resultado = ResultadoMensajeWhatsapp.EN_CURSO,
            MediaId = "m1", MimeType = "image/jpeg", NombreArchivo = "x.jpg", Intentos = RecepcionWhatsappService.MaximoIntentos,
            FechaRecepcion = e.Reloj.Ahora, FechaActualizacion = e.Reloj.Ahora
        });
        await e.Context.SaveChangesAsync();
        e.Context.ChangeTracker.Clear();

        await e.Recepcion.ProcesarArchivoAsync("wamid.1", CancellationToken.None);

        Assert.Equal(0, e.Proveedor.Descargas);
        Assert.Equal(TextosWhatsapp.ErrorTemporal, Assert.Single(e.Proveedor.Enviados).Texto);
        Assert.Equal("intentos_agotados", (await e.Context.MensajesWhatsapp.AsNoTracking().SingleAsync()).Motivo);
    }

    // Requisito 5: entre mensajes distintos rigen las reglas de PB-10 (aviso de duplicado, no bloqueo).
    [Fact]
    public async Task ArchivoIdenticoAUnoSubidoPorLaWeb_SeIncorporaConAvisoDeDuplicado()
    {
        var e = await Entorno.CrearAsync(nameof(ArchivoIdenticoAUnoSubidoPorLaWeb_SeIncorporaConAvisoDeDuplicado));
        e.Proveedor.Media["m1"] = (Pdf, "application/pdf");
        using (var web = new MemoryStream(Pdf))
            Assert.True((await e.Documentos.SubirAsync(Ramon, "original.pdf", web, web.Length, CancellationToken.None)).EsCorrecto);
        e.Context.ChangeTracker.Clear();

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.DOCUMENTO, "m1", "application/pdf", "copia.pdf"));

        var copia = await e.Context.Documentos.AsNoTracking().Include(d => d.Anomalias).SingleAsync(d => d.Origen == OrigenDocumento.WHATSAPP);
        Assert.Contains(copia.Anomalias, a => a.Tipo == TipoAnomalia.POSIBLE_DUPLICADO);
        Assert.Single(e.Proveedor.Enviados, m => m.Texto.StartsWith("Hemos recibido"));
    }

    // Recorrido completo contra PostgreSQL: claves, índices únicos y CHECK reales (InMemory no los aplica).
    [PostgreSqlFact]
    public Task EnPostgreSql_FotoRecibidaYEntregadaDosVeces_UnDocumentoUnaConfirmacion() => WhatsappPersistenciaTests.EnEsquemaAsync(async opciones =>
    {
        var autonomo = new UsuarioActual(WhatsappPersistenciaTests.AutonomoA, Rol.AUTONOMO);
        var proveedor = new ProveedorFalso();
        proveedor.Media["m1"] = (Jpg, "image/jpeg");
        await using (var db = new ApplicationDbContext(opciones))
        {
            db.NumerosWhatsapp.Add(new NumeroWhatsapp { Id = Guid.NewGuid(), UsuarioId = autonomo.Id, TelefonoVerificado = MovilRamon });
            await db.SaveChangesAsync();
        }

        // Id con la forma y longitud de un wamid real.
        var wamid = "wamid.HBgLMzQ2MDAxMTEyMjIVAgASGBQzQUIyRjc0QkI5QjE5RDQ1NUQ2NgA=";
        var mensaje = new MensajeEntrante(wamid, MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg");
        var cola = new ColaMensajesWhatsapp();
        for (var entrega = 0; entrega < 2; entrega++)
        {
            await using var db = new ApplicationDbContext(opciones);
            var recepcion = RecepcionReal(db, proveedor, cola);
            await recepcion.RecibirAsync(mensaje, CancellationToken.None);
            while (cola.TryLeer(out var id)) await recepcion.ProcesarArchivoAsync(id, CancellationToken.None);
        }

        await using var comprobacion = new ApplicationDbContext(opciones);
        var documento = await comprobacion.Documentos.SingleAsync();
        Assert.Equal(OrigenDocumento.WHATSAPP, documento.Origen);
        Assert.Equal(wamid, documento.ReferenciaMensaje);
        Assert.Equal(EstadoDocumento.PENDIENTE_VALIDACION, documento.Estado);
        Assert.Equal(ResultadoMensajeWhatsapp.DOCUMENTO_CREADO, (await comprobacion.MensajesWhatsapp.SingleAsync()).Resultado);
        Assert.Single(proveedor.Enviados);
    });

    private static RecepcionWhatsappService RecepcionReal(ApplicationDbContext db, IProveedorWhatsapp proveedor, ColaMensajesWhatsapp cola)
    {
        var documentosOptions = Options.Create(new DocumentosOptions());
        var documentos = new DocumentosService(db, new AlmacenamientoFalso(), documentosOptions, TimeProvider.System,
            NullLogger<DocumentosService>.Instance, new LectorConTexto(Factura), new AccesoDocumentos(db));
        var verificacion = new VerificacionNumeroWhatsappService(db, TimeProvider.System,
            Options.Create(new WhatsappOptions { Habilitado = true }), NullLogger<VerificacionNumeroWhatsappService>.Instance);
        return new RecepcionWhatsappService(db, documentos, verificacion, proveedor, cola, documentosOptions,
            TimeProvider.System, NullLogger<RecepcionWhatsappService>.Instance);
    }

    // ─── PA 3606 · Número no vinculado ───────────────────────────────────────────

    [Theory]
    [InlineData(TipoMensajeWhatsapp.IMAGEN, "m1", null)]
    [InlineData(TipoMensajeWhatsapp.DOCUMENTO, "m1", null)]
    [InlineData(TipoMensajeWhatsapp.TEXTO, null, "hola")]
    [InlineData(TipoMensajeWhatsapp.TEXTO, null, "GESTIO-ZZZZZZ")]
    [InlineData(TipoMensajeWhatsapp.OTRO, null, null)]
    public async Task PA3606_NumeroNoVinculado_NadieRecibeElArchivoYLaRespuestaEsSiempreLaMisma(TipoMensajeWhatsapp tipo, string? mediaId, string? texto)
    {
        var e = await Entorno.CrearAsync($"{nameof(PA3606_NumeroNoVinculado_NadieRecibeElArchivoYLaRespuestaEsSiempreLaMisma)}-{tipo}-{texto}");
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");
        var documentosAntes = await e.Context.Documentos.CountAsync();

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.x", MovilDesconocido, tipo, mediaId, "image/jpeg", Texto: texto));

        Assert.Equal(documentosAntes, await e.Context.Documentos.CountAsync());
        Assert.Equal(0, e.Proveedor.Descargas);
        Assert.Empty(e.Almacenamiento.Archivos);
        var enviado = Assert.Single(e.Proveedor.Enviados);
        Assert.Equal(MovilDesconocido, enviado.Telefono);
        Assert.Equal(TextosWhatsapp.NumeroNoVinculado, enviado.Texto);
        var registro = await e.Context.MensajesWhatsapp.AsNoTracking().SingleAsync();
        Assert.Equal(ResultadoMensajeWhatsapp.NO_VINCULADO, registro.Resultado);
        Assert.Null(registro.UsuarioId);
    }

    // Requisito 7: la respuesta no revela nada de las cuentas existentes, ni siquiera si el número está en
    // verificación en otra cuenta o si ya pertenece a una.
    [Fact]
    public async Task PA3606_LaRespuestaNoContieneDatosDeNingunaCuenta()
    {
        var e = await Entorno.CrearAsync(nameof(PA3606_LaRespuestaNoContieneDatosDeNingunaCuenta));
        var usuarios = await e.Context.Usuarios.AsNoTracking().ToListAsync();

        foreach (var dato in usuarios.SelectMany(u => new[] { u.Nombre, u.PrimerApellido, u.Email, u.Nif, u.NombreAsesoria })
                     .Where(d => !string.IsNullOrEmpty(d)))
            Assert.DoesNotContain(dato!, TextosWhatsapp.NumeroNoVinculado, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(MovilRamon[^3..], TextosWhatsapp.NumeroNoVinculado);
    }

    [Fact]
    public async Task NumeroDesvinculadoMientrasEsperabaEnLaCola_NoSeAsignaANadie()
    {
        var e = await Entorno.CrearAsync(nameof(NumeroDesvinculadoMientrasEsperabaEnLaCola_NoSeAsignaANadie));
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");
        await e.Recepcion.RecibirAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg"), CancellationToken.None);
        await e.Verificacion.DesvincularAsync(Ramon, CancellationToken.None);

        await e.ProcesarColaAsync();

        Assert.False(await e.Context.Documentos.AnyAsync(d => d.Origen == OrigenDocumento.WHATSAPP));
        Assert.Equal(0, e.Proveedor.Descargas);
        Assert.Equal(ResultadoMensajeWhatsapp.NO_VINCULADO, (await e.Context.MensajesWhatsapp.AsNoTracking().SingleAsync()).Resultado);
    }

    // ─── Verificación del número por el canal (une capas 2 y 3) ─────────────────

    [Fact]
    public async Task PA3605_CodigoEnviadoPorElCanal_VinculaElNumeroYLoConfirma()
    {
        var e = await Entorno.CrearAsync(nameof(PA3605_CodigoEnviadoPorElCanal_VinculaElNumeroYLoConfirma), ramonVinculado: false);
        var codigo = (await e.Verificacion.IniciarAsync(Ramon, MovilRamon, CancellationToken.None)).Valor!.CodigoVerificacion!;

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.c", MovilRamon, TipoMensajeWhatsapp.TEXTO, Texto: codigo));

        Assert.Equal(TextosWhatsapp.NumeroVerificado, Assert.Single(e.Proveedor.Enviados).Texto);
        Assert.Equal(MovilRamon, (await e.Context.NumerosWhatsapp.AsNoTracking().SingleAsync()).TelefonoVerificado);
        var registro = await e.Context.MensajesWhatsapp.AsNoTracking().SingleAsync();
        Assert.Equal(ResultadoMensajeWhatsapp.RESPONDIDO, registro.Resultado);
        Assert.Equal(Ramon.Id, registro.UsuarioId);
    }

    [Fact]
    public async Task PA3605_NumeroSinVerificarTodavia_NoPuedeEnviarDocumentos()
    {
        var e = await Entorno.CrearAsync(nameof(PA3605_NumeroSinVerificarTodavia_NoPuedeEnviarDocumentos), ramonVinculado: false);
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");
        await e.Verificacion.IniciarAsync(Ramon, MovilRamon, CancellationToken.None);

        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg"));

        Assert.False(await e.Context.Documentos.AnyAsync(d => d.Origen == OrigenDocumento.WHATSAPP));
        Assert.Equal(TextosWhatsapp.NumeroNoVinculado, Assert.Single(e.Proveedor.Enviados).Texto);
    }

    // ─── PA 3607 · Acceso del gestor ─────────────────────────────────────────────

    [Theory]
    [InlineData(EstadoVinculacion.ACTIVA, true)]
    [InlineData(EstadoVinculacion.PENDIENTE, false)]
    [InlineData(EstadoVinculacion.FINALIZADA, false)]
    public async Task PA3607_ElGestorSoloVeElDocumentoRecibidoSiLaVinculacionEstaActiva(EstadoVinculacion estado, bool loVe)
    {
        var e = await Entorno.CrearAsync($"{nameof(PA3607_ElGestorSoloVeElDocumentoRecibidoSiLaVinculacionEstaActiva)}-{estado}");
        e.Proveedor.Media["m1"] = (Jpg, "image/jpeg");
        await e.RecibirYProcesarAsync(new MensajeEntrante("wamid.1", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg"));
        var id = (await e.Context.Documentos.AsNoTracking().SingleAsync(d => d.Origen == OrigenDocumento.WHATSAPP)).Id;
        foreach (var vinculacion in await e.Context.Vinculaciones.Where(v => v.AutonomoId == Ramon.Id).ToListAsync())
            vinculacion.Estado = estado;
        await e.Context.SaveChangesAsync();
        e.Context.ChangeTracker.Clear();

        Assert.Equal(loVe, await e.Documentos.ObtenerAsync(Pedro, id, CancellationToken.None) is not null);
        Assert.Equal(loVe, (await e.Documentos.ListarAsync(Pedro, CancellationToken.None)).Any(d => d.Id == id));
        Assert.Equal(loVe, await e.Documentos.AbrirOriginalAsync(Pedro, id, CancellationToken.None) is not null);
    }

    // ─── Segundo plano (D1) ──────────────────────────────────────────────────────

    [Fact]
    public async Task D1_ElProcesadorEnSegundoPlano_ProcesaLoEncoladoYRecuperaLosPendientesAlArrancar()
    {
        var nombre = nameof(D1_ElProcesadorEnSegundoPlano_ProcesaLoEncoladoYRecuperaLosPendientesAlArrancar);
        var proveedor = new ProveedorFalso();
        proveedor.Media["m1"] = (Jpg, "image/jpeg");
        proveedor.Media["m2"] = (Pdf, "application/pdf");
        var almacenamiento = new AlmacenamientoFalso();
        var servicios = new ServiceCollection()
            .AddLogging()
            .AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(nombre))
            .AddSingleton<TimeProvider>(TimeProvider.System)
            .AddSingleton<IOptions<DocumentosOptions>>(Options.Create(new DocumentosOptions()))
            .AddSingleton<IOptions<WhatsappOptions>>(Options.Create(new WhatsappOptions { Habilitado = true }))
            .AddSingleton<IFileStorageService>(almacenamiento)
            .AddSingleton<ILectorDocumento>(new LectorConTexto(Factura))
            .AddSingleton<IProveedorWhatsapp>(proveedor)
            .AddSingleton<ColaMensajesWhatsapp>()
            .AddScoped<AccesoDocumentos>()
            .AddScoped<DocumentosService>()
            .AddScoped<VerificacionNumeroWhatsappService>()
            .AddScoped<RecepcionWhatsappService>()
            .AddSingleton<ProcesadorMensajesWhatsapp>()
            .BuildServiceProvider();

        await using (var scope = servicios.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await DbInitializer.SeedAsync(db);
            db.NumerosWhatsapp.Add(new NumeroWhatsapp { Id = Guid.NewGuid(), UsuarioId = Ramon.Id, TelefonoVerificado = MovilRamon });
            // Un archivo que quedó EN_CURSO antes de un reinicio.
            db.MensajesWhatsapp.Add(new MensajeWhatsappRecibido
            {
                MensajeId = "wamid.huerfano", UsuarioId = Ramon.Id, Tipo = TipoMensajeWhatsapp.DOCUMENTO,
                Resultado = ResultadoMensajeWhatsapp.EN_CURSO, MediaId = "m2", MimeType = "application/pdf",
                NombreArchivo = "huerfano.pdf", FechaRecepcion = DateTimeOffset.UtcNow, FechaActualizacion = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<RecepcionWhatsappService>()
                .RecibirAsync(new MensajeEntrante("wamid.nuevo", MovilRamon, TipoMensajeWhatsapp.IMAGEN, "m1", "image/jpeg"), CancellationToken.None);
        }

        var procesador = servicios.GetRequiredService<ProcesadorMensajesWhatsapp>();
        using var parar = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await procesador.StartAsync(parar.Token);
        try
        {
            while (!parar.IsCancellationRequested)
            {
                await using var scope = servicios.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (await db.MensajesWhatsapp.CountAsync(m => m.Resultado == ResultadoMensajeWhatsapp.DOCUMENTO_CREADO) == 2) break;
                await Task.Delay(50, parar.Token);
            }
        }
        finally
        {
            await procesador.StopAsync(CancellationToken.None);
        }

        await using var final = servicios.CreateAsyncScope();
        var documentos = await final.ServiceProvider.GetRequiredService<ApplicationDbContext>().Documentos
            .Where(d => d.Origen == OrigenDocumento.WHATSAPP).Select(d => d.ReferenciaMensaje).ToListAsync();
        Assert.Equal(["wamid.huerfano", "wamid.nuevo"], documentos.Order());
        Assert.Equal(2, proveedor.Enviados.Count(m => m.Texto.StartsWith("Hemos recibido")));
    }

    // ─── Entorno de prueba ───────────────────────────────────────────────────────

    internal sealed class Entorno
    {
        public required ApplicationDbContext Context { get; init; }
        public required DbContextOptions<ApplicationDbContext> Opciones { get; init; }
        public required RelojFijo Reloj { get; init; }
        public required ProveedorFalso Proveedor { get; init; }
        public required AlmacenamientoFalso Almacenamiento { get; init; }
        public required InterceptorFallos Interceptor { get; init; }
        public required ColaMensajesWhatsapp Cola { get; init; }
        public required DocumentosService Documentos { get; init; }
        public required VerificacionNumeroWhatsappService Verificacion { get; init; }
        public required RecepcionWhatsappService Recepcion { get; init; }
        public required List<(LogLevel Nivel, string Texto)> Logs { get; init; }

        public static async Task<Entorno> CrearAsync(string nombre, long tamanoMaximo = 10 * 1024 * 1024,
            bool almacenamientoFalla = false, bool ramonVinculado = true)
        {
            var interceptor = new InterceptorFallos();
            var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(nombre).AddInterceptors(interceptor).Options;
            var context = new ApplicationDbContext(opciones);
            await DbInitializer.SeedAsync(context);
            if (ramonVinculado)
                context.NumerosWhatsapp.Add(new NumeroWhatsapp { Id = Guid.NewGuid(), UsuarioId = Ramon.Id, TelefonoVerificado = MovilRamon });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var reloj = new RelojFijo(new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero));
            var proveedor = new ProveedorFalso();
            var almacenamiento = new AlmacenamientoFalso { Fallar = almacenamientoFalla };
            var cola = new ColaMensajesWhatsapp();
            var logs = new List<(LogLevel, string)>();
            var documentosOptions = Options.Create(new DocumentosOptions { TamanoMaximoBytes = tamanoMaximo });
            var documentos = new DocumentosService(context, almacenamiento, documentosOptions, reloj,
                NullLogger<DocumentosService>.Instance, new LectorConTexto(Factura), new AccesoDocumentos(context));
            var verificacion = new VerificacionNumeroWhatsappService(context, reloj,
                Options.Create(new WhatsappOptions { Habilitado = true, NumeroCanal = "+1 555 000 0000" }),
                NullLogger<VerificacionNumeroWhatsappService>.Instance);
            var recepcion = new RecepcionWhatsappService(context, documentos, verificacion, proveedor, cola, documentosOptions,
                reloj, new RegistroEnLista<RecepcionWhatsappService>(logs));

            return new Entorno
            {
                Context = context, Opciones = opciones, Reloj = reloj, Proveedor = proveedor, Almacenamiento = almacenamiento,
                Interceptor = interceptor, Cola = cola, Documentos = documentos, Verificacion = verificacion,
                Recepcion = recepcion, Logs = logs
            };
        }

        // Lo que hacen juntos el webhook (RecibirAsync) y el procesador en segundo plano (ProcesarArchivoAsync).
        public async Task RecibirYProcesarAsync(MensajeEntrante mensaje)
        {
            await Recepcion.RecibirAsync(mensaje, CancellationToken.None);
            await ProcesarColaAsync();
        }

        public async Task ProcesarColaAsync()
        {
            foreach (var id in VaciarCola())
                await Recepcion.ProcesarArchivoAsync(id, CancellationToken.None);
            Context.ChangeTracker.Clear();
        }

        public List<string> VaciarCola()
        {
            var ids = new List<string>();
            while (Cola.TryLeer(out var id)) ids.Add(id);
            return ids;
        }
    }
}

internal sealed class ProveedorFalso : IProveedorWhatsapp
{
    public Dictionary<string, (byte[] Bytes, string Mime)> Media { get; } = [];
    public List<(string Telefono, string Texto)> Enviados { get; } = [];
    public long? TamanoDeclarado { get; set; } = -1;
    public bool FallarDescarga { get; set; }
    public bool FallarEnvio { get; set; }
    public int Descargas { get; private set; }
    public Action? AlEnviar { get; set; }

    public Task<MediaWhatsapp> ObtenerMediaAsync(string mediaId, CancellationToken cancellationToken)
    {
        if (FallarDescarga) throw new HttpRequestException("Proveedor caído");
        var (bytes, mime) = Media[mediaId];
        return Task.FromResult(new MediaWhatsapp(mime, TamanoDeclarado == -1 ? bytes.Length : TamanoDeclarado));
    }

    public Task<DescargaWhatsapp> DescargarMediaAsync(string mediaId, long maximoBytes, CancellationToken cancellationToken)
    {
        Descargas++;
        if (FallarDescarga) throw new HttpRequestException("Proveedor caído");
        var bytes = Media[mediaId].Bytes;
        return Task.FromResult(bytes.Length > maximoBytes
            ? new DescargaWhatsapp(null, true)
            : new DescargaWhatsapp(new MemoryStream(bytes), false));
    }

    public Task EnviarTextoAsync(string telefono, string texto, CancellationToken cancellationToken)
    {
        if (FallarEnvio) throw new HttpRequestException("No se pudo enviar");
        AlEnviar?.Invoke();
        Enviados.Add((telefono, texto));
        return Task.CompletedTask;
    }
}

internal sealed class AlmacenamientoFalso : IFileStorageService
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

internal sealed class LectorConTexto(string texto) : ILectorDocumento
{
    public Task<TextoDocumento> LeerAsync(Stream original, string tipoMime, CancellationToken cancellationToken) =>
        Task.FromResult(new TextoDocumento(texto, "doble-unitario"));
}

// Simula un fallo de la base de datos al guardar un documento nuevo (InMemory no tiene restricciones que fallen).
internal sealed class InterceptorFallos : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
{
    public bool FallarAlGuardarDocumentos { get; set; }
    public bool FallarAlGuardarMensajes { get; set; }
    public string? FallarAlGuardarMensaje { get; set; }

    public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
        Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
        Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (FallarAlGuardarDocumentos &&
            eventData.Context!.ChangeTracker.Entries<Documento>().Any(e => e.State == EntityState.Added))
            throw new DbUpdateException("Fallo simulado de la base de datos");
        if (FallarAlGuardarMensajes &&
            eventData.Context!.ChangeTracker.Entries<MensajeWhatsappRecibido>().Any(e => e.State == EntityState.Added))
            throw new DbUpdateException("Fallo simulado de la base de datos");
        if (FallarAlGuardarMensaje is not null && eventData.Context!.ChangeTracker.Entries<MensajeWhatsappRecibido>()
                .Any(e => e.State == EntityState.Added && e.Entity.MensajeId == FallarAlGuardarMensaje))
            throw new DbUpdateException("Fallo simulado de la base de datos");
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

internal sealed class RegistroEnLista<T>(List<(LogLevel, string)> destino) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        destino.Add((logLevel, formatter(state, exception)));
}
