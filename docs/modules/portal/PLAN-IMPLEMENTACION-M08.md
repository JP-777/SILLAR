# M08 Portal — Plan de ejecución y entrega a Claude A

**Estado:** D1–D6 RATIFICADAS POR JP EL 09/10/2026; plan preparado para contratistas y Agente A. No es autorización de integración de SHA funcional. **Base:** `4a3fd1ef12fc2226bad185f003acea9168b9a4c9`.
**Responsable propuesto:** Agente A. **Coordinador de integración:** Chat 2. **Ratificación de producto:** JP, con colíder.

## 0. Qué recibe A y qué no debe reinterpretar

Paquete de lectura obligatoria antes de escribir: `docs/modules/portal/SPEC.md`, `MATRIZ-CONTRATOS-Y-DECISIONES.md`, `docs/ANTES-DE-EMPEZAR-UN-MODULO.md`, `docs/DIVISION-DE-TRABAJO.md`, `docs/ROADMAP_MODULAR.md`, `docs/PROTOCOLO-DISENO.md`, ADR-016/018/019, SPEC de M04/M03/M05b/M06, y contratos concretos de CRM/Sales/ServiceOrders. Para M06 no existe todavía `Tracking.Contracts`, y para M08 tampoco existen proyectos/pantallas.

**Prohibiciones de diseño vigentes después de ratificar D1–D6:** ningún DDL, endpoint público de trabajos, copia de auth de CRM, FK a `core.admin_users`, dependencia a DbContext ajeno o nueva biblioteca UI. No asumir `/mis-pedidos` frontend de M03 solo porque está documentada: confirmar árbol real.

## 1. Camino crítico con entregables verificables

| Puerta | Trabajo | Dueño | Salida y barrera |
|---|---|---|---|
| P1.1 | JP ratifica D1–D6 | JP, facilitado por Chat 2 | **HECHO 09/10/2026**, ver SPEC §0/§11 |
| P1.2 | M05b define lectura de órdenes **filtrada por cliente** | Dueño M05b, revisión de Chat 2 | Contracts y prueba PostgreSQL A/B/NULL antes de API M06 cliente |
| P1.3 | M06 define proyección pública de avance | Dueño M06, revisión de Chat 2 | Contrato seguro con lista+detalle; cero notas y autor; revalida pertenencia |
| P1.4 | Paso 1 SPEC y §9 ratificados | Chat 2 con JP | Paso 1 cerrado explícitamente, no solo borrador firmado por A |
| P2 | Datos y clasificación replicable | A, Chat 2 verifica ADR y paridad | Si D1 sin schema, probar esa decisión en la infraestructura modular; si hay DDL, DATOS/ER/migraciones/seed/drop |
| P3 | API y composición | A solo en `Sillar.Modules.Portal*` | Política CRM, backend filtra propiedad en proveedor; combinaciones M03/M06; docs Swagger si hay DTO |
| P3.5 | Diseño visual | JP en Claude Design | §9 completo: cada pantalla con vacío/datos/carga/conflicto, claro/oscuro y móvil/escritorio |
| P4 | UI React | A solo en `frontend/src/modules/portal/*` | Componentes/tokens existentes, sin duplicar perfil/cuentas, manejo de 401/404/ausencia |
| P5 | QA focal, ciclo, cierre | A aporta pruebas; QA independiente certifica; Chat 2 integra | `[M08-CICLO]` dentro de etapa 6, paridad y sabotajes, 6/6 sin omitidas |

P1.2 y P1.3 son la **ruta crítica** de los trabajos: el diseño de interfaces puede preparar estados y campos permitidos, pero el backend público no debe simular contratos inexistentes. No bloquear toda la página de perfil/pedidos por ello; sí bloquear la exposición de trabajos.

## 2. Orden sugerido de lotes para A (sin paralelismo sobre ficheros compartidos)

### Lote A0 — Inventario reproducible (cero modificaciones)

- `git fetch`; fijar SHA de base y crear rama propia tras las decisiones ratificadas de JP, sin asumir que los contratos candidatos ya están integrados.
- Abrir firmas concretas `ICurrentCustomer`, `ICustomerOrderHistory`, `ICustomerServiceOrderReader` ratificada y `ICustomerTrackingProgress` ratificada.
- Documentar el orden de carga de módulos, rutas de autenticación y montaje de frontend realmente existentes.
- **D1 ratificada:** v1 sin schema propio ni DDL; no compete a A agregar tablas de identidad/perfil.

### Lote A1 — Contratos y pruebas de seguridad por dueño

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

Cuando los documentos M08 estén listos y ratificados, Chat 2 notificará expresamente «PAQUETE M08 LISTO PARA A», junto con los SHA, los contratos disponibles y las decisiones no cerradas (si existen). **Solo después**, por orden de JP, empezará la planificación independiente de los siguientes módulos y el reparto de cuatro agentes, basado en los once casos del registro. Esa planificación no se improvisa dentro de la SPEC de Portal.
