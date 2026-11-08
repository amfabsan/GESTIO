using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PerfilesFiscales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "perfiles_fiscales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutonomoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ejercicio = table.Column<int>(type: "integer", nullable: false),
                    RegimenIva = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RegimenIrpf = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TrabajadoresConRetencion = table.Column<bool>(type: "boolean", nullable: false),
                    LocalAlquiladoConRetencion = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_perfiles_fiscales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_perfiles_fiscales_usuarios_AutonomoId",
                        column: x => x.AutonomoId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "historial_perfiles_fiscales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PerfilFiscalId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaCambio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Campo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ValorAnterior = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ValorNuevo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historial_perfiles_fiscales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_historial_perfiles_fiscales_perfiles_fiscales_PerfilFiscalId",
                        column: x => x.PerfilFiscalId,
                        principalTable: "perfiles_fiscales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_historial_perfiles_fiscales_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_historial_perfiles_fiscales_PerfilFiscalId_FechaCambio",
                table: "historial_perfiles_fiscales",
                columns: new[] { "PerfilFiscalId", "FechaCambio" });

            migrationBuilder.CreateIndex(
                name: "IX_historial_perfiles_fiscales_UsuarioId",
                table: "historial_perfiles_fiscales",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_perfiles_fiscales_AutonomoId_Ejercicio",
                table: "perfiles_fiscales",
                columns: new[] { "AutonomoId", "Ejercicio" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "historial_perfiles_fiscales");

            migrationBuilder.DropTable(
                name: "perfiles_fiscales");
        }
    }
}
