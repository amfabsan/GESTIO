using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class HistorialEventosDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UsuarioId",
                table: "eventos_documento",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Campo",
                table: "eventos_documento",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValorAnterior",
                table: "eventos_documento",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValorNuevo",
                table: "eventos_documento",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Campo",
                table: "eventos_documento");

            migrationBuilder.DropColumn(
                name: "ValorAnterior",
                table: "eventos_documento");

            migrationBuilder.DropColumn(
                name: "ValorNuevo",
                table: "eventos_documento");

            migrationBuilder.AlterColumn<Guid>(
                name: "UsuarioId",
                table: "eventos_documento",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
