using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Services;

/// <summary>
/// Autoriza y persiste la marcación de un gasto para incluir en la estimación de IVA.
/// Solo gestores autorizados pueden cambiar la inclusión.
/// </summary>
public sealed class InclusionGastoService(ApplicationDbContext context, TimeProvider timeProvider)
{
    private static readonly Error Prohibido = new(
        "InclusionGasto.Prohibido",
        "No tienes permiso para cambiar la inclusión de este gasto.",
        TipoError.PROHIBIDO);

    private static readonly Error NoEncontrado = new(
        "InclusionGasto.DocumentoNoEncontrado",
        "No se ha encontrado el documento.",
        TipoError.NO_ENCONTRADO);

    private static readonly Error NoEsGasto = new(
        "InclusionGasto.NoEsGasto",
        "Solo se puede incluir/excluir un documento clasificado como GASTO.",
        TipoError.VALIDACION);

    private static readonly Error NoEsValidado = new(
        "InclusionGasto.NoEsValidado",
        "Solo se puede marcar un documento en estado VALIDADO.",
        TipoError.VALIDACION);

    /// <summary>
    /// Actualiza la inclusión de un gasto en la estimación de IVA.
    /// </summary>
    public async Task<Resultado<Documento>> ActualizarInclusiónAsync(
        UsuarioActual usuario,
        Guid documentoId,
        bool incluida,
        CancellationToken cancellationToken)
    {
        // Solo gestores pueden cambiar la inclusión
        if (usuario.Rol != Rol.GESTOR)
            return Prohibido;

        // Cargar el documento
        var documento = await context.Documentos
            .Include(d => d.Usuario)
            .Include(d => d.ExtraccionIa)
            .Include(d => d.Anomalias)
            .Include(d => d.InclusiónEnEstimación)
            .FirstOrDefaultAsync(d => d.Id == documentoId, cancellationToken);

        if (documento is null)
            return NoEncontrado;

        // Validar que el documento es de un autónomo vinculado al gestor
        var puedeAcceder = await context.Vinculaciones.AnyAsync(v =>
            v.AutonomoId == documento.UsuarioId &&
            v.Estado == EstadoVinculacion.ACTIVA &&
            context.Usuarios.Any(u =>
                u.Id == usuario.Id &&
                u.Rol == Rol.GESTOR &&
                u.CodigoAsesoria != null &&
                u.CodigoAsesoria == v.CodigoAsesoria),
            cancellationToken);

        if (!puedeAcceder)
            return Prohibido;

        // Validar que es GASTO
        if (documento.Categoria != CategoriaDocumento.GASTO)
            return NoEsGasto;

        // Validar que es VALIDADO
        if (documento.Estado != EstadoDocumento.VALIDADO)
            return NoEsValidado;

        var ahora = timeProvider.GetUtcNow();

        // Crear o actualizar la inclusión
        if (documento.InclusiónEnEstimación is null)
        {
            documento.InclusiónEnEstimación = new InclusiónGastoEnEstimación
            {
                Id = Guid.NewGuid(),
                DocumentoId = documentoId,
                Incluida = incluida,
                FechaCambio = ahora,
                ActualizadoPorId = usuario.Id
            };
            context.Add(documento.InclusiónEnEstimación);
        }
        else
        {
            documento.InclusiónEnEstimación.Incluida = incluida;
            documento.InclusiónEnEstimación.FechaCambio = ahora;
            documento.InclusiónEnEstimación.ActualizadoPorId = usuario.Id;
        }

        await context.SaveChangesAsync(cancellationToken);

        // Recargar el documento completo para retornarlo
        var documentoActualizado = await context.Documentos
            .Include(d => d.Usuario)
            .Include(d => d.ExtraccionIa)
            .Include(d => d.Anomalias)
            .Include(d => d.InclusiónEnEstimación)
            .Include(d => d.Eventos)
            .Include(d => d.HistorialValidacion)
            .Include(d => d.HistorialCategorias)
            .FirstAsync(d => d.Id == documentoId, cancellationToken);

        return documentoActualizado;
    }
}
