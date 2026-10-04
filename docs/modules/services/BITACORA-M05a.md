# Bitácora M05a

- **Creación:** 28/09/2026 · **Última modificación / verificación:** 30/09/2026 · America/Lima
- **Base integrada:** `e839989432283c755edf7d4ae47b2c37697215ec`; desde el 04/10/2026, `main` = `35647181891a9b78a7399d3b108d9a4415a0d48a` (ver la última sección)
- **Commit de implementación verificado:** `c30c9666534edd4ddaa570870f3e992fe2552c30`

## Evidencia de etapa

- Pruebas focales: 7/7, sin omitidas.
- Falsificación B2: guarda de precio debilitada deliberadamente; 1 fallo esperado. Restauración: 7/7.
- Generación SQL mediante EF: no ejecutada; el proyecto no incorpora `Microsoft.EntityFrameworkCore.Design` y la costura de host aún no está autorizada.
- Montaje/desmontaje real: pendiente de costura con host/solución y prueba con infraestructura; no se declara superado.

## Estado de etapas

1. SPEC: ratificada y consolidada.
2. DATOS: diseñado e implementado en migración inicial y scripts.
3. API: implementada para vitrina pública, administración editorial y snapshots.
3.5. DISEÑO: publicado en `JP-777/SILLAR-DESIGN`, rama `diseno/m05a-paso-3-5`, SHA `40f6b2c48a3ba2398f3a927826a1f48a51917f99`.
4. UI: implementación estructural de S1, S2, S3, S4, S5-admin y S5-public publicada sobre `68a09aa744ce3bea27fff005e4f49378fb24ace8`; microcorrección de validación contextual incorporada posteriormente en esta rama. La validación visual final de S1/S2/S5-public sigue pendiente del tema real de una instalación cliente.
5. CIERRE: no iniciado.

No se declara ejecutada la QA canónica ni cerrado el módulo.

## Decisiones aplicadas

Persistencia propia; CORE como única dependencia dura; sin M01; sin replicación; snapshot sin FK hacia M05b; sin opciones hasta decisión; sin garantía inventada para el binario histórico.

### Rectificación documental de conflictos — 29/09/2026

El colíder adoptó la opción B: el diseño solo representa conflictos respaldados por el
contrato existente. Se retiró la promesa de detectar una edición simultánea porque
`UpdateAsync` no verifica versiones. Quedan representados slug duplicado, transición no
permitida, orden inválido, validación contextual, ausencia pública al consultar o revalidar,
fallback de imagen y fallo de red recuperable. La concurrencia editorial queda pendiente
hasta que más de una persona use simultáneamente el editor.

## Prueba sintética de independencia

La evidencia prevista hoy es: grafo del módulo con solo `core`; proyecto M05a sin referencias a M01/M05b/M06; migración que solo crea `services`; `99_drop.sql` que solo elimina `services`; e instalación sobre una base con CORE. No se atribuye ninguna prueba a M05b inexistente.

## Actualización sobre `main` = `3564718` — 04/10/2026

**Escrito por:** Claude Code D, por encargo de Chat 2 vía JP. **No certifica nada:** son pruebas
focales sobre una base efímera propia, no la puerta canónica.

### Cómo se actualizó

Merge de `main` (`35647181891a9b78a7399d3b108d9a4415a0d48a`) en esta rama, **sin rebase**: la
historia de JP se conserva. Sin conflictos: `main` no toca ningún archivo de M05a. La existencia de
M05a la ratificó JP el 26/09/2026 (SPEC §0.2), y se registra en `DECISIONES-PREVIAS-M05a.md` §3 en la
rama `docs/m05a-existencia-ratificada` (`b74a57f`).

### Supuestos revisados contra `main`

| Supuesto de M05a | Qué cambió en `main` | Clasificación |
|---|---|---|
| Base `e839989` en este documento y en `ESCALADAS.md` | `main` avanzó 39 commits | **Obsoleto** → actualizado a `3564718` |
| FK `fk_service_entries_image_id` → `core.media_assets` | CORE 1.1.0 quitó la FK de `media_assets` hacia `admin_users` y fotografía al autor (ENTREGA-05 de CORE) | **Vigente.** `services` no se replica y `media_assets` sí: combinación local → replicada, permitida por la ADR-018. Ahora lo comprueba una prueba |
| Auditoría con `ICurrentAdmin.AdminUserId` y `Email` | `ICurrentAdmin` gana `DisplayName` y `HomeNode` | **Vigente.** Sin dobles que actualizar |
| `IMediaStorage.GetPublicUrl` para las imágenes | Sin cambios | **Vigente** |
| Barrera de fronteras del frontend | En `main` desde antes; ahora con la lista `COMPOSICION` | **Vigente.** `test:fronteras` 34/34 en esta rama |
| Contrato `IServiceShowcaseSnapshots` sin prueba contra PostgreSQL | `PENDIENTES` §30 (cola de integración) pide evidencia de ejecución real por método | **Era un hueco** → cubierto ahora |
| M03, M04 1.2.0 y C15 | Integrados en `main` | **Sin relación** con M05a |

Ningún conflicto semántico.

### Hallazgo: M05a no se podía instalar

La primera prueba contra PostgreSQL falló al migrar con `PendingModelChangesWarning`. El snapshot,
escrito a mano, no tenía los seis `CHECK` ni la anotación de identidad de `id` que sí tiene
`ServicesInitial`. EF 10 se niega entonces a migrar, y el instalador (`/api/setup` →
`ApplyMigrationsAsync` → `MigrateAsync`) habría fallado igual. Coincide con lo que esta bitácora
declaraba: «Generación SQL mediante EF: no ejecutada».

**Corrección:** el snapshot ahora coincide con el modelo. Además, el modelo declara también
`ck_service_entries_publication_state`, que hasta ahora solo estaba en la migración. **No cambia el
esquema que se crea**: `ServicesInitial` no se tocó.

### Otras correcciones dentro de M05a

- `ServicesDbContextFactory` llevaba una cadena con credenciales de respaldo escrita en el código.
  Ahora lee `.env` o el entorno, y falla si no hay cadena, como la fábrica de CORE (`CLAUDE.md`,
  «Entorno»).
- Una transición no permitida enseñaba los nombres internos del enum en inglés («No se puede pasar de
  Archived a Published»). Ahora dice: «Un servicio archivado no puede pasar a publicado. Recarga la
  lista para ver su estado actual.»
- Los endpoints de administración no tenían resumen en Swagger, y los manejadores no tenían
  comentarios XML. Ya los tienen (`CLAUDE.md`, regla de trabajo 6).

### Pruebas

`Sillar.Modules.Services.Tests/ServicesPostgresTests.cs`: 10 pruebas contra PostgreSQL real. Usan
la regla de base efímera de CRM y CMS (`SILLAR_VERIFY_DATABASE`), con CORE migrado.

- La migración solo crea `services`, con su historial dentro.
- La única FK cruzada va a `core.media_assets` (local → replicada), y no hay `origin_node` ni
  `row_version`.
- La dirección pública es única sin distinguir mayúsculas (`core.es_ci`).
- Contrato `GetPublishedSnapshotAsync`: un servicio publicado devuelve su fotografía con la URL de la
  imagen y precio nulo («a consultar»). Uno en borrador, archivado o inexistente devuelve nulo.
- La vitrina pública solo enseña lo publicado, en su orden, y conserva el precio cero como gratis.
- La transición no permitida se explica en castellano.
- `99_drop.sql` se ejecuta dos veces sin tocar `core`, y la migración reinstala.

**Diagnóstico no canónico:** 17/17 en `Sillar.Modules.Services.Tests` (7 de reglas + 10 nuevas),
0 omitidas.

**Sabotajes:**

| Sabotaje | Pruebas en rojo |
|---|---|
| Fotografía sin filtro de publicado | «Solo un servicio publicado tiene fotografía» |
| Vitrina sin filtro de publicado | «La vitrina pública solo enseña lo publicado» |
| Snapshot sin los `CHECK` (el estado encontrado) | Todas las que migran |

Frontend en esta rama:

| Comprobación | Resultado |
|---|---|
| `typecheck` | verde |
| `test:fronteras` | 34/34 |
| `test:services` | 7/7 |
| `test:frontend-hygiene` | 18/18 |
| `test:audit-vocabulary` | 14/14 |
| `test:api-errors` | 15/15 |

### Costuras compartidas pendientes — `NAV_READY M05A`

Este frente **no las toca**. Están en `ESCALADAS.md` E3:

1. **Navegación y registros del frontend.** `app/routes.tsx`, `layout/navigation.ts` y
   `platform/homeSections.ts` ya los modificó el commit `68a09aa` de esta rama, que es anterior a
   esta actualización. Están en la lista `COMPOSICION` y pasan la barrera, pero necesitan el turno de
   Integración. **Falta** el vocabulario de auditoría de `service_entry` en
   `platform/auditEntityVocabularies`.
2. **Backend.** Faltan cuatro cosas:
   - los tres proyectos de M05a en `backend/Sillar.sln`; sin ellos, la puerta ni compila ni prueba
     M05a;
   - el `ProjectReference` en `Sillar.Api.csproj`;
   - `Sillar.Modules.Services` en la lista de migraciones de `scripts/verificar.mjs` y de
     `e2e/setup/migrate.ts`;
   - `test:services` en la etapa 1.
3. **Guarda de dependientes duros en `99_drop.sql`.** Es el patrón C6/C7 de M07, que no está en
   `main`. Hoy M05a no tiene dependientes duros. **Disparador:** cuando exista M05b.
