using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class DocumentoConfiguration : IEntityTypeConfiguration<Documento>
{
    public void Configure(EntityTypeBuilder<Documento> builder)
    {
        builder.ToTable("documentos");

        builder.HasKey(documento => documento.Id);

        builder.Property(documento => documento.NombreArchivo)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(documento => documento.HashSha256)
            .HasMaxLength(64);

        builder.Property(documento => documento.FechaSubida)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(documento => documento.Estado)
            .IsRequired();

        builder.HasOne(documento => documento.Usuario)
            .WithMany(usuario => usuario.Documentos)
            .HasForeignKey(documento => documento.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(documento => new { documento.UsuarioId, documento.FechaSubida })
            .IsDescending(false, true);

        builder.HasIndex(documento => new { documento.UsuarioId, documento.HashSha256 })
            .IsUnique();

        builder.HasIndex(documento => documento.Estado);
    }
}