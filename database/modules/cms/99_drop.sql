-- ============================================================================
-- SILLAR · Módulo M02 Contenido Web · Desinstalación
--
-- Suelta únicamente el schema cms, incluido su historial de migraciones.
-- Las FK hacia core.media_assets viven en tablas de cms y desaparecen con
-- ellas; ni core ni catalog se modifican.
--
-- Idempotente: ejecutarlo cuando cms ya no existe no falla.
-- ============================================================================

DROP SCHEMA IF EXISTS cms CASCADE;
