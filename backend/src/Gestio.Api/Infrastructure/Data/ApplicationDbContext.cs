using Microsoft.EntityFrameworkCore;
using Gestio.Api.Domain.Entities;

namespace Gestio.Api.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Documento> Documentos => Set<Documento>();

    public DbSet<ExtraccionIa> ExtraccionesIa => Set<ExtraccionIa>();

    public DbSet<Anomalia> Anomalias => Set<Anomalia>();

    public DbSet<EventoDocumento> EventosDocumento => Set<EventoDocumento>();

    public DbSet<Vinculacion> Vinculaciones => Set<Vinculacion>();

    public DbSet<HistorialValidacion> HistorialValidacion => Set<HistorialValidacion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        modelBuilder.Entity<IntentoProcesamiento>(b =>
        {
            b.ToTable("intentos_procesamiento");
            b.HasKey(i => i.Id);
            b.HasOne<Documento>().WithMany(d => d.Intentos).HasForeignKey(i => i.DocumentoId);
            b.Property(i => i.ValoresJson).HasColumnType("jsonb");
            b.HasIndex(i => i.DocumentoId).IsUnique().HasFilter("\"Fin\" IS NULL");
        });
    }
}