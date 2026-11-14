using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gestio.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CodigoPublicoFacturaUT23 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoPublico",
                table: "facturas",
                type: "character varying(22)",
                maxLength: 22,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_facturas_codigo_publico",
                table: "facturas",
                column: "CodigoPublico",
                unique: true,
                filter: "\"CodigoPublico\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_facturas_codigo_publico_solo_emitidas",
                table: "facturas",
                sql: "\"CodigoPublico\" IS NULL OR \"Estado\" = 2");

            // PB-23 req. 5 / decisión D1 (añadido a mano; EF no genera triggers): una vez asignado, el código público
            // no se cambia ni se borra, porque el QR ya impreso dejaría de funcionar. Se permite pasar de NULL a un valor
            // (la emisión). Cubre también SQL directo y ExecuteUpdate. Como en UT22, no protege frente al propietario de la tabla.
            migrationBuilder.Sql("""
                CREATE FUNCTION facturas_codigo_publico_inmutable() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF OLD."CodigoPublico" IS NOT NULL AND NEW."CodigoPublico" IS DISTINCT FROM OLD."CodigoPublico" THEN
                        RAISE EXCEPTION 'El código público de una factura emitida no se puede modificar'
                            USING ERRCODE = 'restrict_violation';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER facturas_codigo_publico_inmutable
                BEFORE UPDATE OF "CodigoPublico" ON facturas
                FOR EACH ROW EXECUTE FUNCTION facturas_codigo_publico_inmutable();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS facturas_codigo_publico_inmutable ON facturas;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS facturas_codigo_publico_inmutable();");

            migrationBuilder.DropIndex(
                name: "UX_facturas_codigo_publico",
                table: "facturas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_facturas_codigo_publico_solo_emitidas",
                table: "facturas");

            migrationBuilder.DropColumn(
                name: "CodigoPublico",
                table: "facturas");
        }
    }
}
