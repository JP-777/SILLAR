# SILLAR — Arquitectura Modular

> **S**istema **I**ntegrado y **L**icenciable de **L**ogística, **A**dministración y **R**etail

**Versión:** 1.1 · **Fecha:** 15 de agosto de 2026 · **Responsable:** JP
**Estado:** Aprobado como base de desarrollo

> **Dos productos, una plataforma (ADR-015, enmendada por la ADR-017).** SILLAR es la plataforma
> y el código común. Sobre ella se publican **SILLAR WEB** —servicio web, en la nube, una
> instancia por cliente— y **SILLAR ERP** —escritorio, base interna del negocio, varias
> máquinas y sucursales—. Comparten código y módulos; no comparten instalación. Un módulo
> escrito una vez sirve a los dos.

Este documento define cómo se construye la plataforma para que cada sistema sea **modular y desmontable**, de modo que pueda venderse por módulos, licenciarse o entregarse como producto completo a distintos negocios.

**El negocio que originó el proyecto deja de ser "el proyecto" y pasa a ser la primera instalación del producto.**

---

## 1. Principio rector

> Las dependencias entre módulos son **dirigidas, declaradas y nunca circulares**.

Cada módulo declara sus dependencias en dos niveles:

| Tipo | Significado | Implicancia técnica |
|---|---|---|
| **Dura** | El módulo no puede funcionar sin el otro | Se permite FK física entre schemas. El instalador impide activarlo si falta la dependencia. |
| **Blanda** | El módulo funciona solo, y se enriquece si el otro está presente | Prohibida la FK en el script base. Columna nullable + datos snapshot. La FK se crea en un *script de integración* solo si ambos módulos están activos. |

Nunca existe una FK en dirección contraria a la dependencia declarada. Nunca hay ciclos.

**Ejemplo de JP, formalizado:** Seguimiento de Servicios depende *duro* de Servicios. Servicios no conoce a Seguimiento. Si Seguimiento no está instalado, Servicios funciona igual y nadie lo nota.

**Descubrimiento de capacidades:** un módulo nunca pregunta "¿está instalado X?" con un `if` disperso en el código. El backend expone `GET /api/capabilities` con la lista de módulos activos y sus versiones; el frontend arma menú, home y secciones a partir de esa respuesta.

---

## 2. Catálogo de módulos

### CORE — Núcleo de plataforma

No es vendible ni desmontable. Es la base sobre la que se enchufa todo lo demás.

**Contiene:** identidad de la instalación, catálogo de módulos y su activación, usuarios administradores, autenticación y roles, configuración general del sitio, gestión de medios, auditoría, y el registro de capacidades.

**Schema:** `core`
**Depende de:** nada.

---

### Módulos funcionales

| ID | Módulo | Schema | Depende de | Nivel |
|---|---|---|---|---|
| **M01** | Catálogo de Productos | `catalog` | CORE | MVP |
| **M02** | Contenido Web / CMS | `cms` | CORE | MVP |
| **M03** | Ventas Online | `sales` | M01 (dura), **M04 (dura)** | MVP |
| **M04** | Clientes y Contacto | `crm` | CORE | MVP |
| **M05a** | Servicios — Vitrina | `services` | CORE | MVP |
| **M05b** | Servicios — Órdenes | `service_orders` | M05a (dura), M04 (blanda) | Fase 2 |
| **M06** | Seguimiento de Servicios | `tracking` | M05b (dura) | Fase 2 |
| **M07** | Solicitudes B2B y Especiales | `b2b` | **M01 (dura), M04 (dura)** | MVP |
| **M08** | Portal del Cliente | `portal` | M04 (dura), M03/M06 (blandas) | Fase 3 |
| **M09** | Inventario | `inventory` | M01 (dura) | Fase 4 |
| **M10** | Reportes y Analítica | `reporting` | consume eventos | Fase 4 |
| **M11** | Pagos en línea | `payments` | M03 (dura) | Futuro |
| **M13** | Punto de Venta — incluye caja y turnos | `pos` | M01 (dura) · M04, M09, M14 (blandas) | **ERP** |
| **M14** | Comprobantes Electrónicos | `billing` | CORE | **ERP** |
| **M15** | Compras y Proveedores | `purchasing` | M01, M09 (duras) | **ERP** |
| **M16** | Sincronización entre nodos | `sync` | CORE | **ERP** |
| **M17** | Sucursales | `branches` | CORE (dura) · M09, M13 (blandas) | **ERP** |
| **M12** | Asistente conversacional | `assistant` | ninguna dura | Futuro |
| **M18** | Campaña Escolar | `school_campaign` | M01 (dura) · M03, M04 | Fase 2 |

> **Tres filas de esta tabla cambiaron el 24 de agosto de 2026, y el porqué no está aquí.**
> Está razonado en `docs/modules/services/DECISIONES-PREVIAS-M05a.md` y en
> `docs/modules/b2b/DECISIONES-PREVIAS-M07.md`, que es donde vive el argumento completo. Aquí
> queda el resultado y una línea de cada uno:
>
> - **M05b deja de compartir schema con M05a.** Dos módulos en un mismo schema tienen un
>   `99_drop.sql` cada uno, y **el de cualquiera de los dos se lleva por delante las tablas del
>   otro**. Desinstalar M05b no puede vaciar la vitrina de servicios.
> - **M07 pasa a depender duro de M01 y de M04.** Una solicitud exige cuenta, así que M04 deja de
>   ser blanda — **el mismo criterio que movió a M03 el 21 de agosto**. Y M01 nunca estuvo
>   declarado, aunque una solicitud especial nombra productos desde que existe.
> - **M18 Campaña Escolar sale de M07.** Es operación de temporada —listas escolares atendidas por
>   personal, con ofertas y, previsto, agentes de IA—, **no contenido publicado**. `BITACORA.md:781`
>   la había aparcado dentro de M07 el 15 de agosto; con el alcance de M07 escrito se ve que no
>   cabe.

### Detalle por módulo

**M01 — Catálogo de Productos.** Categorías, productos, imágenes de producto, marcas, búsqueda y filtros. Es la base de casi todo lo comercial. Vendible solo, como catálogo de exhibición sin venta.

**M02 — Contenido Web / CMS.** Banners, promociones, trabajos destacados, redes sociales, páginas institucionales. **Totalmente independiente**: un negocio puede comprar solo esto y tener una web administrable sin catálogo ni ventas.

**M03 — Ventas Online.** Carrito, pedidos, detalle de pedido, estados de pedido, checkout y confirmación. Depende duro de M01 porque no se vende lo que no está catalogado.

**Y depende duro de M04, desde el 21 de agosto de 2026.** Estaba declarado como blando —«si Clientes no está instalado, el pedido guarda los datos de contacto como snapshot y funciona igual»— y **la cuenta obligatoria para comprar lo convierte en duro**: si hay que tener cuenta, el módulo que guarda al cliente tiene que estar. Lo que sigue siendo cierto es el snapshot: el pedido conserva nombre y dirección de entrega del momento, porque un cliente que se muda no cambia adónde se entregó lo de antes.

> **La identidad del cliente vive en M04, no en CORE.** `core.admin_users` es del personal: su rol es obligatorio y la base lo restringe a los tres de administración (`ck_admin_users_role`), la tabla se llama así, y no hay registro propio ni recuperación de contraseña. Meter clientela ahí exige una migración y hace que «usuarios» signifique dos cosas en el panel.

> Nota: los campos snapshot que ya se añadieron en la v2 del script (`customer_full_name`, `customer_phone`, `customer_email`, `delivery_address`, `delivery_reference`) son exactamente lo que hace posible esta independencia. Ese ajuste, hecho por otra razón, resultó ser la pieza clave del desacople.

**M04 — Clientes y Contacto.** Registro de clientes y mensajes del formulario de contacto. Vendible solo, como módulo de captación de contactos.

**M05a — Servicios (Vitrina).** Catálogo de servicios permanentes (anillado, fotocopiado, manualidades) para mostrarlos en la web. Es lo que pide el PRD para el MVP.

**M05b — Servicios (Órdenes).** Registro real de órdenes de servicio: qué se encargó, quién, cuándo, con qué características y a qué precio. **Esto no existe en el diseño actual y hay que construirlo.**

**M06 — Seguimiento de Servicios.** Historial de estados de una orden de servicio y tablero tipo kanban. Es la funcionalidad de trazabilidad que pidió la cliente en la entrevista.

**M07 — Solicitudes B2B y Especiales.** Pedidos especiales y solicitudes de colegios, empresas y profesores, con estados de atención y cotización.

**M08 — Portal del Cliente.** Cuentas de cliente, historial y consulta de estados. Muestra pedidos si M03 está activo y estados de trabajo si M06 está activo; funciona con solo el perfil si no hay ninguno.

**M09 — Inventario.** Movimientos de stock. **Deja de ser fase 4:** es pieza central de SILLAR ERP, porque sin existencias fiables no hay punto de venta que valga. Cuenta contra `catalog.product_items` —la variante—, no contra el producto: si el verde y el azul comparten existencia, la tienda con 3 verdes y 0 azules diría «hay 3».

**M10 — Reportes.** Analítica comercial construida sobre eventos publicados por los demás módulos.

**M13 — Punto de Venta.** Mostrador, caja y turnos. **Depende de M14 de forma blanda**: sin Comprobantes instalado registra la venta sin documento fiscal, que es lo que permite venderlo fuera de Perú. Caja no es un módulo aparte: apertura, cierre y arqueo viven aquí, porque no existe un negocio que quiera control de caja sin punto de venta.

**M14 — Comprobantes Electrónicos.** Integración con un proveedor autorizado de SUNAT, encapsulada tras `IFiscalDocuments` (ADR-014). Ningún otro módulo sabe que existe un tercero.

**M15 — Compras y Proveedores.** Órdenes de compra y recepción de mercadería.

**M16 — Sincronización entre nodos.** Réplica de los datos compartidos entre la base de mando y las copias (ADR-017). El mando es uno solo y explícito; ningún nodo se declara mando a sí mismo.

**M17 — Sucursales.** Locales del negocio, conteo global de existencias, consulta de disponibilidad ajena, separaciones y traslados entre locales.

> **Este módulo no aporta «sucursal»: aporta «más de una».** Es la distinción que lo hace
> desmontable. M09 guarda las existencias contra una ubicación; sin M17 instalado hay
> exactamente una y todo funciona igual. Con M17 hay varias, y solo entonces aparecen el
> conteo global, la consulta a otro local, la separación y el traslado.
>
> La dependencia va de M17 hacia M09, no al revés: M17 le pregunta a M09 cuánto hay en cada
> ubicación. **M09 no sabe qué es una sucursal** y no debe saberlo. Es blanda en los dos
> sentidos posibles: un negocio de servicios con varios locales instala M17 sin M09 y sigue
> teniendo locales para caja y turnos; un negocio de un solo local instala M09 sin M17 y
> nunca ve la palabra «sucursal» en la interfaz.
>
> La FK de la ubicación de M09 hacia la tabla de locales de M17 va en
> `database/integrations/inventory_branches.sql`, nunca en una migración.

---

## 3. Matriz de dependencias

```
                CORE
                 │
     ┌───────────┼───────────┬──────────┬──────────┐
     │           │           │          │          │
    M01         M02         M04       M05a      (otros)
  Catálogo      CMS       Clientes   Servicios
     │                       │  │        │
     │  ┌────────────────────┘  │        │
     │  │ (blanda)              │        │
     ▼  ▼                       │        ▼
    M03 Ventas                  │      M05b Órdenes
     │                          │        │
     │        ┌─────────────────┘        │
     │        │ (blanda)                 │
     │        ▼                          ▼
     │       M07 B2B                   M06 Seguimiento
     │                                   │
     ├──────────► M09 Inventario         │
     │                                   │
     └───────────┐        ┌──────────────┘
        (blanda) ▼        ▼ (blanda)
                M08 Portal del Cliente
                      │
                      │ (dura)
                      ▼
                 M04 Clientes
```

Leyenda: flecha continua = dependencia dura · flecha marcada como blanda = opcional.

---

## 4. Aislamiento de datos: un schema por módulo

### Reparto de las tablas ya diseñadas

Las 17 tablas del diseño original se reparten así. **Ninguna se pierde**; solo cambian de schema.

| Schema | Tablas existentes | Tablas nuevas a crear |
|---|---|---|
| `core` | `admin_users`, `site_settings` | `installation`, `modules`, `module_activations`, `media_assets`, `audit_log` |
| `catalog` | `categories`, `products`, `product_images` | `brands` (fase 2) |
| `cms` | `banners`, `promotions`, `featured_projects`, `social_links` | `pages` (fase 2) |
| `crm` | `customers`, `contact_messages` | — |
| `sales` | `orders`, `order_items`, `order_statuses` | — |
| `services` | `services` | — |
| `service_orders` | — | `service_orders`, `service_order_items`, `service_order_statuses` |
| `b2b` | `special_order_leads`, `institution_requests` | `quotes` (fase 2) |
| `tracking` | — | `service_status_history` |
| `portal` | — | `users`, `customer_profiles` |
| `inventory` | — | `inventory_movements` |

Decisión sobre `social_links`: pasa a `cms` en lugar de `core`, porque es contenido editable de la web y no configuración de plataforma.

### Reglas de claves foráneas

**Permitidas (dependencia dura, van en el script base del módulo dependiente):**

```
catalog.products.category_id        → catalog.categories        (interna)
catalog.product_images.product_id   → catalog.products          (interna)
sales.order_items.order_id          → sales.orders              (interna)
sales.order_items.product_id        → catalog.products          (cruzada, M03→M01)
sales.orders.order_status_id        → sales.order_statuses      (interna)
crm.contact_messages.customer_id    → crm.customers             (interna)
tracking.service_status_history.service_order_id → service_orders.service_orders  (cruzada, M06→M05b)
inventory.inventory_movements.product_id → catalog.products     (cruzada, M09→M01)
sales.orders.customer_id            → crm.customers             (cruzada, M03→M04)
b2b.special_order_leads.customer_id → crm.customers             (cruzada, M07→M04)
b2b.institution_requests.customer_id→ crm.customers             (cruzada, M07→M04)
```

**Prohibidas en el script base (dependencia blanda, van en script de integración):**

```
portal.customer_profiles.customer_id→ crm.customers             (M08 ⟶ M04, dura pero cross-schema)
```

Estas columnas se crean **nullable y sin FK**. La restricción se añade después mediante:

Hoy no queda ninguno vivo en el plan. El mecanismo sigue siendo el de siempre: **un script por
pareja, en `database/integrations/<a>_<b>.sql`, que solo se ejecuta si los dos módulos están
instalados.**

> **Ni `sales_crm.sql` ni `b2b_crm.sql` están ya en esa lista**, y por el mismo motivo: M03 el 21
> de agosto y M07 el 24 pasaron a depender **duro** de M04, así que sus FK a `crm.customers` van
> en la migración base del módulo dependiente. **Una dependencia dura no se desmonta por
> separado**, y un script de integración existe justamente para poder desmontarla. El de M07
> está razonado en `docs/modules/b2b/DECISIONES-PREVIAS-M07.md`.
>
> Los ejemplos de más abajo siguen nombrando `sales_crm.sql` **porque explican el mecanismo**, no
> porque ese archivo tenga que existir.

### Estructura de scripts

```
database/
├── modules/
│   ├── core/       { 02_seed.sql, 99_drop.sql }
│   ├── catalog/    { 02_seed.sql, 99_drop.sql }
│   ├── cms/        { ... }
│   ├── crm/        { ... }
│   ├── sales/      { ... }
│   ├── services/        { ... }   M05a — vitrina
│   ├── service_orders/  { ... }   M05b — su propio 99_drop.sql, que es el motivo del reparto
│   └── b2b/             { ... }
├── integrations/
│   └── sales_crm.sql    ← ejemplo del mecanismo. Ninguna pareja lo necesita hoy
└── install.sql          orquestador: lee los módulos a instalar y ejecuta en orden
```

Las tablas las crean las **migraciones de EF Core** de cada módulo, con su historial `__migrations` dentro de su propio schema (ADR-009). Los scripts que quedan aquí son el seed y la desinstalación. Desmontar un módulo es ejecutar su `99_drop.sql`.

Todos los scripts son idempotentes.

### Desinstalación: qué pasa realmente al quitar un módulo

El patrón fue probado contra PostgreSQL 16 antes de adoptarlo. Comportamiento verificado:

- Al ejecutar `DROP SCHEMA crm CASCADE`, PostgreSQL elimina también la FK que el script de integración había puesto en `sales.orders`. **El módulo Ventas sobrevive**, conserva los pedidos históricos gracias a los campos snapshot y sigue aceptando pedidos nuevos.
- Una dependencia **dura** sí protege: intentar eliminar `catalog.products` con ventas activas falla con un error explícito, porque `sales.order_items` la referencia. Es el comportamiento correcto: el instalador debe impedir esa operación antes de intentarla.

**Hallazgo que obliga a una regla:** tras desmontar una dependencia blanda, las columnas de referencia quedan con valores huérfanos —un pedido con `customer_id = 1` apuntando a un cliente que ya no existe—. La FK desaparece, pero el dato queda.

Por eso cada script de integración tiene su contraparte:

```
database/integrations/sales_crm.sql        añade la FK
database/integrations/sales_crm_drop.sql   elimina la FK y anula las referencias huérfanas
```

El `99_drop.sql` de un módulo debe ejecutar primero los `_drop.sql` de sus integraciones. Sin esto, la desinstalación deja basura silenciosa en las tablas del módulo que se queda.

---

## 5. Backend: modular monolith en .NET

### Estructura de la solución

```
backend/
├── Sillar.Api/                        host, arranque, middleware, capabilities
├── Sillar.Shared/                     tipos base, resultados, errores, paginación
├── Sillar.Core/                       módulo núcleo: licencias, auth, settings
└── Sillar.Modules/
    ├── Sillar.Modules.Catalog/
    │   ├── Contracts/                   ÚNICO punto visible desde otros módulos
    │   ├── Domain/
    │   ├── Data/                        CatalogDbContext → schema "catalog"
    │   ├── Endpoints/
    │   └── CatalogModule.cs             implementa IModule
    ├── Sillar.Modules.Cms/
    ├── Sillar.Modules.Crm/
    ├── Sillar.Modules.Sales/
    ├── Sillar.Modules.Services/        M05a — vitrina, schema "services"
    ├── Sillar.Modules.ServiceOrders/   M05b — órdenes, schema "service_orders"
    ├── Sillar.Modules.Tracking/
    └── Sillar.Modules.B2B/
```

### El contrato `IModule`

Cada módulo implementa la misma interfaz:

```csharp
public interface IModule
{
    string   Code            { get; }   // "catalog"
    string   DisplayName     { get; }   // "Catálogo de Productos"
    string   Description     { get; }   // obligatoria: alimenta el panel de módulos
    string   Version         { get; }   // "1.0.0"
    int      DisplayOrder    { get; }   // orden en el panel
    string[] HardDependencies{ get; }   // ["core"]
    string[] SoftDependencies{ get; }   // []

    void RegisterServices(IServiceCollection services, IConfiguration config);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
```

`IsCore` no está en la interfaz: se deriva de `Code == "core"`, para que ningún módulo pueda
declararse núcleo a sí mismo.

El host descubre los módulos, **valida el grafo de dependencias**, filtra por licencia activa y registra únicamente los habilitados. Un módulo no licenciado no expone endpoints: no es que devuelva 403, es que la ruta no existe.

### Reglas de código innegociables

1. Un módulo **solo** puede referenciar `Sillar.Shared`, `Sillar.Core.Contracts` y los `Contracts` de los módulos de los que depende. Nunca `Domain` ni `Data` ajenos.
2. Cada módulo tiene **su propio `DbContext`** con `HasDefaultSchema("<modulo>")`. Ningún `DbContext` mapea tablas de otro schema; para leer datos ajenos se usa el contrato del otro módulo.
3. Las dependencias blandas se resuelven pidiendo el contrato al contenedor y comprobando si existe. Si no está, el módulo degrada su comportamiento sin fallar.
4. Los eventos de dominio se publican al bus interno. M10 Reportes se alimenta de ahí, nunca de las tablas ajenas.
5. Todo endpoint documentado en Swagger con XML comments, como ya se había definido.

---

## 6. Frontend: React modular

```
frontend/src/
├── shared/          UI base, hooks, cliente HTTP, tipos comunes
├── layout/          header, footer, navegación, contenedores
├── modules/
│   ├── catalog/     { components, pages, services, types, routes.ts }
│   ├── cms/
│   ├── sales/
│   ├── crm/
│   ├── services/
│   └── b2b/
├── capabilities/    consume /api/capabilities y expone useCapability()
└── app/             composición de rutas y arranque
```

Reglas:

1. Un módulo del frontend **nunca importa** de otro módulo. Lo compartido vive en `shared/`.
2. Cada módulo exporta su `routes.ts`; la app compone solo las rutas de módulos activos.
3. El menú, las secciones de la home y los enlaces del footer se construyen desde las capacidades, no están escritos a mano.
4. Un módulo desactivado no debe dejar rutas muertas, enlaces rotos ni huecos visuales en la home.

---

## 7. Licenciamiento y activación

Tablas en `core`:

- **`modules`** — catálogo de módulos que el producto conoce, con su versión y sus dependencias.
- **`module_activations`** — qué está activo en esta instalación, desde cuándo, hasta cuándo y con qué límites.
- **`installation`** — identidad del negocio instalado, clave de instalación y datos de licencia.

Comportamiento:

- Al arrancar, el host valida el grafo: si un módulo activo tiene una dependencia dura inactiva, **no arranca** y reporta el problema con claridad.
- `GET /api/capabilities` es público y devuelve solo códigos y versiones de módulos activos. Nunca expone datos de licencia.
- La venta como producto instalable usa un archivo de licencia firmado que se valida al arrancar. La firma se implementa en la fase de comercialización, no ahora, pero el esquema de datos ya la contempla.

---

## 8. Qué cambia respecto al diseño anterior

| Aspecto | Antes | Ahora |
|---|---|---|
| Destinatario | Un negocio concreto | Un producto; ese negocio es la primera instalación |
| Base de datos | 1 schema `public`, 17 tablas, 1 script | 1 schema por módulo, scripts por módulo + integraciones |
| Diccionario BD-02 | Incompleto, 3 de 17 tablas | Uno por módulo, dentro de cada SPEC |
| Modelo ER BD-03 | 2 diagramas globales, desfasados | Uno por módulo + mapa de dependencias |
| Backend | Un proyecto con carpetas | Un proyecto por módulo con contratos |
| Frontend | Carpetas por tipo de archivo | Carpetas por módulo, rutas dinámicas |
| Servicios | Solo vitrina | Vitrina + órdenes + seguimiento |
| Configuración | `site_settings` | `site_settings` + licencia y activación |
| Roadmap | BD → Backend → Frontend | Fundación, luego ciclo completo por módulo |

**Nada del trabajo previo se descarta.** El PRD sigue vigente íntegro. Las 17 tablas sobreviven. Las decisiones de nomenclatura, snapshots, eliminación lógica, `timestamptz` e identidades se mantienen tal cual.

---

## 9. Alcance de la primera instalación

Módulos: **CORE + M01 + M02 + M04 + M03 + M05a + M07**.

Esto cubre el backlog de alta prioridad de la primera instalación: homepage con banners, categorías y productos destacados, catálogo con buscador y filtros, ficha de producto, carrito, promociones, servicios visibles, colegios y empresas, pedidos especiales, WhatsApp, contacto, mapa, footer y panel básico de administración.

Quedan fuera de la primera entrega, por decisión explícita: órdenes de servicio, seguimiento kanban, portal del cliente, inventario, reportes y pagos.

---

## 10. Riesgos asumidos

1. **Sobrecosto de la modularización.** Estimado entre 60% y 100% adicional en base de datos y backend frente a una versión a medida. Se asume porque la alternativa —modularizar después— cuesta varias veces más.
2. **Tentación de generalizar de más.** Regla de contención: no se construye una abstracción hasta que exista un segundo caso real que la exija. Nada de configurabilidad para clientes imaginarios.
3. **El cliente espera una web, no un producto.** La modularidad es un asunto interno; los plazos y entregables hacia el negocio se miden en funcionalidad visible, no en arquitectura.
4. **Confusión entre producto y cliente.** El riesgo permanente es que funcionalidad pedida por un cliente concreto se cuele en el núcleo como si fuera genérica. Regla de contención: todo lo específico de un cliente vive en el repositorio de esa instalación (ADR-008), nunca en el código de un módulo.

---

*Documento vivo. Se versiona junto al código y se actualiza al cerrar cada módulo.*
