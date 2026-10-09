# Registro de evidencia para paralelización modular

- **Propósito:** ledger factual para diseñar, después de M08, un plan de cuatro agentes sobre módulos independientes.
- **Creación:** 6 de octubre de 2026 · America/Lima
- **Última modificación parcial:** 9 de octubre de 2026 · America/Lima (entrada QA); **última revisión general:** 7 de octubre de 2026 · America/Lima
- **Base de la secuencia:** `d26f28a0439a9ac72dbedcc097dd8731b37a27c9`

Este archivo no decide el plan, no asigna agentes y no convierte coincidencia temporal en dependencia.
Registra únicamente esperas reales causadas por contratos, decisiones o costuras compartidas.

## 2026-10-06 · M05b / M06 — contrato público

- **Fecha:** 2026-10-06 — America/Lima.
- **Frentes:** M05b / M06.
- **Causa:** `CONTRATO_INEXISTENTE`.
- **Qué tuvo que esperar:** M06 no puede cerrar su contrato ni comenzar una implementación estable hasta que M05b publique y ratifique su contrato público.
- **Contrato o recurso compartido causante:** `Sillar.Modules.ServiceOrders.Contracts`; identidad, código, estado actual, fechas, líneas, responsable, historial y operación de transición autoritativa.
- **Archivos/costura afectados:** SPEC y futuro proyecto Contracts de M05b; futura SPEC y proyecto `Sillar.Modules.Tracking`.
- **Consecuencia:** serialización de contrato: M05b fija primero la frontera; M06 consume después sin leer dominio, entidades EF ni tablas internas.
- **Posible punto de desacople, sin decidirlo:** estabilizar un paquete Contracts versionado y fixtures contractuales para que M06 pueda avanzar contra la frontera publicada.

## 2026-10-06 · M06 / M08 — orden deliberado de producto

- **Fecha:** 2026-10-06 — America/Lima.
- **Frentes:** M06 / M08.
- **Causa:** `DECISION_PENDIENTE`.
- **Qué tuvo que esperar:** JP decidió que M08 espere al cierre de M06 aunque M06 sea dependencia blanda de M08, porque abrir M08 ahora obligaría a reabrir su historial y superficies de seguimiento.
- **Contrato o recurso compartido causante:** contrato público futuro de seguimiento M06 y composición del historial del cliente en M08.
- **Archivos/costura afectados:** futura SPEC de M08, futuros `Sillar.Modules.Tracking.Contracts`, rutas/superficies del portal y pruebas de degradación con M06 activo/inactivo.
- **Consecuencia:** serialización de producto deliberada, sin convertir M06 en dependencia dura.
- **Posible punto de desacople, sin decidirlo:** diseñar M08 contra contribuciones versionadas y estados de ausencia una vez observado el contrato real de M06.

## 2026-10-06 · M05b / M06 — propiedad de estado e historial

- **Fecha:** 2026-10-06 — America/Lima.
- **Frentes:** M05b / M06.
- **Causa:** `DECISION_PENDIENTE`.
- **Hecho:** la propiedad del estado/historial y el uso o no del bus estaban sin resolver.
- **Qué tuvo que esperar:** Paso 2 de M05b.
- **Duración:** aproximadamente un día desde la detección hasta la decisión.
- **Contrato o recurso compartido causante:** frontera autoritativa entre M05b y M06 y semántica de `InProcessEventBus`.
- **Archivos/costura afectados:** `docs/modules/service-orders/SPEC.md`, futuro modelo `service_orders.service_order_status_history` y futuro `Sillar.Modules.ServiceOrders.Contracts`.
- **Consecuencia:** la decisión cambió materialmente el modelo: nueva tabla de historial, transacción estado+historial, contrato de lectura/operación para M06 y bus fuera del camino autoritativo.
- **Posible punto de desacople, sin decidirlo:** cerrar contratos de propiedad de estado/historial antes de iniciar módulos consumidor/proveedor.

## 2026-10-07 · M03 / M05b — numeración transaccional

- **Fecha:** 2026-10-07 — America/Lima.
- **Frentes:** M03 / M05b.
- **Causa:** `COSTURA_COMPARTIDA`.
- **Hecho:** M05b necesita el mecanismo de numeración transaccional actualmente implementado dentro del frente M03/Sales.
- **Qué tuvo que esperar:** la primera migración y el numerador de código visible de M05b deben esperar la propuesta mínima de extracción/reutilización del Paso 2.
- **Contrato o recurso compartido causante:** algoritmo de reserva de serie, guardas transaccionales, año America/Lima, concurrencia y rollback sin hueco.
- **Archivos/costura afectados:** ubicación reusable futura, mecanismo actual `OrderSeries`/asignador de M03, modelo futuro de serie M05b y sus pruebas de concurrencia/rollback.
- **Consecuencia:** Paso 2 debe coordinar la reutilización común antes de implementar el código visible, sin duplicar algoritmo, importar dominio de Sales ni escribir en su schema.
- **Posible punto de desacople, sin decidirlo:** primitiva reusable y estable de numeración transaccional, independiente del módulo consumidor.

## 2026-10-07 · M05b / M06 — la máquina de estados como dato legible

- **Fecha:** 2026-10-07 — America/Lima.
- **Frentes:** M05b / M06.
- **Causa:** `CONTRATO_INEXISTENTE`.
- **Hecho:** el contrato ratificado de M05b (`312cd0dc` §6.2) publica estado actual, historial y la operación de transición, pero **no publica la lista de estados vigentes ni qué transiciones son legales desde cada uno**.
- **Qué tuvo que esperar:** el diseño del tablero de M06. Un tablero necesita saber qué agrupaciones pintar y qué arrastres ofrecer; sin esos dos datos, la única forma de pintarlo es escribir los estados y los movimientos dentro de M06, que es **una segunda máquina de estados** y exactamente lo que la frontera ratificada prohíbe.
- **Contrato o recurso compartido causante:** `Sillar.Modules.ServiceOrders.Contracts`; concretamente la ausencia de una lectura de la máquina de estados y de las transiciones permitidas.
- **Archivos/costura afectados:** `docs/modules/service-orders/SPEC.md` §6.2; `docs/modules/tracking/SPEC.md` §6.2 huecos C1 y C2, §9.1 y §12.
- **Consecuencia:** el Paso 1 de M06 se entrega **sin poder cerrarse**: las agrupaciones del tablero y la legalidad de los arrastres quedan marcadas `CONTRATO_PENDIENTE_DE_MATERIALIZAR`. M06 no propone la firma: la forma la decide M05b, que es el dueño de su máquina.
- **Posible punto de desacople, sin decidirlo:** que el contrato de un módulo dueño de una máquina de estados publique la máquina como **dato de lectura** y no solo la operación, para que cualquier consumidor pueda representarla sin duplicarla.

## 2026-10-07 · M05b / M06 — transición concurrente desde un tablero

- **Fecha:** 2026-10-07 — America/Lima.
- **Frentes:** M05b / M06.
- **Causa:** `DECISION_PENDIENTE`.
- **Hecho:** `TransitionAsync(serviceOrderId, toStatus, cancellationToken)` no recibe el estado que el llamante creía vigente. Dos personas con el tablero abierto ven `in_progress`; una arrastra a `ready` y la otra a `cancelled`. Las dos transiciones son legales por separado, así que M05b acepta las dos y **la segunda gana sin que nadie sepa que hubo conflicto**.
- **Qué tuvo que esperar:** el diseño del arrastre de M06 y sus criterios de aceptación de concurrencia.
- **Contrato o recurso compartido causante:** `IServiceOrderTransitions` y la semántica de concurrencia de la operación autoritativa.
- **Archivos/costura afectados:** `docs/modules/service-orders/SPEC.md` §6.2; `docs/modules/tracking/SPEC.md` §6.2 hueco C7 y §9.5.
- **Consecuencia:** M06 no puede ofrecer arrastre concurrente seguro, y **no lo resuelve reintentando**: reintentar sobre un estado que ya cambió repite el problema. Queda marcado y sin decidir; no se diseña una solución en M06 porque la operación es de M05b.
- **Posible punto de desacople, sin decidirlo:** que las operaciones autoritativas de transición acepten el estado esperado, de modo que un consumidor con vista desactualizada reciba un rechazo en vez de provocar un cambio que nadie pidió.

## 2026-10-07 · M05b / M06 — el ledger compartido vive en una rama y no en `main`

- **Fecha:** 2026-10-07 — America/Lima.
- **Frentes:** M05b / M06.
- **Causa:** `COSTURA_COMPARTIDA`.
- **Hecho:** `docs/integracion/REGISTRO-PARALELIZACION.md` existe en `m05b-service-orders-spec` y no en `main`. Dos frentes documentales en paralelo tienen que escribir en el mismo archivo desde ramas que no se ven.
- **Qué tuvo que esperar:** la actualización del ledger por parte de M06. No esperó al diseño: esperó a tener un sitio donde escribir sin producir dos copias divergentes.
- **Contrato o recurso compartido causante:** el propio archivo de ledger como recurso compartido entre frentes.
- **Archivos/costura afectados:** `docs/integracion/REGISTRO-PARALELIZACION.md`; `docs/modules/tracking/LEDGER-M06-PENDIENTE-DE-TRANSCRIBIR.md`.
- **Consecuencia:** las entradas de M06 se entregan transcribibles en lugar de aplicadas. **El registro no se pierde**, pero queda un paso manual al converger, y ese paso se puede olvidar.
- **Posible punto de desacople, sin decidirlo:** que el ledger llegue a `main` en cuanto exista, y que cada frente añada **solo entradas nuevas al final**, que es la forma de archivo compartido que se fusiona sin conflicto.

## 2026-10-07 · M05b / Integración — paridad binario, migrate y seed

- **Fecha:** 2026-10-07 — America/Lima.
- **Frentes:** M05b / Integración.
- **Causa:** `COSTURA_COMPARTIDA`.
- **Hecho:** publicar el proyecto de M05b no lo incorpora por sí solo a los dos caminos de instalación ni al seed vacío verificable.
- **Qué tuvo que esperar:** el cierre de Datos de M05b, hasta actualizar en conjunto el host, la solución, `scripts/verificar.mjs` y `e2e/setup/migrate.ts`.
- **Contrato o recurso compartido causante:** paridad `binario ↔ setup ↔ migrate ↔ seed`.
- **Archivos/costura afectados:** `backend/Sillar.Api/Sillar.Api.csproj`, `backend/Sillar.sln`, `scripts/verificar.mjs`, `e2e/setup/migrate.ts` y `database/modules/service_orders/02_seed.sql`.
- **Consecuencia:** estos inventarios compartidos deben avanzar juntos; omitir M05b de migrate o seed deja una instalación que conoce el módulo pero no puede materializarlo o acreditar su seed vacío.
- **Posible punto de desacople, sin decidirlo:** derivar el arnés de una única manifestación modular comprobable, sin diseñar todavía el plan posterior a M08.

## 2026-10-07 · CMS / Services / ServiceOrders — observación Operation/Outcome

- **Fecha:** 2026-10-07 — America/Lima.
- **Frentes:** M02 / M05a / M05b.
- **Causa:** `COSTURA_COMPARTIDA`.
- **Hecho:** CMS, Services y ServiceOrders aplican la misma forma `Outcome + Operation<T>`, pero cada módulo mantiene su tipo porque `CmsOutcome`/`CmsOperation<T>` y `ServiceOutcome`/`ServiceOperation<T>` son internos y hoy no existe una abstracción contractual compartida.
- **Qué tuvo que esperar:** nada; es una observación acumulada, no un bloqueo ni una autorización de refactorización.
- **Contrato o recurso compartido causante:** forma equivalente de resultados tipados en tres fronteras modulares.
- **Archivos/costura afectados:** `backend/Sillar.Modules.Cms/Services/CmsOperation.cs`, `backend/Sillar.Modules.Services/Services/ServiceOperation.cs` y `backend/Sillar.Modules.ServiceOrders.Contracts/ServiceOrderContracts.cs`.
- **Consecuencia:** M05b define tipos públicos propios y no referencia CMS/Services para reutilizar tipos internos; tampoco crea un `Result<T>` global durante este paso.
- **Posible punto de desacople, sin decidirlo:** después de M08, evaluar con la evidencia de tres implementaciones si existe una primitiva contractual reusable sin acoplar módulos ni alterar semánticas ya publicadas.

## 2026-10-07 · M06 — prioridad lazy antes del primer DDL

- **Fecha:** 2026-10-07 — America/Lima.
- **Frentes:** M06 / Paso 2 Datos.
- **Causa:** `DECISION_PENDIENTE`.
- **Hecho:** la SPEC ratificaba una fila `order_tracking` *lazy* para distinguir «sin prioridad asignada» de «prioridad cero», pero todavía describía `board_priority` como `integer NOT NULL`. Una fila puede materializarse solo por una nota o un plazo, sin que nadie haya decidido una prioridad.
- **Qué tuvo que esperar:** la primera migración y el primer `CREATE TABLE` de M06.
- **Contrato, recurso o decisión causante:** semántica de ausencia de prioridad manual en `tracking.order_tracking`.
- **Archivos/costura afectados:** `docs/modules/tracking/SPEC.md`, futuro modelo/configuración/migración de `tracking.order_tracking` y pruebas PostgreSQL de Paso 2.
- **Consecuencia:** la contradicción se detectó y resolvió antes de existir DDL o datos reales; no hace falta migración correctiva ni reinterpretar valores ya persistidos.
- **Resolución ratificada:** `board_priority integer NULL`; `NULL` significa que nadie fijó orden manual y el tablero usa `received_at`; entero `>= 0` significa prioridad manual explícita; `CHECK (board_priority IS NULL OR board_priority >= 0)`. Sin centinela `-1`, sin `0` como ausencia y sin booleano paralelo.
- **Prueba exigida:** materialización solo por nota conserva `NULL` y orden implícito; reordenar fija un entero y cambia el orden; prioridad negativa es rechazada físicamente por PostgreSQL.
- **Posible punto de desacople, sin decidirlo:** llevar decisiones de nulabilidad y semántica de ausencia a la SPEC antes de que otro frente materialice schema, para reducir esperas y migraciones correctivas posteriores.

## 2026-10-08–09 · M05b / M06 / Integración — infraestructura de la puerta compartida

- **Fecha:** 2026-10-08 y 2026-10-09 — America/Lima.
- **Frentes:** M05b + M06 / Integración / QA independiente.
- **Causa:** `COSTURA_COMPARTIDA`.
- **Hecho:** el runner canónico ejecutaba simultáneamente doce proyectos sobre un único servidor PostgreSQL de QA; las suites que crean y eliminan bases por prueba provocaron 81 fallos de conexión/timeout sobre `253286b`, sin fallo funcional focal. La prueba del candidato completo quedó bloqueada y `[M06-CICLO]` no se ejecutó en aquella corrida.
- **Qué tuvo que esperar:** certificación de M05b + M06 y, por dependencia deliberada del proceso, el pase a `main`.
- **Contrato o recurso compartido causante:** `scripts/verificar.mjs` y servidor PostgreSQL de QA; 147 bases efímeras durante la corrida roja y checkpoints forzados prolongados.
- **Archivos/costura afectados:** `scripts/verificar.mjs`, `backend/Sillar.sln`, `backend/Sillar.Modules.ServiceOrders.Tests/EphemeralDatabase.cs`, `backend/Sillar.Modules.Tracking.Tests/EphemeralDatabase.cs`.
- **Resolución observada:** commit `92ec272` añadió solo `-m:1` a `dotnet test`; se midieron cero solapamientos entre proyectos y un máximo de un testhost, con 782/782 backend y 180/180 e2e, cero omisiones. Evidencias en `db07c22` (rama QA documental).
- **Posible punto de desacople, sin decidirlo:** disponer de una capacidad/aislamiento de QA dimensionado para la rotación de bases y de presupuestos de concurrencia medidos, en lugar de dejar el paralelismo implícito. Revisar antes de relajar `-m:1`.
- **Clasificación deliberada:** no se asigna `RECURSO_PUERTA_EXCLUSIVA`: no se acreditó colisión de agentes por una puerta reservada, sino sobrecarga interna de la misma ejecución.

## Regla para próximas entradas

Cuando M05b toque `migrate()`, `seed()`, setup, solución/host o pruebas compartidas de instalación,
se añadirá una entrada con archivo exacto, propietario concurrente, espera y efecto. Todavía no existe
un caso nuevo real de `RECURSO_PUERTA_EXCLUSIVA`; no se registra uno hipotético.
