using System.Data;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Models.Response;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Services;

public sealed class ObligacionesFiscalesService(ApplicationDbContext context, PerfilesFiscalesService perfiles, GeneradorObligaciones generador)
{
    private static readonly Error Prohibido = new("ObligacionFiscal.Prohibido", "No tienes permiso para acceder a estas obligaciones fiscales.", TipoError.PROHIBIDO);
    private static readonly Error EjercicioNoValido = new("ObligacionFiscal.EjercicioNoValido", "El ejercicio debe estar entre 1 y 9998.", TipoError.VALIDACION);

    public async Task<Resultado<CalendarioFiscalResponse>> ConsultarAsync(UsuarioActual usuario, Guid autonomoId, int ejercicio, CancellationToken ct)
    {
        if (!await perfiles.PuedeAccederAsync(usuario, autonomoId, false, ct)) return Prohibido;
        if (ejercicio is < 1 or > 9998) return EjercicioNoValido;
        // Perfil y listado proceden de la misma instantánea aunque otro proceso regenere entre consultas.
        await using var lectura = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct) : null;
        var nombre = await context.Usuarios.AsNoTracking().Where(u => u.Id == autonomoId)
            .Select(u => u.Nombre + " " + u.PrimerApellido + " " + u.SegundoApellido).SingleAsync(ct);
        var perfil = await context.Set<PerfilFiscal>().AsNoTracking().SingleOrDefaultAsync(p => p.AutonomoId == autonomoId && p.Ejercicio == ejercicio, ct);
        var obligaciones = await context.Set<ObligacionFiscal>().AsNoTracking()
            .Where(o => o.AutonomoId == autonomoId && o.Ejercicio == ejercicio)
            .OrderBy(o => o.FechaPrevista).ThenBy(o => o.Modelo).ToListAsync(ct);
        return new CalendarioFiscalResponse(ejercicio, nombre.Trim(), perfil is not null,
            perfil is null ? null : new ResumenPerfilFiscalResponse(PerfilesFiscalesService.CatalogoIva[perfil.RegimenIva], PerfilesFiscalesService.CatalogoIrpf[perfil.RegimenIrpf]),
            CatalogoObligacionesDemo.Aviso, obligaciones.Select(ObligacionFiscalResponse.Desde).ToArray());
    }

    public async Task<Resultado<CalendarioFiscalResponse>> GenerarAsync(UsuarioActual usuario, Guid autonomoId, int ejercicio, CancellationToken ct)
    {
        if (!await perfiles.PuedeAccederAsync(usuario, autonomoId, false, ct)) return Prohibido;
        if (ejercicio is < 1 or > 9998) return EjercicioNoValido;
        await using var transaccion = await generador.IniciarAsync(autonomoId, ct);
        var perfil = await context.Set<PerfilFiscal>().SingleOrDefaultAsync(p => p.AutonomoId == autonomoId && p.Ejercicio == ejercicio, ct);
        if (perfil is null)
            return new Error("ObligacionFiscal.SinPerfil", $"No hay perfil fiscal configurado para {ejercicio}. Sin un perfil configurado no se generan obligaciones por suposición.", TipoError.VALIDACION);
        await generador.SincronizarAsync(perfil, usuario.Id, soloSiGenerado: false, ct);
        await context.SaveChangesAsync(ct);
        if (transaccion is not null) await transaccion.CommitAsync(ct);
        return await ConsultarAsync(usuario, autonomoId, ejercicio, ct);
    }

    public async Task<Resultado<ObligacionFiscalDetalleResponse>> DetalleAsync(UsuarioActual usuario, Guid autonomoId, Guid obligacionId, CancellationToken ct)
    {
        if (!await perfiles.PuedeAccederAsync(usuario, autonomoId, false, ct)) return Prohibido;
        var obligacion = await context.Set<ObligacionFiscal>().AsNoTracking().Include(o => o.Historial).ThenInclude(h => h.Usuario)
            .SingleOrDefaultAsync(o => o.Id == obligacionId && o.AutonomoId == autonomoId, ct);
        if (obligacion is null) return new Error("ObligacionFiscal.NoEncontrada", "No se ha encontrado esta obligación fiscal.", TipoError.NO_ENCONTRADO);
        return new ObligacionFiscalDetalleResponse(ObligacionFiscalResponse.Desde(obligacion), CatalogoObligacionesDemo.Aviso,
            obligacion.Historial.OrderByDescending(h => h.Fecha).ThenBy(h => h.Id).Select(h => new CambioObligacionResponse(h.Id, h.UsuarioId,
                $"{h.Usuario.Nombre} {h.Usuario.PrimerApellido} {h.Usuario.SegundoApellido}".Trim(), h.Fecha, h.Descripcion)).ToArray());
    }
}
