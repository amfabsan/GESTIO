using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;

namespace Gestio.Api.Services;

// Única definición de qué documentos puede consultar un usuario: los suyos y, si es gestor,
// los de autónomos vinculados a su asesoría en estado ACTIVA (PB-04).
public sealed class AccesoDocumentos(ApplicationDbContext context)
{
    public IQueryable<Documento> ConsultablesPor(UsuarioActual usuario) =>
        usuario.Rol == Rol.GESTOR
            ? context.Documentos.Where(documento =>
                documento.UsuarioId == usuario.Id ||
                context.Vinculaciones.Any(vinculacion =>
                    vinculacion.AutonomoId == documento.UsuarioId &&
                    vinculacion.CodigoAsesoria == context.Usuarios
                        .Where(administrador => administrador.Id == usuario.Id)
                        .Select(administrador => administrador.CodigoAsesoria)
                        .FirstOrDefault() &&
                    vinculacion.Estado == EstadoVinculacion.ACTIVA))
            : context.Documentos.Where(documento => documento.UsuarioId == usuario.Id);
}