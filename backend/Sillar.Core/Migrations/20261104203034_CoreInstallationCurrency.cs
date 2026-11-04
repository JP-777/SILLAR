using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Core.Migrations
{
    /// <summary>
    /// Prepara la clave privada de la moneda de la instalación, sin moneda.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Solo datos: el modelo no cambia. Idempotente: en una instalación que ya
    /// tenga la clave no toca su valor, sea cual sea. A ninguna instalación se le
    /// atribuye una moneda: la clave nace en <c>PENDIENTE_DEFINIR</c>, que el
    /// lector trata como «sin moneda», y solo la operación administrativa de
    /// super_admin la establece. <c>currency_code</c>, público, no se lee ni se toca.
    /// </para>
    /// <para>
    /// La visibilidad se fuerza a privada también en una fila que ya existiera.
    /// </para>
    /// </remarks>
    public partial class CoreInstallationCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO core.site_settings
                    (setting_key, setting_value, value_type, description, is_public, is_active)
                VALUES
                    ('installation.currency_iso4217', 'PENDIENTE_DEFINIR', 'text',
                     'Moneda de la instalación (ISO 4217). Autoridad monetaria; solo super_admin', false, true)
                ON CONFLICT (setting_key) DO NOTHING;

                UPDATE core.site_settings
                SET is_public = false
                WHERE setting_key = 'installation.currency_iso4217';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se borra la clave al bajar: podría tener una moneda establecida, y
            // su registro de auditoría seguiría apuntando a ella. Las versiones
            // anteriores toleran claves extra.
        }
    }
}
