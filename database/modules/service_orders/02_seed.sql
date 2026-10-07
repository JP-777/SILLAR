-- SIN CONTENIDO DE NEGOCIO: M05b no inventa órdenes, personal ni estados de demostración.
-- Su presencia en seed es deliberada: primero acredita que migrate() creó las
-- cinco tablas y después confirma una transacción vacía e idempotente.
BEGIN;

DO $m05b_seed$
BEGIN
    IF to_regclass('service_orders.service_orders') IS NULL
       OR to_regclass('service_orders.service_order_items') IS NULL
       OR to_regclass('service_orders.service_order_assignment_events') IS NULL
       OR to_regclass('service_orders.service_order_status_history') IS NULL
       OR to_regclass('service_orders.service_order_series') IS NULL THEN
        RAISE EXCEPTION 'No se puede sembrar M05b: sus migraciones no están completas.'
            USING ERRCODE = 'undefined_table';
    END IF;
END
$m05b_seed$;

COMMIT;
