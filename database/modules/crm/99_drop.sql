-- ============================================================
-- M04 Clientes — desinstalación
--
-- Elimina únicamente lo perteneciente a CRM:
--   - el schema crm completo (tablas, triggers, funciones,
--     __migrations y la configuración de búsqueda textual
--     crm.spanish_unaccent)
--   - las funciones crm.set_updated_at() y
--     crm.invalidate_customer_email_verification()
--
-- Conserva intactos:
--   - CORE (schema core, tablas, colaciones core.es_ci, core.es_search)
--   - Catalog (schema catalog)
--   - cualquier objeto de otro módulo
--   - las extensiones pg_trgm y unaccent (compartidas; otro módulo
--     puede estar usándolas)
--
-- DROP SCHEMA ... CASCADE elimina las tablas, los triggers (que
-- pertenecen a las tablas), las funciones del schema y la configuración
-- de text search crm.spanish_unaccent. El historial de migraciones
-- crm.__migrations también desaparece.
-- ============================================================


-- ----------------------------------------------------------------------------
-- Guarda de dependencias duras y eliminación — en UN SOLO bloque (C6)
--
-- `DROP SCHEMA ... CASCADE` se lleva en silencio las claves foráneas que otros
-- módulos declararon hacia crm, y reinstalar M04 Clientes no las devuelve: la
-- migración del otro módulo ya consta aplicada. Así que no se desinstala M04 Clientes
-- mientras otro módulo instalado dependa de él de forma dura.
--
-- Dos señales, ninguna con nombres de módulo escritos aquí:
--   1. claves foráneas de otro schema que apuntan a crm —lo que CASCADE
--      destruiría—;
--   2. módulos registrados en core.module_dependencies con dependencia dura
--      sobre crm cuyo schema sigue existiendo, aunque ya no tengan FK.
--      Supone schema = código de módulo, que se cumple en todos los módulos.
--
-- La comprobación y el DROP van dentro del MISMO bloque a propósito: psql sin
-- ON_ERROR_STOP sigue con la sentencia siguiente tras un error, y con el DROP
-- fuera del bloque la guarda solo pararía a quien lo lanzara bien.
--
-- LÍMITE, dicho sin adorno: esto protege ESTE script. Un
-- `DROP SCHEMA crm CASCADE` escrito a mano no pasa por aquí y nada lo
-- impide. Ver docs/modules/b2b/C6-AUDITORIA-M07.md.
-- ----------------------------------------------------------------------------
DO $guarda$
DECLARE
    objetivo     constant text := 'crm';
    por_fk       text;
    por_registro text;
BEGIN
    SELECT string_agg(DISTINCT dn.nspname, ', ')
      INTO por_fk
      FROM pg_constraint c
      JOIN pg_class     d  ON d.oid  = c.conrelid
      JOIN pg_namespace dn ON dn.oid = d.relnamespace
      JOIN pg_class     r  ON r.oid  = c.confrelid
      JOIN pg_namespace rn ON rn.oid = r.relnamespace
     WHERE c.contype = 'f'
       AND rn.nspname = objetivo
       AND dn.nspname <> objetivo;

    IF to_regclass('core.module_dependencies') IS NOT NULL AND to_regclass('core.modules') IS NOT NULL THEN
        SELECT string_agg(DISTINCT m.code, ', ')
          INTO por_registro
          FROM core.module_dependencies md
          JOIN core.modules m ON m.module_id = md.module_id
          JOIN core.modules t ON t.module_id = md.depends_on_module_id
         WHERE md.kind = 'hard'
           AND t.code = objetivo
           AND EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = m.code);
    END IF;

    IF por_fk IS NOT NULL OR por_registro IS NOT NULL THEN
        RAISE EXCEPTION 'No se desinstala M04 Clientes: hay módulos instalados que dependen de él de forma dura (%). Desinstala antes esos módulos con su 99_drop.sql y repite. No se ha borrado nada.',
            concat_ws('; ', 'con claves foráneas hacia crm: ' || por_fk, 'registrados como dependencia dura: ' || por_registro)
            USING ERRCODE = 'dependent_objects_still_exist';
    END IF;

    EXECUTE 'DROP SCHEMA IF EXISTS crm CASCADE';
END
$guarda$;
