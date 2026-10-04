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
| **§b1** Código visible | **RESUELTA y rectificada el 27/09/2026: `P-2026-0147`.** `W-` fue provisional y **ya no está vigente** |
| **§b1-bis** Continuidad y mecanismo | **RESUELTA el 27/09/2026.** Reinicio anual ratificado como excepción a la ADR-016; contador transaccional por serie `(nodo, año)` |
| **§b2** Cancelación y reactivación | ✅ **CERRADA por JP.** R-12 y R-16, con política pura demostrable |
| **§c** Efectivo | ✅ **CERRADA por JP: Yape y efectivo.** R-01 |
| **§d** Acceso a «a consultar» | ✅ **CERRADA por JP: exige cuenta.** R-11 |
| **§o** Dos necesidades de datos, reportadas sin diseñar | **COSTURA DE DATOS** — ver abajo |
| **§p** `ICurrentAdmin` no da dos de los tres datos de R-14 | **ELEVADA por Chat 2** al líder/colíder — ver abajo |
| **§q** `ItemExisteYEstaActivoAsync` no mira el estado del producto | **HALLAZGO del 03/10**, de M01 — ver abajo |
| **§g** ¿Etiqueta por nodo o por nodo × tipo? | ✅ **CERRADA por JP: la serie es nodo × año × tipo.** Coordinación inter-nodo → **M16** |
| **§h** Unicidad de etiqueta entre nodos desconectados | ✅ **CERRADA: queda deliberadamente en M16**, con §g |
| **§i** `core.media_assets → core.admin_users` cruza la ADR-018 | **HALLAZGO del 27/09, de CORE, no de M03** — ver abajo |
| **§j** Dos costuras para que la puerta vea a M03 | ✅ **RESUELTA el 03/10/2026 por frente A**, por traspaso expreso. Evidencias en `evidencias/J-PUERTA-*.txt` — ver abajo |
| **§k** Atribución del personal cuando el nodo del trabajador no es el del pedido | **RESUELTA.** Tres datos ratificados e implementados; R-14 es autoritativa |
| **§l** ¿El tercer dato es el nodo de pertenencia o el de actuación? | **RESUELTA: son los dos, en columnas distintas** |
| ~~**§b2**~~ ~~Cancelación y reactivación~~ | ~~Abierta~~ → **CERRADA por JP.** Fila del 26/09 conservada; el estado vigente está arriba |
| ~~**§c**~~ ~~Efectivo frente a Yape~~ | ~~Abierta~~ → **CERRADA por JP: los dos medios.** Fila del 26/09 conservada |
| ~~**§d**~~ ~~Navegación entre módulos para «a consultar»~~ | ~~La modalidad de acceso sigue siendo decisión comercial abierta~~ → **CERRADA por JP: exige cuenta.** Lo que queda es solo la vía declarada, de Integración |
| **§e1** `ARQUITECTURA_MODULAR.md:213` apunta al producto | Documento compartido: **de Chat 2** desde el principio. **Repetida el 27/09**: no consta en `docs/integracion/COLA-COSTURAS.md` |
| **§e3** Contrato de M04 sin dirección | **RESPONDIDO el 27/09 — contrato 1.1.0**, pendiente de certificación. Ver abajo |
| **§e2** `order_statuses` como tabla replicada | **Pasa a Chat 2** |
| **§e3** Contrato de M04 para pedidos sin dirección | **Pasa a Chat 2** |
| **§e4** Barrera de fronteras del frontend | ✅ **SUPERADO.** Está en `main`: `frontend/scripts/fronteras-frontend.mjs`, `frontend/tests/fronterasFrontend.test.mjs`, e invocada por `scripts/verificar.mjs:2043`. **Deja de ser bloqueo.** SHA en `MATRIZ-DIFERENCIAS-M03.md` §2 |

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

### §b1 · El formato del código visible · **RESUELTA · rectificada el 27/09/2026**

> **`P-2026-0147`** — etiqueta visible del nodo, año y correlativo. **Rectificación final de JP del
> 27/09/2026**, commit `dfec915`, que **sustituye a `W-2026-0147`**: aquél fue provisional y **ya no
> está vigente**, aunque su bloque figure como «VIGENTE» con la fecha de aquel momento.
>
> **`P` no es una letra universal.** Es el ejemplo del nodo cuyo `NodeIdentity.Code` es `principal`.
> La etiqueta **se deriva del `Code` real** y se fija en un ajuste propio en la instalación.
>
> **El motivo de la rectificación es el hallazgo que esta cola había enviado a la auditoría:** «la
> letra `W` anterior **no corresponde a ningún nodo reconocido del sistema**», y el `Code` por defecto
> es `principal` (`NodeIdentity.cs:38`). Lo señalé el 27/09 en la tercera contradicción del informe.
>
> **Y la continuidad también quedó cerrada** en la misma rectificación → **§b1-bis**.
>
> **De las tres opciones de abajo, ninguna era la definitiva.** La 2 acertó en poner serie delante y
> **falló en la letra**, por la razón que yo mismo había escrito dos párrafos más abajo: el prefijo
> tiene que salir de algo que el sistema reconozca. Queda registrado porque es el caso del §1 de
> `ANTES-DE-EMPEZAR-UN-MODULO.md` en su forma más pura: **propuse una letra porque hoy solo hay un
> nodo.**

---

#### Lo que se sopesó, conservado sin tocar

*Acumula el planteamiento del 26/09 y las precisiones sobre las reglas de la ADR-016 añadidas en la revisión posterior. Se conserva entero: es el registro de por qué la opción 1 no valía.*

- **Fecha:** 26/09/2026.
- **El conflicto, con las dos citas:**
  - El encargo §5 fija el **ejemplo obligatorio `2026-0147`**: año y correlativo.
  - La **regla 2** de la ADR-016 (`:66`) establece que los códigos visibles son «campos aparte,
    legibles y **con su propia serie por nodo**», y su tabla de `:70-79` marca «Lleva la sucursal»
    → **«Sí, delante»**. El ejemplo de la propia ADR es `V-03-000459`. `CLAUDE.md` lo repite en sus
    convenciones de base de datos: «llevan la serie de su nodo delante».
- **El conflicto es estrecho, y conviene decir con qué NO lo hay.** La **decisión** de la ADR-016
  (`:45`) —`uuid` v7 en las replicadas, `integer IDENTITY` en las que no— y sus **reglas 1, 3 y 4**
  están **adoptadas sin reserva** por esta SPEC, igual que la primera mitad de la regla 2 (ningún
  `uuid` se muestra). **Lo único abierto es la segunda mitad de la regla 2:** si el código visible
  lleva serie de nodo. El desglose está en `SPEC.md` §5.1.
- **Requiere ratificación expresa de JP**, no interpretación mía: `2026-0147` es un ejemplo
  obligatorio del encargo, y lo que falta es que alguien diga si desplaza a la regla 2 a sabiendas.
- **Y arrastra una sub-pregunta con precio:** si la serie debe además ser **continua**. Unicidad y
  continuidad son propiedades distintas, y la segunda no está ratificada; su desglose —rollback,
  concurrencia y reinicio anual— está en `SPEC.md` §5.4. **El reinicio anual que `2026-0147` implica
  choca a su vez con la misma tabla `:77`**, que admite renumerar «mientras no salte **ni
  reinicie**».
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

### §b1-bis · La continuidad del correlativo · **RESUELTA el 27/09/2026**

> **Todo lo que esta entrada pedía, concretado en la rectificación de JP.** Las tres cosas que faltaban
> y la contradicción, una por una:
>
> | Lo que pedí | Cómo quedó |
> |---|---|
> | Que el no-salto se confirme **como requisito** | **«Sin huecos APROBADO»** |
> | Qué mecanismo, sabiendo que **la secuencia queda excluida** | **Fila de contador por serie `(nodo, año)`**, `UPDATE … RETURNING` en la misma transacción. **No `nextval()`, y por la razón que yo había documentado**: una transacción revertida consume el número |
> | Qué se acepta en la **concurrencia** | Se resuelve concurrencia y rollback **sin prometer continuidad** ante borrados, correcciones manuales ni repartición entre nodos |
> | **El año dentro del código frente al no-reinicio** | **Resuelto por la primera lectura:** reinicio anual, **como excepción expresa a la ADR-016 ratificada por el líder técnico** |
> | Que la continuidad es **por serie y no global** | Confirmado: «la serie es por nodo y año» |
>
> **Y una precisión que la rectificación añade y yo no había visto:** el número se pide **al final,
> justo antes de confirmar el pedido, nunca al crear el carrito**. Sin eso, cada carrito abandonado
> consumiría un número — y los carritos abandonados son la mayoría. Mi diseño decía «en la misma
> transacción que inserta el pedido», que es compatible pero **no lo decía**.
>
> El mecanismo completo, con su coste de serialización y el caso del cambio de año, está en
> `SPEC.md` §5.4.

---

### §g · La etiqueta de serie: ¿por nodo, o por nodo × tipo de documento? · **ABIERTA**

- **Fecha:** 27/09/2026. **Origen:** la propia rectificación, leída entera.
- **Las dos frases que no cierran juntas:**
  - «`P` es el ejemplo correspondiente al nodo cuyo `NodeIdentity.Code` es `principal`» → la etiqueta
    **es del nodo**;
  - «**M07:** sus cotizaciones siguen la misma convención por nodo/año, **con una letra de serie
    diferente**» → para **el mismo nodo**, M07 usa otra letra.
- **Por tanto la etiqueta no es del nodo: es de la pareja (nodo, tipo de documento).** Si lo fuera solo
  del nodo, M07 y M03 compartirían letra en la misma instalación.
- **Y de eso depende una guarda concreta.** La rectificación dice que el instalador «se niega a
  arrancar si la etiqueta es vacía o **coincide con otra ya existente en la base**». ¿Coincide con qué?
  - Si la etiqueta es del nodo: con las de **otros nodos** — y eso es justo lo que una base local no
    puede comprobar (§h).
  - Si es de (nodo, tipo): con las de **otros tipos de documento del mismo nodo** — comprobable en
    local, pero es **otra validación**, y entonces `P` de pedidos y la letra de M07 **deben** diferir
    dentro de la misma instalación.
- **Consecuencia de no resolver:** no se puede escribir la guarda de arranque ni su prueba, y la
  coordinación con frente B sobre la letra de M07 no tiene criterio.
- **Qué sigue mientras espera:** todo el resto del contador y del modelo. La etiqueta se lee de un
  ajuste; **qué valida quién es lo pendiente, no de dónde se lee.**
- **No la decido:** afecta a un código que ve el cliente y a un módulo que no es mío.

---

### §h · Unicidad de la etiqueta entre instalaciones desconectadas · **ELEVADA AL LÍDER TÉCNICO**

- **Fecha:** 27/09/2026. **La rectificación la eleva ella misma**, y con la razón escrita: «la base
  local **no puede demostrar por sí sola** unicidad entre instalaciones desconectadas; elevar la
  solución inter-nodo al líder técnico **sin inventarla**».
- **Lo que M03 sí hace, y es todo lo que puede hacer:** el instalador rechaza etiqueta vacía y rechaza
  la que ya esté en **su** base. **Eso no es unicidad global**, y el documento no dirá que lo sea.
- **Lo que queda fuera de M03:** quién reparte las etiquetas entre nodos, y qué ocurre si dos
  instalaciones desconectadas eligieron la misma y después sincronizan. Es terreno de M16 y del reparto
  de series, no de Ventas.
- **Qué sigue mientras espera:** todo. Hoy hay un solo nodo.
- **No la invento.**

---

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
Integración. Escribir a mano la ruta de una pantalla de M07 dentro de M03 sería justo la dependencia
directa que el encargo §3 prohíbe, **y esta SPEC no nombra ninguna**: no se presupone ni ruta, ni
endpoint, ni modalidad de acceso. **Efecto observable que pido a Chat 2:** que M03 pueda obtener el
destino de «pedir información» sin conocer `modules/b2b/`, y que cuando M07 no está activo ese
destino no exista y la acción no se pinte.

> **Nomenclatura que no es mía.** El colíder sitúa esta consulta «pendiente de **E1 y E2**». Esos dos
> identificadores **no aparecen en ninguno de mis documentos ni en el repositorio**: los busqué. Serán
> de la cola de frente B o de la del propio colíder. **No presupongo qué contienen** y no los uso como
> si los hubiera leído; quedan anotados aquí para poder emparejarlos cuando lleguen.

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
- **RESPONDIDO el 27/09/2026 por D · contrato M04 1.1.0 · pendiente de certificación.**

  > Sobrecarga `GetForOrderAsync(customerId, ct)` → `CustomerOrderContactSnapshot?`, **sin dirección**,
  > en `integration/m04-contrato-snapshot` = `3758b6e`. **No está en `main`**, así que se lee y **no se
  > fusiona**: ni merge, ni rebase, ni cherry-pick.
  >
  > **Resuelve el caso exacto que esta entrada describía:** «un cliente válido sin ninguna dirección
  > recibe instantánea». Nadie deja de poder comprar por no tener dirección guardada.
  >
  > **Y la resolvió por la vía que no rompe la otra mitad.** Yo había pedido «congelar al cliente sin
  > pasar dirección», que admitía hacer `Address` anulable — y eso habría vuelto permisivo el camino
  > con entrega sin avisar a nadie: quien hoy lee `snapshot.Address.AddressLine` recibiría `null` donde
  > el contrato le garantizaba una dirección. **Con dos métodos, el tipo de retorno dice si hay
  > dirección y el compilador impide el error.** Es mejor que lo que pedí.
  >
  > Cómo lo consume M03: `SPEC.md` §6.3. Verificado sin fusionar, leyendo el árbol del SHA.

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


---

## §i · La ADR-018 disparó, y no sobre M03 · **HALLAZGO · territorio de CORE**

- **Fecha:** 27/09/2026. **Cómo apareció:** al escribir la migración de M03 convertí el barrido de la
  ADR-018 en una consulta y la lancé **sobre los cuatro schemas**, no solo sobre el mío. Devolvió una
  fila, y no era de `sales`.

```
origen             destino             constraint
core.media_assets  core.admin_users    fk_media_assets_created_by
```

- **Comprobado columna por columna, no deducido:**

| Tabla | `origin_node` | `row_version` | ¿Se replica? |
|---|---|---|---|
| `core.media_assets` | **sí** | **sí** | **Sí** — ADR-018, decisión 1 |
| `core.admin_users` | no | no | **No** — ADR-018, decisión 4 |

  Y la restricción es `core.media_assets.created_by → core.admin_users.admin_user_id`, con
  `ON DELETE SET NULL`.

- **Es el tercer renglón de la tabla de la propia ADR-018** (`:28`): origen que se replica, destino
  que no. «La fila viaja y su referencia se queda. Apunta a otra cosa, o a nada.»

- **Y la ADR-018 afirma que esto no pasa.** Su decisión 4 dice: «Sesiones, auditoría, configuración y
  activación de módulos siguen siendo enteras y locales. Son del nodo por naturaleza, **y ninguna tabla
  replicada las referencia**». **Esa última frase es falsa desde su propia decisión 1**, que convirtió
  `core.media_assets` en replicada sin revisar a quién referencia.

- **Qué produciría:** una fila de medios que viaja lleva `created_by = 7`, y en el otro nodo el 7 es
  otra persona o no es nadie. Es el mismo síntoma que la ADR-018 describe para las fotos —«un catálogo
  sin fotos, y sin error»—, aquí aplicado a **quién subió el archivo**.

- **La salida ya está escrita en la propia ADR**, para el caso que ella sí previó: «guardar el nombre
  del vendedor como dato snapshot y renunciar a la FK». Es lo que M03 hace en `order_payments` y
  `order_status_changes`.

- **No lo corrijo.** `core` no es mi territorio y M03 no lo necesita para avanzar. Pero **la ADR-018
  llevaba desde el 15 de agosto sin que nadie la provocara como consulta**, y es exactamente el §2 de
  `ANTES-DE-EMPEZAR-UN-MODULO.md`: «una barrera que calla no se distingue de una barrera que funciona».
  Esta calló seis semanas.

- **Sugerencia, no petición:** el barrido es una consulta de doce líneas y podría vivir en la puerta.
  Una regla comprobable que nadie comprueba es una regla escrita, no puesta.

---

## §j · Dos costuras para que la puerta vea a M03 · **PEDIDAS a Integración**

Ninguna bloquea escribir M03; las dos bloquean **verificarlo en la puerta**.

| Fichero | Efecto observable que necesito |
|---|---|
| `backend/Sillar.Api/Sillar.Api.csproj` | Que `Sillar.Modules.Sales.dll` se publique junto al host, para que `ModuleDiscovery` lo encuentre recorriendo los ensamblados. El propio fichero dice en su `:9` que «cuando se añada un módulo nuevo, su `ProjectReference` va aquí y en ningún [otro sitio]» |
| `scripts/verificar.mjs` | Que la etapa de migraciones incluya `Sillar.Modules.Sales` en su lista de módulos, para que el schema `sales` exista en la base efímera |

**Por qué la segunda importa más de lo que parece.** Las pruebas de persistencia de este proyecto solo
corren contra la base efímera de la puerta —el `Fixture` lo exige comparando `SILLAR_VERIFY_DATABASE`
con la base de la conexión, y falla antes de tocar nada—. Mientras `sales` no se migre en esa base,
**no se pueden escribir las pruebas de persistencia de M03 sin poner la puerta roja**: no serían
omitidas declaradas, serían fallos. Por eso este turno entrega solo las de lógica, y las cinco de
persistencia que la rectificación exige —concurrencia, rollback que no consume número, carrito que no
consume, cambio de año, y falsificación de la guarda— van en la entrega siguiente, con estas dos
costuras dentro.

**Mientras tanto están verificadas a mano** contra PostgreSQL real, y las salidas están en el informe
del turno: siete tablas, seis claves foráneas con sus dos cruzadas en `RESTRICT`, siete triggers,
diecinueve `CHECK`, cero cruces de la ADR-018 en `sales`, cero FK hacia `core.admin_users`, y el ciclo
desinstalar/reinstalar con `catalog`, `cms`, `core` y `crm` intactos.


---

## §k · La atribución del personal · **ratificada en su par, pendiente en su trío**

- **27/09/2026 · RATIFICADO por el líder técnico.** La atribución es una **fotografía inmutable**:
  nombre visible congelado al actuar, más **identificador local del trabajador** con un nombre que
  declare su localidad al nodo, **sin FK a `core.admin_users`**, documentado como **«dato de bitácora,
  no puntero»**. Vale para pago, confirmación de Yape, cancelación, reactivación y cambios de estado. Y
  una transición del sistema **no se atribuye a un trabajador ficticio**.

  **Implementado y aplicado** — R-14 y §5.3 de la SPEC, esquema verificado contra PostgreSQL.

- **28/09/2026 · CORRECCIÓN DEL COLÍDER, pendiente de ratificación.** Los datos pasan de dos a
  **tres**: se añade el **identificador del nodo al que pertenece el trabajador**, porque el
  identificador **no debe interpretarse con el `origin_node` del pedido** — un pedido puede nacer en una
  sucursal y ser atendido desde otra.

  **No se cierra en el esquema.** La instrucción lo dice y además es lo barato: añadir una columna es
  aditivo y no hay datos, mientras cerrar un diseño sin ratificar y tener que deshacerlo no lo es.

- **Qué hay que adaptar cuando se ratifique**, y es poco: dos columnas nuevas, sus dos `CHECK` de
  coherencia del trío, los dos comentarios de columna y las pruebas del par. **El modelo no cambia de
  forma**: la atribución ya vive en la fila de la actuación y no en la cabecera.

- **Lo que ya cumple la corrección sin cambio:** ninguna FK a `core.admin_users`; **ningún campo
  `atendido_por` en la cabecera del pedido** —comprobado: `sales.orders` no tiene ninguna columna de
  atribución de personal—; y cada atribución vive solo en el registro de su actuación.

---

## §l · ¿El tercer dato es el nodo de pertenencia o el de actuación? · **ABIERTA**

- **Fecha:** 28/09/2026. **Origen:** leer la corrección entera, no solo su enunciado.
- **La corrección dice una cosa y la justifica con otra:**
  - **Enunciado:** «Identificador del nodo **al que pertenece** el trabajador».
  - **Justificación:** «un pedido puede nacer en una sucursal y **ser atendido desde otra**».
- **No son el mismo hecho.** Alguien de la sucursal A puede atender desde un terminal de la B.

| Lectura | Para qué sirve | Qué deja sin responder |
|---|---|---|
| **Nodo de pertenencia** | **Resolver el identificador.** `admin_user_id` solo es único dentro del `admin_users` que lo emitió | Dónde ocurrió la actuación |
| **Nodo de actuación** | **Historia operativa:** en qué mostrador se cobró | Cómo resolver el identificador |

- **Para el propósito declarado —interpretar el identificador— hace falta el de pertenencia.** Para el
  caso que se usa como justificación hace falta el de actuación. **Puede que hagan falta los dos**, y
  entonces la fotografía tiene cuatro datos, no tres.
- **Consecuencia de no resolver:** se escribiría una columna con un nombre que no dice cuál de los dos
  hechos guarda, y dentro de un año nadie podrá saberlo mirándola. Es el error que la propia frase
  «dato de bitácora, no puntero» existe para evitar, un nivel más arriba.
- **Qué sigue mientras espera:** todo. El par ratificado está aplicado y las pruebas verdes.
- **No lo elijo.** Es el mismo tipo de decisión que el formato del código visible, y por el mismo
  motivo: se escribe una vez y viaja a comprobantes.


---

## §k y §l · **CERRADAS** · la atribución quedó en tres datos y dos nodos

**Comunicado por Chat 2 el 30/09/2026:** el líder técnico **ratificó finalmente** la corrección del
colíder. La regla autoritativa está en `SPEC.md` R-14; la cronología documental —con la formulación de
dos datos marcada como histórica— también.

**§l se resolvió por la vía que no había considerado: no era «uno de los dos», eran los dos.**

| Columna | Qué guarda | Semántica |
|---|---|---|
| `..._admin_user_home_node` | El nodo **de la cuenta** | Contra él se interpreta el identificador local |
| `origin_node` de la fila de actuación | El nodo **donde se actuó** | Replicación, `ADR-016` regla 4. **Intacta** |

**Y el esquema admite que difieran.** Probado contra PostgreSQL con los tres nodos distintos a
propósito: cuenta en `CUENTA_DEL_NODO_A`, actuación en `ACTUACION_EN_NODO_B`, pedido nacido en
`PEDIDO_NACIO_EN_C`. **Ningún `CHECK` compara `origin_node` con `home_node`**, y es deliberado:
exigirlo prohibiría un hecho real del negocio.

**Lo que aprendí de mi propio planteamiento de §l.** Puse las dos lecturas como alternativas —«para
resolver el identificador hace falta el de pertenencia; para la historia operativa, el de actuación»— y
añadí «puede que hagan falta los dos» como una posibilidad remota. **Era la respuesta.** Las dos hacían
falta, y una de las dos ya estaba en la tabla desde el primer día: `origin_node`. La pregunta correcta
no era «cuál de los dos», sino «¿cuántas columnas hay que no estoy contando».

---

## §m · Una barrera que paró en falso sobre su propia documentación · **resuelta en el acto**

La primera versión de la prueba «cero FK hacia `core.admin_users`» buscaba **la palabra**
`admin_users` en la migración. Se puso roja — y el culpable era el **comentario de columna que explica
que NO hay clave foránea**.

**Es el mismo defecto que ya tuvo el barrido de R-13**, y por el mismo motivo: **prohibir la palabra en
vez del hecho**. Ahora busca la declaración —`REFERENCES core.admin_users`, `principalTable:
"admin_users"`, `FOREIGN KEY … admin_users`— y la prueba hermana comprueba que la migración referencia
exactamente dos schemas ajenos, `catalog` y `crm`.

Se anota aquí porque es la segunda vez en este módulo, y la lección de
`ANTES-DE-EMPEZAR-UN-MODULO.md` §2 va justo a eso: «una barrera que calla te deja seguir; una que para
en falso te para. **Son la misma enfermedad.**» La forma de evitarla las dos veces fue la misma:
**escribir qué se prohíbe, no qué palabra se prohíbe.**


---

## §n · Dos defectos técnicos propios, corregidos el 30/09/2026

Los dos salieron de la auditoría comparando M03 con el numerador que M07 ya tenía escrito. **Ninguno
es una decisión de producto:** son defectos demostrados, y se corrigen sin tocar formato, etiqueta,
schema ni §g.

**1 · El año era el de UTC.** `OrderCodeAllocator` usaba `clock.GetUtcNow().Year`. El 31 de diciembre a
las 20:00 de Lima ya es el 1 de enero en UTC, así que **el pedido de esa tarde habría abierto la serie
del año siguiente** y el cliente habría leído un año que no era el de su calendario. Ahora
`OrderCode.AnioDe` convierte explícitamente a `America/Lima`, que es la zona en la que M07 ya numeraba.

> **Por qué no se vio antes.** Es un defecto que solo se manifiesta **una tarde al año**, y ninguna
> prueba lo miraba. Lo destapó comparar con otro módulo que resolvía el mismo problema — que es
> exactamente la segunda vía del §4 de `ANTES-DE-EMPEZAR-UN-MODULO.md`.

**2 · La guarda de transacción estaba escrita y no puesta.** El `<remarks>` decía «se llama dentro de
la transacción que persiste el pedido» y **nada lo comprobaba**. Ahora se comprueba
`Database.CurrentTransaction` y se lanza antes de tocar configuración o base. Mismo efecto que la que
M07 ya demostró.

> Es el §2 de `ANTES-DE-EMPEZAR-UN-MODULO.md` en su forma exacta: **una barrera que nunca ha dicho no
> no se distingue de una que funciona.** Esta no podía decir no.

**Y la continuidad no se debilitó:** verificado contra PostgreSQL que un `ROLLBACK` devuelve el número
—tomó el 1, se deshizo, el siguiente volvió a tomar el 1— y que dos transacciones de la misma serie
reciben **números distintos y consecutivos**, porque la segunda espera el bloqueo de fila.

---

## §g · **SIGUE ABIERTA**, y ahora con una tensión documental confirmada

**No se cierra en este turno y no se toca ni M03 ni M07 para «resolverla».**

Chat 2 verificó que la `ADR-016` contiene hoy una tensión: dice que la etiqueta del nodo va delante,
pero M07 usa **otra letra en el mismo nodo**, y la serie queda definida como `(nodo, año, tipo)`.
**Y M07 no replica ninguna tabla**, así que el motivo que obliga a M03 a llevar nodo no le aplica igual.

Lo que consta en código, sin interpretarlo:

| | M03 | M07 |
|---|---|---|
| Letra | `P`, **derivada del nodo** | `C`, **tipo de documento** |
| Clave de la serie | `(node_code, year)` | `(series_code, year)` — sin nodo |
| Tablas replicadas | Sí | **Ninguna** |

**Es decisión de arquitectura y producto. Queda escalada.**


---

## §b2 · §c · §d · **CERRADAS por JP**

Las tres eran decisiones comerciales y **las tres las cerró JP**. El texto anterior de cada una se
conserva más arriba sin reescribir: registraba qué se sopesó, y sigue explicando por qué hubo que
preguntar.

| § | Estaba abierto | Cerrado así |
|---|---|---|
| **§c** | `PENDIENTES.md:487-488` decía «Yape **y efectivo**»; el encargo del 26/09 nombraba solo Yape | **Los dos.** La tarjeta llega con M11 |
| **§b2** | A qué estado vuelve un Vencido pagado; quién cancela; si exige motivo; si se cancela un Entregado | **Vencido → Preparando** con mercancía, **sin escala**. Solo el personal cancela, **exige motivo**, y **Entregado no se cancela** |
| **§d** | Si «a consultar» exige cuenta | **La exige.** Visitante → iniciar sesión o registrarse → M07. **Sin vía anónima nueva** |

**Lo único que sobrevive de §d, y no es de producto:** la **vía declarada** para que M03 obtenga el
destino de M07 sin conocer `modules/b2b/`. Es costura de Integración, sigue en §j, y **no bloquea**: sin
destino la acción no se pinta y nada falla.

---

## §g y §h · **CERRADAS** · la serie es nodo × año × tipo, y M16 coordina

**Ratificado por JP.** Los pedidos mantienen `P-AAAA-NNNN` y las cotizaciones `C-AAAA-NNNN`; la serie se
entiende conceptualmente por **nodo × año × tipo de documento**. **M03 no cambia schema ni formato otra
vez**, y **M07 no se toca ni se reinterpreta desde esta rama**.

**La coordinación y la garantía de unicidad inter-nodo quedan deliberadamente en M16**, que es el
módulo cuyo trabajo es precisamente eso. Con ello se cierra también §h: **no es un hueco de M03**, y
adelantarlo aquí sería inventar una solución inter-nodo desde un módulo que solo ve su propia base.

### La deuda que queda, para Integración y M16 · **no reabre P/C**

Registrada como pedía el encargo, **sin corregir ninguno de los dos módulos**:

> El almacenamiento actual de M07 guarda la serie como `(series_code, year)` — **sin columna de
> nodo**—, mientras la definición ratificada es **nodo × año × tipo**. Hoy coinciden porque hay un solo
> nodo; el día que haya dos, la tabla de M07 **no puede distinguir** las cotizaciones de cada uno.
> M03 sí puede, porque su clave es `(node_code, year)`.
>
> **No es un error de M07:** su módulo no replica ninguna tabla, así que el problema no se le presenta
> hoy. Es una tensión entre la definición y el almacenamiento, y **le corresponde a quien construya
> M16**, que es donde la multiplicidad de nodos deja de ser hipotética.
>
> **La decisión P/C no se reabre.** Las letras están ratificadas.

---

## §o · Dos necesidades de datos · **REPORTADAS, no diseñadas**

El encargo pide detenerse y reportar en vez de diseñar en silencio. **Ni una ni otra bloquean el
Tramo 1.**

### §o1 · El motivo de la cancelación no tiene dónde guardarse

`order_status_changes` tiene `from_status`, `to_status`, `changed_at` y los tres datos de atribución.
**No tiene columna de motivo.**

- **La política pura ya lo exige** —`OrderTransitionPolicy.Cancelar` rechaza sin motivo, con prueba— así
  que la regla está viva **antes** de que exista la columna.
- **Persistirlo necesita una columna.** El precedente más cercano es `b2b.quotes.invalidated_reason`
  (`docs/modules/b2b/SPEC.md:183`).
- **No la diseño.** Pertenece al tramo que implemente la cancelación, y entonces la pediré con su
  efecto observable.

### §o2 · El pendiente operativo se puede detectar, pero no marcar como atendido

**Detectarlo no necesita nada nuevo:** es derivable sin ambigüedad —*un pedido en Vencido con al menos
un pago registrado*—, y esa consulta alimenta el panel. Está en R-12.

**Lo que no es derivable** es marcarlo **atendido**: quién lo resolvió y cuándo, sin mover el pedido de
estado. Para eso haría falta una columna o una tabla.

> **Y la pregunta que va antes que el esquema, que no es mía:** ¿hace falta? Un pendiente que
> desaparece cuando el pedido cambia de estado puede bastar — el personal llama al cliente, y lo que
> ocurra después mueve el pedido. **Si basta, no hay nada que construir.** Si no basta, es una costura
> de datos y la pido entonces.
>
> No añado la columna «por si acaso»: sin un segundo caso real no se generaliza.


---

## §p · `ICurrentAdmin` no puede alimentar R-14 · **ELEVADA**

Apareció al planificar el tramo de API, y es un bloqueo **independiente de C15**.

`ICurrentAdmin` en `main` expone `AdminUserId`, `Email`, `Role` e `IsInRole`. R-14 exige tres datos:

| R-14 exige | Fuente |
|---|---|
| Identificador local | ✅ `AdminUserId` |
| **Nombre visible congelado** | ❌ **No existe.** Da `Email`, que no es un nombre |
| **Nodo de pertenencia de la cuenta** | ❌ **No existe** |

`core.admin_users` **sí tiene `full_name`** —comprobado en la base— pero **M03 no puede leer esa
tabla**: su propio `<remarks>` dice que ni la tabla ni la cookie ni el token le pertenecen. Y la tabla
**no tiene ninguna columna de nodo**.

**Qué bloquea:** registrar pago, cancelar y cualquier cambio de estado por personal. **Qué no
bloquea:** el vencimiento automático, porque es acción del sistema y sus tres datos van `NULL`.

**Lo que NO se hizo, por instrucción expresa y porque las tres son trampas:**

| Atajo | Por qué no |
|---|---|
| Derivar `home_node` de `NodeIdentity.Code` | Hoy sería correcto —`admin_users` no se replica, así que toda cuenta es local— pero es **la derivación que R-14 prohíbe**, y solo vale mientras ninguna cuenta venga de fuera |
| Usar `Email` como nombre visible | Identifica, pero **no es un nombre**. Y congelarlo sería congelar la decisión |
| Leer `core.admin_users` desde M03 | Prohibido, y además rompería la frontera que hace desmontable el módulo |

**Elevada por Chat 2.** M03 no modifica `ICurrentAdmin`.

---

## Tramo D1–D3 + vencimiento · hecho el 02/10/2026

Primer tramo de API de M03, con `main` (`fb44057`) incorporado por merge normal. **Sin creación de
pedido, sin Catálogo, sin operaciones de personal.**

| Pieza | Qué consume | Estado |
|---|---|---|
| **D1** Congelar al cliente | `ICustomerSnapshotReader`, **sobrecarga sin dirección** | Hecho |
| **D2** Lectura propia | `ICurrentCustomer` | Hecho, dos rutas |
| **D3** `ICustomerOrderHistory` | **Nada de CRM** | Implementado sobre `sales` |
| **Vencimiento** | Nada | Hecho, atribución `NULL` |

**Y el tramo encontró un defecto propio.** `VencimientoDePlazos` abría su transacción **sin mirar si
ya había una**, así que no se podía componer: PostgreSQL no admite anidarlas y la operación reventaba
dentro de otra. Lo destapó la propia prueba, que envuelve cada caso en una transacción para no dejar
filas. Ahora **se une a la del llamador si existe** y solo abre la suya si no hay ninguna.

> **El criterio queda escrito porque las dos operaciones del módulo hacen lo contrario:**
> `OrderCodeAllocator` **exige** transacción abierta —numerar fuera dejaría un hueco en la serie—,
> y `VencimientoDePlazos` **la abre si falta** —vencer plazos es una operación completa por sí misma—.
> La atomicidad la garantiza quien abre la transacción.

### Las pruebas de persistencia y la puerta · **dicho sin adorno**

Las 13 pruebas de D2, D3 y vencimiento corren **contra PostgreSQL real**, no con EF InMemory: lo que
comprueban es traducción, aislamiento y que el `CHECK` de atribución **acepte** tres nulos, y un
proveedor en memoria daría verde sin tocar nada de eso.

**No son destructivas** —cada una vive en una transacción que se deshace—, así que corren contra la
base de desarrollo sin estropearla, al contrario que las de M04 y M02.

> **Pero en la puerta canónica fallarán hasta que se resuelva §j.** La etapa de migraciones de
> `scripts/verificar.mjs` no incluye `Sillar.Modules.Sales`, así que la base efímera no tiene el schema
> `sales` y el `SalesDbFixture` **falla diciendo exactamente eso y nombrando §j**. No se saltan en
> silencio: una omitida sin declarar es peor que una roja, porque la roja se ve. **No toqué
> `verificar.mjs` ni ejecuté la puerta**, las dos cosas por instrucción.


---

## §q · `ItemExisteYEstaActivoAsync` no considera si el producto está de baja

- **Fecha:** 03/10/2026. **Cómo apareció:** M03 es el primer consumidor que usa ese método
  **para vender**, y al leerlo junto a sus vecinos la asimetría salta.

```
ItemExisteYEstaActivoAsync  →  item.Id == itemId && item.IsActive
BuscarPorCodigoAsync        →  item.IsActive && item.Product!.IsActive && …
BuscarAsync                 →  item.IsActive && item.Product!.IsActive && …
```

- **Las búsquedas excluyen las variantes de un producto dado de baja; la comprobación de
  vendibilidad, no.** Así que una variante activa cuyo **producto** se desactivó **pasa** el método
  que pregunta si se puede vender, mientras no aparece en ninguna búsqueda.

- **Qué produciría en M03:** un pedido de un producto que la tienda ya no muestra. No es un error de
  integridad —la FK aguanta— sino de criterio: el catálogo dice «esto no se enseña» y la venta dice
  «esto se vende».

- **No lo compenso, y no puedo.** `ItemSnapshot` no lleva el estado del producto, así que M03 **no
  tiene con qué** comprobarlo; y añadir una comprobación propia sería poner en el consumidor una
  guarda que pertenece a la operación de M01 — exactamente lo que el §3 de
  `ANTES-DE-EMPEZAR-UN-MODULO.md` dice que no hace.

- **M03 usa el contrato certificado como autoridad**, que es lo que el encargo manda, y deja el
  hallazgo escrito. **De frente B / Integración.**

---

## §r · El numerador nunca se había ejecutado por su propio camino

**Lo destapó el primer pedido creado de verdad**, y merece quedar escrito porque es la lección del
§4 de `ANTES-DE-EMPEZAR-UN-MODULO.md` en su forma más limpia.

`OrderCodeAllocator` tomaba el número con `SqlQueryRaw<int>(...).SingleAsync()`. **No funciona:** un
`UPDATE … RETURNING` no es SQL componible, así que EF Core intenta envolverlo en una subconsulta al
añadirle el `Single()` y **falla al traducir**.

> **Y estaba «verificado».** En el turno del paso 2 comprobé a mano, con `psql`, que el rollback
> devolvía el número y que dos transacciones concurrentes recibían números consecutivos. **Las dos
> cosas eran ciertas y ninguna acreditaba el código**: el `psql` no pasa por `SqlQueryRaw`. Comprobar
> por otra vía no acredita la vía que el código usa.
>
> Es además el defecto que C15 acaba de documentar en `main` el 02/10 —«nada que solo se rompa cuando
> EF Core traduce queda demostrado por una prueba en memoria»— y que ahí costó cuatro métodos del
> contrato de M01. Aquí costó uno del numerador, y lo encontró la primera ejecución real.

**Corregido** con un `DbCommand` enlistado en la transacción en curso y un solo
`INSERT … ON CONFLICT DO UPDATE … RETURNING` — el mismo mecanismo que M07 ya usaba, cuya razón ahora
se entiende.

---

## Tramo M03 ↔ Catálogo · hecho el 03/10/2026, con `main` (`4561d9b`) incorporado

| Pieza | Autoridad | Estado |
|---|---|---|
| Vendibilidad de la variante | `ICatalogService.ItemExisteYEstaActivoAsync` | Hecho — ver §q |
| Precio de la línea | `ICatalogService.ObtenerItemAsync` | Hecho. **Nunca del navegador** |
| «A consultar» fuera del pedido | `ItemSnapshot.Price is null` | Hecho. **Cero sí se vende** |
| `ItemSnapshot` congelado en la línea | — | Hecho, con prueba de que no se mueve |
| Cliente congelado | D1, contrato de M04 | Integrado |
| Número al final, en la transacción | `OrderCodeAllocator` | Hecho — ver §r |
| `POST /api/sales/orders` | — | Hecho, con CSRF |

**La forma fuerte de R-09:** la petición **no tiene dónde poner un precio**. No es que se valide y se
descarte — es que `LineaPedida` solo lleva `ItemId` y `Quantity`, y hay una prueba que se pone roja si
alguien añade un campo cuyo nombre contenga precio, total o importe. Un campo que se ignora sigue
invitando a leerlo el día que a alguien le venga bien.

**Y nace `TransaccionDeOperacion`**, porque el mismo defecto apareció **dos veces**: tanto el
vencimiento como la creación abrían su transacción sin mirar si ya había una, y PostgreSQL no las
anida. Se generaliza ahora y no antes porque ahora hay un segundo caso real. **No se aplica al
numerador**, que al contrario **exige** una abierta: numerar fuera dejaría un hueco permanente.


---

## §j · **RESUELTA el 03/10/2026** · la puerta migra Sales

Absorbida por frente A por traspaso expreso de Chat 2. **Nadie la había empezado:** el worktree
`integration/sales-puerta-j` existía reservado y estaba en `main` con **cero** referencias a Sales.

### Los dos cambios, y por qué hacían falta los dos

| Fichero | Por qué |
|---|---|
| `scripts/verificar.mjs` | Añade `Sillar.Modules.Sales` a la lista de la etapa 4, **al final**: sus dos FK cruzadas apuntan a `catalog` y a `crm`, así que esas tablas tienen que existir antes |
| `backend/Sillar.Api/Sillar.Api.csproj` | `migrar()` invoca `dotnet ef` con `--startup-project backend/Sillar.Api`, así que **sin el `ProjectReference` el DLL no está** y la migración no corre. Comprobado: `File '…/Sillar.Modules.Sales.dll' not found`. El propio fichero dice en su cabecera que el `ProjectReference` de un módulo nuevo «va aquí y en ningún otro sitio» |

### La prueba, en las dos direcciones

**SIN Sales en la lista → puerta ROJA**, y no por donde parecía: la etapa 4 pasa —sus migraciones
simplemente no corren— y el rojo sale en la **etapa 5**, cuando el `SalesDbFixture` pide el schema que
nadie creó. Dieciocho pruebas de persistencia, con la causa escrita en el propio mensaje. Evidencia
completa en `evidencias/J-PUERTA-A-SIN-SALES-20261003.txt`.

**CON Sales → etapas 1 a 5 en verde.** Sales migra, las dieciocho pasan a cero y `Sillar.Api.Tests`
queda 3/3.

> **Un módulo que falta en esa lista no se nota donde se añade.** Es la asimetría que conviene tener
> escrita: el hueco está en la etapa 4 y el rojo aparece en la 5, así que quien lea el veredicto irá a
> mirar las pruebas y no la lista.

### Dos cosas que §j rompió, y las dos eran reglas escritas con el nombre de hoy

**1 · `ArranqueConModuloActivoAusenteTests` usaba `CodigoAusente = "sales"`.** Una prueba **de
plataforma** —un módulo activo que el binario no trae impide arrancar— con el nombre del único módulo
que entonces faltaba. **Se rompió el día que M03 entró en el binario.** Es el §1 de
`ANTES-DE-EMPEZAR-UN-MODULO.md` en su tercera señal, literal: ponerle a algo transversal el nombre del
único que lo usa hoy. Ahora el código es `modulo_que_no_existe`, que no puede ser de nadie.

> **Es territorio de CORE/Integración y se tocó porque §j lo rompió.** Si se prefiere revertirlo y
> pedirlo por el cauce, el cambio es una constante y su `<remarks>`.

**2 · Cinco de mis propias pruebas solo pasaban con el catálogo sembrado.** Buscaban una variante
existente con `SELECT … LIMIT 1`, lo encontraban en la base de desarrollo y morían en un
`Assert.NotNull` en la base efímera, que migra el catálogo **vacío**. Ahora cada prueba crea su
producto y su variante dentro de la transacción que se deshace.

> **Una prueba que solo pasa cuando alguien sembró antes no acredita lo que dice acreditar: acredita
> la semilla.** Y lo peor es que su rojo no lo dice — dice que faltaba un dato.

### El verde completo · `EXIT=0`, las seis etapas

**TODO EN VERDE** sobre `c1188f9d`, con la lista de la etapa 4 ya incluyendo Sales. Base efímera
`sillar_verify_1791070672465_933326`. Evidencia en `evidencias/J-PUERTA-B-CON-SALES-20261003.txt`, y
el emparejamiento de las dos direcciones en `evidencias/J-INDICE-20261003.md`.

> **El log del verde tiene catorce líneas y el del rojo mil ciento treinta y cuatro, y no es un
> descuido.** La puerta **solo volca la salida de una etapa cuando esa etapa falla**: en verde
> imprime el rótulo de cada una y el veredicto. La brevedad **es** la forma del verde. Los conteos
> —96/96 de Sales, 3/3 de `Sillar.Api.Tests`— van verificados aparte en el índice, porque el log
> verde no los trae.

### La etapa 6 y lo que la tumbó tres veces antes, que no era M03

Tres intentos, tres causas **ajenas al módulo**:

| Intento | Causa |
|---|---|
| 1 | `Permission denied` en `/app/appsettings.Development.json`. Archivo **gitignored**, con solo niveles de registro —ningún secreto—, modo `0600` en esta copia de trabajo; `COPY . .` lo mete en la imagen y el usuario del contenedor no puede leerlo. **Arreglado solo en local** con `chmod 644`: es configuración de máquina y no entra en un commit de producto |
| 2 | Descargas de NuGet fallando dentro del build de Docker. Transitorio |
| 3 | La corrida se interrumpió sin escribir veredicto |

> **Recomendación para Integración, no aplicada:** `backend/.dockerignore` no excluye
> `appsettings.Development.json`, y un archivo de desarrollo local no tiene nada que hacer en la
> imagen. Es infraestructura compartida y **no se toca desde aquí**.
