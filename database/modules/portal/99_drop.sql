-- M08: baja transaccional y restringida; nunca borra objetos ajenos.
DO $portal_drop$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'portal') THEN
        RETURN;
    END IF;
    DROP TABLE IF EXISTS portal.__migrations RESTRICT;
    -- Un objeto desconocido impide la baja y revierte también el DROP anterior.
    DROP SCHEMA portal RESTRICT;
END
$portal_drop$;
