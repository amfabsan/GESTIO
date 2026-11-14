using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Services.Verifactu;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class FacturaConfiguration : IEntityTypeConfiguration<Factura>
{
    // UT23: nombres fijos para que las pruebas comprueben que cada restricción existe por su nombre.
    public const string IndiceCodigoPublico = "UX_facturas_codigo_publico";
    public const string RestriccionCodigoSoloEmitidas = "CK_facturas_codigo_publico_solo_emitidas";

    public void Configure(EntityTypeBuilder<Factura> builder)
    {
        // PB-23 req. 5: un borrador no tiene QR. EstadoFactura se guarda como entero (EMITIDA = 2).
        builder.ToTable("facturas", t =>
            t.HasCheckConstraint(RestriccionCodigoSoloEmitidas,
                $"\"CodigoPublico\" IS NULL OR \"Estado\" = {(int)EstadoFactura.EMITIDA}"));
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
        builder.Property(factura => factura.CodigoPublico).HasMaxLength(CodigoPublicoFactura.Longitud);
        builder.HasOne(factura => factura.Autonomo).WithMany().HasForeignKey(factura => factura.AutonomoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(factura => factura.Contacto).WithMany().HasForeignKey(factura => factura.ContactoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(factura => factura.Lineas).WithOne(linea => linea.Factura)
            .HasForeignKey(linea => linea.FacturaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(factura => new { factura.AutonomoId, factura.Serie, factura.Numero })
            .IsUnique().HasFilter("\"Numero\" IS NOT NULL");
        builder.HasIndex(factura => new { factura.AutonomoId, factura.Estado, factura.Fecha });
        // PB-23 req. 1 y prueba 1608: un código identifica a una sola factura.
        builder.HasIndex(factura => factura.CodigoPublico).IsUnique()
            .HasFilter("\"CodigoPublico\" IS NOT NULL").HasDatabaseName(IndiceCodigoPublico);
    }
}