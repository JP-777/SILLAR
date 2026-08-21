# SPEC — M02 Contenido Web

- **Módulo:** `cms` · **Schema:** `cms` · **Nivel:** MVP
- **Estado:** Aprobado como base del paso 2
- **Depende de:** CORE (dura), M01 (blanda)

---

## 1. Propósito

Lo que el negocio quiere decir, frente a lo que el negocio vende.

M01 responde «qué tengo»; M02 responde «qué quiero que veas primero». Son dos oficios distintos y por eso son dos módulos: el catálogo cambia cuando llega mercadería, el contenido cambia cuando hay campaña escolar, Navidad o una promoción de tres días.

M02 es el módulo que hace que la portada no sea una lista de productos ordenada por fecha de alta.

---

## 2. Valor comercial

**Es el módulo más vendible del catálogo entero, y el único que se vende solo.** Un negocio puede comprar únicamente M02 y tener una web administrable —portada, promociones, trabajos, redes— sin catálogo, sin carrito y sin clientes. Nada más lo permite.

Y es la prioridad número uno declarada por la primera clienta: los banners.

---

## 3. La pregunta que filtra cada campo

Igual que en M01, cada campo se justifica antes de existir:

> **¿Esto lo edita quien administra el negocio, o lo decide quien diseñó la web?**

Lo primero es una columna. Lo segundo es código, y meterlo en la base convierte el panel en un editor de páginas que nadie pidió y que todos temen tocar.

**El caso que valida el diseño: la campaña escolar.** La dueña quiere que del 1 al 28 de febrero la portada abra con «Campaña escolar», con una foto apaisada en computadora y una vertical en teléfono, que lleve al listado de cuadernos, y que el 1 de marzo desaparezca sola sin que nadie se acuerde. Todo eso son columnas. El color del botón no.

---

## 4. Las dos decisiones estructurales

### 4.1 Un banner son dos imágenes, no una imagen redimensionada

Un banner apaisado y uno casi cuadrado no son la misma imagen con menos píxeles: **al recortar se pierde el sujeto.** Eso es dirección de arte y la decide quien sube la foto, no un algoritmo.

Por eso cada banner tiene **imagen de escritorio, obligatoria, e imagen de móvil, opcional con respaldo a la de escritorio**. Quien publica decide si el recorte merece una foto aparte.

Esto desacopla a M02 de la decisión pendiente sobre derivados de imagen: una columna guarda un identificador de medio, y que ese identificador resuelva a un archivo o a cinco es asunto de CORE.

### 4.2 Los destacados son una selección curada, no un atributo del producto

El §12 del SPEC de M01 aparta a M02 los destacados de portada: son contenido, no clasificación. M01 no tiene ni tendrá una casilla «destacado».

En consecuencia M02 guarda **qué cinco productos se destacan esta semana y en qué orden**, que es una decisión editorial con vigencia, no una propiedad del cuaderno.

Es la única dependencia hacia M01, y es **blanda**: se resuelve con columna nullable sin FK más datos snapshot, exactamente como la arquitectura exige. Si M01 no está instalado, la sección no se ofrece; si M01 se desinstala con destacados vivos, las filas conservan el nombre y la imagen del momento y no queda ninguna referencia rota.

---

## 5. Dependencias

| Módulo | Tipo | Qué necesita de él | Si no está |
|---|---|---|---|
| CORE | **Dura** | Autenticación, auditoría, `core.media_assets`, colaciones `es_ci` y `es_search` | No aplica: CORE siempre está |
| M01 | **Blanda** | Solo para elegir qué producto se destaca, y **solo en el panel** | La sección de destacados no se ofrece en el panel ni se publica. Las filas existentes sobreviven con su snapshot |

**La portada pública no llama a M01.** Renderiza desde el snapshot propio, y por eso sobrevive a la desinstalación y no depende de que el catálogo responda. La dependencia existe únicamente en el panel, en el instante de elegir el producto.

**Lo que M01 tiene que exponer para eso**, y hoy no expone:

```csharp
public sealed record ProductPickerItem(
    Guid ProductId,
    string Name,
    string Slug,
    Guid? PrimaryImageId,
    string? PrimaryCategoryName,   // la categoría EFECTIVA, nula si no tiene
    decimal? Price,                // null = a consultar · 0 = GRATIS · >0 = precio
    bool PriceVaries,              // true = «Desde»
    bool IsPublic,                 // false = se puede destacar, no se publica
    bool IsActive);                // false = existe, pero está dado de baja

Task<IReadOnlyList<ProductPickerItem>> BuscarParaSeleccionAsync(
    string texto, int limite, CancellationToken ct);

Task<ProductPickerItem?> ObtenerParaSeleccionAsync(
    Guid productId, CancellationToken ct);
```

`BuscarParaSeleccionAsync` solo devuelve productos activos. `ObtenerParaSeleccionAsync` también devuelve los dados de baja, con `IsActive = false`; `null` significa que el producto ya no existe y el destacado debe volver a enlazarse.

`Price` y `PriceVaries` salen de `ItemPricing.ForCard` de M01. **M02 no vuelve a derivar la regla**, porque derivarla otra vez es tener dos versiones de ella, y la que se queda atrás es la que está más lejos de los casos reales.

**No va dentro de `ItemSnapshot`.** Ese record es el congelado de la operación de venta y lo consumen M03 y M13; añadirle slug e imagen para una necesidad de la web lo convierte en el cajón de todos. Es un contrato aparte, de lectura para selección.

**Módulos que dependen de M02:** ninguno. Nadie lee contenido editorial desde otro módulo.

---

## 6. Modelo de datos — schema `cms`

**Ninguna tabla de M02 se replica** (ADR-017: contenido y banners son «solo en la WEB»). Claves primarias `integer GENERATED ALWAYS AS IDENTITY`, según la ADR-016.

**Pero las referencias a medios son `uuid`.** `core.media_assets` sí se replica y su clave es `uuid` v7 desde la ADR-018. La dirección es la permitida —una tabla que no se replica puede referenciar a una que sí— y **la FK es física, no snapshot**.

Campos comunes a todas las tablas:

| Campo | Tipo | Nulo | Descripción | Default |
|---|---|---|---|---|
| `is_active` | `boolean` | no | Eliminación lógica | `true` |
| `created_at` | `timestamptz` | no | | `now()` |
| `updated_at` | `timestamptz` | no | Trigger `set_updated_at()` | `now()` |

### 6.1 `cms.banners`

La pieza principal de la portada.

| Campo | Tipo | Nulo | Clave | Descripción | Regla |
|---|---|---|---|---|---|
| `id` | `integer` | no | PK | | identidad |
| `title` | `text` | sí | | Texto sobre la imagen | no vacío si viene |
| `subtitle` | `text` | sí | | | |
| `image_desktop_id` | `uuid` | **sí** | FK → `core.media_assets` | `ON DELETE SET NULL` | Sin ella el banner no se publica |
| `image_mobile_id` | `uuid` | sí | FK → `core.media_assets` | Si falta, se usa la de escritorio | |
| `alt_text` | `text` | no | | Accesibilidad | no vacío |
| `link_url` | `text` | sí | | Adónde lleva | ruta interna o URL absoluta |
| `link_label` | `text` | sí | | Texto del botón | **obligatorio si hay `link_url`** |
| `display_order` | `integer` | no | | Orden en el carrusel | `>= 0` |
| `starts_at` | `timestamptz` | sí | | Desde cuándo se publica | |
| `ends_at` | `timestamptz` | sí | | Hasta cuándo | **`> starts_at`** |

**Restricciones:** `ck_banners_vigencia` sobre las dos fechas; `ck_banners_enlace` que impide `link_url` sin `link_label`; `ck_banners_alt` que exige `alt_text` cuando hay imagen.

**Ninguna FK hacia medios es `RESTRICT`.** Todas son `ON DELETE SET NULL`, y por eso las columnas de imagen son nullable. La conducta que M01 ya verificó es que borrar un archivo usado deja a quien lo usaba sin imagen y ninguna operación falla; `RESTRICT` haría que CORE no pudiera borrar un medio porque un banner lo usa, que es justo lo contrario. **Un banner sin imagen existe pero no se publica**, igual que una marca sin logo sigue existiendo.
**Índices:** `idx_banners_publicados` sobre `(is_active, starts_at, ends_at)`.

`alt_text` es obligatorio **cuando hay imagen**, y se comprueba con un `CHECK`, no con una regla de aplicación. Es la única forma de que la portada de un negocio real no acabe con cuatro imágenes mudas: si se pudiera omitir, se omitiría siempre.

### 6.2 `cms.promotions`

Una promoción no es un banner: el banner ocupa el escenario y la promoción vive en una rejilla de varias.

Mismos campos que `banners` salvo que la imagen es **una sola** (`image_id`, `uuid`, nullable — una promoción puede ser solo texto) y que `alt_text` sigue la misma regla condicional: obligatorio si hay imagen, nulo si no la hay. Añade:

| Campo | Tipo | Nulo | Descripción | Regla |
|---|---|---|---|---|
| `description` | `text` | sí | | |
| `badge_text` | `text` | sí | Etiqueta corta: «-20%», «2x1» | máx. 20 caracteres |

**No hay campo de precio ni de porcentaje.** Un descuento real que afecte al carrito es de M03; aquí es texto, y ponerlo como número invitaría a creer que calcula algo.

### 6.3 `cms.featured_products`

La selección curada de la decisión 4.2.

| Campo | Tipo | Nulo | Clave | Descripción |
|---|---|---|---|---|
| `id` | `integer` | no | PK | |
| `product_id` | `uuid` | sí | **sin FK en el script base** | Dependencia blanda hacia M01 |
| `product_name` | `text` | no | | **Snapshot** del nombre al destacarlo |
| `product_slug` | `text` | sí | | **Snapshot**, para el enlace |
| `product_price` | `numeric(10,2)` | sí | | **Snapshot**. Nulo = a consultar · **`0` = gratis** · `>0` = el precio |
| `product_category` | `text` | sí | | **Snapshot** de la categoría **efectiva**, para la tarjeta sin foto. Nula si el producto no tiene ninguna |
| `product_price_varies` | `boolean` | no | | Si las presentaciones cuestan distinto → «Desde» |
| `product_is_public` | `boolean` | no | | **Snapshot**. Si es falso, no se publica |
| `product_is_active` | `boolean` | no | | **Snapshot**. Si es falso, M01 lo dio de baja; default `true` para snapshots anteriores |
| `image_id` | `uuid` | sí | FK → `core.media_assets` | Imagen elegida para la portada |
| `display_order` | `integer` | no | | `>= 0` |
| `starts_at` / `ends_at` | `timestamptz` | sí | | Misma vigencia que un banner |

**La FK hacia `catalog.products` vive en `database/integrations/cms_catalog.sql`**, con su `cms_catalog_drop.sql` que la elimina y anula las referencias huérfanas.

**Retirar la integración anula todos los `product_id`, y eso no se deshace.** Reinstalar M01 después no restaura el vínculo: los destacados conservan nombre, slug e imagen, pero hay que volver a elegir el producto. Es la conducta correcta —la alternativa es dejar referencias a filas inexistentes, que es la basura silenciosa que la arquitectura prohíbe—, pero **el panel tiene que decirlo**: un destacado con `product_id` nulo y snapshot lleno se muestra como «pendiente de volver a enlazar», no como un destacado normal.

El snapshot no es solo para sobrevivir a la desinstalación: **también evita que una petición pública dependa de M01 o cambie a mitad de lectura.** Los eventos y la reconciliación sustituyen el snapshot entero de forma explícita; si el nombre cambia, la portada lo refleja después de una de esas relecturas.

### 6.4 `cms.featured_projects`

Los trabajos del negocio: el mural que pintaron, la tarjetería de una boda, el anillado de una tesis. No son productos y no se venden desde aquí.

| Campo | Tipo | Nulo | Descripción | Regla |
|---|---|---|---|---|
| `id` | `integer` | no | PK | |
| `title` | `text` | no | | no vacío |
| `description` | `text` | sí | | |
| `image_id` | `uuid` | **sí** | FK → `core.media_assets` | `ON DELETE SET NULL` |
| `alt_text` | `text` | sí | | Obligatorio **si hay imagen** |
| `display_order` | `integer` | no | | `>= 0` |

### 6.5 `cms.social_links`

| Campo | Tipo | Nulo | Clave | Descripción | Regla |
|---|---|---|---|---|---|
| `id` | `integer` | no | PK | | |
| `platform` | `text` | no | **único** | `facebook`, `instagram`, `tiktok`, `whatsapp`, `youtube` | de la lista |
| `url` | `text` | no | | | URL absoluta |
| `display_order` | `integer` | no | | | `>= 0` |

**Restricciones:** `uq_social_links_plataforma`, con colación `core.es_ci`. Una cuenta por red: dos Instagram en el pie es un error de carga, no una funcionalidad.

`whatsapp` vive aquí y no en `site_settings` porque en el pie aparece junto a las demás. El número de contacto del negocio sigue siendo de CORE; esto es el enlace de la red.

### 6.6 Datos semilla

**Ninguno con contenido.** Ni un banner de ejemplo ni una red social precargada. El módulo recién instalado arranca vacío y cada pantalla lo dice con una frase útil.

Lo único que el seed crea, si hace falta, son claves de configuración propias del módulo en `core.site_settings` — y solo si se justifican una por una.

---

## 7. Contrato público — `Sillar.Modules.Cms.Contracts`

**Vacío, a propósito.** Ningún módulo lee contenido editorial: la portada la compone el armazón desde `GET /api/capabilities` y cada módulo aporta su sección. Crear un contrato que nadie consume es la abstracción por si acaso que prohíbe `CLAUDE.md`.

El proyecto de contratos existe igual, vacío, para no cambiar la forma de la solución cuando aparezca el primer consumidor real.

**Eventos publicados:** ninguno.

**Eventos consumidos: `ProductoActualizado`, `ProductoDesactivado` y `CategoriaDesactivada`.** Refrescan el snapshot de los destacados.

Cuando cambian los datos de un producto —incluida su asignación de categorías mediante `SetCategoriesAsync`— M01 emite `ProductoActualizado` para ese producto. Los eventos finos de variante existen y significan otra cosa; **no son de M02**.

Cuando se da de baja una categoría entera, M01 emite un solo `CategoriaDesactivada`, sin una ráfaga proporcional al catálogo. Como el snapshot conserva el nombre efectivo y no el identificador de categoría, M02 relee todos sus destacados sin intentar adivinar cuáles cambiaron. **La ráfaga sería proporcional al catálogo; esta relectura es proporcional a la portada.** La decisión vale mientras el conjunto de destacados esté acotado por lo que cabe en una portada; si deja de tener límite, hay que revisarla.

La razón está en la naturaleza del snapshot: guarda **valores derivados** —el precio efectivo sale de las presentaciones, la categoría efectiva de qué categorías siguen activas— y un valor derivado cambia cuando cambia su entrada, no cuando cambia la fila del producto.

### El handler vuelve a leer, no aplica el evento

**Los handlers de producto toman su identificador, se lo preguntan a M01 y sobrescriben el snapshot entero.** El handler de categoría provoca la misma relectura para todos los destacados enlazados. Ninguno aplica valores transportados por el evento.

Es lo que lo hace idempotente, y hace falta que lo sea: guardar la tabla de presentaciones desde el panel de M01 llama a varios endpoints seguidos, así que **una sola pulsación de «Guardar» puede emitir tres o cuatro `ProductoActualizado`**. Refrescar cuatro veces tiene que dar lo mismo que refrescar una.

Aplicar el contenido del evento, en cambio, hace que el resultado dependa del orden de llegada, y ese orden no es una garantía sobre la que convenga construir.

**El handler serializa por producto**, con independencia de cómo despache el bus hoy. Que sea en serie es un detalle de implementación de `InProcessEventBus`, no una promesa de su contrato; construir sobre un detalle no prometido es lo mismo que construir sobre una casualidad.

### Reconciliación: el cuarto momento

Los eventos son en proceso y no se reintentan. Si un handler falla, si el host cae entre la emisión y el consumo, o si M01 estuvo desactivado mientras algo cambió, **el snapshot se queda desfasado para siempre y nadie se entera**: la portada seguiría enseñando el precio de ayer sin ninguna señal.

Por eso el panel ofrece **actualizar los datos de un destacado**, y de todos a la vez. Es el mismo refresco del handler, disparado por una persona en lugar de por un evento.

No hace falta nada nuevo del contrato de M01: es la misma lectura por identificador. Y como el refresco es idempotente, ejecutarlo de más no cuesta nada.

Es una corrección de este SPEC. La versión anterior decía que suscribirse era trabajo sin caso, y se equivocaba: **el slug de M01 se puede corregir a mano**, y un snapshot que no se entera deja el enlace de la portada apuntando a un 404. Es el mismo daño contra el que existe la regla del slug en M01, en otra superficie.

Lo que se refresca son **los datos** del producto destacado. **Lo que nunca cambia solo es cuál está destacado**: eso es la decisión editorial, y solo la toma una persona.

**M02 es el primer consumidor del bus interno.** Antes de este paso el bus publicaba sin suscriptores; por eso su integración se verifica por el efecto y cualquier hallazgo de esa primera conexión se reporta en vez de rodearlo.

---

## 8. Endpoints

### Públicos

| Método | Ruta | Devuelve |
|---|---|---|
| `GET` | `/api/cms/banners` | Los vigentes ahora, ordenados |
| `GET` | `/api/cms/promotions` | Las vigentes ahora |
| `GET` | `/api/cms/featured-products` | Los destacados vigentes |
| `GET` | `/api/cms/featured-projects` | Los activos, ordenados |
| `GET` | `/api/cms/social-links` | Los activos, ordenados |

**«Vigente» es una sola definición y vive en un solo sitio:** `is_active`, y `starts_at` nulo o pasado, y `ends_at` nulo o futuro, contra `now()`. Se escribe una vez y las tres tablas con vigencia la usan.

### Administración — requieren sesión

Para cada una de las cinco entidades: listar (incluidas las no vigentes), obtener, crear, actualizar, desactivar y **reordenar**. Rol `editor` para todo salvo desactivar, que es `admin`, igual que en M01.

`PUT /api/admin/cms/<entidad>/order` recibe la lista completa de identificadores en su nuevo orden. Reordenar mandando `display_order` uno por uno produce estados intermedios con dos elementos en la misma posición.

---

## 9. Interfaz

**La composición de la portada es trabajo de M02, y es superficie compartida.** Hoy `platform/PublicSite.tsx` pregunta a mano si `catalog` está activo. Con M02 aparece el segundo caso, y por tanto el momento de extraer el registro declarativo — siguiendo el modelo de `layout/navigation.ts`, que ya lo hace bien para el menú. Copiar el `if` en vez de extraerlo convierte la regla del segundo caso en «se copió en el segundo, el tercero y el cuarto».

Es del paso 4, toca armazón, y **no se hace hasta que M01 esté fusionado**. Se avisa antes de tocarlo.


**Qué aparece si el módulo está activo:** una entrada de menú «Contenido» en administración, con cinco secciones; y en la web pública, el carrusel de la portada, la rejilla de promociones, la tira de destacados, la galería de trabajos y los iconos del pie.

**Qué desaparece si se desactiva:** las cinco pantallas de administración y sus rutas, y las cinco secciones públicas. **La portada no queda con un hueco: la sección no se renderiza, no se renderiza vacía.** El pie pierde los iconos sociales sin dejar un espacio en blanco.

### Estados y acciones

Cada pantalla declara las dos cosas. Un SPEC que solo describe estados produce listas bonitas que nadie puede tocar.

| Pantalla | Estados | Acciones |
|---|---|---|
| Banners | Vigente, programado, caducado, inactivo, sin imagen de móvil | Crear, editar, **reordenar arrastrando**, programar, previsualizar en móvil y escritorio, desactivar |
| Promociones | Vigente, programada, caducada, inactiva | Crear, editar, reordenar, desactivar |
| Destacados | Vigente, programado, **producto no publicado** (`product_is_public=false`), **producto desactivado en M01** (`product_is_active=false`), pendiente de reenlace | **Elegir producto del catálogo**, reordenar, **actualizar datos** (uno o todos), volver a enlazar, quitar de la portada |
| Trabajos | Activo, inactivo | Crear, editar, reordenar, desactivar |
| Redes | Activa, inactiva | Añadir, editar, reordenar, desactivar |

**Previsualizar es una acción, no una decoración.** Un banner con la foto mal recortada solo se ve en su proporción real, y el paso 4 tiene que ofrecerlo antes de publicar.

---

## 10. Reglas de negocio

1. **Una vigencia con `ends_at` anterior a `starts_at` se rechaza al guardar**, con la frase que dice qué fecha corregir.
2. Un banner caducado **no se borra**: se queda inactivo y visible en el panel. Las campañas se repiten cada año.
3. **`link_url` sin `link_label` se rechaza.** Un botón sin texto es un botón que nadie pulsa.
4. Reordenar es atómico: se recibe la lista entera y se escribe en una transacción.
5. **Al destacar un producto se copia su nombre, su slug y su imagen.** No se leen en cada petición: la portada pública no debe depender de que M01 responda.
6. **El snapshot se refresca con los eventos de M01**: nombre, slug, imagen, precio, categoría, publicación y estado de alta del producto. Lo que nunca cambia solo es **qué producto está destacado**. Sin M01, dejan de llegar eventos y el snapshot se queda como estaba.
7. Con M01 inactivo, la sección de destacados no se ofrece en el panel y su endpoint público devuelve lista vacía. **No falla.**
7b. **Un destacado cuyo producto no está publicado no se publica**, aunque su vigencia esté abierta. Se puede preparar la portada con antelación; lo que no se puede es enlazar a algo que responde 404.
7c. **El precio tiene tres estados y ninguno se confunde con otro: nulo es «a consultar», cero es «gratis», y cualquier otro es el precio.** Tratar el cero como vacío es el defecto que M01 ya sufrió en su tarjeta pública. El importe se copia tal cual lo devuelve `ItemPricing.ForCard` de M01: **M02 no vuelve a derivar la regla**, porque derivarla otra vez es tener dos versiones de ella.
7d. **El precio del snapshot se guarda como importe más marca de variación, no como texto ya formateado.** «Desde S/ 12.50» es presentación: la moneda y el idioma son del frontend, y congelarlos en la base los deja fijos para siempre.
8. Al borrar un medio en CORE que un banner usa, el banner queda sin imagen y **no se rompe**: misma conducta que M01 verificó en `medios-compartidos.spec.ts`. Un banner sin imagen no se publica, y el panel lo muestra como incompleto en vez de esconderlo.
8b. Un destacado sin `product_id` pero con snapshot está **pendiente de volver a enlazar**. Se muestra en el panel, no se publica, y ofrece elegir producto de nuevo.
9. `platform` se valida contra la lista cerrada. Una red nueva es una migración, no un texto libre.

---

## 11. Criterios de aceptación

- [ ] El schema `cms` se crea y se elimina sin afectar a `core` ni a `catalog`, y se vuelve a crear
- [ ] Los scripts son idempotentes: se corren dos veces y se comparan estados, no la ausencia de excepciones
- [ ] Con M02 desactivado, la aplicación arranca, el menú no muestra «Contenido», y la portada y el pie no quedan con huecos
- [ ] **El caso de la campaña escolar:** un banner con vigencia de febrero no aparece en enero, aparece en febrero y desaparece en marzo, sin que nadie lo toque
- [ ] Un banner sin imagen de móvil usa la de escritorio; con las dos, cada una en su proporción
- [ ] `ends_at` anterior a `starts_at` se rechaza con una frase que nombra la fecha
- [ ] `link_url` sin `link_label` se rechaza
- [ ] Reordenar cinco banners y recargar devuelve el mismo orden; una petición interrumpida no deja dos en la misma posición
- [ ] Dos enlaces de la misma red se rechazan, sin distinguir mayúsculas
- [ ] **Con M01 desinstalado, los destacados conservan nombre e imagen y la portada no muestra enlaces rotos**
- [ ] Renombrar un producto destacado actualiza su tarjeta en la portada, sin que nadie toque M02
- [ ] Corregir a mano el slug de un producto destacado deja el enlace de la portada apuntando al sitio correcto
- [ ] Desactivar un producto en M01 lo retira de la portada y lo deja visible en el panel
- [ ] Destacar un producto activo pero no publicado se permite, se avisa en el buscador, y **no** aparece en el endpoint público hasta que se publique
- [ ] Un producto con presentaciones de distinto precio muestra «Desde»; uno sin precio, «a consultar»
- [ ] **Un producto gratis muestra «Gratis», y uno con una presentación gratis y otra de pago no dice que sea gratis**
- [ ] Retirar la presentación más cara de un producto destacado actualiza el precio de la portada
- [ ] Editar el precio de una presentación de un producto destacado actualiza el de la portada
- [ ] Desactivar la categoría principal emite `CategoriaDesactivada`; M02 relee todos sus destacados y actualiza la categoría efectiva de la tarjeta
- [ ] Un destacado sin foto muestra el nombre con su categoría encima, igual que la tarjeta del catálogo
- [ ] Con un evento perdido a propósito, «actualizar datos» deja el snapshot al día
- [ ] Dos refrescos simultáneos del mismo producto no dejan el valor viejo
- [ ] Tras retirar la integración, los destacados aparecen en el panel como «pendiente de volver a enlazar», con su nombre, y se pueden reasignar
- [ ] Borrar en CORE un medio usado por un banner **no falla**: el banner queda sin imagen y deja de publicarse
- [ ] Desactivar en CORE un medio usado por un banner deja el banner sin imagen y ninguna operación falla
- [ ] Ninguna pantalla muestra un identificador
- [ ] Ningún «Ha ocurrido un error» ni ningún botón «Aceptar»
- [ ] Todos los endpoints en Swagger
- [ ] La interfaz responde en móvil y escritorio, navegable por teclado, sin colores escritos a mano
- [ ] `cms_catalog.sql` añade la FK solo con ambos módulos instalados, y su `_drop` la retira anulando huérfanos

---

## 12. Fuera de alcance

| Queda para | Qué | Por qué se puede aplazar |
|---|---|---|
| Fase 2 | `cms.pages` — páginas institucionales editables | Tabla añadida, no toca nada existente |
| Por decidir | Editor de texto enriquecido | Sin caso: hoy son títulos y descripciones cortas |
| M03 | Descuentos que afecten al carrito | Aquí el descuento es texto |
| Por decidir | Versiones y borradores del contenido | No hay segundo caso real |
| CORE | Derivados de imagen por tamaño | M02 solo anota qué proporción espera cada hueco |
| Por decidir | Varios idiomas | |
