using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Gestio.Api.Services;

public sealed record GuardarPerfilFiscalRequest(string? RegimenIva, string? RegimenIrpf,
    bool? TrabajadoresConRetencion, bool? LocalAlquiladoConRetencion);

public sealed class PerfilesFiscalesService(ApplicationDbContext context, TimeProvider timeProvider, GeneradorObligaciones generador)
{
    public static readonly IReadOnlyDictionary<string, string> CatalogoIva = new Dictionary<string, string>
    {
        ["GENERAL"] = "Régimen general", ["SIMPLIFICADO"] = "Régimen simplificado",
        ["RECARGO_EQUIVALENCIA"] = "Recargo de equivalencia"
    }.AsReadOnly();
    public static readonly IReadOnlyDictionary<string, string> CatalogoIrpf = new Dictionary<string, string>
    {
        ["ESTIMACION_DIRECTA_SIMPLIFICADA"] = "Estimación directa simplificada",
        ["ESTIMACION_DIRECTA_NORMAL"] = "Estimación directa normal",
        ["ESTIMACION_OBJETIVA"] = "Estimación objetiva (módulos)"
    }.AsReadOnly();

    private static readonly Error Prohibido = new("PerfilFiscal.Prohibido", "No tienes permiso para acceder a este perfil fiscal.", TipoError.PROHIBIDO);
    private static readonly Error NoEncontrado = new("PerfilFiscal.AutonomoNoEncontrado", "No se ha encontrado el autónomo.", TipoError.NO_ENCONTRADO);

    public async Task<bool> PuedeAccederAsync(UsuarioActual usuario, Guid autonomoId, bool escritura, CancellationToken ct)
    {
        if (usuario.Rol == Rol.AUTONOMO) return !escritura && usuario.Id == autonomoId;
        return usuario.Rol == Rol.GESTOR && await context.Vinculaciones.AnyAsync(v =>
            v.AutonomoId == autonomoId && v.Estado == EstadoVinculacion.ACTIVA &&
            context.Usuarios.Any(u => u.Id == usuario.Id && u.Rol == Rol.GESTOR && u.CodigoAsesoria != null && u.CodigoAsesoria == v.CodigoAsesoria), ct);
    }

    public async Task<Resultado<string?>> ConsultarDireccionFiscalAsync(UsuarioActual usuario, Guid autonomoId, CancellationToken ct)
    {
        if (usuario.Rol != Rol.AUTONOMO || usuario.Id != autonomoId) return Prohibido;
        var autonomo = await context.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == autonomoId, ct);
        return autonomo is null
            ? NoEncontrado
            : Resultado<string?>.Correcto(autonomo.DireccionFiscal);
    }

    public async Task<Resultado<string>> GuardarDireccionFiscalAsync(UsuarioActual usuario, Guid autonomoId, string? direccionFiscal, CancellationToken ct)
    {
        if (usuario.Rol != Rol.AUTONOMO || usuario.Id != autonomoId) return Prohibido;
        var direccion = direccionFiscal?.Trim();
        if (string.IsNullOrWhiteSpace(direccion) || direccion.Length > 500)
            return new Error("PerfilFiscal.DireccionFiscalNoValida", "La dirección fiscal es obligatoria y no puede superar los 500 caracteres.", TipoError.VALIDACION);

        var autonomo = await context.Usuarios.SingleOrDefaultAsync(u => u.Id == autonomoId, ct);
        if (autonomo is null) return NoEncontrado;
        autonomo.DireccionFiscal = direccion;
        await context.SaveChangesAsync(ct);
        return direccion;
    }

    public async Task<Resultado<PerfilFiscal?>> ConsultarAsync(UsuarioActual usuario, Guid autonomoId, int ejercicio, CancellationToken ct)
    {
        if (!await PuedeAccederAsync(usuario, autonomoId, false, ct)) return Prohibido;
        return Resultado<PerfilFiscal?>.Correcto(await context.Set<PerfilFiscal>().AsNoTracking()
            .Include(p => p.Historial).ThenInclude(h => h.Usuario)
            .SingleOrDefaultAsync(p => p.AutonomoId == autonomoId && p.Ejercicio == ejercicio, ct));
    }

    public async Task<Resultado<PerfilFiscal>> GuardarAsync(UsuarioActual usuario, Guid autonomoId, int ejercicio, GuardarPerfilFiscalRequest request, CancellationToken ct)
    {
        if (!await PuedeAccederAsync(usuario, autonomoId, true, ct)) return Prohibido;
        var pendientes = new List<string>();
        if (string.IsNullOrWhiteSpace(request.RegimenIva)) pendientes.Add("Régimen de IVA");
        if (string.IsNullOrWhiteSpace(request.RegimenIrpf)) pendientes.Add("Régimen de IRPF");
        if (request.TrabajadoresConRetencion is null) pendientes.Add("Trabajadores con retención");
        if (request.LocalAlquiladoConRetencion is null) pendientes.Add("Local alquilado con retención");
        if (pendientes.Count > 0) return new Error("PerfilFiscal.DatosObligatorios", "Campos pendientes: " + string.Join(" · ", pendientes), TipoError.VALIDACION);
        if (!CatalogoIva.ContainsKey(request.RegimenIva!) || !CatalogoIrpf.ContainsKey(request.RegimenIrpf!))
            return new Error("PerfilFiscal.RegimenNoAdmitido", "Los regímenes de IVA e IRPF deben pertenecer al catálogo admitido.", TipoError.VALIDACION);

        if (ejercicio is < 1 or > 9998) return new Error("PerfilFiscal.EjercicioNoValido", "El ejercicio debe estar entre 1 y 9998.", TipoError.VALIDACION);
        await using var transaccion = await generador.IniciarAsync(autonomoId, ct);

        var perfil = await context.Set<PerfilFiscal>().Include(p => p.Historial).ThenInclude(h => h.Usuario)
            .SingleOrDefaultAsync(p => p.AutonomoId == autonomoId && p.Ejercicio == ejercicio, ct);
        var nuevo = perfil is null;
        perfil ??= new PerfilFiscal { Id = Guid.NewGuid(), AutonomoId = autonomoId, Ejercicio = ejercicio, RegimenIva = request.RegimenIva!, RegimenIrpf = request.RegimenIrpf! };
        var fecha = timeProvider.GetUtcNow();
        void Registrar(string campo, string? anterior, string valor)
        {
            if (nuevo || anterior != valor)
            {
                var evento = new HistorialPerfilFiscal
                {
                    Id = Guid.NewGuid(), PerfilFiscalId = perfil.Id, UsuarioId = usuario.Id, FechaCambio = fecha,
                    Campo = campo, ValorAnterior = nuevo ? null : anterior, ValorNuevo = valor
                };
                perfil.Historial.Add(evento);
                context.Add(evento);
            }
        }
        Registrar("RegimenIva", perfil.RegimenIva, request.RegimenIva!);
        Registrar("RegimenIrpf", perfil.RegimenIrpf, request.RegimenIrpf!);
        Registrar("TrabajadoresConRetencion", perfil.TrabajadoresConRetencion ? "Sí" : "No", request.TrabajadoresConRetencion!.Value ? "Sí" : "No");
        Registrar("LocalAlquiladoConRetencion", perfil.LocalAlquiladoConRetencion ? "Sí" : "No", request.LocalAlquiladoConRetencion!.Value ? "Sí" : "No");
        perfil.RegimenIva = request.RegimenIva!;
        perfil.RegimenIrpf = request.RegimenIrpf!;
        perfil.TrabajadoresConRetencion = request.TrabajadoresConRetencion.Value;
        perfil.LocalAlquiladoConRetencion = request.LocalAlquiladoConRetencion.Value;
        if (nuevo) context.Add(perfil);
        await generador.SincronizarAsync(perfil, usuario.Id, soloSiGenerado: true, ct);
        try
        {
            await context.SaveChangesAsync(ct);
            if (transaccion is not null) await transaccion.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return new Error("PerfilFiscal.Conflicto", "El perfil ha sido creado por otra petición. Recarga e inténtalo de nuevo.", TipoError.CONFLICTO);
        }
        return perfil;
    }
}
