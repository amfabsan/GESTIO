using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class FacturaConfiguration : IEntityTypeConfiguration<Factura>
{
    public void Configure(EntityTypeBuilder<Factura> builder)
    {
        builder.ToTable("facturas");
        builder.HasKey(factura => factura.Id);
        builder.Property(factura => factura.Serie).HasMaxLength(20).IsRequired();
        builder.Property(factura => factura.Estado).IsRequired();
        builder.Property(factura => factura.RetencionPorcentaje).HasPrecision(5, 2);
        builder.Property(factura => factura.BaseImponible).HasPrecision(18, 2);
        builder.Property(factura => factura.CuotaIva).HasPrecision(18, 2);
        builder.Property(factura => factura.ImporteRetencion).HasPrecision(18, 2);
        builder.Property(factura => factura.Total).HasPrecision(18, 2);
        builder.Property(factura => factura.EmisorNombre).HasMaxLength(400);
        builder.Property(factura => factura.EmisorNif).HasMaxLength(30);
        builder.Property(factura => factura.EmisorDireccion).HasMaxLength(500);
        builder.Property(factura => factura.ClienteNombre).HasMaxLength(200);
        builder.Property(factura => factura.ClienteNif).HasMaxLength(30);
        builder.Property(factura => factura.ClienteDireccion).HasMaxLength(500);
        builder.Property(factura => factura.Creada).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(factura => factura.Actualizada).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(factura => factura.Emitida).HasColumnType("timestamp with time zone");
        builder.HasOne(factura => factura.Autonomo).WithMany().HasForeignKey(factura => factura.AutonomoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(factura => factura.Contacto).WithMany().HasForeignKey(factura => factura.ContactoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(factura => factura.Lineas).WithOne(linea => linea.Factura)
            .HasForeignKey(linea => linea.FacturaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(factura => new { factura.AutonomoId, factura.Serie, factura.Numero })
            .IsUnique().HasFilter("\"Numero\" IS NOT NULL");
        builder.HasIndex(factura => new { factura.AutonomoId, factura.Estado, factura.Fecha });
    }
}