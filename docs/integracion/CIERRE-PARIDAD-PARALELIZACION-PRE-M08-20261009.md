# SILLAR — Cierre pre-M08: paridad del arnés y registro factual de paralelización

- **Fecha:** 9 de octubre de 2026 · America/Lima.
- **Referencia integrada:** `main db07c22b3d66ba2d1ac29ed9a9168715c5a968af`.
- **Candidato funcional QA:** `92ec272a958c575c7a04b30d36cd4064a46b7a78`.
- **Carácter:** resumen de observaciones verificadas; no autoriza apertura de M08 ni decide el futuro plan de agentes.

## 1. Paridad binario / instalación / restauración

Inspección estática de `backend/Sillar.Api/Sillar.Api.csproj`, `e2e/setup/migrate.ts`,
`scripts/verificar.mjs` y `database/modules/*/02_seed.sql` sobre el árbol integrado.
En la columna «puerta» se registra la presencia en su inventario de migraciones,
no la equivalencia funcional de todos los caminos.

| Módulo | Binario | migrate() | seed() | Migraciones de la puerta |
|---|---|---|---|---|
| Core | Sí | Sí | `core` | Sí |
| M01 Catalog | Sí | Sí | `catalog` | Sí |
| M02 Cms | Sí | Sí | `cms` | Sí |
| M03 Sales | Sí | Sí | `sales` | Sí |
| M04 Crm | Sí | Sí | `crm` | Sí |
| M05a Services | Sí | Sí | `services` | Sí |
| M05b ServiceOrders | Sí | Sí | `service_orders` | Sí |
| M06 Tracking | Sí | Sí | `tracking` | Sí |
| M07 B2B | Sí | Sí | `b2b` | Sí |

**Resultado estático:** nueve módulos reales sobre nueve presentes en los cuatro
inventarios. `Sillar.Modules.Demo` solo está referenciado para Debug y no tiene
migraciones ni seed de dominio que sincronizar.

**Evidencia dinámica disponible sobre `92ec272`:** la puerta ejecutada sobre PostgreSQL
real pasó 6/6, con 782/782 backend y 180/180 e2e. `[M06-CICLO]` instala y activa
M05a, M05b y M06, comprueba la FK física de Tracking a ServiceOrders, desactiva
M06, ejecuta `tracking/99_drop.sql` dos veces, reconstruye con `migrate()+seed()`,
comprueba schema/tablas/datos ajenos y escritura posterior. También observa el seed
`service_orders.series_label`. La prueba cerró verde.

**Límite explícito:** esta evidencia no es una comparación estructural exhaustiva de
`POST /api/setup` con una desinstalación/reinstalación integral de **M05b** además
de M06; la prueba destructiva acredita directamente la restauración de M06 y la
conservación de M05b, mientras `SeriesConfigurationPostgresTests` acredita la
configuración/seed de M05b. No convertir esa diferencia de alcance en un PASS
no ejecutado. Una comprobación integral adicional, si se exige, será focal y en
una base exclusivamente efímera, nunca en desarrollo o producción.

**Observación documental:** comentarios antiguos de `e2e/setup/migrate.ts` y la salida
de `global-setup.ts` no enumeran ya todos los módulos, aunque las listas ejecutables
incluyen M05b y M06. El seed de M05b registra la configuración privada
`service_orders.series_label=PENDIENTE_DEFINIR`; no contiene órdenes de negocio.
No se modifica código en este registro.

## 2. Registro de paralelización tal como consta en la historia

Fuente canónica: `docs/integracion/REGISTRO-PARALELIZACION.md`. Cada fila resume una
entrada original con causa, espera y el posible requisito para no esperar; los
requisitos son hipótesis de desacople, **no decisiones de implementación**.

| Fecha / frentes | Causa | Espera o efecto observado | Qué habría hecho falta para no esperar |
|---|---|---|---|
| 06/10 M05b → M06 contrato base | `CONTRATO_INEXISTENTE` | M06 esperó la ratificación de Contracts. | Contrato versionado y fixtures públicos antes de los frentes consumidores. |
| 06/10 M06 → M08 | `DECISION_PENDIENTE` | JP ordenó esperar el cierre de M06. | Decisión de producto previa y contribuciones versionadas; no elimina una espera expresamente elegida. |
| 06/10 M05b/M06 estado e historial | `DECISION_PENDIENTE` | Datos de M05b esperó dueño del historial y semántica del bus. | Propiedad autoritativa fijada antes del diseño dependiente. |
| 07/10 M03/M05b numeración | `COSTURA_COMPARTIDA` | Primera migración y numerador M05b aguardaron extracción. | Primitiva de serie transaccional compartida, estable y sin dominio M03. |
| 07/10 M05b/M06 estados legibles | `CONTRATO_INEXISTENTE` | Tablero M06 esperó lectura de estados/transiciones legales. | Contrato del dueño que publique la máquina como dato de lectura. |
| 07/10 M05b/M06 transición concurrente | `DECISION_PENDIENTE` | Diseño del arrastre esperó semántica de conflicto. | Operación autoritativa que acepte estado/versión esperado y rechace vista obsoleta. |
| 07/10 M05b/M06 ledger compartido | `COSTURA_COMPARTIDA` | M06 redactó entradas fuera del ledger hasta convergencia de ramas. | Ledger publicado temprano en main y anexos independientes por frente. |
| 07/10 M05b/Integración instalación | `COSTURA_COMPARTIDA` | Host, solución, migrate y seed esperaron la definición de datos. | Manifiesto modular único o barrera automática de paridad. |
| 07/10 CMS/Services/ServiceOrders Outcome | `COSTURA_COMPARTIDA` | **Ninguna espera**, solo observación de repetición. | Evaluación posterior a M08 de una abstracción, solo con caso real. |
| 07/10 M06 prioridad lazy | `DECISION_PENDIENTE` | Migración esperó la semántica de ausencia de prioridad. | Nullabilidad y casos negativos fijados en SPEC antes del primer DDL. |
| 08–09/10 M05b/M06/Integración QA | `COSTURA_COMPARTIDA` | Certificación esperó la corrección de saturación PostgreSQL. | Presupuesto de concurrencia QA/aislamiento medido antes de abrir la puerta. |

**Recuento:** 11 entradas (5 costuras compartidas, 4 decisiones pendientes,
2 contratos inexistentes y 0 recursos de puerta exclusiva acreditados).
De las cinco costuras, una es observación sin espera real.

## 3. Seguimiento después de la fusión

Los tres pendientes del colíder tienen disparador operativo en `docs/PENDIENTES.md`
§§31–33: límite `-m:1` y revisión de infraestructura/pool; duración canónica
(umbral de 3 horas); y nomenclatura de pruebas en español desde los nuevos módulos.
No son defectos funcionales de M05b/M06 ni autorizan alterar retrospectivamente
el candidato QA. El orden de trabajo para M08 queda sujeto a la decisión de JP.
