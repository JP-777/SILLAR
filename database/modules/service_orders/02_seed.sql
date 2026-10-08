-- SIN CONTENIDO DE NEGOCIO: M05b no inventa órdenes, personal ni estados de demostración.
-- Su presencia en seed es deliberada: primero acredita que migrate() creó las
-- cinco tablas y registra la metadata de configuración que M05b necesita.
-- La etiqueta de serie no tiene valor universal: cada nodo debe configurarla.
-- Por eso nace con el marcador canónico PENDIENTE_DEFINIR y permanece privada.
-- No es un dato de dominio ni un valor por defecto de numeración.
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

INSERT INTO core.site_settings
    (setting_key, setting_value, value_type, description, is_public, is_active)
VALUES
    (
        'service_orders.series_label',
        'PENDIENTE_DEFINIR',
        'text',
        'Etiqueta visible de la serie anual de órdenes de servicio para este nodo',
        false,
        true
    )
ON CONFLICT (setting_key) DO NOTHING;

COMMIT;
