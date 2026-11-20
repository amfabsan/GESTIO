using Gestio.Api.Common;
using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Services;

/// <summary>
/// Calcula la estimación trimestral orientativa del IVA para un autónomo.
/// Reutiliza el perfil fiscal (PB-12) y valida autorización (PB-04, PB-07).
/// </summary>
public sealed class EstimacionIvaService(
    ApplicationDbContext context,
    PerfilesFiscalesService perfilesFiscalesService,
    TimeProvider timeProvider)
{
    private static readonly Error Prohibido = new(
        "Estimacion.Prohibido",
        "No tienes permiso para acceder a esta estimación.",
        TipoError.PROHIBIDO);

    private static readonly Error NoEncontrado = new(
        "Estimacion.NoEncontrado",
        "No se ha encontrado el autónomo.",
        TipoError.NO_ENCONTRADO);

    /// <summary>
    /// Calcula la estimación de IVA para un trimestre específico.
    /// </summary>
    public async Task<Resultado<EstimacionResponse>> CalcularAsync(
        UsuarioActual usuario,
        Guid autonomoId,
        int ejercicio,
        int trimestre,
        CancellationToken cancellationToken)
    {
        // 1. Validar autorización
        if (!await perfilesFiscalesService.PuedeAccederAsync(usuario, autonomoId, false, cancellationToken))
            return Prohibido;

        if (trimestre < 1 || trimestre > 4)
            return new Error("Estimacion.TrimestreNoValido", "El trimestre debe estar entre 1 y 4.", TipoError.VALIDACION);

        if (ejercicio < 1 || ejercicio > 9998)
            return new Error("Estimacion.EjercicioNoValido", "El ejercicio debe estar entre 1 y 9998.", TipoError.VALIDACION);

        // 2. Cargar perfil fiscal - si no existe, la estimación no está disponible
        var resultadoPerfil = await perfilesFiscalesService.ConsultarAsync(usuario, autonomoId, ejercicio, cancellationToken);
        if (!resultadoPerfil.EsCorrecto) return resultadoPerfil.Error!;

        var perfil = resultadoPerfil.Valor;
        string? motivoNoDisponible = null;

        // Validar si el perfil fiscal es soportado por el prototipo
        if (perfil is null)
        {
            motivoNoDisponible = "No hay perfil fiscal configurado para este ejercicio.";
        }
        else if (!EsPerfilSoportado(perfil))
        {
            motivoNoDisponible = "Estimación no disponible para este perfil fiscal.";
        }

        // Si no está disponible, retornar respuesta sin cifras
        if (motivoNoDisponible is not null)
        {
            return new EstimacionResponse(
                Ejercicio: ejercicio,
                Trimestre: trimestre,
                Disponible: false,
                MotivoNoDisponible: motivoNoDisponible,
                IvaIngresos: 0,
                IvaGastos: 0,
                Diferencia: 0,
                FechaCalculoUtc: timeProvider.GetUtcNow(),
                DocumentosIncluidos: [],
                ElementosPorRevisar: []);
        }

        // 3. Calcular rango de fechas del trimestre (traducible a SQL por EF Core)
        var mesInicio = (trimestre - 1) * 3 + 1;
        var fechaInicio = new DateOnly(ejercicio, mesInicio, 1);
        var fechaFinExclusiva = fechaInicio.AddMonths(3);

        // A. Filtrar documentos externos (tickets, facturas recibidas, etc.) en estado VALIDADO
        var documentosDelTrimestre = await context.Documentos
            .AsNoTracking()
            .Where(d =>
                d.UsuarioId == autonomoId &&
                d.Estado == EstadoDocumento.VALIDADO &&
                d.ExtraccionIa != null &&
                d.ExtraccionIa.FechaEmision.HasValue &&
                d.ExtraccionIa.FechaEmision.Value >= fechaInicio &&
                d.ExtraccionIa.FechaEmision.Value < fechaFinExclusiva)
            .Include(d => d.ExtraccionIa)
            .Include(d => d.Anomalias)
            .Include(d => d.InclusiónEnEstimación)
            .Include(d => d.Usuario)
            .ToListAsync(cancellationToken);

        // B. Filtrar facturas propias emitidas por el autónomo en estado EMITIDA
        var facturasEmitidasDelTrimestre = await context.Facturas
            .AsNoTracking()
            .Where(f =>
                f.AutonomoId == autonomoId &&
                f.Estado == EstadoFactura.EMITIDA &&
                f.Fecha >= fechaInicio &&
                f.Fecha < fechaFinExclusiva)
            .Include(f => f.Autonomo)
            .ToListAsync(cancellationToken);

        var documentosIncluidos = new List<EstimacionDocumentoResponse>();
        var elementosPorRevisar = new List<ElementoPorRevisarResponse>();

        decimal ivaIngresos = 0;
        decimal ivaGastos = 0;

        // Procesar primero las facturas propias emitidas (cuentan directamente como INGRESO)
        foreach (var factura in facturasEmitidasDelTrimestre)
        {
            if (factura.CuotaIva.HasValue)
            {
                ivaIngresos += factura.CuotaIva.Value;
                var numeroFormateado = factura.Numero.HasValue ? $"{factura.Serie}-{factura.Numero.Value:D4}" : factura.Serie;

                documentosIncluidos.Add(new EstimacionDocumentoResponse(
                    DocumentoId: factura.Id,
                    Tipo: "INGRESO",
                    Emisor: factura.EmisorNombre ?? "Emisor",
                    Nif: factura.EmisorNif ?? "",
                    NumeroFactura: numeroFormateado,
                    IvaIncluido: factura.CuotaIva.Value));
            }
        }

        // Procesar los documentos del buzón (ingresos/gastos escaneados)
        foreach (var documento in documentosDelTrimestre)
        {
            var tipo = ObtenerTipoDocumento(documento);
            var extraccion = documento.ExtraccionIa!;

            // Validar datos monetarios completos
            if (!TieneDatosMonetariosCompletos(extraccion))
            {
                elementosPorRevisar.Add(new ElementoPorRevisarResponse(
                    DocumentoId: documento.Id,
                    Motivo: "Datos monetarios incompletos (base imponible, IVA o total faltante)",
                    Severidad: "MEDIA"));
                continue;
            }

            // Validar que no haya anomalías pendientes que afecten al IVA
            var anomaliasPendientes = documento.Anomalias
                .Where(a => !a.Resuelta && (a.Campo == "CuotaIva" || a.Campo == "TipoIva" || a.Campo == "Total"))
                .ToList();

            if (anomaliasPendientes.Count > 0)
            {
                elementosPorRevisar.Add(new ElementoPorRevisarResponse(
                    DocumentoId: documento.Id,
                    Motivo: $"Anomalía pendiente: {anomaliasPendientes.First().Descripcion}",
                    Severidad: anomaliasPendientes.First().Severidad.ToString()));
                continue;
            }

            // Validar IVA coherente: TipoIva vs CuotaIva
            if (!EsIvaCoherente(extraccion))
            {
                elementosPorRevisar.Add(new ElementoPorRevisarResponse(
                    DocumentoId: documento.Id,
                    Motivo: "IVA incoherente (tipo vs cuota no coinciden)",
                    Severidad: "MEDIA"));
                continue;
            }

            if (documento.Categoria == CategoriaDocumento.INGRESO || tipo == "INGRESO")
            {
                ivaIngresos += extraccion.CuotaIva!.Value;
                documentosIncluidos.Add(new EstimacionDocumentoResponse(
                    DocumentoId: documento.Id,
                    Tipo: "INGRESO",
                    Emisor: extraccion.EmisorNombre,
                    Nif: extraccion.EmisorNif,
                    NumeroFactura: extraccion.NumeroFactura,
                    IvaIncluido: extraccion.CuotaIva.Value));
            }
            else if (documento.Categoria == CategoriaDocumento.GASTO)
            {
                var inclusion = documento.InclusiónEnEstimación;
                if (inclusion?.Incluida == true)
                {
                    ivaGastos += extraccion.CuotaIva!.Value;
                    documentosIncluidos.Add(new EstimacionDocumentoResponse(
                        DocumentoId: documento.Id,
                        Tipo: "GASTO",
                        Emisor: extraccion.EmisorNombre,
                        Nif: extraccion.EmisorNif,
                        NumeroFactura: extraccion.NumeroFactura,
                        IvaIncluido: extraccion.CuotaIva.Value));
                }
                else
                {
                    elementosPorRevisar.Add(new ElementoPorRevisarResponse(
                        DocumentoId: documento.Id,
                        Motivo: "Gasto clasificado pero no confirmado para incluir en la estimación",
                        Severidad: "BAJA"));
                }
            }
            else
            {
                elementosPorRevisar.Add(new ElementoPorRevisarResponse(
                    DocumentoId: documento.Id,
                    Motivo: "Documento pendiente de clasificación como GASTO antes de incluir en la estimación",
                    Severidad: "MEDIA"));
            }
        }

        // 4. Redondear importes a céntimos
        ivaIngresos = Math.Round(ivaIngresos, 2);
        ivaGastos = Math.Round(ivaGastos, 2);
        var diferencia = Math.Round(ivaIngresos - ivaGastos, 2);

        return new EstimacionResponse(
            Ejercicio: ejercicio,
            Trimestre: trimestre,
            Disponible: true,
            MotivoNoDisponible: null,
            IvaIngresos: ivaIngresos,
            IvaGastos: ivaGastos,
            Diferencia: diferencia,
            FechaCalculoUtc: timeProvider.GetUtcNow(),
            DocumentosIncluidos: documentosIncluidos,
            ElementosPorRevisar: elementosPorRevisar);
    }

    /// <summary>
    /// Valida si el perfil fiscal es soportado por el prototipo.
    /// </summary>
    private static bool EsPerfilSoportado(PerfilFiscal perfil)
    {
        return PerfilesFiscalesService.CatalogoIva.ContainsKey(perfil.RegimenIva) &&
               PerfilesFiscalesService.CatalogoIrpf.ContainsKey(perfil.RegimenIrpf);
    }

    /// <summary>
    /// Valida que los datos monetarios estén completos.
    /// </summary>
    private static bool TieneDatosMonetariosCompletos(ExtraccionIa extraccion)
    {
        return extraccion.BaseImponible.HasValue &&
               extraccion.TipoIva.HasValue &&
               extraccion.CuotaIva.HasValue &&
               extraccion.Total.HasValue;
    }

    /// <summary>
    /// Valida que el IVA sea coherente (tipo y cuota deben ser coherentes con la base).
    /// </summary>
    private static bool EsIvaCoherente(ExtraccionIa extraccion)
    {
        if (!extraccion.BaseImponible.HasValue || !extraccion.TipoIva.HasValue || !extraccion.CuotaIva.HasValue)
            return false;

        var esperado = Math.Round(extraccion.BaseImponible.Value * extraccion.TipoIva.Value / 100, 2);
        var diferencia = Math.Abs(esperado - extraccion.CuotaIva.Value);

        return diferencia <= 0.01m;
    }

    /// <summary>
    /// Determina si un documento es INGRESO o GASTO según el NIF del emisor.
    /// </summary>
    private static string ObtenerTipoDocumento(Documento documento)
    {
        var nifEmisor = documento.ExtraccionIa?.EmisorNif?.Trim();
        var nifPropietario = documento.Usuario.Nif.Trim();

        return !string.IsNullOrEmpty(nifEmisor) &&
               string.Equals(nifEmisor, nifPropietario, StringComparison.OrdinalIgnoreCase)
             ? "INGRESO"
             : "GASTO";
    }
}