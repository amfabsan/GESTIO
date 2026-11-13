using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class ContactoConfiguration : IEntityTypeConfiguration<Contacto>
{
    public void Configure(EntityTypeBuilder<Contacto> builder)
    {
        builder.ToTable("contactos");

        builder.HasKey(contacto => contacto.Id);

        builder.Property(contacto => contacto.Clasificacion)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(contacto => contacto.Nombre)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(contacto => contacto.Nif)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(contacto => contacto.Email)
            .HasMaxLength(320);

        builder.Property(contacto => contacto.Telefono)
            .HasMaxLength(50);

        builder.Property(contacto => contacto.Direccion)
            .HasMaxLength(500);

        builder.Property(contacto => contacto.Activo)
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasOne(contacto => contacto.Autonomo)
            .WithMany(usuario => usuario.Contactos)
            .HasForeignKey(contacto => contacto.AutonomoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(contacto => new { contacto.AutonomoId, contacto.Nif })
            .IsUnique();

        builder.HasIndex(contacto => new { contacto.AutonomoId, contacto.Nombre });
    }
}
