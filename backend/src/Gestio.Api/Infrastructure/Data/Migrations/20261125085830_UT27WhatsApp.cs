using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UT27WhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenciaMensaje",
                table: "documentos",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mensajes_whatsapp",
                columns: table => new
                {
                    MensajeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Resultado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Motivo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    DocumentoId = table.Column<Guid>(type: "uuid", nullable: true),
                    MediaId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NombreArchivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Intentos = table.Column<int>(type: "integer", nullable: false),
                    FechaRecepcion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FechaActualizacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mensajes_whatsapp", x => x.MensajeId);
                    table.ForeignKey(
                        name: "FK_mensajes_whatsapp_documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mensajes_whatsapp_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "numeros_whatsapp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    TelefonoVerificado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FechaVerificacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TelefonoPendiente = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CodigoVerificacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CodigoCaducaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_numeros_whatsapp", x => x.Id);
                    table.CheckConstraint("ck_numeros_whatsapp_codigo", "\"CodigoVerificacion\" ~ '^GESTIO-[A-Z0-9]{6}$'");
                    table.CheckConstraint("ck_numeros_whatsapp_telefono_pendiente", "\"TelefonoPendiente\" ~ '^[1-9][0-9]{7,14}$'");
                    table.CheckConstraint("ck_numeros_whatsapp_telefono_verificado", "\"TelefonoVerificado\" ~ '^[1-9][0-9]{7,14}$'");
                    table.CheckConstraint("ck_numeros_whatsapp_verificacion_completa", "(\"CodigoVerificacion\" IS NULL) = (\"TelefonoPendiente\" IS NULL) AND (\"CodigoVerificacion\" IS NULL) = (\"CodigoCaducaEn\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_numeros_whatsapp_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_documentos_referencia_mensaje",
                table: "documentos",
                column: "ReferenciaMensaje",
                unique: true,
                filter: "\"ReferenciaMensaje\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_documentos_referencia_solo_whatsapp",
                table: "documentos",
                sql: "(\"Origen\" = 'WHATSAPP') = (\"ReferenciaMensaje\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_mensajes_whatsapp_DocumentoId",
                table: "mensajes_whatsapp",
                column: "DocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_mensajes_whatsapp_Resultado",
                table: "mensajes_whatsapp",
                column: "Resultado");

            migrationBuilder.CreateIndex(
                name: "IX_mensajes_whatsapp_UsuarioId",
                table: "mensajes_whatsapp",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "ux_numeros_whatsapp_codigo",
                table: "numeros_whatsapp",
                column: "CodigoVerificacion",
                unique: true,
                filter: "\"CodigoVerificacion\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_numeros_whatsapp_telefono_verificado",
                table: "numeros_whatsapp",
                column: "TelefonoVerificado",
                unique: true,
                filter: "\"TelefonoVerificado\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_numeros_whatsapp_usuario",
                table: "numeros_whatsapp",
                column: "UsuarioId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mensajes_whatsapp");

            migrationBuilder.DropTable(
                name: "numeros_whatsapp");

            migrationBuilder.DropIndex(
                name: "ux_documentos_referencia_mensaje",
                table: "documentos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_documentos_referencia_solo_whatsapp",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "ReferenciaMensaje",
                table: "documentos");
        }
    }
}
