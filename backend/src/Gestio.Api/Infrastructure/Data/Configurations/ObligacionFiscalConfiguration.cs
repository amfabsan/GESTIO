using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class ObligacionFiscalConfiguration : IEntityTypeConfiguration<ObligacionFiscal>
{
    public void Configure(EntityTypeBuilder<ObligacionFiscal> builder)
    {
        builder.ToTable("obligaciones_fiscales");
        builder.HasKey(o => o.Id);
        builder.HasIndex(o => new { o.AutonomoId, o.Modelo, o.Ejercicio, o.Periodo }).IsUnique();
        builder.HasOne<Usuario>().WithMany().HasForeignKey(o => o.AutonomoId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(o => o.Modelo).HasMaxLength(3).IsRequired();
        builder.Property(o => o.Nombre).HasMaxLength(120).IsRequired();
        builder.Property(o => o.Periodo).HasMaxLength(2).IsRequired();
        builder.HasMany(o => o.Historial).WithOne().HasForeignKey(h => h.ObligacionFiscalId);
    }
}

public sealed class HistorialObligacionFiscalConfiguration : IEntityTypeConfiguration<HistorialObligacionFiscal>
{
    public void Configure(EntityTypeBuilder<HistorialObligacionFiscal> builder)
    {
        builder.ToTable("historial_obligaciones_fiscales");
        builder.HasKey(h => h.Id);
        builder.HasOne(h => h.Usuario).WithMany().HasForeignKey(h => h.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(h => h.Descripcion).HasMaxLength(500).IsRequired();
        builder.HasIndex(h => new { h.ObligacionFiscalId, h.Fecha });
    }
}
