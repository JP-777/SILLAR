# SILLAR — Relevo al colíder | Acta cronológica M08-07A

**Registro:** 2026-10-10 08:08:01 America/Lima. **Autoridad:** JP (ejecutó pruebas locales); Chat 2 (preparó verificación/documentación). **Estado:** PRUEBA HTTP REAL FOCAL 1/1 PASS; **M08 NO CERTIFICADO** y **NO MERGE A MAIN**.

## 1. Identidad y ascendencia

- Contratos M05b/M06: `963e0f1d12b131c4296cd836c88c3334fc22288e`.
- Backend M08: `91d617606b078cc2692dab6b82aca9a7f1cccb37`.
- Host integrado probado: `e549fd92dcc167be98cae2d39c7966abe89b44ed` (rama `integration/m08-host-manual-20261010`).
- **Prueba HTTP E2E publicada:** `985531c16e1eb134bd53b75097c6291c98ec69fb` (rama `test/m08-http-manual-20261010`), padre exacto `e549fd92dcc167be98cae2d39c7966abe89b44ed`.
- Acta precedente M08-06: `0f1995ddcd6c9312f52284f64d134b14f02368a0`.
- Corte de `main`: `4a3fd1ef12fc2226bad185f003acea9168b9a4c9` (sin integración de M08).

## 2. Secuencia de ejecución (incidentes incluidos)

1. Primer ensayo HTTP: un verificador global detectó offsets duplicados **solo en otras worktrees**; identidad M08 91 aislada.
2. Segundo ensayo: la comprobación propia confundió la worktree M08 consigo misma; corregida sin tocar otras worktrees.
3. Tercer ensayo: `dotnet ef` no halló `project.assets.json` en la worktree recién creada. Se ejecutó `dotnet restore` de manera aislada.
4. Cuarto ensayo: EF Core migró los módulos del stack efímero y aplicó los seeds. La construcción Docker registró timeouts parciales de NuGet, **reintentó y concluyó**, levantando API y la instalación. La prueba cayó en `page.evaluate` porque una navegación destruyó el contexto antes del registro de cliente.
5. Quinto ensayo: se sustituyó únicamente el helper `registerAndLogin` de la spec por solicitudes `page.request` (cookie compartida con navegador), sin modificar código de producción ni arnés. TypeScript PASS; Docker cacheada; API instalada en PostgreSQL efímero; Playwright cerró con **1 passed, 0 failed, 0 errors, 0 skipped**, sin afirmar otras pruebas.
6. Durante los reinicios de activación/desactivación del host, Vite registró respuestas proxy `ECONNREFUSED` / `ECONNRESET`. Se conservan en el log; el test HTTP completo salió PASS, no deben eliminarse del expediente.
7. Global teardown eliminó contenedores, red y volumen **solo** del proyecto `sillar_m08_http_manual_e2e`. Las colisiones ajenas no fueron modificadas.

## 3. Lo realmente observado en HTTP

- Portal OFF: ruta no expuesta (404); ON: ruta disponible y sujeto derivado de cookie cliente.
- Solo cookie administrativa: rutas protegidas devuelven 401.
- Sesiones separadas de **cliente A y cliente B sin órdenes sembradas**: perfiles distintos; A conserva su identidad al enviar `?customerId=<otro>`.
- Sin proveedores opcionales M03/M06: estados `unavailable` y detalle 503.
- Con M03 activado sin pedidos: `orders.state=empty`.
- Con cadena M05a/M05b/M06 activa sin trabajos: `work.state=empty`; código inexistente devuelve 404.
- Portal OFF después del ensayo: 404 en M08; M04 permanece accesible.
- Ausencia de claves JSON seleccionadas `customerId`, `receivedNotes`, `trackingNotes`, `boardPriority`, `internalDueAt`, `performedBy`, `adminUserId`, `serviceOrderId` en el overview HTTP sin filas.

**NO probó**: órdenes A/B/NULL reales con propietario y sabotajes de aislamiento; los datos internos poblados; regresión completa; `[M08-CICLO]` de desinstalación/restaurabilidad y barrera de FK; certificación independiente. La compilación global completa sigue **HOLD (128 errores de analizadores xUnit ajenos según acta 06)**.

## 4. Evidencias inmutables

Directorio E2E local del ensayo aprobado (máquina JP): `/var/tmp/sillar-m08-http-fix-navegacion-20261010-TSvORsiX`.

| Archivo | SHA-256 |
|---|---|
| `e2e/tests/m08-portal-http-real.spec.ts` (publicado en rama de prueba) | `a2b9cb40517fbd7bb40637947b3dc045f5b6b8e269b8c8a4c169aa49f81df07e` |
| `http.log` | `fbdb0fcdc6e6aec735e68c3abc1580862cfd5f886779ae7557d0a2936e1543b6` |
| `m08-http-junit.xml` | `c34e5cc5f40b525f4e7af6b9e5e99b03f5ba0be142c1c39e9fb1e5a4c0e11587` |
| `identidades-globales.log` | `44306e6df76bbe40eafd0504a5966fd4d53bc867f844d185f2ff2fe7f4e4eba4` |
| `typecheck.log` | `8366207267355d3e3d5bf3bf6e8c94c5f93f6078c34f08973fa2b38cdda6cc92` |

La prueba se ejecutó por JP en un stack efímero. Ni Chat 2 ni el colíder han reproducido de manera independiente ese resultado. El expediente conserva los fallos previos y las trazas de reinicio, sin borrar evidencia roja.

## 5. Siguiente fase y HOLD

1. Preparar **07B** con dos clientes A/B y órdenes reales A, B y anónimas (NULL): asociación explícita M05b, códigos propios y ajenos, verificaciones no enumerables, detalles 404 para códigos de otro cliente. Probar datos privados reales, no solo JSON vacío.
2. Añadir matriz de rechazo de cookies y query spoofing al detalle, no solo al overview.
3. Ejecutar `[M08-CICLO]` con sabotajes y restaurabilidad sobre base efímera aislada y barreras de FK, con pruebas VERDE→ROJO→VERDE.
4. Resolver la deuda ajena de analizadores xUnit sin debilitar políticas; implementar UI e2e y repetir compuerta integral.
5. Colíder debe certificar de forma independiente; **JP decide cualquier integración a `main`**.

**Veredicto 07A:** prueba HTTP focal PASS 1/1, cero omisiones. **M08 continúa en desarrollo, sin certificación independiente, sin merge y con deuda global registrada.**
