using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class HistorialValidacionConfiguration : IEntityTypeConfiguration<HistorialValidacion>
{
    public void Configure(EntityTypeBuilder<HistorialValidacion> builder)
    {
        builder.ToTable("historial_validacion");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Campo).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TipoCambio).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ValorAnterior).HasColumnType("text");
        builder.Property(x => x.ValorNuevo).HasColumnType("text");

        builder.HasOne(x => x.Documento)
            .WithMany(d => d.HistorialValidacion)
            .HasForeignKey(x => x.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}