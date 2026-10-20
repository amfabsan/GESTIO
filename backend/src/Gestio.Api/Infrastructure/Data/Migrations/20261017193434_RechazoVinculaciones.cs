using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RechazoVinculaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FechaRechazo",
                table: "vinculaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RechazadaPorId",
                table: "vinculaciones",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaRechazo",
                table: "vinculaciones");

            migrationBuilder.DropColumn(
                name: "RechazadaPorId",
                table: "vinculaciones");
        }
    }
}
