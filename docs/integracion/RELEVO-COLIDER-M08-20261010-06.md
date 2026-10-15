# SILLAR — Relevo al colíder | Acta cronológica M08-06

**Fecha del cierre documental:** 2026-10-10 07:13:33 America/Lima.
**Autoridad:** JP — ejecución local y publicación; Chat 2 — coordinación, barreras y documentación.
**Estado:** candidata de host PUBLICADA EN RAMA. **QA INDEPENDIENTE PENDIENTE. HOLD MERGE A `main`.**

## 1. Historia verificable

- Base contractual M05b/M06: `963e0f1d12b131c4296cd836c88c3334fc22288e` (rama `integration/m08-contratos-m05b-m06-20261009`).
- Backend de Portal: `91d617606b078cc2692dab6b82aca9a7f1cccb37` (rama `feature/m08-portal-backend-manual-20261010`).
- **Candidata de integración del host:** `e549fd92dcc167be98cae2d39c7966abe89b44ed` (rama `integration/m08-host-manual-20261010`), cuyo padre exacto es `91d617606b078cc2692dab6b82aca9a7f1cccb37`.
- Base documental anterior: `08c0e115dc20d6ab2897733eac84550b392be9c7` (acta 05). Este documento es el acta siguiente.
- Rama `main` preservada en el corte: `4a3fd1ef12fc2226bad185f003acea9168b9a4c9`. No se fusionó ninguna de estas candidatas.

## 2. Territorio de la candidata

Solo se modificaron dos archivos compartidos:

1. `backend/Sillar.Api/Sillar.Api.csproj` — referencia de host a `Sillar.Modules.Portal`.
2. `backend/Sillar.sln` — añade proyectos `Sillar.Modules.Portal` y `Sillar.Modules.Portal.Tests`.

Cambio completo: **2 archivos, 29 inserciones, 0 borrados**, cotejado byte por byte contra `costuras.patch` probado (SHA-256 `cf297d2005ea0ab2f9b2ba917ab91836671198ca4a404bcfed115bf3c8688c66`). No se alteraron contratos, código M08 ni tablas, migraciones o datos PostgreSQL.

## 3. Secuencia de incidentes y recuperación

1. Primer intento: la worktree fue creada y las dos costuras quedaron instaladas; el script se detuvo por incompatibilidad Python 3.14 / GLIBC_2.44 en la máquina local, no por compilación de M08.
2. Reanudación sin Python: **compilación global `backend/Sillar.sln` ROJA**, con 128 errores de analizadores xUnit **reportados por JP** en pruebas existentes de CRM, Catálogo y B2B. No se editaron esos módulos ni se silenciaron las reglas.
3. Diagnóstico focal: **build estricto del host Debug, 0 advertencias / 0 errores**, más tests M08 6/6 sin skips. Primer `dotnet publish Release` terminó correctamente, pero una guarda del script lo marcó HOLD al esperar erróneamente un texto de resumen no requerido.
4. Recuperación focal: **build estricto del host Release, 0 advertencias / 0 errores**, `dotnet publish Release` con salida correcta y archivos `Sillar.Api.dll`, `Sillar.Modules.Portal.dll`, `Sillar.Modules.Crm.Contracts.dll` y `Sillar.Modules.Tracking.Contracts.dll` presentes. Pruebas M08 Release: **6/6 PASS, 0 fallos, 0 omitidas**.

**Límite de estos resultados:** los tests M08 aún usan dobles de proveedores. No hay ejecución HTTP real del host ni pruebas reales de identidad, propiedad A/B/NULL, estado del grafo o PostgreSQL para este incremento.

## 4. Evidencia local reproducible y huellas

**Directorio de pruebas:** `/var/tmp/sillar-m08-host-release-20261010-6qi7iOJX` (en la máquina de JP; no se suben binarios o TRX al repositorio).

| Evidencia | SHA-256 |
|---|---|
| `costuras.patch` | `cf297d2005ea0ab2f9b2ba917ab91836671198ca4a404bcfed115bf3c8688c66` |
| `build-host-previo.log` (Debug) | `bb3722be320150f0c0d78e55596c80ad3d5fb79eeee6f9354220377971ec957b` |
| `build-release.log` (Release) | `09c6347ee93671b91f18b6cd2cd097a8fbaf4d83b2cfbe68ad18e043c74bfec2` |
| `publish-release.log` | `1b8dfbcbfba65651fbb38eed41395590963f5c494c720a165ad8a3720bfe2c05` |
| `M08-Host-Focal-previo.trx` | `0164fffde833c2d08e41604d185e556b1b46d89e607025811ee606dbcc18bcf7` |
| `M08-Host-Release.trx` | `e26d4ec73e2d96954af7953f73c2c2bae6a114216c0c6b1ce8a81d240ecf557f` |
| `build-solucion-completa-rojo.log` | `c3f6dd38171272c2005d79d744f61f12283e4b7be39a5cc853c25e89f7a8b7ea` |

El expediente original está en `RELEVO-COLIDER-M08-20261010-06-PRELIMINAR.md` dentro del directorio de evidencias. La compilación global fallida se conserva íntegra, sin presentarla como PASS.

## 5. Barreras del colíder y siguiente frente

- Reproducir el checkout exacto `e549fd92dcc167be98cae2d39c7966abe89b44ed` y comprobar activación `portal` con M04 como dependencia dura y M03/M06 blandas.
- Ejecutar **HTTP real**: 401 sin sesión o solo cookie admin, 404 para código ajeno/inexistente, parámetros `CustomerId` manipulados, privacidad de JSON, 503 cuando no hay M06 y estados empty/unavailable/error correctamente diferenciados.
- Construir y reproducir pruebas de ciclo `[M08-CICLO]`, restaurabilidad e instalación/desactivación en base efímera autorizada; conservar datos ajenos.
- Resolver la compilación global roja de xUnit por territorio y revisión separados; **no** marcar puerta `node scripts/verificar.mjs` 6/6 antes de acreditarla.
- Ejecutar sabotajes de seguridad y montaje VERDE→ROJO→VERDE sobre candidata congelada, sin skips.
- Completar UI de Portal, diseño ratificado por JP y QA independiente. **Ningún desarrollador certifica su propio SHA**.

**Veredicto interno:** HOST DEBUG/RELEASE PASS; PUBLICACIÓN RELEASE PASS; UNITARIAS M08 6/6; COMPILACIÓN GLOBAL HOLD; HTTP/POSTGRES/CICLO PENDIENTES; QA INDEPENDIENTE PENDIENTE; `main` SIN MERGE.
