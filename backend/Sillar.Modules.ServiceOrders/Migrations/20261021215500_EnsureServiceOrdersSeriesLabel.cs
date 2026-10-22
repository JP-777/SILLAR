using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sillar.Modules.ServiceOrders.Data;

#nullable disable

namespace Sillar.Modules.ServiceOrders.Migrations;

/// <summary>
/// Asegura el ajuste obligatorio de numeración en la instalación HTTP.
/// La API aplica migraciones; el arnés de restauración además ejecuta 02_seed.sql.
/// Ambos caminos deben dejar exactamente el mismo ajuste privado y pendiente.
/// </summary>
[DbContext(typeof(ServiceOrdersDbContext))]
[Migration("20261021215500_EnsureServiceOrdersSeriesLabel")]
public partial class EnsureServiceOrdersSeriesLabel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO core.site_settings
                (setting_key, setting_value, value_type, description, is_public, is_active)
            VALUES
                ('service_orders.series_label',
                 'PENDIENTE_DEFINIR',
                 'text',
                 'Etiqueta visible de la serie anual de órdenes de servicio para este nodo',
                 false, true)
            ON CONFLICT (setting_key) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // NO borrar una configuración del negocio en un downgrade.
        // Es un ajuste persistente administrado por cada instalación.
    }
}
