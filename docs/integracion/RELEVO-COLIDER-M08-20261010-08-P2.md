# SILLAR — Relevo al colíder | Acta cronológica M08-08 (C2-R1, F4, Sales P0–P2)

**Corte documental:** 10 de octubre de 2026, posterior a P2 `pnpm typecheck` PASS (zona America/Lima).
**Responsable:** Chat 2, coordinación de integración técnica. **JP:** autoridad final de producto y merge.
**Proyecto:** repositorio privado `JP-777/SILLAR`.
**Estado general:** **M08 en HOLD de certificación y merge a `main`**. Se permite trabajo preparatorio y pruebas focales coordinadas sin alterar el candidato reservado para Chat 3.
**Clasificación de evidencia:** los logs de terminal se recibieron de JP; no se ha accedido ni ejecutado la laptop de JP desde este chat. Las lecturas del código vía conector GitHub se identifican como inspecciones remotas. El documento no acredita por sí solo QA independiente.

**Cadena documental del repositorio:** las actas previas 01–07C están publicadas en `docs/integracion/` de la rama `docs/m08-paso1-propuesta-20261009`; la última es `RELEVO-COLIDER-M08-20261010-07C.md` (07C real PASS). Este expediente consolidado incorpora los hitos posteriores. No fusionar sin autorización JP.

## 0. Reglas irrenunciables de coordinación

- JP aprueba cambios y autoriza el merge a `main`; el colíder emite **QA independiente** antes de esa autorización. Chat 2 no sustituye al colíder.
- **Un trabajo activo por agente**; no interrumpir ni sobrecargar. Codex está sin nuevas tareas por agotamiento de contexto. Chat 3 tiene reservada/asumida la **revalidación UI C2-R2**, sin evidencias todavía de ejecución ni resultado.
- Ninguna prueba Playwright simulada sustituye pruebas HTTP contra PostgreSQL real. Distinguir PASS, FAIL, HOLD, 'pendiente', y pruebas no realizadas. Cero skips para la puerta.
- No tocar `main`, ramas fuente C1/HTTP/documentación ni la worktree de integración de Chat 3; las pruebas Sales se desarrollan en **worktree aislada**. No ejecutar dos pruebas pesadas concurrentes, ni `docker compose down -v` ajeno, ni puerta canónica sin coordinación.
- Registrar SIEMPRE hallazgos rojos, correcciones, SHAs, ausencia de ejecución, trazas, verificaciones, y qué evidencia deriva de JP vs verificación remota.
- No hacer force-push, reset/clean destructivo, rebase/merge canónico sin autorización. No modificar componentes internos de módulos propietarios sin coordinación.

## 1. Foto Git conocida y candidatos

| Elemento | Rama | SHA | Evidencia/estado |
|---|---|---|---|
| `main` remoto, último SHA verificado previamente | `main` | `4a3fd1ef12fc2226bad185f003acea9168b9a4c9` | **Sin M08** al último control remoto; verificar antes de una decisión nueva |
| Diseño documental y ratificación P3.5 | `docs/m08-paso1-propuesta-20261009` | `360a24418eb0912b49551077cd254355f30a80df` | Publicada; decisiones JP ratificadas |
| HTTP 07C, pruebas manuales | `test/m08-http-manual-20261010` | `3c27cc7186bc9c9abca403c63121c741cf53ce61` | Publicada |
| C1 UI original | `feature/m08-portal-ui` | `3f8692e0d5db2b4b1e2ef62083937da1aac854bd` | Publicada, inalterada |
| C2-R1 Codex, 2 tests | `fix/m08-portal-ui-tests-c2-r1` | `6b190ea2821727c2069ba0aa72722db3f0e47e72` | Remoto verificado por Chat 2; +5/−4 en solo `e2e/tests/m08-portal-ui.spec.ts` |
| Composición C1 + 07C | worktree de integración | `c350525fede3a664b85add2a70289db8e51cdd11` | Merge local aislado |
| Costuras originales frontend | rama de integración | `149bae50784e6858917e75b2ad1d830262dfccb4` | Candidato previo C2 |
| Cherry-pick de C2-R1 | rama de integración | `4c6580f18c18ef894c8bc357b706847775a5deed` | JP reportó apply PASS |
| Reparación F4, HEAD vigente de integración | `integration/m08-c1-http-07c-20261010` | **`58684e370905998ea89049633aa5a15a3926e9da`** | **Candidato a revalidar**, no certificado; JP reportó apply PASS |
| Worktree Sales P1, derivada sin commits nuevos | `test/m08-sales-poblado-20261010-2` | **`58684e370905998ea89049633aa5a15a3926e9da`** | Preparación P2 sin commit; NO modificar el candidato upstream |

**Precaución:** no tomar el SHA de `main` registrado aquí como valor actual sin volver a contrastar el remoto en el momento crítico.

## 2. Arquitectura contractual M08

- M08 Portal es **solo lectura** y **sin schema/DDL/migraciones/semillas** propios. Duras `core`,`crm`; blandas `sales`,`tracking`.
- Sujeto server-side `ICurrentCustomer.CustomerId` de M04, nunca tomado de query/URL/cuerpo.
- M03 provee historial por `ICustomerOrderHistory`; el backend M08 no consulta ni modifica directamente `sales.*`. M03 filtra en SQL por `CustomerId` e `IsActive` antes de proyectar, y el detalle `GET /api/sales/my-orders/{orderCode}` incluye dueño en la consulta; pedido ajeno e inexistente deben producir 404 indistinguible.
- Proveedor faltante, vacío y error deben diferenciarse (`unavailable`,`empty`,`error`); un proveedor no debe romper al otro.
- M05b/M06: seguimiento del cliente con propiedad A/B/NULL y DTO público sin notas ni metadatos internos. 404 ajeno/inexistente. Sin GUID internos en UI ni API de Portal.
- P3.5 JP ratificó D-UI-01 a 05: cinco rutas del Portal, guard CRM, portada «Ir a Mi portal», navegación local sin barra global nueva, máximo 3 pedidos y 3 trabajos recientes, `capabilities` y 503 sin atribución interna; aprobación estructural no es certificado visual definitivo.

## 3. Historial de validaciones: lo aprobado y sus límites

### 3.1 Contratos y backend

- Backend M08 implementado; unitarias Portal 6/6 PASS reportadas históricamente. Contratos de otros dueños fueron revisados en su fase previa, no implica certificación global.
- Diseño P3.5 ZIP SHA-256 `065234263f9c0ff03a187aadfd36cefea85466a14e6ddd84cc3fe7ed05aaaeb4`; hashes internos 8/8 verificados históricamente.

### 3.2 Manual HTTP y PG (reportado por JP)

| Frente | Resultado | Límite |
|---|---|---|
| 07A | PASS 1/1 | Humo API activación y sesiones, sin ventas pobladas |
| 07B | PASS 1/1 | Trabajos M05b/M06 A/B/NULL reales, 404 y privacidad, no pedidos Sales M03 |
| 07C | PASS 1/1 | Pre-ciclo OFF/ON, migración+seed, tablas de dueños preservadas, esquema Portal ausente; **no `[M08-CICLO]` integral** |
| 07D VRV **unitario Node** | PASS 3/3 rojo→verde | Sabotajes `hard_crm`, `sales_identity`, `json_privacy`; **no equivale a sabotajes HTTP 07D** |

Evidencia histórica 07C: `/var/tmp/sillar-m08-preciclo-api-20261010-DKtCedr5` (laptop JP). 07D unitario: `/var/tmp/sillar-m08-07d-vrv-unit-Jre1GT`; salida final `M08_07D_VRV_UNIT=3/3_VERDE_ROJO_VERDE` y `M08_07D_HTTP_SABOTAJES=PENDIENTES`.

### 3.3 C1, C2 y reparación

- C1 UI de Codex: P1–P6 implementadas; 9 escenarios Playwright escritos; compila y typecheck publicados. C1 no tenía costuras centrales propias.
- Integración original `149bae5`: 07C + C1 + 2 costuras `frontend/src/app/routes.tsx` y `frontend/src/platform/homeSections.ts`; no merge en main.
- **C2** auditó `149bae5`: frontend typecheck PASS; build PASS (228 módulos); higiene PASS 18/18; E2E typecheck PASS; **fronteras FAIL (F4)**; **Playwright FAIL 7/9, 2 fallos, 0 skipped**. Diagnóstico: import directo no permitido a `modules/crm/session` en app/routes; test P2 dependía de contador bruto de requests afectado por React StrictMode; selector A→B coincidía ambiguamente con «Administrar mi cuenta». A→B real NO acreditado.
- Codex **C2-R1** remoto `6b190ea...`: solo test UI +5/−4; P2 usa fases explícitas `empty → error`; navegación A→B acota «Mi cuenta» con `exact: true`. No ejecutó nueve tests sin costuras.
- JP ejecutó `--audit` PASS y `--apply` PASS de script de integración C2-R1+F4. Commits locales resultantes `4c6580f...` (cherry-pick tests) y **`58684e3...`** (M04 expone `RequireCustomerAuth` por `modules/crm/routes`; `app/routes.tsx` importa la superficie permitida); según consola: `MAIN_MODIFICADA=NO`, `PUSH_EJECUTADO=NO`, `E2E_UI_9=NO_EJECUTADOS_EN_ESTE_PASO`, `QA_INDEPENDIENTE=PENDIENTE`.
- **C2-R2**: pendiente de Chat 3; no hay todavía evidencia de fronteras PASS ni UI 9/9 sobre `58684e3`.

## 4. Nuevo frente manual exclusivo Sales — P0, P1 y P2

### P0 — Auditoría de solo lectura (PASS reportado por JP)

- Script `SILLAR_M08_SALES_P0_PREVUELO_READONLY_20261010.sh` `--audit` PASS: contratos y candidato `58684e3` verificados; worktree integración limpio, herramientas Node/pnpm/dotnet/Docker presentes.
- Offset 64 en worktree integración; propuesta Sales offset 72 sin choques observados; identidades calculadas, no reserva formal de puerto Docker.
- `M08_SALES_P0_PG_HTTP=NO_EJECUTADO` y `M08_SALES_P0_QA=HOLD`.

### P1 — Creación de worktree Sales (PASS reportado por JP)

JP ejecutó `SILLAR_M08_SALES_P1_WORKTREE_AISLADA_20261010.sh --audit && --apply` con ambos PASS.

- **Worktree:** `/home/JP777/sillar-m08-sales-poblado-20261010-2`.
- **Rama:** `test/m08-sales-poblado-20261010-2`.
- **HEAD inicial:** `58684e370905998ea89049633aa5a15a3926e9da`.
- **Proyecto Compose E2E:** `sillar_m08_sales_poblado_20261010_2_e2e`.
- **Offset:** 72; puertos 55972 PostgreSQL, 56072 API, 56172 Vite, 56372 Mailpit; guardas detectaron 0 choques.
- Salida literal: `M08_SALES_P1_APPLY=PASS`; `FUENTES_MODIFICADAS=NO`; `COMMIT_PUSH_MERGE_MAIN=NO`; `DOCKER_POSTGRESQL=NO_EJECUTADO`; `QA=HOLD`.

### P2 — Preparación de spec poblada (PASS preparación; NO QA)

**Transcripción recibida en el chat, 10/10/2026:** JP hizo `unzip -o ...`, `bash -n ...`, `--audit` y `--apply` sobre paquete `SILLAR_M08_SALES_P2_PREPARAR_SPEC_20261010.zip`.

**Auditoría P2:**

```text
M08_SALES_P2_E2E_PROYECTO=sillar_m08_sales_poblado_20261010_2_e2e
M08_SALES_P2_E2E_OFFSET=72
M08_SALES_P2_E2E_PUERTOS=55972,56072,56172,56372
M08_SALES_P2_CHOQUES=0
M08_SALES_P2_BRANCH=test/m08-sales-poblado-20261010-2
M08_SALES_P2_HEAD=58684e370905998ea89049633aa5a15a3926e9da
M08_SALES_P2_SPEC_SHA256=5e947a1d33f39993224259383799327b08ae2ac6365d8ef5917fcba80131d620
M08_SALES_P2_AUDIT=PASS
M08_SALES_P2_SPEC=NO_CREADA
M08_SALES_P2_DOCKER_POSTGRES=NO_EJECUTADO
M08_SALES_P2_QA=HOLD
```

**Aplicación P2:**

```text
M08_SALES_P2_APPLY=PASS
M08_SALES_P2_SPEC=PREPARADA_SIN_EJECUTAR
M08_SALES_P2_FUENTES_PRODUCTIVAS=INTACTAS
M08_SALES_P2_DOCKER_POSTGRES=NO_EJECUTADO
M08_SALES_P2_COMMIT_PUSH_MERGE=NO
M08_SALES_P2_TYPECHECK=PENDIENTE_DEPENDENCIAS_NO_INSTALADAS
M08_SALES_P2_QA=HOLD
```

**Archivo preparado por el script**, solo en la worktree exclusiva:

`/home/JP777/sillar-m08-sales-poblado-20261010-2/e2e/tests/m08-sales-poblado-http.spec.ts`

**SHA-256 de contenido:** `5e947a1d33f39993224259383799327b08ae2ac6365d8ef5917fcba80131d620`. Se cotejó con la fuente del ZIP de P2 en el entorno del asistente, **no se ha releído el archivo escrito en laptop**.

**Contenido funcional PREVISTO de la spec, no demostrado:** crea dos sesiones M04 A/B y clientes únicos, usa SQL **solo como fixture en base efímera** para marcar correo verificado (NO valida SMTP); crea producto real por admin M01; configura `sales.order_series_label='P'` exclusivamente para nodo de prueba `principal` (seed M03 no la define); activa M03+M08; hace `POST /api/sales/orders` reales por A y B; comprueba dueño en `sales.orders`; lista Sales propia y detalles, 404 ajeno/inexistente, anonimato 401, portal overview con los códigos visibles correctos, negativa a suplantar por `?customerId=`, ausencia de claves internas, restituye módulos activados.

**Advertencia técnica:** la spec no sustituye `[M08-CICLO]`, pruebas de sabotaje HTTP, ni QA independiente. **Typecheck E2E PASS** tras instalar dependencias con lockfile congelado; falta ejecutar Playwright y verificar contenedores/DB. No limpiar la worktree ni asumir commit: `P2_COMMIT_PUSH_MERGE=NO`.


### P2 — Resolución posterior del typecheck (PASS, evidencia de consola JP)

JP ejecutó en la worktree exclusiva de Sales los comandos:

```bash
cd /home/JP777/sillar-m08-sales-poblado-20261010-2
sha256sum e2e/tests/m08-sales-poblado-http.spec.ts
cd e2e
pnpm install --frozen-lockfile
pnpm typecheck
git status --short
```

Observaciones literales del terminal:

- SHA-256 de la spec: `5e947a1d33f39993224259383799327b08ae2ac6365d8ef5917fcba80131d620`; **coincide** con P2.
- Instalación: `Lockfile is up to date`, 8 paquetes reutilizados, sin descarga; `Done in 1.5s using pnpm v11.24.0`.
- E2E typecheck: `$ tsc --noEmit`, salida sin diagnósticos, proceso continuó a `git status`, **PASS**.
- Estado Git: `?? tests/m08-sales-poblado-http.spec.ts` — la spec continúa sin commit.
- No se invocó Playwright, no se levantó PostgreSQL/Docker, no se comprobó aún el escenario con ventas reales.

**Interpretación correcta:** tipado PASS no equivale a escenario funcional PASS ni QA del colíder.

## 5. Pendientes, responsables y secuencia segura

| Paso | Responsable | Estado |
|---|---|---|
| C2-R2: F4, build, higiene y UI Playwright 9/9 sobre `58684e3` | Chat 3 | PENDIENTE: no adjudicar PASS |
| Sales P2: dependencias E2E y `pnpm typecheck`, exclusivamente worktree Sales | Chat 2 + JP | **PASS**, salida real de JP recibida |
| Sales poblado M03↔M08 real: una corrida Playwright con PG efímero y evidencias | Chat 2 + JP | Typecheck PASS; **HOLD de ejecución** hasta comprobar concurrencia Docker/Chat 3 |
| `[M08-CICLO]` real OFF→ON→RESTORE, preservación de dueños | Coordinación Chat 2 | PENDIENTE |
| Sabotajes 07D HTTP con matriz H01–H25 / VRV real | Coordinación Chat 2 | PENDIENTE; no confundir 07D unitario |
| Puerta `node scripts/verificar.mjs` canónica; tratar 128 fallos xUnit históricos separadamente | Coordinación; Chat 3 según capacidad/autorización JP | PENDIENTE, no ejecutar concurrente |
| Auditoría independiente | Colíder Claude B | PENDIENTE por ausencia/disponibilidad |
| Merge a `main` y postmerge | JP autoriza | **HOLD** |

**Regla de progreso inmediato:** dependencias y typecheck E2E ya aprobaron. Antes de cualquier prueba PostgreSQL, comprobar que no hay ejecución pesada de Chat 3, identidades/offsets, puertos y propiedad de contenedores. Ejecutar solo suite focal, guardar trazas/resultados y no publicar PASS sin log.

## 6. Relevo al colíder (lista de auditoría)

Revisar contratos D1–D6 y D-UI-01…05, integridad del ZIP de diseño, ancestría C1 + 07C, costuras app/home, reparación F4 y diff de C2-R1, el fallo C2 original 7/9 y posterior repetición, resultados 07A/B/C, sabotaje Node 3/3 con sus pruebas rojas verificables, matrices de HTTP y M08-CICLO, Sales P0–P2 incluyendo `typecheck` PASS, aislamiento físico y lógico de escenarios, resultados canónicos y preservación de dueños, sin asumir que una preparación equivale a QA. **El dictamen del colíder continúa pendiente.**

## 7. Referencias de artefactos / continuidad

- Histórico anterior: `SILLAR_CHAT2_RELEVO_INTEGRAL_M08_20261010(1).md`, `SILLAR_CHAT2_MIGRACION_INTEGRAL_M08_20261010(1).zip` (útiles para antecedentes, pero el estado operativo allí es ANTERIOR al actual).
- Actas incrementales: `SILLAR_M08_ACTA_CHAT2_COLIDER_20261010_SALES_P0.md`, `...SALES_P1.md`, `...SALES_P2.md`.
- Scripts P0, P1 y P2 están empaquetados en sus ZIP específicos; P2 también contiene la spec TypeScript original con hash arriba.
- La siguiente migración deberá adjuntar ESTE documento, no solo el histórico antiguo. Si surge un PASS/FAIL/HOLD nuevo, añadir un corte con timestamp y ruta de evidencia antes de abandonar el chat.

**Dictamen al corte:** **Sales P2 = preparación + integridad SHA + typecheck PASS; ejecución real HOLD. M08 = no certificado, `main` intacta según los últimos logs de JP; QA colíder y aprobación JP pendientes.**
