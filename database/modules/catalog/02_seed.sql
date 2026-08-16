-- ============================================================================
-- SILLAR · Módulo M01 Catálogo · Datos semilla
--
-- Requiere que las migraciones de EF Core del módulo ya estén aplicadas
-- (ADR-009: las tablas las crean las migraciones, no este script).
--
--   dotnet ef database update --project backend/Sillar.Modules.Catalog \
--                             --startup-project backend/Sillar.Api
--
-- SIN DATOS DE NEGOCIO (SPEC §6.9). Ni una categoría de ejemplo, ni un
-- producto de muestra: este repositorio contiene el producto, nunca a un
-- cliente. El módulo recién instalado arranca vacío, y la primera pantalla
-- lo dice con una frase útil, no con una tabla en blanco.
--
-- Este script no hace nada hoy. Existe —vacío, pero idempotente— porque el
-- ciclo de módulo lo espera en el mismo sitio en todos: si M01 alguna vez
-- necesita un valor mínimo de verdad (no de ejemplo), va aquí y no en otro
-- lado.
-- ============================================================================

BEGIN;

COMMIT;
