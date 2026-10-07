-- M05b · comprobación idempotente de schema.
--
-- Las migraciones EF son la única fuente de DDL (ADR-009). Este archivo existe
-- para la paridad del arnés y verifica que migrate() no omitió M05b; no mantiene
-- una segunda copia de CREATE TABLE.
DO $m05b_schema$
DECLARE
    faltan text[] := ARRAY[]::text[];
    tabla text;
BEGIN
    FOREACH tabla IN ARRAY ARRAY[
        'service_orders',
        'service_order_items',
        'service_order_assignment_events',
        'service_order_status_history',
        'service_order_series'
    ]
    LOOP
        IF to_regclass(format('service_orders.%I', tabla)) IS NULL THEN
            faltan := array_append(faltan, tabla);
        END IF;
    END LOOP;

    IF cardinality(faltan) > 0 THEN
        RAISE EXCEPTION 'M05b está en setup/seed pero migrate() no materializó: %',
            array_to_string(faltan, ', ')
            USING ERRCODE = 'undefined_table';
    END IF;
END
$m05b_schema$;
