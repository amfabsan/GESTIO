using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class DocumentoConfiguration : IEntityTypeConfiguration<Documento>
{
    public const string IndiceReferenciaMensaje = "ux_documentos_referencia_mensaje";
    public const string RestriccionReferenciaSoloWhatsapp = "ck_documentos_referencia_solo_whatsapp";

    public void Configure(EntityTypeBuilder<Documento> builder)
    {
        // UT27: un documento de WhatsApp siempre lleva la referencia del mensaje, y uno de la web nunca.
        builder.ToTable("documentos", t => t.HasCheckConstraint(RestriccionReferenciaSoloWhatsapp,
            "(\"Origen\" = 'WHATSAPP') = (\"ReferenciaMensaje\" IS NOT NULL)"));

        builder.HasKey(documento => documento.Id);

        builder.Property(documento => documento.NombreArchivo)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(documento => documento.TipoMime)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(documento => documento.TamanoBytes)
            .IsRequired();

        builder.Property(documento => documento.Origen)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(documento => documento.StorageKey)
            .HasMaxLength(500);

        builder.Property(documento => documento.HashSha256)
            .HasMaxLength(64);

        builder.Property(documento => documento.FechaSubida)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(documento => documento.Estado)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasOne(documento => documento.Usuario)
            .WithMany(usuario => usuario.Documentos)
            .HasForeignKey(documento => documento.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(documento => new { documento.UsuarioId, documento.FechaSubida })
            .IsDescending(false, true);

        // Sin IsUnique: PB-05 no bloquea repetidos; la detección de duplicados es de PB-10.
        builder.HasIndex(documento => new { documento.UsuarioId, documento.HashSha256 });

        builder.HasIndex(documento => documento.Estado);

        // UT27 (requisito 5, PA 3603): un mensaje del proveedor da como mucho un documento.
        builder.Property(documento => documento.ReferenciaMensaje)
            .HasMaxLength(128);

        builder.HasIndex(documento => documento.ReferenciaMensaje)
            .IsUnique()
            .HasFilter("\"ReferenciaMensaje\" IS NOT NULL")
            .HasDatabaseName(IndiceReferenciaMensaje);
    }
}
