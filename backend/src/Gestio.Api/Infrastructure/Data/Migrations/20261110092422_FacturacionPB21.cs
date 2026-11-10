using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FacturacionPB21 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "facturas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutonomoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Serie = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    RetencionPorcentaje = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    BaseImponible = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CuotaIva = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ImporteRetencion = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    EmisorNombre = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    EmisorNif = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    EmisorDireccion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClienteNombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClienteNif = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ClienteDireccion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Creada = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Actualizada = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Emitida = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facturas", x => x.Id);
                    table.ForeignKey("FK_facturas_contactos_ContactoId", x => x.ContactoId, "contactos", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_facturas_usuarios_AutonomoId", x => x.AutonomoId, "usuarios", "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lineas_factura",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Cantidad = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    PrecioUnitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    IvaPorcentaje = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lineas_factura", x => x.Id);
                    table.ForeignKey("FK_lineas_factura_facturas_FacturaId", x => x.FacturaId, "facturas", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_facturas_ContactoId", "facturas", "ContactoId");
            migrationBuilder.CreateIndex("IX_facturas_AutonomoId_Estado_Fecha", "facturas", new[] { "AutonomoId", "Estado", "Fecha" });
            migrationBuilder.CreateIndex("IX_facturas_AutonomoId_Serie_Numero", "facturas", new[] { "AutonomoId", "Serie", "Numero" }, unique: true, filter: "\"Numero\" IS NOT NULL");
            migrationBuilder.CreateIndex("IX_lineas_factura_FacturaId_Orden", "lineas_factura", new[] { "FacturaId", "Orden" }, unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "lineas_factura");
            migrationBuilder.DropTable(name: "facturas");
        }
    }
}
