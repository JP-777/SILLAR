# Registro de evidencia para paralelización modular

- **Propósito:** ledger factual para diseñar, después de M08, un plan de cuatro agentes sobre módulos independientes.
- **Creación:** 6 de octubre de 2026 · America/Lima
- **Última verificación:** 7 de octubre de 2026 · America/Lima
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

## Regla para próximas entradas

Cuando M05b toque `migrate()`, `seed()`, setup, solución/host o pruebas compartidas de instalación,
se añadirá una entrada con archivo exacto, propietario concurrente, espera y efecto. Todavía no existe
un caso nuevo real de `RECURSO_PUERTA_EXCLUSIVA`; no se registra uno hipotético.
