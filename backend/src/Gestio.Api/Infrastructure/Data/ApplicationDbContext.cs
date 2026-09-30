using Microsoft.EntityFrameworkCore;
using Gestio.Api.Domain.Entities;

namespace Gestio.Api.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Documento> Documentos => Set<Documento>();

    public DbSet<ExtraccionIa> ExtraccionesIa => Set<ExtraccionIa>();

    public DbSet<Anomalia> Anomalias => Set<Anomalia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
