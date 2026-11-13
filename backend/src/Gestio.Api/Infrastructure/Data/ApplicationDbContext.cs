using Microsoft.EntityFrameworkCore;
using Gestio.Api.Domain.Entities;

namespace Gestio.Api.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Contacto> Contactos => Set<Contacto>();

    public DbSet<Factura> Facturas => Set<Factura>();

    public DbSet<LineaFactura> LineasFactura => Set<LineaFactura>();

    public DbSet<Documento> Documentos => Set<Documento>();

    public DbSet<ExtraccionIa> ExtraccionesIa => Set<ExtraccionIa>();

    public DbSet<Anomalia> Anomalias => Set<Anomalia>();

    public DbSet<EventoDocumento> EventosDocumento => Set<EventoDocumento>();

    public DbSet<Vinculacion> Vinculaciones => Set<Vinculacion>();

    public DbSet<HistorialValidacion> HistorialValidacion => Set<HistorialValidacion>();

    public DbSet<HistorialCategoria> HistorialCategorias => Set<HistorialCategoria>();

    public DbSet<RegistroFacturacion> RegistrosFacturacion => Set<RegistroFacturacion>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ImpedirCambiosEnRegistrosFacturacion();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ImpedirCambiosEnRegistrosFacturacion();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Req. 7 / PA 1598: los registros de facturación son inmutables desde la aplicación.
    // En PostgreSQL lo refuerza además un trigger (migración RegistrosFacturacionUT22), que también cubre
    // ExecuteUpdate/ExecuteDelete y SQL directo, que no pasan por SaveChanges.
    private void ImpedirCambiosEnRegistrosFacturacion()
    {
        var alterado = ChangeTracker.Entries<RegistroFacturacion>()
            .FirstOrDefault(e => e.State is EntityState.Modified or EntityState.Deleted);
        if (alterado is not null)
            throw new InvalidOperationException(
                $"Los registros de facturación no se pueden {(alterado.State == EntityState.Deleted ? "eliminar" : "modificar")} una vez creados.");
    }

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

        modelBuilder.Entity<Anomalia>(b =>
        {
            b.HasOne<Documento>()
                            .WithMany()
                            .HasForeignKey(a => a.DocumentoRelacionadoId)
                            .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(a => a.DocumentoRelacionadoId);
        });
    }
}
