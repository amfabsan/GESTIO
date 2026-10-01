using Gestio.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Controllers;

[ApiController]
[Route("api/documentos")]
public sealed class DocumentosController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentoResponse>>> GetDocumentos(CancellationToken cancellationToken)
    {
        var documentos = await context.Documentos
            .AsNoTracking()
            .AsSplitQuery()
            .Include(documento => documento.Usuario)
            .Include(documento => documento.ExtraccionIa)
            .Include(documento => documento.Anomalias)
            .OrderByDescending(documento => documento.FechaSubida)
            .ToListAsync(cancellationToken);

        return Ok(documentos.Select(documento => new DocumentoResponse(
            documento.Id,
            documento.NombreArchivo,
            documento.Usuario.Nombre,
            documento.FechaSubida,
            documento.Estado.ToString(),
            documento.ExtraccionIa is null
                ? null
                : new ExtraccionResponse(
                    documento.ExtraccionIa.EmisorNombre,
                    documento.ExtraccionIa.EmisorNif,
                    documento.ExtraccionIa.FechaEmision,
                    documento.ExtraccionIa.BaseImponible,
                    documento.ExtraccionIa.TipoIva,
                    documento.ExtraccionIa.CuotaIva,
                    documento.ExtraccionIa.Total,
                    documento.ExtraccionIa.CategoriaSugerida,
                    documento.ExtraccionIa.NivelConfianza?.ToString(),
                    documento.ExtraccionIa.DatosCrudosJson),
            documento.Anomalias
                .OrderByDescending(anomalia => anomalia.FechaDeteccion)
                .Select(anomalia => new AnomaliaResponse(
                    anomalia.Tipo.ToString(),
                    anomalia.Descripcion,
                    anomalia.Severidad.ToString(),
                    anomalia.Resuelta,
                    anomalia.FechaDeteccion))
                .ToList())).ToList());
    }
}

public sealed record DocumentoResponse(
    Guid Id,
    string NombreArchivo,
    string Usuario,
    DateTimeOffset FechaSubida,
    string Estado,
    ExtraccionResponse? Extraccion,
    IReadOnlyList<AnomaliaResponse> Anomalias);

public sealed record ExtraccionResponse(
    string? EmisorNombre,
    string? EmisorNif,
    DateOnly? FechaEmision,
    decimal? BaseImponible,
    decimal? TipoIva,
    decimal? CuotaIva,
    decimal? Total,
    string? CategoriaSugerida,
    string? NivelConfianza,
    string? DatosCrudosJson);

public sealed record AnomaliaResponse(
    string Tipo,
    string Descripcion,
    string Severidad,
    bool Resuelta,
    DateTimeOffset FechaDeteccion);
