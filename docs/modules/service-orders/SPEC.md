# SPEC — M05b Servicios — Órdenes

- **ID:** `M05b`
- **Código de módulo propuesto:** `service_orders`
- **Nombre:** M05b Servicios — Órdenes
- **Backend previsto:** `Sillar.Modules.ServiceOrders`
- **Proyecto de contratos previsto:** `Sillar.Modules.ServiceOrders.Contracts`
- **Schema:** `service_orders`
- **Versión propuesta:** 1.0.0
- **Estado:** Ratificada · Paso 1 cerrado
- **Fase:** Fase 2 — Operación de servicios
- **Fecha de creación:** 6 de octubre de 2026
- **Última verificación:** 7 de octubre de 2026
- **Zona horaria de negocio:** America/Lima
- **Base efectivamente leída:** `d26f28a0439a9ac72dbedcc097dd8731b37a27c9`
- **Siguiente paso:** Paso 2 Datos, exclusivamente después de autorización expresa de Chat 2

> Esta SPEC cierra el Paso 1, pero no autoriza todavía migraciones, código productivo, endpoints ni
> interfaz. La clasificación de replicación y la costura de numeración deben auditarse antes del
> primer `CREATE TABLE`.

## 0. Decisiones y evidencia de partida

### 0.1 Decisiones que no se reabren

1. M05b existe como módulo propio, usa `service_orders` y no comparte schema con M05a.
2. M05b depende **duro** de M05a Servicios — Vitrina.
3. M04 Clientes puede enriquecer M05b como dependencia **blanda**; no se vuelve dura sin decisión de arquitectura.
4. M05b consume exclusivamente `IServiceShowcaseSnapshots.GetPublishedSnapshotAsync` para fotografiar el servicio.
5. La fotografía se copia a tablas propias de M05b. Es congelada, no una referencia viva.
6. No existe FK desde `service_orders` hacia `services`.
7. Toda atribución de personal usa exclusivamente `ICurrentAdmin` y congela `DisplayName`, `AdminUserId` y `HomeNode`.
8. No existe FK hacia `core.admin_users`, ni lectura de esa tabla, cookie, token o `DbContext` de CORE.
9. ADR-016 y ADR-018 se aplican antes de la primera migración.
10. M06 depende duro de M05b, pero M05b no implementa M06.

### 0.2 Evidencia leída

- `ServiceSnapshot` expone `ServiceId`, `Name`, `Slug`, `ShortDescription`, `Description`, `Price`,
  `SaleUnit`, `MediaAssetId`, `ImageUrl` e `ImageAltText`.
- M05a no replica y su identidad es un entero local. Por eso ese entero no puede interpretarse fuera
  del nodo sin conservar además el nodo donde se tomó la fotografía.
- `ICurrentAdmin.AdminUserId` también es entero local; su contrato documenta expresamente que solo
  tiene sentido junto a `HomeNode`.
- `ProductItemConfiguration` y otras implementaciones existentes no son autoridad para el modelo de
  M05b. Las ADR y esta SPEC prevalecen.

### 0.3 Decisiones D1–D9 ratificadas el 6 de octubre de 2026

- una orden contiene **1..N líneas**;
- M05b es dueño del estado actual, su máquina y su historial autoritativo;
- los estados v1 son `received`, `in_progress`, `ready`, `completed` y `cancelled`;
- M06 lee estado e historial mediante Contracts y provoca cambios llamando la operación de M05b;
- los eventos son avisos opcionales, nunca el camino autoritativo;
- el código visible usa serie anual propia y el mecanismo transaccional reusable ya demostrado por
  M03/M07, con rollback sin consumo y nunca `nextval()` como garantía;
- la cantidad común es `numeric(12,3)` y admite fracciones;
- `editor+` opera; cancelar exige `admin+`;
- M05b conserva `MediaAssetId`, URL y alt, pero no duplica el binario;
- v1 solo permite “asignarme” y “liberar”;
- `promised_at` es opcional y, si existe, no precede a `received_at`.

La trazabilidad completa, dueño y efecto de D1–D9 está en §11. Todas las respuestas de producto
están cerradas para abrir el Paso 2; permanecen las comprobaciones y costuras técnicas ya decididas.

## 1. Propósito y valor comercial

M05b registra órdenes reales de servicios: quién encargó qué, con qué características, cuándo se
recibió, qué precio se acordó y qué personal la recibió o tiene asignada.

M05a responde “qué servicios ofrece el negocio”. M05b responde “qué trabajo concreto aceptó el
negocio”. No vuelve a editar la vitrina ni usa sus textos como referencia viva.

Un negocio paga por M05b cuando necesita dejar de administrar encargos mediante papeles, mensajes o
memoria: cada orden obtiene un código humano, detalle congelado, contacto, asignación y estado actual.
M05b conserva sentido histórico aunque cambien nombres, precios, descripciones o publicación en M05a.

Sin M05b todavía puede mostrarse la vitrina M05a, pero no se pueden recibir ni administrar órdenes de
servicio dentro de SILLAR.

## 2. Alcance y fronteras

### 2.1 Dentro de alcance

- alta administrativa de una orden;
- selección de **uno o más** servicios publicados de M05a;
- fotografía inmutable de cada servicio al agregarlo a la orden;
- características concretas solicitadas por línea;
- cantidad y precio acordado, admitiendo precio todavía pendiente;
- contacto congelado, manual o enriquecido por M04 cuando esté disponible;
- código visible separado de la PK técnica;
- estado actual, máquina de estados e historial autoritativo de transiciones;
- responsable actual y registro inmutable de toma/liberación/reasignación a la cuenta actual;
- auditoría administrativa con nombres humanos;
- contrato de lectura de estado/historial y operación autoritativa de transición para el futuro M06;
- ciclo completo de instalación, activación, desactivación, desmontaje, reinstalación y reactivación.

### 2.2 Fuera de alcance

- vitrina y edición de servicios — M05a;
- tablero kanban, notas y seguimiento operativo adicional — M06; el historial autoritativo de estados pertenece a M05b;
- portal e historial visible al cliente — M08;
- cobro, caja, turnos o comprobantes — M13/M14;
- pagos en línea — M11;
- bloqueo o movimiento de inventario — M09;
- agenda o reserva de citas;
- archivos de producción, pruebas de arte o adjuntos del trabajo, hasta que producto los solicite;
- sincronización física entre nodos — M16; M05b solo nace con datos aptos para ella;
- notificaciones por correo, WhatsApp o SMS;
- implementación del plan de paralelización posterior a M08.

## 3. Dependencias y ciclo entre módulos

| Módulo | Tipo | Qué consume M05b | Ausencia o desactivación |
|---|---|---|---|
| CORE | Plataforma | `ICurrentAdmin`, autenticación, CSRF, auditoría, capacidades, reloj/nodo y medios indirectamente | CORE siempre está |
| M05a Servicios | **Dura** | `IServiceShowcaseSnapshots` para obtener una fotografía publicada | M05b no se activa ni crea líneas nuevas |
| M04 Clientes | **Blanda** | Contrato público de identidad/snapshot | Alta manual de contacto; ninguna ruta falla |
| M06 Seguimiento | No es dependencia de M05b | M06 consumirá estado, historial y transición mediante Contracts | M05b opera con su estado e historial, sin tablero M06 |
| M08 Portal | Ninguna | Futuro consumidor indirecto por M06/M04 | Sin efecto |

### 3.1 Qué significa que M05a sea dependencia dura

- Instalar M05b exige que el binario y contrato compatible de M05a estén desplegados.
- Activar M05b exige M05a activo y el schema propio de M05b ya migrado.
- La activación no ejecuta migraciones.
- Con M05b activo no puede desactivarse M05a dejando un grafo incoherente.
- El desmontaje de M05a se bloquea mientras M05b siga instalado como dependiente duro.
- El orden soportado es instalar/activar M05a antes de M05b y desactivar/desmontar M05b antes de M05a.
- La dependencia modular **no es una FK de datos**. Las líneas conservan snapshots, nunca referencias vivas.
- Si una restauración defectuosa o manipulación externa deja M05a ausente, las filas históricas de
  M05b siguen siendo legibles; M05b no puede operar ni reactivarse hasta reparar el grafo.

### 3.2 M04 como dependencia blanda

M05b debe aceptar un contacto escrito por el personal sin M04. Si M04 está activo, podrá ofrecerse
un selector y copiar nombre, correo y teléfono desde un contrato público; la orden conserva su propia
fotografía. `customer_id`, si se conserva como procedencia, es nulable y **no lleva FK**: M05b debe
seguir leyendo la orden si M04 se desactiva o elimina al cliente.

No se leen tablas `crm`, no se importa dominio de M04 y no se convierte la cuenta de cliente en
requisito para recibir una orden en mostrador.

### 3.3 M06 como dependiente

M05b es dueño del estado actual, de la máquina y del historial autoritativo. M06 expondrá tablero y
seguimiento, pero no define estados, no escribe el historial de M05b, no actualiza
`service_orders.status` y no lee el schema. Consume exclusivamente Contracts.

Si una acción futura del tablero mueve una tarjeta, M06 llama la operación de transición de M05b.
M05b valida, cambia estado, inserta historial y confirma ambas escrituras en una única transacción
PostgreSQL; M06 lee después el resultado. Dos módulos no mantienen copias autoritativas del mismo hecho.

## 4. Replicación antes de la primera migración

Pregunta aplicada a cada tabla: **¿esta fila puede nacer en un nodo y tener que existir en otro?**

| Tabla conceptual | ¿Replicable? | Motivo | PK | `origin_node` | Versión | FKs y clasificación |
|---|---|---|---|---|---|---|
| `service_orders` | **Sí** | Una orden puede recibirse en un nodo y consultarse/operarse en otro | UUID v7 generado por aplicación | Sí | `row_version` | Ninguna FK a tablas locales/ajenas |
| `service_order_items` | **Sí** | El detalle debe viajar con la orden | UUID v7 generado por aplicación | Sí | `row_version` | `order_id` UUID → orden replicada; válido |
| `service_order_assignment_events` | **Sí** | La asignación debe entenderse en cualquier nodo | UUID v7 generado por aplicación | Sí | `row_version` | `order_id` UUID → orden replicada; sin FK a admin local |
| `service_order_status_history` | **Sí** | La historia autoritativa debe viajar con la orden replicada | UUID v7 generado por aplicación | Sí | `row_version` | `order_id` UUID → orden replicada; sin FK a admin local |
| `service_order_series` | **No** | El contador pertenece a una serie local; replicarlo produciría dos autoridades | integer identity | No | No | Ninguna tabla replicada lo referencia |

### 4.1 Consecuencias ADR-016/018

- Los UUID v7 los genera la aplicación, nunca PostgreSQL.
- `origin_node` dice dónde nació la fila; no identifica al trabajador ni al servicio fotografiado.
- `row_version` permite ordenar cambios futuros; su mecanismo físico se define en Datos.
- Todas las FK entre las cuatro tablas replicadas son UUID.
- Ninguna tabla replicada referencia `core.admin_users`, M05a ni la serie local.
- El código visible se copia como texto en la orden y no referencia al contador.
- El entero local de M05a se conserva junto a `service_source_node`, sin FK.
- El entero local de personal se conserva junto a `..._admin_user_home_node`, sin FK.

### 4.2 Barrido de referencias

| Origen | Destino | Origen | Destino | Resultado |
|---|---|---:|---:|---|
| item.order_id | service_orders | replicada | replicada | Permitida |
| assignment.order_id | service_orders | replicada | replicada | Permitida |
| status_history.order_id | service_orders | replicada | replicada | Permitida |
| item.service_source_id | M05a services | replicada | no replicada | **Sin FK; snapshot + nodo** |
| order.customer_id | M04 customer | replicada | replicada, pero dependencia blanda | **Sin FK; snapshot propio** |
| atribuciones | core.admin_users | replicada | no replicada | **Sin FK; triple congelada** |
| order.visible_code | service_order_series | replicada | no replicada | **Sin FK; valor copiado** |
| item.media_asset_id | core.media_assets | replicada | replicada | Sin FK por semántica histórica; es parte del snapshot |

No hay tabla ambigua en su clasificación. La auditoría previa a la migración debe comprobar que las
cinco tablas conservan esta decisión, en especial que historial y orden sean replicables y que la
serie local no sea destino de una FK replicada.

## 5. Modelo conceptual de datos

Los tipos son propuestas conceptuales; el diccionario físico definitivo pertenece al Paso 2.

### 5.1 `service_orders.service_orders` — orden replicada

| Campo conceptual | Tipo propuesto | Nulo | Significado e invariantes |
|---|---|---:|---|
| `service_order_id` | uuid v7 | no | PK técnica; jamás visible |
| `visible_code` | text | no | Código humano único de serie anual propia; separado de la PK |
| `status` | text | no | `received`, `in_progress`, `ready`, `completed` o `cancelled` |
| `customer_id` | uuid | sí | Procedencia opcional de M04, sin FK |
| `customer_name_snapshot` | text | no | Nombre de quien encarga, congelado |
| `customer_phone_snapshot` | text | sí | Teléfono congelado |
| `customer_email_snapshot` | text | sí | Correo congelado |
| `received_notes` | text | sí | Contexto general no repetido en una línea |
| `received_at` | timestamptz | no | Momento en que el negocio recibe la orden |
| `promised_at` | timestamptz | sí | Compromiso opcional; si existe, no precede a `received_at` |
| `created_by_admin_name` | text | no | `ICurrentAdmin.DisplayName` congelado |
| `created_by_admin_user_id` | integer | no | ID local de la cuenta |
| `created_by_admin_user_home_node` | text | no | Nodo contra el que se interpreta el ID |
| `current_assignee_name` | text | sí | Responsable actual congelado |
| `current_assignee_admin_user_id` | integer | sí | ID local del responsable |
| `current_assignee_admin_user_home_node` | text | sí | Nodo de pertenencia del responsable |
| `last_status_changed_at` | timestamptz | no | Último cambio del estado actual; al crear coincide con recepción |
| `last_status_changed_by_name` | text | sí | Autor del último cambio; nulo si fue transición de sistema |
| `last_status_changed_by_admin_user_id` | integer | sí | ID local del autor; nulo si fue de sistema |
| `last_status_changed_by_admin_user_home_node` | text | sí | Nodo de la cuenta; nulo si fue de sistema |
| `origin_node` | text | no | Nodo donde nació la fila |
| `row_version` | bigint | no | Marca para sincronización futura |
| `created_at` | timestamptz | no | Creación técnica |
| `updated_at` | timestamptz | no | Última modificación técnica |

**Invariantes:**

- al menos teléfono o correo debe estar presente;
- las tres columnas del responsable son todas nulas o todas no nulas;
- cada triple de autor es completa o enteramente nula; no admite correo como sustituto del nombre;
- `promised_at`, si existe, no puede ser anterior a `received_at`;
- el estado inicial es `received`; `completed` y `cancelled` son terminales;
- no existe borrado físico operativo; cancelar es una transición exclusiva de `admin+`, no `DELETE`;
- el total se deriva de líneas: si alguna no tiene precio acordado, el total es “pendiente”, no cero.

### 5.2 `service_orders.service_order_items` — línea y snapshot congelado

Cada línea representa un servicio concreto encargado con sus características. Una orden contiene
**1..N líneas**; una recepción puede agrupar varios servicios.

| Campo conceptual | Tipo propuesto | Nulo | Significado e invariantes |
|---|---|---:|---|
| `service_order_item_id` | uuid v7 | no | PK técnica |
| `service_order_id` | uuid | no | FK interna a orden, ambas replicadas |
| `service_source_id` | integer | no | `ServiceSnapshot.ServiceId`; trazabilidad, no FK |
| `service_source_node` | text | no | Nodo de M05a donde se tomó la fotografía |
| `service_name_snapshot` | text | no | `Name` congelado |
| `service_slug_snapshot` | text | no | `Slug` congelado; no se usa para reconsultar |
| `service_short_description_snapshot` | text | sí | `ShortDescription` congelada |
| `service_description_snapshot` | text | sí | `Description` congelada |
| `showcase_price_snapshot` | numeric(12,2) | sí | `Price` de M05a; nulo sigue significando “a consultar” |
| `sale_unit_snapshot` | text | sí | `SaleUnit` congelada |
| `media_asset_id_snapshot` | uuid | sí | `MediaAssetId` congelado, sin FK |
| `image_url_snapshot` | text | sí | `ImageUrl` congelada; no promete binario eterno |
| `image_alt_text_snapshot` | text | sí | `ImageAltText` congelado |
| `requested_details` | text | no | Características del encargo concreto; no vacío |
| `quantity` | numeric(12,3) | no | Positiva; admite fracciones; un servicio podrá exigir enteros con una regla más restrictiva |
| `agreed_unit_price` | numeric(12,2) | sí | Precio acordado para esta orden; nulo = pendiente, cero = gratuito |
| `sort_order` | integer | no | Orden estable dentro de la orden; `>= 0` |
| `origin_node` | text | no | Nodo donde nació la línea y se tomó la fotografía |
| `row_version` | bigint | no | Marca futura de sincronización |
| `created_at` | timestamptz | no | Momento de incorporación |
| `updated_at` | timestamptz | no | Última modificación permitida |

**Todos** los campos procedentes de `ServiceSnapshot` quedan históricos desde que se agrega la línea.
Editar M05a después no cambia ninguno. El precio de vitrina y el acordado son distintos: el primero
explica qué se publicó; el segundo qué aceptaron las partes.

Una línea solo se crea si M05a devuelve un snapshot publicado. Si después el servicio se despublica
o desaparece, la línea permanece y nunca se “refresca”.

### 5.3 `service_orders.service_order_assignment_events` — atribución replicada

Registro inmutable de tomar, reasignar a la cuenta actual o dejar sin responsable. La orden conserva además la
proyección del responsable actual para lectura eficiente; ambos se escriben en una sola transacción.

| Campo conceptual | Tipo propuesto | Nulo | Significado |
|---|---|---:|---|
| `assignment_event_id` | uuid v7 | no | PK técnica |
| `service_order_id` | uuid | no | FK interna a orden replicada |
| `action` | text | no | `assigned` o `unassigned`, con `CHECK` |
| `assignee_name` | text | no | Persona afectada, congelada |
| `assignee_admin_user_id` | integer | no | ID local de esa cuenta |
| `assignee_admin_user_home_node` | text | no | Nodo de pertenencia de esa cuenta |
| `performed_by_name` | text | no | Actor de la operación, desde `ICurrentAdmin` |
| `performed_by_admin_user_id` | integer | no | ID local del actor |
| `performed_by_admin_user_home_node` | text | no | Nodo de pertenencia del actor |
| `occurred_at` | timestamptz | no | Momento del acto |
| `origin_node` | text | no | Nodo donde ocurrió el acto; puede diferir de ambos `HomeNode` |
| `row_version` | bigint | no | Marca futura de sincronización |

En `unassigned` se conserva la triple de quien dejó de estar asignado. No se crea una “persona
vacía”. No hay FK, JOIN ni resolución posterior contra CORE.

### 5.4 `service_orders.service_order_status_history` — historial autoritativo replicado

Cada orden recibe al crearse una fila inicial `NULL → received`. Cada transición posterior inserta
exactamente una fila. La actualización de `service_orders.status` y la inserción del historial ocurren
en la misma transacción PostgreSQL: se confirman ambas o ninguna.

| Campo conceptual | Tipo propuesto | Nulo | Significado |
|---|---|---:|---|
| `status_history_id` | uuid v7 | no | PK técnica generada por aplicación |
| `service_order_id` | uuid | no | FK interna a orden replicada |
| `from_status` | text | sí | Nulo solo en la fila inicial de creación |
| `to_status` | text | no | Estado destino válido |
| `occurred_at` | timestamptz | no | Momento de la transición |
| `performed_by_name` | text | sí | Nombre congelado; nulo en transición de sistema |
| `performed_by_admin_user_id` | integer | sí | ID local; nulo en transición de sistema |
| `performed_by_admin_user_home_node` | text | sí | Nodo de la cuenta; nulo en transición de sistema |
| `origin_node` | text | no | Nodo donde ocurrió/nació la fila; no sustituye `HomeNode` |
| `row_version` | bigint | no | Marca futura de sincronización |

La triple de actor es completa o enteramente nula. Una transición humana toma las tres piezas
exclusivamente de `ICurrentAdmin`. Una transición de sistema guarda tres `NULL`; nunca crea una
persona ficticia llamada `"Sistema"`. No hay FK a `core.admin_users`.

### 5.5 `service_orders.service_order_series` — contador local

M05b conserva su propia tabla y sus propios datos de serie. La referencia reusable es el mecanismo
probado de M03 —`OrderSeries`, contador local `(node_code, year)`, índice único, reserva dentro de la
transacción y alta concurrente—; M07 confirma el mismo principio. M05b no depende de Sales, no escribe
en `sales` y no copia otra variante del algoritmo.

| Campo conceptual | Tipo propuesto | Nulo | Significado |
|---|---|---:|---|
| `service_order_series_id` | integer identity | no | PK local |
| `node_code` | text | no | Nodo dueño de la serie |
| `year` | integer | no | Año en America/Lima |
| `last_number` | integer | no | Último correlativo confirmado, `>= 0` |

`UNIQUE(node_code, year)`. El código sigue el patrón `<LETRA_PROPIA>-AAAA-NNNN`; la letra/etiqueta
pertenece a M05b y se configura para la instalación, no se toma de Sales. La serie reinicia por año
de America/Lima y reserva el número dentro de la misma transacción que crea la orden. El mecanismo común debe conservar
concurrencia, `UPDATE ... RETURNING` y rollback sin hueco; nunca usa `nextval()` como garantía.

Antes de la migración, Paso 2 presentará la extracción/reutilización mínima de ese mecanismo desde
su ubicación actual en M03/Sales, sin cambiar la semántica certificada de M03 y sin importar su dominio.

### 5.6 Relaciones

```text
service_orders 1 ─── 1..N service_order_items
service_orders 1 ─── 0..N service_order_assignment_events
service_orders 1 ─── 1..N service_order_status_history
service_order_series ─── sin FK hacia órdenes
```

No hay FK hacia `services`, `crm`, `core.admin_users` ni `core.media_assets`.

### 5.7 Datos semilla

M05b no necesita órdenes, estados de negocio ni personal de demostración para funcionar. La
clasificación propuesta es **sin datos semilla de dominio**. Su `02_seed.sql` será transaccional,
idempotente y vacío, o el arnés declarará explícitamente esa ausencia; en ambos casos M05b debe figurar
en la paridad E2E para que “olvidado” no se confunda con “vacío”.

## 6. Contratos, eventos y API propuesta

### 6.1 Consumo de M05a — contrato decidido

Al agregar una línea, dentro de la operación autoritativa:

1. se llama una vez a `GetPublishedSnapshotAsync(serviceId, ct)`;
2. `null` rechaza la línea como servicio inexistente o no publicado;
3. se validan características, cantidad y precio acordado;
4. se copian todos los campos del snapshot y `service_source_node` desde el código del
   `NodeIdentity` de la instalación que atendió a M05a; nunca desde `ICurrentAdmin.HomeNode`;
5. se persisten orden y líneas en una transacción;
6. nunca se reconsulta M05a para renderizar la historia.

La fotografía se toma al confirmar el alta de la línea, no al abrir el formulario ni al listar la
vitrina. Reintentar una petición idempotente no debe crear dos líneas ni tomar dos fotografías.

### 6.2 Contrato público de M05b para M06 — frontera ratificada

```csharp
namespace Sillar.Modules.ServiceOrders.Contracts;

public interface IServiceOrderTrackingSource
{
    Task<ServiceOrderTrackingSnapshot?> GetAsync(
        Guid serviceOrderId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ServiceOrderTrackingSummary>> ListOpenAsync(
        CancellationToken cancellationToken);
}

public interface IServiceOrderTransitions
{
    Task<ServiceOrderTransitionResult> TransitionAsync(
        Guid serviceOrderId,
        string toStatus,
        CancellationToken cancellationToken);
}

public sealed record ServiceOrderTrackingSummary(
    Guid ServiceOrderId,
    string VisibleCode,
    string CustomerName,
    string CurrentStatus,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? PromisedAt,
    StaffSnapshot? CurrentAssignee);

public sealed record ServiceOrderTrackingSnapshot(
    Guid ServiceOrderId,
    string VisibleCode,
    string CustomerName,
    string CurrentStatus,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? PromisedAt,
    IReadOnlyList<ServiceOrderWorkItemSnapshot> Items,
    StaffSnapshot? CurrentAssignee,
    IReadOnlyList<ServiceOrderStatusHistorySnapshot> StatusHistory);

public sealed record ServiceOrderWorkItemSnapshot(
    Guid ServiceOrderItemId,
    string ServiceName,
    string? SaleUnit,
    decimal Quantity,
    string RequestedDetails);

public sealed record StaffSnapshot(
    string DisplayName,
    int AdminUserId,
    string HomeNode);

public sealed record ServiceOrderStatusHistorySnapshot(
    Guid StatusHistoryId,
    string? FromStatus,
    string ToStatus,
    DateTimeOffset OccurredAt,
    StaffSnapshot? PerformedBy,
    string OriginNode);

public sealed record ServiceOrderTransitionResult(
    Guid ServiceOrderId,
    string CurrentStatus,
    ServiceOrderStatusHistorySnapshot HistoryEntry);
```

Este contrato no expone entidades EF ni permite lectura directa del schema. M06 no define estados,
no escribe historial y no actualiza `service_orders.status`. Si su tablero provoca una transición,
llama `IServiceOrderTransitions`; la implementación autoritativa de M05b valida la máquina, cambia el
estado, inserta el historial y confirma una única transacción. M06 lee el resultado después.

### 6.3 Eventos como avisos opcionales

**El bus nunca es el camino por el que viaja un hecho que alguien necesita; es el camino por el que
viaja un aviso.**

La verdad durable es el estado y el historial de M05b. M06 los lee por contrato y jamás depende de
que un evento llegue. Como avisos opcionales se permiten:

- `ServiceOrderCreated`: ID, código, estado inicial, recepción y resumen congelado.
- `ServiceOrderAssignmentChanged`: ID de orden, responsable anterior/nuevo como triples, actor y fecha.
- `ServiceOrderStatusChanged`: ID, estado anterior/nuevo, triple opcional del actor y fecha.

Estos avisos pueden refrescar una UI, disparar una notificación o alimentar una integración futura
que pueda releer la verdad. Tampoco se excluye un futuro M10 si su contrato permite reconstrucción.
Ningún consumidor que necesite garantía depende exclusivamente del bus. No se publica “servicio
actualizado”: las líneas son históricas y no se refrescan; tampoco se inventa una cola general.

### 6.4 Endpoints administrativos propuestos

Todos requieren sesión administrativa y CSRF en escritura. Leer, crear, editar mientras el estado
lo permita, tomar/liberar y transiciones ordinarias exigen `editor+`; cancelar exige `admin+`.

| Método | Ruta | Responsabilidad | Respuestas relevantes |
|---|---|---|---|
| GET | `/api/admin/service-orders` | Listar con filtros por estado, responsable, texto y fechas | 200, 400, 403 |
| POST | `/api/admin/service-orders` | Crear orden con 1..N líneas, snapshot M05a, estado `received` e historial inicial | 201, 400, 404/409, 403 |
| GET | `/api/admin/service-orders/{id}` | Detalle histórico completo | 200, 404, 403 |
| PUT | `/api/admin/service-orders/{id}` | Editar contacto, compromiso y líneas mientras la regla de estado lo permita | 200, 400, 409, 403 |
| POST | `/api/admin/service-orders/{id}/take` | Asignar/reasignar a la cuenta actual y registrar evento | 200, 400, 409, 403 |
| POST | `/api/admin/service-orders/{id}/unassign` | Retirar responsable actual conservando su fotografía previa | 200, 409, 403 |
| POST | `/api/admin/service-orders/{id}/transition` | Ejecutar la operación autoritativa estado+historial; cancelar solo `admin+` | 200, 400, 409, 403 |

No hay endpoints públicos ni de cliente en 1.0. M08 no se anticipa.

## 7. Reglas, atribución, auditoría y errores

### 7.1 Reglas de negocio

1. Una orden tiene entre una y muchas líneas; no existe “una orden = un servicio”.
2. Solo un servicio publicado de M05a puede originar una línea nueva.
3. La línea nunca se refresca desde M05a.
4. `showcase_price_snapshot`, `agreed_unit_price` y cero/nulo conservan significados distintos.
5. Toda orden tiene un contacto legible aunque M04 esté ausente.
6. La PK técnica nunca se muestra; se usa `visible_code`.
7. El estado inicial es `received`. Solo se permiten `received → in_progress`,
   `received → cancelled`, `in_progress → ready`, `in_progress → cancelled`,
   `ready → completed`, `ready → in_progress` y `ready → cancelled`.
8. `completed` y `cancelled` son terminales; `ready → in_progress` es la reapertura operativa explícita.
9. La guarda vive dentro de la operación autoritativa M05b, no en endpoint, UI ni M06.
10. Cada transición válida cambia estado e inserta exactamente un historial en la misma transacción.
11. Una orden cancelada no se borra y sus snapshots permanecen; cancelar exige `admin+`.
12. Toda atribución guardada lleva las tres piezas o ninguna cuando el actor de estado sea sistema.
13. `HomeNode` del personal no se deriva de `origin_node`: pertenencia de cuenta y lugar de actuación son hechos distintos.
14. No se consulta CORE para “actualizar” nombres históricos.
15. Tomar/reasignar a la cuenta actual crea un evento y actualiza la proyección actual en la misma transacción.
16. La versión base no permite elegir a otra cuenta: toda nueva triple de asignado procede del mismo
    `ICurrentAdmin` que autoriza el acto. Liberar conserva en el evento la triple histórica ya almacenada.
17. El código visible reutiliza el mecanismo común M03/M07; la orden y el número se confirman juntos.
18. `MediaAssetId`, URL y alt son snapshot; M05b no copia el binario y sigue legible si este desaparece.
19. `promised_at` es opcional y, cuando existe, cumple `promised_at >= received_at`.

### 7.2 Lugares de atribución triple

| Acto/dato | Triple congelada | Fuente |
|---|---|---|
| Crear/recibir orden | `created_by_*` | `ICurrentAdmin` actual |
| Estado inicial y transición humana | `last_status_changed_by_*` y `status_history.performed_by_*` | `ICurrentAdmin` actual |
| Transición de sistema | las mismas columnas | Tres `NULL`; nunca `"Sistema"` ficticio |
| Tomar/reasignar a la cuenta actual — actor | `performed_by_*` | `ICurrentAdmin` actual |
| Tomar/reasignar a la cuenta actual — persona asignada | `assignee_*` | La misma triple de `ICurrentAdmin` actual |
| Liberar — actor | `performed_by_*` | `ICurrentAdmin` actual |
| Liberar — persona que deja la asignación | `assignee_*` | Copia de la triple histórica del responsable actual, originalmente obtenida de `ICurrentAdmin` |
| Responsable actual | `current_assignee_*` | Copia del último evento válido |

El contrato actual solo identifica al **actor actual**. Por ello v1 ofrece “tomar para mí” y
“liberar”. Seleccionar a otra cuenta está fuera de v1; no se propone otro contrato, no se elude
leyendo `admin_users` y no se inventa un nombre desde correo, cookie o token.

### 7.3 Auditoría

Las operaciones administrativas persistentes escriben auditoría de CORE mediante su contrato. El
resumen nombra la fila humana:

- “Orden de servicio OS-… creada para ‹nombre›.”
- “Orden de servicio OS-… asignada a ‹nombre visible›.”
- “Orden de servicio OS-… pasó de ‹estado› a ‹estado›.”

Nunca “Orden actualizada” sin código. La auditoría local no sustituye las triples replicadas.

### 7.4 Errores observables

| Situación | Comportamiento |
|---|---|
| Servicio inexistente/despublicado al confirmar | Rechazo contextual en la línea; no se crea fotografía parcial |
| Servicio cambia después | Sin conflicto: la fotografía anterior permanece |
| Línea sin características | 400 junto al campo |
| Cantidad/precio inválido | 400; precio nulo permitido, negativo rechazado |
| Contacto insuficiente | 400 contextual |
| Código visible concurrente | El mecanismo común transaccional serializa la misma serie; nunca duplica ni deja hueco por rollback |
| Transición no permitida | 409 con estado origen/destino |
| Editor intenta cancelar | 403; no se modifica estado ni historial |
| Falla la inserción de historial | Rollback completo; el estado anterior permanece |
| Asignación cambió mientras se editaba | 409; no sobrescribe silenciosamente |
| Medio físico no disponible | Fallback textual con nombre, descripciones y alt congelados; la orden sigue legible |
| M04 ausente | Selector no aparece; alta manual sigue disponible |
| M05a inactivo | M05b no debe estar activo; inconsistencia diagnosticada, no 500 genérico |
| Sin permiso | 403 y pantalla de permiso denegado |
| Red/servidor | Error recuperable; datos del formulario se conservan |
| Error interno | Sin SQL, stack trace, schema ni UUID mostrado a la persona |

## 8. Barreras, comprobación independiente, ciclo y paridad E2E

### 8.1 Reglas que podrían ser ciertas solo porque hoy hay uno

- Un entero de personal o servicio no identifica globalmente sin su nodo.
- El nodo donde ocurre una asignación puede diferir del nodo al que pertenece la cuenta.
- Una orden puede contener varias líneas y cada línea puede tener cantidad fraccionaria.
- M04 puede estar ausente; no se hace obligatorio porque hoy esté instalado.
- Un servicio puede tener precio nulo, cero o fijo.
- Una orden puede estar activa sin responsable.
- La existencia del schema no significa que el módulo esté activo.
- La palabra “Servicios” no identifica de forma fiable las superficies de M05b.

### 8.2 Barreras nuevas y lugar de aplicación

| Barrera | Protege | Lugar futuro |
|---|---|---|
| B1 Snapshot publicado | No crear línea sin snapshot M05a válido | Operación de alta de línea |
| B2 Snapshot congelado | No refrescar historia ni depender de FK viva | Servicio de dominio y mapping |
| B3 Atribución triple | No guardar ID/nombre/nodo incompletos | Operaciones de creación, estado y asignación |
| B4 Replicación coherente | No FK replicada → local | Modelo y migración |
| B5 Estado + historial atómicos | No transición fuera de máquina ni estado sin historial | Operación autoritativa de transición M05b |
| B6 Código visible | No duplicado, algoritmo divergente ni consumo por rollback | Primitiva reusable y transacción de alta |
| B7 Dependencia dura | No activar M05b sin M05a | Operación de activación/grafo |
| B8 Aislamiento | Desmontar M05b no toca `services`, `tracking`, `crm` ni CORE | `99_drop.sql` y orquestador |
| B9 Paridad E2E | Ningún módulo presente queda fuera de migrate/seed/setup | Detector del arnés |

### 8.3 Pruebas futuras, negativos y sabotaje

| Barrera | Legal verde | Ilegal rojo atribuible | Sabotaje del detector/prueba |
|---|---|---|---|
| B1 | Crear con servicio publicado | ID inexistente/despublicado | Sustituir lector por aceptación constante; prueba debe fallar |
| B2 | Cambiar M05a y releer snapshot intacto | Intentar refrescar línea histórica | Reintroducir consulta viva en detalle; comparación debe fallar |
| B3 | Triple humana completa o triple de sistema enteramente nula | ID sin HomeNode, o `"Sistema"` ficticio | Omitir una columna o escribir actor ficticio; prueba roja |
| B4 | FK UUID entre replicadas | Añadir FK a admin/serie/M05a | Sabotear analizador para ignorar M05b; autoprueba roja |
| B5 | Transición válida cambia estado y crea exactamente un historial; ambas confirman juntas | Transición ilegal o escritura productiva directa de `status` se rechaza/detecta | Romper la inserción de historial mientras cambia estado; la prueba debe quedar roja y observar rollback |
| B6 | Altas concurrentes conservan unicidad y rollback no consume | Duplicar el algoritmo o reservar fuera de transacción | Sustituir la primitiva por `nextval()`/`count+1`; prueba roja |
| B7 | Activar tras M05a | Activar sin M05a | Hacer que detector devuelva siempre true; prueba roja |
| B8 | Ciclo conserva datos centinela ajenos | Drop apunta a otro schema | Alterar destino en base desechable; prueba roja |
| B9 | Binario/setup/migrate/seed coherentes | Quitar M05b de migrate o mitad aplicable | Romper el detector para que acepte omisión; autoprueba roja |

Cada barrera debe verse dejar pasar y rechazar. La falsificación ocurre en entorno desechable, se
documenta y se restaura; no se ejecuta en Paso 1.

Para B5 no existe atajo productivo: endpoint, UI y M06 llaman la misma operación autoritativa. Una
escritura que intente cambiar `status` por fuera debe ser rechazada o detectada por la barrera. El
sabotaje obligatorio rompe deliberadamente la inserción del historial después de solicitar el cambio
y demuestra que la orden conserva el estado anterior porque la transacción completa retrocede.

### 8.4 Segunda vía independiente

| Afirmación | Primera vía | Segunda vía |
|---|---|---|
| Snapshot congelado | Prueba de servicio | Lectura SQL antes/después de editar M05a |
| Sin FK M05a/CORE admin | Modelo EF | Catálogos de PostgreSQL |
| UUID v7 y columnas réplica | Prueba de dominio | Inspección física/migración |
| INNER/relaciones internas correctas | Modelo | SQL generado y constraints reales |
| Código visible distinto de PK | API | Lectura de columnas/serialización |
| Estado e historial atómicos | Prueba de operación real | Lectura SQL de orden/historial tras éxito y fallo provocado |
| Actor de sistema nulo | Prueba de dominio | Constraints y lectura SQL de las tres columnas |
| Numeración común no duplicada | Pruebas de la primitiva reusable | Inspección de dependencias y sabotaje rollback/concurrencia |
| M04 degrada | Prueba sin registro M04 | Arranque/API con capacidad ausente |
| Aislamiento de desmontaje | Ciclo aplicación | Hash/conteo de schemas centinela |
| Navegación desaparece | Prueba UI | Inventario de rutas/capacidades |
| Paridad E2E | Detector | Ejecución real del setup y consulta de schemas |

### 8.5 Ciclo de vida

**Instalación:** el instalador migra M05b después de CORE y M05a. Instalar no activa. Solo crea
`service_orders` y su historial de migraciones.

**Activación:** comprueba schema y M05a activo; no ejecuta DDL. Compone endpoints, navegación y
pantallas en el siguiente arranque coherente.

**Desactivación:** retira rutas y navegación, conserva todas las órdenes/snapshots y no cambia estados.

**Desmontaje:** exige M05b inactivo y sin dependiente duro activo (M06). Elimina exclusivamente
`service_orders`; nunca usa `CASCADE` como sustituto de comprobar dependencias.

**Reinstalación:** recrea schema vacío. Según el contrato actual de persistencia, desmontar elimina
los datos propios; restaurarlos requiere backup explícito, no reconstrucción desde M05a.

**Reactivación:** exige nuevamente M05a activo y schema preparado; recupera superficies y datos si
solo hubo desactivación, o queda vacío si hubo desmontaje y reinstalación sin restauración.

### 8.6 Costura E2E desde la primera candidata

Para M05b, la paridad `binario ↔ setup ↔ migrate ↔ seed` significa:

- **binario:** `Sillar.Modules.ServiceOrders` y Contracts están compilados/copied y el módulo se descubre;
- **setup:** el registro conoce código, versión, schema y dependencia dura `services`;
- **migrate:** el arnés incluye `ServiceOrdersDbContext` en orden posterior a M05a;
- **seed:** el arnés incluye el módulo aunque su seed de dominio esté explícitamente vacío;
- **prueba:** setup real deja registro coherente, schema migrado y cero filas semilla de negocio.

Negativo obligatorio: si M05b aparece en binario/setup pero se omite de `migrate()` o de la mitad
aplicable de seed/registro, la barrera termina roja y nombra `service_orders`/M05b. No basta un fallo
genérico por conteo.

Regla triple: configuración legal verde; ilegal roja y atribuible; sabotaje deliberado del detector
rojo. La prueba `[M05B-CICLO]` se ejecutará dentro de la **etapa 6 de la puerta canónica**, no como
script aislado de borrar/recrear schema.

## 9. Pantallas para el Paso 3.5

No se diseñan componentes en esta etapa. Todas las pantallas usarán componentes/tokens SILLAR y
tendrán variantes clara/oscura, móvil/escritorio, foco visible y estados no expresados solo por color.

### 9.1 A1 — Bandeja de órdenes

- **Ruta:** `/admin/ordenes-servicio`.
- **Propósito:** localizar, priorizar y abrir órdenes.
- **Quién entra:** `editor` o superior.
- **Datos:** código visible, cliente, resumen de líneas, estado actual, responsable, recepción,
  compromiso y señal de precio pendiente.
- **Acciones:** crear, abrir, filtrar por estado/responsable/fechas, buscar por código o cliente.
- **Navegación:** grupo “Servicios”, después de Vitrina; desaparece con capacidad inactiva.
- **Cargando:** estructura de lista/tabla con anuncio accesible.
- **Vacío:** “Todavía no hay órdenes”, con acción Crear; no se confunde con filtros sin resultados.
- **Con datos:** móvil en tarjetas apiladas; escritorio en tabla adaptable.
- **Conflicto:** si una orden cambia al abrirla, se recarga el detalle; la bandeja no promete edición.
- **Error:** aviso recuperable con reintento sin borrar filtros.
- **Permiso denegado:** pantalla 403; no se muestra acción.

### 9.2 A2 — Nueva orden

- **Ruta:** `/admin/ordenes-servicio/nueva`.
- **Propósito:** recibir una orden y congelar servicios/contacto/autor.
- **Quién entra:** `editor` o superior.
- **Datos:** contacto manual; selector M04 opcional; **1..N líneas** con selector de servicios publicados,
  características, cantidad, precio de vitrina y precio acordado; recepción/compromiso; opción “asignarme”.
- **Acciones:** agregar/quitar/reordenar líneas, elegir cliente si M04 existe, asignarse, confirmar o cancelar formulario.
- **Navegación:** desde A1; éxito conduce a A3.
- **Cargando:** al abrir selector o confirmar, conserva los datos y nombra qué se espera.
- **Vacío:** formulario inicial con una línea vacía; no trae servicios demo y no permite confirmar sin línea válida.
- **Con datos:** resumen del código aún no asignado y diferencias claras entre precio publicado/acordado.
- **Conflicto:** servicio despublicado entre selección y confirmación señala esa línea; toma incompatible
  o código concurrente no duplica la orden; datos escritos se conservan.
- **Error:** red/servidor recuperable, con reintento idempotente.
- **Permiso denegado:** 403 sin formulario.
- **M04 ausente:** selector de cliente desaparece sin hueco; contacto manual permanece completo.

### 9.3 A3 — Detalle de orden

- **Ruta:** `/admin/ordenes-servicio/:id`.
- **Propósito:** leer la verdad histórica y operar sobre la orden actual.
- **Quién entra:** `editor` o superior.
- **Datos:** código, estado actual, contacto congelado, recepción/compromiso, creador triple presentada como
  nombre, responsable, líneas con snapshot y precio acordado, total/pendiente, historial autoritativo
  de estados e historial de asignación.
- **Acciones:** editar lo permitido, tomar/liberar asignación y ejecutar únicamente las transiciones
  habilitadas por la máquina; cancelar solo aparece para `admin+`; imprimir/copiar código.
- **Navegación:** regreso a A1 conservando filtros; acceso a A4/A5.
- **Cargando:** esqueleto por secciones.
- **Vacío:** no aplica; ID inexistente presenta ausencia amigable y regreso a bandeja.
- **Con datos:** snapshot no se mezcla visualmente con el estado vigente de M05a.
- **Conflicto:** transición rechazada por el estado vigente conserva la pantalla, explica el cambio y ofrece recargar.
- **Error:** reintento; no muestra UUID, SQL o schema.
- **Permiso denegado:** 403.

### 9.4 A4 — Edición de orden y líneas

- **Ruta:** desde A3, pantalla o drawer según Diseño.
- **Propósito:** corregir contacto, compromiso, características y precio cuando el estado lo permita.
- **Quién entra:** `editor+`; cancelar no pertenece a esta edición y exige acción separada de `admin+`.
- **Datos:** valores actuales, snapshot M05a en solo lectura y campos propios editables claramente separados.
- **Acciones:** guardar/cancelar edición; agregar una nueva línea toma un snapshot nuevo; una existente no se refresca.
- **Cargando:** bloqueo solo durante carga/guardado.
- **Vacío:** no aplica; si no hay líneas es inconsistencia y se bloquea guardar.
- **Con datos:** validación contextual.
- **Conflicto:** estado ya no editable; no sobrescribe silenciosamente.
- **Error:** conserva cambios locales y permite reintentar.
- **Permiso denegado:** 403.

### 9.5 A5 — Tomar/liberar y registro de asignaciones

- **Ruta:** acción desde A1/A3; drawer o diálogo según Diseño.
- **Propósito:** tomar la orden para la cuenta actual o liberar al responsable sin perder autoría.
- **Quién entra:** `editor+`.
- **Datos:** responsable actual triple presentada por nombre/nodo y eventos previos con actor/fecha.
- **Acciones:** “Asignarme esta orden”, “Liberar responsable” y cancelar.
- **Cargando:** confirmación con anuncio; no aparece un selector de cuentas.
- **Vacío:** “Sin responsable”; historial vacío no es error.
- **Con datos:** responsable actual destacado; historial cronológico.
- **Conflicto:** otra asignación ganó; muestra responsable vigente y exige decisión nueva.
- **Error:** conserva selección y permite reintentar.
- **Permiso denegado:** control ausente y acceso directo 403.

### 9.6 A6 — Confirmación de transición/cancelación

- **Ruta:** acción contextual desde A3.
- **Propósito:** evitar transiciones destructivas accidentales y explicar consecuencias.
- **Quién entra:** `editor+` para transiciones ordinarias; exclusivamente `admin+` si el destino es `cancelled`.
- **Datos:** código, estado origen/destino, responsable y efecto terminal o de reapertura.
- **Acciones:** confirmar o volver.
- **Cargando:** confirmación bloqueada una vez enviada.
- **Vacío:** no aplica.
- **Con datos:** muestra la transición exacta; `ready → in_progress` se presenta como reapertura explícita.
- **Conflicto:** estado cambió; no intenta encadenar una segunda transición automáticamente.
- **Error:** reintento seguro.
- **Permiso denegado:** 403.

### 9.7 Estados de composición

- M05b inactivo: sin grupo, enlaces, rutas ni huecos.
- M05b activo/M05a coherente: superficies anteriores disponibles.
- Grafo incoherente: el arranque/activación lo diagnostica; la UI no simula un modo degradado para una dependencia dura.
- M04 activo: selector opcional de cliente.
- M04 inactivo: alta manual, sin aviso alarmista ni espacio reservado.
- M06 ausente: detalle conserva estado e historial autoritativo M05b; solo faltan tablero y seguimiento adicional.

## 10. Criterios de aceptación futuros

Ninguno se declara cumplido en Paso 1.

### 10.1 Funcionales y snapshots

- [ ] Crear acepta entre una y muchas líneas y rechaza una orden sin líneas.
- [ ] Crear una orden congela todos los campos de `ServiceSnapshot` por línea.
- [ ] Cambiar/despublicar/eliminar la entrada M05a no altera una línea existente.
- [ ] Un servicio no publicado no puede crear una línea nueva.
- [ ] La línea conserva `service_source_id` junto a su nodo y sin FK.
- [ ] Precio de vitrina nulo permanece nulo; precio acordado cero permanece cero.
- [ ] Cantidad `numeric(12,3)` conserva fracciones; una regla de servicio puede exigir enteros sin cambiar el tipo común.
- [ ] `MediaAssetId`, URL y alt se congelan; no se duplica el binario y su ausencia no vuelve ilegible la orden.
- [ ] Contacto manual funciona sin M04.
- [ ] Con M04, seleccionar cliente copia snapshot y la orden sobrevive a su cambio/baja.
- [ ] Una orden tiene PK UUID v7 y código visible distinto.
- [ ] `promised_at` admite nulo y, si existe, no precede a `received_at`.
- [ ] Cancelar exige `admin+`, no ejecuta `DELETE` y no borra snapshots.

### 10.2 Atribución triple

- [ ] Creación, transición humana, actor de asignación y persona asignada guardan nombre, ID local y HomeNode obtenidos de `ICurrentAdmin`.
- [ ] Una triple parcial es rechazada por dominio y restricción física cuando aplique.
- [ ] Una transición de sistema guarda las tres columnas de actor en `NULL` y nunca `"Sistema"`.
- [ ] `origin_node` puede diferir de `HomeNode` y ambos conservan su significado.
- [ ] Renombrar/desactivar una cuenta no reescribe fotografías históricas.
- [ ] No hay FK, JOIN ni consulta directa a `core.admin_users`.
- [ ] La versión base solo permite asignarse a la cuenta actual; no ofrece selección de terceros ni lee CORE.

### 10.3 Estado, historial y contrato M06

- [ ] Toda orden nace `received` y con exactamente una fila inicial `NULL → received`.
- [ ] Solo se aceptan las siete transiciones de §7.1; `completed` y `cancelled` son terminales.
- [ ] `ready → in_progress` funciona como reapertura explícita.
- [ ] Una transición legal modifica estado e inserta exactamente una fila de historial; ambas confirman juntas.
- [ ] Una transición ilegal deja estado e historial intactos y devuelve conflicto atribuible.
- [ ] Ningún endpoint, UI, servicio productivo ni M06 escribe `status` saltándose la operación M05b.
- [ ] Sabotear la inserción del historial mientras se cambia estado deja la barrera roja y demuestra rollback total.
- [ ] M06 lee identidad, código, estado, fechas, líneas, responsable e historial mediante Contracts sin entidades EF ni schema directo.
- [ ] Una acción M06 que mueve tarjeta llama la transición de M05b; M06 no define estados ni mantiene otro historial autoritativo.
- [ ] Perder un aviso del bus no pierde el hecho: estado e historial siguen disponibles para relectura.

### 10.4 Replicación, datos y numeración

- [ ] Cada fila replicable recibe UUID v7 de aplicación, `origin_node` y `row_version`.
- [ ] La serie local usa integer identity y no es destino de FK replicada.
- [ ] Todas las FK replicada→replicada usan UUID.
- [ ] Catálogos PostgreSQL confirman ausencia de FK a M05a, M04, admin users y medios.
- [ ] Cero/nulo y timestamps sobreviven serialización y persistencia real.
- [ ] La clasificación de las cinco tablas —cuatro replicables y una local— queda auditada antes de la migración.
- [ ] Paso 2 presenta la extracción/reutilización mínima del numerador común antes de migrar M05b.
- [ ] M05b conserva tabla y datos propios sin depender de M03/Sales ni escribir en `sales`.
- [ ] Altas concurrentes en la misma serie son únicas; rollback no consume número y el año es America/Lima.
- [ ] Sabotear con `nextval()`, `count+1` o reserva fuera de transacción deja las pruebas rojas.

### 10.5 API, errores y UI

- [ ] Los endpoints están documentados en OpenAPI y exigen rol/CSRF correctos.
- [ ] `editor+` crea, edita cuando corresponde, toma/libera y ejecuta transiciones ordinarias.
- [ ] Un editor no puede cancelar; `admin+` sí puede hacerlo desde un estado permitido.
- [ ] Errores son contextuales y no filtran detalles técnicos.
- [ ] Las seis pantallas cubren carga, vacío, datos y conflicto; error/403 donde aplica.
- [ ] El detalle muestra el historial autoritativo M05b aunque M06 esté ausente.
- [ ] Móvil/escritorio y claro/oscuro tienen paridad funcional.
- [ ] Teclado, foco y regiones vivas permiten operar sin depender del color.
- [ ] M04 ausente no deja hueco ni rompe alta.
- [ ] M05b inactivo elimina navegación y rutas.

### 10.6 Barreras y falsificación

- [ ] B1–B9 tienen positivo, negativo, mensaje/efecto observable y segunda vía.
- [ ] Cada barrera fue provocada para dejar pasar y rechazar.
- [ ] Cada prueba se rompió deliberadamente y se observó roja antes de restaurarla.
- [ ] La paridad E2E legal queda verde, la omisión queda roja nombrando M05b y el sabotaje del detector queda rojo.

### 10.7 `[M05B-CICLO]` — cierre real dentro de etapa 6

- [ ] La prueba identificable `[M05B-CICLO]` corre dentro de etapa 6 de la puerta canónica.
- [ ] Instala M05a y M05b en orden, activa M05b y crea una orden centinela mediante aplicación/API.
- [ ] Desactiva M05b y demuestra ausencia total de rutas/enlaces sin perder datos.
- [ ] Reactiva y recupera superficies y orden centinela.
- [ ] Impide desmontar M05a mientras M05b lo requiere.
- [ ] Desactiva y desmonta M05b respetando M06; confirma que `services`, CORE, CRM y demás centinelas sobreviven.
- [ ] Reinstala M05b, reactiva y recupera superficies según contrato: vacío sin restauración o datos restaurados desde backup explícito.
- [ ] No acepta como sustituto una prueba aislada de `DROP/CREATE SCHEMA`.
- [ ] No deja rutas muertas, enlaces rotos, huecos visuales, fallos de arranque, dependencia circular ni destrucción ajena.

### 10.8 Paridad de instalación

- [ ] Binario, setup, migrate y seed/ausencia explícita enumeran M05b coherentemente.
- [ ] Quitar M05b de `migrate()` pone la barrera roja y lo nombra.
- [ ] Quitar su mitad aplicable de setup/seed pone la barrera roja y lo nombra.
- [ ] Sabotear el detector para aceptar una omisión también pone su autoprueba roja.

## 11. Decisiones D1–D9 cerradas

| ID | Decisión | Dueño | Fecha | Efecto |
|---|---|---|---|---|
| D1 | Una orden contiene 1..N líneas | JP Producto | 06/10/2026 | Modelo, alta, API, UI y pruebas admiten varios servicios |
| D2 | M05b posee `received`, `in_progress`, `ready`, `completed`, `cancelled` y las siete transiciones de §7.1 | JP + líder técnico | 06/10/2026 | Guarda dentro de la operación; terminales y reapertura explícita |
| D3 | M05b posee estado e historial; M06 lee y llama la transición M05b; el bus solo avisa | JP + líder técnico | 06/10/2026 | Nueva tabla replicable, transacción atómica y contrato sin lectura de schema |
| D4 | Código humano con serie anual propia, mecanismo común M03/M07 y rollback sin hueco | JP + líder técnico | 06/10/2026 | Paso 2 coordina extracción reusable; M05b conserva sus datos y no depende de Sales |
| D5 | Cantidad común `numeric(12,3)` con fracciones | JP Producto | 06/10/2026 | Un servicio puede restringir a enteros sin cambiar tipo físico |
| D6 | `editor+` opera; cancelar exige exclusivamente `admin+` | JP Producto | 06/10/2026 | Autorización de API/UI; no existe DELETE operativo |
| D7 | Snapshot conserva `MediaAssetId`, URL y alt; no copia binario | JP + líder técnico | 06/10/2026 | Ausencia física usa fallback textual; réplica/retención es CORE/M16 |
| D8 | Asignación a terceros fuera de v1 | JP Producto | 06/10/2026 | Solo asignarme/liberar; sin selector, lectura de cuentas ni ampliación de `ICurrentAdmin` |
| D9 | `promised_at` opcional y `>= received_at` cuando existe | JP Producto | 06/10/2026 | Una orden puede recibirse sin fecha comprometida |

D1–D9 están cerradas. Paso 1 queda cerrado. Antes de crear la primera migración, Paso 2
debe auditar la clasificación ADR-016/018 y presentar la extracción/reutilización mínima del mecanismo
de numeración, ambas decisiones ya tomadas y no nuevas consultas de producto.

## 12. Flujo y parada obligatoria

```text
Paso 1 SPEC
→ Paso 2 Datos
→ Paso 3 API
→ STOP 3.5
→ JP / Claude Design
→ Paso 4 UI
→ Paso 5 cierre
```

No se cruza 3.5 sin diseño entregado y validado. No se implementan M06, M08, M11, M18 ni M09/ERP
desde este frente.
