using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class ExtraccionIaConfiguration : IEntityTypeConfiguration<ExtraccionIa>
{
    public void Configure(EntityTypeBuilder<ExtraccionIa> builder)
    {
        builder.ToTable("extracciones_ia");

        builder.HasKey(extraccion => extraccion.Id);

        builder.Property(extraccion => extraccion.EmisorNombre)
            .HasMaxLength(200);

        builder.Property(extraccion => extraccion.EmisorNif)
            .HasColumnType("varchar(9)");

        builder.Property(extraccion => extraccion.FechaEmision)
            .HasColumnType("date");

        builder.Property(extraccion => extraccion.BaseImponible)
            .HasColumnType("numeric(18,2)");

        builder.Property(extraccion => extraccion.TipoIva)
            .HasColumnType("numeric(5,2)");

        builder.Property(extraccion => extraccion.CuotaIva)
            .HasColumnType("numeric(18,2)");

        builder.Property(extraccion => extraccion.Total)
            .HasColumnType("numeric(18,2)");

        builder.Property(extraccion => extraccion.CategoriaSugerida)
            .HasMaxLength(100);

        builder.Property(extraccion => extraccion.NivelConfianza)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(extraccion => extraccion.DatosCrudosJson)
            .HasColumnType("jsonb");

        builder.HasOne(extraccion => extraccion.Documento)
            .WithOne(documento => documento.ExtraccionIa)
            .HasForeignKey<ExtraccionIa>(extraccion => extraccion.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(extraccion => extraccion.DocumentoId)
            .IsUnique();
    }
}