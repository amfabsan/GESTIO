using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExtraccionDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NumeroFactura",
                table: "extracciones_ia",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "intentos_procesamiento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Fin = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Procesador = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: true),
                    Resultado = table.Column<string>(type: "text", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    ValoresJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intentos_procesamiento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_intentos_procesamiento_documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_intentos_procesamiento_DocumentoId",
                table: "intentos_procesamiento",
                column: "DocumentoId",
                unique: true,
                filter: "\"Fin\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "intentos_procesamiento");

            migrationBuilder.DropColumn(
                name: "NumeroFactura",
                table: "extracciones_ia");
        }
    }
}
