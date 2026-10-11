using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class EventoDocumentoConfiguration : IEntityTypeConfiguration<EventoDocumento>
{
    public void Configure(EntityTypeBuilder<EventoDocumento> builder)
    {
        builder.ToTable("eventos_documento");

        builder.HasKey(evento => evento.Id);

        builder.Property(evento => evento.Tipo)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(evento => evento.Fecha)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(evento => evento.Detalle)
            .HasMaxLength(500);

        builder.HasOne(evento => evento.Documento)
            .WithMany(documento => documento.Eventos)
            .HasForeignKey(evento => evento.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(evento => evento.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(evento => new { evento.DocumentoId, evento.Fecha });
    }
}