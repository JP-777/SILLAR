# SPEC — M01 Catálogo de Productos

- **Código:** `catalog`
- **Schema:** `catalog`
- **Versión:** 1.1.0 — categorías N:M y nivel de variante
- **Estado:** Borrador · en revisión
- **Fase:** MVP · primer módulo del ciclo de cinco pasos

---

## 1. Propósito

Registrar y publicar **qué vende el negocio**: categorías, productos, variantes, imágenes, marcas y búsqueda.

Es el módulo del que cuelga casi todo lo comercial. Sin él no hay tienda en línea, no hay punto de venta y no hay inventario: no se vende, ni se cuenta, lo que no está catalogado.

**Este módulo describe el producto. No dice cuánto hay, ni dónde, ni a quién se le vende más barato.** Esa frontera es lo que lo mantiene común a los dos productos de la familia.

## 2. Valor comercial

Vendible solo, como **catálogo de exhibición sin venta**. Un negocio que solo quiere mostrar lo que ofrece —una carta de restaurante, un catálogo de servicios, una lista de productos con precio a consultar— compra M01 y nada más.

Es también el módulo que primero necesitan los dos productos: SILLAR WEB para publicar, SILLAR ERP para cobrar.

---

## 3. Las dos preguntas que filtran cada campo

M01 lo comparten SILLAR WEB y SILLAR ERP. Eso lo pone en riesgo por los dos lados a la vez, y cada campo se decide con dos preguntas que tiran en direcciones opuestas.

| Pregunta | De qué protege | Si la respuesta es «no» |
|---|---|---|
| **¿Tendría sentido en un negocio que solo tiene la web?** | De que el catálogo engorde con cosas que solo usa el mostrador | El campo no es de M01 |
| **¿Esto le cierra la puerta al mostrador?** | De diseñar el catálogo con la cabeza puesta solo en la web y que el punto de venta no se pueda construir encima | Hay que rediseñarlo aunque la web no lo necesite hoy |

> **«Mostrador»** = el punto de venta físico, la caja donde se atiende cara a cara. Es SILLAR ERP,
> el módulo M13. Se usa como taquigrafía en todos los documentos del proyecto.

### El caso que valida el diseño: el restaurante

Un restaurante que solo quiere publicar su carta, sus servicios de bufé y su delivery **es un cliente válido de M01 solo**. Y obliga a tres cosas:

| Un plato… | Consecuencia para M01 |
|---|---|
| no tiene existencias que contar | **Ningún campo de stock vive aquí.** Va en M09, y M09 puede no estar instalado |
| no tiene código de barras | El código y el código de barras son **opcionales**, nunca obligatorios |
| no se vende «por unidad» sino por plato, por porción o por persona | La unidad de venta es **texto libre**, no una lista cerrada |

Un catálogo que exija stock, código y unidad estándar deja fuera a los restaurantes, a los servicios y a media Arequipa.

### Aplicación campo por campo

| Campo | ¿Sentido sin mostrador? | ¿Cierra la puerta al mostrador? | Veredicto |
|---|---|---|---|
| Nombre, descripción, imágenes | Sí | No | **M01** |
| Precio de lista | Sí — la carta lo muestra | No | **M01**, y nulo permitido |
| Código visible | Sí — referencia de pedido | Sin él, la caja no encuentra nada rápido | **M01**, opcional |
| Código de barras | Poco, pero es del producto, no de la venta | Sí: sin él no hay lectora ni recepción de compras en M15 | **M01**, opcional |
| Unidad de venta | Sí — «por porción», «por millar» | Sí: sin ella no se venden hojas sueltas de un paquete | **M01**, texto libre |
| Cantidad en existencia | **No** | — | **M09** |
| Existencia por local | **No** | — | **M17** |
| Precio por mayor, descuento a conocido | **No** | — | **M13** |
| Conversión paquete → unidad | **No** — el restaurante no la usa | — | **M09** |

---

## 4. Las dos decisiones estructurales

Las dos que cuestan caras si se toman tarde. Ambas se resuelven aquí, con casos reales sobre la mesa.

### 4.1 Un producto puede estar en varias categorías

**Hay segundo caso, y hay tercero.** Los conos son deporte y también juguete. Una calculadora es tecnología y también material del curso de matemáticas. En una librería esto no es la excepción: es la mitad del catálogo.

Se implementa **N:M** desde el principio, con una de las categorías marcada como **principal** — la que da la ruta pública y la miga de pan. Sin principal, cada producto tendría tantas URL como categorías y ninguna sería la buena.

> **Observación para más adelante, no para ahora.** «Deporte» y «curso de matemáticas» no son
> categorías del mismo tipo: la primera dice *qué es* el producto, la segunda *para qué sirve*.
> La N:M aguanta las dos sin distinguirlas y con eso basta hoy. Si más adelante hace falta
> «lista de útiles de 3.º de primaria del colegio X», eso **no es una categoría, es un
> conjunto con dueño y vigencia**, y va en otro sitio — probablemente M07. No forzar la
> categoría a hacer de lista escolar.

### 4.2 El nivel que se cuenta y se cobra no es el producto

Tu caso del plumón lo decide: plumón de pizarra Artesco verde y azul comparten **todo** —nombre, descripción, imagen, precio, costo— y solo se diferencian en el código. Duplicar el producto por color es duplicar seis campos para variar uno.

Pero hay una pregunta que separa dos cosas que parecen la misma:

> ### ¿Puedo quedarme sin esta variante teniendo las otras?

| Respuesta | Qué es | Dónde vive |
|---|---|---|
| **Sí** — se me acaba el verde y me queda azul | **Variante.** Es una cosa distinta en el estante | **`product_items`, en M01.** Tiene código propio y existencia propia |
| **No** — el color se elige al momento y no hay nada que se acabe | **Característica de venta.** Un dato de la línea, no del inventario | La línea de venta, en M13. **Fuera del alcance de esta versión** |

**Por qué importa tanto.** Si el verde y el azul son un solo producto con dos códigos, M09 cuenta un solo número. La tienda tiene 3 verdes y 0 azules, entra un cliente pidiendo azul y el sistema dice que hay 3. Es exactamente el error que la ADR-017 acaba de prohibir: **prometer de más.** El sistema puede quedarse corto; no puede prometer lo que no hay.

**Cómo se resuelve sin duplicar nada.** El producto guarda lo compartido; la variante guarda lo que varía:

```
products                            product_items
─────────────────────────           ─────────────────────────
Plumón de pizarra Artesco    1 ── N   · Verde  · cod. 4501 · cb. 77…21
  descripción, imagen,                · Azul   · cod. 4502 · cb. 77…38
  precio, categorías, marca           · Negro  · cod. 4503 · cb. 77…45
```

Nada se duplica: el nombre, la foto y el precio se escriben una vez. Lo que se repite es lo único que de verdad cambia.

**Y no aparece cuando no hace falta.** Igual que la ADR-017 con las sucursales:

> **La tabla de variantes no aporta «variante»: aporta «más de una».**

Todo producto tiene **exactamente una** variante, creada sola y sin nombre. Un plato de restaurante tiene una. Un cuaderno tiene una. Mientras solo haya una, la interfaz no muestra la palabra «variante» en ninguna parte: el código y el código de barras se editan en el formulario del producto, como si vivieran ahí. La segunda variante la crea la persona cuando la necesita, y solo entonces aparece la tabla.

**Por qué ahora y no después.** M09 va a contar contra esta tabla y M13 va a vender contra ella. Si se construyen apuntando al producto y luego hay que bajar un nivel, no es una migración: hay que rehacer inventario, ventas y comprobantes con datos reales dentro. Es el mismo caso que la colación del clúster y que las claves `uuid` — la ventana está abierta y se cierra al escribir la primera tabla de M09.

**Lo que sí se aplaza.** La característica de venta pura —grabar un nombre, elegir envoltura, «sin azúcar»— **no cambia ninguna clave**: es un texto en la línea de venta. Se puede añadir en M13 cualquier día sin tocar M01. Por eso se aplaza sin riesgo, mientras que la variante no.

---

## 5. Dependencias

| Módulo | Tipo | Qué necesita de él | Si no está |
|---|---|---|---|
| CORE | **Dura** | Autenticación, auditoría, `core.media_assets` para las imágenes, colaciones `es_ci` y `es_search` | No aplica: CORE siempre está |

M01 **no depende de nada más**. Es deliberado: es el primer módulo del árbol comercial y todo lo demás cuelga de él.

**Módulos que dependen de M01:** M03 Ventas (dura), M09 Inventario (dura), M13 Punto de Venta (dura), M15 Compras (dura), M08 y M10 (blandas).

---

## 6. Modelo de datos — schema `catalog`

Todas las tablas **se replican entre nodos** (ADR-017: el catálogo es dato compartido), así que todas llevan `uuid` v7 generado por la aplicación, más `origin_node` y `row_version` según la regla 4 de la ADR-016.

Para no repetirlo en cada tabla, estos cuatro campos se dan por incluidos en todas:

| Campo | Tipo | Nulo | Descripción | Default |
|---|---|---|---|---|
| `origin_node` | `text` | no | Nodo donde nació la fila | — |
| `row_version` | `bigint` | no | Marca de versión para M16 | `1` |
| `created_at` | `timestamptz` | no | | `now()` |
| `updated_at` | `timestamptz` | no | Trigger `set_updated_at()` | `now()` |

### 6.1 `catalog.categories`

| Campo | Tipo | Nulo | Clave | Descripción | Regla | Default |
|---|---|---|---|---|---|---|
| `id` | `uuid` | no | PK | v7, generado por la app | Nunca visible al usuario | — |
| `parent_id` | `uuid` | sí | FK → `categories.id` | Categoría padre | No puede formar ciclo | `null` |
| `name` | `text COLLATE core.es_search` | no | | Nombre visible | No vacío | — |
| `slug` | `text COLLATE core.es_ci` | no | UQ | Para la URL pública | Solo `a-z0-9-` | — |
| `description` | `text` | sí | | Cabecera de la categoría | | `null` |
| `image_id` | `uuid` | sí | FK → `core.media_assets` | Portada | | `null` |
| `sort_order` | `integer` | no | | Orden de presentación | `>= 0` | `0` |
| `is_active` | `boolean` | no | | Baja lógica | | `true` |

**Restricciones:** `uq_categories_slug`, `ck_categories_slug_formato`, `ck_categories_name_no_vacio`, `ck_categories_parent_no_self`.
**Índices:** `idx_categories_parent`, `idx_categories_activas` parcial sobre `is_active`.

### 6.2 `catalog.brands`

| Campo | Tipo | Nulo | Clave | Descripción | Regla | Default |
|---|---|---|---|---|---|---|
| `id` | `uuid` | no | PK | v7 | | — |
| `name` | `text COLLATE core.es_ci` | no | UQ | Marca o fabricante | No vacío. Único ignorando mayúsculas, respetando tildes | — |
| `slug` | `text COLLATE core.es_ci` | no | UQ | | Solo `a-z0-9-` | — |
| `logo_id` | `uuid` | sí | FK → `core.media_assets` | | | `null` |
| `is_active` | `boolean` | no | | | | `true` |

Existe para **filtrar**, que es lo que pidió el PRD; el nombre del producto ya lleva la marca por la convención de nomenclatura.

> Un restaurante no usa marcas y la tabla se queda vacía. Es correcto: una tabla vacía no
> estorba, un campo obligatorio sí.

### 6.3 `catalog.products`

Lo que **comparten** todas las variantes: identidad, presentación y precio.

| Campo | Tipo | Nulo | Clave | Descripción | Regla | Default |
|---|---|---|---|---|---|---|
| `id` | `uuid` | no | PK | v7 | Nunca visible al usuario | — |
| `name` | `text COLLATE core.es_search` | no | | Producto + característica + marca + presentación, según el diccionario | No vacío | — |
| `slug` | `text COLLATE core.es_ci` | no | UQ | URL pública | Solo `a-z0-9-` | — |
| `short_description` | `text` | sí | | Una o dos líneas, para la tarjeta | | `null` |
| `description` | `text` | sí | | Ficha completa | | `null` |
| `primary_category_id` | `uuid` | sí | FK → `categories.id` | La que da la ruta y la miga de pan | Debe estar también en `product_categories` | `null` |
| `brand_id` | `uuid` | sí | FK → `brands.id` | | | `null` |
| `list_price` | `numeric(12,2)` | **sí** | | Precio de lista compartido | `>= 0`. Nulo = «consultar precio» | `null` |
| `sale_unit` | `text` | sí | | `unidad`, `plato`, `porción`, `millar`, `paquete de 100` | Texto libre | `null` |
| `variant_label` | `text` | sí | | Cómo se llama lo que varía: `Color`, `Tamaño`, `Sabor` | Solo se usa si hay más de una variante | `null` |
| `is_public` | `boolean` | no | | Si aparece en la web pública | | `true` |
| `is_active` | `boolean` | no | | Baja lógica | | `true` |

**Restricciones:** `uq_products_slug`, `ck_products_name_no_vacio`, `ck_products_list_price_no_negativo`, `ck_products_slug_formato`.

**Índices:**

- `idx_products_marca`, `idx_products_categoria_principal`
- `idx_products_publicos`, parcial sobre `is_active AND is_public`
- `idx_products_busqueda`: GIN sobre `to_tsvector('spanish', name || ' ' || coalesce(short_description,''))`

**Por qué `list_price` admite nulo.** «Precio a consultar» es real en pedidos B2B y por encargo, y el catálogo de exhibición sin venta lo necesita. **Cero no es lo mismo que nulo: cero es gratis.**

### 6.4 `catalog.product_items` — la variante

**Es la unidad que se cuenta, se cobra y se factura.** M09 cuenta contra esta tabla; M13 y M03 venden contra esta tabla.

| Campo | Tipo | Nulo | Clave | Descripción | Regla | Default |
|---|---|---|---|---|---|---|
| `id` | `uuid` | no | PK | v7 | Nunca visible al usuario | — |
| `product_id` | `uuid` | no | FK → `products.id` | | | — |
| `variant_value` | `text COLLATE core.es_search` | **sí** | | Lo que la distingue: `Verde`, `A4`, `200 hojas` | Nulo solo si es la única del producto | `null` |
| `code` | `text COLLATE core.es_ci` | **sí** | UQ parcial | Código visible del negocio. Es lo que se teclea en caja | Único entre los no nulos, en toda la instalación | `null` |
| `barcode` | `text COLLATE core.es_ci` | **sí** | UQ parcial | Código de barras | Único entre los no nulos | `null` |
| `price_override` | `numeric(12,2)` | sí | | Precio propio, si esta variante no vale lo mismo | `>= 0`. Nulo = usa `list_price` del producto | `null` |
| `image_id` | `uuid` | sí | FK → `core.media_assets` | Imagen propia de la variante | | `null` |
| `sort_order` | `integer` | no | | | `>= 0` | `0` |
| `is_active` | `boolean` | no | | Baja lógica | Un producto activo necesita ≥ 1 variante activa | `true` |

**Restricciones:**

- `uq_product_items_code` y `uq_product_items_barcode` — índices únicos **parciales**, `WHERE ... IS NOT NULL`
- `uq_product_items_valor` — único `(product_id, variant_value)` entre los no nulos
- `ck_product_items_price_no_negativo`

**Índices:** `idx_product_items_producto`, `idx_product_items_code_trgm` para búsqueda por fragmento de código en la caja.

**Por qué `code` y `barcode` son nulos y no obligatorios.** Un plato no tiene ninguno de los dos; un cuaderno tiene los dos. Hacerlos obligatorios expulsaría del producto a todo negocio de servicios. Únicos entre los no nulos da lo mejor de ambos: quien los use no puede duplicarlos, quien no los use no los ve.

**Por qué `price_override` y no un precio obligatorio por variante.** Tu caso —mismo precio, mismo costo, solo cambia el código— es el común, y en ese caso el precio se escribe una vez. El campo existe para el otro caso, que también es real: el cuaderno de 100 y el de 200 hojas.

### 6.5 `catalog.product_categories`

| Campo | Tipo | Nulo | Clave | Descripción |
|---|---|---|---|---|
| `product_id` | `uuid` | no | PK compuesta, FK → `products.id` | |
| `category_id` | `uuid` | no | PK compuesta, FK → `categories.id` | |

**Índices:** `idx_product_categories_categoria`, para listar una categoría.

La categoría principal del producto **tiene que estar** en esta tabla. Se garantiza en la aplicación, no con un `CHECK`: una restricción que mira otra tabla exige un disparador, y un disparador que se salta en una carga masiva miente.

### 6.6 `catalog.product_images`

| Campo | Tipo | Nulo | Clave | Descripción | Regla | Default |
|---|---|---|---|---|---|---|
| `id` | `uuid` | no | PK | v7 | | — |
| `product_id` | `uuid` | no | FK → `products.id` | | | — |
| `media_asset_id` | `uuid` | no | FK → `core.media_assets` | El archivo lo gestiona CORE | | — |
| `alt_text` | `text` | sí | | | Accesibilidad | `null` |
| `sort_order` | `integer` | no | | | `>= 0` | `0` |
| `is_primary` | `boolean` | no | | Imagen de la tarjeta | **Máximo una por producto** | `false` |

**Restricciones:** `uq_product_images_una_principal` — único parcial sobre `(product_id) WHERE is_primary`.

Las imágenes son **del producto**. La variante puede tener una propia en `product_items.image_id` cuando el color se ve, pero no lleva galería: seis fotos por color multiplican el trabajo de quien carga el catálogo por nada.

### 6.7 Relaciones

```
categories 1 ─── N categories        (padre → hijas)
categories N ─── N products          (vía product_categories)
brands     1 ─── N products
products   1 ─── N product_items     ← el nivel que M09 cuenta y M13 vende
products   1 ─── N product_images
```

### 6.8 Relaciones cruzadas

| Origen | Destino | Tipo | FK física | Dónde se declara |
|---|---|---|---|---|
| `catalog.categories.image_id` | `core.media_assets.id` | dura | sí | Migración de M01 |
| `catalog.brands.logo_id` | `core.media_assets.id` | dura | sí | Migración de M01 |
| `catalog.product_items.image_id` | `core.media_assets.id` | dura | sí | Migración de M01 |
| `catalog.product_images.media_asset_id` | `core.media_assets.id` | dura | sí | Migración de M01 |

**Qué pasa al borrar un archivo que está en uso.** Las tres referencias nulables van a
`ON DELETE SET NULL`: la marca se queda sin logo y la categoría sin portada, pero siguen
existiendo. `product_images.media_asset_id` es `NOT NULL`, así que ahí no cabe: va a
**`ON DELETE CASCADE`**, porque una fila de galería sin archivo no es nada.

`RESTRICT` se descarta a propósito: haría fallar el borrado con una violación de clave foránea,
que llega a la interfaz como el «Ha ocurrido un error» que este proyecto tiene prohibido.

**A cambio, la galería de CORE avisa antes de borrar** — con una frase, no con un recuento:

> *Si esta imagen está en uso, desaparecerá de donde esté.*

Es deliberado que no diga cuántos. Contar referencias entre módulos ya se descartó por no tener
segundo caso real, y sigue sin tenerlo. La frase cumple la regla que importa: **no ser
silencioso**, que es distinto de ser exacto.

### 6.9 Datos semilla

**Ninguno con contenido de negocio.** Ni una categoría de ejemplo, ni un producto de muestra: este repositorio contiene el producto, nunca a un cliente. El módulo recién instalado arranca vacío y la primera pantalla lo dice con una frase útil, no con una tabla en blanco.

---

## 7. Contrato público — `Sillar.Modules.Catalog.Contracts`

```csharp
namespace Sillar.Modules.Catalog.Contracts;

/// <summary>
/// Lo que se vende y lo que se cuenta. Congelado en el momento de la operación:
/// el pedido y la venta guardan esto, no una referencia viva.
/// </summary>
public sealed record ItemSnapshot(
    Guid     ItemId,
    Guid     ProductId,
    string   ProductName,
    string?  VariantValue,   // "Verde" — nulo si el producto no tiene variantes
    string?  Code,
    string?  Barcode,
    decimal? Price,          // ya resuelto: price_override ?? list_price
    string?  SaleUnit);

public interface ICatalogService
{
    Task<ItemSnapshot?> ObtenerItemAsync(Guid itemId, CancellationToken ct);

    /// <summary>Código exacto o código de barras. Lo que usa la caja con la lectora.</summary>
    Task<ItemSnapshot?> BuscarPorCodigoAsync(string codigo, CancellationToken ct);

    /// <summary>Texto libre, sin distinguir mayúsculas ni tildes. Devuelve variantes.</summary>
    Task<IReadOnlyList<ItemSnapshot>> BuscarAsync(string texto, int limite, CancellationToken ct);

    /// <summary>Las variantes de un producto. Para elegir color en pantalla.</summary>
    Task<IReadOnlyList<ItemSnapshot>> VariantesDeAsync(Guid productId, CancellationToken ct);

    Task<bool> ItemExisteYEstaActivoAsync(Guid itemId, CancellationToken ct);
}
```

**El contrato habla de `ItemId`, no de `ProductId`.** Es la consecuencia práctica de la decisión 4.2: quien vende, cuenta o factura, lo hace contra la variante. `ProductId` va en el snapshot solo para agrupar en informes.

**Eventos publicados:** `ProductoCreado`, `ProductoActualizado`, `ProductoDesactivado`, `VarianteCreada`, `VarianteDesactivada`, `CategoriaDesactivada`.
**Eventos consumidos:** ninguno.

---

## 8. Endpoints

### Públicos

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/catalog/categories` | Árbol de categorías activas |
| `GET` | `/api/catalog/categories/{slug}` | Categoría con sus productos, paginados |
| `GET` | `/api/catalog/products` | Listado con filtros `category`, `brand`, `q`, `page`, `pageSize` |
| `GET` | `/api/catalog/products/{slug}` | Ficha completa: imágenes y variantes disponibles |
| `GET` | `/api/catalog/brands` | Marcas activas con al menos un producto público |

Los públicos devuelven **solo** `is_active AND is_public`. Un producto despublicado responde 404, no 403: no se filtra información sobre lo que existe.

### Administración — requieren sesión

| Método | Ruta | Descripción |
|---|---|---|
| `GET` `POST` | `/api/admin/catalog/categories` | Listar todas · crear |
| `PUT` `DELETE` | `/api/admin/catalog/categories/{id}` | Editar · baja lógica |
| `GET` `POST` | `/api/admin/catalog/brands` | |
| `PUT` `DELETE` | `/api/admin/catalog/brands/{id}` | |
| `GET` `POST` | `/api/admin/catalog/products` | Listar con filtros · crear, con su variante única |
| `GET` `PUT` `DELETE` | `/api/admin/catalog/products/{id}` | |
| `PUT` | `/api/admin/catalog/products/{id}/categories` | Fijar el conjunto de categorías y cuál es la principal |
| `GET` `POST` | `/api/admin/catalog/products/{id}/items` | Variantes · crear la segunda y siguientes |
| `PUT` `DELETE` | `/api/admin/catalog/items/{itemId}` | Editar · baja lógica |
| `POST` `DELETE` | `/api/admin/catalog/products/{id}/images` | Asociar y quitar imágenes |
| `PUT` | `/api/admin/catalog/products/{id}/images/order` | Reordenar y marcar la principal |
| `GET` | `/api/admin/catalog/items/lookup?codigo=` | Resolución rápida por código, para la caja |

Todas las de escritura exigen token CSRF y quedan en `core.audit_log`.

---

## 9. Interfaz

**Rutas públicas:** `/catalogo`, `/catalogo/:categoria`, `/producto/:slug`.
**Rutas de administración:** `/admin/catalogo/productos`, `/admin/catalogo/categorias`, `/admin/catalogo/marcas`.

**Componentes principales:** tarjeta de producto, ficha con galería y selector de variante, árbol de categorías, buscador con resultados en vivo, formulario de producto con selector de imágenes de la galería de CORE.

**La variante es invisible mientras haya una sola.** El formulario de producto muestra código, código de barras y precio como si fueran campos del producto —lo son, de su variante única— y un botón discreto: *«Este producto viene en varias presentaciones»*. Al pulsarlo aparecen el nombre de lo que varía (`Color`) y la tabla de variantes. **Nunca al revés:** obligar a pensar en variantes para dar de alta un plato de menú es cargarle a todo el mundo la complejidad de unos pocos.

**Qué desaparece si el módulo se desactiva:** las tres entradas de menú público, las tres de administración y sus rutas. El home no queda con un hueco: si M01 no está, la sección de productos **no se renderiza** — no se renderiza vacía.

---

## 10. Reglas de negocio

1. Un producto **siempre** tiene nombre, slug y **al menos una variante activa**. Todo lo demás puede faltar.
2. Al crear un producto se crea su variante única, con `variant_value` nulo, sin que la persona lo vea.
3. El slug se genera del nombre y se puede corregir a mano. **Nunca cambia solo** al editar el nombre: cambiarlo rompe enlaces ya compartidos por WhatsApp, que es como se comparte un producto aquí.
4. Código y código de barras viven en la variante, son opcionales, y son únicos en toda la instalación entre los que existen.
5. El precio efectivo de una variante es `price_override ?? list_price` del producto. Nulo en ambos significa «consultar». **Cero significa gratis.** No se confunden en la interfaz.
6. Un producto puede estar en varias categorías. La **principal** determina su ruta pública y su miga de pan, y tiene que ser una de las suyas.
7. La baja es lógica. Un producto o variante desactivado desaparece de la web y **sigue existiendo** en pedidos y ventas anteriores.
8. **No se puede desactivar la última variante activa** de un producto activo: se desactiva el producto. El mensaje lo dice así, no con un error genérico.
9. Desactivar una categoría **no desactiva sus productos** y no actúa en cascada: el sistema avisa cuántos quedan sin esa categoría y la persona decide.
10. Una categoría no puede ser su propia ancestra.
11. Máximo una imagen principal por producto. Si no hay ninguna marcada, se usa la de menor `sort_order`.
12. Borrar un archivo de la galería de CORE **vacía** el logo de la marca, la portada de la categoría y la imagen de la variante, y **quita** la fila de la galería del producto. Nunca falla con un error de clave foránea, y nunca ocurre sin aviso previo.
12. La búsqueda pública ignora mayúsculas y tildes (`core.es_search`): *lapiz* encuentra *LÁPIZ*.
13. La unicidad de código, slug y marca ignora mayúsculas pero **respeta tildes** (`core.es_ci`): *Artesco* y *ARTESCO* son la misma marca; *Peña* y *Pena* no.
14. Los `uuid` **no se muestran nunca** en la interfaz ni en las URL. Fuera se usa el slug; dentro, el código del negocio.

---

## 11. Criterios de aceptación

- [ ] El schema `catalog` se crea y se elimina sin afectar a `core`
- [ ] Los scripts son idempotentes
- [ ] Con M01 desactivado, la aplicación arranca, el menú no muestra sus entradas y no queda ninguna ruta muerta ni hueco visual en el home
- [ ] **El caso del restaurante:** crear un plato sin código, sin código de barras y sin precio, publicarlo, y que en ninguna pantalla aparezca la palabra «variante»
- [ ] **El caso del plumón:** un producto con tres variantes de color, un solo nombre, una sola descripción, un solo precio y tres códigos de barras distintos; cada uno resuelve a su variante
- [ ] **El caso del cono:** un producto en «Deporte» y «Juguetes» aparece en el listado de las dos y su URL usa solo la principal
- [ ] **El caso del cuaderno:** dos variantes con `price_override` distinto conviven con el `list_price` del producto
- [ ] Dos variantes con `code` nulo conviven sin violar la unicidad
- [ ] Intentar desactivar la última variante activa da un mensaje que propone desactivar el producto
- [ ] Buscar `lapiz` devuelve `LÁPIZ`; crear la marca `ARTESCO` existiendo `Artesco` falla
- [ ] Desactivar una categoría con productos avisa y no desactiva nada más
- [ ] Cambiar el nombre de un producto no cambia su slug
- [ ] Las imágenes se asocian desde la galería de CORE y borrar la asociación no borra el archivo
- [ ] Borrar un archivo **usado** por una marca, una categoría, una variante y un producto: los tres primeros quedan sin imagen, el cuarto pierde esa fila de galería, y ninguna operación falla con un error de base de datos
- [ ] La galería avisa antes de borrar, con la frase, sin recuento
- [ ] Todos los endpoints en Swagger, con ejemplos
- [ ] La interfaz responde en móvil y escritorio, navegable por teclado, sin colores escritos a mano

---

## 12. Fuera de alcance

| Queda para | Qué | Por qué se puede aplazar sin riesgo |
|---|---|---|
| **M13** | **Característica de venta**: el dato que se elige al cobrar y no se cuenta —grabar un nombre, envoltura, «sin azúcar»— | Es un texto en la línea de venta. No cambia ninguna clave; se añade cualquier día |
| **M09** | Existencias, conversión paquete → unidad, alertas de mínimo | Cuenta contra `product_items`, que ya existe |
| **M17** | Existencia por local, conteo global | |
| **M13** | Precio por mayor, descuentos, listas de precios por tipo de cliente | |
| **M02** | «Ofertas», «Novedades», destacados de portada — son contenido, no categorías | |
| **M07** | Listas de útiles por curso, grado y colegio | Es un conjunto con dueño y vigencia, no una categoría |
| Por decidir | Varios códigos de barras para una misma variante — envase viejo y nuevo | Tabla añadida, sin tocar lo existente |
| Por decidir | Atributos dinámicos por categoría | No hay segundo caso real |
