using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class MensajeDocumentoConfiguration : IEntityTypeConfiguration<MensajeDocumento>
{
    public void Configure(EntityTypeBuilder<MensajeDocumento> builder)
    {
        builder.ToTable("mensajes_documento");
        builder.HasKey(mensaje => mensaje.Id);
        builder.Property(mensaje => mensaje.Contenido).HasMaxLength(1000).IsRequired();
        builder.HasIndex(mensaje => new { mensaje.DocumentoId, mensaje.Fecha, mensaje.Id });
        builder.HasOne<Documento>().WithMany().HasForeignKey(mensaje => mensaje.DocumentoId);
        builder.HasOne(mensaje => mensaje.Autor).WithMany().HasForeignKey(mensaje => mensaje.AutorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AvisoMensajeDocumentoConfiguration : IEntityTypeConfiguration<AvisoMensajeDocumento>
{
    public void Configure(EntityTypeBuilder<AvisoMensajeDocumento> builder)
    {
        builder.ToTable("avisos_mensaje_documento");
        builder.HasKey(aviso => new { aviso.MensajeId, aviso.DestinatarioId });
        builder.HasIndex(aviso => new { aviso.DestinatarioId, aviso.FechaLectura });
        builder.HasOne(aviso => aviso.Mensaje).WithMany(mensaje => mensaje.Avisos).HasForeignKey(aviso => aviso.MensajeId);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(aviso => aviso.DestinatarioId);
    }
}
