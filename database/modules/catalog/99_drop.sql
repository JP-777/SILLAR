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
-- Aviso: qué otros módulos dependen de M01 de forma DURA
--
-- M03 Ventas, M09 Inventario, M13 Punto de Venta y M15 Compras no pueden
-- funcionar sin catálogo. Si alguno está instalado, CASCADE se lleva también
-- la clave foránea que ese módulo declaró hacia catalog en su propia
-- migración —la tabla del otro módulo no desaparece, se queda con la columna
-- huérfana—. El instalador ya impide activar M01 mientras algo dependa de él
-- de forma dura; este aviso es para quien ejecute el script a mano.
-- ----------------------------------------------------------------------------
DO $$
DECLARE
    remaining text;
BEGIN
    SELECT string_agg(nspname, ', ' ORDER BY nspname)
      INTO remaining
      FROM pg_namespace
     WHERE nspname IN ('sales', 'inventory', 'pos', 'purchasing');

    IF remaining IS NOT NULL THEN
        RAISE NOTICE 'Se eliminará catalog con estos módulos aún instalados: %', remaining;
        RAISE NOTICE 'Sus claves foráneas hacia catalog se pierden; sus tablas, no.';
    END IF;
END
$$;

-- ----------------------------------------------------------------------------
-- Eliminación
--
-- CASCADE arrastra tablas, la función catalog.set_updated_at() con sus
-- triggers y la tabla catalog.__migrations. NO arrastra pg_trgm ni las
-- colaciones core.es_ci/core.es_search: son compartidas y viven en core.
-- Desinstalar un módulo es soltar su schema: no queda rastro y una
-- reinstalación parte de cero (ADR-009).
-- ----------------------------------------------------------------------------
DROP SCHEMA IF EXISTS catalog CASCADE;

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
