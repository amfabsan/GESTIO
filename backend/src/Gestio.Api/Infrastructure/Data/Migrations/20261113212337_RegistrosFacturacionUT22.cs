using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegistrosFacturacionUT22 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "registros_facturacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutonomoId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Secuencia = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IdEmisorFactura = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NumSerieFactura = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    FechaExpedicionFactura = table.Column<DateOnly>(type: "date", nullable: false),
                    TipoFactura = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CuotaTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImporteTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FechaHoraHusoGenRegistro = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    Generado = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AnteriorId = table.Column<Guid>(type: "uuid", nullable: true),
                    HuellaAnterior = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Huella = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registros_facturacion", x => x.Id);
                    table.UniqueConstraint("AK_registros_facturacion_autonomo_id", x => new { x.AutonomoId, x.Id });
                    table.CheckConstraint("CK_registros_facturacion_huella_anterior", "(\"HuellaAnterior\" IS NULL) = (\"AnteriorId\" IS NULL)");
                    table.CheckConstraint("CK_registros_facturacion_inicio_cadena", "(\"Secuencia\" = 1) = (\"AnteriorId\" IS NULL) AND \"Secuencia\" >= 1");
                    table.ForeignKey(
                        name: "FK_registros_facturacion_anterior_mismo_autonomo",
                        columns: x => new { x.AutonomoId, x.AnteriorId },
                        principalTable: "registros_facturacion",
                        principalColumns: new[] { "AutonomoId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registros_facturacion_facturas_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "facturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registros_facturacion_usuarios_AutonomoId",
                        column: x => x.AutonomoId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_registros_facturacion_AutonomoId_AnteriorId",
                table: "registros_facturacion",
                columns: new[] { "AutonomoId", "AnteriorId" });

            migrationBuilder.CreateIndex(
                name: "UX_registros_facturacion_anterior",
                table: "registros_facturacion",
                column: "AnteriorId",
                unique: true,
                filter: "\"AnteriorId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_registros_facturacion_autonomo_secuencia",
                table: "registros_facturacion",
                columns: new[] { "AutonomoId", "Secuencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_registros_facturacion_factura",
                table: "registros_facturacion",
                column: "FacturaId",
                unique: true);

            // Req. 7 / PA 1598: inmutabilidad en la propia BD (añadido a mano; EF no genera triggers).
            // Cubre lo que no pasa por SaveChanges: ExecuteUpdate/ExecuteDelete, SQL directo y TRUNCATE.
            // No protege frente al propietario de la tabla (DISABLE TRIGGER, DROP TABLE, Down()): es una simulación académica.
            migrationBuilder.Sql("""
                CREATE FUNCTION registros_facturacion_inmutables() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Los registros de facturación son inmutables: % no permitido', TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER registros_facturacion_sin_cambios
                BEFORE UPDATE OR DELETE ON registros_facturacion
                FOR EACH ROW EXECUTE FUNCTION registros_facturacion_inmutables();
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER registros_facturacion_sin_truncate
                BEFORE TRUNCATE ON registros_facturacion
                FOR EACH STATEMENT EXECUTE FUNCTION registros_facturacion_inmutables();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registros_facturacion");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS registros_facturacion_inmutables();");
        }
    }
}
