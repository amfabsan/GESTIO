using System.Net.Mail;
using System.Text.RegularExpressions;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Gestio.Api.Services;

public sealed record GuardarContactoRequest(string? Clasificacion, string? Nombre, string? Nif, string? Email, string? Telefono, string? Direccion);

public sealed class ContactosService(ApplicationDbContext context)
{
    private static readonly HashSet<string> Clasificaciones = ["CLIENTE", "PROVEEDOR", "AMBOS"];
    private static readonly Regex TelefonoConSeparadores = new(@"^\+?[0-9\s().-]+$", RegexOptions.Compiled);
    private static readonly Regex TelefonoEspanolValido = new(@"^(?:\+34)?[6789][0-9]{8}$", RegexOptions.Compiled);
    private static readonly Regex DocumentoIdentidadValido = new("^[0-9]{8}[A-Z]$|^[XYZ][0-9]{7}[A-Z]$", RegexOptions.Compiled);
    private static readonly Regex NifEmpresaValido = new("^[ABCDEFGHJNPQRSUVW][0-9]{7}[0-9A-J]$", RegexOptions.Compiled);
    private const string LetrasControlDni = "TRWAGMYFPDXBNJZSQVHLCKE";
    private const string LetrasControlCif = "JABCDEFGHI";

    public async Task<bool> PuedeAccederAsync(UsuarioActual usuario, Guid autonomoId, CancellationToken ct)
    {
        if (usuario.Rol == Rol.AUTONOMO) return usuario.Id == autonomoId;
        return usuario.Rol == Rol.GESTOR && await context.Vinculaciones.AnyAsync(v =>
            v.AutonomoId == autonomoId && v.Estado == EstadoVinculacion.ACTIVA &&
            context.Usuarios.Any(u => u.Id == usuario.Id && u.Rol == Rol.GESTOR && u.CodigoAsesoria != null && u.CodigoAsesoria == v.CodigoAsesoria), ct);
    }

    public async Task<Resultado<IReadOnlyList<Contacto>>> ListarAsync(UsuarioActual usuario, Guid autonomoId, string? busqueda, string? tipo, string? estado, CancellationToken ct)
    {
        if (!await PuedeAccederAsync(usuario, autonomoId, ct)) return ContactoErrores.Prohibido;

        var consulta = context.Contactos.AsNoTracking().Where(c => c.AutonomoId == autonomoId);
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var termino = busqueda.Trim().ToLowerInvariant();
            consulta = consulta.Where(c => c.Nombre.ToLower().Contains(termino) || c.Nif.ToLower().Contains(NormalizarNif(termino)));
        }
        if (tipo?.ToUpperInvariant() is "CLIENTE" or "PROVEEDOR")
        {
            var clasificacion = tipo.ToUpperInvariant();
            consulta = consulta.Where(c => c.Clasificacion == clasificacion || c.Clasificacion == "AMBOS");
        }
        if (string.Equals(estado, "ACTIVO", StringComparison.OrdinalIgnoreCase)) consulta = consulta.Where(c => c.Activo);
        if (string.Equals(estado, "INACTIVO", StringComparison.OrdinalIgnoreCase)) consulta = consulta.Where(c => !c.Activo);

        return await consulta.OrderBy(c => c.Nombre).ToListAsync(ct);
    }

    public async Task<Resultado<Contacto>> CrearAsync(UsuarioActual usuario, Guid autonomoId, GuardarContactoRequest request, CancellationToken ct)
    {
        if (!await PuedeAccederAsync(usuario, autonomoId, ct)) return ContactoErrores.Prohibido;
        var validacion = Validar(request);
        if (validacion is not null) return validacion;

        var nif = NormalizarNif(request.Nif!);
        if (await context.Contactos.AnyAsync(c => c.AutonomoId == autonomoId && c.Nif == nif, ct)) return ContactoErrores.Duplicado;

        var contacto = new Contacto
        {
            Id = Guid.NewGuid(), AutonomoId = autonomoId,
            Clasificacion = request.Clasificacion!.Trim().ToUpperInvariant(),
            Nombre = request.Nombre!.Trim(), Nif = nif,
            Email = Opcional(request.Email), Telefono = Opcional(request.Telefono), Direccion = Opcional(request.Direccion)
        };
        context.Contactos.Add(contacto);
        var error = await GuardarAsync(ct);
        return error is null ? contacto : error;
    }

    public async Task<Resultado<Contacto>> EditarAsync(UsuarioActual usuario, Guid autonomoId, Guid id, GuardarContactoRequest request, CancellationToken ct)
    {
        if (!await PuedeAccederAsync(usuario, autonomoId, ct)) return ContactoErrores.Prohibido;
        var validacion = Validar(request);
        if (validacion is not null) return validacion;

        var contacto = await context.Contactos.SingleOrDefaultAsync(c => c.Id == id && c.AutonomoId == autonomoId, ct);
        if (contacto is null) return ContactoErrores.NoEncontrado;
        var nif = NormalizarNif(request.Nif!);
        if (await context.Contactos.AnyAsync(c => c.AutonomoId == autonomoId && c.Nif == nif && c.Id != id, ct)) return ContactoErrores.Duplicado;

        contacto.Clasificacion = request.Clasificacion!.Trim().ToUpperInvariant();
        contacto.Nombre = request.Nombre!.Trim();
        contacto.Nif = nif;
        contacto.Email = Opcional(request.Email);
        contacto.Telefono = Opcional(request.Telefono);
        contacto.Direccion = Opcional(request.Direccion);
        var error = await GuardarAsync(ct);
        return error is null ? contacto : error;
    }

    public async Task<Resultado<Contacto>> ConsultarAsync(UsuarioActual usuario, Guid autonomoId, Guid id, CancellationToken ct)
    {
        if (!await PuedeAccederAsync(usuario, autonomoId, ct)) return ContactoErrores.Prohibido;
        var contacto = await context.Contactos.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && c.AutonomoId == autonomoId, ct);
        return contacto is null ? ContactoErrores.NoEncontrado : contacto;
    }

    public async Task<Resultado<Contacto>> CambiarEstadoAsync(UsuarioActual usuario, Guid autonomoId, Guid id, bool activo, CancellationToken ct)
    {
        if (!await PuedeAccederAsync(usuario, autonomoId, ct)) return ContactoErrores.Prohibido;
        var contacto = await context.Contactos.SingleOrDefaultAsync(c => c.Id == id && c.AutonomoId == autonomoId, ct);
        if (contacto is null) return ContactoErrores.NoEncontrado;
        contacto.Activo = activo;
        await context.SaveChangesAsync(ct);
        return contacto;
    }

    public async Task<Resultado<bool>> EliminarAsync(UsuarioActual usuario, Guid autonomoId, Guid id, CancellationToken ct)
    {
        if (!await PuedeAccederAsync(usuario, autonomoId, ct)) return ContactoErrores.Prohibido;
        var contacto = await context.Contactos.SingleOrDefaultAsync(c => c.Id == id && c.AutonomoId == autonomoId, ct);
        if (contacto is null) return ContactoErrores.NoEncontrado;
        if (await context.Facturas.AnyAsync(f => f.AutonomoId == autonomoId && f.ContactoId == id, ct))
            return ContactoErrores.Referenciado;
        context.Contactos.Remove(contacto);
        await context.SaveChangesAsync(ct);
        return true;
    }

    public static string NormalizarNif(string nif) => string.Concat(nif.Where(c => !char.IsWhiteSpace(c) && c != '-')).ToUpperInvariant();

    private static bool EsIdentificadorFiscalValido(string nif)
    {
        if (DocumentoIdentidadValido.IsMatch(nif))
        {
            var numero = nif[0] switch
            {
                'X' => "0" + nif[1..8],
                'Y' => "1" + nif[1..8],
                'Z' => "2" + nif[1..8],
                _ => nif[..8]
            };
            return nif[^1] == LetrasControlDni[int.Parse(numero) % 23];
        }

        if (!NifEmpresaValido.IsMatch(nif)) return false;

        var suma = 0;
        for (var i = 0; i < 7; i++)
        {
            var digito = nif[i + 1] - '0';
            if (i % 2 == 0)
            {
                var doble = digito * 2;
                suma += doble / 10 + doble % 10;
            }
            else
            {
                suma += digito;
            }
        }

        var control = (10 - suma % 10) % 10;
        var controlNumerico = (char)('0' + control);
        var controlAlfabetico = LetrasControlCif[control];
        var prefijo = nif[0];
        if ("ABEH".Contains(prefijo)) return nif[^1] == controlNumerico;
        if ("PQS".Contains(prefijo)) return nif[^1] == controlAlfabetico;
        return nif[^1] == controlNumerico || nif[^1] == controlAlfabetico;
    }

    private static Error? Validar(GuardarContactoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre)) return new("Contacto.NombreObligatorio", "El nombre o razón social es obligatorio.", TipoError.VALIDACION);
        if (request.Nombre.Trim().Length > 200) return new("Contacto.NombreNoValido", "El nombre no puede superar los 200 caracteres.", TipoError.VALIDACION);
        if (string.IsNullOrWhiteSpace(request.Nif) || NormalizarNif(request.Nif).Length == 0) return new("Contacto.NifObligatorio", "El NIF es obligatorio.", TipoError.VALIDACION);
        if (!EsIdentificadorFiscalValido(NormalizarNif(request.Nif))) return new("Contacto.NifNoValido", "Introduce un DNI, NIE o NIF de empresa válido.", TipoError.VALIDACION);
        if (request.Clasificacion is null || !Clasificaciones.Contains(request.Clasificacion.Trim().ToUpperInvariant()))
            return new("Contacto.ClasificacionNoValida", "Selecciona una clasificación válida: cliente, proveedor o ambos.", TipoError.VALIDACION);
        if (!string.IsNullOrWhiteSpace(request.Email) && (request.Email.Trim().Length > 320 || !MailAddress.TryCreate(request.Email.Trim(), out _)))
            return new("Contacto.EmailNoValido", "Introduce un correo electrónico válido.", TipoError.VALIDACION);
        if (!string.IsNullOrWhiteSpace(request.Telefono) && !EsTelefonoValido(request.Telefono.Trim()))
            return new("Contacto.TelefonoNoValido", "Introduce un teléfono español de 9 cifras, con prefijo +34 opcional.", TipoError.VALIDACION);
        if (request.Direccion?.Length > 500) return new("Contacto.DireccionNoValida", "La dirección no puede superar los 500 caracteres.", TipoError.VALIDACION);
        return null;
    }

    private async Task<Error?> GuardarAsync(CancellationToken ct)
    {
        try
        {
            await context.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ContactoErrores.Duplicado;
        }
    }

    private static string? Opcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static bool EsTelefonoValido(string telefono)
    {
        if (!TelefonoConSeparadores.IsMatch(telefono)) return false;
        var normalizado = string.Concat(telefono.Where(caracter => char.IsDigit(caracter) || caracter == '+'));
        return TelefonoEspanolValido.IsMatch(normalizado);
    }
}
