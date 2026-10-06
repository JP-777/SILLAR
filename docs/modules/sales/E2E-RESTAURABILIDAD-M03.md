# M03 — Restaurabilidad en el arnés e2e

Creado: 05/10/2026, America/Lima · Última verificación: 05/10/2026, America/Lima
Commit base comprobado: `6bde7c9cfcad0f8932da006e7f22555c35cfc8cb` — recompuesto
sobre ese `main`, que ya trae M05a Services dentro del arnés. La rama
`fix/m03-e2e-restauracion` nació de `3564718` y se recompuso por **merge normal**,
no rebase: conserva sus commits de evidencia.
Base PostgreSQL usada: **`sillar_m03b_paridad`**, en el stack de desarrollo de la
worktree `sillar-m03-e2e` (PostgreSQL 16, puerto 55690, colación ICU `es-PE`). Es
la de la recomposición, y la misma que declaran las evidencias publicadas. La
medición anterior, sobre `main 3564718` y sin Services, usó
`sillar_m03_paridad`; se conserva en el §2 como estado anterior.

---

## 1 · El hallazgo causal, dicho sin rodeos

> **`POST /api/setup` instala Sales desde el binario y `migrate()+seed()` no
> sabía restaurarlo.**

El arnés e2e tiene **dos caminos** para dejar la base lista, y tenían que
converger al mismo estado:

| Camino | Qué módulos toca |
|---|---|
| `POST /api/setup` | **Todos los del binario.** M03 está en `Sillar.Api.csproj`, así que el schema `sales` se creaba siempre |
| `migrate()` + `seed()` de `e2e/setup/migrate.ts` | Una lista **escrita a mano**, y M03 no estaba en ella |

No convergían. El escenario e2e siempre tuvo `sales` —puesto por la
instalación—, pero **el arnés no sabía reconstruirlo**: una prueba destructiva
que soltara `sales`, o que se llevara sus claves foráneas con un `CASCADE`
ajeno, dejaba la base sin forma de volver.

Y hay una trampa peor que «no vuelve». Si el schema `sales` sobrevive pero sus
claves cruzadas se van con un `DROP SCHEMA catalog CASCADE`, **su historial de
migraciones sigue ahí**, así que EF Core no reaplica nada: la base queda con
`sales` presente, sin sus claves foráneas, y sin nada que avise.

---

## 2 · Diagnóstico medido · los dos caminos, lado a lado

Tres estados sobre la **misma** base `sillar_m03b_paridad`, **en el árbol
combinado** —el que ya trae M05a Services de `main`—. Las listas van ordenadas
en JavaScript, no en SQL: la colación ICU coloca un `__migrations` inicial donde
`Array.sort()` no lo coloca, y depender de eso daría un rojo cierto y mudo.

**Services aparece en los tres estados a propósito.** Es lo que permite leer el
defecto sin ambigüedad: el que no vuelve es M03, y Services queda igual todo el
tiempo porque nadie lo toca.

### A · lo que deja `POST /api/setup` (201)

```
schema_sales        1
tablas_sales        __migrations, cart_items, carts, order_lines,
                    order_payments, order_series, order_status_changes, orders
migraciones         20260930185029_SalesInitial
fk_cruzadas         fk_order_lines_item_id -> catalog.product_items
                    fk_orders_customer_id  -> crm.customers
dep_duras_sales     catalog, core, crm
schema_services     1
migrac_services     20260928090000_ServicesInitial
```

### B · tras soltar Sales y correr `migrate()+seed()` **sin Sales y con Services** — el defecto

```
sales/99_drop.sql   OK
migrate()           Core · Catalog · Cms · Crm · Services       ← cinco, sin M03
seed()              core, catalog, cms, crm, services           ← cinco

schema_sales        0        ← DESAPARECIDO
tablas_sales        (vacío)
migraciones         (vacío)
fk_cruzadas         (vacío)
dep_duras_sales     catalog, core, crm   ← la fila de catálogo sobrevive, y está bien
schema_services     1                    ← INTACTO: el defecto es de M03, de nadie más
migrac_services     20260928090000_ServicesInitial
```

**El arnés no sabía volver.** Dos cosas que conviene leer juntas:

- `core.modules` sigue diciendo que `sales` existe y de qué depende, porque ese
  catálogo no se va con el schema. Es correcto, y es además lo que hace que la
  guarda C6 de M01 se niegue mientras el schema esté.
- **Services no se mueve.** Con los dos módulos fuera del arnés el rojo no diría
  de quién es; con Services dentro, queda atribuido.

### C · tras `migrate()+seed()` **con los seis** — reconstruido

```
migrate()           Core · Catalog · Cms · Crm · Services · Sales
seed()              core, catalog, cms, crm, services, sales

schema_sales        1
tablas_sales        __migrations, cart_items, carts, order_lines,
                    order_payments, order_series, order_status_changes, orders
migraciones         20260930185029_SalesInitial
fk_cruzadas         fk_order_lines_item_id -> catalog.product_items
                    fk_orders_customer_id  -> crm.customers
dep_duras_sales     catalog, core, crm
schema_services     1
migrac_services     20260928090000_ServicesInitial
```

**C es idéntico a A en los cinco aspectos de Sales**, y Services queda igual en
los tres estados. Los dos caminos convergen.

> **No se encontró ninguna diferencia real entre los dos caminos** más allá de la
> ausencia de M03, que es lo que esta rama repara. No hay nada que escalar por
> este punto.

### Estado anterior · la primera medición, sobre `main 3564718` y sin Services

Se conserva porque es donde se vio el defecto por primera vez, y **describe un
árbol que ya no existe**: entonces `main` no traía M05a, así que `migrate()`
enumeraba cuatro módulos y el seed cuatro. Base `sillar_m03_paridad`.

```
A  /api/setup (201)              sales 1 · 8 tablas · SalesInitial · 2 FK
B  soltar + migrate()+seed()     Core · Catalog · Cms · Crm      ← cuatro
   de entonces                   sales 0 · sin tablas · sin historial · sin FK
C  soltar + migrate()+seed()     Core · Catalog · Cms · Crm · Sales
   con el arreglo de entonces    idéntico a A
```

La conclusión fue la misma y no cambia: los dos caminos convergen en cuanto
Sales entra al arnés. Lo que cambió entre las dos mediciones es **el árbol**, no
el hallazgo.

---

## 3 · La regla que este incidente deja escrita

> **La auditoría de paridad entre los módulos del binario / `/api/setup` y los
> que enumeran `migrate()` + `seed()` debe repetirse cada vez que entre a `main`
> un módulo real nuevo.**

**Por qué es una regla y no una anécdota.** El arnés tiene dos caminos para
dejar la base lista y uno de ellos —`/api/setup`— se actualiza **solo**, porque
migra lo que haya en el binario. El otro es una lista escrita a mano. Cada
módulo que entra al binario sin entrar a esa lista abre el mismo hueco que abrió
M03, y **el hueco no produce ningún rojo**: la base sale bien de la instalación,
así que todo parece funcionar hasta que una prueba destructiva necesita
reconstruir.

Es la asimetría la que hace falta vigilar, no el módulo. Por eso la regla se
dispara con la **entrada de un módulo a `main`**, que es el momento en que la
lista puede quedarse atrás.

**Lo que ya se vio dos veces.** M03 abrió el hueco al entrar en `main` el 3 de
octubre de 2026 y se cerró aquí. **M05a Services lo abrió y se cerró en el mismo
cambio**, dentro de `main`: la lista de `migrate()` y el bucle de `seed()`
llegaron con el módulo. Esa es la forma correcta — el arreglo entra con el
módulo, no después.

**El próximo caso conocido es M07 B2B.** Su candidata
(`m07-b2b-sobre-main`, `343ff7c`) ya lleva su mitad hecha: añade
`Sillar.Modules.B2B` a `migrate()` y `b2b` al bucle de seeds. **Cuando M07 entre
a `main`, esta auditoría se repite** y la tabla de abajo tiene que crecer con su
fila. Si entrara sin ella, el hueco sería el mismo y volvería a no haber ningún
rojo que lo dijera.

---

## 4 · Auditoría de paridad del arnés completo

No se dio por supuesto que M03 fuera el único atrasado. Se comparó, para **todos**
los módulos reales presentes, qué hay en el binario contra qué enumeran
`migrate()` y `seed()`.

**Vuelta a medir sobre el árbol combinado**, no heredada de la medición
anterior: `main` cambió entre las dos.

| Módulo | En binario / `/api/setup` | `migrate.ts` | `seed()` | Diferencia |
|---|:--:|:--:|:--:|---|
| `Sillar.Core` | sí | sí | sí (`core`) | **ninguna** |
| `Sillar.Modules.Catalog` | sí | sí | sí | **ninguna** |
| `Sillar.Modules.Cms` | sí | sí | sí | **ninguna** |
| `Sillar.Modules.Crm` | sí | sí | sí | **ninguna** |
| `Sillar.Modules.Services` | sí | sí | sí | **ninguna** — ya venía de `main` con M05a, y **no se reescribió** |
| `Sillar.Modules.Sales` | sí | **NO** → **sí** | **NO** → **sí** | era el **hueco real**; cerrado por esta rama |
| `Sillar.Modules.Demo` | sí, **solo en Debug** | no | no | **Módulo sin migraciones ni seed aplicables:** 0 archivos de migración y no existe `database/modules/demo/`. No hay nada que migrar ni sembrar |

Medido así, sobre el árbol ya recompuesto:

```
binario (Sillar.Api.csproj)   Core · Catalog · Cms · Crm · Demo · Sales · Services
migrate()                     Core · Catalog · Cms · Crm · Services · Sales
seed()                        core · catalog · cms · crm · services · sales
migraciones por proyecto      Core 5 · Catalog 2 · Cms 2 · Crm 3 · Sales 2 ·
                              Services 2 · Demo 0
```

**El orden importa en un solo sitio.** Sales va el último porque es el único con
claves foráneas cruzadas —a `catalog` y a `crm`, `SalesModule.cs:74`—. M05a solo
depende de `core` (`ServicesModule.cs:24`), así que su sitio es indiferente y se
conserva **donde `main` lo puso**.

**Una carpeta de seed que no es un hueco:** `database/modules/b2b/`. M07 **no
está en `main`**, así que no hay asimetría que reparar aquí; su mitad ya la lleva
hecha su candidata, y es el próximo caso de la regla del §3.

**Y una que dejó de serlo.** Hasta la medición anterior, `database/modules/services/`
estaba en esta misma lista como «módulo futuro, sin proyecto». **Ya no:** M05a
trajo `Sillar.Modules.Services` al binario, a `migrate()` y a `seed()`, así que
su fila de la tabla de arriba dice «ninguna» como las demás. Se anota porque es
la regla del §3 funcionando: un módulo entró a `main` y entró con su mitad del
arnés.

**Conclusión de la auditoría sobre el árbol combinado: no queda ningún módulo
atrasado.** Services ya estaba al día —lo trajo M05a— y Sales es el que esta rama
pone al día. Nada más que agregar y nada que escalar por paridad.

---

## 5 · Qué cambió

| Archivo | Cambio |
|---|---|
| `e2e/setup/migrate.ts` | `applyMigrations('Sillar.Modules.Sales')`, **el último**: sus dos claves foráneas cruzadas apuntan a `catalog` y `crm`, que tienen que existir antes. Y `'sales'` en el bucle de seeds — su `02_seed.sql` está intencionalmente vacío y se aplica por simetría |
| `e2e/tests/zz-instalacion.spec.ts` | La prueba destructiva respeta que M03 depende duro de M01: mide el estado de M03, **comprueba que lo midió**, lo retira por su mecanismo, retira M01, reconstruye con `migrate()+seed()` y exige **equivalencia estructural**, no existencia |

**Lo que no se tocó**, y es explícito: producto de M03, `catalog/99_drop.sql`,
las guardas C6, la activación global de Sales en `global-setup.ts`, M07, la
numeración, y `main`.

Esto repara la **restaurabilidad del arnés**. No pretende cerrar la cobertura
E2E funcional de M03, que es otro trabajo.

---

## 6 · Las tres direcciones

Los rojos van en archivos aparte del verde: una barrera que solo se ha visto en
verde no se ha visto.

| | Qué se hace | Esperado | Evidencia |
|---|---|---|---|
Las tres se repitieron **sobre el árbol combinado**, no se heredaron.

| | Resultado | Evidencia |
|---|---|---|
| **1 · legal** · el flujo nuevo, con Services de `main` y Sales de esta rama conviviendo | **VERDE · 3 passed, 0 failed, 0 skipped**. La destructiva, 1,1 min | `evidencias/M03-E2E-1-LEGAL.txt` |
| **2 · ilegal** · se retira **solo Sales** del arnés y **Services se conserva** | **ROJO · 1 failed, 2 passed**: «M03 no volvió: migrate() no lo reconstruye», esperaba `1` y recibió `0`. **57,8 s** | `evidencias/M03-E2E-2-ILEGAL.txt` |
| **3 · sabotaje** · el detector deja de ver a Sales | **ROJO · 1 failed, 2 passed**, y **en la preparación**. **1,6 s** | `evidencias/M03-E2E-3-SABOTAJE.txt` |

**La diferencia de tiempos entre la 2 y la 3 es parte de la evidencia.** La
dirección 2 tarda 57,8 s porque llega a hacer la cirugía entera y solo falla al
comparar; la 3 muere a los 1,6 s, en la guarda, **antes de destruir nada**. Si la
guarda no estuviera, la 3 habría destruido el catálogo y después habría
confirmado en verde que «las cero claves foráneas volvieron».

**Que la 2 conserve Services importa.** Con los dos fuera, el rojo no diría de
quién es; con Services dentro, el único que no vuelve es M03, y eso es
exactamente lo que el arreglo arregla.

La dirección 2 es la que acredita que el arreglo hace algo: es exactamente el
defecto histórico, provocado a propósito. La 3 es la que impide que la prueba
pase sin haber comprobado lo que dice comprobar — con el detector vacío, «las
cero claves foráneas volvieron» sería trivialmente cierto.

Como evidencia secundaria se conserva además el caso de **omitir
`sales/99_drop.sql` antes de soltar Catalog**: la guarda C6 de M01 se niega, que
es el comportamiento correcto.

Ningún sabotaje queda en HEAD.
