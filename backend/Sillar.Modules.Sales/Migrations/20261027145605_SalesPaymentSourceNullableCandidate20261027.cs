using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Modules.Sales.Migrations
{
    /// <summary>
    /// CANDIDATA, NO APLICADA. Origen del pago según la <b>opción B ratificada por JP</b>
    /// el 27/10/2026: los históricos conservan <c>source = NULL</c> y los nuevos lo exigen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sustituye a <c>SalesPaymentSourceCandidate20261026</c>, que rellenaba con
    /// <c>staff</c> y queda fuera de la compilación como evidencia (ver el .csproj).
    /// <b>Sin valor por defecto y sin <c>UPDATE</c>:</b> ninguna fila existente cambia.
    /// </para>
    /// <para>
    /// Tres barreras, ninguna de las cuales toca una fila existente:
    /// <c>ck_order_payments_source</c> admite solo lo ratificado (un NULL lo cumple);
    /// <c>ck_order_payments_source_obligatorio</c> nace <c>NOT VALID</c>, así que se exige
    /// a cada INSERT y UPDATE pero no se comprueba contra lo que ya estaba; y el trigger
    /// impide cambiar el origen de un pago ya registrado, que es lo que reclasificaría un
    /// histórico.
    /// </para>
    /// <para>
    /// <b>Una base que ya aplicó la candidata anterior se trata aparte, no aquí.</b> Allí
    /// los históricos ya dicen <c>staff</c>, y esta migración no puede distinguir cuáles
    /// lo eran: se detiene con un error en vez de seguir sobre datos ya reclasificados.
    /// </para>
    /// </remarks>
    public partial class SalesPaymentSourceNullableCandidate20261027 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM sales.__migrations
                        WHERE "MigrationId" = '20261026235423_SalesPaymentSourceCandidate20261026')
                    THEN
                        RAISE EXCEPTION 'Esta base aplicó SalesPaymentSourceCandidate20261026, que reclasificó los pagos históricos como staff. La opción B lo prohíbe y aquí ya no se distingue qué filas eran históricas: requiere un tratamiento decidido por JP antes de continuar.';
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'sales' AND table_name = 'order_payments' AND column_name = 'source')
                    THEN
                        RAISE EXCEPTION 'sales.order_payments ya tiene la columna source sin una migración que la explique. Requiere un tratamiento decidido por JP antes de continuar.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<string>(
                name: "source",
                schema: "sales",
                table: "order_payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_payments_source",
                schema: "sales",
                table: "order_payments",
                sql: "source IN ('staff')");

            // NOT VALID a propósito, y nunca VALIDATE: validarla rechazaría los históricos.
            migrationBuilder.Sql("""
                ALTER TABLE sales.order_payments
                    ADD CONSTRAINT ck_order_payments_source_obligatorio
                    CHECK (source IS NOT NULL) NOT VALID;
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION sales.order_payments_source_inmutable()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF NEW.source IS DISTINCT FROM OLD.source THEN
                        RAISE EXCEPTION 'El origen de un pago registrado no se cambia: un histórico sin origen no se reclasifica.';
                    END IF;
                    RETURN NEW;
                END;
                $$;

                DROP TRIGGER IF EXISTS trg_order_payments_source_inmutable ON sales.order_payments;
                CREATE TRIGGER trg_order_payments_source_inmutable
                    BEFORE UPDATE ON sales.order_payments
                    FOR EACH ROW EXECUTE FUNCTION sales.order_payments_source_inmutable();
                """);

            migrationBuilder.Sql("""
                COMMENT ON COLUMN sales.order_payments.source IS
                    'Quien asienta el pago (staff). No es el medio: eso es method. NULL solo en pagos históricos, anteriores a la columna; nunca se reclasifican.';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_order_payments_source_inmutable ON sales.order_payments;
                DROP FUNCTION IF EXISTS sales.order_payments_source_inmutable();
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_payments_source_obligatorio",
                schema: "sales",
                table: "order_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_payments_source",
                schema: "sales",
                table: "order_payments");

            migrationBuilder.DropColumn(
                name: "source",
                schema: "sales",
                table: "order_payments");
        }
    }
}
