using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class VinculacionEvidenciasChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vinculos_evidencia_checklist",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ElementoChecklistObligacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechaPropuesta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmadaPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaConfirmacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vinculos_evidencia_checklist", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vinculos_evidencia_checklist_documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vinculos_evidencia_checklist_elementos_checklist_obligacion~",
                        column: x => x.ElementoChecklistObligacionId,
                        principalTable: "elementos_checklist_obligacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vinculos_evidencia_checklist_usuarios_ConfirmadaPorId",
                        column: x => x.ConfirmadaPorId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "historial_vinculos_evidencia_checklist",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VinculoEvidenciaChecklistId = table.Column<Guid>(type: "uuid", nullable: false),
                    EstadoAnterior = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EstadoNuevo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    Fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historial_vinculos_evidencia_checklist", x => x.Id);
                    table.ForeignKey(
                        name: "FK_historial_vinculos_evidencia_checklist_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_historial_vinculos_evidencia_checklist_vinculos_evidencia_c~",
                        column: x => x.VinculoEvidenciaChecklistId,
                        principalTable: "vinculos_evidencia_checklist",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_historial_vinculos_evidencia_checklist_UsuarioId",
                table: "historial_vinculos_evidencia_checklist",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_historial_vinculos_evidencia_checklist_VinculoEvidenciaChec~",
                table: "historial_vinculos_evidencia_checklist",
                columns: new[] { "VinculoEvidenciaChecklistId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_vinculos_evidencia_checklist_ConfirmadaPorId",
                table: "vinculos_evidencia_checklist",
                column: "ConfirmadaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_vinculos_evidencia_checklist_DocumentoId_ElementoChecklistO~",
                table: "vinculos_evidencia_checklist",
                columns: new[] { "DocumentoId", "ElementoChecklistObligacionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vinculos_evidencia_checklist_ElementoChecklistObligacionId",
                table: "vinculos_evidencia_checklist",
                column: "ElementoChecklistObligacionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "historial_vinculos_evidencia_checklist");

            migrationBuilder.DropTable(
                name: "vinculos_evidencia_checklist");
        }
    }
}
