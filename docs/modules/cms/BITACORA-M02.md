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

---

## Encargo 02 — avance HTTP después de integrar M01 paso 4

### Línea base integrada

- Se fusionó el commit confirmado `e0e96c6` mediante el merge `03c47fd`, sin rehacer la rama ni perder los tres commits propios. `origin/main` seguía apuntando a `ccf939f`, por lo que se usó el SHA exacto entregado por coordinación.
- El único conflicto fue `backend/Sillar.sln`; la resolución conserva los proyectos de Catálogo que llegaron y los tres proyectos de CMS. `Sillar.Api.csproj` se fusionó automáticamente con ambas referencias.
- Antes de escribir código nuevo se restauró la solución y se observó: compilación Release con 0 advertencias y 0 errores; 254/254 pruebas superadas (132 CORE, 54 Shared, 41 Catálogo y 27 CMS).
- El SPEC canónico permanece sin editar, con SHA-256 `A10E6C75D6521A0A2BD88809A0BE3CC9EF7310FA69A50E7DCBB59B8E939E9E2C`; `docs/modules/SPEC.md` no existe y `DATOS.md` conserva únicamente diccionario, ER y objetos físicos.

### Construido

- Se montaron las cinco rutas públicas y las operaciones administrativas de listado, detalle, edición, reorden y baja. Banners, promociones, trabajos y redes incluyen además el alta completa.
- Los grupos administrativos requieren `editor`, todas sus mutaciones pasan por `CsrfEndpointFilter` y cada baja añade la política `admin`. Altas, ediciones, reordenamientos y bajas escriben auditoría de CORE mediante `IAuditWriter`.
- El endpoint público de destacados pide `ICatalogService` al contenedor: si el contrato no existe devuelve una lista vacía; si existe, sirve el snapshot propio sin consultar tablas de Catálogo.
- Todos los handlers montados tienen comentarios XML, nombre, resumen, tags y respuestas Swagger.

### Decidido durante este avance

- **Reversible — auditoría en el borde HTTP:** los servicios CMS siguen sin conocer sesión ni transporte; el endpoint audita solo después de una operación correcta. Se descartó duplicar en cada servicio los argumentos de usuario que ya posee el borde.
- **Reversible — disponibilidad pública de destacados:** se usa la presencia real de `ICatalogService` como señal de que M01 está activo. No se consulta el nombre del módulo ni la tabla de activaciones.
- **Reversible — no inventar el selector de M01:** no se añadió un contrato espejo ni se consultó el schema `catalog`. Se dejaron sin montar búsqueda, alta y reenlace de productos destacados hasta que el dueño de M01 publique el contrato que el SPEC exige.

### Encontrado roto o pendiente fuera de M02

- `e0e96c6` confirmó los cinco contratos compartidos de CORE, incluido `SessionTokens`, pero `Sillar.Modules.Catalog.Contracts` todavía contiene únicamente `ItemSnapshot` e `ICatalogService`. No existe `ProductPickerItem` ni `BuscarParaSeleccionAsync`, aunque el SPEC de M02 los declara necesarios y dice expresamente que M01 todavía no los expone.
- Por esa ausencia, Swagger muestra 34 operaciones CMS con resumen, pero aún no puede mostrar el alta, reenlace y búsqueda administrativa de productos destacados. Implementarlas leyendo M01 por HTTP, por schema o con un tipo local violaría la frontera modular.

### Verificación HTTP y PostgreSQL observada

Stack exclusivo: proyecto Compose `sillar_m02`, contenedor `sillar_m02_db`, base `sillar_m02`, puerto host `55442`; host de prueba en `127.0.0.1:5082`.

- **Línea base y host:** con CMS inactivo, `/api/cms/banners` devolvió 404. Activado y reiniciado, el host declaró `core, cms` activos y `catalog` inactivo. Las cinco rutas públicas devolvieron 200.
- **Vigencia:** se crearon cinco banners. El futuro y el caducado aparecieron en administración con `isCurrent=false`, no en público; el caducado se editó y devolvió 200 conservando `isCurrent=false`. Antes de retirar su medio, público devolvió únicamente `Actual con imagen`.
- **Validación:** imagen presente con `altText=null` devolvió 400 y «Escribe el texto alternativo de la imagen»; sin imagen y texto nulo devolvió 201. Fechas 28-febrero → 1-febrero devolvieron 400 y «La fecha de fin debe ser posterior a la fecha de inicio», no un error de PostgreSQL.
- **Orden atómico:** `editor` creó cinco banners y reordenó `[8,7,6,5,4]`; el listado devolvió exactamente `8,7,6,5,4`. Sustituir un ID por `999999` devolvió 400 y el listado posterior conservó el mismo orden. La auditoría dejó cinco `create:banner` y un `update:banner` de orden.
- **Roles y CSRF:** `editor` obtuvo 403 al desactivar un banner; `super_admin` desactivó promociones, trabajos y redes con 200. Una escritura autenticada sin cabecera CSRF devolvió 403 con la frase que pide `X-CSRF-Token`.
- **Medios:** el alta de un PNG devolvió 201. La baja lógica por `DELETE /api/admin/media/{id}` devolvió 204; el banner dejó de publicarse, administración devolvió `imageDesktopUrl=null` e `isComplete=false`. Como control físico posterior, `DELETE` de esa única fila ya inactiva terminó correctamente y PostgreSQL dejó `cms.banners.image_desktop_id IS NULL = true`.
- **Dependencia blanda:** con Catálogo inactivo, destacados devolvió 200 y `[]`. Con integración y Catálogo activos devolvió el snapshot `Nombre snapshot conservado`. Tras `cms_catalog_drop.sql`, administración devolvió el mismo nombre con `pendingRelink=true` y público devolvió `[]`.
- **Desinstalación de Catálogo:** después de retirar la integración y ejecutar su `99_drop.sql`, quedaron 0 tablas en `catalog`, 10 en `core`, 6 en `cms` (cinco de negocio más historial), cero `product_id` no nulos y el snapshot intacto. El host arrancó con `core,cms` sin advertencias; administración siguió mostrando el pendiente y público devolvió 200 vacío.
- **Rutas restantes:** promociones, trabajos y redes recorrieron por HTTP `201 alta → 200 edición → 200 reorden → 200 baja`; después sus endpoints públicos devolvieron 200 vacíos.
- **M02 desactivado:** tras desactivar y reiniciar, las cinco rutas públicas y `/api/admin/cms/banners` devolvieron 404. `/api/capabilities` devolvió 200 con solo `core`; el arranque declaró Catálogo y CMS inactivos y no emitió advertencias.
- **Swagger:** 20 paths y 34 operaciones CMS montadas, todas con resumen XML; la diferencia hasta el conjunto completo corresponde exclusivamente al selector pendiente de M01.
- **Cierre técnico:** `git diff --check` sin hallazgos; build Release con 0 advertencias y 0 errores; 254/254 pruebas superadas. EF Core respondió «No changes have been made to the model since the last migration».

### Estado al terminar esta vuelta

- CMS queda inactivo en la base exclusiva; su schema y sus datos de verificación permanecen. Catálogo queda inactivo y desinstalado como resultado de la casilla que exigía comprobar su desinstalación.
- Falta que coordinación publique en M01 el contrato de selección descrito por el SPEC. Una vez integrado, se montan búsqueda, alta y reenlace de destacados y se repite Swagger/HTTP para cerrar el paso 3.

---

## Encargo sustitutivo — cierre de lo implementable

### Línea base y corrección

- `git fetch origin` confirmó que `origin/main` sigue en `e0e96c6`, ya contenido en la rama por el merge `03c47fd`; no hubo un segundo merge ni conflictos nuevos.
- Se corrigió la publicación de trabajos destacados: una fila activa solo sale en público cuando conserva una imagen resoluble y texto alternativo. Administración conserva la fila y devuelve `isComplete=false` cuando falta cualquiera de esos elementos.
- La regla de completitud vive en `FeaturedProjectRules` y la respuesta pública hace no nulos `imageUrl` y `altText`, porque una fila incompleta ya no puede alcanzar ese contrato.
- Se añadieron tres pruebas de lógica con nombres en español para trabajo sin imagen, medio inactivo y trabajo completo, más una prueba de contrato para el indicador administrativo.

### Decidido durante este cierre

- **Reversible — completitud del trabajo como regla de aplicación:** se mantuvo nullable la imagen en el esquema y se concentró la publicabilidad en una regla pura. Permite conservar y editar borradores sin imagen sin debilitar el contrato público.
- **Reversible — contrato público estricto:** `FeaturedProjectResponse.ImageUrl` y `AltText` son no nulos. Si en el futuro se decide publicar una tarjeta sin imagen, el cambio queda localizado en la regla, el servicio y ese DTO.
- **Reversible — selector de productos ausente:** se mantienen fuera búsqueda, alta y reenlace administrativo de destacados. No se reprodujo `ProductPickerItem`, no se amplió `ItemSnapshot` y no se tocó Catálogo.

### Verificación adicional observada

Stack exclusivo: proyecto Compose `sillar_m02`, contenedor `sillar_m02_db`, base `sillar_m02`, puerto host `55442`; host HTTP en `127.0.0.1:5082`.

- **Trabajo incompleto:** `POST /api/admin/cms/featured-projects` como `editor` devolvió 201 para el ID 4 con `isComplete=false`. El listado administrativo devolvió ese ID con el mismo indicador; `GET /api/cms/featured-projects` devolvió 200 y no contuvo el ID 4.
- **Enlace inválido:** crear un banner con `linkUrl=/destino` y `linkLabel=null` devolvió 400 con «Escribe el texto que se mostrará en el enlace.». La misma escritura autenticada sin `X-CSRF-Token` devolvió 403.
- **Auditoría de ciclo completo:** el trabajo 4 se creó, editó y desactivó por HTTP con estados 201, 200 y 200. La consulta posterior de `core.audit_log` devolvió exactamente las acciones `create`, `update` y `delete` para `module_code=cms`, `entity_type=featured_project`, `entity_id=4`. Una baja adicional de banner como `super_admin` devolvió 200 y dejó el conjunto observable de auditoría de banners en cinco `create`, dos `update` y un `delete`; promociones, trabajos y redes también conservaron las tres acciones en el registro.
- **Dependencia blanda:** con Catálogo inactivo y sin su schema, `GET /api/cms/featured-products` devolvió 200 y `[]`.
- **Swagger:** en entorno Development devolvió 200, 34 operaciones CMS y cero operaciones sin resumen. Las tres operaciones que faltan para el conjunto del SPEC siguen siendo búsqueda, alta y reenlace dependientes del selector de Catálogo no publicado.
- **M02 inactivo:** después de desactivar y reiniciar, el host declaró solo CORE activo, sin avisos. Las cinco rutas públicas CMS devolvieron 404 y `/api/capabilities` devolvió solo `core`. La consulta final de activaciones dejó `core=true`, `catalog=false` y `cms=false`.
- **Cierre técnico:** EF Core respondió «No changes have been made to the model since the last migration». La compilación Release terminó con 0 advertencias y 0 errores. Se superaron 258/258 pruebas: 132 CORE, 54 Shared, 41 Catálogo y 31 CMS.

### Incidencia durante la verificación

- En un primer reinicio se escribió por error `ConnectionStrings__DefaultConnection` en vez de la clave correcta `ConnectionStrings__Default`. El proceso conservó o cargó una clave correcta desde una fuente que entonces no se identificó, alcanzó otro stack, descubrió CMS y sincronizó su entrada como inactiva; se detuvo inmediatamente y no se envió ninguna petición de negocio. No se intentó revertir esa base porque está fuera del stack autorizado. Toda verificación y toda mutación posteriores usaron explícitamente `127.0.0.1:55442/sillar_m02`.

### Pendiente y congelado

- `Sillar.Modules.Catalog.Contracts` todavía no publica `ProductPickerItem` ni su búsqueda. El paso 3 queda implementado y verificado salvo las tres rutas administrativas que dependen de ese contrato.
- El `Dockerfile` recibido de `main` sigue sin CMS. Permanece intacto por instrucción y queda pendiente de la fusión final; mientras CMS esté inactivo, ADR-019 evita un arranque inconsistente.
- Frontend, E2E y paso 4 no se tocaron.

---

## Encargo 03 — ampliación del snapshot de destacados

### Blindaje del entorno local

- **Corrección del diagnóstico (2026-08-21):** `appsettings.json` nunca tuvo una cadena predeterminada. La cadena añadida localmente a `backend/Sillar.Api/appsettings.Development.json` convertía un error de clave en un arranque silencioso y además duplicaba credenciales fuera de `.env`; como el archivo no contenía nada más, se eliminó entero. `.env` vuelve a ser la única fuente local de `ConnectionStrings__Default`.
- Con `sillar_m02_db` levantada y saludable se cambió temporalmente la clave local por `ConnectionStrings__DefaultConnection`. El host descubrió los tres ensamblados y abortó antes de conectarse con «Falta la cadena de conexión 'Default'. Se define en el archivo .env de la raíz como ConnectionStrings__Default». Después se restauró `.env` y se comprobó su clave correcta.
- Dos controles temporales de la implementación real de `DotEnv` se ejecutaron sin construir el host ni abrir PostgreSQL: el binario de M02 prioriza `C:\sillar-m02\.env` por `AppContext.BaseDirectory` aunque el directorio de trabajo sea `C:\SILLAR`; cuando esa primera búsqueda no encuentra un archivo, el fallback desde `C:\SILLAR\backend` sí resuelve `C:\SILLAR\.env`. Las dos aserciones pasaron y el archivo temporal se retiró. No hay scripts, tareas ni atajos versionados que cambien el directorio de trabajo.
- Después de retirar el fallback, la solución compiló en Release con 0 advertencias y 0 errores y superó 264/264 pruebas: 132 CORE, 54 Shared, 41 Catálogo y 37 CMS.
- **Causa histórica abierta:** `C:\sillar-m02\.env` tiene `CreationTime` 2026-08-20 04:01:35 -05:00; la incidencia quedó registrada en `ac9c64d` el 2026-08-21 01:10:38 -05:00, 21 h 09 min después. Por tanto, se descarta que faltara el `.env` y que `DotEnv` tuviera que caer al directorio de trabajo. Una `ConnectionStrings__Default` heredada explica el efecto y se comprobó que prevalece sobre `.env`, pero la sesión original ya no existe y no hay evidencia de que esa variable estuviera definida entonces.
- Eliminar `appsettings.Development.json` no protege frente a ese mecanismo: una variable heredada sigue teniendo precedencia. Hasta incorporar desde `main` el log de arranque que informa siempre la base de destino, este worktree continúa expuesto a un valor heredado silencioso.
- **Cierre de la investigación:** el historial persistente de PSReadLine existe, pero contiene cero menciones de `ConnectionStrings` y cero asignaciones mediante `$env`, `SetEnvironmentVariable` o `setx`; `.bash_history` tampoco contiene coincidencias y no se encontró otro historial PSReadLine. Los comandos contextuales sobre SILLAR no inyectan una conexión, el archivo no aporta marcas de tiempo y una terminal distinta o un proceso no interactivo puede no dejar rastro allí. El incidente queda cerrado como **mecanismo probado, causa sin establecer, contenido pero no resuelto**.

### Construido

- `cms.featured_products` incorpora `product_price numeric(10,2) NULL`, `product_price_varies boolean NOT NULL`, `product_category text NULL` y `product_is_public boolean NOT NULL`. Se editó la migración inicial, su snapshot EF, entidad, configuración y `DATOS.md`; no se añadió una migración incremental.
- `ck_featured_products_product_price` acepta precio nulo o no negativo y `ck_featured_products_product_category_no_vacia` permite categoría nula pero no texto vacío.
- El precio permanece como `decimal?` en los DTO: `null` significa a consultar, `0` significa gratis y un valor positivo es el importe. `productPriceVaries` viaja por separado y no existe ningún campo de precio formateado.
- El listado público combina la vigencia compartida con una expresión que exige `product_id` vivo y `product_is_public=true`. Administración no aplica ese filtro.
- Alta y reenlace internos ya reciben y sobrescriben los cuatro campos nuevos junto con nombre, slug e imagen; siguen sin montarse por HTTP hasta que M01 publique el selector.

### Decidido durante este encargo

- **Reversible — precio sin estado textual adicional:** se conservó `decimal?` de extremo a extremo. No se añadió un enum ni una etiqueta porque los tres estados ya se representan sin pérdida y la presentación pertenece a la interfaz.
- **Reversible — booleanos sin default SQL:** `product_price_varies` y `product_is_public` son obligatorios y cada snapshot debe escribirlos. Esto evita que una carga incompleta publique por accidente; EF escribe sus valores explícitos.
- **Reversible — categoría efectiva opcional:** `NULL` es un estado normal; si hay texto, el modelo y PostgreSQL rechazan que esté vacío.
- **Reversible — filtro traducible por EF:** la condición de producto enlazado y público vive en una expresión reutilizable por la consulta y las pruebas, separada de la vigencia compartida.

### Verificación observada

- **Fallo seguro de configuración:** con la base propia detenida y la variable mal escrita, el host terminó antes de sincronizar. Al volver a levantar Compose, el recurso resuelto siguió siendo `sillar_m02_db` en `55442`, base `sillar_m02`.
- **Migración desde cero:** se verificaron los destinos exactos por etiquetas y se ejecutó `docker compose down -v`; solo desaparecieron `sillar_m02_db`, `sillar_m02_default` y `sillar_m02_db_data`. CORE, Catálogo y CMS aplicaron sus migraciones sobre el volumen nuevo.
- **Columnas reales:** PostgreSQL devolvió `numeric(10,2) NULL`, `boolean NOT NULL`, `text NULL` y `boolean NOT NULL` para los cuatro campos nuevos. Una inserción con `product_price=-0.01` terminó con código 1, nombró `ck_featured_products_product_price` y dejó cero filas con ese nombre.
- **Desinstalación:** antes del drop existían exactamente 10 tablas CORE, 7 de Catálogo y 6 CMS. `99_drop.sql` dejó las mismas 10 y 7 tablas, eliminó el schema CMS y luego la migración inicial se reaplicó correctamente.
- **Publicación:** con Catálogo y CMS activos se insertaron cuatro snapshots vigentes. El ID 2 con `product_is_public=false` apareció en administración y no en público. Como control, al cambiarlo temporalmente a `true` apareció en público; al devolverlo a `false` volvió a desaparecer. Ambas peticiones devolvieron 200.
- **Tres estados de precio:** la respuesta pública observada devolvió `productPrice:null`, `productPrice:0.00` y `productPrice:8.00`; el último llevaba `productPriceVaries:true`. Los dos primeros productos tenían `productCategory:null` y la respuesta terminó en 200.
- **Sin precio formateado:** la respuesta HTTP solo incluyó `productPrice` numérico nullable y `productPriceVaries`; la inspección de propiedades no encontró `PriceText`, `Formatted` ni `Label`.
- **Pruebas:** la primera parametrización decimal falló dos casos porque xUnit no convierte `InlineData` entero a `decimal?`; se sustituyó por literales decimales dentro de una prueba. La corrida posterior superó 264/264 pruebas: 132 CORE, 54 Shared, 41 Catálogo y 37 CMS. La compilación terminó con 0 advertencias y 0 errores, y EF informó que modelo y snapshot no tienen cambios pendientes.
- **Estado final de la base:** después de las verificaciones, CMS y Catálogo quedaron inactivos; CORE quedó activo.

### Discrepancias y pendiente

- El `docs/modules/cms/SPEC.md` nuevo llegó al worktree durante la verificación. Se conservó sin editar, con SHA-256 `BCE954153BE4AAA68371FAA275A2661B2168D0416695672C2DAAAD29469A8891`; ya documenta los cuatro campos, los dos eventos y la reconciliación manual.
- El SPEC entregado conserva dos discrepancias internas que coordinación debe resolver: el ejemplo de `ProductPickerItem` todavía solo lleva identificador, nombre, slug e imagen aunque el snapshot nuevo necesita también precio, variación, categoría y publicación; además repite dos veces el párrafo «M02 sería el primer consumidor del bus interno». No se corrigió el archivo mantenido por JP.
- El encargo llama a `product_is_public` un renombrado de `product_is_published`, pero el padre `ac9c64d` no contenía ninguna propiedad ni columna con ese nombre anterior. Como no hay despliegues y se edita la migración inicial, se creó directamente `product_is_public`; no se inventó una operación de renombrado sobre una columna inexistente.
- `git fetch origin` dejó `origin/main` en `e0e96c6`; `Sillar.Modules.Catalog.Contracts` todavía no publica `ProductPickerItem`, `BuscarParaSeleccionAsync` ni `ObtenerParaSeleccionAsync`.
- Quedan fuera búsqueda, alta, reenlace y reconciliación HTTP, además de los handlers de `ProductoActualizado` y `ProductoDesactivado`. No se suscribió a eventos finos de variantes ni se tocó Catálogo.
- `Dockerfile`, frontend, E2E, ADR y paso 4 permanecen congelados.

---

## Encargo 04 — cierre del bloque de productos destacados

### Línea base y contradicciones encontradas antes de escribir código

- Se integró `origin/main` en `b2ea894`; el merge propio `99fc295` conserva M01 y M02. La solución de partida compiló con 0 advertencias y 0 errores y superó 270 pruebas, con 2 pruebas de colación de Catálogo omitidas.
- **Distinción ausente en el modelo:** `ProductPickerItem` separa `IsActive` (el producto existe pero está dado de baja) de `IsPublic` (existe y se puede elegir, pero no se publica). El snapshot solo conservaba el segundo y no podía pintar esa diferencia con M01 desinstalado. Se añadió `product_is_active` en vez de reutilizar `product_is_public` o anular `product_id`: las dos alternativas confunden estados que el contrato distingue.
- **Dos cambios de categoría mezclados:** se comprobó antes de implementar que `ProductService.SetCategoriesAsync` publica `ProductoActualizado`, mientras `CategoryService.DeactivateAsync` publica un único `CategoriaDesactivada` y no una ráfaga por producto. El SPEC se partió en ambos casos; M02 relee un producto para el primero y todos sus destacados enlazados para el segundo.
- **Texto antiguo incompatible con el refresco:** el SPEC todavía decía que el snapshot impedía que un renombrado reescribiera la portada. Eso contradice el handler y el criterio observable que exigen actualizar nombre y slug. Se precisó que el snapshot evita la dependencia durante cada petición y que eventos/reconciliación lo sustituyen explícitamente.

### Construido

- `cms.featured_products` incorpora `product_is_active boolean NOT NULL DEFAULT true`. Entidad, configuración, migración inicial, snapshot EF, DTO públicos/administrativos y diccionario usan el mismo nombre que identifica al dueño del estado.
- `FeaturedProductAdminResponse.ProductIsActive` describe el estado de alta del producto en M01; `FeaturedProductAdminResponse.IsActive` describe la baja editorial del destacado en CMS. Ambos están documentados en el DTO donde los consume el panel.
- El filtro público exige enlace vivo, vigencia, alta editorial, `product_is_public=true` y `product_is_active=true`. Administración conserva y distingue productos no publicados, dados de baja y pendientes de reenlace.
- Se montaron búsqueda de productos activos, alta, reenlace, refresco individual y reconciliación total. Alta y reenlace vuelven a resolver el UUID en M01 y copian nombre, slug, imagen, precio, variación, categoría efectiva, publicación y alta; el selector resuelve URL de imagen y no expone el UUID del medio.
- `FeaturedProductSnapshotCoordinator` centraliza la relectura completa, serializa por producto y abre scopes propios. Los caminos manual y de eventos llaman al mismo código; `null` anula solo `product_id` y conserva el snapshot para reenlace.
- CMS consume `ProductoActualizado`, `ProductoDesactivado` y `CategoriaDesactivada`. Los dos primeros releen el producto del evento; el tercero relee todos los UUID enlazados. Los tres manejadores son singleton porque el bus también se resuelve desde el contenedor raíz.
- Se añadieron pruebas en español para la distinción de estados, publicación, precio, idempotencia del snapshot, contratos HTTP y vida útil de los tres manejadores.

### Verificación observada — PostgreSQL y HTTP reales

Stack exclusivo: proyecto Compose `sillar_m02`, contenedor `sillar_m02_db`, base `sillar_m02`, puerto host `55442`; host HTTP en `127.0.0.1:5082`. Cada arranque informó `C:\sillar-m02\.env · base sillar_m02 en 127.0.0.1:55442` antes de recibir peticiones.

- **Migración:** se recreó el volumen propio y la migración inicial produjo `product_is_active boolean NOT NULL DEFAULT true`. Había exactamente 0 filas en `cms.featured_products`, por lo que no correspondía reconciliar snapshots envejecidos durante la migración.
- **Drop/reaplicación:** antes del drop había 10 tablas CORE, 7 de Catálogo y 6 CMS. `99_drop.sql` dejó 0 tablas CMS y conservó, nombre por nombre, las mismas 17 tablas ajenas (`DIFFERENCES=0`); reaplicar la migración restauró 6 tablas CMS y EF respondió que no había cambios pendientes.
- **Selector y alta:** un producto activo con `is_public=false` apareció en el selector como `False/True`, se destacó con 201 y no apareció en público. Tras publicarlo en M01, el evento actualizó el snapshot y la misma fila apareció en público.
- **Idempotencia de producto:** se capturó el snapshot después de un `ProductoActualizado`; cuatro actualizaciones idénticas adicionales dejaron exactamente la misma firma de nombre, slug, precio, categoría, publicación, alta, enlace y estado de reenlace.
- **Categorías, los dos casos:** cambiar la principal mediante `SetCategoriesAsync` movió el snapshot de «Categoría Principal M02» a «Categoría Alterna M02» por `ProductoActualizado`. Dar de baja la principal hizo la misma sustitución mediante `CategoriaDesactivada`; reactivarla y volver a emitir la baja dejó la misma firma que la primera vez.
- **Baja de producto:** `ProductoDesactivado` dejó `productIsActive=false`, `pendingRelink=false` y el UUID vivo en administración; el público quedó sin esa fila y el selector dejó de ofrecerla.
- **Reactivación manual suficiente:** se devolvió `catalog.products.is_active` a `true` directamente, sin evento. CMS continuó mostrando `false` hasta `PUT .../{id}/refresh`; después mostró `true` y la fila volvió al público. El camino manual no depende de que M01 emita un evento de reactivación.
- **Evento perdido y reconciliación:** nombre, slug, precio y publicación se cambiaron directamente en Catálogo. CMS conservó el valor anterior; `PUT .../refresh` respondió `refreshedCount=1`, `pendingRelinkCount=0` y sustituyó el snapshot por los valores nuevos.
- **Tres estados de precio y categoría opcional:** altas resueltas desde M01 conservaron `12.50`, `null` y `0.00` como tres estados distintos. El producto sin categoría devolvió `primaryCategoryName=null` y se pudo destacar; ningún contrato devolvió precio formateado.
- **Retirada y reenlace:** `cms_catalog_drop.sql` dejó los tres destacados con `productId=null`, `pendingRelink=true` y público vacío. Reenlazar el ID 1 a «Producto Sin Categoría M02» sustituyó todo el snapshot y lo devolvió al público.
- **Producto inexistente:** sin FK de integración se colocó un UUID inexistente y se pidió refrescar. M01 devolvió `null`; CMS anuló el enlace, conservó «Producto Gratis M02» y marcó `pendingRelink=true`, sin error HTTP.
- **Concurrencia:** tras cambiar un nombre directamente en M01, dos refrescos simultáneos del mismo destacado terminaron en 200 y administración devolvió «Concurrente Final M02», nunca el valor viejo.
- **Dependencia blanda:** con Catálogo inactivo y CMS activo, público respondió exactamente `200 []`; administración siguió listando snapshots; el selector respondió 404 y Swagger omitió búsqueda, alta, reenlace y ambos refrescos dependientes.
- **Swagger y auditoría:** con ambos módulos activos Swagger publicó 39 operaciones CMS y ninguna carecía de resumen; incluyó selector, alta, reenlace y ambos refrescos. La corrida dejó auditoría de altas y actualizaciones de destacados.
- **Pruebas y compilación:** la solución compiló con 0 advertencias y 0 errores. Pasaron 274 pruebas (132 CORE, 54 Shared, 47 Catálogo y 41 CMS); quedaron omitidas las 2 pruebas de Catálogo que requieren su colación SQL auxiliar.
- **Estado final de la base:** la FK de integración quedó aplicada; hay tres snapshots de verificación. CORE quedó activo y Catálogo/CMS inactivos; el host de prueba quedó detenido.

### Encontrado al estrenar el bus interno

- Cada relectura de `CatalogService.ObtenerParaSeleccionAsync` emitió `Microsoft.EntityFrameworkCore.Query[20504] MultipleCollectionIncludeWarning`: la proyección materializa las colecciones `product.Items` y `product.Categories` en una sola consulta. El origen está en `CatalogService.SeleccionAsync`; se reporta y no se rodea porque el proyecto de Catálogo está congelado para M02.
- `Sillar.Shared.Events.IEventPublisher` todavía afirma en su comentario que «hoy nadie escucha». Ya no es cierto después de estos handlers, pero `shared/` está congelado por coordinación; queda para la costura común.

### Decisiones de este encargo

- **DECIDÍ:** no tomar decisiones irreversibles.
- **DECIDÍ:** guardar `product_is_active` junto a `product_is_public`. **DESCARTÉ:** reutilizar el segundo o anular `product_id` al dar de baja. **POR QUÉ:** publicación, baja e inexistencia producen tres mensajes y acciones distintas incluso sin M01. **REVERSIBLE:** sí; es una columna de snapshot añadida antes de despliegues.
- **DECIDÍ:** releer todos los destacados ante `CategoriaDesactivada`. **DESCARTÉ:** pedir otro contrato o forzar una ráfaga por producto en M01. **POR QUÉ:** el coste queda acotado por la portada y no crece con el catálogo. **REVERSIBLE:** sí; debe revisarse si los destacados dejan de estar acotados.
- **DECIDÍ:** compartir coordinador entre eventos y botones y serializar por UUID. **DESCARTÉ:** aplicar datos del evento o duplicar lógica por endpoint. **POR QUÉ:** la relectura completa es idempotente y no depende del orden de entrega. **REVERSIBLE:** sí.
- **DECIDÍ:** montar las cinco rutas dependientes solo cuando el contenedor registra `ICatalogService`. **DESCARTÉ:** comprobar el código del módulo. **POR QUÉ:** la disponibilidad se expresa por el contrato opcional, no por conocimiento de activaciones ajenas. **REVERSIBLE:** sí.

### Fuera y congelado

- No se tocaron `PublicSite`, `frontend/shared`, `e2e`, `Dockerfile`, Catálogo, Shared, ADR ni el paso 4.
- La advertencia de consulta de M01 y el comentario obsoleto del bus se reportan para sus dueños; corregirlos desde M02 violaría los archivos congelados.
- El `Dockerfile` sigue sin CMS y queda pendiente de la fusión final.
