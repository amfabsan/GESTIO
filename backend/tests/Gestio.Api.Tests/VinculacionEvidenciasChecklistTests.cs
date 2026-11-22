using Gestio.Api.Common;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gestio.Api.Tests;

public sealed class VinculacionEvidenciasChecklistTests
{
    [Fact]
    public async Task ProponeUnaVezSoloDocumentoValidadoDelMismoAutonomoYPeriodo()
    {
        await using var fixture = await CrearAsync();
        var documentoId = await fixture.AgregarDocumentoAsync(fixture.AutonomoId, CategoriaDocumento.INGRESO,
            EstadoDocumento.VALIDADO, new DateOnly(2026, 2, 15));
        var documentoGastoId = await fixture.AgregarDocumentoAsync(fixture.AutonomoId, CategoriaDocumento.GASTO,
            EstadoDocumento.VALIDADO, new DateOnly(2026, 2, 20));

        Assert.Equal(1, await fixture.Servicio.ProponerParaDocumentoAsync(documentoId, fixture.GestorId, default));
        Assert.Equal(3, await fixture.Servicio.ProponerParaDocumentoAsync(documentoGastoId, fixture.GestorId, default));
        Assert.Equal(0, await fixture.Servicio.ProponerParaDocumentoAsync(documentoId, fixture.GestorId, default));

        var vinculos = await fixture.Db.VinculosEvidenciaChecklist.Include(v => v.Historial).ToListAsync();
        Assert.Equal(4, vinculos.Count);
        Assert.All(vinculos, v => Assert.Equal(EstadoVinculoEvidencia.PROPUESTA, v.Estado));
        Assert.All(vinculos, v => Assert.Single(v.Historial));
        var vinculo = vinculos.Single(v => v.DocumentoId == documentoId);
        Assert.Equal(EstadoVinculoEvidencia.PROPUESTA, vinculo.Estado);
        Assert.Equal(EstadoVinculoEvidencia.PROPUESTA, vinculo.Historial.Single().EstadoNuevo);
        var elemento = await fixture.Db.ElementosChecklistObligacion.SingleAsync(e => e.Id == vinculo.ElementoChecklistObligacionId);
        Assert.Equal("facturas_emitidas", elemento.CodigoPlantilla);
        Assert.Equal(EstadoElementoChecklist.PENDIENTE, elemento.Estado);
        Assert.Equal(EstadoObligacion.INCOMPLETA, (await fixture.Db.Set<ObligacionFiscal>().SingleAsync(o => o.Id == fixture.ObligacionId)).Estado);
    }

    [Fact]
    public async Task AprobarDocumentoProponeEvidenciaAutomaticamente()
    {
        await using var fixture = await CrearAsync();
        var documentoId = await fixture.AgregarDocumentoAsync(fixture.AutonomoId, CategoriaDocumento.INGRESO,
            EstadoDocumento.PENDIENTE_VALIDACION, new DateOnly(2026, 2, 15));

        var resultado = await fixture.CrearDocumentosService().AprobarAsync(
            new UsuarioActual(fixture.GestorId, Rol.GESTOR), documentoId, default);

        Assert.True(resultado.EsCorrecto, resultado.Error?.Mensaje);
        Assert.Equal(EstadoDocumento.VALIDADO, resultado.Valor!.Estado);
        Assert.Single(await fixture.Db.VinculosEvidenciaChecklist.ToListAsync());
    }

    [Fact]
    public async Task NoProponeDocumentoPendienteCategoriaPeriodoODuenoDistintos()
    {
        await using var fixture = await CrearAsync();
        var pendiente = await fixture.AgregarDocumentoAsync(fixture.AutonomoId, CategoriaDocumento.INGRESO,
            EstadoDocumento.PENDIENTE_VALIDACION, new DateOnly(2026, 2, 15));
        var categoriaDistinta = await fixture.AgregarDocumentoAsync(fixture.AutonomoId, CategoriaDocumento.OTRO,
            EstadoDocumento.VALIDADO, new DateOnly(2026, 2, 15));
        var periodoDistinto = await fixture.AgregarDocumentoAsync(fixture.AutonomoId, CategoriaDocumento.INGRESO,
            EstadoDocumento.VALIDADO, new DateOnly(2026, 4, 1));
        var otroPropietario = await fixture.AgregarDocumentoAsync(fixture.OtroAutonomoId, CategoriaDocumento.INGRESO,
            EstadoDocumento.VALIDADO, new DateOnly(2026, 2, 15));

        Assert.Equal(0, await fixture.Servicio.ProponerParaDocumentoAsync(pendiente, fixture.GestorId, default));
        Assert.Equal(0, await fixture.Servicio.ProponerParaDocumentoAsync(categoriaDistinta, fixture.GestorId, default));
        Assert.Equal(0, await fixture.Servicio.ProponerParaDocumentoAsync(periodoDistinto, fixture.GestorId, default));
        var reintento = await fixture.Servicio.BuscarParaObligacionAsync(
            new UsuarioActual(fixture.GestorId, Rol.GESTOR), fixture.AutonomoId, fixture.ObligacionId, default);

        Assert.True(reintento.EsCorrecto);
        Assert.Equal(0, reintento.Valor);
        Assert.Empty(await fixture.Db.VinculosEvidenciaChecklist.ToListAsync());
        Assert.NotEqual(fixture.AutonomoId, (await fixture.Db.Documentos.SingleAsync(d => d.Id == otroPropietario)).UsuarioId);
    }

    [Fact]
    public async Task ConfirmarYRetirarActualizaChecklistYConservaHistorial()
    {
        await using var fixture = await CrearAsync();
        var documentoId = await fixture.AgregarDocumentoAsync(fixture.AutonomoId, CategoriaDocumento.INGRESO,
            EstadoDocumento.VALIDADO, new DateOnly(2026, 2, 15));
        await fixture.Servicio.ProponerParaDocumentoAsync(documentoId, fixture.GestorId, default);
        var vinculoId = await fixture.Db.VinculosEvidenciaChecklist.Select(v => v.Id).SingleAsync();
        var gestor = new UsuarioActual(fixture.GestorId, Rol.GESTOR);

        var confirmada = await fixture.Servicio.CambiarEstadoAsync(gestor, fixture.AutonomoId, fixture.ObligacionId,
            vinculoId, EstadoVinculoEvidencia.CONFIRMADA, default);
        Assert.True(confirmada.EsCorrecto);
        var elementoConfirmado = confirmada.Valor!.Checklist.Single(e => e.CodigoPlantilla == "facturas_emitidas");
        Assert.Equal(EstadoElementoChecklist.CONFIRMADO, elementoConfirmado.Estado);
        Assert.Equal(OrigenCambioChecklist.VINCULACION_EVIDENCIA, elementoConfirmado.OrigenUltimoCambio);
        Assert.Equal(fixture.GestorId, (await fixture.Db.VinculosEvidenciaChecklist.SingleAsync()).ConfirmadaPorId);
        Assert.NotNull((await fixture.Db.VinculosEvidenciaChecklist.SingleAsync()).FechaConfirmacion);
        Assert.Equal(EstadoObligacion.INCOMPLETA, confirmada.Valor.Estado);

        var retirada = await fixture.Servicio.CambiarEstadoAsync(gestor, fixture.AutonomoId, fixture.ObligacionId,
            vinculoId, EstadoVinculoEvidencia.RETIRADA, default);
        Assert.True(retirada.EsCorrecto);
        var elementoRetirado = retirada.Valor!.Checklist.Single(e => e.CodigoPlantilla == "facturas_emitidas");
        Assert.Equal(EstadoElementoChecklist.PENDIENTE, elementoRetirado.Estado);
        Assert.Equal(EstadoVinculoEvidencia.RETIRADA,
            (await fixture.Db.VinculosEvidenciaChecklist.Include(v => v.Historial).SingleAsync()).Estado);
        Assert.Equal(3, (await fixture.Db.HistorialVinculosEvidencia.ToListAsync()).Count);
        Assert.Equal(EstadoObligacion.INCOMPLETA, retirada.Valor.Estado);
    }

    [Fact]
    public async Task RechazoSeConservaYNoSeVuelveAProponerEnUnReintento()
    {
        await using var fixture = await CrearAsync();
        var documentoId = await fixture.AgregarDocumentoAsync(fixture.AutonomoId, CategoriaDocumento.INGRESO,
            EstadoDocumento.VALIDADO, new DateOnly(2026, 2, 15));
        await fixture.Servicio.ProponerParaDocumentoAsync(documentoId, fixture.GestorId, default);
        var vinculoId = await fixture.Db.VinculosEvidenciaChecklist.Select(v => v.Id).SingleAsync();
        var gestor = new UsuarioActual(fixture.GestorId, Rol.GESTOR);
        var gestorAjeno = new UsuarioActual(fixture.GestorAjenoId, Rol.GESTOR);

        var prohibido = await fixture.Servicio.CambiarEstadoAsync(gestorAjeno, fixture.AutonomoId,
            fixture.ObligacionId, vinculoId, EstadoVinculoEvidencia.CONFIRMADA, default);
        Assert.Equal("ChecklistEvidencia.Prohibido", prohibido.Error!.Codigo);

        var rechazo = await fixture.Servicio.CambiarEstadoAsync(gestor, fixture.AutonomoId, fixture.ObligacionId,
            vinculoId, EstadoVinculoEvidencia.RECHAZADA, default);
        Assert.True(rechazo.EsCorrecto);
        var reintento = await fixture.Servicio.BuscarParaObligacionAsync(gestor, fixture.AutonomoId, fixture.ObligacionId, default);

        Assert.True(reintento.EsCorrecto);
        Assert.Equal(0, reintento.Valor);
        var vinculo = await fixture.Db.VinculosEvidenciaChecklist.Include(v => v.Historial).SingleAsync();
        Assert.Equal(EstadoVinculoEvidencia.RECHAZADA, vinculo.Estado);
        Assert.Equal(2, vinculo.Historial.Count);
        Assert.Equal(fixture.GestorId, vinculo.Historial.OrderBy(h => h.Fecha).Last().UsuarioId);
    }

    [Fact]
    public async Task ReintentoRetiraPropuestaQueYaNoCumpleLaCategoriaYConservaHistorial()
    {
        await using var fixture = await CrearAsync();
        var documentoId = await fixture.AgregarDocumentoAsync(fixture.AutonomoId, CategoriaDocumento.INGRESO,
            EstadoDocumento.VALIDADO, new DateOnly(2026, 2, 15));
        await fixture.Servicio.ProponerParaDocumentoAsync(documentoId, fixture.GestorId, default);
        var vinculoId = await fixture.Db.VinculosEvidenciaChecklist.Select(v => v.Id).SingleAsync();
        (await fixture.Db.Documentos.SingleAsync(d => d.Id == documentoId)).Categoria = CategoriaDocumento.OTRO;
        await fixture.Db.SaveChangesAsync();

        var reintento = await fixture.Servicio.BuscarParaObligacionAsync(
            new UsuarioActual(fixture.GestorId, Rol.GESTOR), fixture.AutonomoId, fixture.ObligacionId, default);

        Assert.True(reintento.EsCorrecto);
        var vinculo = await fixture.Db.VinculosEvidenciaChecklist.Include(v => v.Historial).SingleAsync(v => v.Id == vinculoId);
        Assert.Equal(EstadoVinculoEvidencia.RETIRADA, vinculo.Estado);
        Assert.Equal(2, vinculo.Historial.Count);
        Assert.Equal(EstadoVinculoEvidencia.RETIRADA, vinculo.Historial.OrderBy(h => h.Fecha).Last().EstadoNuevo);
    }

    private static async Task<Fixture> CrearAsync()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var autonomoId = Guid.NewGuid();
        var otroAutonomoId = Guid.NewGuid();
        var gestorId = Guid.NewGuid();
        var gestorAjenoId = Guid.NewGuid();
        db.Usuarios.AddRange(CrearUsuario(autonomoId, Rol.AUTONOMO, null),
            CrearUsuario(otroAutonomoId, Rol.AUTONOMO, null), CrearUsuario(gestorId, Rol.GESTOR, "ASE-000001"),
            CrearUsuario(gestorAjenoId, Rol.GESTOR, "ASE-000002"));
        db.Vinculaciones.Add(new Vinculacion
        {
            Id = Guid.NewGuid(), AutonomoId = autonomoId, CodigoAsesoria = "ASE-000001",
            Estado = EstadoVinculacion.ACTIVA, FechaSolicitud = DateTimeOffset.UtcNow
        });
        var obligacionId = Guid.NewGuid();
        db.Add(new ObligacionFiscal
        {
            Id = obligacionId, AutonomoId = autonomoId, Modelo = "303", Nombre = "IVA",
            Ejercicio = 2026, Periodo = "1T", FechaPrevista = new DateOnly(2026, 4, 25)
        });
        db.Add(new ObligacionFiscal
        {
            Id = Guid.NewGuid(), AutonomoId = otroAutonomoId, Modelo = "303", Nombre = "IVA",
            Ejercicio = 2026, Periodo = "1T", FechaPrevista = new DateOnly(2026, 4, 25)
        });
        await db.SaveChangesAsync();

        var generador = new GeneradorObligaciones(db, TimeProvider.System);
        var perfiles = new PerfilesFiscalesService(db, TimeProvider.System, generador);
        var checklists = new ChecklistObligacionesService(db, perfiles, TimeProvider.System);
        var servicio = new VinculacionEvidenciasChecklistService(db, checklists, perfiles, TimeProvider.System);
        return new Fixture(db, servicio, autonomoId, otroAutonomoId, gestorId, gestorAjenoId, obligacionId);
    }

    private static Usuario CrearUsuario(Guid id, Rol rol, string? asesoria) => new()
    {
        Id = id,
        Nombre = "Ada",
        PrimerApellido = "López",
        SegundoApellido = "Prueba",
        Email = $"{id}@test",
        Nif = id.ToString()[..9],
        Rol = rol,
        CodigoAsesoria = asesoria
    };

    private sealed class Fixture(
        ApplicationDbContext db, VinculacionEvidenciasChecklistService servicio,
        Guid autonomoId, Guid otroAutonomoId, Guid gestorId, Guid gestorAjenoId, Guid obligacionId) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public VinculacionEvidenciasChecklistService Servicio { get; } = servicio;
        public Guid AutonomoId { get; } = autonomoId;
        public Guid OtroAutonomoId { get; } = otroAutonomoId;
        public Guid GestorId { get; } = gestorId;
        public Guid GestorAjenoId { get; } = gestorAjenoId;
        public Guid ObligacionId { get; } = obligacionId;

        public async Task<Guid> AgregarDocumentoAsync(Guid propietarioId, CategoriaDocumento categoria,
            EstadoDocumento estado, DateOnly fechaEmision)
        {
            var documentoId = Guid.NewGuid();
            Db.Documentos.Add(new Documento
            {
                Id = documentoId,
                UsuarioId = propietarioId,
                Categoria = categoria,
                NombreArchivo = $"evidencia-{documentoId}.pdf",
                TipoMime = "application/pdf",
                TamanoBytes = 100,
                Origen = OrigenDocumento.SUBIDA_WEB,
                FechaSubida = DateTimeOffset.UtcNow,
                Estado = estado,
                ExtraccionIa = new ExtraccionIa
                {
                    Id = Guid.NewGuid(), DocumentoId = documentoId, FechaEmision = fechaEmision,
                    EmisorNombre = "Comercio de prueba", EmisorNif = "B12345674", NumeroFactura = "F-1",
                    BaseImponible = 100m, TipoIva = 21m, CuotaIva = 21m, Total = 121m
                }
            });
            await Db.SaveChangesAsync();
            return documentoId;
        }

        public DocumentosService CrearDocumentosService() => new(
            Db, null!, Options.Create(new DocumentosOptions { TamanoMaximoBytes = 1024 }), TimeProvider.System,
            NullLogger<DocumentosService>.Instance, null!, new AccesoDocumentos(Db), Servicio);

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
