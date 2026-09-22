using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Modules.Crm.Migrations
{
    /// <inheritdoc />
    public partial class CrmCustomersEmailTrimAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Antes de volver más estricta una instalación existente, se
            // comprueba el dato. No se hace UPDATE ni Trim aquí: limpiar
            // automáticamente podría fusionar dos identidades que una persona
            // tiene que resolver.
            migrationBuilder.Sql(
                """
                DO $m04$
                DECLARE
                    filas_incompatibles bigint;
                BEGIN
                    SELECT count(*)
                      INTO filas_incompatibles
                      FROM crm.customers
                     WHERE (email COLLATE "C") ~
                           U&'^[\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000]|[\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000]$';

                    IF filas_incompatibles > 0 THEN
                        RAISE EXCEPTION USING
                            ERRCODE = '23514',
                            MESSAGE = format(
                                'No se puede aplicar ck_customers_email_sin_blancos_en_bordes: %s fila(s) de crm.customers tienen blancos al principio o al final que String.Trim() recorta.',
                                filas_incompatibles),
                            HINT = $hint$
                Encuéntralas con:
                SELECT customer_id, email
                  FROM crm.customers
                 WHERE (email COLLATE "C") ~
                       U&'^[\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000]|[\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000]$';
                $hint$;
                    END IF;
                END
                $m04$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_customers_email_sin_blancos_en_bordes",
                schema: "crm",
                table: "customers",
                sql: "(email COLLATE \"C\") !~ U&'^[\\0009\\000A\\000B\\000C\\000D\\0020\\0085\\00A0\\1680\\2000\\2001\\2002\\2003\\2004\\2005\\2006\\2007\\2008\\2009\\200A\\2028\\2029\\202F\\205F\\3000]|[\\0009\\000A\\000B\\000C\\000D\\0020\\0085\\00A0\\1680\\2000\\2001\\2002\\2003\\2004\\2005\\2006\\2007\\2008\\2009\\200A\\2028\\2029\\202F\\205F\\3000]$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_customers_email_sin_blancos_en_bordes",
                schema: "crm",
                table: "customers");
        }
    }
}
