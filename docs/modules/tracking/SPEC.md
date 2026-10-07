# SPEC — M06 Seguimiento · Paso 1 documental (pre-SPEC)

- **Creación:** 7 de octubre de 2026 · America/Lima
- **Última verificación:** 7 de octubre de 2026 · America/Lima
- **Commit base de este documento:** `d26f28a0439a9ac72dbedcc097dd8731b37a27c9` (`main` vigente)
- **Base de conocimiento autoritativa:** rama `m05b-service-orders-spec`, cierre del Paso 1 de M05b
  en `312cd0dcdab256ce5cdbf4b99011c62611fcccb2`

> **Qué es este documento y qué no es.** Es el Paso 1 **documental** de M06, escrito contra la
> frontera M05b/M06 **ya ratificada**. No autoriza código: no hay proyecto `Sillar.Modules.Tracking`,
> ni migraciones, ni schema, ni endpoints, ni frontend.
>
> **Y no está definitivamente cerrado.** Todo lo que M06 espera consumir de M05b existe hoy como
> **decisión ratificada en prosa y como firma C# propuesta**, no como paquete publicado. Cada punto en
> esa situación lleva la marca `CONTRATO_PENDIENTE_DE_MATERIALIZAR`. El Paso 1 de M06 solo podrá
> cerrarse cuando se contraste esta SPEC contra el `Sillar.Modules.ServiceOrders.Contracts` real.

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
  M05b**, citados aquí como dato, no adoptados como constantes de M06 (ver §6.2, hueco C6).
- **El bus nunca es el camino por el que viaja un hecho que alguien necesita; es el camino por el que
  viaja un aviso.** M06 no reconstruye historia desde `InProcessEventBus`.
- M06 depende **duro** de M05b.

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
> **Eso sería una segunda máquina de estados**, aunque empiece como un `const`. M06 pide esa lista al
> contrato; si el contrato no la da, no se inventa: se registra (§6.2, hueco C6).

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
| `tracking.order_tracking` — una fila por orden seguida: su prioridad y sus plazos internos | **Sí** (propuesto) | La orden viaja; si su posición de trabajo no viajara, al llegar a otro nodo la tarjeta aparecería sin prioridad y alguien tendría que reordenarla a mano | uuid v7 generado por la aplicación | Sí | `row_version` |
| `tracking.tracking_notes` — notas de avance | **Sí** (propuesto) | Una nota de seguimiento es parte de cómo se entiende el trabajo; leerla solo en el nodo donde se escribió la vuelve inútil | uuid v7 generado por la aplicación | Sí | `row_version` |

> **`DECISION_PENDIENTE` · la replicación de los datos de seguimiento no está ratificada.**
> Lo de arriba es **propuesta razonada, no decisión**, y es exactamente de las caras de deshacer: cambia
> el tipo de la clave primaria. Si JP decide que la prioridad del tablero es **operativa de cada nodo**
> —«cada taller ordena su propia cola»— entonces `order_tracking` pasa a `integer GENERATED ALWAYS AS
> IDENTITY`, sin `origin_node` ni `row_version`, y **deja de poder ser referenciada por cualquier tabla
> replicada**. No se decide aquí. Se decide antes del primer `CREATE TABLE`, que es cuando todavía es
> barato.

### 4.1 Consecuencias ADR-016/018, bajo la propuesta de arriba

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

> **Si la decisión del §4 cambia a «local», este barrido cambia con ella** y la FK cruzada hacia la
> orden replicada deja de ser aceptable sin volver a mirar. Es la razón de pedir la ratificación antes
> y no después.

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

> **`DECISION_PENDIENTE` · ¿se crea la fila sola?** Si el seguimiento solo existe cuando alguien
> ordena una tarjeta, hay órdenes sin fila y el tablero las pinta con prioridad implícita. Si se crea
> al ver la orden por primera vez, M06 escribe por el hecho de leer. Hay una tercera: prioridad
> implícita por `received_at` mientras nadie la toque, y fila solo al tocarla. **La tercera es la que
> recomiendo** —no escribe por leer y no necesita fila para funcionar— pero es decisión de producto.

### 5.2 `tracking.tracking_notes`

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

## 6. Contrato que M06 espera consumir de M05b

### 6.1 Lo ratificado, tal como está

M06 consume `Sillar.Modules.ServiceOrders.Contracts` según `312cd0dc` §6.2:
`IServiceOrderTrackingSource` (`GetAsync`, `ListOpenAsync`), `IServiceOrderTransitions`
(`TransitionAsync`) y los `record` de snapshot, resumen, línea, triple de personal, historial y
resultado de transición.

> **`CONTRATO_PENDIENTE_DE_MATERIALIZAR` · todo el bloque anterior.** La decisión está ratificada y la
> firma está propuesta, pero **el paquete no existe**. Ningún dato de este documento se apoya en una
> firma publicada.

### 6.2 Huecos entre lo que un tablero necesita y lo que la firma propuesta ofrece

Esto **no son campos inventados**: son necesidades del tablero que la firma actual no cubre. Se
registran para contrastarlas cuando C materialice el contrato. **Ninguna se da por concedida.**

| # | Qué necesita el tablero | Qué ofrece hoy la firma | Marca |
|---|---|---|---|
| **C1** | Saber **qué columnas pintar**: la lista de estados vigentes, en su orden | nada | `CONTRATO_PENDIENTE_DE_MATERIALIZAR` |
| **C2** | Saber **qué arrastres son legales** desde un estado, para no ofrecer un movimiento que M05b va a rechazar | nada | `CONTRATO_PENDIENTE_DE_MATERIALIZAR` |
| **C3** | Listar órdenes **cerradas o canceladas**, para una columna de terminadas o una vista de historia | `ListOpenAsync()` solo abiertas | `CONTRATO_PENDIENTE_DE_MATERIALIZAR` |
| **C4** | **Filtrar, ordenar y paginar** el listado: un tablero de treinta tarjetas no es el de trescientas | `ListOpenAsync()` sin parámetros | `CONTRATO_PENDIENTE_DE_MATERIALIZAR` |
| **C5** | Saber **si algo cambió** desde la última lectura, para refrescar sin recargar todo | el resumen no lleva versión ni marca temporal de cambio | `CONTRATO_PENDIENTE_DE_MATERIALIZAR` |
| **C6** | **Qué pasa cuando la transición es ilegal**: ¿excepción, o resultado que lo dice? | `TransitionAsync` solo devuelve el caso correcto | `CONTRATO_PENDIENTE_DE_MATERIALIZAR` |
| **C7** | **Transición condicionada al estado que el tablero vio**, para que dos personas arrastrando a la vez no provoquen un movimiento que ninguna quiso | `TransitionAsync(id, toStatus, ct)` sin estado esperado | `CONTRATO_PENDIENTE_DE_MATERIALIZAR` |

**C1 y C2 son las que deciden si M06 puede existir sin romper la frontera.** Sin ellas, la única forma
de pintar un tablero es escribir los estados y los arrastres dentro de M06 — es decir, **una segunda
máquina de estados**, que es justo lo que la frontera prohíbe. No propongo una firma: propongo que el
contrato de M05b publique su máquina como **dato de lectura**, y que la forma la decida M05b, que es su
dueño.

**C7 es la que más se parece a un defecto latente.** Dos personas con el tablero abierto ven
`in_progress`; una arrastra a `ready` y la otra a `cancelled`. Las dos transiciones son legales por
separado, así que M05b aceptará las dos y la segunda ganará sin que nadie sepa que hubo un conflicto.
No lo arregla M06 reintentando: hace falta que la operación acepte el estado esperado. **Lo reporto;
no lo decido.**

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
| «La prioridad es un entero y basta» | **Por la circunstancia.** Con un solo nodo nadie reordena en dos sitios a la vez. Es la decisión del §4 |
| «El tablero cabe en una pantalla» | **Por la circunstancia**: hoy no hay instalación con trescientas órdenes. Es el hueco C4 |
| «Las columnas son cinco» | **Por la circunstancia**, y además no es de M06. Huecos C1 y C2 |
| «Solo hay un tablero» | **Por la circunstancia.** Si mañana hay dos talleres, ¿un tablero o dos? No se decide aquí |

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

Agrupaciones **derivadas de los estados que publica M05b** (huecos C1/C2), y en cada una las tarjetas
en su `board_priority`. Cada tarjeta: código visible, cliente, responsable actual y plazo interno si lo
tiene. Las fijadas, arriba. Vacío honesto: «Todavía no hay órdenes que seguir», sin inventar columnas.

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

Dice el estado de origen, el destino y qué se va a registrar. Al aceptar llama a M05b; al volver,
**relee** y pinta lo que M05b conteste. Si M05b rechaza, se enseña su frase tal cual.

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

### 10.2 Datos propios

7. Reordenar no cambia el estado; cambiar de estado no altera la prioridad de las demás.
8. El plazo interno no sustituye `promised_at`, y las dos fechas se enseñan como lo que son.
9. La triple de atribución es completa o enteramente nula, forzado por `CHECK`.
10. Una nota del sistema guarda tres `NULL`, nunca «Sistema».
11. Las bajas son lógicas: ningún `DELETE` físico en tablas de negocio.

### 10.3 Replicación y numeración

12. Las tablas de M06 cumplen la decisión del §4, comprobado por consulta ejecutable y no por lectura.
13. Ninguna FK de M06 apunta a `core.admin_users` ni a la serie local de M05b.
14. M06 **no emite** ningún código visible.

### 10.4 API, errores e interfaz

15. Las rutas de M06 en Swagger son el conjunto que la suite ejerce, contrastado contra el documento.
16. Ningún `*Request` de M06 queda sin ejemplo, **comprobado con M06 activo**.
17. Ninguna frase dice «error» ni ofrece «Aceptar»; cada botón nombra su acción.
18. Ningún componente lleva un color escrito.

### 10.5 Barreras y falsificación

19. Las ocho barreras del §8.2 se ven dejar pasar y rechazar, con el sabotaje del detector en rojo.
20. Ningún sabotaje queda en HEAD, comprobado con `git diff` y un barrido por la palabra.

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

21. La auditoría binario ↔ setup ↔ migrate ↔ seed se **vuelve a medir** con M06 dentro, arrancando el
    host, no leyendo listas.
22. Quitar M06 de `migrate()` pone la barrera roja **nombrando `tracking`**.

---

## 11. Lo que este documento no decide

| Punto | Marca | De quién es |
|---|---|---|
| Replicación de los datos de seguimiento (§4) | `DECISION_PENDIENTE` | JP. **Cara de deshacer:** cambia la clave primaria |
| Creación implícita o explícita de la fila de seguimiento (§5.1) | `DECISION_PENDIENTE` | Producto |
| Uno o varios tableros si hubiera dos talleres (§8.1) | `DECISION_PENDIENTE` | Producto |
| Lista de estados y transiciones legales como dato (C1, C2) | `CONTRATO_PENDIENTE_DE_MATERIALIZAR` | **M05b**, que es su dueño |
| Listado de cerradas, filtro, orden y paginación (C3, C4) | `CONTRATO_PENDIENTE_DE_MATERIALIZAR` | M05b |
| Marca de cambio para refresco (C5) | `CONTRATO_PENDIENTE_DE_MATERIALIZAR` | M05b |
| Forma del rechazo de una transición (C6) | `CONTRATO_PENDIENTE_DE_MATERIALIZAR` | M05b |
| Transición condicionada al estado visto (C7) | `CONTRATO_PENDIENTE_DE_MATERIALIZAR` | M05b |
| Forma del contrato de M06 para M08 (§6.5) | abierto a propósito | se decide al abrir M08 |

---

## 12. Flujo y parada obligatoria

**Paso 1 documental: entregado, no cerrado.**

No se cruza a Datos. No hay proyecto, migración, schema, endpoint ni frontend. El Paso 1 de M06 queda
cerrado **solo** cuando se contraste esta SPEC contra el `Sillar.Modules.ServiceOrders.Contracts` real
y se resuelvan, al menos, **C1 y C2** — sin ellas no hay forma de pintar un tablero sin fabricar una
segunda máquina de estados.

Las entradas de ledger que este trabajo produce están en
`docs/modules/tracking/LEDGER-M06-PENDIENTE-DE-TRANSCRIBIR.md`, con el motivo de que todavía no estén
en el archivo compartido.
