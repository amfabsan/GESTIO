using System.Reflection;
using System.Security.Cryptography;
using Gestio.Api.Common;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Models.Requests;
using Gestio.Api.Models.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gestio.Api.Services;

public sealed record ReglasSubida(long TamanoMaximoBytes, IReadOnlyList<TipoArchivoPermitido> TiposPermitidos);

public sealed record ArchivoOriginal(Stream Contenido, string TipoMime, string NombreArchivo);

public sealed class DocumentosService(
    ApplicationDbContext context,
    IFileStorageService storage,
    IOptions<DocumentosOptions> options,
    TimeProvider timeProvider,
    ILogger<DocumentosService> logger,
    ILectorDocumento lector,
    AccesoDocumentos acceso)
{
    public ReglasSubida ObtenerReglasSubida() => new(options.Value.TamanoMaximoBytes, ValidadorArchivo.TiposPermitidos);

    public async Task<IReadOnlyList<Documento>> ListarAsync(UsuarioActual usuario, CancellationToken cancellationToken) =>
        await ConDetalle(ConsultablesPor(usuario))
            .OrderByDescending(documento => documento.FechaSubida)
            .ToListAsync(cancellationToken);

    public Task<Documento?> ObtenerAsync(UsuarioActual usuario, Guid id, CancellationToken cancellationToken) =>
        ConDetalle(ConsultablesPor(usuario))
            .FirstOrDefaultAsync(documento => documento.Id == id, cancellationToken);

    public async Task<ArchivoOriginal?> AbrirOriginalAsync(UsuarioActual usuario, Guid id, CancellationToken cancellationToken)
    {
        var documento = await ConsultablesPor(usuario)
            .AsNoTracking()
            .FirstOrDefaultAsync(documento => documento.Id == id, cancellationToken);
        if (documento?.StorageKey is null) return null;

        var contenido = await storage.AbrirAsync(documento.StorageKey, cancellationToken);
        return contenido is null ? null : new ArchivoOriginal(contenido, documento.TipoMime, documento.NombreArchivo);
    }

    public async Task<Resultado<Documento>> SubirAsync(UsuarioActual usuario, string nombreArchivo, Stream contenido, long tamanoBytes, CancellationToken cancellationToken)
    {
        if (usuario.Rol != Rol.AUTONOMO) return DocumentoErrores.SoloAutonomoPuedeSubir;
        if (tamanoBytes <= 0) return DocumentoErrores.ArchivoVacio;
        if (tamanoBytes > options.Value.TamanoMaximoBytes) return DocumentoErrores.TamanoExcedido(options.Value.TamanoMaximoBytes);

        var cabecera = new byte[ValidadorArchivo.BytesCabecera];
        var leidos = await contenido.ReadAtLeastAsync(cabecera, cabecera.Length, throwOnEndOfStream: false, cancellationToken);
        var tipoMime = ValidadorArchivo.ObtenerTipoMimeReal(nombreArchivo, cabecera.AsSpan(0, leidos));
        if (tipoMime is null) return DocumentoErrores.TipoNoPermitido(ValidadorArchivo.TiposPermitidos.Select(tipo => tipo.Extension));

        contenido.Position = 0;
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(contenido, cancellationToken));
        contenido.Position = 0;

        string storageKey;
        try
        {
            storageKey = await storage.GuardarAsync(contenido, usuario.Id, ValidadorArchivo.ExtensionDeAlmacenamiento(tipoMime), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "No se pudo almacenar el archivo subido por {UsuarioId}", usuario.Id);
            return DocumentoErrores.AlmacenamientoFallido;
        }

        var nombre = Path.GetFileName(nombreArchivo);
        if (nombre.Length > 255) nombre = nombre[^255..];

        var ahora = timeProvider.GetUtcNow();
        var documento = new Documento
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            NombreArchivo = nombre,
            TipoMime = tipoMime,
            TamanoBytes = tamanoBytes,
            Origen = OrigenDocumento.SUBIDA_WEB,
            StorageKey = storageKey,
            HashSha256 = hash,
            FechaSubida = ahora,
            Estado = EstadoDocumento.SUBIDO
        };

        documento.Eventos.Add(new EventoDocumento(
            usuario.Id,
            TipoEventoDocumento.SUBIDA,
            ahora,
            $"Archivo original recibido ({tamanoBytes} bytes)."));

        context.Documentos.Add(documento);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await storage.EliminarAsync(storageKey, CancellationToken.None);
            throw;
        }

        try
        {
            await ProcesarAsync(usuario, documento.Id, false, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo confirmar el procesamiento de {DocumentoId}; la carga original se conserva", documento.Id);
            context.ChangeTracker.Clear();
        }

        return (await ObtenerAsync(usuario, documento.Id, CancellationToken.None))!;
    }

    public async Task<Resultado<Documento>> ProcesarAsync(UsuarioActual usuario, Guid id, bool reintento, CancellationToken cancellationToken)
    {
        var documento = await ConsultablesPor(usuario).Include(d => d.Eventos).Include(d => d.ExtraccionIa)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (documento is null) return new Error("documento.no_encontrado", "Documento no encontrado.", TipoError.NO_ENCONTRADO);
        if (reintento ? documento.Estado is not (EstadoDocumento.ERROR or EstadoDocumento.PENDIENTE_VALIDACION)
            : documento.Estado != EstadoDocumento.SUBIDO)
            return new Error("documento.estado", "El documento no admite procesamiento en su estado actual.", TipoError.CONFLICTO);

        Stream? contenidoOriginal = null;
        Exception? falloOriginal = null;

        try
        {
            if (documento.StorageKey is not null)
                contenidoOriginal = await storage.AbrirAsync(documento.StorageKey, cancellationToken);
        }
        catch (Exception ex)
        {
            falloOriginal = ex;
        }

        await using var original = contenidoOriginal;

        var intento = new IntentoProcesamiento { DocumentoId = id, Inicio = timeProvider.GetUtcNow() };
        documento.Estado = EstadoDocumento.PROCESANDO;
        var inicio = new EventoDocumento(reintento ? usuario.Id : null, TipoEventoDocumento.PROCESAMIENTO_INICIADO, intento.Inicio, "PROCESANDO: inicio de extracción del original.");
        documento.Eventos.Add(inicio);
        context.Add(inicio);
        context.Add(intento);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            return new Error("documento.ocupado", "Otro procesamiento ya ha comenzado. Actualiza el documento.", TipoError.CONFLICTO);
        }

        try
        {
            if (original is null) throw new InvalidOperationException("No se puede acceder al archivo original conservado.", falloOriginal);

            var reconocido = await lector.LeerAsync(original, documento.TipoMime, cancellationToken);
            intento.Version = reconocido.Version;

            var extraccion = ExtractorCampos.Extraer(reconocido, id);
            if (ExtractorCampos.ContarDatos(extraccion) == 0)
                throw new InvalidOperationException("Se ha reconocido texto, pero no se ha podido identificar ningún dato de factura. Prueba con el PDF original o una imagen más nítida y comprueba que el documento sea legible.");

            intento.ValoresJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                extraccion.EmisorNombre,
                extraccion.EmisorNif,
                extraccion.NumeroFactura,
                extraccion.FechaEmision,
                extraccion.BaseImponible,
                extraccion.TipoIva,
                extraccion.CuotaIva,
                extraccion.Total,
                extraccion.DesglosesIva,
                extraccion.Advertencias,
                Confianza = (decimal?)null
            });

            extraccion.DatosCrudosJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                texto = reconocido.Texto,
                lecturas = reconocido.Lecturas,
                desglosesIva = extraccion.DesglosesIva,
                advertencias = extraccion.Advertencias
            });

            if (documento.ExtraccionIa is not null) context.Remove(documento.ExtraccionIa);

            documento.ExtraccionIa = extraccion;
            context.Add(extraccion);
            documento.Estado = EstadoDocumento.PENDIENTE_VALIDACION;
            intento.Resultado = "CORRECTO";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo de extracción de {DocumentoId}", id);
            intento.Error = ex is InvalidOperationException ? ex.Message : ex is OperationCanceledException
                ? "Se agotó el tiempo de extracción. Puedes reintentar." : "No se pudo extraer el documento. Comprueba su legibilidad e inténtalo de nuevo.";
            intento.Resultado = "ERROR";
            documento.Estado = EstadoDocumento.ERROR;
        }

        intento.Fin = timeProvider.GetUtcNow();
        var fin = new EventoDocumento(
            null,
            intento.Error is null ? TipoEventoDocumento.PROCESAMIENTO_CORRECTO : TipoEventoDocumento.PROCESAMIENTO_FALLIDO,
            intento.Fin.Value,
            intento.Error ?? "PENDIENTE_VALIDACION: datos propuestos disponibles para revisión.");
        documento.Eventos.Add(fin);
        context.Add(fin);

        await context.SaveChangesAsync(CancellationToken.None);
        return (await ObtenerAsync(usuario, id, CancellationToken.None))!;
    }

    public async Task<IReadOnlyList<HistorialCategoria>> ObtenerHistorialCategoriasAsync(
        UsuarioActual usuario,
        Guid id,
        CancellationToken cancellationToken)
    {
        var documento = await ConsultablesPor(usuario)
            .AsNoTracking()
            .Include(d => d.HistorialCategorias)
            .ThenInclude(h => h.Usuario)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (documento is null)
            return [];

        return documento.HistorialCategorias
            .OrderByDescending(h => h.FechaCambio)
            .ToList();
    }

    public async Task<Resultado<Documento>> CambiarCategoriaAsync(
        UsuarioActual usuario,
        Guid id,
        string? nuevaCategoria,
        CancellationToken cancellationToken)
    {
        if (usuario.Rol != Rol.GESTOR)
            return new Error(
                "documento.categoria.sin_permiso",
                "Solo el gestor de la asesoría puede cambiar la categoría del documento.",
                TipoError.PROHIBIDO);

        var documento = await ConsultablesPor(usuario)
            .Include(d => d.Eventos)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (documento is null)
            return new Error("documento.no_encontrado", "Documento no encontrado.", TipoError.NO_ENCONTRADO);

        if (!TryLeerCategoria(nuevaCategoria, out var categoria))
            return new Error(
                "documento.categoria.invalida",
                "La categoría indicada no es válida. Usa INGRESO, GASTO u OTRO.",
                TipoError.VALIDACION);

        // Guardar la categoría vigente no es un cambio: no se registra nada (PA 110).
        // Se devuelve con todo su detalle, porque MapearDocumento necesita documento.Usuario.
        if (documento.Categoria == categoria)
            return (await ObtenerAsync(usuario, id, cancellationToken))!;

        var categoriaAnterior = documento.Categoria;
        documento.Categoria = categoria;
        var ahora = timeProvider.GetUtcNow();

        context.HistorialCategorias.Add(new HistorialCategoria
        {
            Id = Guid.NewGuid(),
            DocumentoId = documento.Id,
            UsuarioId = usuario.Id,
            CategoriaAnterior = categoriaAnterior,
            CategoriaNueva = categoria,
            FechaCambio = ahora
        });

        var evento = new EventoDocumento(
            usuario.Id,
            TipoEventoDocumento.CATEGORIZACION_ACTUALIZADA,
            ahora,
            $"Categoría actualizada de {categoriaAnterior} a {categoria}.");
        documento.Eventos.Add(evento);
        context.Add(evento);

        await context.SaveChangesAsync(cancellationToken);
        return (await ObtenerAsync(usuario, id, CancellationToken.None))!;
    }

    public async Task<Resultado<Documento>> CorregirAsync(
        UsuarioActual usuario,
        Guid id,
        IEnumerable<CampoValidacionRequest> cambios,
        CancellationToken cancellationToken)
    {
        if (usuario.Rol != Rol.GESTOR)
            return DocumentoErrores.SinPermisoValidacion;

        var documento = await ConsultablesPor(usuario)
            .Include(d => d.ExtraccionIa)
            .Include(d => d.Eventos)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (documento is null)
            return new Error("documento.no_encontrado", "Documento no encontrado.", TipoError.NO_ENCONTRADO);

        if (documento.Estado == EstadoDocumento.VALIDADO)
            return DocumentoErrores.DocumentoYaValidado;

        if (documento.Estado != EstadoDocumento.PENDIENTE_VALIDACION)
            return DocumentoErrores.EstadoNoPermitidoParaValidacion;

        if (documento.ExtraccionIa is null)
            return DocumentoErrores.ValidacionCamposObligatorios;

        var listaCambios = cambios.ToList();
        if (listaCambios.Count == 0)
            return DocumentoErrores.ValidacionSinCambios;

        foreach (var cambio in listaCambios)
        {
            if (string.IsNullOrWhiteSpace(cambio.Campo))
                return DocumentoErrores.ValidacionCamposObligatorios;

            if (!TryGetPropiedad(documento.ExtraccionIa, cambio.Campo, out var propiedad))
                return DocumentoErrores.CampoConValorInvalido;

            var valorAnterior = propiedad.GetValue(documento.ExtraccionIa);

            if (!TryConvertirValor(cambio.Campo, cambio.Valor, out var valorNuevo))
            {
                return new Error(
                    "documento.campo_invalido",
                    $"{Etiqueta(cambio.Campo)}: «{cambio.Valor}» no tiene un formato válido.",
                    TipoError.VALIDACION);
            }

            if (Igual(valorAnterior, valorNuevo))
                continue;

            propiedad.SetValue(documento.ExtraccionIa, valorNuevo);

            context.HistorialValidacion.Add(new HistorialValidacion
            {
                Id = Guid.NewGuid(),
                DocumentoId = documento.Id,
                UsuarioId = usuario.Id,
                Campo = propiedad.Name,
                ValorAnterior = FormatearValor(valorAnterior),
                ValorNuevo = FormatearValor(valorNuevo),
                TipoCambio = "CORRECCION",
                FechaCambio = timeProvider.GetUtcNow()
            });

            // context.Add explícito, igual que en ProcesarAsync: sin él, EF Core puede tratar
            // el evento (con Id ya asignado) como existente y lanzar un UPDATE en vez de un INSERT.
            var correccion = EventoDocumento.Correccion(
                usuario.Id,
                timeProvider.GetUtcNow(),
                propiedad.Name,
                FormatearValor(valorAnterior),
                FormatearValor(valorNuevo));
            documento.Eventos.Add(correccion);
            context.Add(correccion);
        }

        var errores = ValidarCampos(documento.ExtraccionIa);
        if (errores.Count > 0)
            return ErrorDeValidacion(errores);

        await context.SaveChangesAsync(cancellationToken);

        // Se devuelve el documento con todo su detalle (Usuario, Anomalias, Historial…),
        // porque MapearDocumento necesita documento.Usuario.
        return (await ObtenerAsync(usuario, id, CancellationToken.None))!;
    }

    public async Task<Resultado<Documento>> AprobarAsync(
        UsuarioActual usuario,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (usuario.Rol != Rol.GESTOR)
            return DocumentoErrores.SinPermisoValidacion;

        var documento = await ConsultablesPor(usuario)
            .Include(d => d.ExtraccionIa)
            .Include(d => d.Eventos)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (documento is null)
            return new Error("documento.no_encontrado", "Documento no encontrado.", TipoError.NO_ENCONTRADO);

        if (documento.Estado == EstadoDocumento.VALIDADO)
            return DocumentoErrores.DocumentoYaValidado;

        if (documento.Estado != EstadoDocumento.PENDIENTE_VALIDACION)
            return DocumentoErrores.EstadoNoPermitidoParaValidacion;

        if (documento.ExtraccionIa is null)
            return DocumentoErrores.ValidacionCamposObligatorios;

        var errores = ValidarCampos(documento.ExtraccionIa);
        if (errores.Count > 0)
            return ErrorDeValidacion(errores);

        documento.Estado = EstadoDocumento.VALIDADO;

        var evento = new EventoDocumento(
            usuario.Id,
            TipoEventoDocumento.VALIDACION_APROBADA,
            timeProvider.GetUtcNow(),
            "Documento validado y aprobado por el gestor.");
        documento.Eventos.Add(evento);
        context.Add(evento);

        context.HistorialValidacion.Add(new HistorialValidacion
        {
            Id = Guid.NewGuid(),
            DocumentoId = documento.Id,
            UsuarioId = usuario.Id,
            Campo = "DOCUMENTO",
            ValorAnterior = EstadoDocumento.PENDIENTE_VALIDACION.ToString(),
            ValorNuevo = EstadoDocumento.VALIDADO.ToString(),
            TipoCambio = "APROBACION",
            FechaCambio = timeProvider.GetUtcNow()
        });

        await context.SaveChangesAsync(cancellationToken);

        return (await ObtenerAsync(usuario, id, CancellationToken.None))!;
    }

    private static readonly Dictionary<string, string> Etiquetas = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(ExtraccionIa.EmisorNombre)] = "Proveedor",
        [nameof(ExtraccionIa.EmisorNif)] = "NIF",
        [nameof(ExtraccionIa.NumeroFactura)] = "N.º factura",
        [nameof(ExtraccionIa.FechaEmision)] = "Fecha",
        [nameof(ExtraccionIa.BaseImponible)] = "Base imponible",
        [nameof(ExtraccionIa.TipoIva)] = "IVA",
        [nameof(ExtraccionIa.CuotaIva)] = "Cuota de IVA",
        [nameof(ExtraccionIa.Total)] = "Total",
        [nameof(ExtraccionIa.CategoriaSugerida)] = "Categoría",
    };

    // Margen de redondeo entre importes calculados y leídos.
    private const decimal ToleranciaImportes = 0.02m;

    // Tipos de retención de IRPF habituales: el total puede ser base + cuota - retención.
    private static readonly decimal[] RetencionesIrpf = [1m, 2m, 7m, 15m, 19m];

    // Catálogo asignable (PB-09). SIN_CLASIFICAR es solo el estado inicial: no se puede elegir.
    private static readonly CategoriaDocumento[] CatalogoCategorias =
        [CategoriaDocumento.INGRESO, CategoriaDocumento.GASTO, CategoriaDocumento.OTRO];

    // Se compara por nombre: Enum.TryParse aceptaría también "SIN_CLASIFICAR" o números como "99".
    private static bool TryLeerCategoria(string? valor, out CategoriaDocumento categoria)
    {
        categoria = CatalogoCategorias.FirstOrDefault(c =>
            string.Equals(c.ToString(), valor?.Trim(), StringComparison.OrdinalIgnoreCase));
        return categoria != CategoriaDocumento.SIN_CLASIFICAR;
    }

    private static string Etiqueta(string campo) =>Etiquetas.GetValueOrDefault(campo.Trim(), campo);

    private static string Euros(decimal importe) =>
        importe.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-ES")) + " €";

    private static Error ErrorDeValidacion(IEnumerable<string> errores) => new(
        "documento.validacion.campos_obligatorios",
        $"Revisa estos campos: {string.Join("; ", errores)}.",
        TipoError.VALIDACION);

    private static List<string> ValidarCampos(ExtraccionIa extraccion)
    {
        var errores = new List<string>();

        void Falta(string campo) => errores.Add($"{Etiqueta(campo)} es obligatorio");

        if (string.IsNullOrWhiteSpace(extraccion.EmisorNombre))
            Falta(nameof(ExtraccionIa.EmisorNombre));
        if (string.IsNullOrWhiteSpace(extraccion.EmisorNif))
            Falta(nameof(ExtraccionIa.EmisorNif));
        if (string.IsNullOrWhiteSpace(extraccion.NumeroFactura))
            Falta(nameof(ExtraccionIa.NumeroFactura));
        if (extraccion.FechaEmision is null)
            Falta(nameof(ExtraccionIa.FechaEmision));
        if (extraccion.Total is null)
            Falta(nameof(ExtraccionIa.Total));

        if (!string.IsNullOrWhiteSpace(extraccion.EmisorNif) && !EsNifValido(extraccion.EmisorNif))
            errores.Add($"{Etiqueta(nameof(ExtraccionIa.EmisorNif))} no tiene un formato válido");

        if (extraccion.FechaEmision is not null && extraccion.FechaEmision.Value > DateOnly.FromDateTime(DateTime.Today.AddDays(30)))
            errores.Add($"{Etiqueta(nameof(ExtraccionIa.FechaEmision))} es posterior a hoy en más de 30 días");

        errores.AddRange(ValidarImportes(extraccion));

        return errores;
    }

    private static IEnumerable<string> ValidarImportes(ExtraccionIa extraccion)
    {
        var baseImponible = extraccion.BaseImponible;
        var tipo = extraccion.TipoIva;
        // Con varios tipos de IVA, TipoIva no describe toda la base: solo se comprueba el total.
        var tipoUnico = extraccion.DesglosesIva.Count <= 1;

        if (baseImponible is { } b && tipo is { } t && extraccion.CuotaIva is { } cuota && tipoUnico &&
            Math.Abs(Math.Round(b * t / 100m, 2) - cuota) > ToleranciaImportes)
            yield return $"{Etiqueta(nameof(ExtraccionIa.CuotaIva))} ({Euros(cuota)}) no es el {t:0.##} % de la base imponible ({Euros(Math.Round(b * t / 100m, 2))})";

        var cuotaEfectiva = extraccion.CuotaIva ??
            (baseImponible is { } bc && tipo is { } tc && tipoUnico ? Math.Round(bc * tc / 100m, 2) : null);

        if (baseImponible is { } bt && cuotaEfectiva is { } ct && extraccion.Total is { } total)
        {
            var diferencia = bt + ct - total;
            var cuadra = Math.Abs(diferencia) <= ToleranciaImportes ||
                RetencionesIrpf.Any(r => Math.Abs(diferencia - Math.Round(bt * r / 100m, 2)) <= ToleranciaImportes);

            if (!cuadra)
                yield return $"{Etiqueta(nameof(ExtraccionIa.Total))} ({Euros(total)}) no coincide con base imponible + cuota de IVA ({Euros(bt + ct)})";
        }
    }

    private static bool EsNifValido(string nif)
    {
        var limpio = nif.Trim().ToUpperInvariant();
        if (limpio.Length < 9) return false;

        if (char.IsLetter(limpio[0]))
        {
            if (limpio.Length != 9) return false;

            var cuerpo = limpio[1..8];
            if (!cuerpo.All(char.IsAsciiDigit)) return false;

            // NIE: X/Y/Z + 7 dígitos + letra de control (X=0, Y=1, Z=2 delante del número).
            if (limpio[0] is 'X' or 'Y' or 'Z')
            {
                const string letras = "TRWAGMYFPDXBNJZSQVHLCKE";
                var prefijo = limpio[0] - 'X';
                var numero = int.Parse($"{prefijo}{cuerpo}");
                return char.IsAsciiLetter(limpio[8]) && letras[numero % 23] == limpio[8];
            }

            // CIF (sociedades): letra + 7 dígitos + dígito o letra de control.
            // Solo se comprueba el formato, no el dígito de control.
            return char.IsAsciiLetterOrDigit(limpio[8]);
        }

        return true;
    }

    private static bool TryGetPropiedad(
    object target,
    string nombreCampo,
    out PropertyInfo propiedad)
    {
        propiedad = target.GetType().GetProperty(
            nombreCampo,
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.IgnoreCase)!;

        return propiedad is not null;
    }

    private static bool TryConvertirValor(
    string campo,
    string? valor,
    out object? resultado)
    {
        resultado = null;

        if (valor is null)
        {
            return true;
        }

        var nombreCampo = campo.Trim().ToLowerInvariant();
        var valorLimpio = valor.Trim();

        switch (nombreCampo)
        {
            case "numerofactura":
                resultado = valorLimpio;
                return true;

            case "emisornombre":
                resultado = valorLimpio;
                return true;

            case "emisornif":
                resultado = valorLimpio;
                return true;

            case "fechaemision":
                if (DateOnly.TryParse(
                        valorLimpio,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out var fecha))
                {
                    resultado = fecha;
                    return true;
                }

                if (DateOnly.TryParse(
                        valorLimpio,
                        new System.Globalization.CultureInfo("es-ES"),
                        System.Globalization.DateTimeStyles.None,
                        out fecha))
                {
                    resultado = fecha;
                    return true;
                }

                return false;

            case "baseimponible":
            case "tipoiva":
            case "cuotaiva":
            case "total":
                var numeroNormalizado = valorLimpio
                    .Replace("€", string.Empty)
                    .Replace(" ", string.Empty);

                if (numeroNormalizado.Contains(','))
                {
                    numeroNormalizado = numeroNormalizado
                        .Replace(".", string.Empty)
                        .Replace(',', '.');
                }

                if (decimal.TryParse(
                        numeroNormalizado,
                        System.Globalization.NumberStyles.AllowDecimalPoint |
                        System.Globalization.NumberStyles.AllowLeadingSign,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var numero))
                {
                    resultado = numero;
                    return true;
                }

                return false;

            default:
                return false;
        }
    }

    private static bool Igual(object? a, object? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;

        return a.Equals(b);
    }

    private static string? FormatearValor(object? valor) =>
        valor switch
        {
            null => null,
            DateOnly d => d.ToString("yyyy-MM-dd"),
            DateTimeOffset dto => dto.ToString("o"),
            decimal d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => valor.ToString()
        };

    private IQueryable<Documento> ConsultablesPor(UsuarioActual usuario) => acceso.ConsultablesPor(usuario);

    private static IQueryable<Documento> ConDetalle(IQueryable<Documento> consulta) => consulta
        .AsNoTracking()
        .AsSplitQuery()
        .Include(documento => documento.Usuario)
        .Include(documento => documento.ExtraccionIa)
        .Include(documento => documento.Anomalias)
        .Include(documento => documento.Eventos)
        .Include(documento => documento.Intentos)
        .Include(documento => documento.HistorialValidacion);
}