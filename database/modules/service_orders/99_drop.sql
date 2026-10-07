-- M05b · desinstalación idempotente y aislada.
-- La guarda y todo el desmontaje viven en una sola sentencia transaccional.
-- No usa CASCADE: un dependiente desconocido provoca error y revierte el bloque.
DO $m05b_drop$
DECLARE
    objetivo constant text := 'service_orders';
    por_fk text;
    por_registro text;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = objetivo) THEN
        RETURN;
    END IF;

    SELECT string_agg(DISTINCT dn.nspname, ', ')
      INTO por_fk
      FROM pg_constraint c
      JOIN pg_class d ON d.oid = c.conrelid
      JOIN pg_namespace dn ON dn.oid = d.relnamespace
      JOIN pg_class r ON r.oid = c.confrelid
      JOIN pg_namespace rn ON rn.oid = r.relnamespace
     WHERE c.contype = 'f'
       AND rn.nspname = objetivo
       AND dn.nspname <> objetivo;

    IF to_regclass('core.module_dependencies') IS NOT NULL
       AND to_regclass('core.modules') IS NOT NULL THEN
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
        RAISE EXCEPTION 'No se desinstala M05b Servicios — Órdenes: hay dependientes duros instalados (%). Desinstálalos antes; no se ha borrado nada.',
            concat_ws('; ', 'FK desde: ' || por_fk, 'registro modular: ' || por_registro)
            USING ERRCODE = 'dependent_objects_still_exist';
    END IF;

    DROP TABLE IF EXISTS service_orders.service_order_assignment_events;
    DROP TABLE IF EXISTS service_orders.service_order_items;
    DROP TABLE IF EXISTS service_orders.service_order_status_history;
    DROP TABLE IF EXISTS service_orders.service_order_series;
    DROP TABLE IF EXISTS service_orders.service_orders;
    DROP TABLE IF EXISTS service_orders.__migrations;
    DROP FUNCTION IF EXISTS service_orders.set_updated_at();
    DROP SCHEMA service_orders;
END
$m05b_drop$;
