# Certificación final — CORE + inventario, sobre `1f0f401`

Creado: 04/10/2026, America/Lima · Última modificación: 04/10/2026 · Última verificación:
04/10/2026 · Publicado por: frente B (Claude Code B), en papel de QA independiente, por encargo
de Chat 2 vía JP.

| | SHA |
|---|---|
| **Código certificado** | `1f0f40114552f9e14b05f4d6ee7d9509a65f0f50` (`integration/core-inventario-final`) |
| **`main` al certificar** | `6ceb18e84534273d555b3ed73d043f61f3ad9450`. Es la base exacta: la candidata va 6 por delante y 0 por detrás. No se tocó |
| **Commit documental** | El que añade estos archivos, hijo directo de `1f0f401` en `qa/core-1f0f401-evidencias`. Solo añade evidencias: no cambia código, y la QA no se repitió sobre él |

**Equipo:** el equipo local de JP (Linux `archlinux`). Worktree aislada
`/home/JP777/sillar-qa-m04`, desacoplada en `1f0f401`, con 0 cambios antes y después, e identidad
offset 61, sin choque.

- La candidata y `main` estaban en esos SHA antes del preflight, al lanzar y al publicar.
- El diff `6ceb18e..1f0f401` no toca dependencias, lockfiles ni `scripts/verificar.mjs`.
- `OMITIDAS_ESPERADAS = []` (`scripts/verificar.mjs:75`).

## Resultado: corrida única, certificadora

| Comprobación | Resultado | Archivo |
|---|---|---|
| `node scripts/verificar.mjs` | **6/6 PASS**, `rc=0`, «TODO EN VERDE — las 6 etapas pasaron.» | `QA-CORE-1F0F401-PUERTA-20261004.txt` |
| Etapa 5, pruebas del backend | **594/594** superadas · 0 fallidas · **0 no ejecutadas** · `timeout="0"` y `aborted="0"` en los 8 TRX | `QA-CORE-1F0F401-TRX-*.trx`, `…-BACKEND-PRUEBAS-20261004.tsv` |
| Etapa 6, suite e2e | **166/166** esperadas · 0 inesperadas · 0 inestables · **0 omitidas** | `…-PLAYWRIGHT-REPORT-20261004.json` (`stats`), `…-LAST-RUN-…json` (`passed`) |
| **Total** | **760 PASS · 0 FAIL · 0 SKIPPED** | |

Reparto de la etapa 5 por proyecto:

| Proyecto | Pruebas |
|---|---|
| Shared | 54 |
| Shared.Data | 11 |
| Cms | 54 |
| Catalog | 58 |
| **Sales** | **96** |
| **Core** | **193** (179 en `6ceb18e`, +14) |
| Api | 3 |
| Crm | 125 |

Etapas, todas **PASS**, con su hora de inicio:

| # | Etapa | Inicio |
|---|---|---|
| 1 | Tipos del frontend | 12:45:06 |
| 2 | Tipos del arnés e2e | 12:45:24 |
| 3 | Compilación del backend | 12:45:27 |
| 4 | Migraciones en la BD efímera `sillar_verify_1791135904833_1788306` | 12:46:07 |
| 5 | Pruebas del backend | 12:46:37 |
| 6 | Suite e2e | 12:48:24 |

| Hito | Hora |
|---|---|
| Inicio de la puerta | 2026-10-04 12:45:04 -0500 |
| Fin de la puerta | 2026-10-04 13:20:17 -0500 |
| `startTime` de Playwright | 12:48:27 |
| Duración de Playwright | 31,8 min |

**Corridas fallidas o ambientales: ninguna.** Antes de esta hubo un solo preflight **NO LISTO**
(04/10, 00:18), que no lanza la puerta. Falló por memoria: 2326 MiB, con un emulador y Brave
abiertos. Ese mismo momento la red acababa de cambiar y tenía 490–760 ms de latencia. JP liberó la
memoria y se lanzó a las 12:45.

## Migraciones completas, con Sales

Comprobado en la base efímera **de esta corrida**, por lectura, a las 12:46:46, ya en la etapa 5
(`QA-CORE-1F0F401-MIGRACIONES-ETAPA4-20261004.txt`):

```
schemas: catalog,cms,core,crm,sales
core:    CoreInitial, CoreEmailSettings, CoreSmtpSettings, 20261003203701_CoreHomeNodeYAutorDeMedios  ← nueva
catalog: CatalogInitial · cms: CmsInitial · crm: CrmInitial, CrmCustomersEmailTrimAuthority
sales:   20260930185029_SalesInitial · tablas sales: 8 (7 + __migrations)
columnas nuevas de core: admin_users.home_node, media_assets.origin_node,
                         media_assets.created_by_admin_user_home_node
```

**Sales sigue migrando y sus pruebas siguen corriendo:** `Sillar.Modules.Sales.Tests` da 96/96
en `Passed`, las mismas 96 que en la certificación de `e4e95e5`.

## Pruebas nuevas de CORE

Están en `backend/Sillar.Core.Tests/`. Todas `Passed`; hora de inicio y duración, tomadas de
`QA-CORE-1F0F401-TRX-Sillar.Core.Tests-20261004.trx`.

Las que dicen «PostgreSQL» usan `BaseDePrueba.ConBaseVaciaAsync`
(`backend/Sillar.Core.Tests/BaseDePrueba.cs:126-162`):

- Crea su propia base `sillar_vacia_<guid>` en el servidor de la cadena que pasa la puerta.
- La borra con `DROP DATABASE … WITH (FORCE)` en un `finally`.
- Se omite a sí misma (`:132`) solo si no hay cadena. En esta corrida no se omitió ninguna: todas
  están en `Passed` y `notExecuted=0`.

### `NodoYAutoriaTests` (`NodoYAutoriaTests.cs`): 10/10

| Prueba (línea) | Tipo | Inicio · duración |
|---|---|---|
| `En_una_instalacion_limpia_la_migracion_no_necesita_nodo_y_deja_el_esquema_nuevo` (:43) | PostgreSQL | 12:47:50 · 4,0 s |
| `Sobre_datos_preexistentes_rellena_el_nodo_de_las_cuentas_y_fotografia_al_autor_valido` (:72) | PostgreSQL | 12:47:37 · 3,3 s |
| `Sin_nodo_configurado_y_con_cuentas_la_migracion_aborta_sin_cambiar_nada` (:112) | PostgreSQL | 12:47:45 · 5,1 s |
| `Un_autor_que_no_existe_es_corrupcion_la_migracion_aborta_dice_cuantas_y_como_localizarlas` (:133) | PostgreSQL | 12:47:02 · 14,7 s |
| `La_fotografia_del_autor_no_se_modifica_una_vez_escrita` (:169) | PostgreSQL | 12:47:33 · 3,5 s |
| `Media_fotografia_del_autor_no_se_admite` (:190) | PostgreSQL | 12:47:17 · 6,1 s |
| `ICurrentAdmin_da_lo_de_siempre_y_ademas_el_nombre_visible_y_el_nodo_de_la_cuenta` (:215) | PostgreSQL | 12:47:23 · 10,2 s |
| `Sin_sesion_ICurrentAdmin_no_lanza_y_lo_nuevo_tambien_es_nulo` (:243) | Lógica | 12:47:23 · 0,02 s |
| `Al_subir_la_fotografia_lleva_el_nodo_de_la_cuenta_y_no_el_de_la_instalacion` (:254) | PostgreSQL | 12:47:40 · 4,6 s |
| `El_nodo_viaja_en_la_conexion_solo_si_esta_configurado` (:309) | Lógica | 12:47:40 · 0,002 s |

### `ModuleRegistryTests` (`ModuleRegistryTests.cs`): 2/2, de lógica pura

| Prueba (línea) | Inicio · duración |
|---|---|
| `IsActive_devuelve_true_si_el_modulo_esta_en_la_foto_activa` (:12) | 12:47:01 · 0,05 s |
| `IsActive_devuelve_false_si_el_modulo_no_esta_en_la_foto_activa` (:25) | 12:47:00 · 0,43 s |

Prueban `IModuleRegistry.IsActive` sobre una `ModuleActivationSnapshot` construida en memoria.
**No tocan PostgreSQL**, ni lo pretenden.

### `ModuleSynchronizerReadActivePostgresTests`: 1/1, PostgreSQL

| Prueba (línea) | Inicio · duración |
|---|---|
| `ReadActiveAsync_devuelve_exactamente_el_modulo_activo_de_PostgreSQL` (:15) | 12:47:02 · 20,6 s |

### `SettingsReaderPostgresTests`: 1/1, PostgreSQL

| Prueba (línea) | Inicio · duración |
|---|---|
| `GetT_con_cache_fria_lee_PostgreSQL_y_respeta_conversion_defaults_e_inactivos` (:18) | 12:47:00 · 21,1 s |

## Disparador de la observación de timeouts de limpieza: NO reapareció

La observación está en `docs/modules/core/ENTREGA-03-ACTIVACION-SETTINGS-AUDITORIA.md:439-451`:
11 timeouts de limpieza en una corrida paralela de `Sillar.Core.Tests`. En esta certificación,
**no reapareció**, por tres vías:

1. **Los TRX.** Ningún resultado fallido. Contadores `timeout="0"`, `error="0"` y `aborted="0"`
   en los 8 TRX. Las únicas apariciones de la palabra «timeout» son esos contadores.
2. **Las bases huérfanas.** Ninguna `sillar_vacia_*` en el PostgreSQL de la worktree de QA, ni
   antes (línea base del 04/10, 00:18) ni después de la corrida (13:20). Archivo:
   `QA-CORE-1F0F401-BASES-HUERFANAS-20261004.txt`.
3. **La propia limpieza.** El `DROP` de cada base está en el `finally`
   (`BaseDePrueba.cs:151-161`), así que un timeout ahí habría hecho fallar la prueba.

**No se atribuye causa ni se descarta el patrón:** una corrida verde no demuestra que no vaya a
reaparecer. Hay una diferencia de condiciones que conviene tener presente:

- La puerta lanza `dotnet test` sobre la solución con un único servidor PostgreSQL.
- La corrida donde se vio («corrida paralela») pudo tener otra concurrencia. No se sabe cuál.

## Defectos conocidos, no tocados

Estaban fuera del alcance por orden expresa:

- `SalesDbFixture` no compara `SILLAR_VERIFY_DATABASE`.
- `backend/.dockerignore` no excluye `appsettings.Development.json`.

## Mecanismo de observación

Es el runner v4 con TRX, el mismo aceptado por Chat 2 vía JP: `…RUNNER-V4-20261004.sh.txt` y
`…TRX-20261004.runsettings.txt`. Solo añade un logger TRX: no filtra, no omite y no toca
`verificar.mjs` ni la candidata.

```
export TMPDIR=/var/tmp/sillar-qa-m04-preparacion/tmpdir
export VSTestSetting=/var/tmp/sillar-qa-m04-1.2.0/trx.runsettings
export VSTestResultsDirectory=/var/tmp/sillar-qa-m04-preparacion/corrida-20261004-124501/trx-1
node scripts/verificar.mjs
```

El preflight es la v2 (`…PREFLIGHT-V2-20261004.sh.txt`), con la detección de «puerta en marcha»
corregida en la certificación de M03.

## Condiciones

**Preflight a las 12:45:01** (`…PREFLIGHT-20261004.txt`): LISTO.

| Comprobación | Valor |
|---|---|
| Memoria disponible | 8878 MiB (≥ 6000) |
| Disco libre | 13808 MiB (≥ 10240) |
| Brave y emulador | Cerrados |
| Cerrojo y puerta en marcha | Ninguno |
| Pilas e2e | Ninguna |
| TMPDIR | En disco, vacío, modo 700 |

**Red** (`…RED-WLAN0-20261004.txt`): una sola conexión `wlan0`, con IP `192.168.25.66` desde las
12:43:50.

- La comprobación de 5 minutos (12:49) no vio cambios y dio 0 % de pérdida.
- Durante la corrida solo hubo una renovación de DHCP, a las 13:13:49, con la **misma**
  dirección.
- Se quitaron de este registro las líneas `authenticate`, que llevan la MAC del equipo.

**Recursos** (`…MEMORIA-20261004.tsv`, 414 muestras):

| Medida | Valor |
|---|---|
| Memoria disponible mínima | 6236 MiB (12:53:17) |
| Chromium, máximo | 734 MiB |
| TMPDIR, máximo | 11 MiB |
| Disco libre, mínimo | 5672 MiB (13:16:38) |

El vigía no intervino.

## Originales, copias y SHA-256

Originales en `/var/tmp/sillar-qa-m04-preparacion/corrida-20261004-124501/` (`V`) y
`/var/tmp/sillar-qa-core-1f0f401/` (`C`).

**Todos los archivos son copias byte a byte, salvo:**

- `…PUERTA-20261004.txt`, **normalizado.** Se quitó el espacio final de 1 línea, que viene del
  prefijo de hora sobre una línea vacía, para que pase `git diff --check`. Van los dos hashes.
- `…BACKEND-PRUEBAS-20261004.tsv`, **derivado** de los TRX con `…TRX-RESUMEN-20261004.py.txt`.

El informe JSON de Playwright es la entrada `report.json` del zip embebido en el HTML. El HTML no
se publica:

```
406d9b6f46b609d5e56c29ff99a59f0df2177b80ed67940e3bab259b53ab32f9  V/artefactos-1/playwright-report/index.html
```

| Publicado | Original | SHA-256 |
|---|---|---|
| `QA-CORE-1F0F401-PUERTA-20261004.txt` (normalizado) | `V/PUERTA-1.log` | original `5255acb92b2e00301f84cdfa8238b3ffeeba380213049e35013003a41f0a3801` · publicado `bc1947d8c95a391760c9c3c1b33a7cfe847c34d7e3868a78259005cb7597543b` |
| `QA-CORE-1F0F401-PREFLIGHT-20261004.txt` | `V/PREFLIGHT-1.log` | `55b75579665a7936d954a811fc12df752b4e376088c11682e19fdc6e01a8346e` |
| `QA-CORE-1F0F401-MEMORIA-20261004.tsv` | `V/MEMORIA-1.tsv` | `24e1d124f6de11d6802db5643272ae2a9ab8091861ccc5890764d788657ae193` |
| `QA-CORE-1F0F401-RED-WLAN0-20261004.txt` | `V/RED-WLAN0.log` | `8223f9a72bae03370a69b48caa1b96d8ca7eb33ca3374a2c5cd61ad420ae05e6` |
| `QA-CORE-1F0F401-MIGRACIONES-ETAPA4-20261004.txt` | `C/MIGRACIONES-ETAPA4.txt` | `66cd5deeb7d62811aa2972bdea9abf88833b37452a2a96e817f9eef77db8e5ef` |
| `QA-CORE-1F0F401-BASES-HUERFANAS-20261004.txt` | `C/BASES-HUERFANAS.txt` | `e447825634d03316f3dc368b8b916c15a026aa616fee4bdb769753d7fc7d2cf5` |
| `QA-CORE-1F0F401-PLAYWRIGHT-REPORT-20261004.json` | `C/PLAYWRIGHT-REPORT-1.json` | `c479ef73b0e7581787c01638defc3e338a7ce5ebf1886be0f1d738fa835feb9d` |
| `QA-CORE-1F0F401-PLAYWRIGHT-LAST-RUN-20261004.json` | `V/artefactos-1/test-results/.last-run.json` | `91d1c43004802cd49950d78eb11c8fa7d05da8ffffe219a8b13b2f561bc00903` |
| `QA-CORE-1F0F401-TRX-Sillar.Shared.Tests-20261004.trx` | `V/trx-1/puerta_net10.0_20261004124703.trx` | `46d165caed6378e657a3d76ec2b0e099a4f1cc18f08fefad9382792bc5d43f71` |
| `QA-CORE-1F0F401-TRX-Sillar.Shared.Data.Tests-20261004.trx` | `V/trx-1/puerta_net10.0_20261004124707.trx` | `a09839536ee87d0909abb7e1183199c98c72ef98f6b40e9dd74ed71c43088a32` |
| `QA-CORE-1F0F401-TRX-Sillar.Modules.Cms.Tests-20261004.trx` | `V/trx-1/puerta_net10.0_20261004124715.trx` | `ad95083f6be2e43a1ae328a3f75223839cafdeaad1f9e071268f6dd77e850451` |
| `QA-CORE-1F0F401-TRX-Sillar.Modules.Catalog.Tests-20261004.trx` | `V/trx-1/puerta_net10.0_20261004124717.trx` | `ca64dce6c5cf0901f5c37e33056e31d6c10032e200512705e9679d65417f2fb5` |
| `QA-CORE-1F0F401-TRX-Sillar.Modules.Sales.Tests-20261004.trx` | `V/trx-1/puerta_net10.0_20261004124724.trx` | `cf06042c102182acccaad4766cf2f99e3144d358f200bb011c165999e76f0dcc` |
| `QA-CORE-1F0F401-TRX-Sillar.Core.Tests-20261004.trx` | `V/trx-1/puerta_net10.0_20261004124800.trx` | `96b6f6dc4fd402a62da05c9cecf06282fba5ab315009f0d3945851c6216d0cf2` |
| `QA-CORE-1F0F401-TRX-Sillar.Api.Tests-20261004.trx` | `V/trx-1/puerta_net10.0_20261004124818.trx` | `0cd041ba4f0ad3fa7ee1c3d27a9b85e74eda778b5c5863ac2dc06276011281cc` |
| `QA-CORE-1F0F401-TRX-Sillar.Modules.Crm.Tests-20261004.trx` | `V/trx-1/puerta_net10.0_20261004124824.trx` | `55e20f3d8d51d5306395d244838e02b4e0059d70b25c8b3e91a1df3bb33322c7` |
| `QA-CORE-1F0F401-BACKEND-PRUEBAS-20261004.tsv` | `C/BACKEND-PRUEBAS-1.tsv` | `cbfc435b623a0c36296a9c4f0e74d2c4953596f2dc6215b517fe8c746746c0b7` |
| `QA-CORE-1F0F401-RUNNER-V4-20261004.sh.txt` | `/var/tmp/sillar-qa-m04-preparacion/correr-puerta-v4.sh` | `befe0680c3da075c0d0a915e105b1641971e9a44f6398c314e4805fdb9123dfd` |
| `QA-CORE-1F0F401-TRX-20261004.runsettings.txt` | `/var/tmp/sillar-qa-m04-1.2.0/trx.runsettings` | `d25e0ed0c0ec6cf0f7007fea2fe709996ac27e12ad4225ae9e3fdf4ec3668bcf` |
| `QA-CORE-1F0F401-PREFLIGHT-V2-20261004.sh.txt` | `/var/tmp/sillar-qa-m04-preparacion/preflight.sh` | `aa068bc26ba37efad4f23b79cff03a78d274ed577ee486b1445a12c7a363e07e` |
| `QA-CORE-1F0F401-TRX-RESUMEN-20261004.py.txt` | `/var/tmp/sillar-qa-m04-1.2.0/trx-resumen.py` | `90b972a078d7259c5ce7feb28bd267a6a97502e09f57ad9e0e7b1c8506541ef0` |

## Secretos y redacciones

**Ninguna redacción.**

- Se buscaron en todos los originales los valores reales de `POSTGRES_PASSWORD` y
  `PGADMIN_PASSWORD` del `.env` de la worktree, sin imprimirlos: ninguna coincidencia.
- También se buscó `Host=`, `Username=`, `Password=` y `ConnectionStrings__`: ninguna
  coincidencia.
- El nombre de la base efímera no es un secreto.
