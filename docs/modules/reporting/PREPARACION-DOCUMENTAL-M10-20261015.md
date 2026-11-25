# M10 Reportes y Analítica — Preparación documental

- **Módulo:** M10 · **Código:** `reporting` · **Producto:** SILLAR WEB
- **Carácter:** PROPUESTA documental. **No es SPEC, no autoriza implementar, migrar ni abrir rama de código.**
  No declara M10 aprobado ni certificado.
- **Autor:** Claude C, bajo coordinación de Chat 3, por decisión de JP.
- **Fecha:** 15/10/2026 (America/Lima).
- **Base inspeccionada:** `origin/main` `58bb93a6f3021490a14c9144afd31fc7c02458a0`,
  tree `319b88aad723179c1f10a8351f755994170a6fe5`.
- **Rama / worktree:** `docs/m10-reporting-preimplementacion-20261015` en
  `/home/JP777/sillar-m10-docs-20261015`.
- **Expediente de arranque:** `SILLAR_ARRANQUE_CLAUDE_C_M10_20261015.tar.gz`, SHA-256
  `d2a8b088d4a064b05d079e794e1cb6bbc28c6b5da2b91680b4d321039fd9612d`; sus tres archivos verificados
  contra `SHA256SUMS.txt` (OK).

**Regla de lectura de este documento.** Todo lo que afirma sobre el repositorio cita
`ruta:línea` en la base `58bb93a6`. Lo que no puede citarse se marca **NO VERIFICADO** o
**PROPUESTA**. Ninguna propuesta de aquí es decisión de JP.

---

## 0. La discrepancia «SPEC V3.1»

El relevo habla de una «SPEC V3.1 de M10». Resultado de la búsqueda:

| Dónde se buscó | Resultado |
|---|---|
| Todas las refs locales y remotas (`refs/heads`, `refs/remotes`), árbol completo de cada una, rutas `*reporting*` / `*M10*` | **Ninguna** contiene `docs/modules/reporting/` |
| `git log --all -S'V3.1'`, `-S'SPEC V3'`, `-S'IReportingFactsReader'` | **Cero commits** |
| `git log --all --grep` por `m10` / `reporting` / `reportes` | Solo `58bb93a` (mensaje) y `4fe76b4` (CORE, ajeno) |
| `git stash list` | Vacío |
| Archivos `*.md` sin rastrear en los 15 worktrees listados por `git worktree list` | Ninguna mención a una SPEC M10 versionada |
| `~/Downloads` | `SILLAR_WEB_PAQUETE_ARRANQUE_M10_M11_M12_20261014.zip` (ver §1) y el propio expediente |

**Qué significa «V3» donde sí aparece.** El mensaje de `58bb93a` dice: «no incorpora adenda JP-02
ni **SPEC V3 M10/M12**». El mismo texto está en `~/Downloads/SILLAR_Docs_M08.sh:68`, que es el
script de reconciliación. Y `~/Downloads/SILLAR_ESTADO_Y_PUERTAS_DE_DELEGACION_20261014.md:24`
habla del «script V3» y en `:35` de «V3 reconciliado […] para los SPECs M10 y M12». **«V3» es la
versión del paquete/script de reconciliación, no un número de versión de la SPEC de M10.**

**Conclusión, con la evidencia disponible:**

1. La única SPEC de M10 localizable es la propuesta de Paso 1 del 14/10, SHA-256
   `87f40cb1…dfa8`. Existe en dos copias **byte a byte idénticas**: `01_SPEC_HISTORICA_M10_20261014.md`
   del expediente y `docs/modules/reporting/SPEC.md` dentro del zip del 14/10.
2. Lo más probable es que «V3.1» sea la forma informal de nombrar esa propuesta dentro del
   «V3 reconciliado». **Esto es una inferencia, no un hecho.**
3. No se puede descartar que exista una V3.1 fuera de lo accesible (otro equipo, otro chat). Si
   existe, este documento no la ha visto y **ningún contenido de aquí debe tomarse como reflejo
   de ella**. Se escala como D-M10-08.

No se ha creado ni copiado ninguna `SPEC.md`.

---

## 1. Inventario documental y trazabilidad

### 1.1 Fuentes históricas (fuera del repositorio)

| Fuente | Hash | Fecha / estado declarado | Ubicación | Observaciones |
|---|---|---|---|---|
| `01_SPEC_HISTORICA_M10_20261014.md` | SHA-256 `87f40cb146560c9905f4e037bc0ddd1cf2fbde8dfa4c25e0fb95f976af55dfa8` | 14/10/2026 · «PROPUESTA DE PASO 1 para ratificación de JP» (línea 4) | **histórico** (expediente) | Base de inspección declarada `4a3fd1e` (línea 7), anterior a la base actual |
| `docs/modules/reporting/SPEC.md` del zip 14/10 | SHA-256 `87f40cb1…dfa8` | Idéntico al anterior | **histórico** (`~/Downloads/SILLAR_WEB_PAQUETE_ARRANQUE_M10_M11_M12_20261014.zip`, SHA-256 `01d16c27…20a0`) | Duplicado exacto |
| `02_MATRIZ_HISTORICA_FRENTES_20261014.md` | SHA-256 `a584eef7ae074c7d37800442df87be1ccd78707d9debdb0720f1c9b6968c1704` | 14/10/2026 · «recomendaciones, NO ratificadas por silencio» (§C) | **histórico** | Idéntico a `coordinacion/MATRIZ_Y_DECISIONES.md` del zip. Fuente secundaria |
| `handoffs/CLAUDE_CODE_M10.md` del zip | SHA-256 `5132c7b4…0eb08` | 14/10/2026 · encargo de preparación | **histórico** | Pide leer `ANTES-DE-EMPEZAR-UN-MODULO.md` y `DIVISION-DE-TRABAJO.md`; no forman parte del alcance de este expediente |
| `README.md` del zip | SHA-256 `2dadfba8…1695` | 14/10/2026 | **histórico** | Línea 28: «Todo el esquema de datos […] `IReportingFactsReader` […] es propuesta» |
| `SILLAR_ESTADO_Y_PUERTAS_DE_DELEGACION_20261014.md` | SHA-256 `a40feb2e152c26a46a323a23d39ed02afdd315abf54ba3c21f20f907a693d81f` | 14/10/2026 · «no autoriza código» (línea 4) | **histórico** (`~/Downloads`) | Puertas M10 en `:40-44`; recuerda que no existe `Sillar.Modules.B2B.Contracts` (`:42`) |
| Una «SPEC V3.1» de M10 | — | — | **no localizado** | Ver §0 |

### 1.2 Fuentes canónicas en `main` `58bb93a6`

Blob = `git rev-parse 58bb93a:<ruta>`, primeros 12 caracteres.

| Ruta | Blob | Relevancia para M10 |
|---|---|---|
| `docs/ARQUITECTURA_MODULAR.md` | `f43e1502a195` | `:66` M10 «consume eventos»; `:120` «construida sobre eventos»; `:341` «se alimenta de ahí [bus], nunca de las tablas ajenas» |
| `docs/ROADMAP_MODULAR.md` | `0b207a30104c` | `:124` «Analítica sobre eventos publicados por los demás módulos» |
| `docs/PENDIENTES.md` | `2555da639ade` | §29 `:528-560` (bus no garantiza entrega; disparador «cualquier garantía futura de M10», `:549`); §34 `:642-667` (Infra QA, decisión JP pendiente antes de abrir M10, `:663`); §35 `:671` |
| `docs/adr/ADR-016-identificadores-replicables.md` | `7e86acb04791` | `:48` uuid v7 si replica, identity si no; tabla `:54-60` |
| `docs/adr/ADR-017-mando-y-respaldo.md` | `6bf912731eb4` | ERP aparcado; mando y copia, no satélites con proyección |
| `docs/adr/ADR-018-medios-replicables.md` | `5f19d0243e86` | Replicada no referencia a no replicada (resumido en `CLAUDE.md:106`) |
| `docs/modules/_PLANTILLA_SPEC.md` | `f0296582bd50` | Estructura §1–§10 que deberá seguir la futura SPEC |
| `backend/Sillar.Shared/Events/IEventPublisher.cs` | `9693f935992f` | `:15-20` «ni cola, ni reintentos, ni orden garantizado» |
| `backend/Sillar.Shared/Events/InProcessEventBus.cs` | `6a6d99dba15b` | `:10-12` sin cola ni reintentos; `:27-31` sin manejadores = se descarta; `:39-49` excepción de manejador = log y sigue |
| `backend/Sillar.Modules.Sales.Contracts/Events/SalesEvents.cs` | `c35c802d4ce9` | `PedidoCreado` `:8-12`, `PagoConfirmado` `:25-28`, `PlazoDePagoVencido` `:42-45` — **declarados, no publicados** (§3.1) |
| `backend/Sillar.Modules.Sales.Contracts/ICustomerOrderHistory.cs` | `1b723f6156ab` | Lectura **por cliente**, `:42-45`; DTO `:64-70` |
| `backend/Sillar.Modules.Sales/Domain/OrderPayment.cs` | `11d080c89233` | Hecho de cobro: `Amount` `:47`, `RegisteredAt` `:99`, replicado `:27`, `:112-115` |
| `backend/Sillar.Modules.ServiceOrders.Contracts/ServiceOrderContracts.cs` | `3f6601c05960` | `IServiceOrderTrackingSource` `:66-75`; resumen con `CustomerName` `:87-95` |
| `backend/Sillar.Modules.ServiceOrders.Contracts/ICustomerServiceOrderReader.cs` | `06dc0a8428e9` | Lectura por cliente `:12-19`; único cambio en contratos desde `4a3fd1e` |
| `backend/Sillar.Modules.B2B/Domain/Entidades.cs` | `b20bd9f9a5a6` | `Quote` con `TotalAmount`, `PaidAt` `:47-60`; estados `:127-144` — **dominio interno, no contrato** |
| `backend/Sillar.Core/Services/ModuleActivationService.cs` | `f50fda0b1fa9` | `:246-248` publica activación/desactivación como `PublishAsync<object>` (§3.4) |
| `docs/modules/sales/SPEC.md` | `6156206db795` | §6.4 `:587-594` eventos publicados «nombres técnicos definitivos pertenecen a la implementación» |
| `docs/modules/service-orders/SPEC.md` | `ff8ae71bd9e0` | `:53` eventos = avisos; §6.3 `:495-511`, `:508` «no se excluye un futuro M10 si su contrato permite reconstrucción» |
| `docs/modules/b2b/SPEC.md` | `6bd32ec53c66` | `:137-139` ninguna tabla replica; §5 `:405-433` `IB2BService` especificado; `:453` ningún evento publicado |
| `docs/modules/payments/SPEC.md` | `0fd11be9e042` | `:61` «M10: ninguna hacia M10, consumidor futuro» |
| `docs/modules/reporting/` | — | **No existe en `main`** antes de este documento |

### 1.3 Deriva entre la base histórica y la actual

`4a3fd1e` es ancestro de `58bb93a` (28 commits). En las rutas relevantes para M10
(`Sillar.Shared/Events`, `Sales.Contracts`, `ServiceOrders.Contracts`, `Sillar.Modules.B2B`,
`ARQUITECTURA_MODULAR.md`, `ROADMAP_MODULAR.md`) el único cambio es **la adición de
`ICustomerServiceOrderReader.cs` (+40)**. `PENDIENTES.md` ganó §34 y §35 y cerró §25. El candidato
M08 `44e3b1f` que la propuesta citaba como «no certificado» es hoy ancestro de `main`.

### 1.4 Contradicciones detectadas

| # | Entre | Contradicción |
|---|---|---|
| C1 | `ARQUITECTURA_MODULAR.md:66,120,341` y `ROADMAP_MODULAR.md:124` ↔ `PENDIENTES.md:528-560` | La arquitectura dice que M10 se alimenta del bus; §29 dice que el bus «nunca es el camino por el que viaja un hecho que alguien necesita». **Las dos son canónicas.** La propuesta histórica se alinea con §29. Corregir la arquitectura es de Chat 2 + JP (matriz histórica §B, fila de docs) |
| C2 | `ARQUITECTURA_MODULAR.md:341` ↔ código | Aunque se quisiera alimentar M10 del bus, **M03 no publica ningún evento** (§3.1). No hay qué consumir |
| C3 | Propuesta histórica §3.1 ↔ código | Dice que `PedidoCreado`/`PagoConfirmado` «no incluyen las líneas o importes». Es cierto (`SalesEvents.cs:8-12,25-28`), pero **el problema previo es que no se emiten** |
| C4 | Propuesta §7 «fechas America/Lima» ↔ regla de producto | No es contradicción: la zona es constante de plataforma, `Sillar.Shared.Data/Numbering/TransactionalSeriesAllocator.cs:19`. Se registra para que nadie la convierta en configuración de cliente sin decisión |
| C5 | Encargo 14/10 (`handoffs/CLAUDE_CODE_M10.md`) ↔ expediente 15/10 | El 14/10 asignaba M10 a «Claude Code» con rama `feat/m10-reporting`; el 15/10 asigna el frente documental a Claude C, rama `docs/…`, DOCS-ONLY. **Prevalece el expediente del 15/10** por ser posterior y por instrucción expresa |

---

## 2. Brechas de la fuente histórica contra `58bb93a6`

### 2.1 Hechos obsoletos o imprecisos

| Ubicación en la propuesta | Qué dice | Qué hay en `58bb93a6` |
|---|---|---|
| línea 7 | Base `4a3fd1e`, M08 «NO certificado» | Base actual `58bb93a6`; M08 integrado en contenido (`docs/integracion/CIERRE-M08-CERTIFICADO-EN-CONTENIDO-20261015.md`), Infra QA §34 abierto |
| línea 8, §0 | «`PENDIENTES.md` §29» | Sigue siendo §29 (`PENDIENTES.md:528`) — **vigente**, sin renumerar |
| §3.1 | Eventos Sales «no incluyen líneas o importes» | Además **no se publican**: `grep PublishAsync` en `backend/Sillar.Modules.Sales` → 0 resultados; solo Catalog y Core publican (`Sillar.Modules.Catalog/Services/ProductService.cs:368,463,500,556`, `ProductItemService.cs:132-261`, `CategoryService.cs:352`, `Sillar.Core/Services/SiteSettingService.cs:160`, `ModuleActivationService.cs:246`) |
| §3 tabla, M07 | «solicitudes y cotizaciones bajo DTOs agregables» | **No existe proyecto `Sillar.Modules.B2B.Contracts`**: `ls -d backend/Sillar.Modules.*` no lo lista y `IB2BService` no aparece en ningún `.cs`. La SPEC de M07 lo especifica (`b2b/SPEC.md:405-433`) pero no está construido |
| §2, M07 | «conversión solamente con relación de origen comprobable» | Dentro de M07 **sí existe** relación comprobable: una cotización nace de exactamente una solicitud (`Entidades.cs:46`, `ck_quotes_origen`). La propuesta no la aprovecha |
| §2 / §8.1 | Cobros solo desde M03 | **M07 también registra cobros**: `Quote.PaidAt`, `PaymentMethod`, `PaymentReference` (`Entidades.cs:59-61`), estado `pagada` (`:142`). La propuesta no los contempla |
| §3 tabla, M05b | «hechos de órdenes de servicio» | Hay contrato durable (`IServiceOrderTrackingSource`, `ServiceOrderContracts.cs:66-75`) registrado en DI (`ServiceOrdersModule.cs:58`), pero su resumen **incluye `CustomerName`** (`:90`) y pagina por **offset** (`Sillar.Shared/Paging/PageRequest.cs:35`), no por cursor |
| §8.5 | «admin/super_admin; editor no ve totales» | Los tres roles existen (`Sillar.Core.Contracts/AdminRole.cs:16-25`); no hay permisos por módulo («se difieren», `:5-6`). La regla es implementable sin cambiar CORE |

### 2.2 Decisiones o hechos de la propuesta que siguen válidos

- M10 no lee schemas ajenos ni declara FK cruzadas (coincide con `ARQUITECTURA_MODULAR.md:341` en
  la parte «nunca de las tablas ajenas» y con `CLAUDE.md:65`).
- M10 no es dependencia de nadie (`payments/SPEC.md:61`; no hay `HardDependencies` hacia
  `reporting` en ningún módulo — no existe el código `reporting` en el backend).
- El bus no es garantía (`IEventPublisher.cs:15-20`, `InProcessEventBus.cs:10-12`, `PENDIENTES.md:544`).
- `IReportingFactsReader` **no existe** (búsqueda en `backend/` y `docs/` sin resultados; ningún
  commit en `--all` lo introdujo).
- Pedido ≠ cobro: `PagoConfirmado` «es el hecho de pago, no el estado del pedido» (`SalesEvents.cs:17-20`);
  `Expired` no es `Cancelled` y admite pago posterior (`OrderStatus.cs:21-24`).
- Distinguir ausente / sin datos / desactualizado / error (propuesta §8.2) — sigue siendo la única
  forma de cumplir `CLAUDE.md:69` («degrada sin fallar») sin mostrar ceros falsos.
- Seed vacío; nada de datos demo (coherente con `b2b/SPEC.md:398`, mismo criterio).
- CSV con neutralización de fórmulas; sin PII; sin identificadores en UI (`CLAUDE.md:112`).

### 2.3 Propuestas no ratificadas (siguen siendo propuestas)

| Propuesta | Estado | Bloquea |
|---|---|---|
| Eventos = invalidación; lecturas paginadas/reanudables de los dueños = verdad (§0, D-M10-01) | **NO ratificada.** Requiere contratos nuevos en módulos cerrados → **PENDIENTE JP + Chat 2** (expediente §8) | Ingesta |
| `IReportingFactsReader` genérico + `ReportableFact` (§5) | NO ratificado; la propia propuesta dice «NO copiar en `Sillar.Shared`» | Contratos |
| Tablas `source_checkpoints`, `fact_projection`, `saved_views` (§4) | NO ratificadas; dependen de D-M10-02 (¿hay proyección?) | Migración |
| Endpoints `/api/reporting/*` (§6) | NO ratificados; Paso 3 | API |
| Ruta `/admin/reportes`, tres superficies (§7) | NO ratificado; STOP 3.5 | UI |
| Roles y rangos máximos (§8.5, D-M10-03) | NO ratificados | Endpoints |
| D01–D08 de la matriz histórica | «NO ratificadas por silencio» (matriz §C); solo D01 (M18) expresada por JP | — |

---

## 3. Matriz de fuentes de datos, eventos y contratos

### 3.1 Qué publica realmente el bus hoy

| Evento | Declarado en | ¿Se publica? | Consumidores | Útil para M10 |
|---|---|---|---|---|
| `PedidoCreado`, `PagoConfirmado`, `PlazoDePagoVencido` | `SalesEvents.cs:8,25,42` | **No.** 0 llamadas en `Sillar.Modules.Sales` | Ninguno | No hay nada que escuchar |
| `ServiceOrderCreated/StatusChanged/AssignmentChanged` | Solo en `service-orders/SPEC.md:503-505` como «permitidos» | **No existen en código** (0 coincidencias en `backend/`) | — | — |
| Eventos de M07 | — | «ninguno en la 1.0.0» (`b2b/SPEC.md:453`) | — | — |
| `ProductoCreado/Actualizado/Desactivado`, `VarianteCreada/Desactivada`, `CategoriaDesactivada` | Catalog | Sí (citas en §2.1) | M02 (`CmsModule.cs:72-74`), M07 (`B2BModule.cs:79-80`) | Solo dimensiones; no son hechos comerciales |
| `ModuleActivated` / `ModuleDeactivated` | `Sillar.Core.Contracts/Events/CoreEvents.cs:11,16` | Sí, pero como `PublishAsync<object>` (`ModuleActivationService.cs:246`) | Ninguno | Ver §3.4 |
| `SettingChanged` | `CoreEvents.cs:27` | Sí (`SiteSettingService.cs:160`) | — | No |

**Consecuencia:** la frase «M10 consume eventos» de la arquitectura no tiene hoy ningún evento
comercial detrás. Cualquier camino exige una costura nueva en el módulo dueño.

### 3.2 Matriz por fuente

| Fuente | Dueño | Dato que M10 necesitaría | Contrato / evento existente | Faltante | Durable | Replicación del dato en el dueño | Si el dueño está ausente o desactivado | Riesgo de ownership |
|---|---|---|---|---|---|---|---|---|
| **M03 pedidos** | M03 `sales` | Pedidos por período: fecha, estado, `total_amount`, nº de líneas | `ICustomerOrderHistory` (`ICustomerOrderHistory.cs:25-45`) — **por cliente** y con `limit` acotado | Lectura **por período** de todos los pedidos, con cursor o agregado | Sí (lee la tabla) | Replica: uuid v7 + `origin_node` (`sales/SPEC.md:280`) | Contrato no registrado → `null` desde DI (se registra en `SalesModule.cs:105`, y el host «registra únicamente los habilitados», `ARQUITECTURA_MODULAR.md:334`; comportamiento en runtime **NO VERIFICADO** en este hito) → «módulo no disponible» | **Alto** si se intenta reconstruir recorriendo clientes: obliga a enumerar M04 (PII) y es N+1. **Rechazado** |
| **M03 cobros** | M03 `sales` | Pagos registrados: fecha, método, importe, si fue tardío | Ninguno. `OrderPayment` es dominio interno (`OrderPayment.cs:27-121`) | Contrato de lectura de hechos de cobro | Sí (fila) | Replica (`OrderPayment.cs:27` `IReplicatedEntity`, `:112-115`) | Igual que arriba | Alto: el registro lleva atribución humana (`:59-96`); M10 no debe recibirla. Además la **adenda JP-02** (`source=staff|provider`, expediente de puertas `:48`) **no está en `main`** y cambiará este modelo |
| **M05b órdenes** | M05b `service_orders` | Órdenes por estado/período; plazo cumplido (`promised_at` vs transición a `completed`) | `IServiceOrderTrackingSource.ListAsync` / `GetAsync` (`ServiceOrderContracts.cs:66-75`), registrado (`ServiceOrdersModule.cs:58`) | Lectura sin PII; historial sin N+1; paginación estable | Sí | Replica: orden, líneas e historial (`service-orders/SPEC.md:157-162`) | Contrato ausente → «no disponible» | **Medio**: el resumen trae `CustomerName` (`:90`) y `StaffSnapshot` (`:94`). Usarlo obliga a M10 a recibir PII que debe descartar. El historial solo viene por orden (`GetAsync`, `:106`). Paginación por offset (`PageRequest.cs:35`) sobre datos que cambian puede **saltar o repetir** filas al ordenar por `UpdatedAt` (`:46`) |
| **M05b importes** | M05b | Importe acordado por orden | `ServiceOrderWorkItemSnapshot` sin precio (`:109-114`) | Contrato con importe | — | Replica | — | El total puede ser «pendiente», no cero (`service-orders/SPEC.md:234`, `:258`). M10 no debe sumar nulos como cero |
| **M07 solicitudes y cotizaciones** | M07 `b2b` | Solicitudes por tipo/estado; cotizaciones por estado; conversión solicitud→cotización→pagada; cobros B2B | **Ninguno construido.** `IB2BService` especificado (`b2b/SPEC.md:405-433`) pero sin proyecto `Contracts`; además es **por cliente** | Proyecto Contracts + lectura agregada por período | Sí | **No replica**, integer identity (`b2b/SPEC.md:137-139`) | No hay contrato que pedir; M10 no tendría fuente | Alto: `StaffNotes` (`Entidades.cs:19,40`) e institución/persona de contacto (`:33-35`) no pueden salir. `b2b/SPEC.md` exige afirmarlo con prueba |
| **M01 catálogo** | M01 | Dimensiones (nombre de producto) para desgloses | `Catalog.Contracts` (fuera de alcance de este inventario) | — | — | Replica | Desglose por producto se omite | Bajo, pero **nunca revalorar** pedidos con precios actuales (propuesta §3; `CLAUDE.md:119`) |
| **CORE** | CORE | Rol del usuario, módulo activo, auditoría | `AdminRole` (`AdminRole.cs:13-26`), `IModuleRegistry.IsActive` (`IModuleRegistry.cs:48`), `IAuditWriter` (`IAuditWriter.cs:33-36`) | — | Sí | No replica (`CLAUDE.md:104`) | CORE siempre está | Ninguno |
| **M04, M06, M08, M11, M12** | — | — | — | Fuera de v1 (propuesta §3) | — | — | — | M04 sería **puro riesgo de PII** para un módulo de agregados |

### 3.3 Lo que esta matriz implica

1. **Hoy M10 no tiene ninguna fuente utilizable sin tocar un módulo dueño.** M05b es la más cercana
   y aun así entrega PII y pagina por offset.
2. Cada fuente necesita **un contrato nuevo, propiedad de su dueño**. Eso es exactamente lo que el
   expediente §8 deja **PENDIENTE JP + Chat 2**. Este documento no lo da por hecho.
3. Las tres fuentes viven en módulos ya cerrados. Ninguna costura se pide aquí; se describen para
   que JP decida el orden (D-M10-03).

### 3.4 Observación lateral para Chat 2 (no es trabajo de M10)

`ModuleActivationService.cs:246` publica con `PublishAsync<object>(…)`, e
`InProcessEventBus.cs:25` resuelve `IEventHandler<TEvent>` con el tipo genérico **de la llamada**,
es decir `IEventHandler<object>`. Un manejador registrado como `IEventHandler<ModuleDeactivated>`
**no sería invocado**. Hoy no hay ninguno (búsqueda sin resultados), así que no rompe nada; pero
M10 **no debe** basar la detección de «fuente desactivada» en ese evento. Además el host se
reinicia tras activar (`CoreEvents.cs:5-7`). La detección correcta es en el momento de leer:
contrato presente en DI o `IModuleRegistry.IsActive`. **No se propone corregir nada; se registra.**

---

## 4. Modelo conceptual de reportes

### 4.1 Vocabulario que M10 no puede mezclar

| Término | Qué es | De dónde saldría | Qué **no** es |
|---|---|---|---|
| **Pedido creado** | Un pedido de M03 nació en ese período | Fecha de creación del pedido | No es venta ni cobro: puede vencer (`OrderStatus.cs:44-47`) o cancelarse (`:49-50`) |
| **Valor de pedidos** | Suma de `total_amount` de los pedidos de un conjunto | `total_amount` = suma de líneas y nada más (`sales/SPEC.md:346`) | **No es ingreso.** Incluye impagos |
| **Cobro registrado** | Una persona registró un pago contra un pedido o una cotización | `OrderPayment.Amount` + `RegisteredAt` (`OrderPayment.cs:47,99`); `Quote.PaidAt` (`Entidades.cs:59`) | No es «ingreso contable» ni comprobante: no hay modelo de reverso/devolución en `OrderPayment.cs:27-121`, y SUNAT está fuera de alcance |
| **Ingreso** | — | — | **M10 v1 no usa esta palabra.** Recomendación: no existe dato que la sostenga (D-M10-04) |
| **Conversión B2B** | Proporción solicitud → cotización → aprobada → pagada | Relación interna de M07 (`Entidades.cs:46,52-53`) | No es «solicitud B2B que acabó en pedido M03»: no existe vínculo entre M07 y M03 |
| **Orden de servicio por estado** | Recuento por estado vigente de M05b | `ServiceOrderStatuses` (`ServiceOrderContracts.cs:6-35`) | No incluye importes si el precio está pendiente |
| **Plazo cumplido** | Orden completada en o antes de `promised_at` | `PromisedAt` + historial (`:103,106,121-127`) | Si `promised_at` es nulo, la orden **no cuenta** ni como cumplida ni como incumplida |

### 4.2 Indicadores defendibles (si existe la fuente)

| Indicador | Fuente | Defendible porque | Condición |
|---|---|---|---|
| Nº de pedidos creados por período y estado actual | M03 | Fecha de creación inmutable; estado del dueño | Mostrar que el estado es **el actual**, no el del período |
| Valor de pedidos por período, desglosado por estado | M03 | `total_amount` congelado | Etiqueta explícita: «valor de pedidos», nunca «ventas» ni «ingresos» |
| Cobros registrados por período y método | M03 | Hecho de pago con fecha propia | Solo `yape` y `efectivo` en v1 (`PaymentMethod.cs:44`); depende de JP-02 |
| Cobros B2B registrados por período | M07 | `PaidAt` + `TotalAmount` | Serie separada de M03; no sumar sin decisión (D-M10-04) |
| Pedidos vencidos (no cancelados) | M03 | `expired` es estado propio | Recordar que pueden pagarse después (`OrderStatus.cs:21-24`) |
| Órdenes de servicio por estado | M05b | Estado autoritativo de M05b | Sin nombres de cliente |
| Cumplimiento de plazo | M05b | Historial autoritativo | Excluye órdenes sin `promised_at` y lo dice |
| Embudo B2B | M07 | `ck_quotes_origen` | Requiere contrato M07 |

### 4.3 Indicadores **no** defendibles hoy

- «Ingresos», «facturación», «ventas netas»: no hay reversos ni comprobantes.
- Ticket medio sobre pedidos impagos presentado como venta.
- Conversión visita → pedido: no existe fuente de visitas.
- Conversión B2B → M03: sin vínculo.
- Cualquier cifra recalculada con precio actual de M01.

### 4.4 Estados de una cifra

Cada tarjeta o celda tiene **uno** de estos estados, y nunca se colapsa en `0`:

| Estado | Cuándo | Qué ve el usuario (propuesta de texto) |
|---|---|---|
| **No disponible** | Contrato del dueño ausente en DI o módulo inactivo | «Pedidos no está instalado. Este reporte aparecerá cuando lo esté.» |
| **Sin datos** | Fuente disponible y respondió que no hay hechos en el rango | «No hubo pedidos entre el 1 y el 15 de octubre.» |
| **Desactualizado** | Solo si existe proyección (D-M10-02) y su última lectura completa es anterior a un umbral | «Datos hasta el 14/10 18:00. Actualizar.» |
| **Error de fuente** | La fuente falló al leer | «No se pudo leer Pedidos. Reintenta en unos minutos; el resto del reporte sí está al día.» |
| **Pendiente** | El dato existe pero el dueño lo marca incompleto (precio por acordar en M05b) | «3 órdenes con precio por acordar no están sumadas.» |

Textos sujetos a STOP 3.5 y a la regla de `CLAUDE.md` sobre mensajes (sin «Ha ocurrido un error»).

---

## 5. Permisos, privacidad y minimización

### 5.1 Roles

Existen tres (`AdminRole.cs:16-22`) y no hay permisos por módulo (`:5-6`). **PROPUESTA**, a decidir
en D-M10-05:

| Acción | `super_admin` | `admin` | `editor` |
|---|---|---|---|
| Ver resumen operativo (recuentos por estado) | Sí | Sí | Sí / No — **decisión JP** |
| Ver importes (valor de pedidos, cobros) | Sí | Sí | No |
| Exportar CSV | Sí | Sí | No |
| Forzar actualización (solo si hay proyección) | Sí | No | No |

Ningún endpoint de M10 es público ni del portal de cliente.

### 5.2 Agregados frente a PII

- **Regla de frontera, no de pantalla:** M10 no debe **recibir** PII, no basta con no mostrarla.
  Si un contrato existente la trae (`ServiceOrderTrackingSummary.CustomerName`, `:90`), la
  recomendación es un contrato propio sin ese campo, no un filtro en M10.
- Nada de nombres de cliente, documento, correo, teléfono, dirección, institución, persona de
  contacto, notas de personal ni nombre del trabajador que registró un pago.
- Dimensiones admitidas (propuesta): fecha, estado, método de pago, tipo de solicitud B2B, nombre
  de producto/servicio **congelado por el dueño**, `origin_node` solo cuando exista más de un nodo.
- Códigos visibles (`P-2026-0147`, `C-2026-0147`) **no** aparecen en reportes agregados. Si algún
  día hay detalle por pedido, es otra decisión (identifica a un cliente indirectamente).
- Umbral de anonimato para agregados pequeños: **NO VERIFICADO** que haga falta en un negocio de
  una sola tienda; se deja como pregunta abierta en D-M10-05.

### 5.3 Exportaciones

- CSV de **exactamente** la vista filtrada, mismos totales que la pantalla.
- Neutralizar celdas que empiezan por `=`, `+`, `-`, `@`, tabulador o retorno de carro.
- Encabezados en español; fechas con zona explícita (`America/Lima`, constante de plataforma,
  `TransactionalSeriesAllocator.cs:19`).
- Rango máximo y número máximo de filas impuestos en servidor.
- PDF y gráficos exportables fuera de v1 (propuesta §2).

### 5.4 Auditoría

- Cada exportación se audita con `IAuditWriter` (`IAuditWriter.cs:33-36`): quién, cuándo, tipo de
  reporte y rango. **Nunca** el contenido exportado.
- La auditoría es local al nodo (`CLAUDE.md:104`); no se replica.
- Ver un reporte no se audita (propuesta; evita ruido). Decisión en D-M10-05.

---

## 6. Matriz de aceptación documental

Criterios **propuestos**, en lenguaje de prueba. Ninguno está implementado ni ejecutado. La
columna «Nivel» indica dónde se probaría: **L** lógica sin BD, **P** PostgreSQL real, **H** HTTP,
**E** e2e.

| # | Escenario | Dado | Entonces | Nivel |
|---|---|---|---|---|
| A01 | Fuente disponible | M03 activo, su contrato de reporte registrado, 3 pedidos en el rango | Recuento = 3; tarjeta indica fuente, rango y momento de lectura | P, H |
| A02 | Fuente ausente (no instalada) | M03 no montado; el contrato no está en DI | 200 con estado «no disponible»; **ninguna** cifra `0`; M10 arranca | L, H |
| A03 | Fuente desactivada después de haber datos | M03 desactivado en `core.modules` | Mismo resultado que A02. Si hay proyección: se muestra «no disponible», no los datos viejos como si fueran actuales | H, E |
| A04 | Todas las fuentes ausentes | Instalación con solo CORE + M10 | Pantalla explica qué módulos alimentan los reportes; sin enlaces rotos ni rutas muertas | E |
| A05 | Evento perdido | Un manejador del bus lanza o no está registrado al ocurrir un pago | El cobro aparece igual en el reporte (la verdad no depende del bus) | P |
| A06 | Corte a mitad de lectura | Proceso interrumpido tras la página *k* | Al reanudar no se duplican ni omiten hechos; totales = fuente | P |
| A07 | Reconstrucción desde cero | Proyección vaciada | Reconstruir produce los mismos totales que la fuente | P |
| A08 | Hecho corregido por el dueño | Pedido pasa de `pending_payment` a `cancelled` | El reporte refleja el estado nuevo sin contar dos veces | P |
| A09 | Paginación estable | Filas que cambian `UpdatedAt` durante la lectura | Ninguna fila saltada ni repetida (falla con offset puro: caso rojo esperado contra `PageRequest.Skip`) | P |
| A10 | Rango sin datos | Fuente disponible, rango sin hechos | «Sin datos» explícito, distinto de A02 | H |
| A11 | Rango inválido | `from > to`, rango mayor que el máximo, fecha mal formada | 400 con frase que dice qué corregir | H |
| A12 | Límite de zona horaria | Pedido a las 23:30 del 31/10 hora Lima | Cae en octubre, no en noviembre | L, P |
| A13 | Pedido vencido | Pedido `expired` sin pago | Aparece en «valor de pedidos» con su estado; **no** en cobros | P |
| A14 | Pago tardío | Pago registrado sobre pedido `expired` (`OrderPayment.WasLate`, `:109`) | Cuenta como cobro en la fecha del pago, no del pedido | P |
| A15 | Precio pendiente M05b | Orden con `agreed_unit_price` nulo | No suma `0`; la tarjeta indica cuántas quedaron fuera | P |
| A16 | Dato replicado | Hechos con `origin_node` distinto (escenario futuro) | Se cuentan una vez por clave del dueño; `origin_node` es dimensión, no clave | P |
| A17 | Dato no replicado | Cotización M07 (identity local) | La clave del hecho incluye el nodo o se declara local; no se mezcla con claves uuid | L |
| A18 | Proyección local | Tablas de M10 | Ninguna es replicable; ninguna FK hacia otro schema (`CLAUDE.md:65,106`) | P |
| A19 | Sin sesión | Petición sin cookie | 401 | H |
| A20 | Permiso insuficiente | `editor` pide importes o exporta | 403 | H |
| A21 | Permiso suficiente | `admin` | 200 | H |
| A22 | Exportación fiel | Mismos filtros en pantalla y CSV | Mismos totales y filas | H |
| A23 | Exportación segura | Nombre de producto `=HYPERLINK(...)` | Celda neutralizada | L |
| A24 | Exportación sin PII | Cualquier exportación | Ninguna columna de §5.2 | L, H |
| A25 | Exportación auditada | `admin` exporta | Una entrada de auditoría sin contenido | P |
| A26 | Frontera de schema | Revisión del código | Ningún `DbContext` de M10 mapea otro schema; sin referencias a `Domain`/`Data` ajenos | L (arquitectura) |
| A27 | Desinstalación | `99_drop.sql` | Elimina solo `reporting`; idempotente; el resto arranca | P, E |
| A28 | Venta no bloqueada | M10 falla al leer | La operación del dueño no se ve afectada (M10 nunca está en su camino) | P |

---

## 7. Decisiones pendientes para JP

Ninguna se ha tomado. Las marcadas **NO REVERSIBLE** implican esquema, migraciones o contratos de
módulos cerrados.

### D-M10-01 · Fuente autoritativa de los hechos — **NO REVERSIBLE**

- **Pregunta:** ¿de dónde obtiene M10 la verdad?
- **Opciones:**
  - **A.** Lectura por contrato del dueño; el bus, como mucho, aviso de refresco. (Propuesta histórica.)
  - **B.** Outbox durable en cada dueño + consumidor con reconciliación periódica.
  - **C.** Eventos del bus como única fuente.
- **Impacto:** A exige un contrato nuevo por dueño. B exige mecanismo durable transversal (§29
  «decidir mecanismo durable»), tablas nuevas en cada dueño y cambios en sus transacciones.
  C contradice `PENDIENTES.md:544` y además hoy **no hay eventos que consumir** (§3.1).
- **Recomendación técnica:** **A.** Es el patrón que §29 ya fijó para M06 («si puede releer la
  verdad → lectura», `PENDIENTES.md:556`) y el que M05b dejó abierto para M10 (`service-orders/SPEC.md:508`).
  C se descarta. B solo si un caso real exige entrega asíncrona.
- **Qué cambia:** SPEC M10 §3/§5; contratos de M03, M05b y M07 (**PENDIENTE JP + Chat 2**, expediente
  §8); y la redacción de `ARQUITECTURA_MODULAR.md:66,120,341` y `ROADMAP_MODULAR.md:124` (Chat 2 + JP).

### D-M10-02 · ¿Proyección persistida o consulta en vivo? — **NO REVERSIBLE**

- **Pregunta:** ¿M10 guarda una copia de los hechos o pide los agregados al dueño cada vez?
- **Opciones:**
  - **A.** **Agregados en vivo:** cada dueño expone una lectura agregada por rango (recuentos e
    importes por día/estado). M10 no tiene tablas de hechos.
  - **B.** **Proyección con cursor** (propuesta histórica §4): M10 copia hechos a
    `reporting.fact_projection` mediante lectura reanudable.
  - **C.** A en v1, B solo cuando el volumen lo exija.
- **Impacto:** A no tiene evento perdido, ni reconstrucción, ni checkpoint, ni datos
  desactualizados — A05–A09 dejan de aplicar —, pero pone la semántica de agregación en el dueño y
  cada reporte nuevo exige tocarlo. B mantiene la lógica en M10 pero exige cursor estable,
  correcciones, reconstrucción y tablas propias (migraciones). Esto decide si M10 tiene esquema
  propio de hechos en su primera migración.
- **Recomendación técnica:** **C.** Una tienda pequeña no justifica una proyección; los agregados
  por rango pueden calcularse en el dueño con índices propios. Se generaliza cuando exista un caso
  real (`CLAUDE.md`, regla de trabajo 5). **Debilidad de la recomendación:** obliga a modificar
  tres módulos cerrados con lógica de reporte; si JP prefiere no reabrirlos más de una vez, B
  concentra el cambio en «exponer hechos» y nunca más.
- **Qué cambia:** SPEC M10 §4 (tablas sí/no), §5, §9; contratos de los dueños; `99_drop.sql`.

### D-M10-03 · Fuentes y orden de v1

- **Pregunta:** ¿qué fuentes entran en v1 y en qué orden?
- **Opciones:**
  - **A.** Solo M03 (pedidos + cobros).
  - **B.** M03 + M05b.
  - **C.** M03 + M05b + M07 (propuesta histórica).
- **Impacto:** M07 exige crear `Sillar.Modules.B2B.Contracts`, que hoy no existe. M05b tiene el
  contrato más cercano pero con PII. Cada fuente es un contrato nuevo en un módulo cerrado.
- **Recomendación técnica:** **A**, con M05b como segunda entrega. M03 concentra el valor
  comercial; pero M03 cobros depende de la adenda JP-02 (no integrada), así que conviene que el
  contrato de cobros nazca después de JP-02.
- **Qué cambia:** SPEC M10 §2/§3; posible `ENTREGA-01-*.md`; cola de costuras de Chat 2.

### D-M10-04 · Vocabulario monetario

- **Pregunta:** ¿cómo se llaman y se combinan las cifras de dinero?
- **Opciones:**
  - **A.** «Valor de pedidos» y «Cobros registrados», M03 y M07 en series separadas; la palabra
    «ingresos» no aparece.
  - **B.** Igual que A, con una línea «Cobros totales» que suma M03 + M07.
  - **C.** Usar «Ingresos» para cobros registrados.
- **Impacto:** C promete algo que no hay forma de sostener: sin reversos (`OrderPayment.cs`) ni
  comprobantes. B mezcla fuentes con reglas de atribución distintas.
- **Recomendación técnica:** **A.**
- **Qué cambia:** SPEC M10 §2, §8; textos de UI en STOP 3.5.

### D-M10-05 · Roles, umbral de privacidad y auditoría

- **Pregunta:** ¿quién ve qué?
- **Opciones:**
  - **A.** `admin+` ve y exporta todo; `editor` sin acceso a M10.
  - **B.** `editor` ve recuentos operativos sin importes; `admin+` importes y exportación.
  - **C.** Todos ven todo.
- **Impacto:** B necesita dos niveles de respuesta en los mismos endpoints. A es el más simple.
  Sub-preguntas: ¿se audita ver o solo exportar? ¿umbral mínimo para mostrar un agregado?
- **Recomendación técnica:** **A** en v1, auditando solo exportaciones, sin umbral de anonimato.
- **Qué cambia:** SPEC M10 §6, §8, §9.

### D-M10-06 · Fecha de un hecho

- **Pregunta:** cuando un pedido cambia de estado, ¿en qué período cuenta?
- **Opciones:**
  - **A.** Por su fecha de creación, con el estado actual.
  - **B.** Por la fecha de cada transición (requiere historial de M03 por contrato).
- **Impacto:** A es reproducible con lo que el dueño ya guarda; B da «qué pasó en octubre» pero
  exige exponer `order_status_changes`.
- **Recomendación técnica:** **A** para pedidos; cobros siempre por fecha de registro del pago.
- **Qué cambia:** SPEC M10 §8, contrato de M03.

### D-M10-07 · Apertura de M10 frente a Infra QA §34

- **Pregunta:** ¿se abre la implementación de M10 antes de resolver §34?
- **Opciones:** **A.** No, primero §34. **B.** Sí, con puerta serializada. **C.** Solo trabajo documental hasta §34.
- **Impacto:** `PENDIENTES.md:663` exige que JP lo decida antes de abrir M10. No afecta a este
  documento.
- **Recomendación técnica:** **C** (es lo que se está haciendo).
- **Qué cambia:** `PENDIENTES.md` §34 (Chat 2 + JP).

### D-M10-08 · Identidad de la «SPEC V3.1»

- **Pregunta:** ¿«V3.1» es la propuesta `87f40cb1…`, o existe otro texto?
- **Opciones:** **A.** Es la propuesta; se renombra así. **B.** Existe otro texto; se entrega a Claude C.
- **Impacto:** si es B, §2 de este documento debe rehacerse contra ese texto.
- **Recomendación técnica:** confirmar A o entregar el texto antes del siguiente hito.
- **Qué cambia:** este documento §0–§2.

---

## 8. Lo que este documento no hace

- No crea ni modifica `SPEC.md` ni ningún documento canónico.
- No propone código, migraciones, SQL ni cambios en `e2e/`, `scripts/verificar.mjs`, Docker o Infra QA.
- No pide todavía ninguna costura a los dueños; las describe.
- No convierte la propuesta histórica de «eventos como invalidación + lecturas reanudables» en
  contrato: queda **PENDIENTE JP + Chat 2**.
- No declara M10 aprobado ni certificado.
