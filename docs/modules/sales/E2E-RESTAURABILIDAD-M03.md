# M03 — Restaurabilidad en el arnés e2e

Creado: 05/10/2026, America/Lima · Última verificación: 05/10/2026, America/Lima
Commit base comprobado: `35647181891a9b78a7399d3b108d9a4415a0d48a` (rama
`fix/m03-e2e-restauracion`, partida de ese `main` exacto; `merge-base` verificado
igual al propio `main`).
Base PostgreSQL usada: `sillar_m03_paridad`, en el stack de desarrollo de la
worktree `sillar-m03-e2e` (PostgreSQL 16, puerto 55690, colación ICU `es-PE`).

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

Tres estados sobre la **misma** base `sillar_m03_paridad`. Las listas van
ordenadas en JavaScript, no en SQL: la colación ICU coloca un `__migrations`
inicial donde `Array.sort()` no lo coloca, y depender de eso daría un rojo
cierto y mudo.

### A · lo que deja `POST /api/setup` (201)

```
schema_sales        1
tablas              __migrations, cart_items, carts, order_lines,
                    order_payments, order_series, order_status_changes, orders
migraciones         20260930185029_SalesInitial
fk_cruzadas         fk_order_lines_item_id -> catalog.product_items
                    fk_orders_customer_id  -> crm.customers
dep_duras_de_sales  catalog, core, crm
```

### B · tras soltar Sales y correr `migrate()+seed()` **tal como estaba** — el defecto

```
sales/99_drop.sql   OK (cascada a 9 objetos)
migrate()           Core OK · Catalog OK · Cms OK · Crm OK      ← cuatro, sin M03
seed()              core, catalog, cms, crm                     ← cuatro

schema_sales        0        ← DESAPARECIDO
tablas              (vacío)
migraciones         (vacío)
fk_cruzadas         (vacío)
dep_duras_de_sales  catalog, core, crm   ← la fila de catálogo sobrevive, y está bien
```

**El arnés no sabía volver.** Y nótese el último campo: `core.modules` sigue
diciendo que `sales` existe y de qué depende, porque ese catálogo no se va con
el schema. Es correcto, y es además lo que hace que la guarda C6 de M01 se niegue
mientras el schema esté.

### C · tras `migrate()+seed()` **con el arreglo** — reconstruido

```
migrate()           Core OK · Catalog OK · Cms OK · Crm OK · Sales OK
seed()              core, catalog, cms, crm, sales

schema_sales        1
tablas              __migrations, cart_items, carts, order_lines,
                    order_payments, order_series, order_status_changes, orders
migraciones         20260930185029_SalesInitial
fk_cruzadas         fk_order_lines_item_id -> catalog.product_items
                    fk_orders_customer_id  -> crm.customers
dep_duras_de_sales  catalog, core, crm
```

**C es idéntico a A en los cinco aspectos.** Los dos caminos convergen.

> **No se encontró ninguna diferencia real entre los dos caminos** más allá de la
> ausencia de M03, que es lo que esta rama repara. No hay nada que escalar por
> este punto.

---

## 3 · Auditoría de paridad del arnés completo

No se dio por supuesto que M03 fuera el único atrasado. Se comparó, para **todos**
los módulos reales presentes, qué hay en el binario contra qué enumeran
`migrate()` y `seed()`.

| Módulo | En binario / `/api/setup` | `migrate.ts` | `seed()` | Diferencia |
|---|:--:|:--:|:--:|---|
| `Sillar.Core` | sí | **sí** | sí (`core`) | — |
| `Sillar.Modules.Catalog` | sí | **sí** | sí | — |
| `Sillar.Modules.Cms` | sí | **sí** | sí | — |
| `Sillar.Modules.Crm` | sí | **sí** | sí | — |
| `Sillar.Modules.Sales` | sí | **NO** → **sí** | **NO** → **sí** | **Hueco real. Es lo que esta rama repara** |
| `Sillar.Modules.Demo` | sí, **solo en Debug** | no | no | **Módulo sin migraciones ni seed aplicables:** 0 migraciones en su carpeta y no existe `database/modules/demo/`. No hay nada que migrar ni sembrar |

Dos filas que conviene no confundir con huecos:

- **`database/modules/services/02_seed.sql` existe y no hay proyecto `Sillar.Modules.Services`.** Es un módulo futuro: no está en el binario, así que `/api/setup` no lo instala y no hay asimetría que reparar.
- **`database/modules/b2b/`** está igual, en este árbol: M07 **no está en `main`**, y esta rama parte de `main`. Su reparación equivalente ya la hizo la candidata de M07 en su propia rama.

**Conclusión de la auditoría: M03 era el único módulo atrasado.** Nada más que
agregar, y nada que escalar por paridad.

---

## 4 · Qué cambió

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

## 5 · Las tres direcciones

Los rojos van en archivos aparte del verde: una barrera que solo se ha visto en
verde no se ha visto.

| | Qué se hace | Esperado | Evidencia |
|---|---|---|---|
| **1 · legal** | El flujo nuevo: Sales se retira → Catalog se retira → `migrate()+seed()` → los dos vuelven → las claves de Sales vuelven | **VERDE · 3 passed, 0 failed, 0 skipped** (la destructiva, 57,1 s) | `evidencias/M03-E2E-1-LEGAL.txt` |
| **2 · ilegal** | Se retira **temporalmente Sales del arnés** —fuera de `migrate()` y fuera del seed— y se corre el mismo escenario | **ROJO · 1 failed, 2 passed**: «M03 no volvió: migrate() no lo reconstruye», esperaba `1` y recibió `0` | `evidencias/M03-E2E-2-ILEGAL.txt` |
| **3 · sabotaje** | Se sabotea el **detector**: `estadoDeSales()` deja de ver a Sales | **ROJO · 1 failed, 2 passed**, y **en la preparación**: 2,1 s frente a los 48,5 s de la dirección 2. Murió sin soltar ni un schema | `evidencias/M03-E2E-3-SABOTAJE.txt` |

**La diferencia de tiempos entre la 2 y la 3 es parte de la evidencia.** La
dirección 2 tarda 48,5 s porque llega a hacer la cirugía entera y solo falla al
comparar; la 3 muere a los 2,1 s, en la guarda, antes de destruir nada. Si la
guarda no estuviera, la 3 habría destruido el catálogo y después habría
confirmado en verde que «las cero claves foráneas volvieron».

La dirección 2 es la que acredita que el arreglo hace algo: es exactamente el
defecto histórico, provocado a propósito. La 3 es la que impide que la prueba
pase sin haber comprobado lo que dice comprobar — con el detector vacío, «las
cero claves foráneas volvieron» sería trivialmente cierto.

Como evidencia secundaria se conserva además el caso de **omitir
`sales/99_drop.sql` antes de soltar Catalog**: la guarda C6 de M01 se niega, que
es el comportamiento correcto.

Ningún sabotaje queda en HEAD.
