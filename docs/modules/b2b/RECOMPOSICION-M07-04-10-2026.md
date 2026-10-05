# Recomposición de M07 sobre `main` — 4 de octubre de 2026

Qué se hizo con los **15 commits** de `m07-b2b-sobre-main` cuando `main` llevaba
39 commits de ventaja, y en qué se equivocó el inventario que los clasificó.

Existe porque el inventario se dio en una conversación y la conversación no es
trazabilidad. Lo que sigue está medido contra el repositorio, no recordado.

---

## 1 · Base y resultado

```
origin/main al auditar      3564718  (certificación final de CORE)
rama antes de recomponer    cd2bb40  (15 commits sobre la base común e839989)
base común                  e839989  (27/09, cierre de M02)
                            M07 ahead 15 · behind 39

merge normal (no rebase)    eb8ff3b   un único conflicto: backend/Sillar.sln
```

**Merge y no rebase, con el motivo.** Con un solo archivo en solape textual, el
merge resuelve `Sillar.sln` una vez y conserva los 15 commits con sus evidencias.
Un rebase los habría reescrito quince veces y habría perdido la trazabilidad de
la puerta 6/6 corrida sobre `0fd8dad`.

**Los 15 siguen siendo historia.** Comprobado uno a uno con
`git merge-base --is-ancestor <commit> HEAD`: ninguno reescrito, descartado ni
reordenado.

**El conflicto de `Sillar.sln`** se resolvió conservando los proyectos de los dos
lados: los **dos** de M07 —`Sillar.Modules.B2B` y `Sillar.Modules.B2B.Tests`
(`47b924c`)— y los **tres** de Sales que `main` había añadido —`Sillar.Modules.Sales`,
`.Contracts` y `.Tests` (`3564718:backend/Sillar.sln`)—. **Cinco**, no seis.

> **El inventario decía «seis» y el commit del merge lo repitió.** Era un recuento
> de memoria: M07 no tiene `Sillar.Modules.B2B.Contracts` —no lo necesita, nadie
> depende de M07— y se le contó uno de más por simetría con Sales. Se corrige aquí
> porque los mensajes de commit ya son historia. Comprobado:
> `grep -c '^Project(' backend/Sillar.sln` → **24**, igual que `EndProject`.

---

## 2 · El inventario, corregido

El inventario del 4/10 **se contradecía consigo mismo**: su tabla clasificaba
**11** commits como «CONSERVAR tal cual» y su prosa decía «Diez de los quince
commits se conservan intactos». El colíder lo señaló y ratificó la cifra de la
tabla:

> **Son 11, no 10.**

| Commit | Qué es | Clase (ratificada) |
|---|---|---|
| `9c176ab` | Reconciliación con el encargo del 26/09 y cola de escaladas | CONSERVAR tal cual |
| `21501b6` | Plan de pruebas de lo ya decidido | CONSERVAR tal cual |
| `49dd7c8` | C6 como riesgo futuro deducido, no fallo de la suite | CONSERVAR tal cual |
| `a0ed9f0` | SPEC con las decisiones de JP del 27/09 | CONSERVAR tal cual |
| `0fd8dad` | Auditoría C6, escaladas y bitácora con evidencias | CONSERVAR tal cual |
| `e8dcec4` | Puerta 6/6 sobre `0fd8dad` y hallazgo de memoria | CONSERVAR tal cual |
| `6d74aae` | Corrige hash citado y respalda C11 | CONSERVAR tal cual |
| `693d9a7` | Datos de M07 con guardas de instalación/desinstalación | CONSERVAR tal cual |
| `51d0275` | `99_drop` de catalog y crm rechazan con dependientes duros | CONSERVAR tal cual |
| `7fb97eb` | Rutas de cliente con límite por cuenta | CONSERVAR tal cual |
| `cd2bb40` | Pruebas de los selectores de catálogo | CONSERVAR tal cual |
| `47b924c` | Proyectos de M07 en `Sillar.sln` | COSTURA COMPARTIDA |
| `eb722df` | Frontend tramo 1 con navegación modular | COSTURA COMPARTIDA |
| `7d002d0` | Panel de solicitudes y reacción a eventos de M01 | CONSERVAR PERO ADAPTAR |
| `3b69e34` | Cotizaciones `C-AAAA-NNNN` y E3b cerrada | CONSERVAR PERO ADAPTAR |

Ninguno **OBSOLETO** y ninguno en **CONFLICTO SEMÁNTICO**: M07 no duplicó nada de
lo que `main` arregló después. Consume `ICatalogService` y reacciona a sus
eventos, así que C15 no le quitó trabajo.

---

## 3 · Y lo que pasó de verdad, que no es exactamente eso

Al terminar la recomposición se midió qué contenido de los 15 llegó a cambiar.
Se obtiene con `git diff --name-only eb8ff3b HEAD` y mirando quién introdujo cada
archivo en `cd2bb40`:

| Commit | Clasificado | Medido |
|---|---|---|
| `9c176ab` `21501b6` `49dd7c8` `a0ed9f0` `0fd8dad` `e8dcec4` `6d74aae` `51d0275` `7fb97eb` | CONSERVAR tal cual | **intacto** |
| `47b924c` | COSTURA COMPARTIDA | **intacto** tras resolver el merge |
| `693d9a7` | CONSERVAR tal cual | **rehecho**: su `B2bInitial` se regeneró |
| `cd2bb40` | CONSERVAR tal cual | **tocado**: trajo `BandejaAdminEndpoints.cs`, que R-14 cambió |
| `3b69e34` `7d002d0` `eb722df` | adaptar / costura | **tocado**, como estaba previsto |

**Diez quedaron sin tocar, no once.** Las dos diferencias son errores del
inventario, y van en direcciones opuestas:

- **`693d9a7` estaba mal clasificado, y el propio inventario lo delataba.** Su
  tabla decía «CONSERVAR tal cual» mientras el §3a del mismo documento decía que
  la migración inicial había que rehacerla por el trío de R-14. Una contradicción
  interna más, hermana de la del 10 contra 11.
- **`47b924c` fue al revés:** clasificado como conflicto, acabó intacto —el merge
  resolvió `Sillar.sln` y ningún commit posterior volvió a tocarlo—.
- **`cd2bb40` no se vio venir:** se le miró el nombre («pruebas de los selectores»)
  y no los archivos; traía también `BandejaAdminEndpoints.cs`.

**Para qué queda escrito.** La cifra ratificada es 11 y así consta. Pero una
clasificación por asunto no predice qué archivos se van a tocar, y aquí falló
tres veces de quince. La próxima vez, el inventario se hace **por archivo**, no
por commit: `git log --format=%h -1 <base> -- <archivo>` es una línea y no se
equivoca.

---

## 4 · Lo que la recomposición añadió encima

Tres commits, cada uno con su verificación en las dos direcciones:

| | |
|---|---|
| `bb26c68` | **R-14**: la atribución del pago son tres datos congelados (nombre visible, identificador local, nodo de la cuenta), sin FK a `core.admin_users`. `B2bInitial` rehecha, no migración correctiva: M07 no ha llegado a `main` y no existe ninguna instalación |
| `e250799` | **Identidad del cliente**: la bandeja adopta `ICustomerIdentityReader` (M04 1.2.0) y el `customer_id` deja de viajar en los DTO del panel |
| `aecf4aa` | **Costura §j**: `ProjectReference` de B2B en `Sillar.Api.csproj`, B2B en la lista de migraciones de la puerta, y `ModulosEnElDespliegueTests` para que no vuelva a pasar |

Lo absorbido por `main` mientras M07 esperaba, y que por tanto ya no es deuda de
M07: **§p** —`ICurrentAdmin` expone ahora `DisplayName` y `HomeNode`, que eran
justo los dos datos que R-14 no tenía de dónde sacar— y **§i**, la fotografía del
autor de los medios sin FK (`7b59054`).

---

## 6 · Hallazgo abierto · M03 y M07 leen la misma convención al revés

No se corrige aquí: es una decisión de esquema sobre una convención ratificada y
afecta a los dos módulos. Se plantea.

La excepción de la ADR-016 del 27/09 fija el código visible como **serie de nodo,
año y correlativo**. Los dos módulos lo implementaron, y la letra significa cosas
distintas en cada uno:

| | M03 | M07 |
|---|---|---|
| Tabla de serie | `sales.order_series (node_code, year)` | `b2b.quote_number_series (series_code, year)` |
| Qué es la letra | **el nodo**, leída del ajuste de la instalación (`OrderCodeAllocator.cs:103`) | **el tipo de documento**, constante `"C"` (`NumeradorDeCotizaciones.cs:32`) |
| Nodo en la clave | sí (`OrderCodeAllocator.cs:136`) | **no lo hay en ninguna columna** |

M07 restringe además `series_code` a una sola letra mayúscula
(`B2bConfiguration.cs:190`), así que el hueco no es «falta rellenar una columna»:
es que la única columna disponible ya está ocupada por el tipo.

**Por qué importa y por qué no corre prisa hoy.** Con un solo nodo los dos
esquemas funcionan. Con dos, M03 da `A-2026-0147` y `B-2026-0147` sin colisión, y
M07 da `C-2026-0147` en los dos nodos: el mismo número visible para dos
cotizaciones distintas. Y es de las caras de deshacer —toca clave primaria,
números ya emitidos y la lectura de la ADR— así que es exactamente lo que no se
decide solo.

La letra `C` **no se reabre**: la ratificó JP el 30/09. Lo que está sin decidir es
si el nodo entra como columna propia en la serie de M07, o si la letra pasa a ser
del nodo y el tipo se va a otra parte. Queda para la decisión de multinodo (M16).

---

## 5 · Navegación

`frontend/src/app/routes.tsx:9,56` y `frontend/src/layout/navigation.ts:1,54`
**ya registraban M07** antes de esta recomposición: los trajo `eb722df`, y el
merge los reconcilió sin conflicto. Ningún commit de la recomposición los toca.

**NAV_READY M07.**
