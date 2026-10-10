# SILLAR — Relevo al colíder | Acta cronológica M08-07B

**Registro:** 2026-10-10 08:41:53 America/Lima. **Autoridad:** JP ejecutó la prueba local; Chat 2 preparó la verificación y este relevo. **Estado:** 07B HTTP REAL PASS **1/1, sin fallos ni omisiones**. **No constituye QA independiente ni certificación integral.**

## 1. Identidad, base y alcance de la publicación

- Contratos M05b/M06: `963e0f1d12b131c4296cd836c88c3334fc22288e`.
- Backend Portal: `91d617606b078cc2692dab6b82aca9a7f1cccb37`.
- Host integrado: `e549fd92dcc167be98cae2d39c7966abe89b44ed`.
- HTTP 07A: `985531c16e1eb134bd53b75097c6291c98ec69fb`, validado en Acta 07A.
- **HTTP 07B:** `fcde5be7befbf018a2a40075a797520a565cb6e3`; padre exacto `985531c16e1eb134bd53b75097c6291c98ec69fb`, rama `test/m08-http-manual-20261010`.
- Base documental 07A: `5b3b6ead0563fcbff7684ab6e7fd94d8494a7bb3`.
- Corte de `main`: `4a3fd1ef12fc2226bad185f003acea9168b9a4c9`, sin integrar M08.

## 2. Procedimiento efectuado

- Worktree de prueba única: `/home/JP777/sillar-m08-http-manual`, identidad Docker `sillar_m08_http_manual_e2e`, offset `91` y puertos DB 55991, API 56091, frontend 56191.
- Se creó una nueva spec sin modificar código de producto: `e2e/tests/m08-portal-ownership-http.spec.ts`; comprobación TypeScript `tsc --noEmit` aprobada.
- El arnés instaló CORE, Catálogo, CMS, CRM, Servicios, Órdenes de servicio, Tracking, Sales y B2B sobre PostgreSQL efímero, aplicó semillas y levantó la API.
- El test activó la cadena real `services → service_orders → tracking → portal`, creó un servicio publicable y registró dos clientes independientes, A y B.
- El administrador creó órdenes A, B y una orden manual anónima con `customerId = NULL`, más prioridad, plazo y nota **internos** en seguimiento para A.
- Playwright cerró con `1 passed` y JUnit `tests=1 failures=0 errors=0 skipped=0`. El teardown retiró solo contenedores, red y volumen del stack M08.

## 3. Evidencia funcional observada

1. Cada cliente ve exclusivamente su código de trabajo en `/api/portal/overview`. El cliente A no recibe los códigos de B ni anónimo, y viceversa.
2. El detalle propio responde `200`; las órdenes ajenas, anónima e inexistente responden `404` para ambos clientes.
3. `?customerId=<id ajeno>` no sustituye la identidad de la cookie, ni en listado ni en detalle ajeno.
4. Una cookie de cliente no abre `/api/admin/service-orders/{id}` (`401`); la cookie administrativa no sirve para leer un detalle Portal (`401`).
5. Los JSON comprobados no exponen campos como `customerId`, `requestedDetails`, `receivedNotes`, `boardPriority`, `internalDueAt`, `adminUserId` ni los marcadores privados insertados; no se basó solo en arrays vacíos.

**Límite:** Es una prueba E2E focal con órdenes de servicio M05b/M06. No acredita órdenes reales de M03 Sales con propietarios, UI cliente, ciclo destructivo `[M08-CICLO]`, regresión global, sabotajes completos ni QA de colíder.

## 4. Evidencias y huellas verificables

**Directorio local JP:** `/var/tmp/sillar-m08-http-07b-20261010-Gsi62czS`. Evidencias no subidas al repositorio, preservadas en la máquina de JP.

| Elemento | SHA-256 |
|---|---|
| `e2e/tests/m08-portal-ownership-http.spec.ts` | `d84290c2af2ec525d5f00f236932aadde436ebcdf9ede43140867660dacfb4bc` |
| `07b-junit.xml` | `6089ca21c8fb22fc00af51c42e3e84856c99dd317f479c5aeb2734ff71e07a54` |
| `http07b.log` | `4a0a10cbea12c3cfcfa1e687c90c830f2be1a33f50735e368a1971536217b551` |
| `typecheck.log` | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |

Los reinicios del host generaron errores de proxy Vite `ECONNREFUSED`/`ECONNRESET`. Se conservan en el log aunque la prueba sea verde. Las colisiones de offsets ajenas no fueron alteradas.

## 5. Barreras y continuación

- **Pendiente:** `[M08-CICLO]` real de desactivar/desinstalar/reinstalar con restaurabilidad, FK y sabotajes VERDE→ROJO→VERDE en DB efímera.
- **Pendiente:** UI del Portal y sus pruebas E2E de accesibilidad, permisos y navegación.
- **Pendiente:** compuerta global `backend/Sillar.sln` (128 errores de analizadores xUnit en el último registro), sin silenciar analizadores ni tocar módulos ajenos sin coordinación.
- **Pendiente:** revalidación de pruebas M03 Sales con datos y certificación por el colíder independiente. **JP controla cualquier integración a `main`.**

**Veredicto 07B:** propiedad A/B/NULL y privacidad poblada PASS focal 1/1; M08 aún NO certificado; sin merge.
