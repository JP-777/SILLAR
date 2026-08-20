# Bitácora M02 — Encargo 01, paso 2 (datos)

## Construido

- Proyecto `Sillar.Modules.Cms` con declaración modular y `CmsDbContext` aislado en el schema `cms`.
- Proyecto `Sillar.Modules.Cms.Contracts` vacío a propósito.
- Cinco entidades, configuraciones y migración inicial escrita a mano.
- Seed vacío, desinstalación e integración blanda CMS–Catálogo.
- Proyecto de pruebas de lógica pura para la definición compartida de vigencia.

## Decidido durante el encargo

- **Reversible — referencias obligatorias a medios:** `RESTRICT` ante borrado físico; las opcionales usan `SET NULL`. CORE hace baja lógica, por lo que la regla normal de desactivación no depende de un borrado físico.
- **Reversible — retirada de la integración:** `cms_catalog_drop.sql` anula todas las referencias `product_id`, no solo las ya huérfanas. Así un `product_id` válido no se convierte en huérfano durante la desinstalación posterior de M01; los snapshots permanecen.
- **Reversible — promoción sin imagen:** se conservó `alt_text` obligatorio porque el SPEC dice que las promociones tienen los mismos campos que banners salvo la imagen nullable. Es una tensión con la frase «puede ser solo texto» y debe revisarse antes del endpoint de creación.
- **Reversible — formato de enlaces:** las rutas internas empiezan por `/` y las URL absolutas admitidas por el esquema son HTTP o HTTPS.
- **Reversible — metadatos del módulo:** versión inicial `1.0.0` y orden `20`, después de M01 (`10`).

## Encontrado roto o discrepante

- La worktree `m02` nació dos commits antes del cierre de datos de M01 y no contenía ADR-018. Se avanzó por `fast-forward` al baseline local confirmado antes de construir M02.
- El SPEC llegó después del primer intento y apareció también como copia idéntica en `docs/modules/SPEC.md`. La ruta canónica usada y versionada es `docs/modules/cms/SPEC.md`; la copia adicional no se modifica ni se incluye.
- El puerto previsto `55432` ya estaba ocupado. No se detuvo ningún proceso ajeno: el stack exclusivo pasó a `55442`.
- El primer `POST /api/setup` enviado desde PowerShell llevaba una `ó` en la página de códigos de la consola y el host rechazó el JSON no UTF-8 con 500. Repetido con bytes UTF-8 explícitos, respondió 201. No fue un defecto del endpoint ni dejó una instalación parcial.

## Verificación

Stack exclusivo: proyecto Compose `sillar_m02`, contenedor `sillar_m02_db`, base `sillar_m02`, puerto host `55442`, volumen `sillar_m02_db_data`.

- **Migración desde vacío:** CORE, Catálogo y CMS aplicaron sus migraciones. CMS creó `cms.__migrations` y las cinco tablas de negocio.
- **Tipos y relaciones:** las cinco PK observadas fueron `integer`; `image_desktop_id`, `image_mobile_id` y los tres `image_id` fueron `uuid`; `product_id` fue `uuid` nullable. Se observaron cinco FK a `core.media_assets` y ninguna a `catalog.products` en el esquema base. No apareció ninguna columna `origin_node` ni `row_version`.
- **Seed dos veces:** estado antes y después idéntico: cero filas en las cinco tablas CMS y cero cambios en `core.site_settings` (`IDENTICAL=True`).
- **Restricciones negativas:** una vigencia 28-febrero → 1-febrero terminó con código 1 y `ck_banners_vigencia`; un enlace sin etiqueta terminó con código 1 y `ck_banners_enlace`; `Instagram` después de `instagram` terminó con código 1 y `uq_social_links_plataforma`.
- **Integración:** `cms_catalog.sql` creó `fk_featured_products_product_id` con `ON DELETE SET NULL`; la segunda ejecución dejó exactamente una restricción. `cms_catalog_drop.sql` dejó `product_id` nulo, conservó nombre y slug snapshot y dejó cero FK; la segunda ejecución conservó una fila y cero referencias no nulas.
- **Desinstalación de Catálogo:** partiendo de la integración aplicada y una referencia viva, se ejecutó primero su `_drop` de integración y luego `catalog/99_drop.sql`. `catalog` desapareció, `cms` siguió presente y la fila quedó con `product_id` nulo y snapshot intacto.
- **Desinstalación de CMS:** antes y después quedaron exactamente las mismas 7 tablas de Catálogo y 10 de CORE. El conteo del schema `cms` quedó en cero. Dos ejecuciones consecutivas de `99_drop.sql` terminaron correctamente.
- **Reinstalación:** la firma MD5 de columnas, restricciones, índices y triggers de CMS fue `cc04494a8691ddeda832fcbdcd5dbb44` antes del drop y después de reaplicar la migración (`IDENTICAL=True`).
- **Modelo y compilación:** EF informó que no hay cambios pendientes entre modelo y snapshot. `dotnet build` Release terminó con 0 advertencias y 0 errores. La solución ejecutó 187 pruebas: 132 CORE, 49 Shared y 6 CMS; cero fallos.
- **Host inactivo:** descubrió `catalog`, `cms` y `core`; sincronizó CMS como inactivo y arrancó con solo CORE activo. `GET /api/capabilities` respondió 200 con solo `core`. Como control negativo, al activar CMS temporalmente el mismo endpoint sí devolvió `cms`; al terminar, la base quedó otra vez con `cms = false`.

## Abierto y fuera de alcance

- Endpoints y frontend: pertenecen a los pasos 3 y 4; este encargo entrega solo el esquema.
- `Dockerfile`: M02 todavía no viajará en la imagen. Se deja intacto por instrucción de la ADR-019 y se resolverá al fusionar con el trabajo que ya lo modifica.
- Revisar antes del paso 3 si `promotions.alt_text` debe ser nullable cuando `image_id` es null; no se cambió el SPEC ni se inventó una regla distinta en este encargo.
