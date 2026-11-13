using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class LineaFacturaConfiguration : IEntityTypeConfiguration<LineaFactura>
{
    public void Configure(EntityTypeBuilder<LineaFactura> builder)
    {
        builder.ToTable("lineas_factura");
        builder.HasKey(linea => linea.Id);
        builder.Property(linea => linea.Descripcion).HasMaxLength(500);
        builder.Property(linea => linea.Cantidad).HasPrecision(18, 4);
        builder.Property(linea => linea.PrecioUnitario).HasPrecision(18, 4);
        builder.Property(linea => linea.IvaPorcentaje).HasPrecision(5, 2);
        builder.HasIndex(linea => new { linea.FacturaId, linea.Orden }).IsUnique();
    }
}