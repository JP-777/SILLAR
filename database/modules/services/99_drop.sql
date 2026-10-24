-- M05a · desmontaje aislado y fail-closed de services.
-- La operación entera es transaccional: un error revierte incluso DROP TABLE previos.
-- Nunca CASCADE. Los datos/objetos de módulos ajenos no se borran aquí.
DO $m05a_drop$
DECLARE
    objetivo CONSTANT text := 'services';
    dependientes_conocidos text;
    por_fk text;
    por_registro text;
BEGIN
    -- Repetir una desinstalación ya terminada no es error.
    IF NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = objetivo) THEN
        RETURN;
    END IF;

    -- M05b captura snapshots de M05a: no existe necesariamente una FK física.
    -- Una restauración incompleta de CORE tampoco autoriza a borrar esos datos.
    SELECT string_agg(nspname, ', ' ORDER BY nspname)
      INTO dependientes_conocidos
      FROM pg_namespace
     WHERE nspname IN ('service_orders', 'tracking');

    -- Dependencias físicas desde cualquier otro schema hacia services.
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

    -- Sin las tablas de CORE no se puede demostrar que la baja sea segura.
    IF to_regclass('core.modules') IS NULL
       OR to_regclass('core.module_dependencies') IS NULL THEN
        RAISE EXCEPTION
          'No se desinstala M05a: falta metadata CORE para verificar dependencias. No se ha borrado nada.'
          USING ERRCODE = 'dependent_objects_still_exist';
    END IF;

    SELECT string_agg(DISTINCT m.code, ', ')
      INTO por_registro
      FROM core.module_dependencies md
      JOIN core.modules m ON m.module_id = md.module_id
      JOIN core.modules t ON t.module_id = md.depends_on_module_id
     WHERE md.kind = 'hard'
       AND t.code = objetivo
       AND EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = m.code);

    IF dependientes_conocidos IS NOT NULL
       OR por_fk IS NOT NULL
       OR por_registro IS NOT NULL THEN
        RAISE EXCEPTION
          'No se desinstala M05a: dependientes instalados (%). Desinstálalos primero; no se ha borrado nada.',
          concat_ws('; ',
              'schemas conocidos: ' || dependientes_conocidos,
              'FK desde: ' || por_fk,
              'registro modular: ' || por_registro)
          USING ERRCODE = 'dependent_objects_still_exist';
    END IF;

    DROP TABLE IF EXISTS services.service_entries;
    DROP TABLE IF EXISTS services.__migrations;
    DROP FUNCTION IF EXISTS services.set_updated_at();
    -- RESTRICT es el comportamiento por defecto: objeto no contemplado => ERROR,
    -- nunca destrucción colateral ni borrado parcial de la transacción DO.
    DROP SCHEMA services RESTRICT;
END
$m05a_drop$;
