using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DeteccionDuplicadosPorHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocumentoRelacionadoId",
                table: "anomalias",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_anomalias_DocumentoRelacionadoId",
                table: "anomalias",
                column: "DocumentoRelacionadoId");

            migrationBuilder.AddForeignKey(
                name: "FK_anomalias_documentos_DocumentoRelacionadoId",
                table: "anomalias",
                column: "DocumentoRelacionadoId",
                principalTable: "documentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_anomalias_documentos_DocumentoRelacionadoId",
                table: "anomalias");

            migrationBuilder.DropIndex(
                name: "IX_anomalias_DocumentoRelacionadoId",
                table: "anomalias");

            migrationBuilder.DropColumn(
                name: "DocumentoRelacionadoId",
                table: "anomalias");
        }
    }
}
