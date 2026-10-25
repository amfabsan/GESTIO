using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class HistorialCategoriaConfiguration : IEntityTypeConfiguration<HistorialCategoria>
{
    public void Configure(EntityTypeBuilder<HistorialCategoria> builder)
    {
        builder.ToTable("historial_categorias");

        builder.HasKey(historial => historial.Id);

        builder.Property(historial => historial.CategoriaAnterior)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(historial => historial.CategoriaNueva)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(historial => historial.FechaCambio)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne(historial => historial.Documento)
            .WithMany(documento => documento.HistorialCategorias)
            .HasForeignKey(historial => historial.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(historial => historial.Usuario)
            .WithMany()
            .HasForeignKey(historial => historial.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(historial => new { historial.DocumentoId, historial.FechaCambio });
    }
}
