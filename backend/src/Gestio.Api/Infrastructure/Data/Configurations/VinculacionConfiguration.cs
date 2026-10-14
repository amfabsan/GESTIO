using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class VinculacionConfiguration : IEntityTypeConfiguration<Vinculacion>
{
    public void Configure(EntityTypeBuilder<Vinculacion> builder)
    {
        builder.ToTable("vinculaciones");

        builder.HasKey(vinculacion => vinculacion.Id);

        builder.Property(vinculacion => vinculacion.CodigoAsesoria)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(vinculacion => vinculacion.Estado)
            .IsRequired();

        builder.Property(vinculacion => vinculacion.FechaSolicitud)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(vinculacion => vinculacion.FechaAceptacion)
            .HasColumnType("timestamp with time zone");

        builder.Property(vinculacion => vinculacion.FechaFinalizacion)
            .HasColumnType("timestamp with time zone");

        builder.HasOne(vinculacion => vinculacion.Autonomo)
            .WithMany(usuario => usuario.VinculacionesComoAutonomo)
            .HasForeignKey(vinculacion => vinculacion.AutonomoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(vinculacion => new
            {
                vinculacion.AutonomoId,
                vinculacion.CodigoAsesoria
            })
            .HasFilter($"\"Estado\" = {(int)EstadoVinculacion.PENDIENTE}")
            .IsUnique();

        builder.HasIndex(vinculacion => vinculacion.AutonomoId)
            .HasFilter($"\"Estado\" = {(int)EstadoVinculacion.ACTIVA}")
            .IsUnique();

        builder.HasIndex(vinculacion => new
        {
            vinculacion.CodigoAsesoria,
            vinculacion.Estado
        });

        builder.HasIndex(vinculacion => new
        {
            vinculacion.AutonomoId,
            vinculacion.FechaSolicitud
        });
    }
}
