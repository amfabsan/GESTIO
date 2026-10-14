using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SubidaDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_documentos_UsuarioId_HashSha256",
                table: "documentos");

            migrationBuilder.AddColumn<string>(
                name: "Origen",
                table: "documentos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "SUBIDA_WEB");

            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "documentos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TamanoBytes",
                table: "documentos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "TipoMime",
                table: "documentos",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "application/pdf");

            migrationBuilder.CreateTable(
                name: "eventos_documento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Detalle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos_documento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_eventos_documento_documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_eventos_documento_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documentos_UsuarioId_HashSha256",
                table: "documentos",
                columns: new[] { "UsuarioId", "HashSha256" });

            migrationBuilder.CreateIndex(
                name: "IX_eventos_documento_DocumentoId_Fecha",
                table: "eventos_documento",
                columns: new[] { "DocumentoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_eventos_documento_UsuarioId",
                table: "eventos_documento",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventos_documento");

            migrationBuilder.DropIndex(
                name: "IX_documentos_UsuarioId_HashSha256",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "Origen",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "TamanoBytes",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "TipoMime",
                table: "documentos");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_UsuarioId_HashSha256",
                table: "documentos",
                columns: new[] { "UsuarioId", "HashSha256" },
                unique: true);
        }
    }
}
