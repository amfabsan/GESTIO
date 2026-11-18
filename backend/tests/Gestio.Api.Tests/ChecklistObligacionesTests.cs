using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Tests;

public sealed class ChecklistObligacionesTests
{
    private readonly Guid autonomoId = Guid.NewGuid();
    private readonly Guid otroAutonomoId = Guid.NewGuid();
    private readonly Guid gestorId = Guid.NewGuid();
    private readonly Guid gestorAjenoId = Guid.NewGuid();

    private async Task<(ApplicationDbContext Db, PerfilesFiscalesService Perfiles,
        ObligacionesFiscalesService Obligaciones, ChecklistObligacionesService Checklist)> CrearAsync()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Usuarios.AddRange(
            CrearUsuario(autonomoId, Rol.AUTONOMO, null),
            CrearUsuario(otroAutonomoId, Rol.AUTONOMO, null),
            CrearUsuario(gestorId, Rol.GESTOR, "ASE-000001"),
            CrearUsuario(gestorAjenoId, Rol.GESTOR, "ASE-000002"));
        db.Vinculaciones.Add(new Vinculacion
        {
            Id = Guid.NewGuid(), AutonomoId = autonomoId, CodigoAsesoria = "ASE-000001",
            Estado = EstadoVinculacion.ACTIVA
        });
        await db.SaveChangesAsync();

        var generador = new GeneradorObligaciones(db, TimeProvider.System);
        var perfiles = new PerfilesFiscalesService(db, TimeProvider.System, generador);
        var checklist = new ChecklistObligacionesService(db, perfiles, TimeProvider.System);
        return (db, perfiles, new ObligacionesFiscalesService(db, perfiles, generador, checklist), checklist);
    }

    [Fact]
    public async Task MarcarConfirmarYDevolverConservaHistorialYActualizaEstado()
    {
        var (db, perfiles, obligaciones, checklist) = await CrearAsync();
        await using (db)
        {
            var perfil = new GuardarPerfilFiscalRequest("GENERAL", "ESTIMACION_DIRECTA_SIMPLIFICADA", false, true);
            Assert.True((await perfiles.GuardarAsync(new(gestorId, Rol.GESTOR), autonomoId, 2026, perfil, default)).EsCorrecto);
            var calendario = (await obligaciones.GenerarAsync(new(autonomoId, Rol.AUTONOMO), autonomoId, 2026, default)).Valor!;
            var obligacion = calendario.Obligaciones.Single(o => o.Modelo == "115" && o.Periodo == "2T");
            var usuario = new UsuarioActual(autonomoId, Rol.AUTONOMO);
            var gestor = new UsuarioActual(gestorId, Rol.GESTOR);

            var inicial = (await checklist.ConsultarAsync(usuario, autonomoId, obligacion.Id, default)).Valor!;
            var requerido = inicial.Checklist.Single(e => e.Obligatorio);
            var opcional = inicial.Checklist.Single(e => !e.Obligatorio);
            Assert.Equal(EstadoObligacion.INCOMPLETA, inicial.Estado);

            var aportado = (await checklist.MarcarAportadoAsync(usuario, autonomoId, obligacion.Id, requerido.Id, true, default)).Valor!;
            var elementoAportado = aportado.Checklist.Single(e => e.Id == requerido.Id);
            Assert.Equal(EstadoElementoChecklist.APORTADO, elementoAportado.Estado);
            Assert.Equal(OrigenCambioChecklist.AUTONOMO, elementoAportado.OrigenUltimoCambio);
            Assert.Equal(autonomoId, elementoAportado.UsuarioUltimoCambioId);
            Assert.NotNull(elementoAportado.FechaUltimoCambio);
            Assert.Equal(EstadoObligacion.INCOMPLETA, aportado.Estado);

            db.ChangeTracker.Clear();
            await obligaciones.GenerarAsync(usuario, autonomoId, 2026, default);
            db.ChangeTracker.Clear();
            var conservado = (await checklist.ConsultarAsync(usuario, autonomoId, obligacion.Id, default)).Valor!;
            Assert.Equal(2, conservado.Checklist.Count);
            Assert.Equal(requerido.Id, conservado.Checklist.Single(e => e.CodigoPlantilla == requerido.CodigoPlantilla).Id);
            Assert.Equal(EstadoElementoChecklist.APORTADO, conservado.Checklist.Single(e => e.Id == requerido.Id).Estado);
            Assert.Single(conservado.Checklist.Single(e => e.Id == requerido.Id).Historial);

            var confirmado = (await checklist.RevisarAsync(gestor, autonomoId, obligacion.Id, requerido.Id, true, default)).Valor!;
            var elementoConfirmado = confirmado.Checklist.Single(e => e.Id == requerido.Id);
            Assert.Equal(EstadoElementoChecklist.CONFIRMADO, elementoConfirmado.Estado);
            Assert.Equal(OrigenCambioChecklist.GESTOR, elementoConfirmado.OrigenUltimoCambio);
            Assert.Equal("Pedro Sánchez", $"{elementoConfirmado.UsuarioUltimoCambio!.Nombre} {elementoConfirmado.UsuarioUltimoCambio.PrimerApellido}");
            Assert.Equal(EstadoElementoChecklist.PENDIENTE, confirmado.Checklist.Single(e => e.Id == opcional.Id).Estado);
            Assert.Equal(EstadoObligacion.LISTA, confirmado.Estado);
            Assert.NotEqual(EstadoObligacion.PRESENTADA, confirmado.Estado);

            var devuelto = (await checklist.RevisarAsync(gestor, autonomoId, obligacion.Id, requerido.Id, false, default)).Valor!;
            var elementoDevuelto = devuelto.Checklist.Single(e => e.Id == requerido.Id);
            Assert.Equal(EstadoElementoChecklist.PENDIENTE, elementoDevuelto.Estado);
            Assert.Equal(EstadoObligacion.INCOMPLETA, devuelto.Estado);
            Assert.Equal(3, elementoDevuelto.Historial.Count);
            Assert.Contains(elementoDevuelto.Historial, cambio =>
                cambio.EstadoAnterior == EstadoElementoChecklist.CONFIRMADO && cambio.EstadoNuevo == EstadoElementoChecklist.PENDIENTE);
            Assert.All(elementoDevuelto.Historial, cambio => Assert.NotNull(cambio.Usuario));
        }
    }

    [Fact]
    public async Task DeniegaChecklistAPropietarioAjenoYGestorSinVinculacionActiva()
    {
        var (db, perfiles, obligaciones, checklist) = await CrearAsync();
        await using (db)
        {
            await perfiles.GuardarAsync(new(gestorId, Rol.GESTOR), autonomoId, 2026,
                new GuardarPerfilFiscalRequest("GENERAL", "ESTIMACION_DIRECTA_SIMPLIFICADA", false, true), default);
            var calendario = (await obligaciones.GenerarAsync(new(autonomoId, Rol.AUTONOMO), autonomoId, 2026, default)).Valor!;
            var obligacion = calendario.Obligaciones.Single(o => o.Modelo == "115" && o.Periodo == "2T");
            var elemento = (await checklist.ConsultarAsync(new(autonomoId, Rol.AUTONOMO), autonomoId, obligacion.Id, default))
                .Valor!.Checklist.First();

            Assert.Equal("Checklist.Prohibido", (await checklist.ConsultarAsync(new(otroAutonomoId, Rol.AUTONOMO), autonomoId, obligacion.Id, default)).Error!.Codigo);
            Assert.Equal("Checklist.Prohibido", (await checklist.ConsultarAsync(new(gestorAjenoId, Rol.GESTOR), autonomoId, obligacion.Id, default)).Error!.Codigo);
            Assert.Equal("Checklist.Prohibido", (await checklist.RevisarAsync(new(gestorAjenoId, Rol.GESTOR), autonomoId, obligacion.Id, elemento.Id, true, default)).Error!.Codigo);

            (await db.Vinculaciones.SingleAsync(v => v.AutonomoId == autonomoId)).Estado = EstadoVinculacion.FINALIZADA;
            await db.SaveChangesAsync();
            Assert.Equal("Checklist.Prohibido", (await checklist.RevisarAsync(new(gestorId, Rol.GESTOR), autonomoId, obligacion.Id, elemento.Id, true, default)).Error!.Codigo);
        }
    }

    private static Usuario CrearUsuario(Guid id, Rol rol, string? asesoria) => new()
    {
        Id = id,
        Nombre = "Pedro",
        PrimerApellido = "Sánchez",
        SegundoApellido = "Prueba",
        Email = $"{id}@test",
        Nif = id.ToString()[..9],
        Rol = rol,
        CodigoAsesoria = asesoria
    };
}
