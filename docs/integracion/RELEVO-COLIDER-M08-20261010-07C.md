# SILLAR — Relevo al colíder | Acta cronológica M08-07C

**Registro:** 2026-10-10 10:08:45 America/Lima. **Autoridad:** JP ejecutó el ensayo E2E; Chat 2 coordinó su revisión y expediente. **Veredicto focal:** PASS [M08-PRECICLO-API], 1/1 pruebas, 0 fallos, 0 errores y 0 omitidas.

## 1. Identidad verificable

- Host integrado en rama: `e549fd92dcc167be98cae2d39c7966abe89b44ed`.
- Base HTTP 07B: `fcde5be7befbf018a2a40075a797520a565cb6e3`.
- **Spec preciclo publicada:** `3c27cc7186bc9c9abca403c63121c741cf53ce61`, padre `fcde5be7befbf018a2a40075a797520a565cb6e3`, rama `test/m08-http-manual-20261010`.
- Base documental previa (Acta 07B): `1a34ae9c023952ce30157334d8516302a07bdf57`.
- `main` sin merge: `4a3fd1ef12fc2226bad185f003acea9168b9a4c9`.
- Worktree local JP: `/home/JP777/sillar-m08-http-manual`; stack exclusivo `sillar_m08_http_manual_e2e` (offset 91).

## 2. Comprobaciones realmente ejecutadas

- TypeScript E2E aprobado; stack PostgreSQL y API Docker efímeros inicializados y desmontados al terminar.
- M08 apagado: la ruta del Portal no existe (404). Activado con CRM sin Sales/Tracking: perfil disponible y proveedores marcados como ausentes.
- Sales activo sin ventas: `orders.empty`; Sales inactivo: `orders.unavailable`.
- M05a/M05b/M06 activados: dos clientes distintos y tres órdenes reales (A, B y NULL), más nota privada de Tracking.
- A puede consultar su trabajo y no puede acceder a B/NULL; el contrato devuelve 404 para ajenos.
- FK real Tracking → ServiceOrders detectada antes y después; ninguna tabla ni esquema propios `portal` en PostgreSQL, conforme a D1.
- Desactivar Portal retira su API sin borrar clientes, órdenes ni seguimiento. Los recuentos de tablas dueñas `crm`, `services`, `service_orders`, `tracking` y `sales` permanecen constantes.
- `migrate()+seed()` se reejecutó sin migraciones pendientes y sin alterar la instantánea de datos de los dueños.
- Reactivar Portal recuperó trabajo propio; apagar/reactivar los proveedores blandos mantuvo degradación honesta y restauración.
- El cleanup dejó grafo inicial y desmontó el stack propio. Hubo mensajes Vite `ECONNREFUSED`/`ECONNRESET` durante reinicios; se conservan.

## 3. Evidencias

Directorio en máquina JP: `/var/tmp/sillar-m08-preciclo-api-20261010-DKtCedr5` (los binarios/logs no se subieron al repositorio).

| Artefacto | SHA-256 |
|---|---|
| `e2e/tests/m08-portal-preciclo-api.spec.ts` | `6a1eed81979865467e2d431d4df1f0cfb69db9d9258c19aa588a4864ec3acdb3` |
| `junit.xml` | `32cc5a88aa5a85a88efeeec44833dd3466c2035f32d76e7416beddf18e29082f` |
| `preciclo.log` | `a7dde1cd7e8ec637e4dc18a72d0d78f24fb1cec0ac64d35b235bd729b2bace4d` |
| `typecheck.log` | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |

JUnit: `tests=1 failures=0 errors=0 skipped=0`. La prueba pobló dos clientes, tres órdenes M05b y una nota Tracking. Los archivos fueron verificados contra `SHA256SUMS.txt` antes de su publicación.

## 4. Límites y HOLD

- **No constituye [M08-CICLO] integral** de etapa 6; faltan UI de Codex, pruebas visuales y sabotajes VERDE→ROJO→VERDE específicos de M08.
- Sales tenía `sales.orders=0` en este ensayo; propiedad de pedidos M03 con datos reales requiere comprobación aparte.
- Frente Codex en HOLD P3.5 hasta aprobación nominal de diseño por JP; el preciclo no levanta esa barrera.
- Compilación global `HOLD_128_XUNIT` según actas anteriores; QA independiente del colíder pendiente; integración a `main` exclusivamente bajo autorización de JP.

**Cierre parcial 07C:** PASS focal del preciclo, NO certificación completa de M08.
