-- ============================================================================
-- SILLAR · Módulo CORE · Desinstalación
--
-- ATENCIÓN: CORE no es un módulo desmontable. Es la base sobre la que se
-- enchufa todo lo demás y en una instalación real nunca se ejecuta esto.
-- Este script existe para reiniciar el entorno de desarrollo desde cero.
--
-- Borra el schema completo: administradores, sesiones, configuración,
-- metadatos de archivos, auditoría y el historial de migraciones del módulo.
-- Los binarios del volumen de medios NO se tocan (ADR-011); quedan en disco.
--
-- Uso:
--   docker compose exec -T db psql -U postgres -d sillar_dev \
--     -f /scripts/modules/core/99_drop.sql
--
-- Idempotente: ejecutarlo sobre una base que ya no tiene el schema no falla.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- Integraciones
--
-- CORE no participa en ninguna integración: nadie depende de él de forma
-- blanda y él no depende de nadie (SPEC §3 y §4.10). No hay ningún
-- database/integrations/*_drop.sql que ejecutar antes.
-- ----------------------------------------------------------------------------

-- ----------------------------------------------------------------------------
-- Aviso: qué otros módulos siguen instalados
--
-- Todos dependen de CORE de forma dura. No los rompe a nivel de esquema
-- —no existen claves foráneas hacia core (SPEC §4.10)—, pero se quedan sin
-- configuración, sin autenticación y sin registro de activación.
-- ----------------------------------------------------------------------------
DO $$
DECLARE
    remaining text;
BEGIN
    SELECT string_agg(nspname, ', ' ORDER BY nspname)
      INTO remaining
      FROM pg_namespace
     WHERE nspname IN ('catalog', 'cms', 'crm', 'sales', 'services',
                       'b2b', 'tracking', 'portal', 'inventory', 'reporting');

    IF remaining IS NOT NULL THEN
        RAISE NOTICE 'Se eliminará CORE con estos módulos aún instalados: %', remaining;
        RAISE NOTICE 'Quedarán sin configuración ni autenticación hasta reinstalar CORE.';
    END IF;
END
$$;

-- ----------------------------------------------------------------------------
-- Eliminación
--
-- CASCADE arrastra tablas, secuencias de identidad, la función
-- core.set_updated_at() con sus triggers, la colación core.es_ci y la tabla
-- core.__migrations. Desinstalar un módulo es soltar su schema: no queda
-- rastro y una reinstalación parte de cero (ADR-009).
-- ----------------------------------------------------------------------------
DROP SCHEMA IF EXISTS core CASCADE;

-- ----------------------------------------------------------------------------
-- Verificación
--
--   SELECT nspname FROM pg_namespace WHERE nspname = 'core';   -- 0 filas
--
-- Para reinstalar:
--   dotnet ef database update --project backend/Sillar.Core \
--                             --startup-project backend/Sillar.Api
--   psql ... -f database/modules/core/02_seed.sql
-- ----------------------------------------------------------------------------
