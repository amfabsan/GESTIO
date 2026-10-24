using Gestio.Api.Common;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Services;

public sealed record ActorEvento(Guid Id, string Nombre, string PrimerApellido, string SegundoApellido);

public sealed record CambioCampo(string Campo, string? ValorAnterior, string? ValorNuevo);

public sealed record EventoHistorial(Guid Id, TipoEventoDocumento Tipo, DateTimeOffset Fecha, string? Detalle, ActorEvento? Actor, CambioCampo? Cambio);

// Consulta de solo lectura de la línea temporal de un documento.
public sealed class HistorialDocumentoService(ApplicationDbContext context, AccesoDocumentos acceso)
{
    public const int TamanoMaximoPagina = 100;

    public async Task<Resultado<Pagina<EventoHistorial>>> ObtenerAsync(
        UsuarioActual usuario,
        Guid documentoId,
        int numeroPagina,
        int tamanoPagina,
        CancellationToken cancellationToken)
    {
        if (numeroPagina < 1 || tamanoPagina is < 1 or > TamanoMaximoPagina)
            return DocumentoErrores.PaginacionNoValida(TamanoMaximoPagina);

        // Sin acceso se responde igual que si no existiera, para no revelar documentos ajenos.
        var accesible = await acceso.ConsultablesPor(usuario).AnyAsync(documento => documento.Id == documentoId, cancellationToken);
        if (!accesible) return DocumentoErrores.NoEncontrado;

        var eventos = context.EventosDocumento.AsNoTracking().Where(evento => evento.DocumentoId == documentoId);
        var verValores = PuedeVerValoresCorregidos(usuario);

        var total = await eventos.CountAsync(cancellationToken);
        var elementos = await eventos
            .OrderBy(evento => evento.Fecha)
            .ThenBy(evento => evento.Id)
            .Skip((numeroPagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(evento => new EventoHistorial(
                evento.Id,
                evento.Tipo,
                evento.Fecha,
                evento.Detalle,
                evento.Usuario == null
                    ? null
                    : new ActorEvento(evento.Usuario.Id, evento.Usuario.Nombre, evento.Usuario.PrimerApellido, evento.Usuario.SegundoApellido),
                verValores && evento.Campo != null
                    ? new CambioCampo(evento.Campo, evento.ValorAnterior, evento.ValorNuevo)
                    : null))
            .ToListAsync(cancellationToken);

        return new Pagina<EventoHistorial>(elementos, numeroPagina, tamanoPagina, total);
    }

    // Lista blanca: un rol nuevo no verá los valores de una corrección hasta que se decida explícitamente.
    private static bool PuedeVerValoresCorregidos(UsuarioActual usuario) =>
        usuario.Rol is Rol.AUTONOMO or Rol.GESTOR;
}
