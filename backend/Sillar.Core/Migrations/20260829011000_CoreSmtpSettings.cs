using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Core.Migrations
{
    /// <summary>
    /// Hace configurables las tres claves SMTP no secretas.
    /// La contraseña queda fuera de site_settings y se lee de SILLAR_SMTP_PASSWORD.
    /// </summary>
    public partial class CoreSmtpSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO core.site_settings
                    (setting_key, setting_value, value_type, description, is_public, is_active)
                VALUES
                    ('smtp_server', 'PENDIENTE_DEFINIR', 'text',
                     'Servidor SMTP de correo saliente', false, true),
                    ('smtp_port', '587', 'number',
                     'Puerto del servidor SMTP', false, true),
                    ('smtp_from', 'PENDIENTE_DEFINIR', 'email',
                     'Correo remitente y usuario SMTP', false, true)
                ON CONFLICT (setting_key) DO NOTHING;

                UPDATE core.site_settings
                SET is_public = false
                WHERE setting_key IN ('smtp_server', 'smtp_port', 'smtp_from');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se borran valores SMTP al bajar la migración: podrían contener
            // configuración real. Versiones anteriores toleran claves extra.
        }
    }
}
