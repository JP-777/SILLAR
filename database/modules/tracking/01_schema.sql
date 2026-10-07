-- M06 · comprobación idempotente del schema.
--
-- Las migraciones EF son la única fuente de DDL. Este archivo no mantiene
-- una segunda copia de CREATE TABLE: verifica que migrate() materializó
-- completamente las piezas físicas de Tracking.

DO $m06_schema$
DECLARE
    faltan text[] := ARRAY[]::text[];
BEGIN
    IF to_regclass('tracking.order_tracking') IS NULL THEN
        faltan := array_append(faltan, 'tracking.order_tracking');
    END IF;

    IF to_regclass('tracking.tracking_notes') IS NULL THEN
        faltan := array_append(faltan, 'tracking.tracking_notes');
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conname = 'ck_order_tracking_priority'
           AND conrelid = to_regclass('tracking.order_tracking')
    ) THEN
        faltan := array_append(faltan, 'ck_order_tracking_priority');
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM pg_class i
          JOIN pg_index ix
            ON ix.indexrelid = i.oid
          JOIN pg_class t
            ON t.oid = ix.indrelid
          JOIN pg_namespace n
            ON n.oid = t.relnamespace
         WHERE n.nspname = 'tracking'
           AND t.relname = 'order_tracking'
           AND i.relname = 'uq_order_tracking_service_order_id'
           AND ix.indisunique
    ) THEN
        faltan := array_append(
            faltan,
            'uq_order_tracking_service_order_id'
        );
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint c
          JOIN pg_class child
            ON child.oid = c.conrelid
          JOIN pg_namespace child_ns
            ON child_ns.oid = child.relnamespace
          JOIN pg_class parent
            ON parent.oid = c.confrelid
          JOIN pg_namespace parent_ns
            ON parent_ns.oid = parent.relnamespace
         WHERE c.contype = 'f'
           AND c.conname = 'fk_tracking_notes_order_tracking_id'
           AND child_ns.nspname = 'tracking'
           AND child.relname = 'tracking_notes'
           AND parent_ns.nspname = 'tracking'
           AND parent.relname = 'order_tracking'
           AND c.confdeltype = 'r'
    ) THEN
        faltan := array_append(
            faltan,
            'fk_tracking_notes_order_tracking_id'
        );
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint c
          JOIN pg_class child
            ON child.oid = c.conrelid
          JOIN pg_namespace child_ns
            ON child_ns.oid = child.relnamespace
          JOIN pg_class parent
            ON parent.oid = c.confrelid
          JOIN pg_namespace parent_ns
            ON parent_ns.oid = parent.relnamespace
         WHERE c.contype = 'f'
           AND c.conname = 'fk_order_tracking_service_order_id'
           AND child_ns.nspname = 'tracking'
           AND child.relname = 'order_tracking'
           AND parent_ns.nspname = 'service_orders'
           AND parent.relname = 'service_orders'
           AND c.confdeltype = 'r'
    ) THEN
        faltan := array_append(
            faltan,
            'fk_order_tracking_service_order_id'
        );
    END IF;

    IF to_regprocedure('tracking.set_updated_at()') IS NULL THEN
        faltan := array_append(faltan, 'tracking.set_updated_at()');
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM pg_trigger t
          JOIN pg_class c
            ON c.oid = t.tgrelid
          JOIN pg_namespace n
            ON n.oid = c.relnamespace
         WHERE NOT t.tgisinternal
           AND n.nspname = 'tracking'
           AND c.relname = 'order_tracking'
           AND t.tgname = 'trg_order_tracking_set_updated_at'
    ) THEN
        faltan := array_append(
            faltan,
            'trg_order_tracking_set_updated_at'
        );
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM pg_trigger t
          JOIN pg_class c
            ON c.oid = t.tgrelid
          JOIN pg_namespace n
            ON n.oid = c.relnamespace
         WHERE NOT t.tgisinternal
           AND n.nspname = 'tracking'
           AND c.relname = 'tracking_notes'
           AND t.tgname = 'trg_tracking_notes_set_updated_at'
    ) THEN
        faltan := array_append(
            faltan,
            'trg_tracking_notes_set_updated_at'
        );
    END IF;

    IF cardinality(faltan) > 0 THEN
        RAISE EXCEPTION
            'M06 está en setup/seed pero migrate() no materializó: %',
            array_to_string(faltan, ', ')
            USING ERRCODE = 'undefined_table';
    END IF;
END
$m06_schema$;
