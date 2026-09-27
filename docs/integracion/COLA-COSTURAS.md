# Cola de costuras compartidas

**Creación:** 27 de septiembre de 2026 — America/Lima
**Última modificación:** 27/09/2026 17:46 — America/Lima (dos peticiones de A)
**Última verificación:** 27 de septiembre de 2026 — America/Lima
**Commit verificado:** `main` = `e839989432283c755edf7d4ae47b2c37697215ec`
**Escritor único durante este turno:** Claude Code D, como integrador (encargo `D_M04_Contrato`)

Una fila por fichero de costura. **Las peticiones se serializan aquí**: un frente que necesita un
cambio lo pide describiendo el efecto observable (`DIVISION-DE-TRABAJO.md` §3), y el integrador lo
aplica en orden. Nadie más escribe en estos ficheros mientras dure el turno.

---

## Estado de cada costura

| Fichero | Dueño | Qué es | Peticiones pendientes | Verificado |
| --- | --- | --- | --- | --- |
| `frontend/src/layout/navigation.ts` | Integración | Compone el menú desde el `routes` de cada módulo; punto de composición declarado en la barrera de fronteras | **Ninguna** | 27/09/2026 |
| `frontend/src/platform/auditEntityVocabularies.ts` | Integración | Registro del vocabulario de auditoría por módulo; punto de composición declarado | **Ninguna** | 27/09/2026 |
| `backend/Sillar.Api/Modularity/ModuleDiscovery.cs` | Integración | Descubre las implementaciones de `IModule` **recorriendo los ensamblados del despliegue** (`ModuleDiscovery.cs:7-18`). Un módulo nuevo se descubre sin editar este fichero | **Ninguna** | 27/09/2026 |
| `backend/Sillar.Api/Modularity/ModuleBootstrapper.cs` | Integración | Secuencia de arranque del SPEC de CORE §7: descubre, valida y registra los servicios **solo de los módulos activos**, antes de construir la aplicación (`ModuleBootstrapper.cs`, cabecera) | **Ninguna** | 27/09/2026 |
| `backend/Sillar.Api/Sillar.Api.csproj` | Integración | Referencias de proyecto del host: un módulo se publica junto al host si su `ProjectReference` está aquí | **1 — A (M03)**: que `Sillar.Modules.Sales.dll` se publique junto al host para que `ModuleDiscovery` lo encuentre | 27/09/2026 |
| `scripts/verificar.mjs` | Integración | La puerta; su etapa `[4/6]` migra la lista de módulos de `verificar.mjs:2085-2088` | **1 — A (M03)**: que la etapa de migraciones incluya `Sillar.Modules.Sales` para que el esquema `sales` exista en la base efímera | 27/09/2026 |
| `backend/Sillar.Core/Modularity/ModuleRegistry.cs` | CORE | **No es un registro manual**: implementa `IModuleRegistry` sobre la foto de activaciones del arranque (`ModuleRegistry.cs:5-9`). **No se edita para registrar un módulo** | — (no admite peticiones de registro) | 27/09/2026 |

## Cómo se comprobó que no hay peticiones

Sobre las 47 ramas remotas, diferencia de cada una contra su base común con `main` en esos cuatro
ficheros: **ninguna rama los modifica**. Una rama se saltó por no tener base común en el clon
superficial: `origin/m02`, sustituida por el cierre de M02 ya integrado (`2191150`).

Por canal, tampoco ha llegado ninguna petición de A, B ni C durante este turno.

## Peticiones pendientes — PENDIENTES, no integradas en `main`

**Origen:** `docs/modules/sales/ESCALADAS-M03.md` §j, rama `m03-ventas-online` en
`c4be2250886d64bf100b61706f24809334263e37` (27/09/2026), publicadas después de abrir esta cola.

| # | Fichero | Efecto observable pedido | Frente | Recibida | Disparador |
| --- | --- | --- | --- | --- | --- |
| C1 | `backend/Sillar.Api/Sillar.Api.csproj` | `Sillar.Modules.Sales.dll` se publica junto al host y `ModuleDiscovery` lo encuentra | A (M03) | 27/09/2026 | Al montar un candidato de integración que contenga **de verdad** `Sillar.Modules.Sales` |
| C2 | `scripts/verificar.mjs` | La etapa `[4/6]` migra `Sillar.Modules.Sales` y el esquema `sales` existe en la base efímera | A (M03) | 27/09/2026 | El mismo que C1: van juntas |

**No se aplican en la candidata de M04** (`integration/m04-contrato-snapshot`, congelada en
`4848f18` para la QA de B). Se aplican en el siguiente turno de integración, en una candidata que
contenga M03, y no antes: añadirlas a una base sin el proyecto `Sales` rompería la compilación y
la etapa 4.

**Observación, sin juicio:** en su rama, A añade sus proyectos a `backend/Sillar.sln` (+42 líneas
sobre la base común `711bfba`). `Sillar.sln` no figura en la lista de costuras de
`DIVISION-DE-TRABAJO.md` §3; se anota porque es un punto de conflicto probable al integrar.

## Lo que M03 necesitará, previsto y no pedido

Cuando A integre M03 hará falta, **en su turno y a petición suya**:

- `navigation.ts`: las entradas de menú de M03 desde su `routes`. Ya está cubierto por el comodín
  `modules/*/routes` de la barrera; el cambio es de composición, no de excepción.
- `auditEntityVocabularies.ts`: el vocabulario de auditoría de M03, por el mismo comodín.
- Descubrimiento modular: nada, si `Sillar.Modules.Sales` se publica junto al host.

**No se adelanta**: se aplica cuando A lo pida con su efecto observable.

## Historial

| Fecha | Fichero | Petición | Frente | Estado |
| --- | --- | --- | --- | --- |
| 27/09/2026 | — | Apertura de la cola | D (integrador) | Sin peticiones |
| 27/09/2026 | `backend/Sillar.Api/Sillar.Api.csproj` | C1 · publicar `Sillar.Modules.Sales` junto al host | A | PENDIENTE — siguiente candidato con M03 |
| 27/09/2026 | `scripts/verificar.mjs` | C2 · migrar `Sillar.Modules.Sales` en la base efímera | A | PENDIENTE — siguiente candidato con M03 |
