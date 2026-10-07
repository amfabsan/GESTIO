using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDosApellidosToUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrimerApellido",
                table: "usuarios",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SegundoApellido",
                table: "usuarios",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql("UPDATE usuarios SET \"Nombre\" = 'Ramón', \"PrimerApellido\" = 'García', \"SegundoApellido\" = 'López' WHERE \"Id\" = '11111111-1111-1111-1111-111111111111';");
            migrationBuilder.Sql("UPDATE usuarios SET \"Nombre\" = 'Pedro', \"PrimerApellido\" = 'Sánchez', \"SegundoApellido\" = 'Martín' WHERE \"Id\" = '22222222-2222-2222-2222-222222222222';");

            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM usuarios WHERE COALESCE(NULLIF(trim(\"PrimerApellido\"), ''), '') = '' OR COALESCE(NULLIF(trim(\"SegundoApellido\"), ''), '') = '') THEN RAISE EXCEPTION 'Provide both surnames for every existing user before applying this migration'; END IF; END $$;");

            migrationBuilder.AlterColumn<string>(
                name: "PrimerApellido",
                table: "usuarios",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SegundoApellido",
                table: "usuarios",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_usuarios_dos_apellidos_obligatorios",
                table: "usuarios",
                sql: "length(trim(\"PrimerApellido\")) > 0 AND length(trim(\"SegundoApellido\")) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE usuarios SET \"Nombre\" = 'Ramón García López' WHERE \"Id\" = '11111111-1111-1111-1111-111111111111';");
            migrationBuilder.Sql("UPDATE usuarios SET \"Nombre\" = 'Pedro Sánchez Martín' WHERE \"Id\" = '22222222-2222-2222-2222-222222222222';");

            migrationBuilder.DropCheckConstraint(
                name: "CK_usuarios_dos_apellidos_obligatorios",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "PrimerApellido",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "SegundoApellido",
                table: "usuarios");
        }
    }
}
