using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Core.Migrations
{
    /// <summary>
    /// Hace obligatoria <c>core.modules.description</c> y añade las dos
    /// restricciones que el SPEC §4.2 añadió con ella.
    /// </summary>
    /// <remarks>
    /// Generada con <c>dotnet ef migrations add</c> y corregida a mano (ADR-009).
    /// La versión generada rellenaba los nulos con cadena vacía y acto seguido
    /// añadía <c>ck_modules_description_not_empty</c>: en una base con filas
    /// antiguas, la migración se caía contra su propia restricción.
    /// </remarks>
    public partial class ModulesDescriptionRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rellenar antes de prohibir el nulo. El sincronizador reescribe
            // estas filas en el siguiente arranque, así que basta con un valor
            // provisional que no esté vacío: el nombre visible del módulo.
            migrationBuilder.Sql(
                """
                UPDATE core.modules
                   SET description = display_name
                 WHERE description IS NULL OR btrim(description) = '';
                """);

            migrationBuilder.Sql(
                """
                UPDATE core.modules
                   SET display_order = 0
                 WHERE display_order < 0;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                schema: "core",
                table: "modules",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_modules_description_not_empty",
                schema: "core",
                table: "modules",
                sql: "btrim(description) <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "ck_modules_display_order",
                schema: "core",
                table: "modules",
                sql: "display_order >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_modules_description_not_empty",
                schema: "core",
                table: "modules");

            migrationBuilder.DropCheckConstraint(
                name: "ck_modules_display_order",
                schema: "core",
                table: "modules");

            // Volver a permitir el nulo no devuelve los que había: esa
            // información se perdió al rellenarlos, y es irrecuperable.
            migrationBuilder.AlterColumn<string>(
                name: "description",
                schema: "core",
                table: "modules",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);
        }
    }
}
