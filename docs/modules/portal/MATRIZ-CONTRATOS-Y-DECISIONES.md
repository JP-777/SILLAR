# M08 — Matriz de fuentes, contratos y decisiones · paquete para JP / Claude A

**Estado:** D1–D6 RATIFICADAS por JP el 09/10/2026; contratos aprobados para implementación focal, aún no integrados. **Base consultada:** `4a3fd1ef12fc2226bad185f003acea9168b9a4c9` (`main`, 2026-10-09).
**Regla:** «Existe en código», «documentado» y «propuesto» no son la misma cosa. No tomar una firma propuesta como disponible en DI.

## 1. Qué existe y a quién pertenece

| Fuente exacta en el repositorio | Hecho verificable | Uso permitido en M08 | Estado |
|---|---|---|---|
| `backend/Sillar.Modules.Crm.Contracts/ICurrentCustomer.cs` | `CustomerId`, `Email`, `EmailVerified` | Identidad de sesión, nunca de parámetro cliente | EXISTE |
| `backend/Sillar.Modules.Crm.Contracts/CustomerAuthorization.cs` | Policy `crm:customer` | Requisito de lectura de Portal | EXISTE |
| `backend/Sillar.Modules.Crm.Contracts/CustomerCsrfEndpointFilter.cs` | CSRF para escrituras del cliente | Solo si M08 añade escrituras futuras | EXISTE |
| `backend/Sillar.Modules.Crm/Endpoints/CustomerProfileEndpoints.cs` | `/api/customer/profile` y direcciones | M04 es dueño de editar perfil | EXISTE |
| `frontend/src/modules/crm/routes.tsx` y `session/guards.tsx` | `/mi-cuenta`, `/entrar`, `RequireCustomerAuth` | Reutilizar protección y navegación real | EXISTE |
| `backend/Sillar.Modules.Sales.Contracts/ICustomerOrderHistory.cs` | `ObtenerPedidosDeAsync(customerId,limit,ct)` y `CustomerOrderSummary` | Tarjetas de pedidos propios | EXISTE |
| `backend/Sillar.Modules.Sales/Endpoints/PedidosDelClienteEndpoints.cs` | `/api/sales/my-orders` y `/{orderCode}`; detalle filtra dueño dentro de consulta EF | Detalle propio o lectura delegada | EXISTE |
| `backend/Sillar.Modules.Sales.Contracts/OrderStatus.cs` | Siete estados técnicos; frase visible en UI | No inventar estados ni afirmar stock reservado | EXISTE |
| `backend/Sillar.Modules.ServiceOrders/Domain/ServiceOrder.cs` | `CustomerId` es `Guid?`; permite NULL | Una orden sin vínculo NO es propia por coincidencia de contacto | EXISTE |
| `backend/Sillar.Modules.ServiceOrders.Contracts/ServiceOrderContracts.cs` | `IServiceOrderTrackingSource` lee por orden sin filtro de dueño; estados M05b; transición de dueño | **No** convertirlo en endpoint cliente | EXISTE pero INSUFICIENTE |
| `backend/Sillar.Modules.Tracking/Endpoints/TrackingEndpoints.cs` | Siete rutas `/api/admin/tracking`, política de personal | Nunca llamarlas desde Portal | EXISTE solo ADMIN |
| `docs/modules/tracking/SPEC.md` §5.2 y §6.5 | Notas solo internas; contrato apto para M08 queda por definir | Solicitar proyección sanitaria nueva al dueño de M06 | COMPROMISO DOCUMENTAL, SIN CÓDIGO |
| `backend/Sillar.Modules.Tracking.Contracts/` | No aparece en árbol de `main` verificado | No asumir proyecto ni firma | **NO EXISTE** |
| `frontend/src/modules/sales/` | No aparece en árbol de `main` verificado | No poner enlaces a rutas UI inventadas | **NO EXISTE** |
| `frontend/src/modules/portal/`, `backend/Sillar.Modules.Portal/` | No aparecen en árbol de `main` verificado | Nuevo trabajo exclusivo de M08 | **NO EXISTEN** |
| `docs/ARQUITECTURA_MODULAR.md` §4 | Prevé `portal.users` y `customer_profiles`, pese a autenticación real M04 | Elevar discrepancia D1; no duplicar silenciosamente | DOCUMENTACIÓN HISTÓRICA DESACTUALIZABLE |
| `docs/ROADMAP_MODULAR.md` Fase 3 | Portal ofrece cuentas, pedidos M03 y trabajos M06 | Propósito de negocio | DOCUMENTADO |
| `docs/adr/ADR-016-*`, `ADR-018-*` | Replicación y prohibición FK replicada→local | Clasificar antes de DDL | RATIFICADO |
| `docs/ANTES-DE-EMPEZAR-UN-MODULO.md` §§1–8 | Falsificar barreras, EF→SQL real, dependencia y colaciones | Diseño de pruebas y gate | REGLA TRANSVERSAL |
| `docs/integracion/CIERRE-PARIDAD-PARALELIZACION-PRE-M08-20261009.md` | Paridad estática 9/9 y límite destructivo M05b | No marcar restaurabilidad integral como PASS | DOCUMENTADO |

## 2. Decisiones RATIFICADAS por JP el 09/10/2026

| Orden | D | Pregunta literal | Valor ratificado | Límite |
|---|---|---|---|---|
| 1 | D1 | ¿Portal v1 tiene datos persistentes propios? | No; sin `portal.users` ni `portal.customer_profiles` porque pertenecen a M04. No migración nueva | No iniciar Paso 2, ni inventar schema `portal` |
| 2 | D2 | ¿Quién es dueño del vínculo pedido de servicio ↔ cliente? | M05b por `customer_id == ICurrentCustomer.CustomerId`; NULL invisible, sin reclamo por código/nombre/email/teléfono | No exponer trabajos al cliente |
| 3 | D3 | ¿Qué avance de M06 se expone exactamente? | Proyección mínima por Contracts: estado comercial/hito y momento permitido, cero notas/prioridades/plazos internos/personal | No definir DTO público ni aceptar API admin como sustituto |
| 4 | D4 | ¿Qué sucede con trabajos si está M05b pero M06 ausente? | Se explica ausencia de seguimiento; M08 v1 no incorpora dependencia nueva a M05b | No pintar datos falsos; alternativa requiere contrato y decisión explícita |
| 5 | D5 | ¿Detalle de pedido/trabajo además de tarjetas? | Sí, pero únicamente mediante APIs/Contracts de dueño con pertenencia propia. M03 tiene API real, no UI frontend confirmada | No inventar URL frontend |
| 6 | D6 | ¿Qué datos públicos mínimos se muestran? | Código visible, estado de negocio en español, fecha relevante y compromiso real, no fecha interna | Dejar campos controversiales fuera del DTO |

**Ratificación:** JP aprueba D1–D6 según recomendación original, y D2 corresponde expresamente a A: órdenes manuales `CustomerId=NULL` invisibles y sin reclamo/vinculación posterior en v1. D1 implica cero tablas y migraciones de Portal. Cualquier cambio exige nueva decisión registrada antes del DDL.

## 3. Costuras técnicas: un escritor, una entrega, una prueba

| Costura | Dueño de implementación | Solicitud concreta de M08 | Consumidor posterior | Criterio de aceptación focal |
|---|---|---|---|---|
| Propiedad de órdenes de servicio | Dueño de M05b | `ICustomerServiceOrderReader` con lista por `customerId` y detalle por código visible; filtro PostgreSQL | M06, **no M08 directo** | A ve A, B recibe null/404 HTTP, NULL invisible, sin búsqueda por texto |
| Proyección segura de seguimiento | Dueño de M06 | `ICustomerTrackingProgress` con lista/detalle por código visible y propiedad delegada en M05b | M08 | Solo estado/fechas públicas/líneas seguras; ausencia de notas, plazo/prioridad internos y personal |
| Composición de pedidos | M03 (contrato ya presente) | Confirmar que su `ICustomerOrderHistory` puede resolverse condicionalmente; no pedir cambios si basta | M08 | M03 inactivo = ausencia de módulo, no lista vacía |
| Sesión/perfil | M04 (contrato ya presente) | Ningún cambio de cuenta; reutilizar `ICurrentCustomer`/política y guardas | M08 | Admin-only no entra, cliente A no ve B |
| Host/activación/API csproj | Chat 2 — Integración | Incorporar proyecto Portal **una sola vez**, sin colisión de agentes | Agente A entrega módulo, Chat 2 cose | Borrar binario o provider produce fallo atribuible |
| Setup/`migrate()`/`seed()`/etapa 4 | Chat 2 — Integración | Definir ruta sin schema o implementar paridad con schema ratificado; no inventar seed | QA | Detector de omisión rojo, setup consistente |
| Frontend plataforma/rutas compartidas | Chat 2 — Integración | Añadir montaje/desmontaje de Portal con navegación y API condicional | Agente A crea `modules/portal/*` | M08 apagado: no ruta, menú ni hueco |
| QA canónica/escenario `M08-CICLO` | QA independiente | Ciclo etapa 6 con módulos vecinos y datos centinela | JP autoriza integración SHA | No skips, 6/6, PostgreSQL real, limpieza de bases |

**No editar en paralelo:** `backend/Sillar.Api/Sillar.Api.csproj`, `e2e/setup/migrate.ts`, `scripts/verificar.mjs`, navegación central y `docs/PENDIENTES.md`. Los dueños solicitan una única costura que aplica Chat 2; los agentes trabajan en rutas propietarias del módulo. Prioridad a decisiones y contratos, no automatización extra.

## 4. Riesgos de producto específicos de M08

**R1 — Orden sin cliente vinculado.** M05b permite contacto capturado sin `CustomerId`. No mostrar por coincidencia de correo (puede haberse reutilizado), teléfono (compartido) o nombre (no único). Si se quiere vinculación posterior, pedir flujo separado con prueba fuerte de titularidad, auditoría e idempotencia.

**R2 — Filtración por proveedor.** `IServiceOrderTrackingSource.GetAsync(serviceOrderId)` **no** filtra por `customerId`; convertirlo en ruta pública es una fuga. M06 tampoco dispone hoy de contrato público apto para cliente. Bloquea la parte de trabajos hasta publicar ambos contratos.

**R3 — Dos cookies.** CORE y M04 separan administradores y clientes; no usar `ICurrentAdmin` para autenticar clientela. El trío administrativo solo aplica a actos de personal; leer un portal no requiere inventar actor administrativo.

**R4 — Desfase de arquitectura.** `portal.users`/`customer_profiles` en el documento de planificación no justifican datos duplicados frente a M04 ya funcional; registrar decisión D1 y, si procede, una posterior corrección de arquitectura en rama documental propiedad de Integración, no de A.

**R5 — Promesas de UI.** M03 tiene API real de pedidos, pero no `frontend/src/modules/sales/` en la base. El Portal puede dibujar una tarjeta/detalle propio *sin poseer los datos*; no prometer botones a una ruta inexistente.

**R6 — Aislamiento en reinstalación.** Si v1 no tiene schema, el ciclo prueba activación/desactivación y no borrado de datos. Si se ratifica schema, `99_drop` solo toca `portal`, sin `CASCADE` indiscriminado ni destrucción de CRM, Sales, ServiceOrders o Tracking.

**R7 — QA tardía.** M05b+M06 mostró el coste de saturar PostgreSQL con proyectos concurrentes; conservar `-m:1`, no ampliar timeouts ni atribuir a producto los fallos del arnés sin prueba.

## 5. Acta breve para el relevo

- `main` funcional integrado: `4a3fd1ef12fc2226bad185f003acea9168b9a4c9`.
- QA M05b+M06 cerrada: 6/6, 782 backend, 180 e2e. La focal destructiva integral de M05b sigue pendiente (registrada en rama `docs/pendiente-restaurabilidad-m05b-20261009`, no integrada).
- M08 Paso 1: D1–D6 y contratos aprobados por JP, **focal técnica pendiente de validación**; no hay código M08 ni migraciones.
- Agente A: ejecutor principal. Chat 2: costuras e integración; JP: producto/ratificación; QA: independiente.
- Prioridad absoluta al volver A: respetar D1–D6 ya ratificadas, verificar los contratos M05b/M06 integrados y después implementar Portal sin tocar módulos ajenos directamente.
