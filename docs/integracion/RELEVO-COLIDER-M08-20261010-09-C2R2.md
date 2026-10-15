# SILLAR — Relevo al colíder | Acta cronológica M08-09 (C2-R2)

**Corte:** 2026-10-10, posterior a la publicación del acta 08; hora exacta de cierre del ensayo no provista (America/Lima).
**Responsable del ensayo:** Codex, cierre de su intervención asignada antes del relevo a Chat 3.
**Responsable de documentación:** Chat 2. **Fuente:** informe textual de ejecución entregado por JP; algunos detalles de la implementación de test contrastados en GitHub.
**Estado:** **C2-R2 HOLD, 8/9 Playwright; M08 NO CERTIFICADO; `main` HOLD.**

## 1. Encadenamiento de la documentación

Acta anterior publicada: `docs/integracion/RELEVO-COLIDER-M08-20261010-08-P2.md`, rama `docs/m08-colider-acta08-p2-20261010`, commit remoto `7f789c10c2b5a07b1e15edea9a8498abf6ee60c9` (verificado por Chat 2 en GitHub). Esta acta **09-C2R2** es un registro incremental y no reescribe ninguna anterior.

Además existe en la Biblioteca `/SILLAR/M08/` un borrador separado **«Acta 09 — Prevuelo P3 Sales»**; es una planificación de ejecución, no evidencia de que Sales se haya corrido. Evitar fusionar cronológicamente eventos diferentes.

## 2. Identidad congelada del candidato auditado

| Campo | Referencia |
|---|---|
| Rama local integrada | `integration/m08-c1-http-07c-20261010` |
| Candidato auditado | `58684e370905998ea89049633aa5a15a3926e9da` |
| Corrección C2-R1 incorporada | `4c6580f18c18ef894c8bc357b706847775a5deed` |
| Worktree local JP | `/home/JP777/sillar-m08-c1-integration-20261010` |
| Árbol | Limpio, según informe Codex |
| Stack Playwright | Efímero; destruido al terminar, según informe Codex |
| Escrituras Git | Sin modificaciones, commits, pushes ni merges en C2-R2, según informe |

No atribuir el candidato local `58684e3` a un commit remoto: esta acta no constituye publicación del candidato.

## 3. Comprobaciones ejecutadas, **según informe C2-R2**

| Prueba | Resultado informado |
|---|---|
| `pnpm typecheck` (frontend) | **PASS** |
| `pnpm build` (frontend) | **PASS**, 228 módulos |
| `pnpm test:fronteras` | **PASS**, 34/34, 0 skipped |
| `pnpm test:frontend-hygiene` | **PASS**, 18/18, 0 skipped |
| `pnpm typecheck` (E2E) | **PASS** |
| `Playwright M08 UI` | **FAIL**, **8 passed / 1 failed / 0 skipped** |

La herramienta de fronteras examinó **194 archivos y 896 imports**, sin violaciones ni excepciones de composición sin uso. El arreglo F4 de la importación de `RequireCustomerAuth` por `modules/crm/routes` quedó validado. `app/routes.tsx` no importa directamente `modules/crm/session`.

Comparación cronológica: C2 original informó **7/9 UI** y F4 FAIL; C2-R1 corrigió dos aspectos del arnés; C2-R2 acredita **8/9 UI** y fronteras PASS. **No es 9/9**.

## 4. Único caso UI pendiente: cambio de cliente A → B

**Caso que falla:** `M08 UI — cambiar de cliente invalida el contenido de A`.

**Síntoma reportado:** el test abre `/mi-portal` con identidad inicial simulada A y comprueba `PEDIDO-ALFA`; al visitar `/mi-cuenta`, ejecuta `GET /api/customer/profile` sin mock correspondiente. El backend real responde **401** porque no hay cookie real. `customerHttp` invalida la sesión y redirige a `/entrar`; por eso no aparece el botón **«Cerrar sesión»**. El test no llega a ejecutar el cambio a B. La traza reporta **dos respuestas 401** de `/api/customer/profile`.

**Contraste del código remoto C2-R1:** `e2e/tests/m08-portal-ui.spec.ts` en `6b190ea2821727c2069ba0aa72722db3f0e47e72` solo registra mocks de `setup/status`, `capabilities`, `admin/auth/me`, `customer/auth/me` y `settings/public` en `prepareBoot`. El caso A→B sí registra mocks de `portal/overview`, `customer/auth/logout`, `customer/auth/login`, **pero no de `customer/profile`**. Asimismo `prepareBoot` captura un cliente fijo A para `customer/auth/me` mientras el test cambia `current` a B. Esta segunda inconsistencia debe considerarse para que el test pruebe el cambio de identidad sin reintroducir A después del login.

**Interpretación:** fallo confirmado en el **escenario simulado**, no demostración de un defecto productivo de invalidación, ni tampoco prueba aprobada de A→B. Corregir solamente el test para simular de forma coherente la identidad A → anónimo → B, la respuesta de `/api/customer/profile` y el logout/login; después ejecutar 9/9 y conservar nuevas trazas. No alterar M04, M08 productivo, contratos ni módulos ajenos por un mock incompleto.

## 5. Lo que pasó en los ocho escenarios restantes

- P2: carga → vacío → error 503 recuperable bajo StrictMode.
- Límites de tres elementos; independencia de proveedores.
- Estados HTTP 401/404/503 en UI simulada.
- No exposición de datos privados en las vistas probadas.
- Navegación local, móvil, tema oscuro y auditoría axe.

**Límite de esta evidencia:** los casos UI utilizan respuestas controladas. El hecho de que el arnés arrancara PostgreSQL y migraciones **no convierte estos ocho casos en comprobaciones HTTP reales con pedidos Sales poblados ni en acreditación de propiedad multiusuario**.

## 6. Evidencias locales remitidas por JP

Ruta HTML: `/home/JP777/sillar-m08-c1-integration-20261010/e2e/playwright-report/index.html`
SHA-256 reportado: `11f05412a6c27644c07ce09aab067ee1df9be902b43ed9fd5074ae269927d1db`.

Ruta de traza (dentro del mismo árbol): `e2e/test-results/m08-portal-ui-M08-UI-—-cam-d493f--invalida-el-contenido-de-A-chromium/trace.zip`
SHA-256 reportado: `d2e0960eb33445267a2ae9fe6276dfef131307e5b1a65989b8e0f9d521fcea31`.

Existe además `error-context.md` en esa carpeta. **Estas rutas/hashes se recibieron en el informe de JP; Chat 2 no ha leído los binarios locales ni comprobado personalmente los hashes.** Se conservan para auditoría independiente.

## 7. Frentes y HOLD tras C2-R2

1. **Codex:** último trabajo entregado. No asignar más tareas por restricción de contexto.
2. **Chat 3:** reparar exclusivamente la preparación del escenario A→B en una rama de tests y revalidar 9/9 + F4/higiene según corresponda. **No proclamar 9/9 hasta ejecutarlo**.
3. **Chat 2 + JP:** Sales M03 poblado P3 preparado, con spec `e2e/tests/m08-sales-poblado-http.spec.ts`, SHA-256 `5e947a1d33f39993224259383799327b08ae2ac6365d8ef5917fcba80131d620`; typecheck PASS, prueba HTTP/PG todavía no ejecutada. Worktree `/home/JP777/sillar-m08-sales-poblado-20261010-2`, offset E2E 72 y puertos 55972/56072/56172/56372. Prevuelo detectó otros contenedores SILLAR activos, ninguno con estos puertos según listado proporcionado. Antes de ejecutar verificar de nuevo exclusión y propietario del stack.
4. **Posterior:** 07D HTTP sabotaje, `[M08-CICLO]` integral, puerta canónica 6/6 y QA independiente del colíder siguen pendientes; antecedente del build global rojo con 128 errores de analizadores xUnit no puede darse por corregido.
5. **JP:** único responsable de autorizar una integración a `main`; **HOLD**.

**Dictamen del corte 09-C2R2:** frontera F4 y estáticas PASS, UI 8/9 con A→B pendiente. Sales poblado P3 sin ejecutar. M08 **NO CERTIFICADO**.
