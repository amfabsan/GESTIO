using Gestio.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gestio.Api.Infrastructure.Data.Configurations;

public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios", table =>
        {
            table.HasCheckConstraint(
                "CK_usuarios_codigo_asesoria_por_rol",
                "(\"Rol\" = 2 AND \"CodigoAsesoria\" ~ '^ASE-[0-9]{6}$') OR (\"Rol\" <> 2 AND \"CodigoAsesoria\" IS NULL)");
            table.HasCheckConstraint(
                "CK_usuarios_dos_apellidos_obligatorios",
                "length(trim(\"PrimerApellido\")) > 0 AND length(trim(\"SegundoApellido\")) > 0");
            table.HasCheckConstraint(
                "CK_usuarios_administrador_es_gestor",
                "\"Rol\" = 2 OR (\"EsAdministradorAsesoria\" = FALSE AND \"NombreAsesoria\" IS NULL)");
        });

        builder.HasKey(usuario => usuario.Id);

        builder.Property(usuario => usuario.Nombre)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(usuario => usuario.PrimerApellido)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(usuario => usuario.SegundoApellido)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(usuario => usuario.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(usuario => usuario.Nif)
            .HasColumnType("varchar(9)")
            .IsRequired();

        builder.HasIndex(usuario => usuario.Nif)
            .IsUnique();

        builder.Property(usuario => usuario.CodigoAsesoria)
            .HasColumnType("varchar(10)");

        builder.Property(usuario => usuario.EsAdministradorAsesoria)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(usuario => usuario.NombreAsesoria)
            .HasMaxLength(200);

        builder.HasIndex(usuario => usuario.Email)
            .IsUnique();

        builder.Property(usuario => usuario.Rol)
            .IsRequired();

        builder.HasOne(usuario => usuario.Gestor)
            .WithMany(usuario => usuario.Clientes)
            .HasForeignKey(usuario => usuario.GestorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}