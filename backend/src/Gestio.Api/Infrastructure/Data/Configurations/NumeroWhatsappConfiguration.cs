using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class NumeroWhatsappConfiguration : IEntityTypeConfiguration<NumeroWhatsapp>
{
    public const string IndiceUnoPorUsuario = "ux_numeros_whatsapp_usuario";
    public const string IndiceTelefonoVerificado = "ux_numeros_whatsapp_telefono_verificado";
    public const string IndiceCodigo = "ux_numeros_whatsapp_codigo";
    public const string RestriccionTelefonoVerificado = "ck_numeros_whatsapp_telefono_verificado";
    public const string RestriccionTelefonoPendiente = "ck_numeros_whatsapp_telefono_pendiente";
    public const string RestriccionCodigo = "ck_numeros_whatsapp_codigo";
    public const string RestriccionVerificacionCompleta = "ck_numeros_whatsapp_verificacion_completa";

    // E.164 sin «+»: solo dígitos, sin cero inicial, de 8 a 15. Un único formato hace que el índice único detecte
    // el mismo número escrito de dos maneras.
    private const string PatronTelefono = "^[1-9][0-9]{7,14}$";

    public void Configure(EntityTypeBuilder<NumeroWhatsapp> builder)
    {
        builder.ToTable("numeros_whatsapp", t =>
        {
            t.HasCheckConstraint(RestriccionTelefonoVerificado, $"\"TelefonoVerificado\" ~ '{PatronTelefono}'");
            t.HasCheckConstraint(RestriccionTelefonoPendiente, $"\"TelefonoPendiente\" ~ '{PatronTelefono}'");
            // Siempre en mayúsculas: el índice único distingue mayúsculas y la comparación con el usuario no.
            t.HasCheckConstraint(RestriccionCodigo, "\"CodigoVerificacion\" ~ '^GESTIO-[A-Z0-9]{6}$'");
            // D2: un código siempre va con el número desde el que debe llegar y con su caducidad; nunca suelto.
            t.HasCheckConstraint(RestriccionVerificacionCompleta,
                "(\"CodigoVerificacion\" IS NULL) = (\"TelefonoPendiente\" IS NULL) AND " +
                "(\"CodigoVerificacion\" IS NULL) = (\"CodigoCaducaEn\" IS NULL)");
        });

        builder.HasKey(numero => numero.Id);

        builder.Property(numero => numero.TelefonoVerificado).HasMaxLength(20);
        builder.Property(numero => numero.TelefonoPendiente).HasMaxLength(20);
        builder.Property(numero => numero.CodigoVerificacion).HasMaxLength(20);
        builder.Property(numero => numero.FechaVerificacion).HasColumnType("timestamp with time zone");
        builder.Property(numero => numero.CodigoCaducaEn).HasColumnType("timestamp with time zone");

        builder.HasOne(numero => numero.Usuario)
            .WithMany()
            .HasForeignKey(numero => numero.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(numero => numero.UsuarioId).IsUnique().HasDatabaseName(IndiceUnoPorUsuario);

        // Requisito 2: un número verificado pertenece a una sola cuenta («asociado de forma inequívoca»).
        builder.HasIndex(numero => numero.TelefonoVerificado).IsUnique()
            .HasFilter("\"TelefonoVerificado\" IS NOT NULL").HasDatabaseName(IndiceTelefonoVerificado);

        builder.HasIndex(numero => numero.CodigoVerificacion).IsUnique()
            .HasFilter("\"CodigoVerificacion\" IS NOT NULL").HasDatabaseName(IndiceCodigo);
    }
}
