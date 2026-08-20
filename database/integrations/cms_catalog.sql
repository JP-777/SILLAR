-- ============================================================================
-- SILLAR · Integración blanda M02 CMS → M01 Catálogo
--
-- Se ejecuta solo cuando ambos módulos están instalados. La migración base de
-- CMS conserva product_id nullable y sin FK; esta restricción física pertenece
-- exclusivamente a la integración.
-- ============================================================================

DO $$
BEGIN
    IF to_regclass('cms.featured_products') IS NULL THEN
        RAISE EXCEPTION 'No está instalado el módulo cms: falta cms.featured_products';
    END IF;

    IF to_regclass('catalog.products') IS NULL THEN
        RAISE EXCEPTION 'No está instalado el módulo catalog: falta catalog.products';
    END IF;

    -- Un snapshot de una instalación anterior de M01 no debe impedir volver a
    -- activar la integración. Conserva el snapshot y anula solo la referencia.
    UPDATE cms.featured_products AS featured
       SET product_id = NULL
     WHERE featured.product_id IS NOT NULL
       AND NOT EXISTS (
           SELECT 1
             FROM catalog.products AS product
            WHERE product.id = featured.product_id);

    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conname = 'fk_featured_products_product_id'
           AND conrelid = 'cms.featured_products'::regclass)
    THEN
        ALTER TABLE cms.featured_products
            ADD CONSTRAINT fk_featured_products_product_id
            FOREIGN KEY (product_id) REFERENCES catalog.products (id)
            ON DELETE SET NULL;
    END IF;
END
$$;
