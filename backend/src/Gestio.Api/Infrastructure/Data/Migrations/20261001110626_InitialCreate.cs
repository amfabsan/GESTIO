using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Nif = table.Column<string>(type: "varchar(9)", nullable: false),
                    CodigoAsesoria = table.Column<string>(type: "varchar(10)", nullable: true),
                    GestorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Rol = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                    table.CheckConstraint("CK_usuarios_codigo_asesoria_por_rol", "(\"Rol\" = 2 AND \"CodigoAsesoria\" ~ '^ASE-[0-9]{6}$') OR (\"Rol\" <> 2 AND \"CodigoAsesoria\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_usuarios_usuarios_GestorId",
                        column: x => x.GestorId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    NombreArchivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    HashSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FechaSubida = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_documentos_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "anomalias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
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
                name: "IX_anomalias_DocumentoId",
                table: "anomalias",
                column: "DocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_Estado",
                table: "documentos",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_UsuarioId_FechaSubida",
                table: "documentos",
                columns: new[] { "UsuarioId", "FechaSubida" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_documentos_UsuarioId_HashSha256",
                table: "documentos",
                columns: new[] { "UsuarioId", "HashSha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_extracciones_ia_DocumentoId",
                table: "extracciones_ia",
                column: "DocumentoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Email",
                table: "usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_GestorId",
                table: "usuarios",
                column: "GestorId");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Nif",
                table: "usuarios",
                column: "Nif",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anomalias");

            migrationBuilder.DropTable(
                name: "extracciones_ia");

            migrationBuilder.DropTable(
                name: "documentos");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
