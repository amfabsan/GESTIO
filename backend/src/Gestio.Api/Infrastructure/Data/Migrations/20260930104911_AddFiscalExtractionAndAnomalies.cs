using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalExtractionAndAnomalies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoAsesoria",
                table: "usuarios",
                type: "varchar(10)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nif",
                table: "usuarios",
                type: "varchar(9)",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE usuarios
                SET "Nif" = CASE "Id"
                    WHEN '11111111-1111-1111-1111-111111111111' THEN '12345678Z'
                    WHEN '22222222-2222-2222-2222-222222222222' THEN '87654321X'
                END
                WHERE "Id" IN ('11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Nif",
                table: "usuarios",
                type: "varchar(9)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(9)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "anomalias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Severidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Resuelta = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    FechaDeteccion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anomalias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_anomalias_documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "extracciones_ia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmisorNombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmisorNif = table.Column<string>(type: "varchar(9)", nullable: true),
                    FechaEmision = table.Column<DateOnly>(type: "date", nullable: true),
                    BaseImponible = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TipoIva = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    CuotaIva = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CategoriaSugerida = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NivelConfianza = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DatosCrudosJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extracciones_ia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_extracciones_ia_documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Nif",
                table: "usuarios",
                column: "Nif",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_usuarios_codigo_asesoria_por_rol",
                table: "usuarios",
                sql: "(\"Rol\" = 2 AND \"CodigoAsesoria\" ~ '^ASE-[0-9]{6}$') OR (\"Rol\" <> 2 AND \"CodigoAsesoria\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_anomalias_DocumentoId",
                table: "anomalias",
                column: "DocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_extracciones_ia_DocumentoId",
                table: "extracciones_ia",
                column: "DocumentoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anomalias");

            migrationBuilder.DropTable(
                name: "extracciones_ia");

            migrationBuilder.DropIndex(
                name: "IX_usuarios_Nif",
                table: "usuarios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_usuarios_codigo_asesoria_por_rol",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "CodigoAsesoria",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "Nif",
                table: "usuarios");
        }
    }
}
