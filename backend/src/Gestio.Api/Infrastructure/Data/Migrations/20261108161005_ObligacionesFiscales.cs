using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ObligacionesFiscales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "obligaciones_fiscales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutonomoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Modelo = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Ejercicio = table.Column<int>(type: "integer", nullable: false),
                    Periodo = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    FechaPrevista = table.Column<DateOnly>(type: "date", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_obligaciones_fiscales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_obligaciones_fiscales_usuarios_AutonomoId",
                        column: x => x.AutonomoId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "historial_obligaciones_fiscales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ObligacionFiscalId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historial_obligaciones_fiscales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_historial_obligaciones_fiscales_obligaciones_fiscales_Oblig~",
                        column: x => x.ObligacionFiscalId,
                        principalTable: "obligaciones_fiscales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_historial_obligaciones_fiscales_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_historial_obligaciones_fiscales_ObligacionFiscalId_Fecha",
                table: "historial_obligaciones_fiscales",
                columns: new[] { "ObligacionFiscalId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_historial_obligaciones_fiscales_UsuarioId",
                table: "historial_obligaciones_fiscales",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_obligaciones_fiscales_AutonomoId_Modelo_Ejercicio_Periodo",
                table: "obligaciones_fiscales",
                columns: new[] { "AutonomoId", "Modelo", "Ejercicio", "Periodo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "historial_obligaciones_fiscales");

            migrationBuilder.DropTable(
                name: "obligaciones_fiscales");
        }
    }
}
