# M08 Portal — Plan de ejecución y entrega a Claude A

**Estado vigente 10/10/2026:** D1–D6 ratificadas; contratos M05b/M06 publicados en rama `963e0f1`; QA focal interna PASS. QA independiente, puerta canónica, integración `main` y desarrollo Portal pendientes. **Base histórica:** `4a3fd1e`. No autoriza merge.
**Responsable propuesto:** Agente A. **Coordinador de integración:** Chat 2. **Ratificación de producto:** JP, con colíder.

## 0. Qué recibe A y qué no debe reinterpretar

Paquete de lectura obligatoria antes de escribir: `docs/modules/portal/SPEC.md`, `MATRIZ-CONTRATOS-Y-DECISIONES.md`, `docs/ANTES-DE-EMPEZAR-UN-MODULO.md`, `docs/DIVISION-DE-TRABAJO.md`, `docs/ROADMAP_MODULAR.md`, `docs/PROTOCOLO-DISENO.md`, ADR-016/018/019, SPEC de M04/M03/M05b/M06, y contratos concretos de CRM/Sales/ServiceOrders. `Tracking.Contracts` ya existe en `963e0f1` (**no** en `main` histórico), pero M08 todavía no tiene proyectos/pantallas en este corte.

**Prohibiciones de diseño vigentes después de ratificar D1–D6:** ningún DDL, endpoint público de trabajos, copia de auth de CRM, FK a `core.admin_users`, dependencia a DbContext ajeno o nueva biblioteca UI. No asumir `/mis-pedidos` frontend de M03 solo porque está documentada: confirmar árbol real.

## 1. Camino crítico con entregables verificables

| Puerta | Trabajo | Dueño | Salida y barrera |
|---|---|---|---|
| P1.1 | JP ratifica D1–D6 | JP, facilitado por Chat 2 | **HECHO 09/10/2026**, ver SPEC §0/§11 |
| P1.2 | Lectura M05b filtrada por cliente | Integración / M05b | PUBLICADO EN RAMA `963e0f1`; PG real A/B/NULL y 47/47 PASS, QA independiente pendiente |
| P1.3 | Proyección pública de avance | Integración / M06 | PUBLICADO EN RAMA `963e0f1`; 35/35 PASS, DTO restringido, QA independiente pendiente |
| P1.4 | Paso 1 SPEC y §9 ratificados | Chat 2 con JP | Paso 1 cerrado explícitamente, no solo borrador firmado por A |
| P2 | Datos y clasificación replicable | A, Chat 2 verifica ADR y paridad | Si D1 sin schema, probar esa decisión en la infraestructura modular; si hay DDL, DATOS/ER/migraciones/seed/drop |
| P3 | API y composición | A solo en `Sillar.Modules.Portal*` | Política CRM, backend filtra propiedad en proveedor; combinaciones M03/M06; docs Swagger si hay DTO |
| P3.5 | Diseño visual | JP en Claude Design | §9 completo: cada pantalla con vacío/datos/carga/conflicto, claro/oscuro y móvil/escritorio |
| P4 | UI React | A solo en `frontend/src/modules/portal/*` | Componentes/tokens existentes, sin duplicar perfil/cuentas, manejo de 401/404/ausencia |
| P5 | QA focal, ciclo, cierre | A aporta pruebas; QA independiente certifica; Chat 2 integra | `[M08-CICLO]` dentro de etapa 6, paridad y sabotajes, 6/6 sin omitidas |

P1.2 y P1.3 ya están materializados en la rama `963e0f1`, pero no en `main`. M08 puede desarrollarse y probarse **sobre esa base de rama**, sin exponer rutas HTTP públicas hasta implementar la política M04 y verificar propiedad.

## 2. Orden sugerido de lotes para A (sin paralelismo sobre ficheros compartidos)

### Lote A0 — Inventario reproducible (cero modificaciones)

- `git fetch`; crear rama de implementación M08 aislada **basada en `963e0f1`** o incorporando ese commit. No asumir contratos en `main` hasta su merge autorizado.
- Abrir firmas concretas `ICurrentCustomer`, `ICustomerOrderHistory`, `ICustomerServiceOrderReader` ratificada y `ICustomerTrackingProgress` ratificada.
- Documentar el orden de carga de módulos, rutas de autenticación y montaje de frontend realmente existentes.
- **D1 ratificada:** v1 sin schema propio ni DDL; no compete a A agregar tablas de identidad/perfil.

### Lote A1 — Contratos publicados y pruebas de seguridad por dueño (auditar, no duplicar)

- M05b: contrato nuevo `ICustomerServiceOrderReader`, `ListForCustomerAsync(customerId,limit,ct)` y `GetForCustomerAsync(customerId,visibleCode,ct)`, filtros en SQL por propietario, nulos invisibles. Verificación por dueño y PostgreSQL real.
- M06: contrato nuevo `ICustomerTrackingProgress` con `ListForCustomerAsync(customerId,limit,ct)` y `GetForCustomerAsync(customerId,visibleCode,ct)`; M06 revalida M05b y proyecta únicamente estado autoritativo, fechas públicas y líneas seguras, sin ninguna nota, plazo interno, prioridad, personal o `Guid` visible.
- Cobertura: dos clientes A/B, una orden no vinculada, un tercero adivinando código, cliente eliminado/bloqueado según política CRM, proveedor sin filas, choque de inactivación durante lectura.
- **No** tocar `ServiceOrderDbContext` o `TrackingDbContext` desde M08. Contrato publicado por dueño antes del consumidor.

### Lote A2 — Módulo Portal API

- `Sillar.Modules.Portal` y, si conviene separar fachada, `Sillar.Modules.Portal.Contracts` solo cuando exista consumidor real; no añadir contratos vacíos.
- Registrar `Code=portal`, `DisplayName="Portal del Cliente"`, dependencia dura `crm`, blandas `sales`/`tracking`, orden de visualización coherente.
- Componer lecturas condicionadas con `IServiceProvider.GetService<T>()` donde corresponda; presencia efectiva de servicio, sin confiar únicamente en la tabla de activación.
- `GET /api/portal/overview`: autorización `crm:customer`, sin `customerId` externo, errores por sección; limit/paginación acotados.
- Detalle propio de trabajo opcional ratificado; propia sesión, revalidación de propiedad por dueño, 404 indistinguible para ajeno.
- Sin endpoints de escritura en v1, CSRF de M04 solo para futuras escrituras. No modificar estados desde Portal.

### Lote A3 — Diseño y frontend

- Enviar §9 a JP/Claude Design como lista de P1–P6 y estados, no como HTML implementable. El mockup no es código a copiar.
- Componentes `frontend/src/shared/ui`, tokens CSS, `RequireCustomerAuth`; sección propia sin cambiar rutas de CRM ni asumir `frontend/src/modules/sales/`.
- Montaje condicional por módulo activo. No crear enlaces rotos a rutas no implementadas.
- Invalidar cache/sesión de clientes al logout y al cambio de cliente; no dejar en DOM los datos de A tras entrar B.
- Accesibilidad axe, teclado, responsive y tema.

### Lote A4 — Focal y QA del módulo

- Pruebas contractuales puras para DTO expuesto y traducción de estados.
- Pruebas PostgreSQL reales para filtros A/B/NULL/404 (EF→SQL), separación autoría y ausencia de filtrado frontend; matrices de proveedores.
- Pruebas e2e sobre la API real + UI real, no mocks para acreditar aislamiento.
- Provocar a propósito: (a) omitir filtro `customer_id`, (b) inyectar nota M06 en DTO, (c) quitar `crm` de dependencias duras, (d) omitir Portal del inventario de la puerta, (e) crear enlace ruta muerta al desactivar. Cada guarda debe ponerse roja por motivo atribuible y recuperarse en verde.
- Ejecutar `node scripts/verificar.mjs` solo en turno de QA, PostgreSQL dimensionado y `-m:1` intacto. Cerrar con TRX y Playwright comparables.

## 3. Ciclo obligatorio dentro de etapa 6

**`[M08-CICLO]`** en `e2e/tests/zz-z-m08-ciclo.spec.ts` (ruta propuesta, ajustar a convenciones existentes):

1. Base efímera con M04; instalar sin activar M08 y verificar que no hay ruta ni navegación.
2. Activar M08 sin M03/M06; autenticar cliente, ver perfil y mensajes honestos de ausencia.
3. Activar M03, crear pedido propio desde sus operaciones; ver solo el del cliente autenticado; apagar M03 y comprobar degradación sin 500 ni fuga.
4. Activar M05b y M06 siguiendo dependencias; crear orden vinculada a A, otra a B y una NULL. A solo ve A; detalle de B devuelve 404; nota interna, prioridad y plazo interno no aparecen en ninguna respuesta. Si los contratos no están ratificados, el ciclo no se considera completo.
5. Desactivar M08: rutas/menú desaparecen; M04 sigue iniciando sesión, M03 conserva pedidos y M06 conserva trabajos/notas, todos con mismos recuentos y datos centinela.
6. Reinstalar/reactivar M08: **si no hay schema**, probar composición/restablecimiento y que no se requiere DDL; **si hay schema ratificado**, ejecutar `99_drop` exclusivamente para `portal`, idempotencia y `migrate()+seed()` restauran su estructura, no datos ajenos.
7. Repetir con el proveedor blando activo/inactivo; comprobar ausencia de residuos y bases/volúmenes huérfanos.

**La etapa 6 y el código de salida real de `node` acreditan el ciclo.** Una prueba focal verde por sí sola no es certificación final. Cero omitidas.

## 4. Barrera de paridad / costura única de integración

No modificar en ramas de módulo `backend/Sillar.Api/Sillar.Api.csproj`, host central, `e2e/setup/migrate.ts`, `scripts/verificar.mjs`, ni registros globales para disimular una instalación incompleta. A entrega lista exacta de efectos a Chat 2; Chat 2 aplica la costura en rama de integración con pruebas de paridad. Para Portal sin schema: documentar explícitamente qué inventarios aplican y demostrar el alta/baja. Para Portal con schema: binario, `POST /api/setup`, migraciones y `seed()` completos; romper cada medio de instalación y exigir fallo.

El historial post-M05b recuerda que `M03` entró una vez sin la restauración equivalente en `migrate()+seed()`. M08 debe obligar esa comparación antes de cerrar, incluso si no tiene dominio persistente propio.

## 5. Guion de entrega de A / lista de comprobación

A reportará en una sola entrega: SHA base; rama y SHA candidato; decisiones ratificadas; archivos propios; costuras solicitadas; tabla de dependencias real; clasificación ADR; firmas efectivas consumidas; matriz E2E de M04/M03/M06; lista de campos del JSON público; pruebas PostgreSQL; negativos y sabotajes; ciclo de instalación/desmontaje; inventarios setup/migrate/seed; capturas y aprobación de diseño; `git status` limpio; QA focal; riesgos abiertos. **No puede certificar su propio SHA como QA independiente.**

## 6. Pausa y transición hacia los cuatro agentes

Cuando la preparación contractual y documental M08 esté lista para desarrollo aislado, Chat 2 entregará SHAs y barreras pendientes. Durante el fin de semana, JP podrá encargar M08 a Codex en un territorio propio. La ausencia del colíder no equivale a certificación. Después de agotar M08, se planificarán módulos nuevos y al final la jornada automática.
