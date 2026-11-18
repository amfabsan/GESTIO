using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChecklistObligaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE obligaciones_fiscales SET \"Estado\" = 3 WHERE \"Estado\" = 1;");

            migrationBuilder.CreateTable(
                name: "elementos_checklist_obligacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ObligacionFiscalId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodigoPlantilla = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    OrigenUltimoCambio = table.Column<int>(type: "integer", nullable: true),
                    UsuarioUltimoCambioId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaUltimoCambio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_elementos_checklist_obligacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_elementos_checklist_obligacion_obligaciones_fiscales_Obliga~",
                        column: x => x.ObligacionFiscalId,
                        principalTable: "obligaciones_fiscales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_elementos_checklist_obligacion_usuarios_UsuarioUltimoCambio~",
                        column: x => x.UsuarioUltimoCambioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "historial_elementos_checklist",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ElementoChecklistObligacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EstadoAnterior = table.Column<int>(type: "integer", nullable: false),
                    EstadoNuevo = table.Column<int>(type: "integer", nullable: false),
                    Origen = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historial_elementos_checklist", x => x.Id);
                    table.ForeignKey(
                        name: "FK_historial_elementos_checklist_elementos_checklist_obligacio~",
                        column: x => x.ElementoChecklistObligacionId,
                        principalTable: "elementos_checklist_obligacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_historial_elementos_checklist_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_elementos_checklist_obligacion_ObligacionFiscalId_CodigoPla~",
                table: "elementos_checklist_obligacion",
                columns: new[] { "ObligacionFiscalId", "CodigoPlantilla" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_elementos_checklist_obligacion_UsuarioUltimoCambioId",
                table: "elementos_checklist_obligacion",
                column: "UsuarioUltimoCambioId");

            migrationBuilder.CreateIndex(
                name: "IX_historial_elementos_checklist_ElementoChecklistObligacionId~",
                table: "historial_elementos_checklist",
                columns: new[] { "ElementoChecklistObligacionId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_historial_elementos_checklist_UsuarioId",
                table: "historial_elementos_checklist",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE obligaciones_fiscales SET \"Estado\" = 1 WHERE \"Estado\" IN (3, 4);");

            migrationBuilder.DropTable(
                name: "historial_elementos_checklist");

            migrationBuilder.DropTable(
                name: "elementos_checklist_obligacion");
        }
    }
}
