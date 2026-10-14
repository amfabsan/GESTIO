using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Gestio.Api.Services;

public sealed class VinculacionesService(ApplicationDbContext context, TimeProvider timeProvider)
{
    public async Task<Resultado<Vinculacion>> SolicitarAsync(
        UsuarioActual usuario,
        string codigoAsesoria,
        CancellationToken cancellationToken)
    {
        if (usuario.Rol != Rol.AUTONOMO) return VinculacionErrores.SoloAutonomoPuedeSolicitar;

        var codigo = NormalizarCodigo(codigoAsesoria);
        if (!await ExisteAsesoriaAsync(codigo, cancellationToken))
        {
            return VinculacionErrores.CodigoAsesoriaNoEncontrado;
        }

        var duplicada = await context.Vinculaciones.AnyAsync(vinculacion =>
            vinculacion.AutonomoId == usuario.Id &&
            vinculacion.CodigoAsesoria == codigo &&
            vinculacion.Estado == EstadoVinculacion.PENDIENTE,
            cancellationToken);
        if (duplicada) return VinculacionErrores.SolicitudDuplicada;

        var vinculacion = new Vinculacion
        {
            Id = Guid.NewGuid(),
            AutonomoId = usuario.Id,
            CodigoAsesoria = codigo,
            Estado = EstadoVinculacion.PENDIENTE,
            FechaSolicitud = timeProvider.GetUtcNow()
        };

        context.Vinculaciones.Add(vinculacion);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return VinculacionErrores.SolicitudDuplicada;
        }

        return vinculacion;
    }

    public async Task<Resultado<Vinculacion>> AceptarAsync(
        UsuarioActual usuario,
        Guid vinculacionId,
        CancellationToken cancellationToken)
    {
        var vinculacion = await context.Vinculaciones
            .FirstOrDefaultAsync(vinculacion => vinculacion.Id == vinculacionId, cancellationToken);
        if (vinculacion is null) return VinculacionErrores.VinculacionNoEncontrada;

        if (!await EsAdministradorDeAsync(usuario, vinculacion.CodigoAsesoria, cancellationToken))
        {
            return VinculacionErrores.SoloAdministradorPuedeGestionar;
        }

        if (vinculacion.Estado != EstadoVinculacion.PENDIENTE)
        {
            return VinculacionErrores.EstadoNoValido;
        }

        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var tieneOtraActiva = await context.Vinculaciones.AnyAsync(otra =>
            otra.AutonomoId == vinculacion.AutonomoId &&
            otra.Id != vinculacion.Id &&
            otra.Estado == EstadoVinculacion.ACTIVA,
            cancellationToken);
        if (tieneOtraActiva) return VinculacionErrores.AutonomoYaTieneAsesoria;

        vinculacion.Estado = EstadoVinculacion.ACTIVA;
        vinculacion.FechaAceptacion = timeProvider.GetUtcNow();
        vinculacion.AceptadaPorId = usuario.Id;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return VinculacionErrores.AutonomoYaTieneAsesoria;
        }

        return vinculacion;
    }

    public async Task<Resultado<Vinculacion>> FinalizarAsync(
        UsuarioActual usuario,
        Guid vinculacionId,
        CancellationToken cancellationToken)
    {
        var vinculacion = await context.Vinculaciones
            .FirstOrDefaultAsync(vinculacion => vinculacion.Id == vinculacionId, cancellationToken);
        if (vinculacion is null) return VinculacionErrores.VinculacionNoEncontrada;

        if (!await EsAdministradorDeAsync(usuario, vinculacion.CodigoAsesoria, cancellationToken))
        {
            return VinculacionErrores.SoloAdministradorPuedeGestionar;
        }

        if (vinculacion.Estado != EstadoVinculacion.ACTIVA)
        {
            return VinculacionErrores.EstadoNoValido;
        }

        vinculacion.Estado = EstadoVinculacion.FINALIZADA;
        vinculacion.FechaFinalizacion = timeProvider.GetUtcNow();
        vinculacion.FinalizadaPorId = usuario.Id;
        await context.SaveChangesAsync(cancellationToken);

        return vinculacion;
    }

    public Task<IReadOnlyList<Vinculacion>> ListarAsync(
        UsuarioActual usuario,
        CancellationToken cancellationToken)
    {
        var consulta = context.Vinculaciones
            .AsNoTracking()
            .Include(vinculacion => vinculacion.Autonomo)
            .AsQueryable();

        consulta = usuario.Rol == Rol.AUTONOMO
            ? consulta.Where(vinculacion => vinculacion.AutonomoId == usuario.Id)
            : consulta.Where(vinculacion => context.Usuarios.Any(administrador =>
                administrador.Id == usuario.Id &&
                administrador.Rol == Rol.GESTOR &&
                administrador.CodigoAsesoria == vinculacion.CodigoAsesoria));

        return consulta
            .OrderByDescending(vinculacion => vinculacion.FechaSolicitud)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Vinculacion>)t.Result, cancellationToken);
    }

    private Task<bool> ExisteAsesoriaAsync(string codigo, CancellationToken cancellationToken) =>
        context.Usuarios.AnyAsync(usuario =>
            usuario.Rol == Rol.GESTOR && usuario.CodigoAsesoria == codigo,
            cancellationToken);

    private Task<bool> EsAdministradorDeAsync(
        UsuarioActual usuario,
        string codigo,
        CancellationToken cancellationToken) =>
        usuario.Rol == Rol.GESTOR
            ? context.Usuarios.AnyAsync(administrador =>
                administrador.Id == usuario.Id &&
                administrador.CodigoAsesoria == codigo,
                cancellationToken)
            : Task.FromResult(false);

    private static string NormalizarCodigo(string codigo) => codigo.Trim().ToUpperInvariant();
}
