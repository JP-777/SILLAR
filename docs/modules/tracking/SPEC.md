# SPEC — M06 Seguimiento · Paso 1 documental

- **Creación:** 7 de octubre de 2026 · America/Lima
- **Última verificación:** 7 de octubre de 2026 · America/Lima
- **Commit base de este documento:** `d26f28a0439a9ac72dbedcc097dd8731b37a27c9` (`main` vigente)
- **Base de conocimiento autoritativa:** rama `m05b-service-orders-spec`, cierre del Paso 1 de M05b
  en `312cd0dcdab256ce5cdbf4b99011c62611fcccb2`
- **Contrato real contrastado:** `f494eae81aa89872ebe4860aad319e25fd9dca1c` —primer SHA remoto donde
  `Sillar.Modules.ServiceOrders.Contracts` existe compilable—, archivo
  `backend/Sillar.Modules.ServiceOrders.Contracts/ServiceOrderContracts.cs`. Paso 2 posterior de M05b
  en `d36ac6066ff7c1cda3a4a1b36ef26805c2632e55`

> **Qué es este documento y qué no es.** Es el Paso 1 **documental** de M06, escrito contra la
> frontera M05b/M06 **ya ratificada**. No autoriza código: no hay proyecto `Sillar.Modules.Tracking`,
> ni migraciones, ni schema, ni endpoints, ni frontend.
>
> **Contrastado el 7 de octubre de 2026 contra el contrato real.** Los siete huecos que esta SPEC
> registró como `CONTRATO_PENDIENTE_DE_MATERIALIZAR` **están los siete resueltos** en el código
> publicado, y las tres decisiones que quedaban abiertas fueron ratificadas. El §6.2 ya no describe
> carencias: describe la firma real, citada por archivo. **No queda ninguna marca
> `CONTRATO_PENDIENTE_DE_MATERIALIZAR` en este documento.**

---

## 0. Decisiones de partida y evidencia leída

### 0.1 Lo que no se reabre

De la frontera ratificada de M05b (`312cd0dc`, §3.3, §6.2 y D1–D9):

- **M05b es dueño del estado actual, de su máquina de estados y del historial autoritativo.**
- **M06 no define estados, no escribe el historial de M05b y no actualiza `service_orders.status`.**
- **M06 no lee el schema de M05b**: consume exclusivamente Contracts, nunca entidades EF ni tablas.
- Si una acción del tablero mueve una tarjeta, **M06 llama la operación de transición de M05b**; M05b
  valida, cambia estado e inserta historial **en una única transacción PostgreSQL**, y M06 relee
  después. Dos módulos no mantienen copias autoritativas del mismo hecho.
- Los estados v1 son `received`, `in_progress`, `ready`, `completed` y `cancelled` — **propiedad de
  M05b**, citados aquí como dato, no adoptados como constantes de M06 (ver §6.1–6.2).
- **El bus nunca es el camino por el que viaja un hecho que alguien necesita; es el camino por el que
  viaja un aviso.** M06 no reconstruye historia desde `InProcessEventBus`.
- M06 depende **duro** de M05b.

Y lo ratificado el 7 de octubre de 2026, que cierra lo que este documento dejaba abierto:

- **Los datos de seguimiento se replican**: `order_tracking` y `tracking_notes` con UUID v7 de
  aplicación, `origin_node` y `row_version`. La razón que lo decide no la había visto yo: **M08 Portal
  puede ejecutarse en un nodo web y tiene que poder ver el seguimiento de órdenes nacidas en
  mostrador.** Sin replicar, el portal no vería nada de lo que ocurre donde se trabaja.
- **Las notas son exclusivamente internas**, y de ahí la frase que gobierna el apartado:
  **replicación física no es exposición contractual.**
- **La fila de seguimiento es *lazy*:** no existe mientras nadie cambie datos propios de M06.
- **Un solo tablero en v1.** No se anticipan talleres ni sucursales.
- **La concurrencia multinodo de prioridad y plazo no se resuelve aquí**: está diferida a M16 y
  registrada en `docs/PENDIENTES.md` §30.

### 0.2 Evidencia leída, con su cita

| Qué | Dónde |
|---|---|
| Frontera M05b/M06 | `312cd0dc:docs/modules/service-orders/SPEC.md` §3.3 |
| Contrato público ratificado para M06 | `…` §6.2 |
| Bus como aviso opcional | `…` §6.3 |
| Clasificación de replicación de M05b y barrido de referencias | `…` §4, §4.1, §4.2 |
| Historial autoritativo y triple de actor | `…` §5.4 |
| Lo que M05b deja expresamente a M06 | `…` §2.2 |
| Ciclo de vida y costura E2E de M05b | `…` §8.5, §8.6 |
| Ledger de paralelización | `312cd0dc:docs/integracion/REGISTRO-PARALELIZACION.md` |
| Reglas de módulo, ADR-016/018, atribución triple | `CLAUDE.md` en `d26f28a` |

### 0.3 Lo que este documento sí decide

Solo lo que es **propio de Seguimiento** y no toca la frontera: qué datos son suyos, cómo se clasifican
frente a ADR-016/018, qué pantallas tiene, qué barreras hacen falta y cómo es su ciclo. Donde una
decisión sería de producto o de JP, queda planteada y marcada, no tomada.

---

## 1. Propósito y valor comercial

M05b responde **«qué trabajo concreto aceptó el negocio»**. M06 responde **«cómo va ese trabajo, hoy,
mirando todo junto»**.

Un negocio con cinco órdenes abiertas no necesita M06: le basta la bandeja de M05b. Lo necesita cuando
tiene treinta y la pregunta deja de ser «¿cuál es el estado de esta orden?» y pasa a ser **«¿qué hay
atascado, qué sale hoy y en qué orden lo hacemos?»**. Eso no es un dato de cada orden: es la vista del
conjunto, y es lo único que M06 aporta.

Lo que M06 añade sobre M05b:

- **Un tablero** donde las órdenes se ven agrupadas por estado, con su código visible, su cliente y su
  responsable, sin abrir una por una.
- **Un orden de trabajo decidido por el personal** dentro de cada agrupación: qué se atiende primero.
  M05b no tiene opinión sobre eso, y es información real que hoy vive en la cabeza de alguien.
- **Plazos internos de taller**, distintos de la promesa al cliente: M05b guarda `promised_at`, que es
  lo prometido; M06 guarda cuándo algo tiene que estar hecho **por dentro** para que esa promesa se
  cumpla.
- **Notas de seguimiento** sobre el avance, que no son las notas internas de la orden.

Sin M06 no se pierde nada de lo que M05b registra: el detalle sigue teniendo estado e historial
autoritativo. Lo que falta es la vista de conjunto.

---

## 2. Alcance y fronteras

### 2.1 Dentro de alcance

- tablero de órdenes agrupadas por el estado que publica M05b;
- orden/prioridad de trabajo **dentro** de una agrupación, decidido por el personal;
- plazos internos propios de seguimiento, distintos de la promesa al cliente;
- notas de seguimiento propias, distintas de las notas internas de la orden;
- lectura de orden, estado actual e historial **por Contracts de M05b**;
- provocar una transición **llamando la operación autoritativa de M05b** y releer el resultado;
- degradación observable cuando M05b no está activo;
- contrato propio de lectura para el futuro M08, sin convertirlo en dependencia dura;
- ciclo completo de instalación, activación, desactivación, desmontaje, reinstalación y reactivación.

### 2.2 Fuera de alcance

- **estados, máquina de estados e historial autoritativo — M05b**, sin excepción;
- alta, edición, líneas, snapshots, contacto y código visible de la orden — M05b;
- vitrina y edición de servicios — M05a;
- portal e historial visible al cliente — M08;
- cobro, caja, turnos o comprobantes — M13/M14; pagos en línea — M11;
- inventario — M09;
- agenda o reserva de citas;
- notificaciones por correo, WhatsApp o SMS;
- sincronización física entre nodos — M16; M06 solo nace con datos aptos para ella;
- cualquier tablero de pedidos de M03 o de solicitudes de M07: M06 v1 sigue órdenes de servicio.

> **La frontera más fácil de cruzar sin darse cuenta.** Un tablero necesita saber qué columnas pintar
> y qué arrastres son legales. La tentación es escribir esa lista en M06 «porque ya se sabe cuál es».
> **Eso sería una segunda máquina de estados**, aunque empiece como un `const`. M06 obtiene esa lista
> de `ServiceOrderStatuses.All` y `.LegalTransitions` (§6.1–6.2); no mantiene una copia propia.

---

## 3. Dependencias y ciclo entre módulos

| Módulo | Tipo | Por qué |
|---|---|---|
| CORE | dura | sesión administrativa, auditoría, activación, ajustes |
| **M05b Servicios — Órdenes** | **dura** | sin órdenes no hay nada que seguir; y el estado, la máquina y el historial son suyos |
| M05a Servicios — Vitrina | **ninguna directa** | M06 no lee servicios: lo que necesita del servicio llega congelado dentro del snapshot de la orden |
| M04 Clientes | **ninguna directa** | el nombre del cliente llega congelado en el contrato de M05b |
| M08 Portal | **M06 será dependencia blanda de M08** | el portal mostrará trabajos si M06 está activo, y funcionará sin él |

### 3.1 Qué significa que M05b sea dependencia dura

- M06 **no se activa** si M05b no está activo; lo impide la plataforma, no una comprobación de M06.
- M06 puede declarar **clave foránea cruzada** hacia la orden de M05b, por ser dependencia dura — con
  la condición de replicación del §4.
- M06 referencia `Sillar.Shared`, `Sillar.Core.Contracts` y
  `Sillar.Modules.ServiceOrders.Contracts`. **Nunca** el `Domain` ni el `Data` de M05b.
- Desmontar M05b exige M06 desmontado antes. Es la guarda C6 ya conocida: mira módulos
  **instalados**, no solo activos.
- **No hay modo degradado para una dependencia dura.** Si el grafo es incoherente, lo diagnostica el
  arranque; la interfaz no simula un tablero vacío como si fuera un estado normal.

### 3.2 M06 como dependiente de nadie más, y como proveedor de M08

M06 no tiene dependencias blandas en v1. Será **dependencia blanda de M08**: el portal enseñará el
avance de un trabajo si M06 está activo y, si no, enseñará lo que M05b publique. Esa degradación es de
M08 y se diseña en su SPEC; aquí solo se promete **no obligar** a M08 a tener M06.

---

## 4. Replicación antes de la primera migración

Pregunta aplicada a cada tabla: **¿esta fila puede nacer en un nodo y tener que existir en otro?**

**Y antes de responderla, el hecho que la condiciona:** las cuatro tablas de M05b que interesan a M06
—orden, líneas, asignaciones e historial— **son replicadas** (`312cd0dc` §4). Una tabla replicada no
puede referenciar a una no replicada (ADR-018), y una FK cruzada de M06 apunta a una orden replicada.

| Tabla conceptual de M06 | ¿Replicable? | Motivo | PK propuesta | `origin_node` | Versión |
|---|---|---|---|---|---|
| `tracking.order_tracking` — una fila por orden seguida: su prioridad y sus plazos internos | **Sí** · ratificado | **M08 Portal puede ejecutarse en un nodo web y tiene que poder ver el seguimiento de órdenes nacidas en mostrador.** Si el seguimiento no viaja, el portal no ve nada de lo que ocurre donde se trabaja | uuid v7 generado por la aplicación | Sí | `row_version` |
| `tracking.tracking_notes` — notas internas de avance | **Sí** · ratificado | Misma razón: la nota explica el trabajo, y el trabajo se consulta desde otro nodo. **Replicarla no la expone** — ver §5.2 | uuid v7 generado por la aplicación | Sí | `row_version` |

> **Ratificado el 7 de octubre de 2026, y por una razón que yo no había visto.** Mi propuesta era la
> correcta pero el argumento era más débil: hablaba de que la tarjeta no perdiera su prioridad al
> cambiar de nodo. El argumento que decide es otro y es de producto: **M08 Portal puede ejecutarse en un
> nodo web y debe poder ver el seguimiento de órdenes nacidas en mostrador.** Con los datos locales, el
> portal sería ciego a todo lo que pasa en el taller.
>
> Queda cerrado: UUID v7 de aplicación, `origin_node` y `row_version` en las dos tablas. **La decisión
> cara de deshacer está tomada antes del primer `CREATE TABLE`**, que era el único momento en que era
> barata.

> **Y lo que la replicación NO decide: la concurrencia.** Dos nodos pueden cambiar la prioridad o el
> plazo de la misma tarjeta y producir dos valores igual de válidos. **M06 no resuelve eso y no inventa
> «última escritura gana»:** está diferido a M16 y registrado en `docs/PENDIENTES.md` §30. Replicar es
> hacer que el dato viaje; converger es otra cosa y tiene otro dueño.

### 4.1 Consecuencias ADR-016/018 de la decisión ratificada

- Los UUID v7 los genera la aplicación, nunca PostgreSQL.
- `origin_node` dice dónde nació la fila; no identifica al trabajador.
- `order_tracking.service_order_id` → `service_orders.service_orders`: **replicada → replicada**, FK
  cruzada permitida por ser dependencia dura, declarada en la migración de M06.
- `tracking_notes.order_tracking_id` → `order_tracking`: replicada → replicada, FK interna.
- **Ninguna tabla de M06 referencia `core.admin_users`.** La atribución es la triple congelada de
  siempre: nombre visible, identificador local y nodo de la cuenta.
- M06 **no guarda** el código visible, el cliente, el estado ni el historial: los lee por contrato. Lo
  único que copia es el `service_order_id`.

### 4.2 Barrido de referencias

| Origen | Destino | Origen | Destino | Resultado |
|---|---|---:|---:|---|
| `order_tracking.service_order_id` | `service_orders.service_orders` | replicada | replicada | **Permitida** (dependencia dura) |
| `tracking_notes.order_tracking_id` | `order_tracking` | replicada | replicada | Permitida |
| atribuciones de M06 | `core.admin_users` | replicada | no replicada | **Sin FK; triple congelada** |
| cualquier fila de M06 | `services.*` de M05a | replicada | no replicada | **Sin referencia alguna**: M06 no conoce M05a |
| cualquier fila de M06 | estado/historial de M05b | — | — | **Sin copia**: se lee por contrato |

> **Esta clasificación queda cerrada para v1.** Cambiarla en el futuro exigiría reabrir explícitamente
> la decisión ADR-016/018 y revisar PK, FK y estrategia de sincronización; no es una variable del Paso 2.

---

## 5. Modelo conceptual de datos propios

Tipos propuestos; el diccionario físico pertenece al Paso 2 (Datos), que **no se abre** todavía.

### 5.1 `tracking.order_tracking`

Una fila por orden seguida. **No duplica nada de M05b.**

| Campo conceptual | Tipo propuesto | Nulo | Significado |
|---|---|---:|---|
| `order_tracking_id` | uuid v7 | no | PK técnica generada por la aplicación |
| `service_order_id` | uuid | no | La orden de M05b. FK cruzada; es el único dato suyo que M06 copia |
| `board_priority` | integer | no | Posición de trabajo dentro de su agrupación. Menor es antes |
| `internal_due_at` | timestamptz | sí | Plazo **interno** de taller. **No es la promesa al cliente**: ésa es `promised_at` y es de M05b |
| `pinned` | boolean | no | Fijada arriba por el personal, con `DEFAULT false` |
| `last_touched_by_name` | text | sí | Nombre congelado de quien tocó el seguimiento por última vez |
| `last_touched_by_admin_user_id` | integer | sí | Identificador local |
| `last_touched_by_admin_user_home_node` | text | sí | Nodo de la cuenta |
| `last_touched_at` | timestamptz | sí | Cuándo |
| `is_active` | boolean | no | Baja lógica, nunca `DELETE` |
| `created_at` / `updated_at` | timestamptz | no | `DEFAULT now()` y trigger `set_updated_at()` |
| `origin_node` | text | no | Nodo donde nació la fila |
| `row_version` | bigint | no | Marca futura de sincronización |

Reglas de valor para el `CHECK`: `board_priority >= 0`; la triple de `last_touched_*` **completa o
enteramente nula**; `internal_due_at` sin relación impuesta con `promised_at` —son dos hechos
distintos— pero **nunca anterior a la recepción de la orden**, que se lee del contrato.

> **Por qué una fila por orden y no una columna en la orden.** Añadir `board_priority` a
> `service_orders` sería más corto y sería exactamente el error: M06 escribiendo en el schema de M05b.
> La fila propia es la forma de que la prioridad sea de Seguimiento y desaparezca al desmontarlo.

> **Ratificado: la fila es *lazy*.** No existe mientras nadie cambie datos propios de M06. El orden
> inicial es **implícito por `received_at`**, que llega en el contrato, y la fila **se materializa** al
> fijar, reordenar, poner un plazo o añadir cualquier información propia.
>
> Es la tercera vía, la que recomendé y por el motivo que importa: **M06 no escribe por el hecho de
> leer.** Un tablero que creara una fila por cada orden que alguien mira ensuciaría la base con
> seguimiento que nadie pidió, y haría imposible distinguir «sin prioridad asignada» de «prioridad
> cero». Con la fila *lazy*, su existencia **significa algo**: alguien decidió sobre esta orden.
>
> Consecuencia para el tablero: una tarjeta sin fila **no es un caso de error**. Se pinta en su orden
> implícito, y la primera acción sobre ella la materializa.

### 5.2 `tracking.tracking_notes` — internas, y solo internas

**Las notas de seguimiento son exclusivamente internas.** Y la regla que lo gobierna, dicha una vez
para no tener que deducirla después:

> **Replicación física no es exposición contractual.**

Que una nota viaje entre nodos no la vuelve visible para nadie fuera del personal. Concretamente,
**M08 Portal no lee `tracking_notes`, no las recibe por Contracts y no accede al schema `tracking`**.
Lo que M06 publique para M08 (§6.5) no incluye notas.

**No existe `customer_visible` en v1.** No es un olvido: una columna así invitaría a escribir en el
mismo sitio dos cosas con audiencias distintas, y el día que alguien marcara mal una casilla una nota
de taller aparecería en el portal de un cliente. Si alguna vez hace falta un mensaje para el cliente,
será **otra cosa con otro nombre**, no una nota interna con un interruptor.

| Campo conceptual | Tipo propuesto | Nulo | Significado |
|---|---|---:|---|
| `tracking_note_id` | uuid v7 | no | PK técnica |
| `order_tracking_id` | uuid | no | FK interna |
| `body` | text | no | La nota. `CHECK` de texto obligatorio no vacío |
| `author_name` | text | sí | Nombre congelado |
| `author_admin_user_id` | integer | sí | Identificador local |
| `author_admin_user_home_node` | text | sí | Nodo de la cuenta |
| `created_at` | timestamptz | no | `DEFAULT now()` |
| `is_active` | boolean | no | Baja lógica |
| `origin_node` | text | no | Nodo donde nació |
| `row_version` | bigint | no | Marca futura |

La triple del autor es **completa o enteramente nula**. Una nota del sistema guarda tres `NULL`;
**nunca se inventa un trabajador llamado «Sistema»**.

### 5.3 Lo que deliberadamente no existe

| No existe | Por qué |
|---|---|
| Tabla de columnas del tablero | Las columnas **son los estados de M05b**. Una tabla propia sería una segunda máquina |
| Copia del estado actual | Es de M05b. Una copia es una segunda autoridad, y la segunda siempre miente algún día |
| Copia del historial | Igual. M06 lo lee y lo pinta |
| Copia del código visible, cliente o líneas | Llegan por contrato cada vez. Un snapshot aquí envejecería sin que nadie lo note |
| Contador de serie propio | M06 no emite ningún código visible: no tiene identidad que mostrar al usuario |

### 5.4 Datos semilla

**Ninguno.** El seguimiento es de cada instalación. `database/modules/tracking/02_seed.sql` existirá
vacío y transaccional, **y se aplicará igual**: la asimetría de omitirlo solo se nota el día que deje
de estar vacío — es la lección que M03 pagó en el arnés e2e.

---

## 6. Contrato que M06 consume de M05b

### 6.1 La firma real, citada

Fuente: `backend/Sillar.Modules.ServiceOrders.Contracts/ServiceOrderContracts.cs` en
`f494eae81aa89872ebe4860aad319e25fd9dca1c`. **No se transcribe aquí una firma inventada**: lo que sigue
es lo que el archivo declara.

| Pieza | Qué publica |
|---|---|
| `ServiceOrderStatuses` | Las cinco constantes, `All` como `IReadOnlyList<ServiceOrderStateDefinition>` y `LegalTransitions` como `IReadOnlyList<ServiceOrderTransitionDefinition>` |
| `ServiceOrderStateDefinition` | `Code`, `DisplayName`, `DisplayOrder`, `IsTerminal` |
| `ServiceOrderTransitionDefinition` | `FromStatus`, `ToStatus` |
| `ServiceOrderScope` | `Open`, `Closed`, `All` |
| `ServiceOrderSort` · `ServiceOrderSortDirection` | `ReceivedAt`, `PromisedAt`, `UpdatedAt`, `VisibleCode` · `Ascending`, `Descending` |
| `ServiceOrderQuery` | `Scope`, `Status?`, `Sort`, `Direction`, `Page` (`PageRequest` de `Sillar.Shared.Paging`) |
| `ServiceOrderOutcome` | `Ok`, `NotFound`, `Invalid`, `Conflict` |
| `ServiceOrderOperation<T>` | `Outcome`, `Error?`, `Value?` |
| `IServiceOrderTrackingSource` | `GetAsync(id, ct)` → `ServiceOrderTrackingSnapshot?` · `ListAsync(query, ct)` → `PagedResult<ServiceOrderTrackingSummary>` |
| `IServiceOrderTransitions` | `TransitionAsync(id, expectedStatus, targetStatus, ct)` → `ServiceOrderOperation<ServiceOrderTransitionResult>` |
| `ServiceOrderTrackingSummary` | id, `VisibleCode`, `CustomerName`, `CurrentStatus`, `ReceivedAt`, `PromisedAt?`, `CurrentAssignee?`, **`UpdatedAt`** |
| `ServiceOrderTrackingSnapshot` | lo del resumen más `Items`, `StatusHistory` y **`UpdatedAt`** |
| `ServiceOrderWorkItemSnapshot` | `ServiceOrderItemId`, `ServiceName`, `SaleUnit?`, `Quantity`, `RequestedDetails` |
| `StaffSnapshot` | `DisplayName`, `AdminUserId`, `HomeNode` — la triple, tal cual |
| `ServiceOrderStatusHistorySnapshot` | `StatusHistoryId`, `FromStatus?`, `ToStatus`, `OccurredAt`, `PerformedBy?`, `OriginNode` |
| `ServiceOrderTransitionResult` | `ServiceOrderId`, `CurrentStatus`, `HistoryEntry` |

El archivo no expone `IQueryable`, ni entidades EF, ni SQL. **M06 no necesita nada más y no pedirá
nada más para v1.**

### 6.2 Los siete huecos, contrastados uno a uno

Los siete están **resueltos en el código publicado**. Se conserva la columna de lo que faltaba porque
el valor de este apartado es poder comprobar que ninguna necesidad quedó sin respuesta — y que ninguna
se resolvió inventándola desde M06.

| # | Lo que el tablero necesitaba | Cómo lo resuelve el contrato real | Estado |
|---|---|---|---|
| **C1** | Qué columnas pintar, y en qué orden | `ServiceOrderStatuses.All`, con `DisplayName` en español y `DisplayOrder` | **RESUELTO** |
| **C2** | Qué arrastres son legales desde un estado | `ServiceOrderStatuses.LegalTransitions`, siete pares | **RESUELTO** |
| **C3** | Listar cerradas, o todas | `ServiceOrderScope { Open, Closed, All }` en la consulta | **RESUELTO** |
| **C4** | Filtrar, ordenar y paginar | `ServiceOrderQuery` con `Status?`, `Sort`, `Direction` y `PageRequest`; respuesta `PagedResult<T>` | **RESUELTO** |
| **C5** | Saber si algo cambió | `UpdatedAt` en resumen **y** en snapshot, y `ServiceOrderSort.UpdatedAt` para ordenar por él | **RESUELTO** |
| **C6** | Qué pasa con una transición ilegal | `ServiceOrderOperation<T>` con `Outcome`: `Ok`, `NotFound`, `Invalid`, `Conflict`. **Sin excepciones** | **RESUELTO** |
| **C7** | Transición condicionada al estado visto | `TransitionAsync(id, **expectedStatus**, targetStatus, ct)` | **RESUELTO** |

Y tres cosas que el contrato real decide y que **cambian cómo M06 las usa**:

**1 · `LegalTransitions` es dato de representación, no autoridad.** El propio archivo lo dice: «la
operación las revalida». M06 las usa para **no ofrecer** un arrastre que va a fracasar; **nunca** para
concluir que uno va a funcionar. La autoridad sigue siendo `TransitionAsync`, y M06 obedece su
`Outcome` aunque su copia de la lista diga otra cosa. Si alguna vez discrepan, **manda la operación** y
la discrepancia es un defecto de M05b que M06 no disimula.

**2 · La semántica vive en `Outcome`, no en `Error`.** M06 **ramifica por el `enum`** y jamás interpreta
el texto. `Error` se enseña a la persona tal como llega; leerlo para decidir sería construir una regla
sobre una cadena que su dueño puede reescribir mañana.

**3 · `IsTerminal` es información nueva, y útil.** `completed` y `cancelled` son terminales. Una columna
terminal no ofrece arrastres de salida, y eso sale del dato, no de que M06 sepa cuáles son.

**C7, que era mi hallazgo más serio, quedó cerrado por donde tenía que cerrarse.** Dos personas con el
tablero abierto ven `in_progress`; ahora la segunda recibe `Conflict` en vez de provocar un cambio que
nadie pidió. M06 **no reintenta en silencio**: relee y enseña el estado real, porque reintentar sobre un
estado que ya cambió repite el problema con otra cara.

### 6.3 El bus, y para qué sirve aquí

**El bus nunca es el camino por el que viaja un hecho que alguien necesita; es el camino por el que
viaja un aviso.**

M06 **no reconstruye nada** desde `InProcessEventBus`. La verdad durable es el estado y el historial de
M05b, leídos por contrato. Los avisos de M05b —`ServiceOrderCreated`,
`ServiceOrderAssignmentChanged`, `ServiceOrderStatusChanged`— pueden servir, **más adelante y como
mejora**, para que un tablero abierto se refresque sin que alguien pulse recargar.

Condición que no se negocia: **el tablero tiene que ser correcto con el bus apagado.** Si una prueba de
M06 solo pasa cuando llega un evento, la prueba está mal escrita o el diseño cruzó la línea.

### 6.4 Endpoints administrativos propuestos

Todos exigen sesión administrativa y CSRF en escritura. **Nada público en v1**: lo que ve el cliente es
de M08.

| Método y ruta | Para qué | Rol |
|---|---|---|
| `GET /api/admin/tracking/board` | El tablero: agrupaciones y tarjetas | `editor+` |
| `GET /api/admin/tracking/orders/{serviceOrderId}` | Detalle de seguimiento de una orden, con su historial leído de M05b | `editor+` |
| `PUT /api/admin/tracking/orders/{serviceOrderId}/priority` | Reordenar o fijar una tarjeta | `editor+` |
| `PUT /api/admin/tracking/orders/{serviceOrderId}/due` | Plazo interno | `editor+` |
| `POST /api/admin/tracking/orders/{serviceOrderId}/notes` | Añadir nota de seguimiento | `editor+` |
| `DELETE /api/admin/tracking/notes/{noteId}` | Baja lógica de una nota | `admin+` |
| `PUT /api/admin/tracking/orders/{serviceOrderId}/status` | **Delega en `IServiceOrderTransitions`**. No escribe estado ni historial | `editor+` |

> **La última fila es la frontera hecha ruta.** Existe por comodidad de la interfaz, no porque M06
> tenga autoridad: recibe la petición, llama a M05b, y devuelve lo que M05b conteste. Si alguna vez
> alguien la implementa escribiendo en `service_orders`, la barrera B2 del §8 tiene que ponerse roja.

Cada endpoint llevará comentarios XML y ejemplo de cuerpo en Swagger — **un `ISchemaExamples` propio
desde el primer día**. M07 aprendió esto tarde: su prueba de ejemplos pasó durante semanas porque el
módulo estaba desactivado y sus esquemas no llegaban al documento.

### 6.5 Contrato propio de M06, para M08

M06 publicará `Sillar.Modules.Tracking.Contracts` con lo mínimo para que M08 enseñe avance sin conocer
el schema de M06. Su forma se decide **cuando se abra M08**, no aquí: diseñar hoy el contrato de un
consumidor que no existe es cómo se fabrica un campo que nadie usa. Queda anotado que **existirá** y
que M06 no será dependencia dura de M08.

---

## 7. Reglas, atribución, auditoría y errores

### 7.1 Reglas de negocio

1. **M06 no cambia estados.** Toda transición pasa por `IServiceOrderTransitions`.
2. **M06 no escribe en `service_orders`.** Ni estado, ni historial, ni nada.
3. **El estado que M06 enseña es el que acaba de leer**, no uno recordado.
4. La prioridad es **por agrupación**: reordenar dentro de un estado no mueve la tarjeta de estado.
5. Un plazo interno **no es** la promesa al cliente, no la sustituye y no la contradice: son dos
   fechas con dueños distintos.
6. Las notas de seguimiento **no son** las notas internas de la orden, y no se mezclan en la misma
   lista.
7. Baja lógica con `is_active`; nunca `DELETE` físico.
8. Si una orden deja de existir para el contrato, su seguimiento **no se pinta**. No se inventa una
   tarjeta sobre un identificador que ya no resuelve.

### 7.2 Atribución triple

M06 congela **nombre visible, identificador local y nodo de la cuenta** —los tres o ninguno— en
`order_tracking.last_touched_*` y en `tracking_notes.author_*`. Se toman exclusivamente de
`ICurrentAdmin`. **Sin FK hacia `core.admin_users`**: es dato de bitácora, no puntero, y esa tabla no
se replica.

Una escritura de sistema guarda tres `NULL`. **Nunca un trabajador ficticio**: dentro de un año
«Sistema» no se distingue de una persona real.

### 7.3 Auditoría

Toda escritura de M06 deja fila con `module_code = 'tracking'` y un resumen que **nombra la fila**, no
la clase: «Prioridad de la orden `OS-2026-0147` cambiada», no «Cambio de prioridad». El número visible
se toma del contrato, no se construye en M06.

### 7.4 Errores observables

Ningún «Ha ocurrido un error» y ningún botón «Aceptar». Un conflicto es una frase que dice qué lo
impide y qué hacer, y un botón nombra la acción que ejecuta.

| Situación | Qué ve la persona |
|---|---|
| Transición rechazada por M05b | Lo que M05b conteste, dicho en la frase de M05b. **M06 no reinterpreta** la regla ajena |
| La orden ya no existe para el contrato | «Esa orden ya no está disponible. Vuelve al tablero para ver las actuales.» |
| Plazo interno anterior a la recepción | «El plazo no puede ser anterior a la recepción de la orden, que fue el <fecha>.» |
| Nota vacía | «Escribe la nota antes de guardarla.» |
| M05b inactivo | **No se pinta un tablero vacío**: es dependencia dura y el grafo lo diagnostica (§9.7) |

---

## 8. Barreras, ciclo y paridad E2E

### 8.1 Reglas que podrían ser ciertas solo porque hoy hay uno

| Regla | ¿Por el mundo, o por la circunstancia? |
|---|---|
| «La prioridad es un entero y basta» | **Por la circunstancia, y sigue abierto.** Con un solo nodo nadie reordena en dos sitios a la vez; con dos, los dos valores son válidos. **Diferido a M16**, `PENDIENTES.md` §30 |
| «El tablero cabe en una pantalla» | **Por la circunstancia**, y ya está cubierto: el contrato pagina (`PageRequest`, por omisión 50 y máximo 200) |
| «Las columnas son cinco» | **Por la circunstancia, y no es de M06.** Se leen de `ServiceOrderStatuses.All`: si M05b añade un estado, el tablero crece sin tocar M06 |
| «Solo hay un tablero» | **Por la circunstancia, y ratificado así para v1.** Si mañana hay dos talleres habrá que volver aquí; hoy no se anticipa |

### 8.2 Barreras nuevas y lugar de aplicación

| Barrera | Protege | Lugar futuro |
|---|---|---|
| **B1 Dependencia dura** | No activar M06 sin M05b | Activación y grafo de la plataforma |
| **B2 Autoridad de estado** | M06 no escribe `service_orders.status` ni el historial, por ninguna vía | Barrido del ensamblado y de la migración de M06 |
| **B3 Sin lectura de schema ajeno** | M06 no referencia `Domain` ni `Data` de M05b, y no consulta sus tablas | Barrido de referencias de proyecto y de SQL |
| **B4 Atribución triple** | Nunca guardar la triple incompleta ni un actor ficticio | `CHECK` en la tabla y guarda en la operación |
| **B5 Replicación coherente** | Ninguna FK replicada → local | Modelo y migración |
| **B6 Aislamiento del ciclo** | Desmontar M06 no toca `service_orders`, `services`, `crm` ni CORE | `99_drop.sql` y orquestador |
| **B7 Paridad E2E** | Ningún módulo presente queda fuera de binario/setup/migrate/seed | Detector del arnés |
| **B8 Ejemplos de Swagger** | Ningún cuerpo de petición de M06 sin ejemplo | `ISchemaExamples` propio |

**B2 y B3 son las barreras de este módulo.** Las demás son las de siempre; éstas dos son las que
impiden que M06 se convierta en lo que la frontera prohíbe. Y van **en el barrido, no en una revisión
humana**: una regla que se cumple porque alguien se acuerda ya se incumplió una vez.

### 8.3 Las tres direcciones, por barrera

| Barrera | Legal verde | Ilegal rojo y atribuible | Sabotaje del detector |
|---|---|---|---|
| B1 | Activar tras M05b | Activar sin M05b | Detector que devuelve siempre `true`; prueba roja |
| B2 | Transición por el contrato cambia estado e historial en M05b | `UPDATE` de `service_orders.status` desde M06, o inserción en su historial | Barrido que no mira el ensamblado de M06; autoprueba roja |
| B3 | Solo `Contracts` en las referencias | Añadir `ProjectReference` a `Sillar.Modules.ServiceOrders` | Barrido que ignora M06; autoprueba roja |
| B4 | Triple completa, o tres `NULL` | Identificador sin nodo, o autor «Sistema» | Quitar una columna del `CHECK`; prueba roja |
| B5 | FK UUID entre replicadas | FK hacia `core.admin_users` o hacia la serie local de M05b | Analizador que ignora el schema `tracking`; autoprueba roja |
| B6 | El ciclo conserva datos centinela ajenos | `99_drop` que apunta a otro schema | Alterar el destino en base desechable; prueba roja |
| B7 | Binario, setup, migrate y seed coherentes | Quitar M06 de `migrate()` o de la mitad aplicable | Romper el detector para que acepte la omisión; autoprueba roja |
| B8 | Todos los `*Request` con ejemplo | Añadir un DTO sin ejemplo | Comprobar con el módulo desactivado — **el error exacto de M07** |

**B8 merece su nota.** La prueba de ejemplos de Swagger solo ve lo que el documento declara, y un
módulo inactivo no mapea endpoints. El verde de M07 fue un artefacto durante semanas. El sabotaje de
esta barrera es **ejecutarla con M06 apagado y comprobar que eso no produce verde**.

Cada barrera se ve **dejar pasar y rechazar**. La falsificación ocurre en entorno desechable, se
documenta y se restaura. **Nada de esto se ejecuta en el Paso 1.**

### 8.4 Segunda vía independiente

Las afirmaciones de este módulo que no se creen por lectura:

- que M06 **no escribe** en `service_orders` → se comprueba contando filas y comparando `row_version`
  del historial antes y después de toda la suite de M06;
- que el estado que pinta es el leído → se cambia el estado por fuera, por el camino autoritativo de
  M05b, y el tablero tiene que reflejarlo al releer, sin que M06 haya sido avisado;
- que el bus no hace falta → la suite de M06 se ejecuta **con el bus apagado** y queda verde.

### 8.5 Ciclo de vida

**Instalación:** el instalador migra M06 después de CORE y M05b. Instalar **no activa**. Crea
`tracking` y su historial de migraciones dentro de su propio schema.

**Activación:** comprueba schema y M05b activo; no ejecuta DDL. Compone endpoints, navegación y
pantallas en el siguiente arranque coherente.

**Desactivación:** retira rutas y navegación. **Conserva prioridades, plazos y notas**, y no toca una
sola orden de M05b. Desactivar no es borrar.

**Desmontaje:** exige M06 inactivo. Elimina **exclusivamente** el schema `tracking`. Nunca `CASCADE`
como sustituto de comprobar dependencias. **Y es el lado del que M06 es responsable de la guarda C6 de
M05b:** mientras el schema `tracking` exista con su FK hacia la orden, soltar `service_orders` tiene
que negarse.

**Reinstalación:** recrea el schema vacío. Según el contrato de persistencia actual, desmontar elimina
los datos propios; **restaurarlos exige backup explícito**, no reconstrucción: la prioridad que alguien
decidió no se puede deducir de ningún otro sitio.

**Reactivación:** exige M05b activo y schema preparado. Recupera superficies y datos si solo hubo
desactivación; queda vacío si hubo desmontaje y reinstalación sin restauración.

### 8.6 Paridad binario ↔ setup ↔ migrate ↔ seed

Para M06 significa:

- **binario:** `Sillar.Modules.Tracking` y su `Contracts` compilados y copiados junto al host, y el
  módulo descubierto. El `ProjectReference` va en `Sillar.Api.csproj` **y en ningún otro sitio**;
- **setup:** el registro conoce código, versión, schema y dependencia dura `service_orders`;
- **migrate:** el arnés incluye `TrackingDbContext` **después** de M05b, porque su FK cruzada apunta
  allí;
- **seed:** el arnés incluye `tracking` aunque su seed esté vacío;
- **prueba:** un setup real deja registro coherente, schema migrado y cero filas de negocio.

Negativo obligatorio: si M06 aparece en binario/setup pero se omite de `migrate()` o de la mitad
aplicable, la barrera termina **roja y nombrando `tracking`/M06**. Un fallo genérico por conteo no
basta.

> **Esta sección está escrita con la factura de dos módulos delante.** M03 entró a `main` sin su mitad
> del arnés y el hueco vivió dos días sin producir ningún rojo; M05a entró **con** su mitad y no abrió
> hueco ninguno. La regla de paridad —repetir la auditoría cada vez que entra un módulo real— nombra a
> M06 como caso futuro. **M06 entra acompañado o no entra.**

---

## 9. Pantallas y estados de composición

No se diseñan componentes aquí. Todas usarán componentes y tokens de SILLAR, sin un color escrito en
ningún sitio, y el contenido visible irá en español.

### 9.1 A1 — Tablero

**Un solo tablero.** Las agrupaciones salen de `ServiceOrderStatuses.All`: el rótulo es su
`DisplayName` —ya en español, de M05b— y el orden su `DisplayOrder`. **M06 no traduce ni ordena estados
por su cuenta.** Las terminales (`IsTerminal`) no ofrecen arrastre de salida.

Dentro de cada agrupación, las tarjetas por `board_priority`, y las que **no tienen fila** —la mayoría,
por el *lazy* del §5.1— en su orden implícito por `ReceivedAt`. Las fijadas, arriba.

Cada tarjeta: código visible, cliente, responsable actual y plazo interno si lo tiene. La lista llega
por `ListAsync` con `Scope.Open` para las columnas de trabajo; `Scope.Closed` alimenta la vista de
terminadas, que **se pagina** porque crece sin parar.

Vacío honesto: «Todavía no hay órdenes que seguir», sin inventar columnas.

### 9.2 A2 — Reordenar y fijar

Arrastrar dentro de una agrupación cambia prioridad, no estado. Si el movimiento cruza de agrupación,
**es una transición**: se confirma y se delega en M05b (A5).

### 9.3 A3 — Detalle de seguimiento

Lo de M05b, **leído**: código, cliente, líneas, responsable, estado, historial completo. Y lo de M06,
editable: prioridad, plazo interno y notas. La separación tiene que verse: lo que no se puede cambiar
aquí no se pinta como si se pudiera.

### 9.4 A4 — Notas

Lista cronológica con autor y fecha. Añadir y dar de baja. **No se mezclan con `staff_notes` de
M05b**, que es otra cosa y de otro dueño.

### 9.5 A5 — Confirmación de transición

Dice el estado de origen, el destino y qué se va a registrar. Al aceptar llama a
`TransitionAsync(id, expectedStatus, targetStatus, ct)` —`expectedStatus` es **el que el tablero estaba
enseñando**, no el que se supone— y al volver **relee** y pinta lo que M05b conteste.

El `Outcome` decide qué se ve, y se ramifica por el `enum`, nunca leyendo `Error`:

| `Outcome` | Qué ve la persona |
|---|---|
| `Ok` | La tarjeta en su nueva columna, releída |
| `Conflict` | «Esta orden cambió mientras lo mirabas: ahora está <estado>. Vuelve a intentarlo si todavía quieres moverla.» Y el tablero se refresca **antes** de que decida |
| `Invalid` | La frase de M05b, tal cual. M06 no reinterpreta la regla ajena |
| `NotFound` | «Esa orden ya no está disponible. Vuelve al tablero para ver las actuales.» |

**`Conflict` no se reintenta solo.** Reintentar sobre un estado que ya cambió repite el problema con
otra cara: lo que hace falta es que la persona vea el estado real y decida.

### 9.6 Estados de composición

- **M06 inactivo:** sin grupo de menú, sin enlaces, sin rutas y **sin hueco**. Un contenedor vacío que
  explica su ausencia sigue siendo un hueco.
- **M06 activo y M05b coherente:** todas las superficies anteriores.
- **Grafo incoherente** (M06 activo, M05b no): lo diagnostica el arranque. **La interfaz no simula un
  modo degradado para una dependencia dura.**
- **Sin órdenes:** tablero con su estado vacío, no una pantalla en blanco.
- **M08 ausente:** irrelevante para M06; nada cambia.

---

## 10. Criterios de aceptación futuros

### 10.1 La frontera

1. Ninguna escritura de M06 cambia `service_orders.status`, comprobado contra la base.
2. Ninguna escritura de M06 inserta en el historial de M05b.
3. Arrastrar entre agrupaciones produce **exactamente una** fila de historial, escrita por M05b.
4. Una transición ilegal se rechaza con la frase de M05b, y **no deja nada escrito** en `tracking`.
5. El ensamblado de M06 no referencia `Domain` ni `Data` de M05b (barrido, no revisión).
6. La suite de M06 pasa **con el bus apagado**.
7. **Las columnas del tablero salen de `ServiceOrderStatuses.All`**: añadir un estado en M05b hace
   crecer el tablero sin tocar una línea de M06. Se comprueba con un estado añadido en una copia
   desechable del contrato, no leyendo el código.
8. **`LegalTransitions` no manda.** Si la lista dijera que un arrastre es legal y `TransitionAsync`
   devolviera `Invalid`, M06 obedece la operación y enseña su frase. Se provoca haciendo divergir la
   lista a propósito en entorno desechable.
9. **M06 ramifica por `Outcome` y nunca por `Error`.** Cambiar el texto de `Error` no cambia ni una
   rama de M06: se comprueba sustituyéndolo por otro y viendo que el comportamiento es idéntico.
10. **`expectedStatus` es el que el tablero enseñaba.** Dos clientes con la misma vista: el primero
    mueve, el segundo recibe `Conflict`, **no** un segundo cambio de estado. El historial queda con
    exactamente una fila nueva.

### 10.2 Datos propios

11. Reordenar no cambia el estado; cambiar de estado no altera la prioridad de las demás.
12. El plazo interno no sustituye `promised_at`, y las dos fechas se enseñan como lo que son.
13. La triple de atribución es completa o enteramente nula, forzado por `CHECK`.
14. Una nota del sistema guarda tres `NULL`, nunca «Sistema».
15. Las bajas son lógicas: ningún `DELETE` físico en tablas de negocio.

### 10.3 Replicación y numeración

16. Las tablas de M06 cumplen la decisión ratificada del §4 —replicadas, UUID v7, `origin_node`,
    `row_version`—, comprobado por consulta ejecutable y no por lectura.
17. Ninguna FK de M06 apunta a `core.admin_users` ni a la serie local de M05b.
18. M06 **no emite** ningún código visible.
19. **Las notas internas no salen por ningún contrato de M06.** Se comprueba contra el cuerpo crudo de
    todo lo que M06 publique: un marcador único escrito en una nota no aparece en ninguna respuesta
    destinada a otro módulo. *Replicación física no es exposición contractual.*

### 10.4 API, errores e interfaz

20. Las rutas de M06 en Swagger son el conjunto que la suite ejerce, contrastado contra el documento.
21. Ningún `*Request` de M06 queda sin ejemplo, **comprobado con M06 activo**.
22. Ninguna frase dice «error» ni ofrece «Aceptar»; cada botón nombra su acción.
23. Ningún componente lleva un color escrito.

### 10.5 Barreras y falsificación

24. Las ocho barreras del §8.2 se ven dejar pasar y rechazar, con el sabotaje del detector en rojo.
25. Ningún sabotaje queda en HEAD, comprobado con `git diff` y un barrido por la palabra.

### 10.6 `[M06-CICLO]` — dentro de la etapa 6

Una sola prueba, en la etapa 6 de la puerta canónica, **no un script aparte**:

```
estado inicial     M05b activo · M06 activo · con prioridades, plazos y notas
                   sembrados por la propia prueba, y acreditados antes de tocar nada
desactivar M06     sin grupo de menú, sin rutas, sin huecos · las órdenes de M05b intactas
                   · los datos de seguimiento siguen en la base
desmontar M06      99_drop de tracking · CORE, M05a, M05b, M04 y M02 con los mismos
                   recuentos fila por fila · el host sigue sirviendo
guarda C6 al revés con tracking presente, soltar service_orders se NIEGA y nombra a M06
reinstalar         migrate() + seed() devuelve el schema vacío
reactivar          M05b primero si hiciera falta, M06 después · vuelven las superficies
                   · la FK cruzada hacia la orden está de vuelta, contada en pg_constraint
```

Y la mitad que se olvida: **que la prueba se vea fallar**. Si el detector de la FK devolviera lista
vacía, «ninguna falta» sería trivialmente cierto; la preparación tiene que ponerse roja antes de
destruir nada.

### 10.7 Paridad de instalación

26. La auditoría binario ↔ setup ↔ migrate ↔ seed se **vuelve a medir** con M06 dentro, arrancando el
    host, no leyendo listas.
27. Quitar M06 de `migrate()` pone la barrera roja **nombrando `tracking`**.

---

## 11. Lo que este documento no decide

| Punto | Marca | De quién es |
|---|---|---|
| Replicación de los datos de seguimiento (§4) | **RESUELTO** · replicables, UUID v7, `origin_node`, `row_version` | JP, 07/10/2026 |
| Creación de la fila de seguimiento (§5.1) | **RESUELTO** · *lazy*, orden implícito por `received_at` | Producto, 07/10/2026 |
| Uno o varios tableros (§8.1) | **RESUELTO** · **un tablero** en v1 | Producto, 07/10/2026 |
| Lista de estados y transiciones legales (C1, C2) | **RESUELTO** · `ServiceOrderStatuses.All` y `.LegalTransitions` | M05b, en `f494eae` |
| Listado por alcance, filtro, orden y paginación (C3, C4) | **RESUELTO** · `ServiceOrderQuery` + `PagedResult<T>` | M05b, en `f494eae` |
| Marca de cambio para refresco (C5) | **RESUELTO** · `UpdatedAt` en resumen y snapshot | M05b, en `f494eae` |
| Forma del rechazo (C6) | **RESUELTO** · `ServiceOrderOutcome` + `ServiceOrderOperation<T>` | M05b, en `f494eae` |
| Transición condicionada al estado visto (C7) | **RESUELTO** · `expectedStatus` + `targetStatus` | M05b, en `f494eae` |
| **Convergencia multinodo de prioridad y plazo** | **DIFERIDO a M16**, `docs/PENDIENTES.md` §30. M06 no inventa «última escritura gana» | M16 |
| Forma del contrato de M06 para M08 (§6.5) | abierto a propósito | se decide al abrir M08 |

---

## 12. Flujo y parada obligatoria

**Paso 1 documental: CERRADO por Chat 2 el 7 de octubre de 2026 tras contraste contra el contrato real.**

Los siete huecos están resueltos en `f494eae` y las tres decisiones abiertas fueron ratificadas. Nada
de este documento se apoya ya en una firma propuesta.

**Y sigue sin haber código.** No se cruza a Datos: ni proyecto `Sillar.Modules.Tracking`, ni migración,
ni schema, ni endpoint, ni frontend, ni costura en `Sillar.Api.csproj`, `routes.tsx` o `migrate.ts`.

**Lo único que queda abierto a propósito**, y no bloquea el cierre:

- la **convergencia multinodo** de prioridad y plazo, diferida a M16 (`docs/PENDIENTES.md` §30). M06 no
  inventa «última escritura gana»;
- la **forma del contrato de M06 para M08** (§6.5), que se decide al abrir M08.

Las tres entradas de ledger de este trabajo **ya están transcritas al ledger canónico** en la rama de
M05b. `docs/modules/tracking/LEDGER-M06-PENDIENTE-DE-TRANSCRIBIR.md` queda como **registro histórico de
cómo se produjeron, no como fuente definitiva**: al converger manda el ledger canónico.
