# Bitácora M02 — Encargo 01, paso 2 (datos)

## Construido

- Proyecto `Sillar.Modules.Cms` con declaración modular y `CmsDbContext` aislado en el schema `cms`.
- Proyecto `Sillar.Modules.Cms.Contracts` vacío a propósito.
- Cinco entidades, configuraciones y migración inicial escrita a mano.
- Seed vacío, desinstalación e integración blanda CMS–Catálogo.
- Proyecto de pruebas de lógica pura para la definición compartida de vigencia.

## Decidido durante el encargo

- **Reversible — referencias obligatorias a medios (superada en el encargo 02):** la primera versión usó `RESTRICT` ante borrado físico. La corrección posterior hizo opcionales todas las imágenes y cambió las cinco FK a `SET NULL`.
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
- La obligatoriedad condicional de `alt_text` quedó resuelta en el encargo 02 antes del paso 3.

---

# Encargo 02 — correcciones previas al paso 3

## Construido

- Se corrigieron entidades, configuraciones, migración inicial escrita a mano y snapshot: todas las imágenes de CMS son nullable y las cinco FK hacia `core.media_assets` usan `ON DELETE SET NULL`.
- `alt_text` pasó a ser nullable en banners, promociones y trabajos destacados. Cada tabla combina un `CHECK` que lo exige cuando hay imagen con otro que rechaza texto vacío si se proporciona.
- Se creó `docs/modules/cms/DATOS.md` como diccionario físico y modelo ER del schema, siguiendo la estructura de la ficha de Catálogo.

## Decidido durante el encargo

- **Reversible — banner con dos imágenes:** cualquiera de `image_desktop_id` o `image_mobile_id` obliga a proporcionar `alt_text`; un banner sin ninguna imagen puede guardarlo nulo.
- **Reversible — texto alternativo vacío:** la condición solicitada distingue `NULL`, pero se conservó además la prohibición previa de cadenas vacías. Así, hacer nullable el campo no permite sustituir la descripción accesible por espacios.
- **Reversible — una sola migración:** se editó `20260820050000_CmsInitial` y su snapshot, sin crear una migración incremental, porque no existe una instalación desplegada que conservar.

## Encontrado roto o discrepante

- JP entregó el SPEC corregido directamente en `docs/modules/cms/SPEC.md`; se conservó byte por byte y se eliminó la copia antigua `docs/modules/SPEC.md` de la ruta equivocada.
- El SPEC corregido conserva dos restos del texto anterior en §4.1 y en la fila `banners.alt_text` de §6.1: allí todavía dice «obligatoria/no nullable», mientras el resto de §6.1 y las reglas 8 y 8b de §10 establecen las imágenes nullable, `ON DELETE SET NULL`, publicación incompleta y `alt_text` condicional. No se editó el archivo mantenido por coordinación; el modelo sigue la corrección expresa del encargo.
- `origin/main` publicó y la rama `m02` integró el commit `ccf939f` con `Sillar.Modules.Catalog.Contracts` e `ISchemaExamples`. El contrato publicado resuelve variantes, pero todavía no expone el slug ni la imagen principal del producto que M02 debe copiar al destacar. Los contratos compartidos de autorización, CSRF y auditoría continúan solo como cambios sin commit en el worktree de M01. No se copiaron ni reprodujeron.

## Verificación de las correcciones

Stack exclusivo: proyecto Compose `sillar_m02`, contenedor `sillar_m02_db`, base `sillar_m02`, puerto host `55442`, volumen `sillar_m02_db_data`.

- Se comprobó el objetivo y después se ejecutó `docker compose down -v`: solo desaparecieron el contenedor, red y volumen cuyo nombre empieza por `sillar_m02`. El clúster se recreó vacío.
- CORE, Catálogo y CMS aplicaron sus migraciones. CMS creó el schema y la migración `20260820050000_CmsInitial` terminó correctamente.
- Las ocho columnas observadas de imagen o texto alternativo fueron nullable y de tipo `uuid`/`text` según corresponde. Las cinco FK de medios mostraron `ON DELETE SET NULL`.
- Sin imagen, las inserciones de banner, promoción y trabajo con `alt_text = NULL` terminaron con código 0.
- Con imagen real y `alt_text = NULL`, las tres inserciones terminaron con código 1 y nombraron su `ck_*_alt_text_si_hay_imagen`.
- Se insertó un medio real, se enlazó desde un banner y se borró desde CORE: el `DELETE` terminó con código 0 y el banner permaneció con `image_desktop_id = NULL`.
- Todas las pruebas de base se ejecutaron en transacciones con `ROLLBACK`; las cinco tablas CMS terminaron con cero filas.
- EF Core respondió que modelo y snapshot no tienen cambios pendientes. `dotnet build` terminó con 0 advertencias y 0 errores; `dotnet test` superó 187 de 187 pruebas.

## Abierto

- Esperar el contrato de Catálogo que exponga nombre, slug e imagen principal del producto, y la publicación de los contratos compartidos de administración; después continuar con los endpoints del paso 3.

## Avance del paso 3 sin contratos de administración

### Construido

- DTO públicos, de administración, creación y actualización para banners, promociones, productos destacados, trabajos y redes sociales. Las respuestas públicas resuelven URL de medios y no exponen sus identificadores; las administrativas conservan ID y URL porque el selector necesita ambos.
- Cinco servicios con listado público, listado y detalle administrativo, creación, actualización, baja lógica y reorden. Productos destacados añade reenlace explícito del snapshot; observar M01 nunca lo actualiza solo.
- `ScheduledCmsEntity` concentra las fechas de las tres entidades programables. `PublicationWindow.CurrentAt<T>` contiene la única expresión de vigencia, traducible por EF Core y compilable para lógica en memoria.
- `OrderPlan` valida el conjunto entero antes de producir una sola asignación. `CmsOrderService` lo aplica con un único `SaveChanges` dentro de una transacción `Serializable`; una lista incompleta, repetida o con un ID inexistente devuelve cero asignaciones.
- Se registraron los servicios en `CmsModule`, sin montar ninguna ruta. Autorización, filtro CSRF y auditoría siguen fuera hasta que sus contratos estén publicados.

### Decidido durante este avance

- **Reversible — alta y orden:** una fila nueva se añade al final; cambiar posiciones siempre usa el endpoint futuro de lista completa. Los DTO de edición no aceptan `display_order` individual.
- **Reversible — baja separada de edición:** los DTO de actualización no incluyen `is_active`; así un `editor` no podrá desactivar por el endpoint de edición y la baja seguirá reservada al endpoint `admin`.
- **Reversible — snapshot de producto:** mientras llega el contrato de M01, el servicio recibe como argumentos internos el nombre, slug e imagen ya resueltos. No declara un contrato espejo ni consulta tablas de Catálogo; el endpoint futuro extraerá esos valores de `ProductPickerItem`.
- **Reversible — orden concurrente:** se eligió aislamiento `Serializable`. Si otra petición cambia la sección durante el reorden, el servicio devuelve un conflicto que pide recargar en vez de confirmar un orden calculado sobre una lista vieja.
- **Reversible — medio dado de baja:** un banner público exige que `IMediaStorage` todavía resuelva su imagen de escritorio. Aunque una baja lógica de CORE conserve temporalmente el UUID en la fila, la respuesta observable lo trata como banner incompleto y no lo publica.

### Verificación

- `dotnet build backend/Sillar.sln -c Release --no-restore`: 0 advertencias y 0 errores.
- `dotnet test backend/Sillar.sln -c Release --no-build --no-restore`: 27/27 pruebas CMS, 49/49 Shared y 132/132 CORE; 208/208 en total.
- Las pruebas nuevas, con nombres en español y sin base de datos, cubren límites de vigencia, la misma expresión en las tres entidades, fechas inválidas con mensaje útil, enlaces, texto alternativo, plataformas, snapshots pendientes, planes de reorden completos o inválidos, ausencia de IDs de medios en respuestas públicas y ausencia de `is_active`/`display_order` en los DTO de edición.
- EF Core informó que extraer `ScheduledCmsEntity` no produjo cambios pendientes en el modelo ni en el snapshot.

### Pendiente

- El enganche HTTP completo: grupos público/administración, autorización por rol, filtro CSRF, auditoría, comentarios XML de endpoints y Swagger.
- Conectar los argumentos internos del snapshot con el contrato `ProductPickerItem` cuando M01 lo publique; entonces se habilitan búsqueda, alta y reenlace de destacados.
- Verificación por HTTP y contra PostgreSQL del paso 3, incluida interrupción observable del reorden y los controles 403, queda para cuando las rutas puedan montarse con los contratos de CORE.
