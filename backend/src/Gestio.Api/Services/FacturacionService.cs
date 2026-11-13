using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Models.Request;
using Gestio.Api.Models.Response;
using Gestio.Api.Services.Verifactu;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Gestio.Api.Services;

public sealed class FacturacionService(
    ApplicationDbContext context,
    GeneradorObligaciones generador,
    TimeProvider timeProvider,
    RegistroFacturacionService registros,
    ILogger<FacturacionService> logger)
{
    // UT22 / PA 1591: texto del mockup 5. La emisión y su registro se guardan juntos; si falla, no queda nada emitido.
    private static readonly Error EmisionRevertida = new("Factura.EmisionRevertida",
        "No se pudo crear el registro de facturación. Se ha revertido la emisión: la factura sigue como borrador y no consume número.",
        TipoError.CONFLICTO);

    private static readonly Error Prohibido = new("Factura.Prohibido", "No tienes permiso para realizar esta operación.", TipoError.PROHIBIDO);
    private static readonly Error NoEncontrada = new("Factura.NoEncontrada", "No se ha encontrado la factura.", TipoError.NO_ENCONTRADO);
    private static readonly Error SoloBorradores = new("Factura.SoloBorradores", "Solo se pueden modificar o eliminar borradores.", TipoError.CONFLICTO);

    public async Task<Resultado<IReadOnlyList<FacturaListaResponse>>> ListarAsync(
        UsuarioActual usuario, Guid autonomoId, CancellationToken ct)
    {
        if (!await PuedeConsultarAsync(usuario, autonomoId, ct)) return Prohibido;

        var facturas = await context.Facturas.AsNoTracking()
            .Include(f => f.Lineas)
            .Include(f => f.Contacto)
            .Where(f => f.AutonomoId == autonomoId)
            .OrderByDescending(f => f.Fecha)
            .ThenByDescending(f => f.Creada)
            .ToListAsync(ct);

        return facturas.Select(f =>
        {
            var calculo = Calcular(f).Calculo;
            var emitida = f.Estado == EstadoFactura.EMITIDA;
            return new FacturaListaResponse(
                f.Id, f.Estado.ToString(), f.Serie, f.Numero,
                f.Numero is null ? null : $"{f.Serie}-{f.Numero.Value:D4}", f.Fecha, f.ContactoId,
                emitida ? f.ClienteNombre : f.Contacto?.Nombre,
                emitida ? f.ClienteNif : f.Contacto?.Nif,
                emitida ? f.ClienteDireccion : f.Contacto?.Direccion,
                f.Lineas.Count,
                calculo?.BaseImponible ?? f.BaseImponible,
                calculo?.Total ?? f.Total);
        }).ToArray();
    }

    public async Task<Resultado<FacturaResponse>> ConsultarAsync(
        UsuarioActual usuario, Guid autonomoId, Guid facturaId, CancellationToken ct)
    {
        if (!await PuedeConsultarAsync(usuario, autonomoId, ct)) return Prohibido;
        var factura = await ObtenerFacturaAsync(autonomoId, facturaId, ct);
        return factura is null ? NoEncontrada : Mapear(factura);
    }

    public async Task<Resultado<FacturaResponse>> GuardarBorradorAsync(
        UsuarioActual usuario, Guid autonomoId, Guid? facturaId, GuardarFacturaRequest request, CancellationToken ct)
    {
        if (!EsPropietario(usuario, autonomoId)) return Prohibido;
        if (!FechaValida(request.Fecha)) return ErrorValidacion("Factura.FechaNoValida", "La fecha de factura no es válida.");
        if (request.RetencionPorcentaje is < 0m or > 100m)
            return ErrorValidacion("Factura.RetencionNoValida", "La retención debe estar entre 0 % y 100 %.");

        await using var transaccion = await generador.IniciarAsync(autonomoId, ct);
        var contacto = await ObtenerContactoBorradorAsync(autonomoId, request.ContactoId, ct);
        if (request.ContactoId is not null && contacto is null)
            return ErrorValidacion("Factura.ClienteNoValido", "El cliente debe pertenecer a la agenda del autónomo.");

        Factura factura;
        if (facturaId is null)
        {
            var emisor = await context.Usuarios.SingleOrDefaultAsync(u => u.Id == autonomoId, ct);
            if (emisor is null) return NoEncontrada;
            factura = new Factura
            {
                Id = Guid.NewGuid(), AutonomoId = autonomoId, Autonomo = emisor,
                Serie = $"F-{request.Fecha.Year}", Fecha = request.Fecha,
                Estado = EstadoFactura.BORRADOR, Creada = timeProvider.GetUtcNow(),
                Actualizada = timeProvider.GetUtcNow()
            };
            context.Facturas.Add(factura);
        }
        else
        {
            var existente = await ObtenerFacturaAsync(autonomoId, facturaId.Value, ct);
            if (existente is null) return NoEncontrada;
            if (existente.Estado != EstadoFactura.BORRADOR) return SoloBorradores;
            factura = existente;
            context.LineasFactura.RemoveRange(factura.Lineas);
            factura.Lineas.Clear();
            factura.Actualizada = timeProvider.GetUtcNow();
        }

        factura.ContactoId = contacto?.Id;
        factura.Contacto = contacto;
        factura.Fecha = request.Fecha;
        factura.Serie = $"F-{request.Fecha.Year}";
        factura.RetencionPorcentaje = request.RetencionPorcentaje;
        factura.Lineas = (request.Lineas ?? Array.Empty<GuardarLineaFacturaRequest>())
            .Select((linea, orden) => new LineaFactura
            {
                Id = Guid.NewGuid(), FacturaId = factura.Id, Factura = factura, Orden = orden,
                Descripcion = NormalizarOpcional(linea.Descripcion),
                Cantidad = linea.Cantidad,
                PrecioUnitario = linea.PrecioUnitario,
                IvaPorcentaje = linea.IvaPorcentaje
            }).ToList();
        context.LineasFactura.AddRange(factura.Lineas);

        var calculo = Calcular(factura);
        factura.BaseImponible = calculo.Calculo?.BaseImponible;
        factura.CuotaIva = calculo.Calculo?.CuotaIva;
        factura.ImporteRetencion = calculo.Calculo?.ImporteRetencion;
        factura.Total = calculo.Calculo?.Total;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new Error("Factura.ModificadaConcurrentemente", "La factura ha cambiado mientras la editabas. Recarga e inténtalo de nuevo.", TipoError.CONFLICTO);
        }

        if (transaccion is not null) await transaccion.CommitAsync(ct);
        return Mapear(factura);
    }

    public async Task<Resultado<bool>> EliminarBorradorAsync(
        UsuarioActual usuario, Guid autonomoId, Guid facturaId, CancellationToken ct)
    {
        if (!EsPropietario(usuario, autonomoId)) return Prohibido;
        await using var transaccion = await generador.IniciarAsync(autonomoId, ct);
        var factura = await context.Facturas.Include(f => f.Lineas)
            .SingleOrDefaultAsync(f => f.Id == facturaId && f.AutonomoId == autonomoId, ct);
        if (factura is null) return NoEncontrada;
        if (factura.Estado != EstadoFactura.BORRADOR) return SoloBorradores;
        context.Facturas.Remove(factura);
        await context.SaveChangesAsync(ct);
        if (transaccion is not null) await transaccion.CommitAsync(ct);
        return true;
    }

    public async Task<Resultado<FacturaResponse>> ComprobarAsync(
        UsuarioActual usuario, Guid autonomoId, Guid facturaId, CancellationToken ct)
    {
        if (!await PuedeConsultarAsync(usuario, autonomoId, ct)) return Prohibido;
        var factura = await ObtenerFacturaAsync(autonomoId, facturaId, ct);
        if (factura is null) return NoEncontrada;
        var validacion = ValidarParaEmitir(factura);
        if (validacion is not null) return validacion;
        return Mapear(factura, Calcular(factura).Calculo);
    }

    public async Task<Resultado<FacturaResponse>> EmitirAsync(
        UsuarioActual usuario, Guid autonomoId, Guid facturaId, CancellationToken ct)
    {
        if (!EsPropietario(usuario, autonomoId)) return Prohibido;

        await using var transaccion = await generador.IniciarAsync(autonomoId, ct);
        var factura = await ObtenerFacturaAsync(autonomoId, facturaId, ct);
        if (factura is null) return NoEncontrada;
        if (factura.Estado == EstadoFactura.EMITIDA) return Mapear(factura, Calcular(factura).Calculo);
        if (factura.Estado != EstadoFactura.BORRADOR) return SoloBorradores;

        var validacion = ValidarParaEmitir(factura);
        if (validacion is not null) return validacion;

        var calculo = Calcular(factura).Calculo!;
        var ultimoNumero = await context.Facturas
            .Where(f => f.AutonomoId == autonomoId && f.Serie == factura.Serie && f.Numero != null)
            .MaxAsync(f => f.Numero, ct) ?? 0;

        // Un único instante para la emisión y para la fecha de generación del registro.
        var instante = timeProvider.GetUtcNow();
        factura.Numero = ultimoNumero + 1;
        factura.Estado = EstadoFactura.EMITIDA;
        factura.Emitida = instante;
        factura.Actualizada = instante;
        factura.BaseImponible = calculo.BaseImponible;
        factura.CuotaIva = calculo.CuotaIva;
        factura.ImporteRetencion = calculo.ImporteRetencion;
        factura.Total = calculo.Total;
        factura.EmisorNombre = $"{factura.Autonomo.Nombre} {factura.Autonomo.PrimerApellido} {factura.Autonomo.SegundoApellido}".Trim();
        factura.EmisorNif = factura.Autonomo.Nif;
        factura.EmisorDireccion = factura.Autonomo.DireccionFiscal!.Trim();
        factura.ClienteNombre = factura.Contacto!.Nombre;
        factura.ClienteNif = factura.Contacto.Nif;
        factura.ClienteDireccion = factura.Contacto.Direccion!.Trim();

        try
        {
            // UT22: registro de alta en la misma transacción y en el mismo SaveChanges que la emisión.
            await registros.PrepararAltaAsync(factura, instante, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "No se pudo preparar el registro de facturación de la factura {FacturaId}", factura.Id);
            return Revertir(EmisionRevertida);
        }

        try
        {
            await context.SaveChangesAsync(ct);
            if (transaccion is not null) await transaccion.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, TableName: "facturas" })
        {
            return Revertir(new Error("Factura.NumeroEnConflicto", "No se pudo asignar el número. Recarga e inténtalo de nuevo.", TipoError.CONFLICTO));
        }
        catch (DbUpdateException ex)
        {
            // Lo único nuevo que se inserta en este guardado es el registro: cualquier otro fallo se trata como fallo
            // del registro. La transacción no se confirma, así que la factura sigue como borrador.
            logger.LogError(ex, "Falló el guardado de la emisión y el registro de facturación de la factura {FacturaId}", factura.Id);
            return Revertir(EmisionRevertida);
        }

        return Mapear(factura, calculo);
    }

    public async Task<Resultado<DireccionContactoFacturaResponse>> GuardarDireccionFiscalAsync(
        UsuarioActual usuario, Guid autonomoId, Guid contactoId, string? direccion, CancellationToken ct)
    {
        if (!EsPropietario(usuario, autonomoId)) return Prohibido;
        var normalizada = NormalizarOpcional(direccion);
        if (normalizada is null || normalizada.Length > 500)
            return ErrorValidacion("Contacto.DireccionNoValida", "Introduce una dirección fiscal de hasta 500 caracteres.");
        var contacto = await context.Contactos.SingleOrDefaultAsync(c => c.Id == contactoId && c.AutonomoId == autonomoId, ct);
        if (contacto is null) return NoEncontrada;
        contacto.Direccion = normalizada;
        await context.SaveChangesAsync(ct);
        return new DireccionContactoFacturaResponse(contacto.Id, contacto.Nombre, contacto.Nif, contacto.Direccion, contacto.Activo);
    }

    // Descarta los cambios en memoria de una emisión fallida: así ningún SaveChanges posterior en la misma
    // petición puede guardar la factura como emitida sin su registro (req. 5).
    private Error Revertir(Error error)
    {
        context.ChangeTracker.Clear();
        return error;
    }

    private Task<bool> PuedeConsultarAsync(UsuarioActual usuario, Guid autonomoId, CancellationToken ct) =>
        AccesoFacturacion.PuedeConsultarAsync(context, usuario, autonomoId, ct);

    private static bool EsPropietario(UsuarioActual usuario, Guid autonomoId) =>
        AccesoFacturacion.EsPropietario(usuario, autonomoId);

    private async Task<Contacto?> ObtenerContactoBorradorAsync(Guid autonomoId, Guid? contactoId, CancellationToken ct)
    {
        if (contactoId is null) return null;
        return await context.Contactos.SingleOrDefaultAsync(c => c.Id == contactoId && c.AutonomoId == autonomoId &&
            (c.Clasificacion == "CLIENTE" || c.Clasificacion == "AMBOS"), ct);
    }

    private async Task<Factura?> ObtenerFacturaAsync(Guid autonomoId, Guid facturaId, CancellationToken ct) =>
        await context.Facturas.Include(f => f.Lineas).Include(f => f.Contacto).Include(f => f.Autonomo)
            .SingleOrDefaultAsync(f => f.Id == facturaId && f.AutonomoId == autonomoId, ct);

    private static ResultadoCalculoFactura Calcular(Factura factura) => CalculadoraFactura.Calcular(
        factura.Lineas.OrderBy(l => l.Orden)
            .Select(l => new LineaCalculoFactura(l.Descripcion, l.Cantidad, l.PrecioUnitario, l.IvaPorcentaje)).ToArray(),
        factura.RetencionPorcentaje);

    private static Error? ValidarParaEmitir(Factura factura)
    {
        var pendientes = new List<string>();
        if (!FechaValida(factura.Fecha)) pendientes.Add("La fecha no es válida.");
        if (factura.Contacto is null) pendientes.Add("Selecciona un cliente de tu agenda.");
        else
        {
            if (!factura.Contacto.Activo) pendientes.Add("El cliente está inactivo.");
            if (factura.Contacto.Clasificacion is not ("CLIENTE" or "AMBOS")) pendientes.Add("El contacto no está clasificado como cliente.");
            if (string.IsNullOrWhiteSpace(factura.Contacto.Nombre)) pendientes.Add("El nombre fiscal del cliente es obligatorio.");
            if (string.IsNullOrWhiteSpace(factura.Contacto.Nif)) pendientes.Add("El NIF del cliente es obligatorio.");
            if (string.IsNullOrWhiteSpace(factura.Contacto.Direccion)) pendientes.Add("La dirección fiscal del cliente es obligatoria.");
        }
        if (string.IsNullOrWhiteSpace(factura.Autonomo.Nif) || string.IsNullOrWhiteSpace(factura.Autonomo.Nombre) ||
            string.IsNullOrWhiteSpace(factura.Autonomo.PrimerApellido) || string.IsNullOrWhiteSpace(factura.Autonomo.SegundoApellido))
            pendientes.Add("Completa los datos fiscales del emisor antes de emitir.");
        if (string.IsNullOrWhiteSpace(factura.Autonomo.DireccionFiscal))
            pendientes.Add("Completa la dirección fiscal del emisor en tu perfil fiscal antes de emitir.");

        var calculo = Calcular(factura);
        if (!calculo.EsValido)
            pendientes.AddRange(calculo.Errores.Select(TraducirErrorCalculo));

        return pendientes.Count == 0 ? null : ErrorValidacion("Factura.NoEmisible", string.Join(" ", pendientes.Distinct()));
    }

    private static string TraducirErrorCalculo(string codigo) => codigo switch
    {
        "Factura.LineasObligatorias" => "Añade al menos una línea.",
        "Factura.Linea.DescripcionObligatoria" => "Cada línea necesita una descripción.",
        "Factura.Linea.CantidadNoValida" => "La cantidad debe ser mayor que cero.",
        "Factura.Linea.PrecioNoValido" => "El precio unitario no puede ser negativo.",
        "Factura.Linea.IvaNoValido" => "El IVA debe ser 0 %, 4 %, 10 % o 21 %.",
        "Factura.RetencionNoValida" => "La retención debe estar entre 0 % y 100 %.",
        _ => "Revisa los datos de las líneas de factura."
    };

    private FacturaResponse Mapear(Factura factura, CalculoFactura? calculo = null) =>
        FacturaResponse.Desde(factura, calculo ?? Calcular(factura).Calculo);

    private static bool FechaValida(DateOnly fecha) => fecha.Year is > 1 and < 9999;

    private static string? NormalizarOpcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static Error ErrorValidacion(string codigo, string mensaje) => new(codigo, mensaje, TipoError.VALIDACION);
}