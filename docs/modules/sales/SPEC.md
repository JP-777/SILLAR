# SPEC — M03 Ventas Online

- **Creación:** 26 de septiembre de 2026 — America/Lima
- **Última verificación:** 26 de septiembre de 2026 — America/Lima
- **Commit verificado:** `ef8cec2ad42e566c722fd640325948f9492f6cc0`
- **Código del módulo:** `sales`
- **Schema:** `sales`
- **Versión:** 1.0.0
- **Estado:** **Borrador. NO APROBADA.**
- **Fase:** MVP · SILLAR WEB

## VEREDICTO

**RECONCILIADA, NO APROBABLE TODAVÍA. NO AUTORIZA CÓDIGO, MIGRACIONES NI TABLAS.**

La SPEC histórica de Diseño del 23/09/2026 se detuvo sobre cinco preguntas bloqueantes. **Las
decisiones de producto del 26/09 responden cuatro de las cinco**, y la quinta a medias. Lo que
queda abierto son **cuatro decisiones comerciales** y **dos condiciones técnicas**, enumeradas en
§0.3. Ninguna se inventa aquí.

**Lo que cambió el fondo del módulo, y no es un detalle de redacción:** la SPEC histórica está
construida alrededor de una **reserva de existencias** que el producto ya no tiene. M09 Inventario
pasó a SILLAR ERP (`docs/ROADMAP_MODULAR.md:122`), y sin inventario no hay existencia que apartar.
Las 48 horas siguen existiendo, pero son **plazo para pagar**. Eso no retoca tres reglas: **disuelve
la pregunta bloqueante más grave de Diseño** —B-01, quién es la autoridad de stock— porque ya no hay
operación de reserva que necesite autoridad.

---

# 0. Reconciliación · de dónde viene cada cosa

## 0.1 Las dos fuentes, y cuál manda

| Fuente | Qué es | Autoridad |
|---|---|---|
| `SPEC-M03-ORIGINAL-DISENO-2026-09-23.md` | La SPEC histórica de Diseño, **íntegra y sin una sola modificación**. Verificada por Diseño sobre `a7416ae` | **Estructura, criterios y método.** Es el esqueleto de este documento |
| `DECISIONES-VIGENTES-M03.md` | Las nueve decisiones de producto del 26/09, del colíder vía JP | **Producto.** Donde las dos hablen del mismo punto, **manda esta** |

> **La SPEC histórica se conserva íntegra y aparte.** Se comprobó que la copia incorporada es
> **idéntica byte a byte** a la recibida (`sha256` `3efbed1f…6f22e`). No se le añadió cabecera,
> aviso ni enmienda: un documento histórico que alguien retoca deja de servir para lo que sirve.
> Solo cambió su ruta.
>
> **Y no hubo copia incompleta que sustituir.** La ronda anterior anunció un adjunto que no llegó, y
> **deliberadamente no se creó ningún archivo con ese nombre** (`INCIDENCIA-SPEC-DISENO-M03.md`).
> Por eso no hay duplicado: nunca hubo un primer archivo.

**Un dato de la cabecera histórica que importa para leer sus OBSERVADO:** Diseño verificó sobre
`a7416ae`, que **es anterior a `711bfba`**. Por eso su §11 exige «M03 no inicia construcción
mientras M04 siga abierto» y su §14 nota que M04 estaba abierto: en `a7416ae` lo estaba. **M04 está
hoy aprobado e integrado**, y ese criterio está satisfecho (§11 de este documento).

## 0.2 Las cinco preguntas bloqueantes de Diseño, hoy

| # | Pregunta de Diseño | Estado | Qué la resuelve |
|---|---|---|---|
| **B-01** | ¿Quién es la autoridad de stock que reserva y libera de forma atómica? | **DISUELTA** — no respondida: **desaparecida** | 26/09 §1: no hay reserva de existencias en v1. Sin operación de reserva no hay autoridad que designar. Con ella se disuelve también **D-01** |
| **B-02** | ¿Qué constituye exactamente el total que paga el cliente? | **RESUELTA** | 26/09 §3: solo recojo, sin entrega ni tarifa. 26/09 §4: «a consultar» fuera del carrito. **Total = suma de las líneas.** Con ella se resuelve **D-02** |
| **B-03** | ¿Qué formato ve una persona como código del pedido? | **RESUELTA · rectificada el 27/09/2026** | **`P-2026-0147`**: etiqueta de nodo, año, correlativo. `P` ← nodo `principal`, **no una letra universal**. Alinea con la **regla 2 de `ADR-016`** (`:66`), **con excepción ratificada para el reinicio anual**. *(Desplazan a `2026-0147` del 26/09 y a `W-2026-0147` del 27/09.)* **Su mecanismo también quedó cerrado** — §5.4 |
| **B-04** | ¿Qué estados ve el cliente y cuáles maneja el personal? | **RESUELTA** | 26/09 §6: **siete estados, idénticos para cliente y personal.** Con ella se resuelve **D-04** |
| **B-05** | ¿Qué pasa si el Yape llega después de vencer? | **RESUELTA EN SU PARTE CRÍTICA** | 26/09 §8: el personal **siempre** puede registrar el pago; con mercancía se reactiva, sin ella queda aviso operativo visible. **Sigue abierto a qué estado vuelve** — §0.3 (b). **D-03** queda así |

> **B-01 merece una frase aparte.** Diseño la marcó como contradicción arquitectónica y se negó a
> resolverla metiendo stock en M01 o aceptándolo del llamador — con razón. La salida no fue ninguna
> de las que enumeró: fue **quitar la reserva**. Conviene que quede escrito, porque la pregunta
> estaba bien planteada y su respuesta no estaba entre las opciones visibles.

## 0.3 Lo que sigue abierto · esta SPEC no se aprueba con esto dentro

**Decisiones comerciales — de JP. No se inventan.**

| | Qué falta | Dónde |
|---|---|---|
| ~~(a)~~ | ~~**Formato del código visible.**~~ **RESUELTA y rectificada el 27/09/2026: `P-2026-0147`** | `DECISIONES-VIGENTES-M03.md` §5 |
| ~~(a′)~~ | ~~**La continuidad del correlativo.**~~ **RESUELTA el 27/09/2026:** reinicio anual ratificado como excepción a la ADR-016, y **contador transaccional por serie `(nodo, año)`** | §5.4 |
| **(g)** | **La etiqueta de serie: ¿por nodo, o por nodo × tipo de documento?** M07 usa «una letra diferente» para el mismo nodo, así que la etiqueta **no es solo del nodo** — y de ello depende contra qué compara el instalador al negarse a arrancar | `ESCALADAS-M03.md` §g |
| **(h)** | **Unicidad de la etiqueta entre instalaciones desconectadas.** La base local no puede demostrarla. **Elevada al líder técnico; no se inventa** | `ESCALADAS-M03.md` §h |
| ~~(k)~~ | ~~Un trabajador que actúa desde un nodo distinto del del pedido.~~ **RESUELTA.** Los tres datos están ratificados e implementados: R-14 |
| ~~(l)~~ | ~~¿Pertenencia o actuación?~~ **RESUELTA: son las dos, en columnas distintas.** `..._admin_user_home_node` guarda la **pertenencia de la cuenta**; `origin_node` conserva la **actuación**. Pueden diferir, y el esquema lo admite |
| **(b)** | **Cancelación y reactivación.** A qué estado vuelve un Vencido pagado; quién cancela y desde dónde; si exige motivo; si se cancela un Entregado | `ESCALADAS-M03.md` §b2 |
| **(c)** | **Efectivo.** `PENDIENTES.md:487-488` dice «Yape **y efectivo**»; el 26/09 nombra solo Yape y no lo resuelve | `ESCALADAS-M03.md` §c |
| **(d)** | **La frontera con M07: la vía y la modalidad de acceso.** Hoy su SPEC exige sesión de cliente en sus cuatro endpoints públicos, pero **eso es el estado de su SPEC, no una modalidad ratificada** para «a consultar». **No se presupone ningún endpoint ni ningún acceso aprobado** | `ESCALADAS-M03.md` §d |

**Condiciones técnicas — no de JP, pero bloquean igual.**

| | Qué falta | Dónde |
|---|---|---|
| **(e)** | **Barrera de fronteras del frontend**, en `main` con pruebas y llamada desde la puerta. Recomprobado tras `git fetch --all`: **no está** | `MATRIZ-DIFERENCIAS-M03.md` §2 · `ESCALADAS-M03.md` §e4 |
| **(f)** | ~~**Contrato de M04 para pedidos sin dirección.**~~ **RESPONDIDO por D el 27/09/2026 — contrato 1.1.0**, con la sobrecarga sin dirección. **Pendiente de certificación e integración**: vive en `integration/m04-contrato-snapshot` (`3758b6e`), **no en `main`**. Sin merge, rebase ni cherry-pick | §6.3 · `ESCALADAS-M03.md` §e3 |

## 0.4 Trazabilidad · cada afirmación histórica desplazada, con su sustituto

| Histórico | Qué decía | Vigente | Autoridad |
|---|---|---|---|
| **VEREDICTO** | «DETENIDA… qué ocurre cuando un pago Yape llega después de vencer la reserva» | Detenida por otras cuatro cosas. B-05 resuelta en su parte crítica | 26/09 §8 |
| **VEREDICTO** | «M04 continúa abierto… ningún código de M03 puede comenzar hasta su cierre» | **M04 aprobado e integrado.** Ese criterio queda satisfecho | Colíder, 26/09 |
| **§3.2 «Stock»** | Barrera bloqueada: falta saber qué contrato compromete existencia | **Suprimida.** No hay barrera de stock porque no hay stock que comprometer | 26/09 §1 |
| **§3.2 «Reserva vencida»** | Probar que al vencer **se libera el stock** | **Reemplazada por «Plazo de pago vencido»**: pasa a Vencido, **nada se libera porque nada se apartó**, no se cancela, se avisa al personal | 26/09 §1 |
| **§3.3** | La operación debe recomprobar «la disponibilidad» y «la creación o renovación del compromiso de stock» | **Ambas suprimidas.** Quedan cliente, item y precio | 26/09 §1 |
| **§3.3 «Punto que impide cerrar»** | «La parte de existencia no tiene un proveedor utilizable por M03» | **Resuelto: no hay parte de existencia** | 26/09 §1 |
| **§3.4 «Stock»** | No se encontró segunda fuente que designe dueño de existencias | **Deja de ser una pregunta** | 26/09 §1 |
| **§4** | Fila «M09 Inventario · PENDIENTE DE DECISIÓN · autoridad sobre existencias/reservas» | **Suprimida.** M09 no participa en SILLAR WEB v1 | `ROADMAP_MODULAR.md:122` |
| **§5.1 «Reserva de stock · NO DECIDIDA»** | No se clasifica hasta resolver la propiedad | **Suprimida.** No se clasifica porque **no existe ni existirá en v1** | 26/09 §1 |
| **§5.2 `sales.orders`** | «El conjunto exacto de estados y el número visible no se cierran aquí» | **Estados cerrados** (siete). **Número visible sigue abierto** | 26/09 §6 · abierta (a) |
| **§6 Eventos** | «reserva vencida y liberada» | **«plazo de pago vencido»**. Sin liberación, porque no hubo retención | 26/09 §1 |
| **§7 Auditoría** | Vocabulario «Reserva de stock, solo después de cerrar B-01» | **Suprimido** | 26/09 §1 |
| **§7 Auditoría** | «Vencimiento de la reserva del pedido {n}; stock liberado.» | **«Vencimiento del plazo de pago del pedido {n}.»** | 26/09 §1 y §9 |
| **R-02** | «Una **reserva** dura 48 horas naturales» | **R-02 nueva: el plazo para pagar dura 48 h naturales.** Mismo número, otro objeto | 26/09 §1 |
| **R-03** | Al vencer: **se libera el stock reservado**, el pedido no se cancela, se avisa al personal | **R-03 nueva:** pasa a **Vencido**, **deja de estar garantizado**, **no se cancela**, se avisa al personal. **Nada se libera** | 26/09 §1 |
| **R-04** | «**Pedido y reserva** son estados distintos» | **R-04 nueva: el hecho de pago y el estado del pedido son estados distintos** | 26/09 §2 · `PENDIENTES.md:497-499` |
| **§9.2** | «No se decide todavía: entrega frente a recojo; si existe costo de entrega; cómo se forma el total; comportamiento de precio nulo» | **Las cuatro decididas.** Solo recojo, sin costo, total = suma de líneas, precio nulo fuera del carrito | 26/09 §3 y §4 |
| **§9** | Conflictos «stock insuficiente» y «reserva que no pudo establecerse» | **Suprimidos.** Añadido: **«a consultar» no entra al carrito** | 26/09 §1 y §4 |
| **§11** | Cuatro criterios de reserva: plazo por defecto, configurable, vencer libera stock, vencer no cancela | **Reescritos sobre el plazo de pago.** «Vencer libera stock» **desaparece**; los otros tres sobreviven cambiando de objeto | 26/09 §1 |
| **§13 D-01** | Contradicción de dependencia alrededor del stock | **Disuelta** | 26/09 §1 |
| **§13 D-02** | El precio de M01 no basta para definir «lo que paga» | **Resuelta** | 26/09 §3 y §4 |
| **§13 D-04** | Estados visibles necesarios pero no cerrados | **Resuelta** | 26/09 §6 |

**Lo que sobrevive de la SPEC histórica sin un solo cambio:** §1, §2, §3.1, §3.2 (las tres primeras
barreras), §3.4 (salvo Stock), §5.1 Pedido y Carrito, §5.2 `order_lines`, `carts` y `cart_items`,
§5.3 entera, §6.1, §6.2, R-01, R-05, R-06, R-07, R-08, R-09, §9.1, §9.3–§9.7, §10, §12, §14, y el
método de verificación OBSERVADO / LEÍDO EN CÓDIGO / DEDUCIDO. **La mayor parte del documento
histórico está vigente**, y conviene decirlo: lo que cambió es un eje, no el trabajo.

---

# 1. Propósito

*(Histórico §1, sin cambio.)*

M03 convierte los artículos publicables de M01 en pedidos de clientes identificados por M04,
conserva la fotografía comercial de lo comprado y gestiona el pago manual mediante Yape.

**OBSERVADO:** `ROADMAP_MODULAR.md:68` coloca M03 como Ventas Online con dependencias duras de M01
y M04. `docs/modules/catalog/SPEC.md` establece que M03 vende contra `catalog.product_items`.
`docs/modules/crm/SPEC.md:318-326` establece que M04 es dueño de la identidad y que M03 guarda
snapshots.

# 2. Valor comercial

*(Histórico §2, sin cambio.)* M03 convierte SILLAR WEB de catálogo consultable en canal de
recepción de pedidos. No sustituye el catálogo de M01, la identidad de M04, el inventario de M09,
el punto de venta de M13, los comprobantes de M14 ni la pasarela de M11.

# 3. Antes de empezar el módulo

## 3.1 ¿Esta regla es cierta porque el mundo es así, o porque hoy solo hay uno?

*(Histórico §3.1, sin cambio, y su segunda observación gana fuerza.)*

Una línea de carrito o de pedido **identifica el item, no el producto**: un producto puede tener
varias presentaciones.

**OBSERVADO:** `backend/Sillar.Modules.Catalog.Contracts/ItemSnapshot.cs:9-11` — «Identificador de
la variante, no del producto: quien vende, cuenta o factura lo hace contra ella».

M03 **no graba como regla universal que existe una sola ubicación de stock**. Con la supresión de la
reserva esto deja de ser un riesgo de contaminación y pasa a ser trivialmente cierto: M03 no habla
de ubicaciones en absoluto.

## 3.2 ¿Alguna vez he visto a esta barrera decir que no?

*(Histórico §3.2. Método sin cambio: cada barrera se provoca en negativo, se comprueba que rechaza
por la causa correcta, se prueba el positivo equivalente, y **se rompe a propósito su propia
autoprueba** para verla ponerse roja.)*

Las barreras de M03 son **cinco**, no las cinco históricas: dos se van y dos entran.

### Cliente sin correo verificado · *sin cambio*

**OBSERVADO:** `docs/modules/crm/SPEC.md:238` — «Sin verificar se puede entrar y mirar. Comprar,
no — eso lo exigirá M03». Negativo: cuenta sin verificar rechazada. Positivo: la misma verificada
continúa.

### Item inexistente o inactivo · *sin cambio*

Negativo: un item que dejó de estar vendible no produce línea válida.

### Precio no lo fija el navegador · *sin cambio*

La operación obtiene el precio autorizado de M01. Negativo: importe manipulado, y la línea usa el
valor autoritativo.

### Producto «a consultar» no entra al carrito · **NUEVA**

`ItemSnapshot.Price` tiene **tres estados, y los tres significan cosas distintas**: `null` es *a
consultar*, `0` es **gratis y se vende**, `> 0` es el precio.

**La comprobación es `Price is null`, jamás una comprobación de «falsy».** Está documentado en
`ProductPickerItem.cs` con el aviso de que **ya mordió una vez**: en la tarjeta pública, «Gratis»
cortocircuitaba antes de llegar al número. **Por eso esta barrera tiene tres direcciones y no dos:**
nulo rechazado, cero **aceptado**, positivo aceptado. Una barrera probada solo con nulo y positivo
no distingue este defecto.

### Plazo de pago vencido · **REEMPLAZA a «Reserva vencida»**

Negativo y positivo: dentro del plazo el pedido no vence; pasado el plazo pasa a **Vencido**. Y
después: **el pedido sigue existiendo y no está cancelado**.

**Y una tercera comprobación que no es de comportamiento sino de lenguaje:** ningún texto visible
—estado, aviso, correo— **afirma** una retención. **Las negaciones correctas sí pasan**: «ya no está
garantizado» es lo que esta SPEC manda decir. La tabla de las dos direcciones está en R-13, y esta
barrera **se provoca en ambas**.

### Suprimida · «Stock»

*(Histórico §3.2 «Stock», marcada BLOQUEADA por B-01.)* **No hay barrera de stock porque no hay
stock que comprometer.** 26/09 §1.

## 3.3 ¿La guarda está en la operación, o en quien la llama?

*(Histórico §3.3, con dos supresiones.)*

La guarda va **en la operación que crea el pedido**, no en la pantalla, el endpoint ni el carrito.
Crear el pedido vuelve a comprobar **dentro de la operación**:

- que el cliente autorizado sigue siendo válido para comprar, **con su correo verificado**;
- que cada item sigue existiendo y es vendible;
- **el precio efectivo actual de cada línea, obtenido de M01**;
- que **ninguna línea tiene precio nulo**.

El carrito, el frontend o el endpoint pueden anticipar estas comprobaciones para responder rápido,
pero **ninguno constituye la barrera**.

**No se acepta:** total calculado por el navegador como fuente de verdad; precio enviado por el
cliente como fuente de verdad; validez comprobada solo al cargar la página.

**Suprimidas del histórico:** «la disponibilidad que corresponda» y «la creación o renovación del
compromiso de stock». **Y con ellas, el «punto que impide cerrar esta sección»:** ya no hay una
parte de existencia sin dueño. Las tres partes que quedan tienen dueño — precio M01, cliente M04,
precio nulo M01.

## 3.4 ¿Lo he comprobado, o me lo ha dicho algo que responde sin fallar?

*(Histórico §3.4, menos «Stock».)*

**AMBOS** — precio y unidad vendible: el SPEC de M01 y su contrato coinciden en `ItemSnapshot` con
`ItemId` y `Price` resuelto. **OBSERVADO** — identidad del cliente en M04. **LEÍDO EN CÓDIGO** — el
arranque monta endpoints solo de módulos activos; el vocabulario de auditoría se compone de
contribuciones de módulos activos y no resuelve en silencio colisiones de `entityType`.

## 3.5 ¿Esto lo necesita el módulo, o lo necesita esta pantalla?

*(No está en el histórico. Se añade porque `CLAUDE.md` lo exige al construir, y dos casos ya
aparecieron.)*

| Lo que se quiso añadir | Veredicto |
|---|---|
| Nombre de quien cobró y de quien cambió el estado, como snapshot | **El módulo.** El mostrador del ERP necesita lo mismo, y la `ADR-018` obliga igual en los dos |
| Una columna con la frase visible del estado | **Esta pantalla.** Congelaría el idioma en la base. Descartado: la frase la pone quien pinta |

# 4. Dependencias

| Módulo | Tipo | Qué necesita M03 | Si falta |
|---|---|---|---|
| CORE | Dura / plataforma | Capacidades, auditoría, configuración, infraestructura modular | SILLAR no funciona como plataforma |
| **M01 Catálogo** | **Dura** | `ItemId`, snapshot comercial, precio efectivo, estado del item | M03 no puede vender |
| **M04 Clientes** | **Dura** | Identidad de sesión de cliente, snapshot del cliente, correo verificado | M03 no puede aceptar pedidos |
| M07 B2B | **Blanda** | Un destino para «pedir información» en los productos «a consultar». **Cuál, y con qué acceso, no está decidido** — (d) | **Degrada sin fallar:** la acción no se pinta. Nunca una excepción porque falte |

**OBSERVADO:** `docs/ARQUITECTURA_MODULAR.md:58` declara M01 y M04 duras. La dependencia sobre M04
pasó a dura el 21/08 y por eso **`sales_crm.sql` ya no está en la lista de integraciones**
(`:235`): la FK cruzada va directa en la migración de M03.

**Suprimida la fila histórica de M09.** M09 Inventario pasó a SILLAR ERP
(`docs/ROADMAP_MODULAR.md:122`) y no participa en SILLAR WEB v1.

## Dependencias de paquetes

**Ninguna nueva.** El pago manual no requiere SDK, cliente Yape ni biblioteca de pagos. Si alguien
propone una durante la implementación, se detiene y se aplica íntegro §6 de
`ANTES-DE-EMPEZAR-UN-MODULO.md` antes de aceptarla.

# 5. Modelo de datos — decisiones previas al primer `CREATE TABLE`

**NO AUTORIZA CREAR TABLAS.** Responde §7 de `ANTES-DE-EMPEZAR-UN-MODULO.md` antes de que exista la
primera migración, que es cuando sale barato.

## 5.1 Qué se replica

**Pedido y sus líneas: sí.** PK `uuid` v7 generada por la aplicación, `origin_node`, `row_version`,
y las FK entre replicadas también `uuid`. **OBSERVADO:** `ADR-016:54` enumera «Ventas y sus líneas»
entre las replicables.

> ### La ADR-016 tiene dos cosas dentro, y solo una está en conflicto
>
> Conviene separarlas explícitamente, porque tener una disputa abierta «con la ADR-016» invita a
> creer que algo de su decisión principal está en duda, **y no lo está.**
>
> | De la ADR-016 | Qué dice | M03 |
> |---|---|---|
> | **Decisión** (`:45`) | Las tablas que se replican usan `uuid` v7 como PK; las que no, `integer IDENTITY` | **Adoptada sin reserva.** `orders` y `order_lines` con `uuid` v7; `carts`, `cart_items` y el contador con `integer IDENTITY` |
> | **Regla 1** (`:65`) | El identificador lo genera **la aplicación**, no la base | **Adoptada sin reserva** |
> | **Regla 2** (`:66`, con su tabla `:70-79`) | Ningún identificador se muestra al usuario, y los códigos visibles son campos aparte «**con su propia serie por nodo**» | **Adoptada, con una excepción ratificada.** Ningún `uuid` se presenta, y **`P-2026-0147`** lleva la etiqueta del nodo delante. **Excepcionado solo el «ni reinicie»** de su tabla `:77`: la serie **reinicia cada año**, ratificado por el líder técnico el 27/09 |
> | **Regla 3** (`:80`) | Las FK entre replicadas también `uuid`; y una replicada no referencia a una que no lo es (ADR-018) | **Adoptada sin reserva.** El barrido está en §5.2 |
> | **Regla 4** (`:82`) | Las replicadas llevan nodo de origen y marca de versión | **Adoptada sin reserva** |
>
> **Las cinco entradas están adoptadas, cuatro enteras y la regla 2 con una excepción expresa.** El
> conflicto se cerró el 27/09 al ratificar `P-2026-0147` más el reinicio anual, y **nunca tocó la
> clave primaria**: `uuid` v7 y código visible son dos columnas con dos oficios, que es justo lo que
> la regla 2 existe para sostener.
>
> **La excepción está ratificada, no interpretada.** No se deduce de que el formato lleve el año: la
> ratificó el líder técnico el 27/09 y su texto entra en la ADR-016 por la rama documental de
> Integración. **Esa publicación es trazabilidad, no una condición previa.** Las cuatro columnas las rellena el `DbContext` al guardar, no quien escribe
la entidad (`backend/Sillar.Shared/Replication/IReplicatedEntity.cs`).

**Carrito y sesión de compra: no.** **OBSERVADO:** ADR-017 los coloca en el lado exclusivo de WEB.
Conservan `integer GENERATED ALWAYS AS IDENTITY`.

**Reserva de stock: no existe.** *(Histórico §5.1 la dejaba «NO DECIDIDA» hasta resolver su
propiedad.)* **No se clasifica porque no hay nada que clasificar**, y **no se crea
`sales.stock_reservations` ni por conveniencia ni por previsión**. Si algún día el ERP trae
inventario, será una decisión de entonces con su propio disparador.

## 5.2 El barrido de la ADR-018, hecho antes de la primera FK

`CLAUDE.md` lo pide como comprobación mecánica: **al escribir cualquier FK, mirar si las dos tablas
están del mismo lado de la línea.**

| FK | Origen | Destino | ¿Vale? |
|---|---|---|---|
| `order_lines.order_id → orders` | replicada | replicada | **Sí** |
| `order_lines.item_id → catalog.product_items` | replicada | replicada | **Sí** — dependencia dura, FK cruzada en la migración de M03 |
| `orders.customer_id → crm.customers` | replicada | replicada | **Sí** — dura desde el 21/08, sin script de integración |
| `cart_items.item_id → catalog.product_items` | **no** replicada | replicada | **Sí** — segundo renglón de `ADR-018:28`: la fila origen se queda en su nodo y su destino está ahí |
| **quién cobró / quién cambió el estado → `core.admin_users`** | replicada | **no** replicada | **NO.** Se guardan **tres datos congelados** —nombre, identificador local y nodo de pertenencia de la cuenta—, documentados como «dato de bitácora, no puntero». Ver R-14 |
| **`orders.order_status_id → sales.order_statuses`** | replicada | tabla de catálogo `integer`, **no** replicada | **NO.** Tercer renglón de `ADR-018:28`, **y no da error**: cada base queda coherente por dentro. El estado va en una columna `text` con `CHECK` |

> **La última fila es un hallazgo y no una preferencia.** `docs/ARQUITECTURA_MODULAR.md:195` y `:214`
> describen `order_statuses` como tabla, **desde antes de que existiera la ADR-018** (15 de agosto).
> Con siete estados fijos y cerrados, una tabla de catálogo no aporta nada que un `CHECK` no dé, y
> cruza la línea. La corrección de ese documento **es de Integración** — `ESCALADAS-M03.md` §e2.

## 5.3 Tablas que sí pueden declararse conceptualmente

### `sales.orders` — replicada, PK `uuid` v7

Conserva snapshots suficientes para que cambios posteriores de catálogo o de cliente **no
reescriban lo que ocurrió**. Conceptualmente:

- **snapshot del cliente:** nombre, correo, teléfono, documento. **Sin dirección** — solo recojo
  (R-10). *Obtenerlo depende de la condición abierta (f).*
- **`status`:** `text` con `CHECK` sobre los **siete** valores de §9.0.
- **`payment_due_at`:** cuándo vence el plazo para pagar. **No es una reserva**, y ninguna pantalla
  lo llama así.
- **`total_amount`:** `numeric(12,2)`, `CHECK >= 0`. Es la suma de las líneas y nada más (R-15).
- **`is_active`:** baja lógica siempre; nunca `DELETE` físico.
- **código visible:** `text` con `UNIQUE`. **Formato `P-2026-0147`** —etiqueta de nodo, año,
  correlativo— rectificado el 27/09. Es una columna **independiente de la PK**, no una
  representación de ella. Su mecanismo está en §5.4.

### `sales.order_lines` — replicada, PK `uuid` v7

Una fila por `catalog.product_item` vendido, con el snapshot congelado: `item_id`, nombre del
producto, variante, unidad de venta, cantidad, **precio unitario congelado**, y subtotal derivable.
`product_id` se guarda **sin FK y solo para agrupar en informes**, que es exactamente lo que
`ItemSnapshot.cs:12-15` dice que es.

`CHECK`: cantidad `> 0`, precio `>= 0`.

### `sales.carts` y `sales.cart_items` — no replicadas

El carrito de la tienda WEB. **El precio visible en el carrito no es precio contractual:** la
operación final vuelve a consultar la autoridad de M01 (R-09).

**No se decide aquí** si el carrito sobrevive sesiones ni cuánto dura; esas decisiones no hacen
falta para fijar su no replicación, y el histórico ya lo dejaba así.

### Los dos hechos que no son estados

Separar el **hecho de pago** del **estado del pedido** está cerrado en `PENDIENTES.md:497-499`, con
su motivo escrito: «Fundirlos obliga a rehacer la máquina de estados **con pedidos reales dentro**».

- **Pagos:** cuándo, método, referencia y **la atribución del personal** —los **tres** datos, todos
  obligatorios—. `payment_method` es `text` con `CHECK` sobre lista cerrada, **y cuál es esa lista está
  abierto** — (c).
- **Historial de estados:** de qué estado a cuál, cuándo, y **la atribución**, que aquí es **los tres o
  ninguno**: los tres `NULL` significa que lo hizo el sistema.

**Las dos atribuciones son fotografías inmutables de tres datos, no punteros** (R-14): nombre congelado,
identificador local, y **nodo de pertenencia de la cuenta**. **Ninguna FK hacia `core.admin_users`, y
ningún `JOIN`.**

> **`origin_node` de estas dos tablas es otra cosa y la conserva:** es **dónde ocurrió la actuación**.
> El nodo de pertenencia de la cuenta va en su propia columna y **no se deriva de él** — una cuenta del
> nodo A puede registrar una actuación desde el nodo B. Y el `origin_node` **del pedido** no interviene
> en absoluto.

### El contador de la serie · `sales.order_series` — no replicada

**Una fila por serie `(node_code, year)`.** PK `integer GENERATED ALWAYS AS IDENTITY`, `UNIQUE
(node_code, year)`, y `last_number` con `CHECK >= 0`. **No se replica**, porque el contador es del
nodo: es exactamente la pregunta de la `ADR-016` respondida con un no —esta fila no puede nacer en un
nodo y tener que existir en otro, y **si existiera en los dos dejaría de contar**.

**ADR-018 limpia:** ninguna tabla replicada la referencia. El pedido guarda el código como `text`,
**no una FK** hacia ella. Comprobado en el barrido de §5.2.

**La etiqueta visible del nodo no vive aquí.** Se **deriva del `NodeIdentity.Code`** y se fija en un
ajuste propio durante la instalación, leído por `ISettingsReader`. **Nunca una letra por defecto**:
`P` es el ejemplo del nodo `principal`, no un valor universal. **El instalador se niega a arrancar**
si la etiqueta está vacía o coincide con otra ya presente en la base — y el alcance real de esa
garantía está en (h), porque una base local no demuestra unicidad entre instalaciones desconectadas.

## 5.4 El número visible · unicidad y continuidad, las dos decididas

**Rectificación de JP del 27/09/2026.** Lo que en la versión anterior de este documento quedaba
abierto —el mecanismo— **está cerrado**, y las dos propiedades siguen siendo distintas aunque ahora
las dos se exijan.

| | Qué es | Estado |
|---|---|---|
| **Unicidad** | No hay dos pedidos con el mismo código dentro de su serie | **Exigida.** `UNIQUE` sobre la columna |
| **Continuidad** | La serie no salta ningún número | **Exigida desde el 27/09** — «sin huecos APROBADO» |

**`UNIQUE` demuestra unicidad y no continuidad**, y por eso no basta: un índice único deja pasar
`P-2026-0001, P-2026-0002, P-2026-0007` sin decir una palabra, porque los tres son distintos. **La
continuidad la da el mecanismo.**

### El mecanismo · decidido, no elegido por mí

1. **Una fila de contador por serie `(nodo, año)`** — `sales.order_series`.
2. **`UPDATE … RETURNING` dentro de la MISMA transacción** que persiste el pedido.
3. **El número se pide al final, justo antes de confirmar el pedido. Nunca al crear el carrito.**
4. **No `nextval()`.**

**Por qué no una secuencia, y es la razón que esta SPEC ya documentaba antes de que se decidiera:**
`nextval` es **deliberadamente no transaccional** —entrega el número fuera de la transacción para no
serializar a quien lo pide—, así que **una transacción revertida consume el número igual** y el hueco
queda para siempre. No es configurable: es lo que una secuencia es. Con «sin huecos» aprobado, **la
secuencia queda excluida**, no penalizada.

**El punto 3 no es un detalle de implementación.** Pedir el número al crear el carrito lo consumiría
en cada carrito abandonado, y los carritos abandonados son la mayoría. Pedirlo al final significa que
**solo un pedido que llega a confirmarse gasta un número**.

### Lo que el mecanismo cuesta, dicho en voz alta

La fila de la serie **se bloquea** mientras la transacción vive, así que **la confirmación de pedidos
de una misma serie se serializa**: el segundo espera a que el primero confirme o deshaga. A escala de
una tienda es irrelevante —y el punto 3 lo acota al tramo más corto posible—, pero es la contrapartida
de la continuidad y **no se esconde**.

### Reinicio anual · con excepción ratificada a la ADR-016

**La serie vuelve al inicio al cambiar el año.** `P-2026-9999` → `P-2027-0001`.

Eso contradice la tabla de la **regla 2** de la `ADR-016` (`:77`), que admite renumerar «mientras no
salte **ni reinicie**». **El líder técnico ratificó expresamente la excepción el 27/09:** se conserva
el nodo delante y **se excepciona solo el «no reinicia»**. Su texto entra en la ADR-016 por la rama
documental de Integración, y **esa publicación es trazabilidad, no una condición previa**.

**Consecuencia concreta:** el primer pedido de un año nuevo puede llegar **dos veces a la vez**, y
entonces las dos transacciones intentan crear la fila `(nodo, año)` que no existe. Se resuelve con un
`INSERT … ON CONFLICT DO NOTHING` seguido del `UPDATE … RETURNING`, dentro de la misma transacción, y
**se prueba provocándolo**: dos confirmaciones simultáneas sobre un año sin fila previa.

### Hasta dónde llega la promesa de continuidad

**No se promete continuidad** ante borrados, correcciones manuales ni repartición entre nodos no
definida. La garantía es estrecha y exacta: **dentro de una serie `(nodo, año)`, un pedido confirmado
consume un número y uno que falla no consume ninguno.**

**Y la continuidad es de cada serie, no global.** Dos nodos numerando cada uno desde 1 es lo correcto
y no un salto — `ADR-016:79`, «SUNAT exige serie propia por punto de emisión». Hoy hay **un solo
nodo**, y por eso no se nota: es el caso del §1 de `ANTES-DE-EMPEZAR-UN-MODULO.md`, una regla que se
graba mal cuando solo hay uno.

## 5.5 Colaciones

*(Histórico §5.3, sin cambio de criterio.)* **No se introduce una colación no determinista por
costumbre.** Se decide columna por columna según la comparación real.

- **Snapshots de nombre y descripción:** evidencia histórica, no claves de búsqueda. **Texto
  ordinario** mientras no exista un requisito concreto.
- **Código visible:** se busca **exacto**. Sin colación no determinista ni trigram. *Su formato
  sigue abierto — (a).*
- **Si la lista del panel llega a buscar por nombre de cliente**, ese snapshot pasa a `core.es_search`
  con su índice, y entonces **`LIKE`, `ILIKE` y regex quedan prohibidos sobre él**: la salida es
  `COLLATE "C"` **en la expresión**, con el índice trigram usando **la misma expresión**.
  **OBSERVADO:** §7 de `ANTES-DE-EMPEZAR-UN-MODULO.md`; la salida ya trabajada en
  `docs/modules/crm/DATOS.md:102`. Cuesta un minuto ahora y un bloqueo a mitad de migración después.

# 6. Contrato público de M03

## 6.1 Historial de pedidos del cliente

**M03 publica el contrato. Nadie está obligado a consumirlo.**

El contrato permite consultar pedidos por `customer_id` **sin que quien pregunte lea tablas de
`sales`**. M03, además, **muestra sus propios pedidos al cliente** en su pantalla «Mis pedidos»
(§9.4): esa superficie es de M03 y no espera a ningún otro módulo.

**Quién hace qué con esto, y es la línea que conviene no torcer:**

| Módulo | Qué le corresponde | Estado |
|---|---|---|
| **M03** | **Publicar el contrato** y mostrar «Mis pedidos» (§9.4) y el detalle (§9.5) | Este documento |
| **M04** | **Rellenar su propio hueco** en la ficha del cliente, **pidiendo el contrato al contenedor y comprobando si vino** —no al registro de módulos—. Su perfil de tienda ya existe y es suyo | En `main`, aprobado |
| **M08** | Un portal que **consolidaría** pedidos y trabajos de M06 en una sola superficie | **Detenido, Fase 3** |

> **M08 no es dueño del historial de pedidos, y no se declara consumidor obligatorio.** El dueño de
> la función es M03, que la publica y la muestra; M04 la consume hoy en su hueco. `ARQUITECTURA_MODULAR.md:116`
> describe M08 como algo que «muestra pedidos **si M03 está activo**» — es decir, **un consumidor
> condicional de un contrato ajeno, no el titular de la función**, y ni esa consolidación ni su
> alcance están ratificados. **M03 no escribe nada para M08**, no adapta su contrato a un consumidor
> detenido, y no se bloquea por él.
>
> **OBSERVADO:** `docs/modules/crm/SPEC.md:341-345` — el hueco de M04 «se declara, no se improvisa»,
> y lo hace pidiendo el contrato al contenedor: «el registro responde según la foto de las
> activaciones; **el contenedor, según lo que de verdad se puede llamar**».

## 6.2 Consulta de pedido

Devuelve una representación **basada en snapshots históricos**. No recompone un pedido antiguo con
los nombres actuales.

## 6.3 Lo que M03 consume de M04 · contrato 1.1.0

**Fuente:** `docs/modules/crm/CONTRATO-SNAPSHOT.md` y
`backend/Sillar.Modules.Crm.Contracts/ICustomerSnapshotReader.cs`, en
`integration/m04-contrato-snapshot` = `3758b6e3a9367bde8a262b4efd9f6927343aa1cd`. **Rama pendiente
de certificación: se lee, no se fusiona.**

**Con solo recojo, M03 usa la sobrecarga sin dirección:**

```
Task<CustomerOrderContactSnapshot?> GetForOrderAsync(Guid customerId, CancellationToken ct)
```

Y devuelve exactamente los campos que §5.3 congela: `FullName`, `Email`, `Phone`, `DocumentType`,
`DocumentNumber`, `EmailVerified`. **Ningún campo de más, ninguno de menos.**

**Un cliente válido sin ninguna dirección guardada recibe instantánea.** Era la carencia que D-05
describía y es la que el contrato resuelve: **nadie deja de poder comprar por no tener dirección.**

### Las tres cosas que este contrato deja en manos de M03

1. **`EmailVerified` no decide nada por sí solo.** El contrato lo entrega; **la regla es R-07**.
2. **`null` no dice por qué**, y no se deduce. Ver el conflicto de §9.8.
3. **La instantánea es una foto, no una referencia.** M03 **guarda su propia copia** en `sales.orders`.
   Una lectura posterior devolvería el estado actual, que ya no es lo que ocurrió (R-08).

### Y una regla de acoplamiento que conviene escribir ahora

`DocumentType`, cuando existe, es `"dni"` o `"ruc"` — lo impone `ck_customers_document_type`, que es
**de M04**. **La columna snapshot de M03 no lleva `CHECK` que replique esa lista.** Copiar la
restricción de otro módulo en la propia tabla ataría el historial de pedidos a una regla ajena: el día
que M04 admita un tercer tipo, un pedido válido dejaría de poder guardarse. **Un snapshot guarda lo
que había, no lo que hoy es válido.**

## 6.4 Eventos publicados

- pedido creado;
- pago confirmado manualmente;
- **plazo de pago vencido** — *(histórico: «reserva vencida y liberada». No hay liberación porque no
  hubo retención.)*

Los nombres técnicos definitivos pertenecen a la implementación.

## 6.5 Eventos consumidos

**Ninguno modifica pedidos históricos.** Los pedidos conservan snapshots precisamente para no
reescribirse cuando cambie M01.

# 7. Auditoría

M03 **escribe auditoría con certeza**, y su vocabulario entra como contribución del módulo.

**Entidades:** `order` → **Pedido**. Y, si el modelo final la necesita como fila propia, la
confirmación de pago → **Confirmación de pago**.

*(Suprimida del histórico: «la entidad que finalmente represente la reserva → Reserva de stock, solo
después de cerrar B-01».)*

## La regla del resumen · nombra la fila, no la clase

**OBSERVADO:** §5 de `ANTES-DE-EMPEZAR-UN-MODULO.md`, y `ADR-016:66` prohíbe presentar UUID a
usuarios.

**Correcto:**

- «Creación del pedido {código visible}.»
- «Confirmación manual del pago Yape del pedido {código visible}.»
- «Vencimiento del plazo de pago del pedido {código visible}.» — *(histórico: «Vencimiento de la
  reserva del pedido {n}; stock liberado.»)*
- «Registro de pago tardío del pedido {código visible}.»

**Incorrecto:** «Creación de un pedido.» · «Actualización de pago.» · «Modificación de reserva.»

El `uuid` técnico **no sustituye** al nombre humano de la fila. **El código visible tiene la forma
`P-2026-0147`** desde la rectificación del 27/09.

# 8. Reglas de negocio cerradas

## R-01 · Medio de pago inicial · *sin cambio*

Pago por **Yape**, con **confirmación manual por personal**. No se integra pasarela automática.
**El hecho de pago no se marca por la pantalla de agradecimiento ni por una declaración del
cliente**: lo confirma una persona que lo comprueba.

*El efectivo sigue abierto — (c).*

## R-02 · Duración del plazo para pagar · **REEMPLAZA a la R-02 histórica**

**48 horas naturales por defecto, configurable por instalación.** Se lee con
`ISettingsReader.Get<T>`, nunca tocando `core.site_settings`.

> **Histórico R-02:** «Una **reserva** dura 48 horas naturales… configurable por instalación.»
> **El número no cambia. El objeto sí:** es plazo para pagar, **no stock apartado**. 26/09 §1.

## R-03 · Vencimiento del plazo · **REEMPLAZA a la R-03 histórica**

Al vencer el plazo:

1. el pedido pasa a **Vencido**;
2. **deja de estar garantizado**;
3. **no se cancela** únicamente por ese vencimiento;
4. se **avisa al personal**.

> **Histórico R-03:** su punto 1 era «se libera el stock reservado». **Se suprime: nada se libera
> porque nada se apartó.** Los puntos 2 y 3 históricos sobreviven, y se añade «deja de estar
> garantizado», que es lo que sustituye a la promesa que la reserva hacía. 26/09 §1.

## R-04 · El hecho de pago y el estado del pedido son cosas distintas · **REEMPLAZA a la R-04 histórica**

El estado del pedido **no se deduce automáticamente** del estado del pago.

> **Histórico R-04:** «**Pedido y reserva** son estados distintos». El par cambia porque uno de sus
> dos miembros desapareció. Y el par nuevo tiene su motivo escrito en `PENDIENTES.md:497-499`:
> fundirlos «obliga a rehacer la máquina de estados con pedidos reales dentro», y es «el mismo
> defecto que M04 corrigió con "de baja" y "bloqueada": **un estado que carga dos significados
> obliga a elegir el peor comportamiento para los dos**».

## R-05 · M03 no hereda la no-caducidad de M07 · *sin cambio*

«No existe caducidad por tiempo» corresponde a presupuestos de M07, no a pedidos de M03.

## R-06 · La identidad del comprador pertenece a M04 · *sin cambio*

M03 no crea identidad alternativa ni usa `core.admin_users` para el comprador.

## R-07 · Correo sin verificar no compra · *sin cambio*

Un cliente puede identificarse y ver su perfil sin verificar. **M03 no acepta la compra** mientras el
correo no esté verificado. **OBSERVADO:** `docs/modules/crm/SPEC.md:238`.

## R-08 · El precio de la línea se congela · *sin cambio*

La línea conserva el precio del momento. Una modificación posterior del catálogo **no reescribe
pedidos existentes**.

## R-09 · El cliente no fija el precio · *sin cambio*

Un total o precio recibido del navegador es **dato no confiable**. La operación obtiene el precio
autorizado de M01.

## R-10 · Solo recojo en tienda · **NUEVA**

No hay entregas a domicilio ni tarifas de envío en v1. **El pedido no guarda dirección de envío.**
26/09 §3.

> Esto es lo que resuelve B-02 y D-02 del histórico, y lo que crea la condición abierta (f): el
> contrato de M04 exige hoy una dirección que un pedido de recojo no tiene.

## R-11 · Los productos «a consultar» no se compran en línea · **NUEVA**

Un item con precio efectivo **nulo** queda fuera del carrito y del pago. La acción **conduce a pedir
información, que pertenece a M07**; M03 no implementa el módulo vecino.

**Cero es gratis y sí se vende.** `null` y `0` no se confunden nunca. 26/09 §4.

### Lo cerrado y lo que no, porque la mitad de esta regla es de otro

**Cerrado, y es de M03:** un item con precio nulo **no entra al carrito ni al pedido**, y el rechazo
está **en la operación**, no en la pantalla (§3.3). Esa mitad no depende de nadie y sus pruebas están
en §11.

**No cerrado, y no es de M03:** **a dónde** conduce la acción y **con qué acceso**.

- **No se presupone ningún endpoint de M07.** Los cuatro de su SPEC son para crear y consultar
  solicitudes y cotizaciones; **ninguno está declarado como destino de «a consultar»**, y M03 no
  inventa uno.
- **No se presupone ninguna modalidad de acceso.** Que su SPEC exija hoy sesión de cliente en todo
  lo público es **el estado de su documento**, no una ratificación de que consultar un precio
  requiera cuenta.
- **La coordinación con frente B alcanza solo a la redacción del contrato y al método de consumo
  permitido.** M07 es de frente B: M03 no modifica su SPEC ni le inventa API.
- **Y falta la vía declarada:** hoy ningún módulo del frontend tiene forma de enlazar a la pantalla
  de otro sin importar de él, lo cual está prohibido. Es costura de Integración.

**Mientras esto no se cierre, la pantalla de un producto «a consultar» no se define** — y M03 sigue
funcionando: sin destino, la acción no se pinta y nada falla. Ver (d).

## R-12 · El pago tardío siempre se puede registrar · **NUEVA**

El personal **siempre** puede registrar un pago, aunque el pedido esté **Vencido**.

- **Si hay mercancía**, el pedido se reactiva.
- **Si no la hay**, queda un **aviso operativo visible** para que alguien llame al cliente.
- **Nunca se acepta un pago sin resultado operativo o pendiente visible.**
- **No hay saldo a favor ni devoluciones automáticas:** son deuda con disparador propio.

26/09 §8. *A qué estado vuelve un pedido reactivado sigue abierto — (b).*

## R-13 · Nada promete mercancía garantizada · **NUEVA · transversal**

**Ningún texto, indicador, estado ni notificación promete existencia garantizada.** Las fechas de
pago **no constituyen una reserva**. 26/09 §9.

Es la regla que cierra el cambio de eje: si sobreviviera una sola frase que diga «te reservamos el
producto», el producto seguiría prometiendo lo que ya no puede cumplir.

### Lo que prohíbe es la promesa, no la palabra

**Distinción obligatoria, y la SPEC ya la necesitaba:** R-03 dice que el pedido vencido «**deja de
estar garantizado**», y §9.8 prescribe decírselo al cliente con «el pedido sigue en pie, pero **ya no
está garantizado**». Las dos son **negaciones correctas** y las dos contienen la palabra.

| Se prohíbe — **afirma** una retención | Se acepta — **la niega** |
|---|---|
| «te reservamos el producto» · «producto apartado» | «ya no está garantizado» |
| «stock garantizado» · «unidades reservadas para ti» | «no está garantizado» |
| «tu reserva vence el…» · «te lo guardamos 48 horas» | «sin garantía de existencia» |
| «disponibilidad asegurada» | «no reservamos existencias» |

> **Un barrido por palabras habría parado en falso sobre la frase que esta misma SPEC aprueba.**
> Es exactamente el modo de fallo que `ANTES-DE-EMPEZAR-UN-MODULO.md` §2 pone en su tabla —«la
> guarda de `.media-e2e`… **Paraba en falso**: confundía "no soy el dueño" con "la creó root"»— y su
> conclusión vale igual aquí: «una barrera que calla te deja seguir; una que para en falso te para.
> **Son la misma enfermedad: no haberla provocado.**»
>
> Esta se cazó sobre el papel, antes de escribir la prueba y antes de que costara nada. **Por eso la
> barrera se provoca en las dos direcciones**, y por eso el corpus positivo de §11 no es un adorno:
> es la mitad que faltaba.

**Tiene prueba propia, con corpus en las dos direcciones** (§11).

## R-14 · La atribución del personal congela tres datos independientes · **AUTORITATIVA**

**Ratificada por el líder técnico.** Es la regla vigente de M03 para toda atribución de personal.

### Los tres datos

| # | Dato | Qué responde | Sin él |
|---|---|---|---|
| **1** | **Nombre visible del trabajador**, congelado al actuar | *Quién fue*, legible dentro de un año | Queda un número que nadie puede resolver si la cuenta se renombró o se dio de baja |
| **2** | **Identificador local del trabajador** | *Cuál de los homónimos*, y permite agrupar y auditar | Dos trabajadores con el mismo nombre son indistinguibles |
| **3** | **Nodo al que pertenece la cuenta** del trabajador | *Contra qué `admin_users` se interpreta el identificador 2* | El identificador es un entero sin universo: el 7 de un nodo no es el 7 de otro |

**Los tres forman una unidad.** Ninguno se deduce de los otros ni de nada más.

### El tercer dato NO es el `origin_node` de la actuación · y es el corazón de la regla

`order_payments` y `order_status_changes` **ya llevan `origin_node`**, porque se replican. Esa columna
**conserva íntegra su semántica** y significa **dónde ocurrió la actuación** — el nodo donde la fila
nació, que es donde estaba la persona en ese momento.

**Nodo de pertenencia y nodo de actuación son hechos distintos y pueden diferir:**

> Una cuenta que pertenece al nodo **A** puede registrar una actuación desde el nodo **B**.
>
> | Columna | Valor | Significa |
> |---|---|---|
> | `..._admin_user_home_node` | **A** | La cuenta vive en el `admin_users` de A. **Contra A se resuelve el identificador local** |
> | `origin_node` de la fila | **B** | La actuación ocurrió en B |
>
> **Ese caso es válido y el esquema lo admite.** No hay `CHECK` que exija que coincidan, y no lo habrá:
> exigirlo prohibiría un hecho real del negocio.

**Tres prohibiciones que se siguen de lo anterior, y ninguna es redundante:**

1. **El nodo de pertenencia no se deriva del `origin_node` de la actuación.** Rellenarlo con el nodo
   actual sería correcto **solo mientras nadie atienda desde otra sucursal**, y fallaría en silencio el
   día que ocurra — que es la peor forma de fallar.
2. **No se usa el `origin_node` del pedido** para resolver al trabajador. Es una tercera columna, en una
   tercera tabla, y puede diferir de las dos anteriores.
3. **No se resuelve con un `JOIN`.** Ver abajo.

### Dato de bitácora, no puntero

> **Ninguna clave foránea hacia `core.admin_users`, y ningún `JOIN` para resolver estos datos.**

La razón no es de estilo: `core.admin_users` **no se replica** y estas tablas **sí**, y la `ADR-018`
prohíbe que una fila que viaja referencie a una que se queda — **sin avisar cuando se incumple**.

La frase va en el código **y en el comentario de la columna en la base**, porque quien inspeccione el
schema con `\d+` ve un entero llamado `..._admin_user_local_id` y su primer impulso es el `JOIN`.
Funcionaría —**en un solo nodo, que es lo peor que puede pasar**.

### Una actuación del sistema no se atribuye a nadie

| Tabla | Actuación humana | Actuación del sistema |
|---|---|---|
| `order_payments` | **Los tres obligatorios** | **No existe.** Un pago lo registra siempre una persona |
| `order_status_changes` | **Los tres presentes** | **Los tres `NULL`** |

**Van los tres o ninguno.** Lo impone un `CHECK`, no una convención: una atribución parcial es peor que
ninguna, porque **parece completa**.

**Trabajador ficticio prohibido.** Ni «Sistema», ni «—», ni identificador cero, ni nodo vacío:
cualquiera de los cuatro convierte una ausencia honesta en una persona que no existe, y dentro de un año
nadie la distinguirá de una real. Los `CHECK` de valor lo impiden uno por uno.

### Alcance

**Toda atribución de personal:** confirmación de Yape, cancelación, reactivación y cualquier cambio de
estado. Hoy son dos tablas; cualquier actuación futura nace con los tres datos.

**Y en ningún caso en la cabecera del pedido.** `sales.orders` **no tiene ninguna columna de atribución
de personal** y no la tendrá: si intervienen varias personas, un único responsable histórico sería
ambiguo. **Cada atribución vive exclusivamente en el registro de su actuación.**

### Las columnas

| Tabla | Nombre congelado | Identificador local | Nodo de la cuenta |
|---|---|---|---|
| `order_payments` | `registered_by` | `registered_by_admin_user_local_id` | `registered_by_admin_user_home_node` |
| `order_status_changes` | `changed_by` | `changed_by_admin_user_local_id` | `changed_by_admin_user_home_node` |

**Por qué `local_id` y `home_node`, y no `_origin`.** La formulación histórica llamaba al identificador
`..._admin_user_id_origin`. Ese sufijo **se leería contra `origin_node`**, que significa otra cosa —el
nodo de actuación—, y confundir las dos es exactamente el error que esta regla existe para evitar.
`local_id` declara que el identificador solo vale dentro de un universo; `home_node` nombra ese universo
sin ambigüedad. **Inglés técnico**, como fija `CLAUDE.md`.

---

### Cronología documental de esta regla

Se conserva entera porque la regla cambió dos veces y conviene saber por qué.

| Fecha | Qué se dijo | Estado |
|---|---|---|
| 26/09/2026 | `PENDIENTES.md:493-495`: el pago guarda **el nombre** del trabajador, «nunca una FK a `core.admin_users`». Precedente ya escrito en `b2b.quotes` (`:188`) | **Vigente en su prohibición**, insuficiente en su alcance |
| 27/09/2026 | Instrucción del líder técnico: la atribución es una **fotografía inmutable** de **dos datos** —nombre congelado más identificador local—, con la frase «dato de bitácora, no puntero» | **SUPERSEDED.** Ver abajo |
| 28/09/2026 | El colíder detecta la insuficiencia y corrige a **tres datos**, añadiendo el nodo de pertenencia de la cuenta. Llegó marcada como pendiente de ratificación | **Corrección aceptada** |
| *(sin fecha en el material recibido)* | **El líder técnico ratificó finalmente la corrección de tres datos.** Chat 2 lo comunicó el 30/09/2026 | **AUTORITATIVA** |

> **Sobre la última fila.** El material recibido **no da la fecha** de esa ratificación final, y **no se
> inventa ninguna**. Lo que consta es el hecho: la corrección **fue ratificada finalmente**, y la
> comunicación llegó el 30/09/2026.

#### La formulación de dos datos · **HISTÓRICA / SUPERSEDED**

Se conserva sin reescribir porque explica qué faltaba:

> *27/09/2026 — «Toda atribución guarda un par: nombre visible congelado, e identificador local del
> trabajador con un nombre que declare que solo significa algo en su nodo. El identificador se interpreta
> junto a la identidad del nodo donde se actuó.»*

**Qué le faltaba, y no era un detalle.** Decía «junto a la identidad del nodo donde se actuó», y eso
llevaba a resolver el identificador contra el nodo **de la actuación**. **Es el nodo equivocado:** el
identificador solo es único dentro del `admin_users` que lo emitió, o sea el nodo **de pertenencia de la
cuenta**. Mientras nadie atienda desde otra sucursal los dos coinciden y el error no se ve — el patrón
del §1 de `ANTES-DE-EMPEZAR-UN-MODULO.md`, una regla cierta «porque hoy solo hay uno».

**Y lo implementado bajo esa formulación también queda superseded:** `c4be225` publicó solo el nombre;
`da4a4f7` publicó el par con el sufijo `_origin`. **Ninguno de los dos es el modelo definitivo.**

## R-15 · El total es la suma de las líneas## R-15 · El total es la suma de las líneas · **NUEVA**

## R-15 · El total es la suma de las líneas · **NUEVA**

Sin costo de entrega, sin tarifas, sin cargos. 26/09 §3. Resuelve B-02.

# 9. Pantallas y estados

Entrada obligatoria del paso 3.5. **Este documento no es el diseño**: lo produce JP con Diseño
cuando los contratos de API estén estables.

## 9.0 Los siete estados visibles · **RESUELVE B-04**

**Idénticos para cliente y personal.** 26/09 §6.

| Secuencia principal | | | | |
|---|---|---|---|---|
| Pendiente de pago | Pago por verificar | Preparando | Listo para recoger | Entregado |

Más **Vencido** y **Cancelado**.

**Vencido no equivale a Cancelado.** Las cinco transiciones de la secuencia principal están
cerradas. **Las de cancelación y reactivación, no** — (b). No se inventan.

## 9.1 Carrito

**Vacío:** explica que todavía no hay productos y ofrece volver al catálogo. **No una tabla vacía.**

**Con datos:** items, presentación, cantidad, precio, e **importe total = suma de las líneas**.
*(Histórico: «BLOQUEADO: no se define todavía que ese parcial sea el "total a pagar" hasta resolver
B-02.» **Desbloqueado por R-15.**)*

**Cargando:** conserva la estructura estable, no inventa progreso, una espera corta no introduce
indicador.

**Conflicto:** debe poder representar que, desde que se añadió un item, **cambió el precio** o **el
item dejó de ser vendible**. *(Suprimido del histórico: «la disponibilidad necesaria ya no está».)*
La acción final **no continúa en silencio con información antigua**.

## 9.2 Confirmación del pedido / checkout · **DESBLOQUEADA**

Trabaja con cliente autenticado, **impide comprar sin correo verificado**, usa el snapshot de M04,
**vuelve a validar precio en la operación** y conduce al pago Yape manual.

*(Histórico: «No se decide todavía: entrega frente a recojo; si existe costo de entrega; cómo se
forma exactamente el total; comportamiento de un producto cuyo precio efectivo es nulo». **Las
cuatro decididas:** R-10, R-15 y R-11.)*

**Vacío:** no aplica. Un carrito sin líneas no entra en una confirmación válida.

**Con datos:** las líneas, el total, y **cuándo vence el plazo para pagar**. *(Histórico:
«Bloqueado por B-02».)*

**Cargando:** representa la validación final sin ocultar lo ya revisado.

**Conflicto:** tratamiento explícito para **precio cambiado**, **item ya no vendible** y **línea a
consultar**. *(Suprimidos: «stock insuficiente» y «reserva que no pudo establecerse».)* **No se
sustituyen por un error genérico.**

## 9.3 Pedido creado / instrucciones de Yape

Referencia humana del pedido, estado **Pendiente de pago**, instrucciones de Yape que JP haya
aprobado, y **cuándo vence el plazo para pagar**.

*(Histórico: «BLOQUEADO: el contenido exacto que ve el cliente después del vencimiento depende de
B-04 y B-05.» **B-04 resuelta; B-05 en su parte crítica.** Lo que queda es (b).)*

**Y aquí R-13 se juega el módulo:** esta pantalla es la que más tienta a escribir «te reservamos el
producto por 48 horas». No lo dice.

## 9.4 Mis pedidos

**Vacío:** «Todavía no tienes pedidos», sin hueco de tabla. **Con datos:** referencia humana, fecha
y estado visible. **Cargando:** estado estable. **Conflicto:** si un pedido cambia de estado con la
lista abierta, la actualización siguiente muestra el estado autoritativo; **no se conserva una
versión local editable**.

## 9.5 Detalle de pedido del cliente

**Vacío:** no existe. **404** si el pedido no existe **o no pertenece al cliente** — y 404 y no 403,
porque distinguirlos convertiría la ruta en un detector de códigos válidos
(`docs/modules/b2b/SPEC.md:376-377`). **Con datos:** snapshots históricos. **Conflicto:** la
respuesta siguiente refleja el estado nuevo.

*(Histórico: «BLOQUEADO: el vocabulario completo de estados visibles al cliente requiere B-04».
**Desbloqueado por §9.0.**)*

## 9.6 Pedidos — panel administrativo

**Vacío:** explica que todavía no existen pedidos. **Con datos:** listado con estado suficiente para
localizar **pagos por verificar** y **pedidos con el plazo vencido que requieren atención**.
**Cargando:** estructura estable sin progreso ficticio. **Conflicto:** la actualización **detecta
que otra persona ya cambió el mismo estado** y recarga la verdad antes de permitir otra transición.

## 9.7 Detalle de pedido — panel

Cliente congelado, líneas congeladas, estado del pedido, **estado del pago**, lo necesario para
confirmar Yape manualmente, y **los avisos al personal**. *(Histórico decía «estado de la reserva».)*

**Conflicto, caso mínimo:** otra persona confirmó el pago con la pantalla abierta; el plazo venció
mientras se intentaba actuar. **La operación no pisa en silencio el estado nuevo.**

## 9.8 Las frases · no se dejan para el final

`CLAUDE.md` lo pide: **ningún «Ha ocurrido un error», ningún botón «Aceptar».** Un conflicto es una
frase que dice **qué lo impide y qué hacer**; un botón **nombra la acción que ejecuta**.

| Situación | Se dice | **No** se dice |
|---|---|---|
| Item con precio nulo | «Este producto se cotiza. Pide información y te decimos el precio.» | «Producto no disponible» |
| Plazo vencido | «El plazo para pagar venció el 28 de septiembre. El pedido sigue en pie, pero ya no está garantizado.» | «Tu reserva expiró» — **R-13** |
| Precio cambiado | «El precio de {producto} cambió de S/ 12,00 a S/ 14,00. Revisa el carrito antes de continuar.» | «Ha ocurrido un error» |
| **La cuenta ya no puede comprar** — `GetForOrderAsync` devolvió `null` | «Esta cuenta no puede completar pedidos ahora mismo. Escríbenos y lo revisamos contigo.» | «Cliente no encontrado» · «Cuenta bloqueada» · «Cuenta dada de baja» — **el contrato no dice el motivo y M03 no lo adivina** |

> **La última fila es la que más tienta a inventar una causa.** M03 llega ahí **con sesión de cliente
> abierta**, así que un `null` significa que la ficha cambió de estado entre el acceso y el pago —de
> baja, bloqueada, o sin cuenta—. El contrato **devuelve `null` sin distinguir el motivo, a propósito**,
> y nombrar uno sería inventarlo. La frase dice **qué lo impide y qué hacer**, que es lo que
> `CLAUDE.md` pide, sin afirmar por qué.

# 10. Endpoints — contrato funcional, no implementación

**No se fijan DTO ni clases.** Todas las escrituras llevan CSRF: `CustomerCsrfEndpointFilter` en las
de cliente, `CsrfEndpointFilter` en las de personal.

**Cliente** — todo con sesión de cliente, **ninguno anónimo**: consultar carrito;
añadir/modificar/quitar líneas; validar la compra; **crear el pedido** (exige correo verificado y
rechaza líneas con precio nulo); consultar pedidos propios; consultar detalle propio (**404, no
403**, para el de otro cliente).

**Administración** — mínimo `editor`: listar pedidos; consultar detalle; **confirmar manualmente el
pago**; **registrar un pago tardío** (R-12); identificar pedidos con el plazo vencido; actuar sobre
las transiciones. **Todas auditadas.**

Cada endpoint con comentarios XML y visible en Swagger.

**NO CERRADO:** las rutas exactas y **las transiciones de cancelación y reactivación** se congelan al
resolver (a) y (b).

# 11. Criterios de aceptación y prueba correspondiente

Nombres de prueba **en español**: la salida de `dotnet test` debe leerse como la lista de reglas que
el sistema garantiza. Las pruebas de lógica **no tocan la base**.

| Criterio | Prueba obligatoria |
|---|---|
| **M04 está aprobado e integrado antes del primer cambio de código** | **SATISFECHO.** M04 en `main`; aprobación del colíder del 26/09 *(histórico: «M03 no inicia construcción mientras M04 siga abierto»)* |
| M03 exige M01 y M04 | Intentar activar sin cada dependencia y observar rechazo explícito; repetir con ambas y observar paso |
| La compra no confía en el precio del navegador | Enviar precio manipulado y comprobar que la línea usa el valor de M01 |
| Se vende contra `ItemId`, no contra `ProductId` | Producto con dos variantes: comprar una y comprobar que el snapshot conserva la variante exacta |
| Cliente sin correo verificado no compra | No verificado rechazado; el mismo verificado aceptado |
| **Un item «a consultar» no entra al carrito · uno gratis sí** | **Tres direcciones:** `null` rechazado, `0` **aceptado**, `> 0` aceptado |
| El pedido conserva snapshot | Crear pedido, cambiar después nombre y precio de origen, y comprobar que el pedido histórico no cambia |
| **El total es la suma de las líneas** | Pedido de varias líneas: el total no incorpora ningún cargo |
| **Plazo para pagar por defecto = 48 h naturales** | Crear pedido con la configuración por defecto y comprobar el vencimiento exacto *(histórico: «Reserva por defecto = 48 horas»)* |
| **Plazo configurable por instalación** | Cambiar la configuración, crear un pedido nuevo y comprobar que usa el plazo nuevo **sin reescribir los anteriores** |
| **Vencer el plazo pasa el pedido a Vencido** | Forzar el reloj más allá del vencimiento y comprobar el estado *(reemplaza a «Vencer reserva libera stock», **suprimido**)* |
| **Vencer no cancela** | Tras la misma expiración, comprobar explícitamente que el pedido sigue existiendo y **no pasó a Cancelado** |
| Vencer avisa al personal | Provocar la expiración y observar el aviso en el panel |
| **Ningún texto promete mercancía garantizada** | Barrido sobre las cadenas visibles con **dos corpus**: el **negativo** rechaza las construcciones que **afirman** una retención; el **positivo** exige que pasen las **negaciones correctas** —«ya no está garantizado», «no reservamos existencias»—, que son las que esta SPEC prescribe. **Rechazar la palabra en vez de la promesa pararía en falso sobre R-03 y §9.8.** **R-13** |
| **El propio barrido se falsifica** | Introducir a propósito «te reservamos el producto por 48 horas» en un texto visible y comprobar que **se pone rojo**; y después «ya no está garantizado» y comprobar que **pasa**. Sin las dos, el barrido solo protege la versión sana de sí mismo |
| Confirmación de pago es manual | Un pedido pendiente no pasa a confirmado sin acción autorizada del personal |
| **Un pago tardío sobre un pedido Vencido siempre deja resultado o pendiente visible** | Registrarlo y comprobar que **nunca** queda un pago huérfano. **R-12** |
| **Un pago humano exige los tres datos** | Nombre, identificador local y nodo de la cuenta: faltando cualquiera, se rechaza |
| **Un cambio de estado humano lleva los tres presentes** | Y el `CHECK` lo impone, no el código que llama |
| **Un cambio automático lleva los tres `NULL`** | Vencer un plazo y comprobar que el asiento no atribuye nada — ni «Sistema», ni identificador cero, ni nodo vacío |
| **Ninguna atribución parcial es posible** | Provocar las seis combinaciones incompletas y comprobar que el `CHECK` rechaza las seis; después el caso completo y el caso nulo |
| **El nodo de la cuenta puede diferir del nodo de la actuación** | Atribución con `home_node = A` y `origin_node = B`: **válida**. Es un hecho real del negocio y ningún `CHECK` lo prohíbe |
| **El nodo de la cuenta no se deriva del nodo de la actuación** | La atribución se construye con su nodo explícito; el código nunca lo rellena con el nodo actual |
| **El `origin_node` del pedido no interviene en la interpretación** | La atribución es autosuficiente: lleva sus tres datos y no alcanza al pedido |
| **Cero FK hacia `core.admin_users`** | Inspección del esquema y de la migración en todo `sales` |
| **Cero columnas de atribución en `sales.orders`** | Inspección: la cabecera del pedido no atribuye a nadie |
| Confirmar el pago queda auditado | Comprobar la entrada con el pedido humano concreto, no «un pedido» |
| Auditoría nombra la fila | Crear dos pedidos y comprobar que sus resúmenes permiten distinguirlos sin abrir el detalle |
| `orders` y `order_lines` usan `uuid` v7 | Comprobar versión 7 de las PK y presencia de `origin_node` y `row_version` |
| Los carritos locales **no** llevan metadatos de replicación | Inspección de schema |
| **El código visible tiene la forma `P-2026-0147`** | Etiqueta de nodo, año y correlativo. Y **el `uuid` no aparece en ninguna superficie**, que es la otra mitad de la regla 2 |
| **La etiqueta se deriva del `Code` del nodo y no es una letra por defecto** | Cambiar `Sillar:Node:Code` y comprobar que el código visible cambia con él. **Ninguna letra grabada en el producto** |
| **El instalador se niega a arrancar con etiqueta vacía o repetida** | Provocar los dos casos y observar el rechazo explícito; y el caso válido pasando |
| **Dos pedidos concurrentes reciben números distintos y consecutivos dentro de su serie** | N confirmaciones simultáneas: sin colisión **y sin hueco** |
| **Un pedido que falla a mitad no consume número** | Provocar el rollback después de pedir el número y comprobar que el siguiente pedido toma **ese mismo** número |
| **El carrito no consume número** | Abrir y abandonar carritos, y comprobar que la serie no avanzó |
| **El cambio de año crea la serie nueva sin colisión** | Dos confirmaciones simultáneas sobre un año **sin fila previa**: ambas obtienen número, ninguna falla, ninguna repite |
| **La guarda del contador se falsifica** | Romper a propósito la asignación y demostrar que la prueba **pasa a rojo** antes de restaurarla |
| Ningún `uuid` se presenta al cliente ni al personal | E2E sobre pantallas y respuestas visibles |
| No entra dependencia nueva por el pago manual | Comparar manifiestos antes y después; cualquier alta exige §6 |
| Las pantallas cubren vacío, datos, carga y conflicto | E2E/visual por cada pantalla del §9, claro/oscuro, móvil/escritorio |
| **Instalar y desinstalar M03 no deja enlace roto, ruta muerta, hueco visual ni error de arranque** | E2E de instalación y desmontaje: con M03 activo existen navegación y rutas; al desactivarlo desaparecen y el resto arranca. **Innegociable** |
| La ficha de M04 funciona sin M03 | Desactivar M03 y comprobar que el hueco de pedidos queda **explicado** y no falla |
| Reinstalar no requiere datos fantasma | Desinstalar, verificar el resto íntegro, reinstalar y comprobar arranque limpio |
| **Las barreras se han visto fallar y pasar** | Para cada barrera del §3.2, pareja negativo/positivo, **y falsificar al menos una vez su propia prueba** para verla ponerse roja |

**Suprimido del histórico:** «Vencer reserva libera stock — forzar el reloj y comprobar liberación
contra la autoridad de stock que se decida en B-01». No hay liberación ni autoridad.

# 12. Fuera de alcance

*(Histórico §12, más cuatro entradas que las decisiones del 26/09 dejaron fuera.)*

| No lo hace M03 | Dónde vive |
|---|---|
| Pasarela de pago automática | M11, Fase 4 |
| Comprobantes electrónicos | M14, ERP |
| POS y caja | M13, ERP |
| **Ser dueño de existencias, o reservarlas** | M09, **en el ERP** (`ROADMAP_MODULAR.md:122`) |
| Varias sucursales y traslados | M17, ERP |
| Cotizar, o atender lo que no cabe en el carrito | M07 |
| Convertir una cotización en pedido | **No pedido** (`docs/modules/b2b/SPEC.md:545`) |
| Modificar la identidad del cliente, o el catálogo | M04 · M01 |
| **Entregar a domicilio, calcular envío** | **Nada de v1** (R-10) |
| **Saldo a favor, devoluciones** | Pendientes con disparador propio |
| **Recordatorio a las 24 horas** | Pendiente: **cuando el envío de correo esté comprobado en producción**. Hoy se acreditó con Mailpit, «solo para desarrollo/pruebas» (`ROADMAP_MODULAR.md:67`) |
| **Un portal de cliente con historial consolidado** de pedidos **y** trabajos de M06 | **M08, Fase 3 · detenido.** Ver la nota de §6.1: **M08 no es dueño del historial de pedidos** ni se declara consumidor |
| **Decidir políticas de producto que JP no haya cerrado** | §0.3 |

# 13. Deducciones separadas

**Nada de esta sección se convierte automáticamente en decisión de producto.**

**D-01 · DISUELTA.** La contradicción de dependencia alrededor del stock desapareció con la
reserva. No se resolvió: dejó de existir.

**D-02 · RESUELTA.** R-10 y R-15: solo recojo, sin cargos, total = suma de líneas.

**D-03 · RESUELTA EN SU PARTE CRÍTICA.** R-12 fija que el pago tardío siempre se registra y siempre
deja resultado o pendiente visible. **Queda (b)**: a qué estado vuelve.

**D-04 · RESUELTA.** §9.0 cierra los siete estados. **Las transiciones de cancelación y
reactivación siguen en (b)**, y no se deducen de una enum técnica.

**D-05 · NUEVA · el histórico no podía verla.** `ICustomerSnapshotReader` exige `customerAddressId`
y devuelve dirección **no nulable** (`ICustomerSnapshotReader.cs:12-15`, `:27`); la implementación
hace un `join` obligatorio y devuelve `null` sin dirección activa
(`CustomerSnapshotReader.cs:21-26`, `:46-49`). Con R-10 un pedido no tiene dirección. **Un cliente
sin dirección guardada no podría comprar, por un motivo sin relación con lo que hizo.** Rellenarlo
con una dirección inventada es peor: deja un snapshot falso en el pedido. **Condición abierta (f),
escalada al líder técnico.**

> El SPEC de M04 predijo esto: «**Este contrato no está cerrado hasta que M03 lo estrene.** En M01,
> mirarlo desde fuera dio dos carencias; **usarlo dio cuatro más**… Un contrato no se cierra: se
> estrena» (`docs/modules/crm/SPEC.md:328`). **Esta es la primera.**

# 14. Hallazgos registrados fuera del encargo

**No se corrige ninguno aquí.**

1. *(Histórico 1: contradicción stock M03 ↔ M09, registrada como B-01.)* **Disuelta.**
2. *(Histórico 2: `DECISIONES-PREVIAS-M03.md` en `a7416ae` contenía las decisiones de reserva.)*
   **Resuelto:** ese documento lleva **enmienda visible** en su cabecera desde `617bb28`, con su
   texto histórico conservado sin tocar.
3. *(Histórico 3: `ROADMAP_MODULAR.md` conserva referencias históricas sobre la identidad del
   cliente.)* **Sigue vigente**, y ahora hay una segunda: su `:67` describe M04 como «cierre
   propuesto… pendiente de aprobación y fusión», cuando M04 está aprobado e integrado. **Documento
   de Integración: no se edita desde aquí.**
4. **NUEVO.** `docs/ARQUITECTURA_MODULAR.md:213` mapea `order_items.product_id → catalog.products`.
   **Contradice** a `ItemSnapshot.cs:9-11`. Escribir la migración desde ese mapa vendería contra el
   producto y no contra la variante. → `ESCALADAS-M03.md` §e1, **de Integración**.
5. **NUEVO.** `docs/ARQUITECTURA_MODULAR.md:195` y `:214` describen `order_statuses` como tabla de
   catálogo referenciada desde `orders`: cruza la `ADR-018` **sin dar error**. Los dos textos son
   **anteriores** a esa ADR. → `ESCALADAS-M03.md` §e2, **de Integración**.
6. **NUEVO.** No existe **vía declarada** para que un módulo del frontend enlace a una pantalla de
   otro. → `ESCALADAS-M03.md` §d, **de Integración**.

---

## Qué falta para que esta SPEC se pueda aprobar

Las cuatro decisiones comerciales de §0.3 (a)–(d) y las dos condiciones técnicas (e)–(f).

**El orden importa en una sola:** la **(a)** se decide **antes de la primera migración**, porque un
código visible que ya se dictó por teléfono no se reformatea. Las demás se pueden decidir en
paralelo al trabajo que no dependa de ellas.

**Esta SPEC no autoriza código, migraciones, API ni interfaz.**
