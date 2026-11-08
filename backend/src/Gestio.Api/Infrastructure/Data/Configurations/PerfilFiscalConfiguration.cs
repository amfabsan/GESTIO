using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class PerfilFiscalConfiguration : IEntityTypeConfiguration<PerfilFiscal>
{
    public void Configure(EntityTypeBuilder<PerfilFiscal> builder)
    {
        builder.ToTable("perfiles_fiscales");
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => new { p.AutonomoId, p.Ejercicio }).IsUnique();
        builder.HasOne<Usuario>().WithMany().HasForeignKey(p => p.AutonomoId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(p => p.RegimenIva).HasMaxLength(40).IsRequired();
        builder.Property(p => p.RegimenIrpf).HasMaxLength(40).IsRequired();
        builder.HasMany(p => p.Historial).WithOne().HasForeignKey(h => h.PerfilFiscalId);
    }
}

public sealed class HistorialPerfilFiscalConfiguration : IEntityTypeConfiguration<HistorialPerfilFiscal>
{
    public void Configure(EntityTypeBuilder<HistorialPerfilFiscal> builder)
    {
        builder.ToTable("historial_perfiles_fiscales");
        builder.HasKey(h => h.Id);
        builder.HasOne(h => h.Usuario).WithMany().HasForeignKey(h => h.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(h => h.Campo).HasMaxLength(50).IsRequired();
        builder.Property(h => h.ValorAnterior).HasMaxLength(40);
        builder.Property(h => h.ValorNuevo).HasMaxLength(40).IsRequired();
        builder.HasIndex(h => new { h.PerfilFiscalId, h.FechaCambio });
    }
}
