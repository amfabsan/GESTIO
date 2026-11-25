using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class MensajeWhatsappRecibidoConfiguration : IEntityTypeConfiguration<MensajeWhatsappRecibido>
{
    public void Configure(EntityTypeBuilder<MensajeWhatsappRecibido> builder)
    {
        builder.ToTable("mensajes_whatsapp");

        // La clave primaria es el id del proveedor: reclamar un mensaje es insertarlo (PA 3603).
        builder.HasKey(mensaje => mensaje.MensajeId);
        builder.Property(mensaje => mensaje.MensajeId).HasMaxLength(128);

        builder.Property(mensaje => mensaje.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(mensaje => mensaje.Resultado).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(mensaje => mensaje.Motivo).HasMaxLength(60);
        builder.Property(mensaje => mensaje.MediaId).HasMaxLength(128);
        builder.Property(mensaje => mensaje.MimeType).HasMaxLength(100);
        builder.Property(mensaje => mensaje.NombreArchivo).HasMaxLength(255);
        builder.Property(mensaje => mensaje.FechaRecepcion).HasColumnType("timestamp with time zone");
        builder.Property(mensaje => mensaje.FechaActualizacion).HasColumnType("timestamp with time zone");

        builder.HasOne(mensaje => mensaje.Usuario)
            .WithMany()
            .HasForeignKey(mensaje => mensaje.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(mensaje => mensaje.Documento)
            .WithMany()
            .HasForeignKey(mensaje => mensaje.DocumentoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Al arrancar se buscan los EN_CURSO para volver a encolarlos.
        builder.HasIndex(mensaje => mensaje.Resultado);
    }
}
