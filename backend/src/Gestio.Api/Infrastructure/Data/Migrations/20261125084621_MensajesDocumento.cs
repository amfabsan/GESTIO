using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MensajesDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mensajes_documento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Contenido = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mensajes_documento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mensajes_documento_documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mensajes_documento_usuarios_AutorId",
                        column: x => x.AutorId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "avisos_mensaje_documento",
                columns: table => new
                {
                    MensajeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinatarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaLectura = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avisos_mensaje_documento", x => new { x.MensajeId, x.DestinatarioId });
                    table.ForeignKey(
                        name: "FK_avisos_mensaje_documento_mensajes_documento_MensajeId",
                        column: x => x.MensajeId,
                        principalTable: "mensajes_documento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_avisos_mensaje_documento_usuarios_DestinatarioId",
                        column: x => x.DestinatarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_avisos_mensaje_documento_DestinatarioId_FechaLectura",
                table: "avisos_mensaje_documento",
                columns: new[] { "DestinatarioId", "FechaLectura" });

            migrationBuilder.CreateIndex(
                name: "IX_mensajes_documento_AutorId",
                table: "mensajes_documento",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_mensajes_documento_DocumentoId_Fecha_Id",
                table: "mensajes_documento",
                columns: new[] { "DocumentoId", "Fecha", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "avisos_mensaje_documento");

            migrationBuilder.DropTable(
                name: "mensajes_documento");
        }
    }
}
