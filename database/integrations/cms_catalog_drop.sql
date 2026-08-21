-- ============================================================================
-- SILLAR · Retirada de la integración blanda M02 CMS → M01 Catálogo
--
-- Antes de retirar la FK se anulan todas las referencias vivas. Los snapshots
-- product_name, product_slug, image_id, precio, categoría y estado público
-- permanecen, así que el contenido editorial sobrevive sin apuntar a filas
-- que dejarán de existir.
--
-- Idempotente y seguro si cms ya fue desinstalado.
-- ============================================================================

DO $$
BEGIN
    IF to_regclass('cms.featured_products') IS NOT NULL THEN
        UPDATE cms.featured_products
           SET product_id = NULL
         WHERE product_id IS NOT NULL;

        ALTER TABLE cms.featured_products
            DROP CONSTRAINT IF EXISTS fk_featured_products_product_id;
    END IF;
END
$$;
