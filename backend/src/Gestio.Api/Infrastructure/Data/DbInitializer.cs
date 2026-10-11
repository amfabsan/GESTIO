using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Gestio.Api.Infrastructure.Data;

public static class DbInitializer
{
    public static readonly Guid RamonId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid PedroId = new("22222222-2222-2222-2222-222222222222");

    private static readonly Guid FacturaLuzId = new("33333333-3333-3333-3333-333333333331");
    private static readonly Guid FacturaInternetId = new("33333333-3333-3333-3333-333333333332");
    private static readonly Guid TicketCombustibleId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid DeclaracionIvaId = new("33333333-3333-3333-3333-333333333334");
    private static readonly Guid FacturaProveedorId = new("33333333-3333-3333-3333-333333333335");

    private static readonly Guid ExtraccionFacturaLuzId = new("44444444-4444-4444-4444-444444444441");
    private static readonly Guid ExtraccionProveedorId = new("44444444-4444-4444-4444-444444444442");

    private static readonly Guid AnomaliaCombustibleId = new("55555555-5555-5555-5555-555555555551");
    private static readonly Guid AnomaliaProveedorId = new("55555555-5555-5555-5555-555555555552");
    private static readonly Guid AnomaliaDeclaracionId = new("55555555-5555-5555-5555-555555555553");

    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var usuarios = new List<Usuario>();

        if (!await context.Usuarios.AnyAsync(usuario => usuario.Id == RamonId, cancellationToken))
        {
            usuarios.Add(new Usuario
            {
                Id = RamonId,
                Nombre = "Ramón",
                PrimerApellido = "García",
                SegundoApellido = "López",
                Email = "ramon@gestio.local",
                Nif = "12345678Z",
                GestorId = PedroId,
                Rol = Rol.AUTONOMO
            });
        }

        if (!await context.Usuarios.AnyAsync(usuario => usuario.Id == PedroId, cancellationToken))
        {
            usuarios.Add(new Usuario
            {
                Id = PedroId,
                Nombre = "Pedro",
                PrimerApellido = "Sánchez",
                SegundoApellido = "Martín",
                Email = "pedro@gestio.local",
                Nif = "87654321X",
                CodigoAsesoria = "ASE-000123",
                Rol = Rol.GESTOR
            });
        }

        if (usuarios.Count > 0)
        {
            await context.Usuarios.AddRangeAsync(usuarios, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        var documentos = new[]
        {
            new Documento
            {

                Id = FacturaLuzId,
                UsuarioId = RamonId,
                NombreArchivo = "factura-luz-enero.pdf",
                TipoMime = "application/pdf",
                Origen = OrigenDocumento.SUBIDA_WEB,
                FechaSubida = new DateTimeOffset(2026, 1, 10, 9, 30, 0, TimeSpan.Zero),
                Estado = EstadoDocumento.VALIDADO

            },

            new Documento
            {
                
                Id = FacturaInternetId,
                UsuarioId = RamonId,
                NombreArchivo = "factura-internet-enero.pdf",
                TipoMime = "application/pdf",
                Origen = OrigenDocumento.SUBIDA_WEB,
                FechaSubida = new DateTimeOffset(2026, 1, 12, 11, 15, 0, TimeSpan.Zero),
                Estado = EstadoDocumento.SUBIDO

            },

            new Documento
            {

                Id = TicketCombustibleId,
                UsuarioId = RamonId,
                NombreArchivo = "ticket-combustible.pdf",
                TipoMime = "application/pdf",
                Origen = OrigenDocumento.SUBIDA_WEB,
                FechaSubida = new DateTimeOffset(2026, 1, 15, 16, 45, 0, TimeSpan.Zero),
                Estado = EstadoDocumento.ERROR

            },

            new Documento
            {

                Id = DeclaracionIvaId,
                UsuarioId = PedroId,
                NombreArchivo = "declaracion-iva-q1.pdf",
                TipoMime = "application/pdf",
                Origen = OrigenDocumento.SUBIDA_WEB,
                FechaSubida = new DateTimeOffset(2026, 1, 18, 8, 0, 0, TimeSpan.Zero),
                Estado = EstadoDocumento.PROCESANDO

            },

            new Documento
            {

                Id = FacturaProveedorId,
                UsuarioId = PedroId,
                NombreArchivo = "factura-proveedor-demo.pdf",
                TipoMime = "application/pdf",
                Origen = OrigenDocumento.SUBIDA_WEB,
                FechaSubida = new DateTimeOffset(2026, 1, 20, 10, 20, 0, TimeSpan.Zero),
                Estado = EstadoDocumento.VALIDADO
                
            }

        };

        var documentosExistentes = await context.Documentos
            .Where(documento => documentos.Select(seed => seed.Id).Contains(documento.Id))
            .Select(documento => documento.Id)
            .ToListAsync(cancellationToken);

        var documentosNuevos = documentos
            .Where(documento => !documentosExistentes.Contains(documento.Id))
            .ToArray();

        if (documentosNuevos.Length > 0)
        {
            await context.Documentos.AddRangeAsync(documentosNuevos, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        var extracciones = new[]
        {
            new ExtraccionIa
            {
                Id = ExtraccionFacturaLuzId,
                DocumentoId = FacturaLuzId,
                EmisorNombre = "Compañía Eléctrica Demo",
                EmisorNif = "B12345678",
                FechaEmision = new DateOnly(2026, 1, 9),
                BaseImponible = 50.00m,
                TipoIva = 21.00m,
                CuotaIva = 10.50m,
                Total = 60.50m,
                CategoriaSugerida = "Suministros",
                NivelConfianza = NivelConfianza.ALTO,
                DatosCrudosJson = "{\"origen\":\"demo\",\"modelo\":\"gestio-ia-v1\"}"
            },
            new ExtraccionIa
            {
                Id = ExtraccionProveedorId,
                DocumentoId = FacturaProveedorId,
                EmisorNombre = "Proveedor Demo",
                EmisorNif = "B12345678",
                FechaEmision = new DateOnly(2026, 1, 19),
                BaseImponible = 100.00m,
                TipoIva = 21.00m,
                CuotaIva = 21.00m,
                Total = 121.00m,
                CategoriaSugerida = "Servicios profesionales",
                NivelConfianza = NivelConfianza.MEDIO,
                DatosCrudosJson = "{\"origen\":\"demo\",\"modelo\":\"gestio-ia-v1\",\"revisionManual\":true}"
            }
        };

        var extraccionesExistentes = await context.ExtraccionesIa
            .Where(extraccion => extracciones.Select(seed => seed.Id).Contains(extraccion.Id))
            .Select(extraccion => extraccion.Id)
            .ToListAsync(cancellationToken);

        var extraccionesNuevas = extracciones
            .Where(extraccion => !extraccionesExistentes.Contains(extraccion.Id))
            .ToArray();

        if (extraccionesNuevas.Length > 0)
        {
            await context.ExtraccionesIa.AddRangeAsync(extraccionesNuevas, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        var anomalias = new[]
        {
            new Anomalia
            {
                Id = AnomaliaCombustibleId,
                DocumentoId = TicketCombustibleId,
                Tipo = TipoAnomalia.NIF_AUSENTE,
                Descripcion = "No se ha podido identificar un NIF válido en el documento.",
                Severidad = SeveridadAnomalia.BLOQUEANTE,
                Resuelta = false,
                FechaDeteccion = new DateTimeOffset(2026, 1, 15, 16, 50, 0, TimeSpan.Zero)
            },
            new Anomalia
            {
                Id = AnomaliaProveedorId,
                DocumentoId = FacturaProveedorId,
                Tipo = TipoAnomalia.DESCUADRE_ARITMETICO,
                Descripcion = "El total detectado requiere revisión manual.",
                Severidad = SeveridadAnomalia.ADVERTENCIA,
                Resuelta = false,
                FechaDeteccion = new DateTimeOffset(2026, 1, 20, 10, 25, 0, TimeSpan.Zero)
            },
            new Anomalia
            {
                Id = AnomaliaDeclaracionId,
                DocumentoId = DeclaracionIvaId,
                Tipo = TipoAnomalia.BAJA_CONFIANZA,
                Descripcion = "La fecha de emisión no estaba presente en el documento.",
                Severidad = SeveridadAnomalia.ADVERTENCIA,
                Resuelta = true,
                FechaDeteccion = new DateTimeOffset(2026, 1, 18, 8, 5, 0, TimeSpan.Zero)
            }
        };

        var anomaliasExistentes = await context.Anomalias
            .Where(anomalia => anomalias.Select(seed => seed.Id).Contains(anomalia.Id))
            .Select(anomalia => anomalia.Id)
            .ToListAsync(cancellationToken);

        var anomaliasNuevas = anomalias
            .Where(anomalia => !anomaliasExistentes.Contains(anomalia.Id))
            .ToArray();

        if (anomaliasNuevas.Length > 0)
        {
            await context.Anomalias.AddRangeAsync(anomaliasNuevas, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
