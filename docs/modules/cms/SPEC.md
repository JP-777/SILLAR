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
| M01 | **Blanda** | Solo para elegir qué producto se destaca | La sección de destacados no se ofrece en el panel ni se publica. Las filas existentes sobreviven con su snapshot |

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
| `image_desktop_id` | `uuid` | no | FK → `core.media_assets` | | |
| `image_mobile_id` | `uuid` | sí | FK → `core.media_assets` | Si falta, se usa la de escritorio | |
| `alt_text` | `text` | no | | Accesibilidad | no vacío |
| `link_url` | `text` | sí | | Adónde lleva | ruta interna o URL absoluta |
| `link_label` | `text` | sí | | Texto del botón | **obligatorio si hay `link_url`** |
| `display_order` | `integer` | no | | Orden en el carrusel | `>= 0` |
| `starts_at` | `timestamptz` | sí | | Desde cuándo se publica | |
| `ends_at` | `timestamptz` | sí | | Hasta cuándo | **`> starts_at`** |

**Restricciones:** `ck_banners_vigencia` sobre las dos fechas; `ck_banners_enlace` que impide `link_url` sin `link_label`.
**Índices:** `idx_banners_publicados` sobre `(is_active, starts_at, ends_at)`.

`alt_text` es obligatorio a propósito. Es la única forma de que la portada de un negocio real no acabe con cuatro imágenes mudas: si se pudiera omitir, se omitiría siempre.

### 6.2 `cms.promotions`

Una promoción no es un banner: el banner ocupa el escenario y la promoción vive en una rejilla de varias.

Mismos campos que `banners` salvo que la imagen es **una sola** (`image_id`, `uuid`, nullable — una promoción puede ser solo texto y precio) y añade:

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
| `image_id` | `uuid` | sí | FK → `core.media_assets` | Imagen elegida para la portada |
| `display_order` | `integer` | no | | `>= 0` |
| `starts_at` / `ends_at` | `timestamptz` | sí | | Misma vigencia que un banner |

**La FK hacia `catalog.products` vive en `database/integrations/cms_catalog.sql`**, con su `cms_catalog_drop.sql` que la elimina y anula las referencias huérfanas.

El snapshot no es solo para sobrevivir a la desinstalación: **también evita que renombrar un producto reescriba la portada en silencio.** Si el nombre cambia, el panel lo señala y alguien decide.

### 6.4 `cms.featured_projects`

Los trabajos del negocio: el mural que pintaron, la tarjetería de una boda, el anillado de una tesis. No son productos y no se venden desde aquí.

| Campo | Tipo | Nulo | Descripción | Regla |
|---|---|---|---|---|
| `id` | `integer` | no | PK | |
| `title` | `text` | no | | no vacío |
| `description` | `text` | sí | | |
| `image_id` | `uuid` | no | FK → `core.media_assets` | |
| `alt_text` | `text` | no | | no vacío |
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
**Eventos consumidos:** ninguno. Si M01 desactiva un producto destacado, el panel lo señala al mirarlo; suscribirse a `ProductoDesactivado` para tacharlo en el momento es trabajo sin caso.

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

**Qué aparece si el módulo está activo:** una entrada de menú «Contenido» en administración, con cinco secciones; y en la web pública, el carrusel de la portada, la rejilla de promociones, la tira de destacados, la galería de trabajos y los iconos del pie.

**Qué desaparece si se desactiva:** las cinco pantallas de administración y sus rutas, y las cinco secciones públicas. **La portada no queda con un hueco: la sección no se renderiza, no se renderiza vacía.** El pie pierde los iconos sociales sin dejar un espacio en blanco.

### Estados y acciones

Cada pantalla declara las dos cosas. Un SPEC que solo describe estados produce listas bonitas que nadie puede tocar.

| Pantalla | Estados | Acciones |
|---|---|---|
| Banners | Vigente, programado, caducado, inactivo, sin imagen de móvil | Crear, editar, **reordenar arrastrando**, programar, previsualizar en móvil y escritorio, desactivar |
| Promociones | Vigente, programada, caducada, inactiva | Crear, editar, reordenar, desactivar |
| Destacados | Vigente, programado, **producto renombrado**, **producto desactivado en M01**, huérfano | **Elegir producto del catálogo**, reordenar, actualizar el snapshot, quitar de la portada |
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
6. Si el snapshot y el producto vivo difieren, el panel lo dice y ofrece actualizar. **Nunca se actualiza solo.**
7. Con M01 inactivo, la sección de destacados no se ofrece en el panel y su endpoint público devuelve lista vacía. **No falla.**
8. Al desactivar un medio en CORE que un banner usa, el banner queda sin imagen y **no se rompe**: misma conducta que M01 verificó en `medios-compartidos.spec.ts`.
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
- [ ] Renombrar un producto destacado no cambia la portada, y el panel señala la diferencia
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
