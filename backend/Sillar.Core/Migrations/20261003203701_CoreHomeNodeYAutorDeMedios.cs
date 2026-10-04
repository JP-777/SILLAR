using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Core.Migrations
{
    /// <summary>
    /// Nodo de pertenencia de las cuentas y fotografía del autor de los medios.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Qué corrige.</b> <c>core.media_assets</c> se replica (ADR-018) y
    /// <c>core.admin_users</c> no, pero <c>fk_media_assets_created_by</c> unía las
    /// dos: la fila viajaba y su <c>created_by = 7</c> apuntaba en el otro nodo a
    /// otra persona o a nadie. Es el renglón prohibido de la tabla de la ADR-018.
    /// La salida es la que la propia ADR describe: fotografía del autor y sin FK.
    /// </para>
    /// <para>
    /// <b>Orden obligatorio, y es el de esta migración:</b> <c>admin_users</c>
    /// completo primero —columna, relleno, cero nulos, NOT NULL— y
    /// <c>media_assets</c> después, porque la fotografía copia
    /// <c>admin_users.home_node</c>. Toda la migración corre en una transacción:
    /// si algo aborta, no queda nada a medias.
    /// </para>
    /// <para>
    /// <b>El nodo del relleno no está escrito aquí.</b> Llega en la conexión como
    /// <c>sillar.node_code</c> (<c>NodoParaMigrar</c>), sale de
    /// <c>Sillar:Node:Code</c> y no tiene valor por defecto. Sin él, si hay alguna
    /// cuenta que rellenar, la migración aborta. Con la tabla vacía —instalación
    /// limpia— no hace falta.
    /// </para>
    /// <para>
    /// Detalle y decisiones: <c>docs/modules/core/ENTREGA-05-NODO-Y-AUTORIA.md</c>.
    /// </para>
    /// </remarks>
    public partial class CoreHomeNodeYAutorDeMedios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ================================================================
            // 1 · admin_users.home_node, COMPLETO antes de tocar media_assets.
            // ================================================================

            // 1a · Nula primero: hay que poder añadirla sobre filas existentes
            // sin inventarles un valor.
            migrationBuilder.AddColumn<string>(
                name: "home_node",
                schema: "core",
                table: "admin_users",
                type: "text",
                nullable: true);

            // 1b · Relleno desde el nodo configurado. Sin nodo y con cuentas,
            // aborta: un relleno no adivina de dónde es una cuenta.
            //
            // El trigger de updated_at se suspende durante el relleno: añadir
            // una columna que faltaba no es una modificación de la cuenta.
            migrationBuilder.Sql(
                """
                ALTER TABLE core.admin_users DISABLE TRIGGER trg_admin_users_set_updated_at;

                DO $relleno$
                DECLARE
                    nodo       text := nullif(btrim(current_setting('sillar.node_code', true)), '');
                    pendientes bigint;
                BEGIN
                    SELECT count(*) INTO pendientes FROM core.admin_users WHERE home_node IS NULL;

                    IF pendientes > 0 AND nodo IS NULL THEN
                        RAISE EXCEPTION
                            'No se puede rellenar core.admin_users.home_node: hay % cuenta(s) y esta conexión no trae el nodo configurado.',
                            pendientes
                            USING ERRCODE = 'invalid_parameter_value',
                                  HINT = 'Configura Sillar:Node:Code (variable Sillar__Node__Code) en la instalación que aplica la migración y repítela. No se ha cambiado nada.';
                    END IF;

                    UPDATE core.admin_users SET home_node = nodo WHERE home_node IS NULL;
                END
                $relleno$;

                ALTER TABLE core.admin_users ENABLE TRIGGER trg_admin_users_set_updated_at;
                """);

            // 1c · Comprobación explícita de cero nulos antes del NOT NULL. El
            // NOT NULL también fallaría, pero con un mensaje que no dice qué
            // hacer.
            migrationBuilder.Sql(
                """
                DO $comprobacion$
                DECLARE
                    nulos bigint;
                BEGIN
                    SELECT count(*) INTO nulos FROM core.admin_users WHERE home_node IS NULL;

                    IF nulos > 0 THEN
                        RAISE EXCEPTION
                            'core.admin_users.home_node sigue nulo en % fila(s) tras el relleno.',
                            nulos
                            USING ERRCODE = 'not_null_violation',
                                  HINT = 'SELECT admin_user_id, email FROM core.admin_users WHERE home_node IS NULL;';
                    END IF;
                END
                $comprobacion$;
                """);

            // 1d · Ya completa: obligatoria y no vacía.
            migrationBuilder.AlterColumn<string>(
                name: "home_node",
                schema: "core",
                table: "admin_users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_admin_users_home_node_not_empty",
                schema: "core",
                table: "admin_users",
                sql: "btrim(home_node) <> ''");

            // ================================================================
            // 2 · media_assets: fotografía del autor y fuera la FK.
            // ================================================================

            // 2a · Corrupción antes de nada. Un autor NULL es válido —subida
            // sin sesión, o cuenta borrada con la antigua ON DELETE SET NULL—,
            // pero un autor que no existe en admin_users no tiene nombre ni
            // nodo que fotografiar, y no se inventan. Solo puede existir si
            // alguien retiró la FK a mano.
            migrationBuilder.Sql(
                """
                DO $corrupcion$
                DECLARE
                    corruptas bigint;
                BEGIN
                    SELECT count(*) INTO corruptas
                      FROM core.media_assets m
                     WHERE m.created_by IS NOT NULL
                       AND NOT EXISTS (SELECT 1 FROM core.admin_users a WHERE a.admin_user_id = m.created_by);

                    IF corruptas > 0 THEN
                        RAISE EXCEPTION
                            'core.media_assets tiene % fila(s) cuyo autor (created_by) no existe en core.admin_users. No se puede fotografiar un autor que no está.',
                            corruptas
                            USING ERRCODE = 'foreign_key_violation',
                                  HINT = 'SELECT m.media_asset_id, m.created_by FROM core.media_assets m WHERE m.created_by IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.admin_users a WHERE a.admin_user_id = m.created_by); — decide qué hacer con cada una y repite la migración. No se ha cambiado nada.';
                    END IF;
                END
                $corrupcion$;
                """);

            // 2b · Fuera la FK y su índice. IF EXISTS: una base donde alguien
            // la retiró a mano tiene que poder llegar hasta la comprobación de
            // arriba y, sin corrupción, seguir.
            migrationBuilder.Sql(
                """
                ALTER TABLE core.media_assets DROP CONSTRAINT IF EXISTS fk_media_assets_created_by;
                DROP INDEX IF EXISTS core.idx_media_assets_created_by;
                """);

            // 2c · El entero pasa a ser lo que es: el identificador LOCAL.
            migrationBuilder.RenameColumn(
                name: "created_by",
                schema: "core",
                table: "media_assets",
                newName: "created_by_admin_user_local_id");

            migrationBuilder.AddColumn<string>(
                name: "created_by_admin_user_name",
                schema: "core",
                table: "media_assets",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by_admin_user_home_node",
                schema: "core",
                table: "media_assets",
                type: "text",
                nullable: true);

            // 2d · Fotografía de las filas existentes. El nodo sale de
            // admin_users.home_node —completo desde el paso 1—, NO de
            // media_assets.origin_node, que es donde nació el archivo y no a
            // qué nodo pertenece quien lo subió.
            migrationBuilder.Sql(
                """
                ALTER TABLE core.media_assets DISABLE TRIGGER trg_media_assets_set_updated_at;

                UPDATE core.media_assets m
                   SET created_by_admin_user_name      = a.full_name,
                       created_by_admin_user_home_node = a.home_node
                  FROM core.admin_users a
                 WHERE a.admin_user_id = m.created_by_admin_user_local_id;

                ALTER TABLE core.media_assets ENABLE TRIGGER trg_media_assets_set_updated_at;
                """);

            migrationBuilder.CreateIndex(
                name: "idx_media_assets_created_by_admin_user",
                schema: "core",
                table: "media_assets",
                columns: new[] { "created_by_admin_user_home_node", "created_by_admin_user_local_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_assets_autor_completo",
                schema: "core",
                table: "media_assets",
                sql: "(created_by_admin_user_local_id IS NULL AND created_by_admin_user_name IS NULL AND created_by_admin_user_home_node IS NULL) OR (created_by_admin_user_local_id IS NOT NULL AND created_by_admin_user_name IS NOT NULL AND created_by_admin_user_home_node IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_assets_autor_home_node_no_vacio",
                schema: "core",
                table: "media_assets",
                sql: "created_by_admin_user_home_node IS NULL OR btrim(created_by_admin_user_home_node) <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_assets_autor_local_id_positivo",
                schema: "core",
                table: "media_assets",
                sql: "created_by_admin_user_local_id IS NULL OR created_by_admin_user_local_id > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_assets_autor_nombre_no_vacio",
                schema: "core",
                table: "media_assets",
                sql: "created_by_admin_user_name IS NULL OR btrim(created_by_admin_user_name) <> ''");

            // 2e · Inmutable una vez escrita. Se crea DESPUÉS del relleno, que
            // es la única escritura legítima de la fotografía sobre filas ya
            // existentes. La aplicación la escribe en el INSERT (MediaStorage).
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION core.media_assets_autor_inmutable()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF NEW.created_by_admin_user_local_id  IS DISTINCT FROM OLD.created_by_admin_user_local_id
                    OR NEW.created_by_admin_user_name      IS DISTINCT FROM OLD.created_by_admin_user_name
                    OR NEW.created_by_admin_user_home_node IS DISTINCT FROM OLD.created_by_admin_user_home_node THEN
                        RAISE EXCEPTION
                            'La fotografía del autor de core.media_assets no se modifica una vez escrita (media_asset_id %).',
                            OLD.media_asset_id
                            USING ERRCODE = 'check_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_media_assets_autor_inmutable
                    BEFORE UPDATE OF created_by_admin_user_local_id,
                                     created_by_admin_user_name,
                                     created_by_admin_user_home_node
                    ON core.media_assets
                    FOR EACH ROW
                    EXECUTE FUNCTION core.media_assets_autor_inmutable();
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Vuelve al esquema anterior. La FK solo se puede restaurar si todos
        /// los autores locales existen en <c>admin_users</c>; si no, falla, que
        /// es lo correcto: restaurarla a ciegas sería reintroducir la referencia
        /// que la ADR-018 prohíbe sobre datos que ya la incumplen.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_media_assets_autor_inmutable ON core.media_assets;
                DROP FUNCTION IF EXISTS core.media_assets_autor_inmutable();
                """);

            migrationBuilder.DropIndex(
                name: "idx_media_assets_created_by_admin_user",
                schema: "core",
                table: "media_assets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_assets_autor_completo",
                schema: "core",
                table: "media_assets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_assets_autor_home_node_no_vacio",
                schema: "core",
                table: "media_assets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_assets_autor_local_id_positivo",
                schema: "core",
                table: "media_assets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_assets_autor_nombre_no_vacio",
                schema: "core",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "created_by_admin_user_home_node",
                schema: "core",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "created_by_admin_user_name",
                schema: "core",
                table: "media_assets");

            migrationBuilder.RenameColumn(
                name: "created_by_admin_user_local_id",
                schema: "core",
                table: "media_assets",
                newName: "created_by");

            migrationBuilder.CreateIndex(
                name: "idx_media_assets_created_by",
                schema: "core",
                table: "media_assets",
                column: "created_by");

            migrationBuilder.AddForeignKey(
                name: "fk_media_assets_created_by",
                schema: "core",
                table: "media_assets",
                column: "created_by",
                principalSchema: "core",
                principalTable: "admin_users",
                principalColumn: "admin_user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.DropCheckConstraint(
                name: "ck_admin_users_home_node_not_empty",
                schema: "core",
                table: "admin_users");

            migrationBuilder.DropColumn(
                name: "home_node",
                schema: "core",
                table: "admin_users");
        }
    }
}
