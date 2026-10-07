# Registro de evidencia para paralelización modular

- **Propósito:** ledger factual para diseñar, después de M08, un plan de cuatro agentes sobre módulos independientes.
- **Creación / última verificación:** 6 de octubre de 2026 · America/Lima
- **Base leída:** `d26f28a0439a9ac72dbedcc097dd8731b37a27c9`

Este archivo no decide el plan, no asigna agentes y no convierte coincidencia temporal en dependencia.
Registra únicamente esperas reales causadas por contratos o costuras compartidas.

## 2026-10-06 · M05b / M06

- **Qué tuvo que esperar:** M06 no puede cerrar su contrato ni comenzar una implementación estable hasta que M05b publique y ratifique su contrato público.
- **Contrato o recurso compartido causante:** `Sillar.Modules.ServiceOrders.Contracts`; identidad de orden, estado actual, líneas resumidas y asignación.
- **Archivos/costura afectados:** futura SPEC de M06, proyecto futuro `Sillar.Modules.Tracking`, FK replicable `tracking.service_status_history.service_order_id` y eventos/transiciones entre ambos módulos.
- **Consecuencia:** serialización de contrato: M05b define primero la frontera; M06 consume después sin leer dominio ni tablas internas de M05b.
- **Posible punto de desacople, sin decidirlo:** estabilizar primero un paquete Contracts versionado y fixtures contractuales para que M06 avance contra esa frontera, manteniendo la integración física para un turno coordinado.

## 2026-10-06 · M06 / M08

- **Qué tuvo que esperar:** JP decidió que M08 espere al cierre de M06 aunque M06 sea dependencia blanda de M08, porque abrir M08 ahora obligaría a reabrir su historial y superficies de seguimiento.
- **Contrato o recurso compartido causante:** contrato público futuro de seguimiento M06 y composición del historial del cliente en M08.
- **Archivos/costura afectados:** futura SPEC de M08, futuros `Sillar.Modules.Tracking.Contracts`, rutas/superficies del portal y pruebas de degradación con M06 activo/inactivo.
- **Consecuencia:** serialización de producto deliberada, no conversión de M06 en dependencia dura.
- **Posible punto de desacople, sin decidirlo:** diseñar M08 contra contribuciones versionadas y estados de ausencia, una vez observado el contrato real de M06.

## Regla para próximas entradas

Cuando M05b toque `migrate()`, `seed()`, setup, solución/host o pruebas compartidas de instalación,
se añadirá una entrada con el archivo exacto, propietario concurrente, espera ocurrida y efecto. En
Paso 1 todavía no ocurrió esa costura, por lo que no se inventa una tercera entrada.
