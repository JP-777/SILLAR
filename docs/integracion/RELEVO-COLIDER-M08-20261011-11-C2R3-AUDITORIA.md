# SILLAR — Acta 11 | Auditoría independiente de Chat 2: C2-R3 UI

**Corte:** 11 de octubre de 2026 (America/Lima).
**Dictamen de Chat 2:** **ACEPTADO, PASS focal C2-R3**, sin certificar M08 y sin autorizar merge a `main`.
**Origen material:** paquete entregado por JP, `sillar-m08-c2-r3-auditoria-independiente-SqpWMyor.zip`, SHA-256 `d4350ebf0dfdf8886bbf20cf0acadc3c6dc3100afee1c3a0ced99dc38ac9cc97`.
**Método:** inspección independiente de contenido ZIP, integridad SHA-256, diff, aplicación inversa del parche, blobs Git reconstruidos, XML JUnit, registro Playwright y límites del alcance. **No se reejecutaron pruebas ni se accedió al equipo de JP.**

## 1. Identidad y control de alcance

- Rama local reportada: `fix/m08-c2-r3-identity-ui-20261010`.
- HEAD local reportado: `d764fa68eea7a9d3477cb761ffbbecc885e52f3d`.
- Parent/base congelada reportada: `58684e370905998ea89049633aa5a15a3926e9da`.
- Único archivo de código presente en el delta: `e2e/tests/m08-portal-ui.spec.ts`; **74 inserciones y 6 eliminaciones**. `archivos_cambiados.txt`, `estadistica.txt` y el parche coinciden.
- El script local reporta árbol limpio, candidato original inalterado y sin push. Chat 2 **no realizó una consulta al git local directamente**. La rama y el SHA del candidato C2-R3 no están publicados en GitHub; por tanto, no se afirma una verificación remota del objeto commit.
- El cambio es exclusivo de las respuestas controladas del test UI. No contiene modificaciones al código productivo.

## 2. Auditoría material independiente

- **13 de 13** sumas SHA-256 del manifiesto raíz del ZIP fueron recalculadas sobre los archivos contenidos y coincidieron.
- SHA-256 real del archivo final `TEST_FINAL_m08-portal-ui.spec.ts`: `6f919cd20babeba6a508f182b6d91c24fc8fd7c3029f92e8883187ce441af52d`.
- SHA-256 del diff incluido `C2R3_DIFF_AUDITAR.patch`: `77f3c2caaee62de2e3cda8ec6d1d259979e1565991e5221686209fe1d3384c9b`.
- `git apply --reverse --check` y la reversión del parche contra el archivo final tuvieron éxito, reconstruyendo el original. El blob Git del archivo final es `64b227b103bd948a666af45c342bfb0fa3f93859` y el del archivo original reconstruido `863e4cea77efecca5e211033302370ba8edc835d`, exactamente los prefijos `64b227b` y `863e4ce` declarados en el encabezado de diff.
- El `SPEC_HASH=FAIL` del auditor automático previo **no es defecto del entregable**: el SHA `0e1628f85dc3ffda37b4dead83de389044d71223932ab8fc139c54e531244053` se declaró como **hash de un archivo de parche local** (`m08-c2-r3.patch`), no del archivo `.spec.ts`. Ese parche original **no está incluido en el ZIP**; por ello su hash específico no fue recalculable desde este paquete. La reconstrucción sí valida el diff exportado y el archivo final.

## 3. Juicio del cambio A → B

El diff corrige el caso `M08 UI — cambiar de cliente invalida el contenido de A` de modo pertinente:

1. `/api/customer/auth/me` usa identidad mutable `A → null → B`, en lugar del cliente A fijo.
2. `/api/customer/profile` recibe un mock compatible con el contrato del frontend (identidad, teléfono, documento, direcciones), con `401` sin sesión.
3. Logout pone `current=null`; login pone `current=CUSTOMER_B`, sin afirmar que sean respuestas del backend real.
4. Verifica `PEDIDO-ALFA` visible al inicio y nombre A en Mi cuenta; posteriormente nombre B, `PEDIDO-BETA` presente y `PEDIDO-ALFA` ausente.
5. Se mantuvieron los demás ocho escenarios, sin `test.skip` ni desactivación de aserciones observadas en el parche.

**Límite:** es una prueba de interfaz con rutas HTTP simuladas. La propiedad real A/B fue probada aparte en M08↔Sales P4 (HTTP/PostgreSQL, 1/1 PASS), recogida en el Acta 10.

## 4. Resultados de prueba y material de respaldo

- JUnit XML contenido en el paquete y analizado directamente: `tests=9`, `failures=0`, `errors=0`, `skipped=0`, con **nueve nodos `<testcase>`**, incluido cambio A→B.
- Registro Playwright incluido confirma `Running 9 tests using 1 worker`, cada uno aprobado y cierre **`9 passed (6.0m)`**.
- Evidencia del typecheck: log `typecheck-e2e.log` (`$ tsc --noEmit`), hash validado; la entrega declara salida cero, pero el log por sí solo no incorpora un `exit code` explícito.
- `dotnet-restore.log`, `manifest.txt`, `playwright-m08-ui.log`, `m08-portal-ui.junit.xml` y `.last-run.json`: hashes cotejados contra el manifiesto ZIP; el manifiesto interno de Playwright coincide para los archivos efectivamente incluidos.
- No se adjuntan trace, video ni capturas porque la corrida pasó. Se detectan mensajes esperables de EF al buscar historial de migraciones inicialmente ausente, seguidos por aplicación completa y teardown de Docker.
- Evidencia local original según la entrega de Chat 3: `/var/tmp/sillar-m08-c2-r3-playwright2-20261011-010348`.

## 5. Control documental y de integración

- Cadena documental remota anterior: Acta 10, `docs/m08-colider-acta10-sales-p4-20261010`, SHA `515c79155ec8d372c9e168e8eccc9f6e7ba0b333`.
- `main` observada en GitHub por Chat 2 en `4a3fd1ef12fc2226bad185f003acea9168b9a4c9` al auditar; la entrega local de Chat 3 informó `8911f9a12847f153fb7e4909119b10837e196fe6`. **No se asume equivalencia ni se explica la discrepancia sin comparar historiales**. Esta acta no toca `main`.
- **C2-R3 aceptado como prueba focal UI y parche limitado a tests.** Aceptación no implica merge ni certificación integral M08.
- Pendientes explícitos: QA independiente del **ciclo integral** M08, sabotaje HTTP 07D en su alcance exigido, barreras canónicas correspondientes y resolución o disposición fundada de incidencias de build global anteriores. El colíder debe revisar las evidencias finales y JP decidir cualquier integración.

**Dictamen final del corte:** **C2-R3 ACEPTADO POR CHAT 2**. M08 **HOLD para certificación integral**, `main` sin intervención de este procedimiento.
