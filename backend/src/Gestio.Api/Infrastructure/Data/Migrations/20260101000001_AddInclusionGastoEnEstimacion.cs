using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInclusionGastoEnEstimacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inclusiones_gastos_en_estimacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    incluida = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_cambio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_por_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inclusiones_gastos_en_estimacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_inclusiones_gastos_en_estimacion_documentos_documento_id",
                        column: x => x.documento_id,
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inclusiones_gastos_en_estimacion_usuarios_actualizado_por_id",
                        column: x => x.actualizado_por_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inclusiones_gastos_en_estimacion_actualizado_por_id",
                table: "inclusiones_gastos_en_estimacion",
                column: "actualizado_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_inclusiones_gastos_en_estimacion_documento_id",
                table: "inclusiones_gastos_en_estimacion",
                column: "documento_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inclusiones_gastos_en_estimacion_fecha_cambio",
                table: "inclusiones_gastos_en_estimacion",
                column: "fecha_cambio");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inclusiones_gastos_en_estimacion");
        }
    }
}
