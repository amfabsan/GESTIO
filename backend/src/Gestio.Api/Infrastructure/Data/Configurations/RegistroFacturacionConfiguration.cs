using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class RegistroFacturacionConfiguration : IEntityTypeConfiguration<RegistroFacturacion>
{
    public const string Tabla = "registros_facturacion";

    // Nombres fijos: la capa de servicio los usa para traducir una violación de unicidad al error adecuado.
    public const string IndiceUnaAltaPorFactura = "UX_registros_facturacion_factura";
    public const string IndiceUnSucesor = "UX_registros_facturacion_anterior";
    public const string IndiceSecuencia = "UX_registros_facturacion_autonomo_secuencia";
    public const string RestriccionInicioCadena = "CK_registros_facturacion_inicio_cadena";
    public const string RestriccionHuellaAnterior = "CK_registros_facturacion_huella_anterior";
    public const string ClaveAutonomoId = "AK_registros_facturacion_autonomo_id";
    public const string FkAnteriorMismoAutonomo = "FK_registros_facturacion_anterior_mismo_autonomo";
    public const string FkFactura = "FK_registros_facturacion_facturas_FacturaId";
    public const string FkAutonomo = "FK_registros_facturacion_usuarios_AutonomoId";

    public void Configure(EntityTypeBuilder<RegistroFacturacion> builder)
    {
        // Req. 3: el registro #1 es el inicio de cadena y es el único sin registro anterior.
        builder.ToTable(Tabla, t =>
        {
            t.HasCheckConstraint(RestriccionInicioCadena, "(\"Secuencia\" = 1) = (\"AnteriorId\" IS NULL) AND \"Secuencia\" >= 1");
            t.HasCheckConstraint(RestriccionHuellaAnterior, "(\"HuellaAnterior\" IS NULL) = (\"AnteriorId\" IS NULL)");
        });
        builder.HasKey(r => r.Id);
        // Clave alternativa para que el registro anterior se referencie junto con su autónomo (ver FK compuesta abajo).
        builder.HasAlternateKey(r => new { r.AutonomoId, r.Id }).HasName(ClaveAutonomoId);

        builder.Property(r => r.Tipo).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(r => r.IdEmisorFactura).HasMaxLength(30).IsRequired();
        builder.Property(r => r.NumSerieFactura).HasMaxLength(60).IsRequired();
        builder.Property(r => r.TipoFactura).HasMaxLength(2).IsRequired();
        builder.Property(r => r.CuotaTotal).HasPrecision(18, 2);
        builder.Property(r => r.ImporteTotal).HasPrecision(18, 2);
        builder.Property(r => r.FechaHoraHusoGenRegistro).HasMaxLength(25).IsRequired();
        builder.Property(r => r.Generado).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(r => r.HuellaAnterior).HasMaxLength(64);
        builder.Property(r => r.Huella).HasMaxLength(64).IsRequired();

        builder.HasOne<Usuario>().WithMany().HasForeignKey(r => r.AutonomoId).HasConstraintName(FkAutonomo).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Factura>().WithMany().HasForeignKey(r => r.FacturaId).HasConstraintName(FkFactura).OnDelete(DeleteBehavior.Restrict);
        // El anterior debe ser del MISMO autónomo: sin esto, un registro de B podría ocupar el único hueco de sucesor
        // de un registro de A y bloquear para siempre su cadena (revisión capa 2). Con AnteriorId NULL no se comprueba.
        builder.HasOne<RegistroFacturacion>().WithMany()
            .HasForeignKey(r => new { r.AutonomoId, r.AnteriorId })
            .HasPrincipalKey(r => new { r.AutonomoId, r.Id })
            .HasConstraintName(FkAnteriorMismoAutonomo)
            .OnDelete(DeleteBehavior.Restrict);

        // Req. 4 / PA 1589: una factura solo genera un registro de alta.
        builder.HasIndex(r => r.FacturaId).IsUnique().HasDatabaseName(IndiceUnaAltaPorFactura);
        // Req. 4 / PA 1590: un registro solo puede tener un sucesor directo (la cadena no se divide).
        builder.HasIndex(r => r.AnteriorId).IsUnique().HasFilter("\"AnteriorId\" IS NOT NULL").HasDatabaseName(IndiceUnSucesor);
        // Orden de la cadena: una posición por autónomo. También sirve para leer el último registro.
        // Junto con la CHECK (inicio ⇔ Secuencia = 1) garantiza un único inicio de cadena por autónomo.
        builder.HasIndex(r => new { r.AutonomoId, r.Secuencia }).IsUnique().HasDatabaseName(IndiceSecuencia);
    }
}
