using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class ElementoChecklistObligacionConfiguration : IEntityTypeConfiguration<ElementoChecklistObligacion>
{
    public void Configure(EntityTypeBuilder<ElementoChecklistObligacion> builder)
    {
        builder.ToTable("elementos_checklist_obligacion");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.ObligacionFiscalId, e.CodigoPlantilla }).IsUnique();
        builder.HasOne(e => e.ObligacionFiscal).WithMany(o => o.Checklist)
            .HasForeignKey(e => e.ObligacionFiscalId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.UsuarioUltimoCambio).WithMany()
            .HasForeignKey(e => e.UsuarioUltimoCambioId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.CodigoPlantilla).HasMaxLength(80).IsRequired();
        builder.Property(e => e.Descripcion).HasMaxLength(200).IsRequired();
        builder.HasMany(e => e.Historial).WithOne(h => h.Elemento)
            .HasForeignKey(h => h.ElementoChecklistObligacionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class HistorialElementoChecklistConfiguration : IEntityTypeConfiguration<HistorialElementoChecklist>
{
    public void Configure(EntityTypeBuilder<HistorialElementoChecklist> builder)
    {
        builder.ToTable("historial_elementos_checklist");
        builder.HasKey(h => h.Id);
        builder.HasOne(h => h.Usuario).WithMany().HasForeignKey(h => h.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(h => h.Origen).IsRequired();
        builder.HasIndex(h => new { h.ElementoChecklistObligacionId, h.Fecha });
    }
}
