-- M10: baja transaccional y restringida; nunca borra objetos ajenos.
DO $reporting_drop$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'reporting') THEN
        RETURN;
    END IF;
    DROP TABLE IF EXISTS reporting.__migrations RESTRICT;
    -- Un objeto desconocido impide la baja y revierte también el DROP anterior.
    DROP SCHEMA reporting RESTRICT;
END
$reporting_drop$;
