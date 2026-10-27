using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdministradorYNombreAsesoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsAdministradorAsesoria",
                table: "usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NombreAsesoria",
                table: "usuarios",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // Hasta ahora cualquier gestor de la asesoría gestionaba sus vinculaciones: se conserva
            // ese permiso para los existentes y los nuevos gestores empiezan sin él.
            migrationBuilder.Sql("UPDATE usuarios SET \"EsAdministradorAsesoria\" = TRUE WHERE \"Rol\" = 2;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_usuarios_administrador_es_gestor",
                table: "usuarios",
                sql: "\"Rol\" = 2 OR (\"EsAdministradorAsesoria\" = FALSE AND \"NombreAsesoria\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_usuarios_administrador_es_gestor",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "EsAdministradorAsesoria",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "NombreAsesoria",
                table: "usuarios");
        }
    }
}
