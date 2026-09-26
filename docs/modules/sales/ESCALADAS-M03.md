# Cola de escaladas — M03 Ventas Online

**Creación:** 26 de septiembre de 2026 — America/Lima
**Última verificación:** 26 de septiembre de 2026 — America/Lima
**Commit verificado:** `711bfba7cf3be80baa146b44e79ddf7a633d695d`

## Estado de la cola tras la ronda del 26/09/2026

Respuesta del colíder, vía JP, sobre el commit `617bb28`: **entrega preparatoria aceptada.** Esto
es lo que cambió de dueño o de estado. Las entradas conservan su texto: **lo que cambia es quién
las tiene.**

| § | Estado tras la ronda |
|---|---|
| **§a** SPEC de Diseño | **RESUELTA el 26/09/2026.** Recibida **completa**: 897 líneas, §7 y §8 presentes, cierra en B-05 con «ESTADO FINAL: DETENIDO». Conservada íntegra e idéntica byte a byte en `SPEC-M03-ORIGINAL-DISENO-2026-09-23.md`. La reconciliación está en `SPEC.md` §0 |
| **§b1** Código visible `2026-0147` frente a `ADR-016` | **Abierta, expresamente.** «No inventes respuestas» |
| **§b2** Cancelación y reactivación | **Abierta** |
| **§c** Efectivo frente a Yape | **Abierta** |
| **§d** Navegación entre módulos para «a consultar» | **Pasa a Chat 2.** La *modalidad de acceso* —si «a consultar» exige cuenta— sigue siendo decisión comercial abierta |
| **§e1** `ARQUITECTURA_MODULAR.md:213` apunta al producto | Documento compartido: **de Chat 2** desde el principio |
| **§e2** `order_statuses` como tabla replicada | **Pasa a Chat 2** |
| **§e3** Contrato de M04 para pedidos sin dirección | **Pasa a Chat 2** |
| **§e4** Barrera de fronteras del frontend | **Pendiente de integración.** Cuando llegue a `main`: **merge normal, nunca rebase**, y se registra el SHA en `MATRIZ-DIFERENCIAS-M03.md` §2 |

**Instrucción de la ronda del 26/09:** no se ejecutan tareas bloqueadas ni se piden autorizaciones.
La entrega se conserva para la fase siguiente. Por eso esta cabecera **no añade ninguna entrada
nueva**: solo registra dueño y estado, que es lo que se pierde si no se escribe.

**Segunda ronda del 26/09 — la SPEC anunciada y no recibida.** §a pasó de «pendiente de
incorporación» a «anunciada como recuperada, y no llegada». Queda registrado en
`INCIDENCIA-SPEC-DISENO-M03.md`, que **no se borra aunque la incidencia esté cerrada**: un intento
fallido sin registro es un intento que se repite.

**El contrato de M04 para pedidos sin dirección (§e3) está escalado al líder técnico**, por encima
de Chat 2.

**Tercera ronda del 26/09 — la SPEC llegó completa, y con ella se cierran tres entradas y media.**
Diseño la había detenido sobre cinco preguntas bloqueantes (B-01 a B-05), y **las decisiones del
26/09 resultaron ser sus respuestas** —cuatro de cinco, y la quinta a medias. El cotejo íntegro está
en `SPEC.md` §0.2. Lo que eso mueve en esta cola:

| Entrada | Antes | Ahora |
|---|---|---|
| **§a** SPEC de Diseño | Bloqueaba el paso 1 | **RESUELTA** |
| **§f** ¿El carrito se guarda en el servidor? | Abierta, con mi recomendación de no persistirlo | **RESUELTA, y contra mi recomendación.** La SPEC histórica §5.2 **sí declara** `sales.carts` y `sales.cart_items`, no replicadas, apoyándose en que ADR-017 pone «carrito y sesiones de compra» del lado exclusivo de WEB. Es decisión de Diseño y está bien fundada: **se adopta** |
| **§b1** Código visible | Abierta | **Abierta, y ahora con dos fuentes que la piden.** La SPEC histórica la había planteado ella sola como **B-03**, y por el mismo motivo: «no se propone uno porque lo verá el cliente» |
| **§b2** Cancelación y reactivación | Abierta | **Abierta.** Coincide con **B-05** en su parte no resuelta y con **D-03** |
| **§c** Efectivo · **§d** acceso a M07 · **§e1–§e4** | Abiertas o de Integración | **Sin cambio** |

**Un hallazgo del cotejo que conviene no perder:** Diseño marcó B-01 —quién es la autoridad de
stock— como contradicción arquitectónica, y enumeró tres salidas posibles. **La salida real no era
ninguna de las tres: fue quitar la reserva.** La pregunta estaba bien planteada y su respuesta no
estaba entre las opciones visibles desde dentro.

---

Ninguna entrada de esta cola detiene a las demás. La columna «qué sigue» dice qué trabajo continúa
mientras la entrada espera, porque **una espera no es una parada**.

**Lo primero, porque es lo único que se escribe primero:** la entrada **§b1 no es reversible.**
Un código de pedido que ya se dictó por teléfono no se reformatea. Se decide antes de la primera
migración, y la primera migración ya está bloqueada por otra razón, así que hay tiempo — pero no
hay tiempo *después*.

---

## §a — La SPEC detenida de Diseño · **BLOQUEA EL PASO 1**

- **Fecha:** 26/09/2026.
- **Punto de decisión:** JP reenvía literalmente, dentro del mensaje, la SPEC de M03 que redactó
  el chat de Diseño.
- **Lo comprobado:** no está en `origin/main`, **no está en ninguna rama** del repositorio
  —`git log --oneline --all -- docs/modules/sales/SPEC.md` devuelve vacío— y **no llegó con el
  encargo del 26/09**. No la tengo en esta conversación: este turno empieza con el encargo y con
  el repositorio, nada más.
- **Opciones vistas:** (1) esperar el reenvío literal; (2) redactar una SPEC nueva desde las
  decisiones cerradas. **La (2) está expresamente prohibida** por el encargo y además tiraría el
  trabajo de Diseño.
- **Consecuencia de no resolver:** no hay SPEC, y sin SPEC consolidada el paso 2 no arranca aunque
  llegue la barrera de fronteras. El paso 3 tampoco.
- **Qué sigue mientras espera:** todo lo de este mensaje —matriz de diferencias, barrido de la
  ADR-018, diseño técnico de lo no controvertido, plan de pruebas— y el resto de esta cola.

> **No afirmo haberla leído.** No la recibí.

---

## §b — Transiciones y política que el encargo no fija

### §b1 · El formato del código visible · **NO REVERSIBLE** · decidir antes de la primera migración

- **Fecha:** 26/09/2026.
- **El conflicto, con las dos citas:**
  - El encargo §5 fija el **ejemplo obligatorio `2026-0147`**: año y correlativo.
  - `ADR-016:66` establece que los códigos visibles son «campos aparte, legibles y **con su propia
    serie por nodo**», y su tabla de `:70-79` marca «Lleva la sucursal» → **«Sí, delante»**. El
    ejemplo de la propia ADR es `V-03-000459`. `CLAUDE.md` lo repite en sus convenciones de base
    de datos: «llevan la serie de su nodo delante».
- **Por qué es caro:** `ADR-016` eligió el prefijo de nodo precisamente para no tener que
  renumerar nunca, y avisa de que pasar de esa decisión «no es migrar: es reconstruir». Un código
  ya dictado a un cliente no se puede reformatear, y `2026-0147` no reserva sitio para el nodo.
- **Opciones vistas:**
  1. **`2026-0147` tal cual.** SILLAR WEB es **una instancia en la nube por cliente** (ADR-001,
     ADR-015): hoy hay un solo nodo y los pedidos nacen todos en él, así que no hay colisión
     posible. El riesgo es futuro y solo aparece si un día un pedido nace en otro nodo.
  2. **`2026-0147` con serie delante**, p. ej. `W-2026-0147`. Cumple la ADR-016 desde la primera
     fila y sigue siendo legible y dictable. Cambia el ejemplo obligatorio del encargo.
  3. Guardar la serie en una columna aparte y no mostrarla hasta que haga falta. **Se descarta
     solo**: es la ADR-016 §«meter la sucursal dentro del número» al revés, y el código que ya se
     dictó seguiría sin llevarla.
- **Consecuencia de no resolver:** no se puede escribir la tabla `sales.orders`. El código visible
  es una columna con `UNIQUE`, y su formato decide el generador y la prueba de concurrencia.
- **Qué sigue mientras espera:** todo el resto del diseño de datos, que no depende del formato;
  y el **mecanismo** del correlativo, que sí se puede diseñar salvo su prefijo.
- **No decido yo qué código ve el cliente.**

### §b2 · Cancelación y reactivación

- **Fecha:** 26/09/2026.
- **Lo que el encargo cierra:** Vencido **no** es Cancelado (§6); el personal siempre puede
  registrar un pago tardío (§8); si hay mercancía «el pedido se reactiva **según el flujo
  validado**».
- **Lo que no se deduce de nada cerrado, y son cuatro preguntas:**
  1. **¿A qué estado vuelve** un pedido Vencido al que se le registra un pago? ¿*Pago por
     verificar*, o directo a *Preparando*?
  2. **¿Quién puede cancelar**, y desde qué estados? ¿El cliente, el personal, los dos?
  3. **¿Una cancelación exige motivo?** M07 guarda `invalidated_reason`
     (`docs/modules/b2b/SPEC.md:183`); es el precedente más cercano, no una decisión.
  4. **¿Desde *Entregado* se puede cancelar?** Presumo que no, pero presumir una transición es
     inventar una regla comercial.
- **«El flujo validado» no está escrito en el repositorio.** Lo busqué: no hay SPEC de M03, y
  `DECISIONES-PREVIAS-M03.md` no menciona reactivación.
- **Consecuencia de no resolver:** la máquina de estados queda incompleta, y `PENDIENTES.md:497`
  avisa de lo que cuesta rehacerla tarde: «**con pedidos reales dentro**».
- **Qué sigue mientras espera:** las cinco transiciones de la secuencia principal, que sí están
  cerradas, y sus pruebas.

---

## §c — El efectivo histórico frente al mandato de Yape

- **Fecha:** 26/09/2026.
- **Punto de decisión:** ¿el checkout de v1 ofrece **efectivo** además de Yape?
- **Las dos fuentes, y no coinciden:**
  - `docs/PENDIENTES.md:487-488`, resuelto el 26 de agosto de 2026: «la tienda abre cobrando con
    **Yape y efectivo**, y la tarjeta llega con M11». Está registrado allí **como restricción del
    SPEC de M03**, no como pendiente.
  - El encargo del 26/09 §2 nombra **solo Yape**, con comprobación manual, y **no dice nada del
    efectivo**.
- **Opciones vistas:** (1) v1 solo Yape, y el efectivo pasa a pendiente con disparador;
  (2) v1 con las dos modalidades, como dice `PENDIENTES.md`; (3) `payment_method` admite los dos
  valores en la base y la pantalla pública ofrece solo Yape.
- **Consecuencia de no resolver:** `payment_method` es una columna con `CHECK` sobre una lista
  cerrada. Añadir un valor después es una migración barata; **quitar uno que ya tiene filas, no.**
  Y hay una asimetría operativa real: un pago en efectivo en un pedido de **recojo en tienda** se
  cobra en el mostrador, así que «pagar en efectivo» y «pago por verificar» pueden no ser el mismo
  flujo.
- **Qué sigue mientras espera:** el registro del pago como hecho consumado, que es idéntico en las
  dos modalidades (`PENDIENTES.md:493-495`).
- **No autorizo ni elimino en silencio una modalidad de cobro.**

---

## §d — «A consultar»: ¿hace falta cuenta? · frontera con M07

- **Fecha:** 26/09/2026.
- **Punto de decisión:** un visitante **sin cuenta** ve un producto «a consultar» y pulsa la
  acción. ¿Qué le pasa?
- **Lo leído en el SPEC de M07, que es de frente B y no se toca desde aquí:**
  - «**Públicos — todos con sesión de cliente, ninguno anónimo**»
    (`docs/modules/b2b/SPEC.md:361`).
  - Regla 1: «**Toda solicitud exige cuenta.** Los cuatro endpoints públicos requieren sesión de
    cliente. Es lo que hace **dura** la dependencia sobre M04» (`:442-443`).
  - Criterio verificable: «**Sin sesión de cliente, los cuatro endpoints públicos dan 401 y no
    crean nada**» (`:523`).
  - Y la razón de que sea así, que no es comodidad: «como toda escritura está autenticada, el
    ritmo se limita contra la cuenta… **Sin sesión no se llega a crear nada, así que no hay nada
    que limitar por IP**» (`:370-374`).
- **Por tanto, con el estado actual de M07, la acción de un visitante anónimo termina en 401.**
  Eso no es un conflicto que M03 pueda resolver: es la puerta de M07.
- **Opciones vistas:** (1) la acción lleva a registrarse o entrar, y después a M07 —cuesta una
  cuenta al visitante y es coherente con M07 tal como está—; (2) la acción lleva al **contacto sin
  cuenta de M04**, que `docs/modules/b2b/SPEC.md:370` señala explícitamente como la otra mitad de
  esa frontera, y entonces no es M07 y el encargo §4 habría que reformularlo; (3) M07 abre un
  endpoint anónimo con limitación por IP, que es exactamente la infraestructura que su SPEC dice
  no necesitar.
- **Consecuencia de no resolver:** **no se define la pantalla** del producto «a consultar», tal
  como el encargo ordena. Tampoco se cierra la redacción del contrato con frente B.
- **Qué sigue mientras espera:** la regla de servidor —una línea con `Price == null` se rechaza al
  crear el pedido— que es de M03 y no depende de esto en absoluto.

**Además, y es de costura:** hoy **no existe una vía declarada** para que un módulo del frontend
enlace a una pantalla de otro. Los aportes entre módulos van por
`frontend/src/platform/surfaceRegistry.tsx`, que resuelve *quién ha pintado algo en una superficie*
—portada y pie— y no *a dónde llevo al usuario*. Las rutas se componen en
`frontend/src/app/routes.tsx:9-12` importando cada módulo por su ruta, y ese archivo es de
Integración. Escribir `/solicitudes/...` a mano dentro de M03 sería justo la importación directa
que el encargo §3 prohíbe. **Efecto observable que pido a Chat 2:** que M03 pueda obtener el
destino de «pedir información» sin conocer `modules/b2b/`, y que cuando M07 no está activo ese
destino no exista y la acción no se pinte.

---

## §e — Costura compartida · con su efecto observable

En las tres, el territorio es de Integración o de frente B. **No lo toco: lo pido, describiendo el
efecto, no el cambio.**

### §e1 · `ARQUITECTURA_MODULAR.md:213` apunta al producto, no a la variante

- **Dice:** `sales.order_items.product_id → catalog.products (cruzada, M03→M01)`.
- **Contra:** `ItemSnapshot.cs:9-11` — «Identificador **de la variante, no del producto**: quien
  vende, cuenta o factura lo hace contra ella (SPEC §4.2 y §7)»; y `ProductPickerItem.cs`, nota de
  cabecera: «aquel devuelve **presentaciones**, y aquí se elige un **producto**».
  `DECISIONES-PREVIAS-M03.md` §3 dice lo mismo: «un pedido vende `catalog.product_items`».
- **Efecto que pido:** que el mapa de FK cruzadas del documento diga `order_items.item_id →
  catalog.product_items`, para que nadie escriba la migración desde el mapa equivocado.
- **Riesgo si no se corrige:** es el fallo de la ADR-018 otra vez, pero de nivel: vender contra el
  producto y no contra la variante es el tercero de los tres casos de los que este proyecto se
  salvó por poco.

### §e2 · `order_statuses` como tabla cruza la línea de la ADR-018

- **Dice:** `docs/ARQUITECTURA_MODULAR.md:195` lista `order_statuses` entre las tablas de `sales`,
  y `:214` la referencia desde `orders.order_status_id`.
- **Contra:** las ventas se replican; una tabla de catálogo de estados con clave `integer` no. Es
  el tercer renglón de la tabla de la `ADR-018:28`, y **no da error**: cada base queda coherente
  por dentro.
- **Efecto que pido:** que el documento no obligue a una tabla de estados. Con siete estados fijos
  y cerrados, `text` con `CHECK` da lo mismo sin cruzar la línea.
- **Nota:** los dos textos son de **antes** de la ADR-018, del 15 de agosto. No es un error de
  quien los escribió: es una regla que llegó después.

### §e3 · `ICustomerSnapshotReader` exige una dirección que un pedido de recojo no tiene

- **Dice:** `GetForOrderAsync(Guid customerId, Guid customerAddressId, …)` y
  `CustomerOrderSnapshot.Address` **no nulable**
  (`backend/Sillar.Modules.Crm.Contracts/ICustomerSnapshotReader.cs:12-15`, `:27`). La
  implementación hace un `join` obligatorio contra `CustomerAddresses` y **devuelve `null` si no
  hay dirección activa** (`backend/Sillar.Modules.Crm/Profiles/CustomerSnapshotReader.cs:21-26`,
  `:46-49`).
- **Contra:** encargo §3 — **solo recojo en tienda**. Un pedido no tiene a dónde enviarse.
- **Lo que pasaría sin cambiarlo:** un cliente sin ninguna dirección guardada **no puede comprar**,
  y el motivo no tendría ninguna relación con lo que hizo. Rellenarlo con una dirección inventada
  o pasar un `Guid.Empty` es peor: dejaría un snapshot falso en el pedido.
- **Efecto que pido:** que M03 pueda congelar los datos del cliente —nombre, correo, teléfono,
  documento y si el correo está verificado— **sin pasar una dirección**, y que la dirección siga
  disponible para cuando exista el envío a domicilio.
- **Y hay precedente escrito de que esto se esperaba:** `docs/modules/crm/SPEC.md:328` — «**Este
  contrato no está cerrado hasta que M03 lo estrene.** En M01, mirarlo desde fuera dio dos
  carencias; **usarlo dio cuatro más**… Un contrato no se cierra: se estrena.» Esta es la primera.
- **Dueño: escalado al líder técnico**, por encima de Chat 2 (26/09/2026, segunda ronda). M04 está en `main` y **aprobado e integrado**; el cambio
  es de `Contracts`, y lo decide Integración. Lo que M03 necesita observar sigue siendo lo de
  arriba: congelar al cliente sin pasar una dirección.

### §e4 · La barrera de fronteras del frontend

- **Estado:** no existe. Cuatro comprobaciones en `MATRIZ-DIFERENCIAS-M03.md` §2.
- **Efecto que pido:** que la puerta se ponga roja cuando un archivo de `frontend/src/modules/<a>/`
  importe de `frontend/src/modules/<b>/`, y que **deje pasar** una importación de `shared/` o de
  `platform/`. Con sus pruebas, y con la llamada dentro de `correrPasosDelFrontend()`
  (`scripts/verificar.mjs:2026-2037`).
- **Lo que pido además, y no es un extra:** que la barrera se **provoque en las dos direcciones y
  se rompa su propia autoprueba a propósito** antes de darla por puesta
  (`docs/ANTES-DE-EMPEZAR-UN-MODULO.md` §2). Y que se anote el **SHA** que la incorpora a `main`:
  el paso 2 de M03 lo necesita por escrito.
- **Bloquea:** el paso 2 entero.

---

## §f — El carrito: ¿se guarda en el servidor?

- **Fecha:** 26/09/2026. **Prioridad baja**, pero es una tabla, y las tablas se piden antes.
- **Lo único escrito:** `docs/ARQUITECTURA_MODULAR.md:195` lista las tablas de `sales` como
  `orders`, `order_items`, `order_statuses`. **No hay tabla de carrito.** Y `:98` incluye el
  carrito en el alcance de M03.
- **Opciones vistas:** (1) el carrito vive en el cliente y solo el pedido se persiste —es lo que
  dice el mapa de tablas—; (2) tabla `sales.carts`, y el carrito sobrevive al cierre del navegador
  y se ve desde otro dispositivo.
- **Lo que recomendaría:** la (1), porque es lo escrito y porque un carrito persistido sin
  inventario no gana nada que un carrito en memoria no dé. Pero decide si el cliente pierde su
  carrito al recargar, y eso se ve en pantalla.
- **Consecuencia de no resolver:** ninguna hoy. El paso 2 está bloqueado por §e4 de todas formas.
- **Qué sigue mientras espera:** todo.
