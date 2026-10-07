# M05b Servicios — Órdenes · Paso 2 Datos

- **Estado:** implementación candidata; pendiente de auditoría de Chat 2
- **Base del paso:** `312cd0dcdab256ce5cdbf4b99011c62611fcccb2`
- **Fecha:** 7 de octubre de 2026 · America/Lima
- **Schema:** `service_orders`
- **Autoridad de DDL:** migraciones EF; `01_schema.sql` verifica paridad y no duplica `CREATE TABLE`

## 1. Clasificación física ADR-016/018

| Tabla | Replicable | PK | Sellos | FKs |
|---|---:|---|---|---|
| `service_orders` | sí | UUID v7 de aplicación | `origin_node`, `row_version`, `created_at`, `updated_at` | ninguna externa |
| `service_order_items` | sí | UUID v7 de aplicación | los cuatro | UUID → `service_orders` |
| `service_order_assignment_events` | sí | UUID v7 de aplicación | los cuatro | UUID → `service_orders` |
| `service_order_status_history` | sí | UUID v7 de aplicación | los cuatro | UUID → `service_orders` |
| `service_order_series` | no | `integer GENERATED ALWAYS AS IDENTITY` | fechas locales; sin `origin_node`/`row_version` | ninguna |

No existen FK a `services`, `crm`, `core.admin_users`, `core.media_assets` ni a la serie local. El
entero de M05a viaja con `service_source_node`; toda identidad de personal persiste nombre, ID local
y `HomeNode` sin FK. Las tres FK reales unen exclusivamente filas replicables.

## 2. Invariantes físicas

- vocabulario de cinco estados ratificados en orden e historial;
- contacto con nombre y al menos teléfono o correo;
- `promised_at IS NULL OR promised_at >= received_at`;
- triple actual/último actor completa o enteramente nula donde el sistema puede actuar;
- triples humanas obligatorias y con ID positivo en creación/asignación;
- actor de sistema como tres `NULL`; se rechaza el nombre ficticio literal `Sistema`;
- snapshot de línea con origen, nombre, slug, contenido y alt cuando conserva `MediaAssetId`;
- `quantity numeric(12,3) > 0`; precios `numeric(12,2)` nulos o no negativos;
- `sort_order >= 0` y único dentro de la orden;
- fila inicial de historial `NULL → received`; los estados distintos se protegen por vocabulario,
  mientras la máquina de transiciones permanece dentro de la operación autoritativa del Paso 3;
- `origin_node` no vacío y `row_version > 0` en cada fila replicable;
- serie local única por `(node_code, year)`, año de cuatro cifras y contador no negativo.

`created_at`/`updated_at` son obligatorios y un trigger propio mantiene `updated_at`. No se impone
`updated_at >= created_at`: PostgreSQL evalúa `now()` al inicio de cada transacción y esa comparación
rechaza actualizaciones concurrentes válidas cuando una transacción más antigua espera una fila
creada por otra más nueva. La prueba concurrente detectó y retiró esa barrera incorrecta.

## 3. Numeración común

`Sillar.Shared.Data.Numbering.TransactionalSeriesAllocator` contiene solo la infraestructura común:

- exige transacción abierta;
- calcula el año en America/Lima;
- valida identificadores SQL de la definición;
- ejecuta un único `INSERT ... ON CONFLICT ... DO UPDATE ... RETURNING` parametrizado;
- no conoce el formato visible ni configuración de ningún módulo.

M03 y M07 delegan ahora la reserva a esa primitiva, pero conservan sus formatos y configuración.
M05b posee `service_order_series`, usa su propio ajuste `service_orders.series_label` y nunca toca
`sales.order_series` ni importa dominio Sales. La reserva ocurre dentro de la transacción que
posteriormente creará la orden; rollback devuelve el número y concurrencia serializa la misma serie.

## 4. Migración, seed y desmontaje

- `ServiceOrdersInitial` comprueba CORE y `services.service_entries` antes de crear tablas.
- La migración crea las cinco tablas, constraints, índices y triggers, con historial local
  `service_orders.__migrations`.
- `01_schema.sql` es una barrera idempotente: nombra M05b y las tablas ausentes si migrate lo omitió.
- `02_seed.sql` acredita presencia y no inserta datos de negocio.
- `99_drop.sql` comprueba FK externas y dependientes duros registrados, elimina objetos propios en
  orden y termina con `DROP SCHEMA` sin `CASCADE`. El bloque completo es transaccional e idempotente.

La paridad temprana enumera M05b en el host/binario, la solución, la migración canónica y el seed
e2e. El ciclo completo `[M05B-CICLO]` sigue reservado para etapa 6: este paso no lo declara cumplido.

## 5. Contrato compilable para M06

`Sillar.Modules.ServiceOrders.Contracts` publica, sin EF ni SQL:

- estados con nombre, orden de presentación y terminalidad;
- transiciones legales como datos de lectura;
- consulta `ServiceOrderQuery` por abiertas/cerradas/todas, estado, orden/dirección y `PageRequest`;
- `PagedResult<ServiceOrderTrackingSummary>` de `Sillar.Shared`;
- summaries/detalle/historial y marca `UpdatedAt`;
- `ServiceOrderOutcome` + `ServiceOrderOperation<T>` propios (`Ok`, `NotFound`, `Invalid`, `Conflict`);
- `TransitionAsync(id, expectedStatus, targetStatus, ct)` sin reintento automático.

El contrato no publica notas de M06. La concurrencia `expectedStatus`, la atomicidad estado+historial
y la carrera de dos consumidores se implementan y ejecutan con la operación autoritativa en Paso 3;
este paso deja la firma compilable y la persistencia necesaria.

## 6. Evidencia exigida para cerrar esta candidata

La suite `Sillar.Modules.ServiceOrders.Tests` usa bases PostgreSQL 16 desechables y cubre migración,
catálogo físico, FK, clasificación, triples, actor de sistema, cantidad decimal, fecha prometida,
serie concurrente, rollback, seed, guardas y aislamiento de drop. También hay regresión focal sobre
M03 y M07 tras la extracción común.

Los sabotajes dirigidos deben observarse antes del commit final:

1. convertir el `CHECK` de triple de historial en `TRUE` pone roja la prueba de triple parcial;
2. cambiar el incremento común de `+ 1` a `+ 2` pone roja la prueba concurrente;
3. restaurar ambas implementaciones devuelve las suites a verde.

Ninguna prueba de Paso 2 sustituye `[M05B-CICLO]` ni autoriza UI antes de STOP 3.5.
