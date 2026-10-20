using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Vinculaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vinculaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutonomoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodigoAsesoria = table.Column<string>(type: "varchar(10)", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    FechaSolicitud = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FechaAceptacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AceptadaPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaFinalizacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinalizadaPorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vinculaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vinculaciones_usuarios_AutonomoId",
                        column: x => x.AutonomoId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vinculaciones_AutonomoId",
                table: "vinculaciones",
                column: "AutonomoId",
                unique: true,
                filter: "\"Estado\" = 2");

            migrationBuilder.CreateIndex(
                name: "IX_vinculaciones_AutonomoId_CodigoAsesoria",
                table: "vinculaciones",
                columns: new[] { "AutonomoId", "CodigoAsesoria" },
                unique: true,
                filter: "\"Estado\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_vinculaciones_AutonomoId_FechaSolicitud",
                table: "vinculaciones",
                columns: new[] { "AutonomoId", "FechaSolicitud" });

            migrationBuilder.CreateIndex(
                name: "IX_vinculaciones_CodigoAsesoria_Estado",
                table: "vinculaciones",
                columns: new[] { "CodigoAsesoria", "Estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vinculaciones");
        }
    }
}
