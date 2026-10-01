using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class AnomaliaConfiguration : IEntityTypeConfiguration<Anomalia>
{
    public void Configure(EntityTypeBuilder<Anomalia> builder)
    {
        builder.ToTable("anomalias");

        builder.HasKey(anomalia => anomalia.Id);

        builder.Property(anomalia => anomalia.Tipo)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(anomalia => anomalia.Descripcion)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(anomalia => anomalia.Severidad)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(anomalia => anomalia.Resuelta)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(anomalia => anomalia.FechaDeteccion)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne(anomalia => anomalia.Documento)
            .WithMany(documento => documento.Anomalias)
            .HasForeignKey(anomalia => anomalia.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(anomalia => anomalia.DocumentoId);
    }
}