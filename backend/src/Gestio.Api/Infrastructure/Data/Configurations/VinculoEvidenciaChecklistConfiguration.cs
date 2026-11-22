using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class VinculoEvidenciaChecklistConfiguration : IEntityTypeConfiguration<VinculoEvidenciaChecklist>
{
    public void Configure(EntityTypeBuilder<VinculoEvidenciaChecklist> builder)
    {
        builder.ToTable("vinculos_evidencia_checklist");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(v => v.FechaPropuesta).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(v => v.FechaConfirmacion).HasColumnType("timestamp with time zone");
        builder.HasOne(v => v.Documento).WithMany(d => d.VinculosChecklist)
            .HasForeignKey(v => v.DocumentoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.ElementoChecklistObligacion).WithMany(e => e.VinculosEvidencia)
            .HasForeignKey(v => v.ElementoChecklistObligacionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(v => v.ConfirmadaPor).WithMany().HasForeignKey(v => v.ConfirmadaPorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(v => new { v.DocumentoId, v.ElementoChecklistObligacionId }).IsUnique();
        builder.HasMany(v => v.Historial).WithOne(h => h.Vinculo)
            .HasForeignKey(h => h.VinculoEvidenciaChecklistId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class HistorialVinculoEvidenciaConfiguration : IEntityTypeConfiguration<HistorialVinculoEvidencia>
{
    public void Configure(EntityTypeBuilder<HistorialVinculoEvidencia> builder)
    {
        builder.ToTable("historial_vinculos_evidencia_checklist");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.EstadoAnterior).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.EstadoNuevo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(h => h.Fecha).HasColumnType("timestamp with time zone").IsRequired();
        builder.HasOne(h => h.Usuario).WithMany().HasForeignKey(h => h.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(h => new { h.VinculoEvidenciaChecklistId, h.Fecha });
    }
}
