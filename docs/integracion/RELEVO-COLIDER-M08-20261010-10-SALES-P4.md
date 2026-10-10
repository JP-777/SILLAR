# SILLAR — Relevo al colíder | Acta 10: M08↔M03 Sales poblado, P4 PASS focal

**Fecha del corte:** 2026-10-10 (America/Lima). **Responsables:** JP ejecutó en su equipo, Chat 2 revisó salida de consola y documentó. **Estado:** Playwright HTTP + PostgreSQL **PASS focal, 1/1** después de recuperación P3→P4. **No equivale a certificación integral ni a QA independiente.**

## 1. Referencias y preservación de ramas

- Antecesor documental publicado: `docs/integracion/RELEVO-COLIDER-M08-20261010-09-C2R2.md`, rama `docs/m08-colider-acta09-c2r2-20261010`, SHA exacto `102de797931fe8e6af1f771852ec67c6bb12c0d3`.
- Repositorio: `JP-777/SILLAR`, `main` remoto observado en `4a3fd1ef12fc2226bad185f003acea9168b9a4c9` antes de publicación documental; **sin integración M08**.
- Worktree de pruebas, separada del candidato UI: `/home/JP777/sillar-m08-sales-poblado-20261010-2`, rama `test/m08-sales-poblado-20261010-2`, HEAD `58684e370905998ea89049633aa5a15a3926e9da`.
- Único archivo de trabajo no versionado: `e2e/tests/m08-sales-poblado-http.spec.ts`; SHA original `5e947a1d33f39993224259383799327b08ae2ac6365d8ef5917fcba80131d620`; **nuevo SHA-256 de P4** `7ffd865719c4387cafeeb46199559f5f19101a4609c07944ff841db4f8b9eb55`.
- La corrección P4 es exclusivamente del **test**: al comparar `404` ajeno e inexistente excluye el identificador variable `traceId` y mantiene la equivalencia de propiedades del error y las verificaciones de estado y privacidad. El API productivo no se modifica.

## 2. Cadena de prueba y recuperación

1. **P3 primer arranque:** `NETSDK1004` por ausencia de `backend/Sillar.Core/obj/project.assets.json`; se resolvió con `dotnet restore backend/Sillar.Api/Sillar.Api.csproj`, salida reportada `Build succeeded`.
2. **P3 primera corrida real:** migraciones, montaje API/PG y preparación de pedidos reales avanzaron; Playwright terminó 1 FAIL por comparar íntegros dos ProblemDetails `404` cuyos `traceId` diferían naturalmente entre peticiones. Evidencias locales: `/var/tmp/sillar-m08-sales-p3-20261010-az7okD`. No se presentó como defecto productivo.
3. **P4 auditoría local:** `M08_SALES_P4_AUDIT=PASS`, hash de origen verificado; único cambio en spec, sin commit a fuentes. `M08_SALES_P4_PATCH=PASS`; `pnpm typecheck` (tsc --noEmit) **PASS**; descubrimiento 1 escenario Playwright.
4. **P4 ejecución real:** script `SILLAR_M08_SALES_P4_CORREGIR_Y_REPROBAR_20261010.sh --all`, que invocó el P3 controlado con el SHA actualizado y stack dedicado. `M08_SALES_P3_AUDIT=PASS` en la corrida, colisiones = 0, 1 worker.
5. **Entorno real:** migraciones CORE, Catalog, CMS, CRM, Services, ServiceOrders, Tracking, Sales y B2B completadas; Docker construyó/levantó API y PostgreSQL; instalación, seeds, módulos y Vite concluyeron. La salida incluye `Failed executing DbCommand` al consultar historial de migraciones iniciales de esquemas nuevos, pero los comandos siguieron hasta `Done`; **no usar esos avisos como evidencia de un fallo final**.
6. **Resultado observado:** el test escribió `[M08-SALES-POBLADO] PASS: 2 pedidos HTTP/PG, A/B aislados, 404 uniforme, privacidad`; Playwright: **`1 passed (4.6m)`**, cero fallos, una prueba total. El proceso devolvió `M08_SALES_P3_PLAYWRIGHT_RC=0`, `M08_SALES_P3_RESULTADO=PASS_FOCAL_SUJETO_A_AUDITORIA`, `M08_SALES_P4_PLAYWRIGHT_RC=0`, `M08_SALES_P4_RESULTADO=PASS_FOCAL_SUJETO_A_REVISION_INDEPENDIENTE`.
7. **Limpieza:** teardown mostró eliminación de contenedores API/DB, volumen y red del proyecto `sillar_m08_sales_poblado_20261010_2_e2e`; no afecta otros stacks. El script informó `M08_SALES_P3_MAIN=NO_TOCADA_POR_SCRIPT`. Durante reinicios aparecieron `ECONNREFUSED` y `ECONNRESET` de proxy Vite, pero la corrida pasó; preservar esas líneas en evidencia, no ocultarlas.

## 3. Evidencia material y límites

- Evidencia local JP: **`/var/tmp/sillar-m08-sales-p3-20261010-KCEy8d`**. El script informó **`M08_SALES_P3_JUNIT=PRESENTE`**; sin archivos binarios o contenido JUnit adjuntos a Chat 2 en este corte, por lo que no inventamos hashes de log, XML, capturas o trazas.
- Spec final SHA-256: `7ffd865719c4387cafeeb46199559f5f19101a4609c07944ff841db4f8b9eb55`. Conservar sin `git clean` o `reset`; todavía no hay commit de esa spec.
- Producto: dos pedidos reales creados mediante HTTP sobre Sales M03 con PostgreSQL efímero; clientes A y B separados; propio visible; ajeno e inexistente ambos `404`; comparativa uniforme sin `traceId`; privacidad y resúmenes M08 completados conforme a las aserciones del escenario. Este es **un ensayo focal**; no demuestra por sí solo seguridad completa del sistema.
- El texto de consola fue facilitado por JP y revisado por Chat 2; **QA independiente del colíder no realizado**.

## 4. Barreras restantes

| Frente | Estado |
|---|---|
| M03 Sales poblado ↔ M08 HTTP/PG (P4) | **PASS focal 1/1** |
| Frontend F4, typecheck, build, fronteras, higiene | **PASS** en informe C2-R2 previo |
| UI C2-R2 Playwright | **8/9**, falla escenario A→B por mocks incompletos de `/api/customer/profile` y sesión fija A; requiere corrección exclusiva del test y nueva prueba completa |
| 07D sabotaje HTTP real | **PENDIENTE**; no sustituir por VRV unitario |
| `[M08-CICLO]` integral de etapa 6 | **PENDIENTE** |
| Puerta canónica `node scripts/verificar.mjs` | **HOLD**; registrar antecedente de analizadores xUnit globales, no declarar resuelto |
| QA independiente del colíder | **PENDIENTE** |
| Autorización JP / integración a `main` | **HOLD**, prohibido merge sin decisión expresa |

## 5. Relevo inmediato

- **Chat 3**: recibir encargo nuevo para corregir el escenario UI A→B sin tocar producto, con transición controlada A → anónimo → B y mocks coherentes de `auth/me` y `customer/profile`; acreditar 9/9 y conservar traza del 8/9 previo.
- **Chat 2**: preservar evidencia P3 FAIL + P4 PASS, registrar hash y protección de la spec no commit; preparar, de manera separada, sabotaje HTTP 07D y `[M08-CICLO]` sin suites concurrentes.
- **Colíder**: revisar el expediente y las pruebas de manera independiente. La presente acta no constituye su dictamen.
- **JP**: preservar `main`, autorizar cualquier integración o publicación de código productivo.

**Dictamen del acta 10:** M08↔Sales real con pedidos A/B = **PASS focal**. M08 global = **HOLD** para UI A→B, ciclo, sabotaje HTTP, puerta canónica, QA independiente y decisión de JP.
