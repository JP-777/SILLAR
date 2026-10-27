using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Modules.Sales.Migrations
{
    /// <summary>
    /// CANDIDATA, NO APLICADA. <b>DECISIÓN CANDIDATA, NO RATIFICADA:</b> origen del pago
    /// separado del medio, y reclasificación de los pagos existentes como <c>staff</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Lo que hay que ratificar es el relleno.</b> El argumento: cada fila existente de
    /// <c>order_payments</c> cumple <c>ck_order_payments_atribucion_local_positiva</c> y
    /// <c>ck_order_payments_atribucion_home_node_no_vacio</c>, así que la base ya garantiza
    /// que la registró una persona. Pero aplicada, <b>no se deshace editando un archivo</b>:
    /// por eso va sola, y puede retirarse sin tocar la moneda.
    /// </para>
    /// <para>
    /// El <c>CHECK</c> admite solo <c>staff</c>. Sin valor por defecto: un escritor futuro
    /// tiene que decir el origen, no heredarlo.
    /// </para>
    /// </remarks>
    public partial class SalesPaymentSourceCandidate20261026 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nace anulable, se rellena con lo que los CHECK de atribución ya prueban y
            // solo entonces pasa a NOT NULL. El valor por defecto "" que generaba EF habría
            // violado el CHECK con cualquier fila dentro.
            migrationBuilder.AddColumn<string>(
                name: "source",
                schema: "sales",
                table: "order_payments",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("UPDATE sales.order_payments SET source = 'staff' WHERE source IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "source",
                schema: "sales",
                table: "order_payments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_payments_source",
                schema: "sales",
                table: "order_payments",
                sql: "source IN ('staff')");

            migrationBuilder.Sql("""
                COMMENT ON COLUMN sales.order_payments.source IS
                    'Quien asienta el pago (staff). No es el medio: eso es method.';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
