# Certificación canónica de M07 sobre `60da5dc`: corrida ROJA

Creado: 06/10/2026, America/Lima · Última modificación: 06/10/2026 · Última verificación:
06/10/2026 · Publicado por: frente B (Claude Code B), en papel de QA independiente, por encargo
de Chat 2 vía JP.

## Veredicto: **NO CERTIFICADA**

Esta corrida **no certifica**. Es evidencia roja, separada de cualquier corrida futura.

**Causa: de la candidata, no ambiental.** La candidata activa M07 en el escenario e2e normal.
M07 declara dependencias duras de M01 (`catalog`) y M04 (`crm`), así que el panel bloquea
desactivar M01 y M04 mientras M07 está activo. Siete pruebas e2e **ya existentes**, que
desactivan M01 o M04, agotan su tiempo esperando un interruptor que nunca se habilita.

| | SHA |
|---|---|
| **Código probado** | `60da5dc39d83ea7653ff93789aa86e9b2e1275c0` (`m07-b2b-sobre-main-9f098a4`) |
| **HEAD remoto antes** | `60da5dc…` a las 12:19:25 |
| **HEAD remoto después** | `60da5dc…` a las 13:29:49 |
| **`main`** | `9f098a4e5e44701be8419867bdf0028703a1753c`, base exacta: 28 por delante, 0 por detrás. Igual antes y después |
| **Commit documental** | El que añade estos archivos, hijo directo de `60da5dc` en `qa/m07-60da5dc-rojo`. Solo evidencias |

## 1 · Resultado

| Etapa | Inicio | Resultado |
|---|---|---|
| 1 · Tipos del frontend | 12:19:59 | PASS |
| 2 · Tipos del arnés e2e | 12:20:24 | PASS |
| 3 · Compilación del backend | 12:20:30 | PASS |
| 4 · Migraciones (BD efímera `sillar_verify_1791307197943_1704228`) | 12:21:35 | PASS |
| 5 · Pruebas del backend | 12:22:25 | PASS: **704/704**, 0 fallidas, **0 no ejecutadas** en 10 proyectos, `Sillar.Modules.B2B.Tests` **92/92** incluido |
| 6 · Suite e2e completa, sin filtro | 12:25:23 | **FALLÓ** a las 13:28:39: **172 passed · 7 failed · 0 skipped · 0 flaky**, 179 pruebas en 63,2 min |

| | |
|---|---|
| Resultado global | `rc=1`, «FALLÓ en la etapa: suite e2e» |
| Total combinado | **876 passed · 7 failed · 0 skipped** |
| `OMITIDAS_ESPERADAS` | `[]` |

## 2 · Las 7 fallidas y su causa

Archivo: `…-FALLOS-E2E-20261006.txt`. Hora de inicio en America/Lima:

| Inicio | Prueba | Interruptor bloqueado |
|---|---|---|
| 12:28:40 | `aa-vacios.spec.ts:246` Con el catálogo vacío y nadie más aportando, la portada lo dice | `#modulo-crm` |
| 12:34:23 | `catalogo.spec.ts:209` Con M01 desactivado no queda entrada de menú, ni ruta viva, ni hueco en el inicio | `#modulo-catalog` |
| 12:39:29 | `contenido.spec.ts:836` Con M02 activo y sin nada publicado, la portada lo dice… | `#modulo-catalog` |
| 12:47:58 | `m02-cierre-evidencia.spec.ts:497` [M02-C24] Catálogo inactivo: ninguna solicitud al selector | `#modulo-catalog` |
| 12:58:23 | `tienda.spec.ts:364` Con M01 desactivado, las rutas públicas desaparecen… | `#modulo-catalog` |
| 13:07:08 | `zz-desmontaje.spec.ts:104` Desactivar M01 no borra nada… | `#modulo-catalog` |
| 13:14:44 | `zz-z-m04-ciclo.spec.ts:302` [M04-CICLO] desactivar, desinstalar, reinstalar y activar M04… | `#modulo-crm` |

**El primer error es el mismo en las siete** (registro de la puerta): `locator.click: Test timeout
… exceeded` sobre `locator('#modulo-catalog' | '#modulo-crm').getByRole('switch')`, que se
resuelve como `<button disabled … aria-label="Desactivar Catálogo de Productos">` (o «Desactivar
Clientes y Contacto»): **«element is not enabled»**.

**Lo que mostraba el panel** (`…-ERROR-CONTEXT-catalogo-209-20261006.md.txt`, instantánea de la
página al fallar `catalogo.spec.ts:209`): en la tarjeta `catalog`, el interruptor estaba
`[checked] [disabled]` y la tarjeta decía «**Lo necesita · Solicitudes B2B y Especiales**».

**De dónde sale:**

- `e2e/setup/global-setup.ts` de `60da5dc` añade `await activateModule(session, 'b2b')`, con un
  comentario sobre la C9. **En `main` `9f098a4` esa activación no existe**: 0 coincidencias.
- `backend/Sillar.Modules.B2B/B2BModule.cs:57`: `HardDependencies => ["core", "catalog", "crm"]`.
- Con M07 activo, la plataforma no permite desactivar sus dependencias duras. Es la regla del
  panel, comprobada en C6 de M07 (`ModuleGraph.ActiveHardDependents`).
- Las siete pruebas existían antes de M07, y en `main` pasan porque allí `b2b` no está activo.

**No es ambiental:**

- La red tuvo roaming dentro de la misma red «WIFI-FIPS», con la **misma IP** `10.7.122.206`
  entre las 13:10:21 y las 13:14:25, y una desautenticación a las 13:12:51
  (`…-RED-WLAN0-20261006.txt`). **El primer fallo es de las 12:28:40**, 42 minutos antes del
  primer evento de red.
- Todos los errores son el mismo interruptor deshabilitado, no fallos de carga ni
  `ERR_NETWORK_CHANGED`.
- Disco mínimo 6930 MiB y memoria mínima 5393 MiB: el vigía no intervino.

**Qué haría falta, sin decidirlo aquí:** que esas siete pruebas, o el arnés, contemplen que M07
activo bloquea la desactivación de M01 y M04. Por ejemplo, desactivando M07 antes y
reactivándolo después, o reordenando, o activando M07 solo dentro de sus propias pruebas. **QA no
lo corrige.** Es trabajo del candidato, y la decisión sobre la C9 corresponde a Chat 2.

## 3 · Lo que sí quedó acreditado en esta misma corrida

Es información, **no certifica**.

| Requisito del encargo | Observado |
|---|---|
| Etapa 4 con siete módulos | Base efímera `sillar_verify_1791307197943_1704228`, por lectura a las 12:22:33 (`…-MIGRACIONES-ETAPA4-20261006.txt`): schemas `b2b, catalog, cms, core, crm, sales, services`, migraciones `B2bInitial`, `CatalogInitial`, `CmsInitial`, `CoreInitial`, `CoreEmailSettings`, `CoreSmtpSettings`, `CoreHomeNodeYAutorDeMedios`, `CrmInitial`, `CrmCustomersEmailTrimAuthority`, `SalesInitial` y `ServicesInitial`. `b2b`: 5 tablas y 5 FK cruzadas hacia `crm.customers`, `catalog.product_items` y `catalog.products` |
| Etapa 5 backend completo con B2B | 704/704, 0 no ejecutadas. Proyectos: Shared 54 · Shared.Data 11 · Cms 54 · Catalog 58 · Sales 96 · Services 17 · Api 4 · Core 193 · **B2B 92** · Crm 125 |
| M07 en e2e | `b2b-cliente.spec.ts` 5/5 · `b2b-panel.spec.ts` 5/5 · `zz-b2b-ciclo.spec.ts` 1/1 · `zz-b2b-instalacion.spec.ts` 1/1, todas en verde |
| `zz-instalacion.spec.ts` | 3/3 en verde, incluida la cirugía combinada (`:174`): retira `sales` (`:224`), `b2b` (`:239`) y `catalog` (`:246`); `migrate()+seed()` (`:270-271`) reconstruye `catalog`, `sales` y `b2b` (`:274-279`); Sales vuelve equivalente (`:296-307`); `core`, `cms` y `crm` intactos (`:319-322`) |
| `[M05A-CICLO]` | 1/1 en verde |
| Swagger de M07 con B2B **activo** | Lo registró el observador de solo lectura (`…-OBSERVADOR-E2E-20261006.txt`). Con capacidades `b2b,catalog,cms,core,crm,…`, el documento tenía **49 esquemas `*Request` y 0 sin ejemplo**, y 9 de los 10 tipos `*Request` de B2B estaban presentes con ejemplo. No se determinó cuál de los 10 no aparece como esquema. La prueba de Swagger (`zz-instalacion.spec.ts:104`) pasó. `zz-b2b-ciclo` deja B2B activo al terminar (`:459`), y la prueba corre después |
| Base e2e | **`sillar_qa_m04_e2e`**, observada en vivo a las 12:27:39 (`…-BASE-E2E-20261006.txt`): contenedor `sillar_qa_m04_e2e_db`, volumen propio, con `b2b`, `sales` y `services` migrados. El nombre viene de la worktree de QA. Esa misma consulta incluye un error mío (columna `is_active` inexistente en `core.modules`), que no afecta a nada |
| Limpieza | 0 contenedores y 0 volúmenes `sillar_qa_m04_e2e*` tras la corrida; base efímera de backend destruida; nodos de compilación propios parados; base de desarrollo intacta |
| Árbol | `60da5dc`, 0 cambios antes y después. Sin cambios temporales ni commits intermedios |

**Paridad binario / setup ↔ migrate ↔ seed (punto 6 del encargo): no se completó en esta corrida
roja.**

- Lo observado: en la pila e2e, que sale de `migrate()+seed()` del arnés, estaban los siete
  schemas.
- No se midió la paridad módulo a módulo por la vía `/api/setup`, ni la clasificación de Demo.
- Queda para la corrida certificadora.

## 4 · Disco (precondición del encargo)

| Medida | Valor |
|---|---|
| Libre antes de que JP liberase espacio | 10304 MiB |
| Libre en el preflight, tras liberar 2,4 GB | **12802 MiB** |
| Mínimo durante la corrida | **6930 MiB** (13:25:40). Lejos del umbral de 3072 |

El vigía no intervino. Monitoreo completo en `…-MEMORIA-20261006.tsv`, 1 muestra cada 5 s.

## Mecanismo y condiciones

Es el runner v4 con TRX, el mismo aceptado por Chat 2 vía JP: `…-RUNNER-V4-20261006.sh.txt` y
`…-TRX-20261006.runsettings.txt`. El preflight es la v2. El observador de solo lectura
(`…-OBSERVADOR-20261006.sh.txt`) hizo, solo, `GET /api/capabilities` cada 10 s y
`GET /swagger/v1/swagger.json` cada 2 min contra la API e2e.

## Originales, copias y SHA-256

Originales en `/var/tmp/sillar-qa-m04-preparacion/corrida-20261006-121954/` (`V`) y
`/var/tmp/sillar-qa-m07-60da5dc/` (`C`).

- **Los textos están normalizados**: sin espacios al final de línea y con un único salto de línea
  final, para que pase `git diff --check`. Donde cambian, van los dos hashes.
- `…-BACKEND-PRUEBAS-20261006.tsv` es **derivado** de los TRX.
- El JSON de Playwright es la entrada `report.json` del zip embebido en el HTML. El HTML no se
  publica:

```
1aade3faa3606c6779cfe93850170a526918002c712f2dbcda41d1d8b5b33b1e  V/artefactos-1/playwright-report/index.html
```

| Publicado | Original | SHA-256 |
|---|---|---|
| `QA-M07-60DA5DC-ROJO-PUERTA-20261006.txt` (normalizado) | `V/PUERTA-1.log` | original `5024ee4a1ebbc288584812b4115a2b7d343f168f7551234a8f9a64e6122353b3` · publicado `7e34bc43f0cc113a695008211ec34717689e197cc310f65fc82f4de7af3ca1ef` |
| `QA-M07-60DA5DC-ROJO-FALLOS-E2E-20261006.txt` (normalizado) | `C/FALLOS-E2E.txt` | original `e8a3755b804a922dab97a6d05368368277577e3a367f013884b90e8e3a4242f8` · publicado `e325c7f8a884c75077c765081edcc7a1cbf97f5bad2e9ac971ba9f024c66e68f` |
| `QA-M07-60DA5DC-ROJO-ERROR-CONTEXT-catalogo-209-20261006.md.txt` (normalizado) | `C/ERROR-CONTEXT-catalogo-209.md` | original `6b818509f2288b866a59a7507a7e204112b0aedd9d2bb286ea0abd12a46bc7af` · publicado `4c966bbcb1d247dd9423643fa19d59b8ebce7abe90731153c46047a6296b672c` |
| `QA-M07-60DA5DC-ROJO-PREFLIGHT-20261006.txt` | `V/PREFLIGHT-1.log` | `348522e5a6c05fca92ad839526e5770c4fc9dbf0abb4134dc662b235df408081` |
| `QA-M07-60DA5DC-ROJO-MEMORIA-20261006.tsv` | `V/MEMORIA-1.tsv` | `38aa7cf64f1257b3f212622d9db42a14c158118707fae975af129e37c06135a4` |
| `QA-M07-60DA5DC-ROJO-RED-WLAN0-20261006.txt` | `V/RED-WLAN0.log` | `f5952b80f8ad9daecb5aaceb8c4801add2de85d83abda27dae8ddc2c0a8e08de` |
| `QA-M07-60DA5DC-ROJO-MIGRACIONES-ETAPA4-20261006.txt` | `C/MIGRACIONES-ETAPA4.txt` | `cbe3af4671bd8c9f00dde8966aa52c04905dd221233c379ed7c6634d9481889f` |
| `QA-M07-60DA5DC-ROJO-BASE-E2E-20261006.txt` | `C/BASE-E2E.txt` | `8e09f5a55369e15b131e18c0648311bdd6fa0b7c4d8fcd92d2142659228d60ff` |
| `QA-M07-60DA5DC-ROJO-BASES-ANTES-20261006.txt` | `C/BASES-ANTES.txt` | `05f1e3cd5b4fbfc6323272b3f2a5873a03ddc4b6b80fc807627ea8ca5c2caebd` |
| `QA-M07-60DA5DC-ROJO-BASES-DESPUES-20261006.txt` | `C/BASES-DESPUES.txt` | `6c102218cb58f234044315a1a6b4440da95283ab42766a6a535aa78340c09158` |
| `QA-M07-60DA5DC-ROJO-RAMA-ANTES-20261006.txt` | `C/RAMA-ANTES.txt` | `15c3e068f1ec5ddb81e1fb1ea62bdc4891d1fffe703a8ae7f4839c302b512dcd` |
| `QA-M07-60DA5DC-ROJO-RAMA-DESPUES-20261006.txt` | `C/RAMA-DESPUES.txt` | `57772d355cce3b6fa46a3a9276ca015740e55563277c5ef85d888e9f11a45a56` |
| `QA-M07-60DA5DC-ROJO-B2B-REQUEST-TIPOS-20261006.txt` | `C/B2B-REQUEST-TIPOS.txt` | `166f14b31d3305b042c497534cf0274d846afde83890893b502b7ccede9c2a9b` |
| `QA-M07-60DA5DC-ROJO-OBSERVADOR-E2E-20261006.txt` | `C/OBSERVADOR-E2E.log` | `7bff51bf51c4b6ca819efe463673233579d58a4cae48e8ebc4d8fe1b93d15da2` |
| `QA-M07-60DA5DC-ROJO-PLAYWRIGHT-REPORT-20261006.json` | `C/PLAYWRIGHT-REPORT-1.json` | `758cf9c180a6bca1113489b33e16cc19169bcba4a848096330843856dc8cc09c` |
| `QA-M07-60DA5DC-ROJO-PLAYWRIGHT-LAST-RUN-20261006.json` | `V/artefactos-1/test-results/.last-run.json` | `63a479bb1c7756634b4a5e7c6d5ec5d80261e053a79dec8aae149d7becc9c803` |
| `QA-M07-60DA5DC-ROJO-TRX-Sillar.Shared.Tests-20261006.trx` | `V/trx-1/puerta_net10.0_20261006122245.trx` | `19ce819a9e121d597d503c9e9bcbfc25316bd13d686774e1654645294bb78a33` |
| `QA-M07-60DA5DC-ROJO-TRX-Sillar.Shared.Data.Tests-20261006.trx` | `V/trx-1/puerta_net10.0_20261006122258.trx` | `54b9a9dfcabb174ce6924b7f72fe0fc4734faafad0b5beacd66ad1f2bc42615c` |
| `QA-M07-60DA5DC-ROJO-TRX-Sillar.Modules.Cms.Tests-20261006.trx` | `V/trx-1/puerta_net10.0_20261006122307.trx` | `1e6aa296857dee8a004e776ae2694dbf38d8c84e39cc927ea90a513d0a770d6d` |
| `QA-M07-60DA5DC-ROJO-TRX-Sillar.Modules.Catalog.Tests-20261006.trx` | `V/trx-1/puerta_net10.0_20261006122308.trx` | `2fb208f2dc8c47c3981d78fd1636447fd2a2a25ed0eeb8a9ea085100c76917b8` |
| `QA-M07-60DA5DC-ROJO-TRX-Sillar.Modules.Sales.Tests-20261006.trx` | `V/trx-1/puerta_net10.0_20261006122321.trx` | `d51b84960a4063a252f671abd5e79df147a71e85e72bbe9035c08acbab3d305d` |
| `QA-M07-60DA5DC-ROJO-TRX-Sillar.Modules.Services.Tests-20261006.trx` | `V/trx-1/puerta_net10.0_20261006122331.trx` | `c19a7e5cefa90efb5442eb4a50ac26bb038e2c6803f052abc70bc32fc9694ae3` |
| `QA-M07-60DA5DC-ROJO-TRX-Sillar.Api.Tests-20261006.trx` | `V/trx-1/puerta_net10.0_20261006122428.trx` | `fff6235d8366fe6eff78ae539ad9f3743e59084991ad7b7a796bd927b6914340` |
| `QA-M07-60DA5DC-ROJO-TRX-Sillar.Core.Tests-20261006.trx` | `V/trx-1/puerta_net10.0_20261006122449.trx` | `7bac1d098034d1ad45735aa34c736e91c76424f30fa493e0f1630336ecfc27d3` |
| `QA-M07-60DA5DC-ROJO-TRX-Sillar.Modules.B2B.Tests-20261006.trx` | `V/trx-1/puerta_net10.0_20261006122513.trx` | `ae0d7a567e2b1726fd971b6b80cb6856a49e66b96754ef260092c220f0d962f5` |
| `QA-M07-60DA5DC-ROJO-TRX-Sillar.Modules.Crm.Tests-20261006.trx` | `V/trx-1/puerta_net10.0_20261006122523.trx` | `9add516cbd735596bcf500429d7ee29784ad525c4653f73e90aa04fd5737d346` |
| `QA-M07-60DA5DC-ROJO-BACKEND-PRUEBAS-20261006.tsv` | `C/BACKEND-PRUEBAS-1.tsv` | `2ef63a19e8ae95f96fc3e0f7f39bcb8bd76f1f8a5aca4739c7ff70164c330e44` |
| `QA-M07-60DA5DC-ROJO-RUNNER-V4-20261006.sh.txt` | `/var/tmp/sillar-qa-m04-preparacion/correr-puerta-v4.sh` | `befe0680c3da075c0d0a915e105b1641971e9a44f6398c314e4805fdb9123dfd` |
| `QA-M07-60DA5DC-ROJO-TRX-20261006.runsettings.txt` | `/var/tmp/sillar-qa-m04-1.2.0/trx.runsettings` | `d25e0ed0c0ec6cf0f7007fea2fe709996ac27e12ad4225ae9e3fdf4ec3668bcf` |
| `QA-M07-60DA5DC-ROJO-PREFLIGHT-V2-20261006.sh.txt` | `/var/tmp/sillar-qa-m04-preparacion/preflight.sh` | `aa068bc26ba37efad4f23b79cff03a78d274ed577ee486b1445a12c7a363e07e` |
| `QA-M07-60DA5DC-ROJO-OBSERVADOR-20261006.sh.txt` | `C/observador.sh` | `e42e68e753b8122dcf7342fa7efec33ffd140595bee54bb78cb8fb50ea5c7d56` |

## Secretos y redacciones

**Una sola redacción:** en `…-BASE-E2E-20261006.txt`, la contraseña de la cadena de la API se
escribió como `Password=<oculta>`.

- Se buscaron, archivo por archivo, los valores reales de `POSTGRES_PASSWORD` y
  `PGADMIN_PASSWORD` de `.env` y de `e2e/.env.e2e`, sin imprimirlos: 0 coincidencias.
