using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Services;

// Única definición de quién puede consultar la facturación de un autónomo (facturas y registros de facturación):
// el propio autónomo, o un gestor de una asesoría con vinculación ACTIVA. Solo el autónomo puede emitir.
public static class AccesoFacturacion
{
    public static bool EsPropietario(UsuarioActual usuario, Guid autonomoId) =>
        usuario.Rol == Rol.AUTONOMO && usuario.Id == autonomoId;

    public static async Task<bool> PuedeConsultarAsync(
        ApplicationDbContext context, UsuarioActual usuario, Guid autonomoId, CancellationToken ct)
    {
        if (usuario.Rol == Rol.AUTONOMO) return usuario.Id == autonomoId;
        if (usuario.Rol != Rol.GESTOR) return false;
        return await context.Vinculaciones.AnyAsync(v =>
            v.AutonomoId == autonomoId && v.Estado == EstadoVinculacion.ACTIVA &&
            context.Usuarios.Any(u => u.Id == usuario.Id && u.Rol == Rol.GESTOR && u.CodigoAsesoria != null && u.CodigoAsesoria == v.CodigoAsesoria), ct);
    }
}
