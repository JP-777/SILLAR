-- ============================================================================
-- SILLAR · Módulo M01 Catálogo · Desinstalación
--
-- Borra el schema completo: categorías, marcas, productos, variantes,
-- asociaciones y galería, con el historial de migraciones del módulo.
-- Los binarios de las imágenes NO se tocan (ADR-011): las fichas de
-- core.media_assets y sus archivos en disco sobreviven, solo dejan de estar
-- asociados a ningún producto.
--
-- Uso:
--   docker compose exec -T db psql -U postgres -d sillar_dev \
--     -f /scripts/modules/catalog/99_drop.sql
--
-- Idempotente: ejecutarlo sobre una base que ya no tiene el schema no falla.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- Integraciones
--
-- M01 no participa en ninguna integración de dependencia blanda: sus cuatro
-- claves foráneas hacia core.media_assets son de dependencia DURA y viven en
-- la propia migración del módulo (SPEC §6.8), no en database/integrations/.
-- No hay ningún *_drop.sql que ejecutar antes.
-- ----------------------------------------------------------------------------

-- ----------------------------------------------------------------------------
-- Qué borra
--
-- CASCADE arrastra tablas, la función catalog.set_updated_at() con sus
-- triggers y la tabla catalog.__migrations. NO arrastra pg_trgm ni las
-- colaciones core.es_ci/core.es_search: son compartidas y viven en core.
-- Desinstalar un módulo es soltar su schema: no queda rastro y una
-- reinstalación parte de cero (ADR-009).
--
-- Antes (hasta el 27/09/2026) aquí había un aviso que solo AVISABA de que se
-- perderían las FK de otros módulos, y luego las perdía. Ahora se rechaza.
-- ----------------------------------------------------------------------------
-- ----------------------------------------------------------------------------
-- Guarda de dependencias duras y eliminación — en UN SOLO bloque (C6)
--
-- `DROP SCHEMA ... CASCADE` se lleva en silencio las claves foráneas que otros
-- módulos declararon hacia catalog, y reinstalar M01 Catálogo no las devuelve: la
-- migración del otro módulo ya consta aplicada. Así que no se desinstala M01 Catálogo
-- mientras otro módulo instalado dependa de él de forma dura.
--
-- Dos señales, ninguna con nombres de módulo escritos aquí:
--   1. claves foráneas de otro schema que apuntan a catalog —lo que CASCADE
--      destruiría—;
--   2. módulos registrados en core.module_dependencies con dependencia dura
--      sobre catalog cuyo schema sigue existiendo, aunque ya no tengan FK.
--      Supone schema = código de módulo, que se cumple en todos los módulos.
--
-- La comprobación y el DROP van dentro del MISMO bloque a propósito: psql sin
-- ON_ERROR_STOP sigue con la sentencia siguiente tras un error, y con el DROP
-- fuera del bloque la guarda solo pararía a quien lo lanzara bien.
--
-- LÍMITE, dicho sin adorno: esto protege ESTE script. Un
-- `DROP SCHEMA catalog CASCADE` escrito a mano no pasa por aquí y nada lo
-- impide. Ver docs/modules/b2b/C6-AUDITORIA-M07.md.
-- ----------------------------------------------------------------------------
DO $guarda$
DECLARE
    objetivo     constant text := 'catalog';
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
        RAISE EXCEPTION 'No se desinstala M01 Catálogo: hay módulos instalados que dependen de él de forma dura (%). Desinstala antes esos módulos con su 99_drop.sql y repite. No se ha borrado nada.',
            concat_ws('; ', 'con claves foráneas hacia catalog: ' || por_fk, 'registrados como dependencia dura: ' || por_registro)
            USING ERRCODE = 'dependent_objects_still_exist';
    END IF;

    EXECUTE 'DROP SCHEMA IF EXISTS catalog CASCADE';
END
$guarda$;

-- ----------------------------------------------------------------------------
-- Verificación
--
--   SELECT nspname FROM pg_namespace WHERE nspname = 'catalog';   -- 0 filas
--
-- Para reinstalar:
--   dotnet ef database update --project backend/Sillar.Modules.Catalog \
--                             --startup-project backend/Sillar.Api
--   psql ... -f database/modules/catalog/02_seed.sql
-- ----------------------------------------------------------------------------
