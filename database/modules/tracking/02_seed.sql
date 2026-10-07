-- SIN CONTENIDO DE NEGOCIO.
-- M06 no inventa prioridades, plazos internos ni notas.
-- La presencia de este archivo acredita que migrate() creó ambas tablas y
-- mantiene seed() simétrico con el resto de módulos.

BEGIN;

DO $m06_seed$
BEGIN
    IF to_regclass('tracking.order_tracking') IS NULL
       OR to_regclass('tracking.tracking_notes') IS NULL THEN
        RAISE EXCEPTION
            'No se puede sembrar M06: sus migraciones no están completas.'
            USING ERRCODE = 'undefined_table';
    END IF;
END
$m06_seed$;

COMMIT;
