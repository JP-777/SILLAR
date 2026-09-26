# Diseño técnico provisional — M03 Ventas Online

**Creación:** 26 de septiembre de 2026 — America/Lima
**Última verificación:** 26 de septiembre de 2026 — America/Lima
**Commit verificado:** `711bfba7cf3be80baa146b44e79ddf7a633d695d`

> ## Esto NO es el SPEC de M03, y no puede usarse como tal
>
> El SPEC de M03 **no existe**. La SPEC detenida que redactó el chat de Diseño no está en
> `origin/main`, no está en ninguna rama, y no llegó con el encargo del 26/09
> (`ESCALADAS-M03.md` §a).
>
> Este documento recoge **solo lo que se deduce de decisiones ya cerradas y de contratos que están
> en `main`**. Su propósito es que, cuando llegue la SPEC, lo no controvertido ya esté pensado y no
> haya que rehacerlo. **No convierte ningún bosquejo en decisión de producto:** donde falta una
> decisión, remite a `ESCALADAS-M03.md` y se detiene.
>
> **Nada de aquí se implementa todavía.** El paso 2 está bloqueado por dos condiciones
> independientes, y las dos están abiertas:
>
> | Condición del paso 2 | Estado |
> |---|---|
> | SPEC consolidada con decisión de producto suficiente | **No.** `ESCALADAS-M03.md` §a |
> | Barrera de fronteras del frontend en `main`, con pruebas y llamada desde la puerta, sobre un SHA anotado | **No.** `MATRIZ-DIFERENCIAS-M03.md` §2 |

---

## 1 · Lo que la plataforma ya decide, y no se vuelve a decidir

Todo esto se lee de módulos que ya están en `main`. Se copia el patrón; no se inventa.

| Pieza | Patrón que se sigue | Dónde está escrito |
|---|---|---|
| Proyecto | `backend/Sillar.Modules.Sales` + `Sillar.Modules.Sales.Contracts` + `Sillar.Modules.Sales.Tests`, junto a él | `CLAUDE.md` §Pruebas; los tres de CRM en `backend/` |
| `IModule` | `SalesModule : IModule, IModuleMigrations`, con `Code`, `Version`, `HardDependencies = ["core","catalog","crm"]`, `SoftDependencies = []` | `backend/Sillar.Modules.Crm/CrmModule.cs:20`, `:63-66` |
| Schema y contexto | `SalesDbContext` con `HasDefaultSchema("sales")` y su propio `__migrations` dentro del schema | `CrmModule.cs:32-39`; `CLAUDE.md` §Módulos |
| Orden en el menú | `DisplayOrder = 30`. M01=10, M02=20, M04=40 | `CrmModule.cs:62-63` — «M01=10, M02=20; M04 conserva el orden del catálogo modular» |
| Nodo y replicación | `services.TryAddNodeIdentity(configuration)`; las entidades replicadas implementan `IReplicatedEntity` y **el `DbContext` rellena las cuatro columnas al guardar, no quien escribe la entidad** | `backend/Sillar.Shared/Replication/IReplicatedEntity.cs`; `NodeIdentity.cs` |
| Sesión de cliente | Política `CustomerAuthorization.PolicyName` (`"crm:customer"`), y `ICurrentCustomer` para saber quién es | `backend/Sillar.Modules.Crm.Contracts/CustomerAuthorization.cs`; `ICurrentCustomer.cs` |
| CSRF | `CustomerCsrfEndpointFilter` en las escrituras de cliente, `CsrfEndpointFilter` en las de personal | `Sillar.Modules.Crm.Contracts/CustomerCsrfEndpointFilter.cs`; `Sillar.Core.Contracts/CsrfEndpointFilter.cs` |
| Configuración | El plazo de pago se lee con `ISettingsReader.Get<T>`, nunca tocando `core.site_settings` | `Sillar.Core.Contracts/ISettingsReader.cs` |
| Auditoría | `IAuditWriter`, y **la entidad concreta en el resumen**: «Pedido 2026-0147 pasa a Preparando», no «cambio de estado» | `Sillar.Core.Contracts/IAuditWriter.cs`; `docs/ANTES-DE-EMPEZAR-UN-MODULO.md` §5 |
| Colaciones | El código visible se busca **exacto**: no necesita colación no determinista ni trigram. **Pero si la lista del panel busca por nombre de cliente**, ese snapshot lleva `core.es_search` y su índice, y entonces `LIKE` está prohibido sobre él — la salida es `COLLATE "C"` en la expresión. **Lo decide la SPEC**, y cuesta un minuto antes del primer `CREATE TABLE` y un bloqueo después | `docs/ANTES-DE-EMPEZAR-UN-MODULO.md` §7; la salida ya trabajada en `docs/modules/crm/DATOS.md:102` |

## 2 · Lo que M03 consume de M01 y de M04 — contratos reales, leídos

**De M01 (`Sillar.Modules.Catalog.Contracts`):** `ICatalogService`, y de él dos métodos:

- `ObtenerItemAsync(itemId)` → `ItemSnapshot`, que es **lo que se congela en la línea del pedido**.
  Trae `ItemId`, `ProductId`, `ProductName`, `VariantValue`, `Code`, `Barcode`, `Price` y
  `SaleUnit` (`ItemSnapshot.cs:30-38`).
- `ItemExisteYEstaActivoAsync(itemId)` → «Para validar antes de vender o contar»
  (`ItemSnapshot.cs`, `ICatalogService`).

**Se vende contra la variante, no contra el producto.** `ItemSnapshot.cs:9-11` no deja margen:
«Identificador **de la variante, no del producto**: quien vende, cuenta o factura lo hace contra
ella». `ProductId` viaja «solo para agrupar en informes: **nada vende ni cuenta contra él**»
(`:12-15`). El mapa de `ARQUITECTURA_MODULAR.md:213` dice lo contrario y hay que corregirlo
(`ESCALADAS-M03.md` §e1).

**De M04 (`Sillar.Modules.Crm.Contracts`):** `ICurrentCustomer` para la sesión y
`ICustomerSnapshotReader` para congelar al cliente — **este último no sirve tal como está** para un
pedido de recojo en tienda (`ESCALADAS-M03.md` §e3).

**`ItemSnapshot.Price` tiene tres estados y los tres significan cosas distintas.** Está documentado
en `ProductPickerItem.cs` con el aviso de que **ya mordió una vez**: «en la tarjeta pública,
"Gratis" cortocircuitaba antes de llegar al número». Para M03:

| `Price` | Qué es | Qué hace M03 |
|---|---|---|
| `null` | **A consultar** | **Fuera del carrito y del pago.** Lleva a pedir información (§d de escaladas) |
| `0` | **Gratis** | **Se vende.** Es un precio |
| `> 0` | El precio | Se vende |

**La comprobación es `Price is null`, jamás una comprobación de «falsy».** Es el mismo defecto de
la tarjeta pública escrito en otro lenguaje.

## 3 · Forma de los datos — propuesta, con el barrido de la ADR-018 ya hecho

**No es un diccionario de datos.** El diccionario es el paso 2 y está bloqueado. Esto fija la forma
para que el barrido de fronteras esté hecho antes, que es cuando sale barato.

Las ventas se replican (`ADR-016:54`; `CLAUDE.md` §convenciones). Por tanto `sales.orders` y
`sales.order_items` llevan **`uuid` v7 generado por la aplicación**, `origin_node` y `row_version`.

### `sales.orders`

| Campo | Tipo | Por qué así |
|---|---|---|
| `order_id` | `uuid` v7 | Se replica. **Nunca se muestra** (ADR-016 regla 2) |
| `order_code` | `text`, `UNIQUE` | Lo que se dicta. **Su formato está escalado** — `ESCALADAS-M03.md` §b1 |
| `customer_id` | `uuid`, FK → `crm.customers` | Dependencia **dura**: FK cruzada directa en la migración de M03, **sin** `sales_crm.sql` (`ARQUITECTURA_MODULAR.md:235`) |
| snapshot del cliente | `text` × varios | Nombre, correo, teléfono, documento. El pedido «conserva nombre y precio del momento» (`CLAUDE.md`) y sobrevive a `DROP SCHEMA crm CASCADE` (`ARQUITECTURA_MODULAR.md:270`) |
| `status` | `text` + `CHECK` de siete valores | **No una tabla de catálogo:** cruzaría la ADR-018. Ver `ESCALADAS-M03.md` §e2 |
| `payment_due_at` | `timestamptz` | El plazo para pagar. **No es una reserva**, y ninguna pantalla lo llama así |
| `total_amount` | `numeric(12,2)`, `CHECK >= 0` | `CLAUDE.md` §convenciones |
| `is_active` | `boolean` | Baja lógica siempre, nunca `DELETE` |
| `origin_node`, `row_version`, `created_at`, `updated_at` | | Los rellena el `DbContext`, no quien escribe (`IReplicatedEntity.cs`) |

**Lo que deliberadamente NO lleva:** ninguna columna de reserva de existencias, ninguna cantidad
apartada, ninguna dirección de envío, y **ninguna FK a `core.admin_users`**.

### `sales.order_items`

Una línea por variante comprada, con el `ItemSnapshot` congelado entero: `item_id` con FK a
`catalog.product_items` —replicada → replicada, y dependencia dura—, más `product_id`,
`product_name`, `variant_value`, `sale_unit`, `unit_price` y `quantity`, los dos últimos con
`CHECK >= 0` y `> 0` respectivamente.

`product_id` se guarda **sin FK y solo para agrupar**, que es exactamente lo que
`ItemSnapshot.cs:12-15` dice que es.

### Los dos hechos que no son estados

Separar el **hecho de pago** del **estado del pedido** no es una preferencia de diseño: está
cerrado en `docs/PENDIENTES.md:497-499`, con el motivo escrito —«Fundirlos obliga a rehacer la
máquina de estados **con pedidos reales dentro**»— y con el precedente de M04, «un estado que carga
dos significados obliga a elegir el peor comportamiento para los dos».

- **Pagos:** cuándo, método, referencia y **el nombre** de quien lo registró. `payment_method` es
  `text` con `CHECK` sobre una lista cerrada, y **cuál es esa lista está escalado** — el efectivo
  (`ESCALADAS-M03.md` §c).
- **Historial de estados:** de qué estado a cuál, cuándo, y **el nombre** de quien lo cambió.

**En los dos, el nombre y jamás una FK.** `docs/PENDIENTES.md:493-495` lo fija para el pago;
`docs/modules/b2b/SPEC.md:188` y `:209-217` lo hacen ya en `paid_registered_by` por el mismo
motivo. M03 lo extiende al cambio de estado **por analogía directa, no como decisión nueva**: la
`ADR-018` obliga igual, y el dato que hace falta dentro de un año es quién actuó, «que sobrevive a
que la cuenta se dé de baja o se renombre».

### El correlativo, y por qué su mecanismo sí se puede diseñar hoy

El formato está escalado; el mecanismo no depende de él.

**Propuesta:** una tabla contador por año, **local al nodo** —clave `integer GENERATED ALWAYS AS
IDENTITY`, **no replicada**— y el número se toma con un `UPDATE … RETURNING` dentro de la misma
transacción que inserta el pedido. **Ninguna tabla replicada la referencia:** el pedido guarda el
código como `text`, no una FK. Por tanto la ADR-018 queda limpia, y el contador es del nodo, que es
lo que debe ser.

**El `UNIQUE` de `order_code` es la barrera, no el adorno.** El contador evita la colisión; el
índice la hace imposible. Y se prueba provocándola: N inserciones concurrentes, y ni un duplicado
ni un hueco.

## 4 · Endpoints — forma, no contrato cerrado

Público, **todo con sesión de cliente** —comprar exige cuenta, y eso es lo que hace dura la
dependencia sobre M04 (`ARQUITECTURA_MODULAR.md:58`, `:100`)— y todas las escrituras con CSRF.

| Método | Ruta | Regla que trae consigo |
|---|---|---|
| POST | `/api/sales/orders` | Exige sesión **y `EmailVerified`** (`docs/modules/crm/SPEC.md:238`). Rechaza toda línea con `Price is null`. Valida cada variante con `ItemExisteYEstaActivoAsync` |
| GET | `/api/sales/my-orders` | Los propios |
| GET | `/api/sales/my-orders/{orderCode}` | **404 y no 403** cuando el código existe pero es de otro cliente. Precedente y motivo en `docs/modules/b2b/SPEC.md:376-377`: distinguirlos «convertiría el endpoint en un detector de números válidos» |

Administración, mínimo `editor`: lista de pedidos, detalle, **registro de pago —incluido el
tardío—** y cambio de estado. Cada endpoint con comentarios XML y visible en Swagger (`CLAUDE.md`
§reglas de trabajo, 6).

**Los conflictos se dicen con una frase que nombra qué lo impide y qué hacer, y los botones nombran
su acción.** Ningún «Ha ocurrido un error», ningún «Aceptar» (`CLAUDE.md` §Frontend). Ejemplos que
este módulo necesita:

- «Este producto se cotiza. Pide información y te decimos el precio.» — no «producto no
  disponible».
- «El plazo para pagar venció el 28 de septiembre. El pedido sigue en pie, pero ya no está
  garantizado.» — **y nunca «tu reserva expiró»**, porque no hubo reserva (encargo §9).

## 5 · Plan de pruebas

Nombres **en español**, para que la salida de `dotnet test` se lea como la lista de reglas que el
sistema garantiza. Las pruebas de lógica **no tocan la base** (`CLAUDE.md` §Pruebas).

**Sin base de datos, en `Sillar.Modules.Sales.Tests`:**

- Un precio nulo es «a consultar» y no entra al carrito · **un precio cero es gratis y sí entra**
- Las cinco transiciones de la secuencia principal se permiten
- Vencido no es Cancelado, y vencer el plazo no cancela
- Ningún texto de estado ni de conflicto promete existencia garantizada — barrido sobre las cadenas
  visibles, con la lista de palabras prohibidas: «reserva», «reservado», «apartado», «garantizado»
- El pago y el estado del pedido no comparten columna ni tipo

**Con base, y contra el esquema real:**

- Dos pedidos simultáneos no obtienen el mismo `order_code`, y no se salta ninguno
- Un cliente no ve el pedido de otro: **404, no 403**
- Sin `EmailVerified` no se puede crear un pedido
- Registrar un pago sobre un pedido Vencido deja **siempre** un resultado operativo o un pendiente
  visible — nunca un pago huérfano
- `DROP SCHEMA crm CASCADE` deja los pedidos históricos legibles por sus snapshots
  (`ARQUITECTURA_MODULAR.md:270`)
- `DROP` de un producto del catálogo con ventas activas **falla con un error explícito**
  (`ARQUITECTURA_MODULAR.md:271`)

**Las barreras nuevas se provocan en tres direcciones** —legal, ilegal, y rompiendo la propia
autoprueba a propósito— antes de darlas por puestas, y se registra el retorno a verde
(`docs/ANTES-DE-EMPEZAR-UN-MODULO.md` §2). Vale para las tres que este módulo va a traer: el
`UNIQUE` del código, el `CHECK` de los siete estados y el rechazo de líneas «a consultar».

**Lo que no se puede planificar todavía:** las transiciones de cancelación y reactivación
(`ESCALADAS-M03.md` §b2) y la lista de métodos de pago (§c). Sin esas decisiones, una prueba
escribiría la regla en vez de comprobarla.

## 6 · Montaje y desmontaje — el criterio de cierre

`sales` se instala aplicando sus migraciones y se desinstala con `99_drop.sql`
—`DROP SCHEMA sales CASCADE`, idempotente—. Después:

- `catalog` y `crm` siguen enteros. La FK cruzada vive en la migración de M03, así que se va con su
  schema.
- El frontend no monta rutas de `sales`: `has('sales')` es falso y la rama del árbol no existe
  (`frontend/src/app/routes.tsx:38-55`). **No queda ruta muerta**, porque la ruta no existe.
- Menú, portada y pie se construyen desde `GET /api/capabilities`, así que no queda hueco escrito a
  mano (`CLAUDE.md` §Frontend).
- El hueco de historial de pedidos de la ficha de cliente de M04 vuelve a decir que no hay módulo
  de pedidos, **preguntando al contenedor y no al registro de módulos**
  (`docs/modules/crm/SPEC.md:341-345`).
- Y el ciclo se prueba entero: **instalar, desinstalar, reinstalar**, sin enlace roto, ruta muerta,
  hueco visual ni fallo de arranque.

## 7 · Lo que M03 NO hace

| No hace | Dónde vive |
|---|---|
| Reservar existencias | M09 Inventario, en el ERP (`ROADMAP_MODULAR.md:122`) |
| Cobrar con tarjeta | M11, Fase 4 (`PENDIENTES.md:486-488`) |
| Entregar a domicilio, calcular envío | Nada de v1 (encargo §3) |
| Cotizar, o atender lo que no cabe en el carrito | M07 (`docs/modules/b2b/SPEC.md:23`, `:456-460`) |
| Convertir una cotización en pedido | **No pedido** (`docs/modules/b2b/SPEC.md:545`) |
| Saldo a favor, devoluciones | Pendientes con disparador propio (encargo §8) |
| Recordatorio a las 24 h | Pendiente: cuando el correo esté comprobado en producción. Hoy se acreditó con **Mailpit, solo desarrollo/pruebas** (`ROADMAP_MODULAR.md:67`) |
| Mostrar el historial de pedidos en el portal del cliente | M08, Fase 3 (`ARQUITECTURA_MODULAR.md:116`) |
