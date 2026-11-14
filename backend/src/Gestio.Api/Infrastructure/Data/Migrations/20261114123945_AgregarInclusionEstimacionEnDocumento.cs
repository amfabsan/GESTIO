using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarInclusionEstimacionEnDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InclusionesGastosEnEstimación",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Incluida = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCambio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoPorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InclusionesGastosEnEstimación", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InclusionesGastosEnEstimación_documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InclusionesGastosEnEstimación_usuarios_ActualizadoPorId",
                        column: x => x.ActualizadoPorId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InclusionesGastosEnEstimación_ActualizadoPorId",
                table: "InclusionesGastosEnEstimación",
                column: "ActualizadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_InclusionesGastosEnEstimación_DocumentoId",
                table: "InclusionesGastosEnEstimación",
                column: "DocumentoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InclusionesGastosEnEstimación");
        }
    }
}
