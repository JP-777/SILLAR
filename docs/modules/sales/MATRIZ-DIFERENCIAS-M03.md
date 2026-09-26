# Matriz de diferencias — repositorio frente a las decisiones del 26/09/2026

**Creación:** 26 de septiembre de 2026 — America/Lima
**Última verificación:** 26 de septiembre de 2026 — America/Lima
**Commit verificado:** `711bfba7cf3be80baa146b44e79ddf7a633d695d`

Toda fila cita archivo y línea. Ninguna afirmación de esta matriz se hizo de memoria: cada una se
comprobó abriendo el archivo en `711bfba`.

**Cómo se leen las columnas de decisión:**

| Marca | Significa |
|---|---|
| **APLICAR** | El repositorio y la decisión coinciden, o el repositorio ya trae la restricción. Se usa tal cual |
| **ENMENDAR** | Documento de mi territorio que ya está corregido, con enmienda visible |
| **PEDIR** | Documento o costura de Integración. No se toca: se pide con su efecto observable |
| **ESCALAR** | Decisión de producto o de arquitectura que JP tiene que tomar |
| **BLOQUEA** | Impide avanzar un paso completo del ciclo |

---

## 1 · Estado del repositorio al empezar

| Hecho | Fuente | Clasificación |
|---|---|---|
| El SHA base del encargo, `711bfba`, es **`origin/main`** | `git log -1 origin/main` | OBSERVADO |
| El `main` **local** estaba en `a7416ae` del 21/09: **diez commits atrasado**, no divergente | `git merge-base --is-ancestor a7416ae 711bfba` → cierto | OBSERVADO |
| Lo que trajeron esos diez commits: cierre propuesto de M04, Mailpit en e2e, `CrmCustomersEmailTrimAuthority`, callbacks de ficha cerrada en M01 | `git diff --stat a7416ae 711bfba` | OBSERVADO |
| **No existe `docs/modules/sales/SPEC.md`**, ni en el árbol ni en ninguna rama del repositorio | `git log --all -- docs/modules/sales/SPEC.md` → vacío; `ls docs/modules/sales/` | OBSERVADO |
| `database/modules/sales/` contiene solo un `.gitkeep` | `ls -la database/modules/sales/` | OBSERVADO |
| **No existe `backend/Sillar.Modules.Sales`** ni `frontend/src/modules/sales` | `ls backend/`, `ls frontend/src/modules/` | OBSERVADO |
| M04 **está en `main`**: `backend/Sillar.Modules.Crm` existe en `711bfba`. Su **cierre** está propuesto, no aprobado | `ls backend/`; `docs/ROADMAP_MODULAR.md:67` | OBSERVADO |

**Consecuencia de la última fila.** La regla 5 de `docs/DIVISION-DE-TRABAJO.md:71` —«un módulo no
arranca hasta que aquello de lo que depende duro esté en `main`»— **está satisfecha**. Lo que no
está cerrado es la *aprobación* de M04, y su propio SPEC advierte de por qué eso importa aquí:
«**Este contrato no está cerrado hasta que M03 lo estrene**» (`docs/modules/crm/SPEC.md:328`).

---

## 2 · La barrera de fronteras del frontend — condición del paso 2

| Hecho | Cómo se comprobó |
|---|---|
| **`frontend/scripts/fronteras-frontend.mjs` no existe** | `ls frontend/` — no hay directorio `scripts/` |
| **No existe en ninguna rama, ni ha existido nunca** | `git log --oneline --all -- 'frontend/scripts/fronteras-frontend.mjs'` → vacío |
| **La cadena «fronteras-frontend» no aparece en ningún archivo del repositorio** | `grep -ril 'fronteras-frontend'` sobre `.mjs`, `.ts`, `.json`, `.md` → vacío |
| **`frontend/package.json` no declara ningún script equivalente** | `frontend/package.json:8-17` — los seis scripts de prueba son otros |
| **`scripts/verificar.mjs` no la invoca** | `correrPasosDelFrontend()` en `scripts/verificar.mjs:2026-2037` lista seis pasos, y ninguno es este |

> Se comprobó **por cuatro vías distintas**, y a propósito: `docs/ANTES-DE-EMPEZAR-UN-MODULO.md:§4`
> advierte que «una búsqueda que no encuentra nada es una respuesta igual de falible, y encima
> convincente». Cuatro vías independientes dan la misma respuesta, y una de ellas —abrir
> `package.json` y `verificar.mjs` y leer las listas— no es una búsqueda.

**Lo más parecido que sí existe** es `frontend/tests/frontendHygiene.test.mjs`, invocado como
`test:frontend-hygiene` (`scripts/verificar.mjs:2035`). Comprueba higiene de pantallas —H-12 a
H-20, §18—, **no fronteras de importación entre módulos**: sus aserciones leen ficheros fuente
concretos por su ruta (`frontendHygiene.test.mjs:35,44,72,103,323`). No cubre la regla de
`CLAUDE.md` de que un módulo nunca importa de otro.

**PASO 2 BLOQUEADO.** No se crea ninguna tabla, migración, seed ni `99_drop.sql` hasta que esa
barrera esté en `main` con sus pruebas y llamada desde la puerta, y se anote aquí el SHA que la
incorpora. **Una espera no autoriza crear tablas provisionalmente.**

**SHA de la barrera:** *pendiente — no ha llegado a `main`.*

---

## 3 · Decisiones de producto: qué dice el repositorio y qué manda hoy

| # | El repositorio dice | Dónde | Vigente el 26/09 | Acción |
|---|---|---|---|---|
| 1 | «liberar la **reserva de stock** no cancela el pedido» | `docs/modules/sales/DECISIONES-PREVIAS-M03.md` §1 | No hay reserva. Vence un **plazo para pagar** → Vencido, sin cancelar | **ENMENDAR** ✅ hecho |
| 2 | «**48 horas** naturales… plazo de la **reserva**» | idem §2 | 48 h naturales configurables = **plazo para pagar** | **ENMENDAR** ✅ hecho |
| 3 | «M09 Inventario sale de esta fase y pasa a SILLAR ERP» | `docs/ROADMAP_MODULAR.md:122` | Es la **razón** de que no haya reserva | **APLICAR** — coincide |
| 4 | «la tienda abre cobrando con **Yape y efectivo**» | `docs/PENDIENTES.md:487-488` | El encargo nombra **solo Yape**, y no resuelve el efectivo | **ESCALAR** §c |
| 5 | «El pago se guarda como **hecho consumado** —cuándo, método, referencia y **el nombre** del trabajador—, **nunca una FK a `core.admin_users`**» | `docs/PENDIENTES.md:493-495` | Coincide con «distingue el hecho de pago del estado del pedido» | **APLICAR** — restricción cerrada |
| 6 | «**El estado del pago y el estado del pedido son dos cosas distintas.** Fundirlos obliga a rehacer la máquina de estados con pedidos reales dentro» | `docs/PENDIENTES.md:497-499` | Idéntico al encargo §2 | **APLICAR** |
| 7 | «Sin verificar se puede entrar y mirar. **Comprar, no** — eso lo exigirá M03» | `docs/modules/crm/SPEC.md:238` | No lo contradice nada | **APLICAR** — el checkout exige `EmailVerified` |
| 8 | Los códigos visibles «llevan la **serie de su nodo** delante»: `V-03-000459` | `ADR-016:66-79`; `CLAUDE.md`, §convenciones | Ejemplo **obligatorio** `2026-0147`, **sin serie de nodo** | **ESCALAR** §b1 — **no reversible** |
| 9 | Un pedido vende **la variante**: «Identificador de la variante, no del producto: quien vende, cuenta o factura lo hace contra ella» | `backend/Sillar.Modules.Catalog.Contracts/ItemSnapshot.cs:9-11`; `DECISIONES-PREVIAS-M03.md` §3 | Coincide | **APLICAR** |
| 10 | `sales.order_items.product_id → catalog.products` | `docs/ARQUITECTURA_MODULAR.md:213` | **Contradice la fila 9.** Vende `catalog.product_items`, no `products` | **PEDIR** §e1 — corrección de documento compartido |
| 11 | `sales.order_statuses` es una **tabla**, y `orders.order_status_id` la referencia | `docs/ARQUITECTURA_MODULAR.md:195` y `:214` | Siete estados **fijos**, idénticos para cliente y personal | **PEDIR** §e2 — y ver §4 de abajo: choca con la ADR-018 |
| 12 | «`sales_crm.sql` **ya no está** en esa lista… M03 el 21 [de agosto]» — la dependencia sobre M04 pasó a dura | `docs/ARQUITECTURA_MODULAR.md:235`; `:58` | Coincide | **APLICAR** — FK cruzada directa en la migración, sin script de integración |
| 13 | M07: los cuatro endpoints públicos **exigen sesión de cliente**; sin ella, **401 y no crean nada** | `docs/modules/b2b/SPEC.md:361`, `:442`, `:523` | «a consultar» conduce a pedir información, que es de M07 | **ESCALAR** §d — un visitante sin cuenta no puede completar la acción |
| 14 | «Convertir una cotización en pedido de M03 — **No pedido.** Si aparece, se especifica entonces» | `docs/modules/b2b/SPEC.md:545` | La frontera va en **una** dirección: M03 → M07 | **APLICAR** |
| 15 | `ICustomerSnapshotReader.GetForOrderAsync(customerId, **customerAddressId**)`, y `CustomerOrderSnapshot.Address` **no es nulable** | `backend/Sillar.Modules.Crm.Contracts/ICustomerSnapshotReader.cs:12-15`, `:27`; implementación con `join` obligatorio en `Profiles/CustomerSnapshotReader.cs:21-26` | **Solo recojo en tienda**: un pedido no tiene dirección que congelar | **ESCALAR/PEDIR** §e3 |
| 16 | `list_price` nulo = «consultar precio»; «**Cero no es lo mismo que nulo: cero es gratis**» | `docs/modules/catalog/SPEC.md:192`, `:206`; `ItemSnapshot.cs:26-28` | «a consultar» fuera del carrito | **APLICAR** — y el filtro es sobre **nulo**, jamás sobre «falsy» |
| 17 | `docs/DIVISION-DE-TRABAJO.md` «**no entra en vigor hasta que la lista de Fase 1 esté cerrada**» | `docs/DIVISION-DE-TRABAJO.md:3` | El encargo reparte cuatro frentes según ese documento | **Se registra y se obedece.** Fase 1 no está cerrada, así que el documento se está aplicando antes de su propia condición. No bloquea nada mío |

---

## 4 · El barrido de la ADR-018, hecho antes de la primera FK

`docs/ANTES-DE-EMPEZAR-UN-MODULO.md:§7` lo exige antes del primer `CREATE TABLE`, y `CLAUDE.md`
lo repite: **al escribir cualquier FK, comprobar que las dos tablas están del mismo lado de la
línea.** Las ventas se replican (`CLAUDE.md`, §convenciones; `ADR-016:54`).

| FK que M03 va a querer | Origen | Destino | ¿Vale? |
|---|---|---|---|
| `sales.order_items.order_id → sales.orders` | replicada | replicada | **Sí** |
| `sales.order_items.item_id → catalog.product_items` | replicada | replicada | **Sí** — y es dependencia dura, así que la FK cruzada va en la migración de M03 |
| `sales.orders.customer_id → crm.customers` | replicada | replicada | **Sí** — dura desde el 21/08; sin `sales_crm.sql` (`ARQUITECTURA_MODULAR.md:235`) |
| **quién cobró / quién cambió el estado → `core.admin_users`** | replicada | **no replicada** | **NO.** Se guarda el **nombre** como snapshot. Precedente idéntico y ya decidido: `docs/PENDIENTES.md:493-495` y `docs/modules/b2b/SPEC.md:188` y `:209-217` |
| **`sales.orders.order_status_id → sales.order_statuses`** (catálogo de estados) | replicada | una tabla de catálogo con clave `integer` **no se replica** | **NO.** Es el tercer renglón de la tabla de la `ADR-018:28`, y **no avisa**: cada base es coherente por dentro |

**La quinta fila es un hallazgo, no una preferencia.** `docs/ARQUITECTURA_MODULAR.md:195` y `:214`
describen `order_statuses` como tabla desde antes de que existiera la ADR-018 (15 de agosto). Con
siete estados **fijos** —cerrados por el encargo §6, iguales para cliente y personal— una tabla de
catálogo no aporta nada que un `text` con `CHECK` no dé, y además cruza la línea.

**Propuesta, no decisión de producto:** columna `text` con `CHECK` sobre los siete valores, y la
traducción a la frase visible **en el frontend**, donde ya viven el idioma y la moneda
(`ProductPickerItem.cs`, nota de `PriceVaries`: «el contrato da el número y el hecho; la frase la
pone quien pinta»). Requiere corregir `ARQUITECTURA_MODULAR.md`, que es de Integración → **PEDIR**
§e2.

**La ADR-018 ya lo veía venir para M13:** «si la venta se replica y referencia al usuario,
`core.admin_users` se replica. No hay tercera opción, salvo guardar el nombre del vendedor como
dato snapshot y renunciar a la FK. **Se decide antes de M13**» (`ADR-018`, §«La segunda aplicación
de la regla»). **M03 llega antes que M13 y toca la misma línea**, pero no la reabre: para el pago
la decisión ya está tomada en `PENDIENTES.md:493-495`, y M03 la extiende al cambio de estado por
analogía directa, sin pedir nada nuevo.

---

## 5 · La pregunta del validador, aplicada

> ¿Esto lo necesita **el módulo**, o lo necesita **esta pantalla**?

Aplicada a las dos cosas que este barrido quiso añadir, y el resultado no fue el mismo:

| Lo que se quiso añadir | Veredicto |
|---|---|
| Nombre de quien cambió el estado, como snapshot en el historial | **El módulo.** Vale para los dos productos: el mostrador del ERP necesita exactamente lo mismo, y la ADR-018 lo obliga igual en los dos |
| Un campo con la frase visible del estado guardada en la fila | **Esta pantalla.** Congelaría el idioma en la base. Se descarta: la frase la pone quien pinta |
