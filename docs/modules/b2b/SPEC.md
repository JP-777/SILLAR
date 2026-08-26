# SPEC — M07 Solicitudes B2B y Especiales

- **Código:** `b2b`
- **Schema:** `b2b`
- **Versión:** 1.0.0
- **Estado:** Borrador
- **Fase:** MVP

---

## 1. Propósito

M07 recoge **lo que no cabe en el carrito**: encargos que se apartan del catálogo porque cambia la
especificación o porque la cantidad cambia el precio. Los pone en una bandeja con estados y produce
una **cotización** que el negocio manda por WhatsApp.

Sin M07, eso vive en conversaciones sueltas de WhatsApp: **no queda registro de qué se cotizó ni a
qué precio**, y cuando el cliente vuelve a preguntar en marzo por lo que se habló en enero, no hay
dónde mirarlo.

## 2. Valor comercial

Es el módulo del **ticket grande**. Un pedido institucional vale lo que cincuenta ventas de
mostrador, y hoy se gestiona con la memoria de quien atendió.

Sirve a cualquier negocio que **fabrique o personalice por encargo**, aunque no compre ventas en
línea: M07 no vende, **cotiza**. Una imprenta, un taller de confección o una floristería lo compran
sin necesitar M03.

---

## 3. Dependencias

| Módulo | Tipo | Qué necesita de él | Comportamiento si no está |
|---|---|---|---|
| CORE | Dura | Autenticación, roles, auditoría, `site_settings` | No aplica: CORE siempre está |
| **M01 Catálogo** | **Dura** | El producto del que parte la solicitud y su precio de lista | No aplica: sin M01 no hay de dónde partir |
| **M04 Clientes** | **Dura** | La cuenta que exige toda solicitud | No aplica: sin cuenta no hay solicitud |
| M11 Pagos | Blanda *(futuro)* | Cobro con tarjeta | El pago se **registra a mano**: Yape y efectivo tecleados por un trabajador |

**Módulos que dependen de este:** **M08 Portal del Cliente** (Fase 3) consume su contrato para que
el cliente vea sus solicitudes y sus cotizaciones desde su cuenta.

> **Hasta que M08 exista, el cliente ve su cotización solo por WhatsApp.** Es deliberado y no un
> hueco: M07 produce el documento y el contrato para leerlo, pero **la pantalla donde el cliente lo
> consulta es de otro módulo y de otra fase**. `GET /api/b2b/quotes/{quoteNumber}` existe desde la
> 1.0.0 justamente para que M08 no tenga que abrir nada nuevo cuando llegue.

### Las dos dependencias que estaban mal declaradas

`ARQUITECTURA_MODULAR.md:63` decía «M04 (blanda)» y no mencionaba a M01. **Las dos estaban mal**, y
la corrección ya está aplicada en el commit `581ff5e`.

**M04 pasa a dura**, y es literalmente la misma frase que convirtió en dura la de M03 el 21 de
agosto de 2026, con «comprar» cambiado por «solicitar». La de M03 está en
`ARQUITECTURA_MODULAR.md:100`:

> «la cuenta obligatoria para comprar lo convierte en duro: si hay que tener cuenta, el módulo que
> guarda al cliente tiene que estar»

Aquí: **si hay que tener cuenta para solicitar, el módulo que guarda al cliente tiene que estar.**

**M01 nunca estuvo declarada**, y es la más visible de las dos en cuanto se mira la pantalla: el
formulario de solicitud **vive dentro de la ficha del producto**. Una solicitud que no parte de
nada no existe en este módulo.

**Consecuencia sobre el esquema, y no es menor:** al ser las dos duras, **las claves foráneas hacia
`catalog` y hacia `crm` van dentro de la migración de M07**, no en un script de integración. Una
dependencia dura no se desmonta por separado, y un script de integración existe precisamente para
poder desmontarla. `database/integrations/b2b_crm.sql` **no hace falta** y ya salió del plan.

---

## 4. Modelo de datos

### Replicación: ninguna tabla de M07 replica

Claves `integer GENERATED ALWAYS AS IDENTITY`, **sin `origin_node` ni `row_version`**.

La regla de decisión es la pregunta de `CLAUDE.md`: *¿puede esta fila nacer en un nodo y tener que
existir en otro?* Para M07 la respuesta es no, y hay dos comprobaciones independientes que lo
confirman:

- **`b2b` no está entre los módulos del ERP** (`CLAUDE.md:13`: `CORE, M01, M04, M09, M13–M17`). Solo
  vive en la instancia web.
- **No es uno de los datos compartidos de la ADR-017**, que nombra exactamente tres:
  «catálogo, clientes y existencias» (`ADR-017-mando-y-respaldo.md:36`).

**Precedente idéntico:** M02 tomó la misma decisión y por el mismo motivo. Sus tablas son `integer`
(`backend/Sillar.Modules.Cms/Migrations/20260820050000_CmsInitial.cs:28`).

### Tablas

Son cuatro, y las dos primeras existen separadas porque **son dos preguntas distintas del negocio**,
no dos variantes de la misma. La regla 3 del §8 lo explica.

#### `b2b.special_order_leads` — personalización

Lo que cambia es **la especificación**. La cantidad es irrelevante: puede ser una sola unidad. Parte
**siempre** de un producto o servicio que ya existe, porque lo que se pide se describe por
diferencia con él.

Los tres casos que dio el negocio: *«un arreglo del Día de la Madre pero con otro peluche y estos
dulces»*, *«una bolsa de dulces de las que hace la tienda pero solo con chocolates»*, *«una maqueta
distinta de las que se ofrecen»*.

| Campo | Tipo | Nulo | Clave | Descripción | Regla de negocio | Default |
|---|---|---|---|---|---|---|
| `special_order_lead_id` | `integer` | no | PK | Identidad | `GENERATED ALWAYS AS IDENTITY` | |
| `customer_id` | `uuid` | **no** | FK → `crm.customers` | Quién pide | Obligatorio: toda solicitud exige cuenta | |
| `product_id` | `uuid` | sí | FK → `catalog.products` | De qué parte | **Nulo solo si el producto se dio de baja** | |
| `product_name` | `text` | no | | Instantánea del nombre | No vacío | |
| `product_slug` | `text` | no | | Instantánea del slug | No vacío | |
| `pending_relink` | `boolean` | no | | El origen se perdió | Se marca al desactivarse el producto | `false` |
| `description` | `text` | no | | Qué quiere exactamente | No vacío | |
| `reference_image_id` | `uuid` | sí | FK → `core.media_assets` | Foto que trae el cliente | Opcional | `null` |
| `quantity` | `integer` | sí | | Cuántos | Si viene, `> 0` | `null` |
| `needed_by` | `date` | sí | | Para cuándo lo quiere | **Informativa**: no calcula ni bloquea nada | `null` |
| `status` | `text` | no | | Estado de la bandeja | Regla 5 | `'recibida'` |
| `staff_notes` | `text` | sí | | Notas internas | **Nunca sale del panel** — §5 | `null` |
| `is_active` | `boolean` | no | | Baja lógica | Nunca `DELETE` físico | `true` |
| `created_at` | `timestamptz` | no | | | | `now()` |
| `updated_at` | `timestamptz` | no | | | Trigger `set_updated_at()` | `now()` |

**Restricciones:** `ck_special_order_leads_description` (no vacía) · `ck_special_order_leads_quantity`
(`quantity IS NULL OR quantity > 0`) · `ck_special_order_leads_product_name`,
`ck_special_order_leads_product_slug` (no vacíos) · `ck_special_order_leads_status` (uno de los cinco).

**Índices:** `idx_special_order_leads_customer` · `idx_special_order_leads_status` ·
`idx_special_order_leads_product`.

> **Por qué la instantánea.** Es el **tercer uso** del patrón de `cms.featured_products`: referencia
> viva al catálogo **más** copia de lo que se leyó. Sin la copia, un producto dado de baja deja la
> solicitud sin poder decir de qué hablaba, y el personal se queda con una descripción que empieza
> por «pero con otro peluche» sin saber pero-con-otro-peluche **de qué**.

#### `b2b.institution_requests` — volumen al por mayor

Lo que cambia es **el precio, por la cantidad**. Y a diferencia de la anterior, **puede no existir
en el catálogo**: *«100 cordones para el desfile»*, *«50 regalos del Día del Padre»*.

| Campo | Tipo | Nulo | Clave | Descripción | Regla de negocio | Default |
|---|---|---|---|---|---|---|
| `institution_request_id` | `integer` | no | PK | Identidad | `GENERATED ALWAYS AS IDENTITY` | |
| `customer_id` | `uuid` | no | FK → `crm.customers` | Quién pide, con su cuenta | Obligatorio | |
| `institution_name` | `text` | no | | Para quién es | **Instantánea, no entidad** | |
| `institution_document` | `text` | sí | | RUC | Opcional | `null` |
| `contact_person` | `text` | sí | | Con quién hablar | **No es el comprador** | `null` |
| `description` | `text` | no | | Qué se pide | No vacío | |
| `quantity` | `integer` | no | | Cuántos | `> 0` | |
| `event_date` | `date` | sí | | Para cuándo | Informativa | `null` |
| `status` | `text` | no | | Estado de la bandeja | Regla 5 | `'recibida'` |
| `staff_notes` | `text` | sí | | Notas internas | **Nunca sale del panel** | `null` |
| `is_active` | `boolean` | no | | Baja lógica | | `true` |
| `created_at` | `timestamptz` | no | | | | `now()` |
| `updated_at` | `timestamptz` | no | | | Trigger | `now()` |

**Restricciones:** `ck_institution_requests_quantity` (`> 0`) · `ck_institution_requests_description`
y `ck_institution_requests_institution_name` (no vacíos) · `ck_institution_requests_status`.

**Índices:** `idx_institution_requests_customer` · `idx_institution_requests_status`.

> **Por qué `institution_name` es una instantánea y no una tabla.** Un colegio **no es cliente de
> M04**: quien tiene cuenta es el profesor que pide. Crear `b2b.institutions` con un solo caso real
> sería exactamente lo que prohíbe `CLAUDE.md:180` — *«No crear abstracciones "por si acaso". Solo se
> generaliza cuando existe un segundo caso real.»*
>
> Y `contact_person` está separado del comprador por lo mismo: **un profesor pide por su colegio**.
> Fundir los dos campos obligaría a elegir cuál de los dos nombres se pierde.

#### `b2b.quotes`

| Campo | Tipo | Nulo | Clave | Descripción | Regla de negocio | Default |
|---|---|---|---|---|---|---|
| `quote_id` | `integer` | no | PK | Identidad | `GENERATED ALWAYS AS IDENTITY` | |
| `quote_number` | `text` | no | **UNIQUE** | El número que se dicta | Legible, con serie de nodo delante | |
| `customer_id` | `uuid` | no | FK → `crm.customers` | A quién se cotiza | | |
| `special_order_lead_id` | `integer` | sí | FK → `special_order_leads` | Origen A | Exactamente uno de los dos | `null` |
| `institution_request_id` | `integer` | sí | FK → `institution_requests` | Origen B | Exactamente uno de los dos | `null` |
| `total_amount` | `numeric(12,2)` | no | | Importe | `>= 0` | |
| `status` | `text` | no | | Estado | Regla 6 | `'borrador'` |
| `invalidated_at` | `timestamptz` | sí | | Cuándo caducó | Regla 7 | `null` |
| `invalidated_reason` | `text` | sí | | Por qué caducó | | `null` |
| `approved_at` | `timestamptz` | sí | | Cuándo la aprobó el cliente | | `null` |
| `paid_at` | `timestamptz` | sí | | Cuándo se registró el pago | | `null` |
| `payment_method` | `text` | sí | | `yape` · `efectivo` · `tarjeta` | `tarjeta` llega con M11 | `null` |
| `payment_reference` | `text` | sí | | Código de operación | | `null` |
| `paid_registered_by` | `text` | sí | | **Nombre** de quien lo registró | Nunca una FK — ver abajo | `null` |
| `is_active` | `boolean` | no | | Baja lógica | | `true` |
| `created_at` / `updated_at` | `timestamptz` | no | | | Trigger | `now()` |

**Restricciones:**

```sql
CONSTRAINT ck_quotes_origen
    CHECK ((special_order_lead_id IS NULL) <> (institution_request_id IS NULL))
```

más `ck_quotes_total_amount` (`>= 0`), `ck_quotes_number` (no vacío), `ck_quotes_status`,
`ck_quotes_payment_method`.

**Índices:** `uq_quotes_number` · `idx_quotes_customer` · `idx_quotes_status`.

> **`quote_number` es lo único de este módulo que se dicta en voz alta.** Por eso es un campo aparte
> y legible: la ADR-016, regla 2, dice que **ningún identificador se muestra al usuario**
> (`ADR-016-identificadores-replicables.md:66`), y que los códigos visibles son *«campos aparte,
> legibles y con su propia serie por nodo»*.

> **`paid_registered_by` guarda el nombre, jamás una FK a `core.admin_users`.** `core.admin_users`
> **no se replica** —lo dice `CLAUDE.md` en su lista— y la ADR-018 establece que *«una tabla que se
> replica no puede referenciar a una que no se replica»*
> (`ADR-018-medios-replicables.md:28`, recogida también como regla 3 en
> `ADR-016-identificadores-replicables.md:81`).
>
> Aquí M07 **tampoco replica**, así que la FK no rompería nada hoy. Se evita igualmente por dos
> motivos: la regla del proyecto es no cruzar esa línea, y el dato que hace falta dentro de un año
> es **quién cobró**, que sobrevive a que esa cuenta se dé de baja o se renombre.

#### `b2b.quote_lines`

Existe por una razón concreta y no por normalizar: **la cotización caduca cuando cambia el precio de
alguno de sus productos**, y para saberlo hay que saber **cuáles son** y **a qué precio se
cotizaron**.

| Campo | Tipo | Nulo | Clave | Descripción | Regla de negocio | Default |
|---|---|---|---|---|---|---|
| `quote_line_id` | `integer` | no | PK | Identidad | | |
| `quote_id` | `integer` | no | FK → `quotes` | A qué cotización pertenece | `ON DELETE CASCADE` | |
| `product_id` | `uuid` | **sí** | FK → `catalog.products` | Producto de catálogo | **Nulable**: «100 cordones» no está en catálogo | `null` |
| `description` | `text` | no | | Qué es esta línea | No vacío | |
| `quantity` | `integer` | no | | Cuántos | `> 0` | |
| `unit_price` | `numeric(12,2)` | no | | **Lo que se le cobra** | `>= 0` | |
| `catalog_price_at_quote` | `numeric(12,2)` | sí | | **Lo que valía en catálogo** | Nulo si la línea no viene del catálogo | `null` |
| `sort_order` | `integer` | no | | Orden en el documento | `>= 0` | `0` |

**Restricciones:** `ck_quote_lines_quantity` (`> 0`) · `ck_quote_lines_unit_price` (`>= 0`) ·
`ck_quote_lines_catalog_price` (`IS NULL OR >= 0`) · `ck_quote_lines_description` (no vacía) ·
`ck_quote_lines_sort_order` (`>= 0`).

> ### Dos precios distintos que no se pueden fundir
>
> | Campo | Qué es | Para qué sirve |
> |---|---|---|
> | `unit_price` | Lo que se le cobra al cliente, **con el descuento mayorista aplicado** | El importe del documento |
> | `catalog_price_at_quote` | **La referencia** contra la que se detecta que el catálogo se movió | La caducidad de la regla 7 |
>
> **Con un solo campo, o no se puede descontar o no se puede caducar.** Si se guarda solo el precio
> cobrado, cualquier descuento se lee como «el catálogo cambió» y la cotización caduca al nacer. Si
> se guarda solo el de catálogo, el descuento no cabe en ningún sitio.
>
> Y `catalog_price_at_quote` **es nulo, no cero**, cuando la línea no viene del catálogo. Es la misma
> distinción que M01 ya pagó por aprender: *«Cero no es lo mismo que nulo: cero es gratis»*
> (`docs/modules/catalog/SPEC.md:206`, y su regla 5 en `:400`; en código,
> `backend/Sillar.Modules.Catalog/Domain/Product.cs:61-63`).

### Relaciones internas

```
special_order_leads   1 ─── 0..N quotes
institution_requests  1 ─── 0..N quotes
quotes                1 ─── 1..N quote_lines
```

### Relaciones cruzadas

| Origen | Destino | Tipo | FK física | Dónde se declara |
|---|---|---|---|---|
| `b2b.special_order_leads.customer_id` | `crm.customers` | dura | **sí** | migración de M07 |
| `b2b.institution_requests.customer_id` | `crm.customers` | dura | **sí** | migración de M07 |
| `b2b.quotes.customer_id` | `crm.customers` | dura | **sí** | migración de M07 |
| `b2b.special_order_leads.product_id` | `catalog.products` | dura | **sí** | migración de M07 |
| `b2b.quote_lines.product_id` | `catalog.products` | dura | **sí** | migración de M07 |
| `b2b.special_order_leads.reference_image_id` | `core.media_assets` | dura | **sí** | migración de M07 |

### Datos semilla

**Ninguno.** Las solicitudes y las cotizaciones son de cada instalación, nunca del producto base. El
`02_seed.sql` es un bloque transaccional vacío e idempotente, igual que el de M02.

El **umbral mayorista** no es semilla de M07: vive en `core.site_settings` (regla 4).

---

## 5. Contrato público

```csharp
namespace Sillar.Modules.B2B.Contracts;

/// <summary>Lo único que otros módulos ven de M07. Su consumidor previsto es M08.</summary>
public interface IB2BService
{
    /// <summary>Las solicitudes de un cliente, de los dos tipos, más recientes primero.</summary>
    Task<IReadOnlyList<CustomerRequestSummary>> ListarSolicitudesDeClienteAsync(
        Guid customerId, CancellationToken ct);

    /// <summary>El detalle de una cotización, por su número visible y acotado a su dueño.</summary>
    Task<QuoteDetail?> ObtenerCotizacionAsync(
        Guid customerId, string quoteNumber, CancellationToken ct);
}

public sealed record CustomerRequestSummary(
    string Kind,                 // "personalizacion" | "volumen"
    int    RequestId,
    string Description,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record QuoteDetail(
    string  QuoteNumber,
    decimal TotalAmount,
    string  Status,
    bool    SigueValida,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? PaidAt,
    IReadOnlyList<QuoteLineDetail> Lines);

public sealed record QuoteLineDetail(
    string  Description,
    int     Quantity,
    decimal UnitPrice);
```

> **`staff_notes` no aparece aquí, ni en ningún endpoint.** Y no basta con no ponerlo: **se afirma
> con una prueba, no con un comentario**. Es la misma regla que M04 se puso para las suyas
> (`docs/modules/crm/SPEC.md:130-131`, con su criterio en `:343`), y por el mismo motivo: un campo
> que solo está fuera del contrato porque nadie lo añadió vuelve a entrar el día que alguien
> proyecte la entidad entera de un tirón.
>
> **`catalog_price_at_quote` tampoco sale.** Es maquinaria interna de la caducidad; enseñarle al
> cliente el precio de lista al lado del suyo es enseñarle su descuento como si fuera un error.

**Eventos publicados:** ninguno en la 1.0.0.

> **Un contrato no se cierra, se estrena.** M07 no tiene hoy ningún consumidor que justifique
> publicar un evento, y publicar uno «por si acaso» crea una promesa que alguien tendrá que mantener
> sin saber para quién. Cuando M08 o M03 necesiten enterarse de algo, se añade entonces, con el caso
> delante.

**Eventos consumidos**, de `Sillar.Modules.Catalog.Contracts.Events`
(`backend/Sillar.Modules.Catalog.Contracts/Events/CatalogEvents.cs`):

| Evento | Qué hace M07 |
|---|---|
| `ProductoActualizado` *(:40)* | Relee el precio de lista. Si cambió respecto a `catalog_price_at_quote`, **invalida las cotizaciones en estado `enviada`** que contengan esa línea |
| `ProductoDesactivado` *(:45)* | Marca `pending_relink` en las solicitudes de ese producto, y **no invalida ninguna cotización** |

> ### La asimetría es deliberada
>
> **Un producto dado de baja sigue teniendo el precio con el que se cotizó.** Lo que cambia el trato
> con el cliente es **el precio**, no la disponibilidad: si se cotizaron 100 cordones a 0,80 y el
> producto deja de venderse, el compromiso de 80 soles sigue siendo el mismo y quien tiene que
> decidir qué hacer es el personal, no un manejador de eventos.
>
> Invalidar por desactivación convertiría cada limpieza de catálogo en una tanda de cotizaciones
> anuladas que el cliente no entiende.

---

## 6. Endpoints

### Públicos — **todos con sesión de cliente, ninguno anónimo**

| Método | Ruta | Descripción | Autenticación |
|---|---|---|---|
| POST | `/api/b2b/special-orders` | Crea una solicitud de personalización | **cliente** |
| POST | `/api/b2b/institution-requests` | Crea una solicitud de volumen | **cliente** |
| GET | `/api/b2b/my-requests` | Las solicitudes propias, de los dos tipos | **cliente** |
| GET | `/api/b2b/quotes/{quoteNumber}` | El detalle de una cotización propia | **cliente** |

> **Aquí no hay ningún formulario de contacto sin cuenta.** Esos son de M04, y esa frontera es la
> que hace que M07 no necesite ninguna infraestructura contra el abuso anónimo: **como toda escritura
> está autenticada, el ritmo se limita contra la cuenta**, igual que hace CORE en el acceso
> (`backend/Sillar.Core/Authentication/LockoutPolicy.cs`, citado por el SPEC de M04 en su `:187`).
> Sin sesión no se llega a crear nada, así que no hay nada que limitar por IP.

`GET /api/b2b/quotes/{quoteNumber}` responde **404 y no 403** cuando el número existe pero es de otro
cliente: distinguirlos convertiría el endpoint en un detector de números de cotización válidos.

### Administración — mínimo `editor`

| Método | Ruta | Descripción | Rol |
|---|---|---|---|
| GET | `/api/admin/b2b/special-orders` | Bandeja, con filtro por estado | editor |
| GET | `/api/admin/b2b/special-orders/{id}` | Detalle, **con** notas internas | editor |
| PUT | `/api/admin/b2b/special-orders/{id}/status` | Cambia el estado | editor |
| PUT | `/api/admin/b2b/special-orders/{id}/notes` | Escribe notas internas | editor |
| PUT | `/api/admin/b2b/special-orders/{id}/relink` | Reenlaza el producto de origen | editor |
| DELETE | `/api/admin/b2b/special-orders/{id}` | Baja lógica | **admin** |
| GET | `/api/admin/b2b/institution-requests` | Bandeja | editor |
| GET | `/api/admin/b2b/institution-requests/{id}` | Detalle | editor |
| PUT | `/api/admin/b2b/institution-requests/{id}/status` | Cambia el estado | editor |
| PUT | `/api/admin/b2b/institution-requests/{id}/notes` | Notas internas | editor |
| DELETE | `/api/admin/b2b/institution-requests/{id}` | Baja lógica | **admin** |
| GET | `/api/admin/b2b/quotes` | Bandeja de cotizaciones | editor |
| GET | `/api/admin/b2b/quotes/{id}` | Detalle con líneas | editor |
| POST | `/api/admin/b2b/quotes` | Crea una cotización **desde una solicitud** | editor |
| PUT | `/api/admin/b2b/quotes/{id}` | Edita líneas e importe — **solo en `borrador`** | editor |
| PUT | `/api/admin/b2b/quotes/{id}/send` | Marca enviada | editor |
| PUT | `/api/admin/b2b/quotes/{id}/approve` | Registra la aprobación del cliente | editor |
| PUT | `/api/admin/b2b/quotes/{id}/payment` | **Registra el pago** | **admin** |
| DELETE | `/api/admin/b2b/quotes/{id}` | Baja lógica | **admin** |

**Todos los de escritura llevan CSRF** y dejan rastro en la auditoría con `module_code = 'b2b'`.

---

## 7. Interfaz de usuario

**Rutas públicas: ninguna.**

No es un olvido, es la decisión que sostiene el modelo: **el formulario se abre desde la ficha de
producto de M01**, y eso es lo que hace que la solicitud **nazca siempre de algo**. Un formulario en
`/solicitudes` recibiría descripciones sueltas sin producto de origen, que es justo lo que las
tablas están diseñadas para no tener.

La pantalla donde el cliente consulta lo suyo es de **M08**.

**Rutas de administración:**

```
/admin/solicitudes/personalizadas
/admin/solicitudes/institucionales
/admin/solicitudes/cotizaciones
```

Grupo de menú **«Solicitudes»**, declarado **junto a las rutas del módulo** — mismo patrón que M02
en `frontend/src/modules/cms/routes.tsx:11-20`, donde `cmsNavigation` exporta un `ModuleNavigation`
con su `group` y sus `items` al lado de `cmsRoutes`.

**Componentes principales:** bandeja con filtro por estado (reutiliza `Table` y `Badge` de
`shared/ui`), cajón de detalle con notas internas, editor de líneas de cotización, y el botón de
solicitud que M01 aloja en la ficha de producto.

**Qué desaparece de la web si el módulo se desactiva:** el grupo «Solicitudes» del menú, las tres
rutas de administración **y el botón de solicitud de la ficha de producto de M01**. Sin ruta muerta y
**sin hueco visual donde estaba el botón**.

---

## 8. Reglas de negocio

1. **Toda solicitud exige cuenta.** Los cuatro endpoints públicos requieren sesión de cliente. Es lo
   que hace **dura** la dependencia sobre M04, y no una comodidad de implementación.

2. **Toda solicitud de personalización parte de un producto.** `product_id` solo es nulo cuando el
   producto se dio de baja después, y entonces `pending_relink` queda en `true` y la instantánea
   sigue diciendo de qué se hablaba.

3. **La pregunta que separa las dos tablas** —y queda escrita porque se va a volver a necesitar:

   > **¿lo que pide existe tal cual en la tienda?**

   | Respuesta | Dónde va |
   |---|---|
   | **No** | `special_order_leads` — personalización |
   | **Sí, en cantidad que cambia el precio** | `institution_requests` — volumen |
   | **Sí, en cantidad normal** | **No es una solicitud: es el carrito de M03** |

   El tercer renglón es el que importa y el que se olvida. **Una solicitud existe para que alguien
   cotice**; sin cotización que hacer, es un carrito con pasos de más y con una espera que el cliente
   no entiende.

4. **El umbral mayorista es un importe, no una cantidad.**

   Veinte arreglos y veinte cordones no son el mismo pedido: **400 soles contra 80**. Contar unidades
   trata igual dos cosas que el negocio trata distinto.

   - Vive en **`core.site_settings`**, no dentro de M07, porque tiene que poder cambiarse sin tocar
     código ni desplegar nada.
   - Se calcula sobre **precio de lista**, nunca sobre el mayorista — usar el precio ya descontado
     para decidir si toca descuento es circular.
   - **Solo se puede evaluar si las líneas están en catálogo.** Lo que se fabrica a medida no tiene
     precio hasta cotizarlo, así que ahí el umbral es **criterio del personal al cotizar**, no
     validación del formulario. Un formulario que exige un importe que todavía no existe rechaza
     encargos legítimos.

5. **Estados de solicitud**, confirmados por el negocio:

   ```
   recibida → en_revision → cotizada → cerrada
                   ↘  rechazada  ↙   (desde cualquiera)
   ```

6. **Estados de cotización:**

   ```
   borrador → enviada → aprobada → pagada
                 ↘  anulada  ↙
   ```

   **Las líneas solo se editan en `borrador`.** Una cotización enviada que cambia sin avisar es una
   que el cliente ya no reconoce, y el documento que tiene en su WhatsApp deja de coincidir con el
   que el negocio cree haber mandado.

7. **Caducidad: solo caducan las que están en `enviada`.**

   - En `borrador` se está editando igual, así que no hay nada que invalidar.
   - En `aprobada` o `pagada` **ya es un acuerdo cerrado**: que el catálogo suba en marzo no puede
     invalidar algo que el cliente pagó en febrero.
   - **No existe caducidad por tiempo.** Solo caduca por precio, y `invalidated_reason` dice cuál.

8. **Nulo y cero no son lo mismo en ningún importe de este módulo.** `catalog_price_at_quote` nulo
   significa «esta línea no viene del catálogo»; cero significa «vale cero». M01 ya pagó por aprender
   la diferencia (`docs/modules/catalog/SPEC.md:206`).

9. **El pago se registra, no se cobra.** Yape y efectivo los teclea un trabajador con su referencia,
   y queda su nombre en `paid_registered_by`. **La tarjeta llega con M11 y no se adelanta nada de
   ella**: `payment_method` admite el valor y ahí termina el alcance de la 1.0.0.

---

## 9. Criterios de aceptación

- [ ] El schema se crea y se elimina sin afectar a otros módulos
- [ ] Los scripts son idempotentes
- [ ] Con el módulo desactivado, la aplicación arranca y no quedan rutas muertas ni enlaces rotos
- [ ] Con una dependencia blanda ausente, el módulo funciona en modo degradado sin errores
- [ ] Todos los endpoints documentados en Swagger
- [ ] La interfaz responde correctamente en móvil y escritorio

Y los propios de M07:

- [ ] **Sin sesión de cliente, los cuatro endpoints públicos dan 401 y no crean nada** — comprobado
      **por API directa**, no solo por pantalla: una pantalla que esconde el botón no prueba que el
      endpoint esté cerrado
- [ ] **`ck_quotes_origen` rechaza los dos orígenes a la vez y también ninguno**, con `INSERT` real
      contra la base
- [ ] **Cambiar el precio de un producto invalida las cotizaciones `enviada` que lo contienen y no
      toca las de `borrador`, `aprobada` ni `pagada`** — afirmado por efecto observable
- [ ] **Dar de baja un producto marca `pending_relink` y no invalida ninguna cotización**
- [ ] **`staff_notes` no aparece en ninguna respuesta pública** — afirmado por prueba
- [ ] **Una cotización en `enviada` no admite edición de líneas**
- [ ] **Con M07 desactivado, la ficha de producto de M01 no deja hueco donde estaba el botón**

---

## 10. Fuera de alcance

| Qué | Dónde va |
|---|---|
| Listas escolares de temporada | **M18 Campaña Escolar**, Fase 2 |
| Cobro con tarjeta | **M11 Pagos** |
| Que el cliente vea lo suyo desde su cuenta | **M08 Portal del Cliente**, Fase 3 |
| Tabla de instituciones | Sin segundo caso real (`CLAUDE.md:180`) |
| Convertir una cotización en pedido de M03 | **No pedido.** Si aparece, se especifica entonces |
| Caducidad por tiempo | **No existe.** Solo se caduca por precio (regla 7) |
| Más de dos orígenes cotizables | **No se decide ahora.** Hoy son dos columnas nulables excluyentes con un `CHECK`, y funciona. Si aparece un tercer origen, dos columnas no escalan — pero elegir hoy entre tabla polimórfica, herencia o discriminador **sin un tercer caso real sería inventar** (`CLAUDE.md:180`). Se replantea con el SPEC de `quotes` de fase 2 |
| Integración con la API de WhatsApp | **Nada, y es una decisión, no una omisión.** Un trabajador manda la cotización desde su propio WhatsApp: sin API de terceros, sin credenciales que custodiar, y sin nada que se rompa el día que Meta cambie algo |
