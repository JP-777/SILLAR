# SPEC — M03 Ventas Online

## VEREDICTO

DETENIDA · REQUIERE DECISIONES DE JP ANTES DE PODER CERRARSE.

La arquitectura base de M03 sí puede especificarse, pero la SPEC completa no puede aprobarse todavía porque aparecen decisiones de producto no resueltas que determinan qué puede comprar el cliente, qué importe paga y qué ocurre cuando un pago Yape llega después de vencer la reserva.

No se resuelven dentro de esta SPEC.

No autoriza código, migraciones ni creación de tablas. M04 continúa abierto y M03 tiene dependencia dura de M04. Ningún código de M03 puede comenzar hasta su cierre.

- Creación: 23 de septiembre de 2026, 19:15 -05:00 — America/Lima
- Última verificación: 23 de septiembre de 2026, 19:15 -05:00 — America/Lima
- Commit verificado: `a7416aece5117433ff5d1ab96d69cc24120065ac`
- Código del módulo: `sales`
- Schema previsto: `sales`
- Versión: 1.0.0
- Estado: Borrador detenido por decisiones de producto
- Fase: MVP · M03 Ventas Online

### Cómo se verificó

En este documento:

- OBSERVADO: abierto y leído directamente en el repositorio en el commit verificado.
- LEÍDO EN CÓDIGO: comprobado en código fuente del producto.
- AMBOS: coincide la documentación con el código observado.
- DEDUCIDO: conclusión derivada de hechos anteriores. Las deducciones están reunidas en una sección propia y no se presentan como decisiones tomadas.

# 1. Propósito

M03 permite convertir los artículos publicables de M01 en pedidos de clientes identificados por M04, conservar la fotografía comercial de lo comprado y gestionar el pago manual mediante Yape.

OBSERVADO: `ROADMAP_MODULAR.md` coloca M03 como Ventas Online y declara dependencias duras sobre M01 Catálogo y M04 Clientes.

OBSERVADO: `docs/modules/catalog/SPEC.md` establece que M03 vende contra `catalog.product_items`, no contra el producto agregado.

OBSERVADO: `docs/modules/crm/SPEC.md` establece que M04 es dueño de la identidad del cliente y que M03 debe conservar snapshots de los datos necesarios para el pedido.

OBSERVADO · decisión de producto recibida: el pago inicial de M03 es mediante Yape con confirmación manual.

Sin M03 puede existir catálogo, contenido y cuenta de cliente, pero no una venta online mediante pedido.

# 2. Valor comercial

M03 convierte SILLAR WEB de catálogo consultable en canal de recepción de pedidos.

No sustituye:

- el catálogo de M01;
- la identidad de cliente de M04;
- el inventario de M09;
- el punto de venta de M13;
- la emisión de comprobantes de M14;
- una futura pasarela de pago automático de M11.

OBSERVADO: la separación anterior coincide con `ROADMAP_MODULAR.md`, el SPEC de M01 y ADR-017.

# 3. Antes de empezar el módulo

## 3.1 §1 — ¿Esta regla es cierta porque el mundo es así, o porque hoy solo hay uno?

### Respuesta

M03 no puede convertir circunstancias de la primera instalación en reglas universales.

OBSERVADO: M01 establece que la unidad vendible es `catalog.product_items`.

Por tanto, una línea de carrito o pedido debe identificar el item, no asumir que un producto tiene una sola presentación.

OBSERVADO: ADR-017 establece que una instalación WEB está asociada a una ubicación para las existencias que despacha, pero también prevé M17 para varias ubicaciones.

Por tanto, M03 no debe grabar como regla universal que «existe una sola ubicación de stock». La primera instalación puede tener una, pero esa circunstancia no debe contaminar el contrato del pedido.

OBSERVADO: M04 es hoy el único módulo de identidad de cliente final.

Eso sí es una frontera de propiedad explícitamente decidida, no una casualidad: M03 consume la identidad de M04 y no crea una segunda ficha de cliente.

OBSERVADO: M01 puede tener `list_price` nulo y lo interpreta como «precio a consultar».

M03 no puede deducir por su cuenta si esos items se pueden comprar online. Esa decisión determina qué puede comprar el cliente y queda escalada al final.

## 3.2 §2 — ¿Alguna vez he visto a esta barrera decir que no?

### Respuesta

Cada barrera nueva de M03 tendrá al menos una prueba donde:

1. se provoca deliberadamente el rechazo;
2. se comprueba que rechaza por la causa correcta;
3. se prueba el caso válido equivalente;
4. cuando exista una autoprueba de la barrera, se introduce deliberadamente el defecto que debe detectar y se comprueba que la prueba se vuelve roja.

Las barreras que ya se conocen para M03 son:

### Cliente sin correo verificado

OBSERVADO: el SPEC de M04 dice expresamente: un cliente sin verificar puede entrar y mirar; comprar, no.

Prueba obligatoria: intentar crear un pedido desde una cuenta no verificada debe ser rechazado; la misma operación con la cuenta verificada debe poder continuar si las demás condiciones se cumplen.

### Item inexistente o inactivo

OBSERVADO: el contrato de M01 ya ofrece la identidad y estado del `ItemId`.

Prueba obligatoria: intentar convertir en pedido un item que dejó de estar disponible no puede producir una línea válida.

### Precio

OBSERVADO: M01 define el precio efectivo del item como `price_override ?? list_price`.

Prueba obligatoria: el importe enviado por el navegador nunca se acepta como autoridad. La operación que crea el pedido debe obtener el precio autorizado desde M01.

### Stock

OBSERVADO: M01 declara explícitamente que no es dueño de las existencias.

Bloqueada: no puede escribirse todavía la prueba final de rechazo por falta de stock porque en el commit verificado no está resuelto qué contrato provee y compromete esa existencia a M03. Véase BLOQUEO B-01.

### Reserva vencida

OBSERVADO · decisión cerrada: al vencer la reserva se libera el stock, el pedido no se cancela por ese hecho y se avisa al personal.

La barrera debe probar al menos:

- reserva dentro del plazo: no se libera;
- reserva vencida: sí se libera;
- después de liberarla: el pedido continúa existiendo y no queda cancelado únicamente por el vencimiento.

## 3.3 §3 — ¿La guarda está en la operación, o en quien la llama?

### Respuesta

La guarda va en la operación que compromete el pedido y la reserva, no en la pantalla, endpoint o carrito que la invoca.

Esta es una condición arquitectónica de M03.

OBSERVADO: `ANTES-DE-EMPEZAR-UN-MODULO.md` exige que una guarda que protege una operación destructiva o sensible viva en la propia operación.

OBSERVADO: M01 entrega el precio efectivo por contrato y M04 entrega identidad/verificación del cliente por contrato.

Por tanto, crear/confirmar un pedido debe volver a comprobar dentro de la operación:

- que el cliente autorizado sigue siendo válido para comprar;
- que el item sigue existiendo y es vendible;
- el precio efectivo actual que corresponda;
- la disponibilidad que corresponda;
- la creación o renovación del compromiso de stock que corresponda.

El carrito, el frontend o el endpoint pueden anticipar estas comprobaciones para dar una respuesta rápida, pero ninguno constituye la barrera.

No se acepta:

- total calculado por el navegador como fuente de verdad;
- precio enviado por el cliente como fuente de verdad;
- disponibilidad calculada únicamente al cargar la página;
- comprobar stock en el endpoint y asumir que continúa igual dentro de la operación.

### Punto que impide cerrar esta sección

La parte de precio tiene dueño: M01.

La parte de cliente tiene dueño: M04.

La parte de existencia no tiene un proveedor utilizable por M03 identificado en el alcance actual.

OBSERVADO: M01 dice expresamente que el stock pertenece a M09.

OBSERVADO: `ROADMAP_MODULAR.md` coloca M03 antes de M09 y solo declara como dependencias duras M01 y M04.

No se resuelve esta contradicción creando stock dentro de M03 ni pasándolo desde el llamador.

Véase BLOQUEO B-01.

## 3.4 §4 — ¿Lo he comprobado, o me lo ha dicho algo que responde sin fallar?

### Respuesta

Las fronteras críticas se contrastaron por más de una vía cuando existía una segunda vía disponible.

### Precio y unidad vendible

AMBOS: el SPEC de M01 declara que se vende contra `product_items` y su contrato público entrega un `ItemSnapshot` con `ItemId` y `Price` ya resuelto.

### Identidad del cliente

OBSERVADO: el SPEC de M04 declara que la cuenta del cliente vive en CRM y que su contrato para M03 debe proporcionar cliente, snapshot y verificación de correo.

### Módulo activo y desmontaje

LEÍDO EN CÓDIGO: el arranque monta endpoints solamente de los módulos activos; no existe una lista global de rutas de negocio que sobreviva independientemente del módulo.

### Auditoría visible

LEÍDO EN CÓDIGO: la plataforma compone vocabularios de auditoría aportados por cada módulo activo y no resuelve silenciosamente colisiones de `entityType`.

### Stock

OBSERVADO: M01 rechaza ser dueño del stock.

OBSERVADO: ADR-017 sitúa las existencias compartidas en M09.

No apareció una segunda fuente en el commit verificado que convierta a otro módulo actual en propietario de existencias. Por tanto, no se afirma que exista un contrato de reserva que no se ha encontrado.

# 4. Dependencias

| Módulo         | Tipo                  | Qué necesita M03                                                                  | Si falta                           |
| -------------- | --------------------- | --------------------------------------------------------------------------------- | ---------------------------------- |
| CORE           | Dura / plataforma     | capacidades, auditoría, configuración, infraestructura modular                    | SILLAR no funciona como plataforma |
| M01 Catálogo   | Dura                  | `ItemId`, snapshot comercial, precio efectivo y estado del item                   | M03 no puede vender                |
| M04 Clientes   | Dura                  | identidad de sesión de cliente, snapshot de cliente/dirección y correo verificado | M03 no puede aceptar pedidos       |
| M09 Inventario | PENDIENTE DE DECISIÓN | autoridad sobre existencias/reservas                                              | BLOQUEO B-01                       |

OBSERVADO: M01 y M04 son dependencias duras en `ROADMAP_MODULAR.md`.

OBSERVADO: M03 no puede empezar código mientras M04 siga abierto, por instrucción de este encargo.

## Dependencias de paquetes

OBSERVADO: el método de pago decidido es Yape con confirmación manual.

DEDUCIDO: el comportamiento especificado hasta aquí no requiere SDK de pasarela, cliente Yape ni biblioteca de pagos.

Decisión de SPEC: no se introduce dependencia nueva para el pago manual.

Si durante implementación alguien propone una dependencia nueva, se detiene esa incorporación y se aplica íntegramente §6 de `ANTES-DE-EMPEZAR-UN-MODULO.md` antes de aceptarla.

# 5. Modelo de datos — decisiones previas al primer `CREATE TABLE`

NO AUTORIZA CREAR TABLAS.

Esta sección responde §7 antes de que exista la primera migración.

## 5.1 Qué se replica

### Pedido

OBSERVADO: ADR-016 enumera «Ventas y sus líneas» entre las filas replicables.

Decisión de SPEC: las filas persistentes que representan el pedido y sus líneas usan:

- PK `uuid` v7 generada por aplicación;
- `origin_node`;
- `row_version`;
- FK replicables también en `uuid`.

### Carrito y sesión de compra

OBSERVADO: ADR-017 coloca «Carrito y sesiones de compra» en el lado exclusivo de WEB.

Decisión de SPEC: un carrito puramente transitorio de la tienda no se replica. Sus identificadores locales pueden conservar el patrón de tablas no replicadas.

### Reserva de stock

NO DECIDIDA.

No se clasifica hasta resolver qué módulo posee la operación de reserva y qué fila representa el compromiso real de existencias.

No se crea una tabla `sales.stock_reservations` por conveniencia antes de resolver esa propiedad.

## 5.2 Tablas que sí pueden declararse conceptualmente

### `sales.orders`

Representa el pedido como documento comercial persistente.

Debe conservar snapshots suficientes para que cambios posteriores de catálogo, cliente o dirección no reescriban lo que ocurrió.

OBSERVADO: esta necesidad de snapshots está declarada tanto en las decisiones previas de M03 como en los SPECS de M01 y M04.

Replicación: sí.

PK: `uuid` v7.

El conjunto exacto de estados y el número visible del pedido no se cierran aquí porque ambos afectan a lo que verá el cliente. Véanse B-03 y B-04.

### `sales.order_lines`

Una fila por `catalog.product_item` vendido.

Conserva al menos, conceptualmente:

- `ItemId` de origen;
- nombre del producto;
- variante;
- unidad de venta;
- cantidad;
- precio unitario congelado en la operación;
- subtotal derivable.

OBSERVADO: M01 exige snapshot porque el catálogo puede cambiar después.

Replicación: sí.

PK: `uuid` v7.

### `sales.carts`

Carrito de la tienda WEB.

OBSERVADO: ADR-017 clasifica carrito y sesiones de compra como datos propios de WEB.

Replicación: no.

No se decide todavía si el carrito sobrevive sesiones, cuánto tiempo permanece ni qué UX utiliza; esas decisiones no son necesarias para fijar su no replicación.

### `sales.cart_items`

Contenido local del carrito.

Replicación: no.

Puede referenciar un `catalog.product_items.id` replicado: la dirección local → replicado no contradice ADR-016.

El precio visible dentro del carrito no se declara precio contractual de la venta; la operación final vuelve a consultar la autoridad de M01.

## 5.3 Colaciones

No se introduce una colación no determinista «por costumbre».

Se decide columna por columna según el tipo de comparación real.

### Snapshots de nombre, dirección y descripción

Son evidencia histórica, no identificadores ni claves de búsqueda por defecto.

Decisión: texto ordinario mientras no exista un requisito concreto de igualdad/búsqueda que justifique otra colación.

### Códigos visibles

El formato del número visible del pedido aún no está decidido.

No se fija colación hasta que se decida el contrato visible.

### Regla obligatoria

Si una futura columna de M03 utiliza `core.es_ci` o `core.es_search` y necesita `LIKE`, `ILIKE` o regex:

- la expresión usa `COLLATE "C"`;
- el índice, si existe, usa exactamente la misma expresión.

OBSERVADO: esta regla está escrita en §7 de `ANTES-DE-EMPEZAR-UN-MODULO.md` y documentada por casos reales de Catálogo y CRM.

# 6. Contrato público de M03

La forma exacta de interfaces no se escribe como código en este paso.

M03 necesitará exponer al menos dos capacidades.

## 6.1 Historial de pedidos de cliente

M04 ya reserva en la ficha del cliente un espacio que M03 rellenará.

El contrato debe permitir consultar pedidos por `customer_id` sin que M04 lea tablas de `sales`.

OBSERVADO: el SPEC de M04 dice que el historial de pedidos será aportado por M03 mediante contrato y que la ausencia del módulo debe dejar un hueco explicado, no un error.

## 6.2 Consulta de pedido

Debe devolver una representación basada en snapshots históricos, no recomponer un pedido antiguo con los nombres o direcciones actuales.

## Eventos publicados

Semánticamente, M03 necesita poder anunciar como mínimo:

- pedido creado;
- pago Yape confirmado manualmente;
- reserva vencida y liberada.

Los nombres técnicos definitivos pertenecen a la implementación del contrato.

## Eventos consumidos

No se declara consumo de eventos de catálogo para modificar pedidos históricos.

OBSERVADO: los pedidos conservan snapshots precisamente para no reescribirse cuando cambie M01.

La integración con existencias queda pendiente de B-01.

# 7. Auditoría — §5 de «Antes de empezar»

M03 **escribe auditoría con certeza**.

La contribución visible del módulo entra por su vocabulario, igual que el resto de módulos.

## Entidades que necesitarán vocabulario

Como mínimo:

- `order` → **Pedido**
- la entidad que finalmente represente la reserva → **Reserva de stock**, solo después de cerrar B-01
- la entidad que represente la confirmación manual, si termina siendo una fila independiente → **Confirmación de pago**, solo si el modelo final la necesita

## Regla del resumen

La auditoría nombra **la fila concreta**, no solo su clase.

Correcto:

- «Creación del pedido {número visible}.»
- «Confirmación manual del pago Yape del pedido {número visible}.»
- «Vencimiento de la reserva del pedido {número visible}; stock liberado.»

Incorrecto:

- «Creación de un pedido.»
- «Actualización de pago.»
- «Modificación de reserva.»

El identificador técnico `uuid` no sustituye al nombre humano de la fila.

**OBSERVADO:** ADR-016 prohíbe presentar los UUID a usuarios.

**LEÍDO EN CÓDIGO:** el vocabulario visible de auditoría se compone desde contribuciones de módulos activos.

El formato del número visible permanece bloqueado por B-03.

---

# 8. Reglas de negocio cerradas

## R-01 · Medio de pago inicial

Pago por **Yape**, con **confirmación manual por personal**.

**OBSERVADO · decisión de producto recibida.**

No se integra una pasarela automática en M03.

---

## R-02 · Duración de la reserva

Una reserva dura **48 horas naturales por defecto**.

El valor es **configurable por instalación**.

**OBSERVADO:** `DECISIONES-PREVIAS-M03.md`.

---

## R-03 · Vencimiento de la reserva

Al vencer:

1. se libera el stock reservado;
2. el pedido **no** se cancela únicamente por ese vencimiento;
3. se avisa al personal.

**OBSERVADO:** decisión cerrada recibida y coherente con `DECISIONES-PREVIAS-M03.md`.

---

## R-04 · Pedido y reserva son estados distintos

El estado del pedido no se deduce automáticamente del estado de la reserva.

**OBSERVADO:** `DECISIONES-PREVIAS-M03.md`.

---

## R-05 · M03 no hereda la no-caducidad de M07

La frase «no existe caducidad por tiempo» corresponde a presupuestos de M07, no a pedidos de M03.

**OBSERVADO · decisión de producto recibida.**

---

## R-06 · La identidad del comprador pertenece a M04

M03 no crea una identidad alternativa ni usa `core.admin_users`.

**OBSERVADO:** `ROADMAP_MODULAR.md` y SPEC de M04.

---

## R-07 · Correo sin verificar no compra

Un cliente puede identificarse y consultar su perfil sin verificar correo, pero M03 no acepta la compra mientras el correo no esté verificado.

**OBSERVADO:** SPEC de M04.

---

## R-08 · El precio de la línea se congela

La línea del pedido conserva el precio aceptado en el momento de la operación.

Una modificación posterior del catálogo no reescribe pedidos existentes.

**OBSERVADO:** SPEC de M01 y decisiones previas de M03.

---

## R-09 · El cliente no fija el precio

Un total o precio recibido desde el navegador es dato no confiable.

La operación vuelve a obtener el precio autorizado desde M01.

**DEDUCIDO:** consecuencia necesaria de §3 y del contrato de precio de M01.

---

# 9. Pantallas y estados

Este apartado es entrada obligatoria del paso 3.5 de diseño.

Queda escrito hasta el nivel permitido por las decisiones actuales. Las partes que requieren una decisión de JP permanecen marcadas como bloqueadas.

## 9.1 Carrito

### Vacío

- explica que todavía no hay productos añadidos;
- ofrece volver al catálogo;
- no muestra una tabla vacía.

### Con datos

- items seleccionados;
- presentación/variante;
- cantidad;
- precio visible disponible desde M01;
- importe parcial correspondiente a las líneas.

BLOQUEADO: no se define todavía que ese parcial sea el «total a pagar» hasta resolver B-02.

### Cargando

- conserva la estructura estable;
- no inventa progreso;
- una espera corta no introduce indicador innecesario.

### Conflicto

Debe poder representar que, desde que se añadió un item:

- cambió el precio;
- el item dejó de ser vendible;
- la disponibilidad necesaria ya no está.

La acción final no puede continuar silenciosamente con información antigua.

## 9.2 Confirmación del pedido / checkout

ESTRUCTURA BLOQUEADA PARCIALMENTE.

Se sabe que debe:

- trabajar con cliente autenticado;
- impedir compra sin correo verificado;
- usar los datos de cliente/dirección entregados por M04;
- volver a validar precio y stock en la operación;
- conducir al flujo de pago Yape manual.

No se decide todavía:

- entrega frente a recojo;
- si existe costo de entrega;
- cómo se forma exactamente el total;
- comportamiento de un producto cuyo precio efectivo es nulo;
- presentación exacta del número visible del pedido.

### Vacío

No aplica como «checkout sin líneas»: un carrito sin items no debe entrar en una confirmación de pedido válida.

### Con datos

Bloqueado por B-02.

### Cargando

Representa la validación final/creación sin ocultar la información ya revisada.

### Conflicto

Debe tener tratamiento explícito para:

- precio cambiado;
- item ya no vendible;
- stock insuficiente;
- reserva que no pudo establecerse.

No sustituye esos casos por un error genérico.

## 9.3 Pedido creado / instrucciones de Yape

Debe existir una superficie posterior a la creación del pedido con:

- referencia humana del pedido;
- estado pendiente de confirmación manual;
- instrucciones de pago Yape que JP haya aprobado;
- vencimiento de la reserva cuando corresponda.

BLOQUEADO: el contenido exacto que ve el cliente después del vencimiento depende de B-04 y B-05.

### Vacío

No aplica: la ruta no debe existir sin un pedido identificable y autorizado.

### Con datos

Pedido e instrucciones.

### Cargando

Carga del pedido ya existente.

### Conflicto

Pedido inexistente, ajeno al cliente o estado modificado en otra sesión.

## 9.4 Mis pedidos

Se integra con la cuenta de M04 mediante contrato de M03.

### Vacío

«Todavía no tienes pedidos», sin hueco de tabla.

### Con datos

Lista de pedidos del cliente con referencia humana, fecha y estado visible.

### Cargando

Estado estable de carga.

### Conflicto

Si un pedido cambia de estado mientras la lista está abierta, una actualización posterior muestra el estado autoritativo; no se conserva una versión local editable.

## 9.5 Detalle de pedido del cliente

### Vacío

No existe como detalle vacío: 404/estado equivalente si el pedido no existe o no pertenece al cliente.

### Con datos

Snapshots históricos del pedido y sus líneas.

### Cargando

Conserva el armazón.

### Conflicto

Si cambia el estado administrativo mientras está abierto, la respuesta siguiente refleja el estado nuevo.

BLOQUEADO: el vocabulario completo de estados visibles al cliente requiere B-04.

## 9.6 Pedidos — panel administrativo

### Vacío

Explica que todavía no existen pedidos.

### Con datos

Listado de pedidos con estado operativo suficiente para localizar:

- pendientes de revisión;
- pagos Yape que requieran confirmación;
- pedidos cuya reserva venció y requieren atención.

### Cargando

Tabla/estructura estable sin progreso ficticio.

### Conflicto

La actualización de un pedido debe detectar que otra persona ya cambió el mismo estado y recargar la verdad antes de permitir otra transición.

## 9.7 Detalle de pedido — panel

Permite inspeccionar:

- cliente congelado;
- líneas congeladas;
- estado del pedido;
- estado de la reserva;
- información necesaria para confirmar manualmente Yape;
- avisos al personal.

### Vacío

Pedido inexistente.

### Con datos

Toda la información operativa disponible.

### Cargando

Armazón estable.

### Conflicto

Caso mínimo:

- otra persona confirmó el pago mientras la pantalla estaba abierta;
- la reserva venció mientras se intentaba actuar.

La operación no pisa silenciosamente el estado nuevo.

# 10. Endpoints — contrato funcional, no implementación

No se fijan DTO ni clases en esta SPEC detenida.

## Cliente

Se necesitarán operaciones para:

- consultar carrito;
- añadir/modificar/quitar líneas;
- validar la compra;
- crear el pedido;
- consultar pedidos propios;
- consultar detalle propio.

## Administración

Se necesitarán operaciones para:

- listar pedidos;
- consultar detalle;
- confirmar manualmente pago Yape;
- actuar sobre los estados administrativos que JP cierre;
- consultar/identificar pedidos con reserva vencida.

Todas las escrituras administrativas quedan auditadas.

NO CERRADO: las rutas exactas y las transiciones de estado se congelan después de responder B-01 a B-05.

# 11. Criterios de aceptación y prueba correspondiente

| Criterio                                                                                       | Prueba obligatoria                                                                                                                                                       |
| ---------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| M03 no inicia construcción mientras M04 siga abierto                                           | Verificación previa de estado de M04 antes del primer cambio de código de M03                                                                                            |
| M03 exige M01 y M04                                                                            | Intentar iniciar/activar M03 sin cada dependencia y observar rechazo explícito; repetir con ambas presentes y observar paso                                              |
| La operación de compra no confía en el precio enviado por cliente                              | Enviar un precio manipulado y comprobar que la línea usa el valor autoritativo de M01 o rechaza según la regla vigente                                                   |
| Se vende contra `ItemId`, no contra `ProductId`                                                | Producto con dos variantes: comprar una y comprobar que el snapshot conserva la variante exacta                                                                          |
| Cliente sin correo verificado no compra                                                        | Caso no verificado rechazado; mismo cliente verificado aceptado                                                                                                          |
| El pedido conserva snapshot                                                                    | Crear pedido, cambiar después nombre/precio/dirección fuente y comprobar que el pedido histórico no cambia                                                               |
| Reserva por defecto = 48 horas naturales                                                       | Crear reserva con configuración por defecto y comprobar vencimiento exacto                                                                                               |
| Reserva configurable por instalación                                                           | Cambiar configuración, crear una reserva nueva y comprobar que usa el nuevo plazo sin reescribir reservas anteriores                                                     |
| Vencer reserva libera stock                                                                    | Forzar reloj más allá del vencimiento y comprobar liberación contra la autoridad de stock que se decida en B-01                                                          |
| Vencer reserva no cancela pedido                                                               | Tras la misma expiración, comprobar explícitamente que el pedido sigue existiendo y no pasó a cancelado solo por ella                                                    |
| Vencimiento avisa al personal                                                                  | Provocar expiración y observar el mecanismo visible decidido para el panel                                                                                               |
| Confirmación Yape es manual                                                                    | Pedido pendiente no cambia a confirmado sin acción autorizada del personal                                                                                               |
| Confirmar Yape queda auditado                                                                  | Confirmar un pago y comprobar entrada con el pedido humano concreto, no solo «un pedido»                                                                                 |
| Auditoría nombra la fila                                                                       | Crear dos pedidos y comprobar que sus resúmenes permiten distinguirlos sin abrir el detalle                                                                              |
| `orders` y `order_lines` usan UUID v7                                                          | Crear filas y comprobar versión 7 de sus PK y presencia de `origin_node`/`row_version`                                                                                   |
| Carritos locales no cargan metadatos de replicación                                            | Inspección de schema + prueba contra columnas esperadas cuando DATOS exista                                                                                              |
| Ningún UUID se presenta al cliente o personal                                                  | E2E sobre pantallas y respuestas visibles                                                                                                                                |
| No entra dependencia nueva por Yape manual                                                     | Comparar manifiestos antes/después de M03; cualquier alta exige revisión §6                                                                                              |
| Pantallas cubren vacío, datos, carga y conflicto                                               | E2E/visual por cada pantalla del §9, claro/oscuro y móvil/escritorio                                                                                                     |
| Instalar y desinstalar M03 no deja enlace roto, ruta muerta, hueco visual ni error de arranque | E2E de instalación/desmontaje: con M03 activo existen navegación/rutas; después de desactivarlo/desinstalarlo desaparecen y el resto de la aplicación arranca y funciona |
| La ficha M04 funciona sin M03                                                                  | Desactivar M03 y comprobar que el hueco de pedidos de M04 queda explicado y no falla                                                                                     |
| Reactivar/reinstalar M03 no requiere datos fantasma                                            | Desinstalar, verificar resto íntegro, reinstalar y comprobar arranque limpio                                                                                             |
| Las barreras se han visto fallar y pasar                                                       | Para cada barrera de §3.2 mantener pareja negativo/positivo y falsificar al menos una vez la propia prueba de guarda                                                     |

El criterio de instalación/desinstalación anterior es innegociable, tomado de `ROADMAP_MODULAR.md`.

# 12. Fuera de alcance

- pasarela de pago automática — M11;
- comprobantes electrónicos — M14;
- POS y caja — M13;
- ser dueño de existencias — M09;
- varias sucursales y traslado entre ellas — M17;
- presupuesto B2B — M07;
- modificar identidad de cliente — M04;
- modificar catálogo — M01;
- decidir políticas de producto que JP no haya cerrado.

# 13. Deducciones separadas

Nada de esta sección se convierte automáticamente en decisión del producto.

## D-01 · Hay una contradicción de dependencia alrededor del stock

DEDUCIDO a partir de tres hechos observados:

1. M03 ya tiene una regla de reserva y liberación de stock.
2. M01 declara que no guarda existencias.
3. M09 es el dueño previsto de existencias, pero el roadmap coloca M03 antes de M09.

Por tanto, hoy falta una decisión de arquitectura: quién ofrece a M03 una operación autoritativa y atómica de disponibilidad/reserva en la primera versión.

No se corrige moviendo el stock a M01 ni creando una fuente paralela dentro de M03.

## D-02 · El precio de M01 no basta para definir «lo que paga»

M01 puede entregar el precio efectivo de una línea, pero eso no define necesariamente el importe final del pedido.

Falta confirmar si existen:

- costo de entrega;
- recojo sin costo;
- otra tarifa;
- compra online de items con precio «a consultar».

Como esto determina cuánto paga el cliente, no se decide aquí.

## D-03 · Confirmación manual después del vencimiento es un caso distinto

La regla conocida dice que al vencer se libera stock pero el pedido permanece.

Si el cliente paga por Yape después del vencimiento, podrían ocurrir varios comportamientos posibles y ninguno está autorizado todavía:

- volver a intentar reservar;
- dejarlo pagado pero pendiente de disponibilidad;
- requerir intervención humana antes de marcar pago;
- otra política.

Elegir una cambiaría directamente la experiencia de un cliente que ya entregó dinero.

Se escala.

## D-04 · El modelo necesita estados visibles, pero sus nombres/transiciones no están cerrados

«Pedido», «reserva» y «pago» son estados distintos.

La SPEC necesita saber qué estados ve la clientela y qué transiciones puede hacer el personal para terminar §9 y congelar los endpoints.

No se deducen de una enum técnica.

# 14. Fallos/hallazgos registrados fuera del encargo

No se corrige ninguno dentro de esta entrega.

1. Inconsistencia arquitectónica stock M03 ↔ M09: registrada como B-01.
2. La copia de `DECISIONES-PREVIAS-M03.md` abierta exactamente en `a7416ae` contiene las decisiones de reserva, pero el encargo aporta además como decisiones cerradas Yape/manual y aviso al personal. Se respetan como decisiones transmitidas por JP; no se reescribe el documento previo dentro de esta entrega.
3. `ROADMAP_MODULAR.md` todavía contiene referencias históricas que describen fases posteriores de identidad de cliente, mientras el SPEC actual de M04 ya sitúa esa identidad en M04. Para M03 se usa el contrato actual de M04, no esa descripción histórica.

# PREGUNTAS PARA JP — BLOQUEANTES

## B-01 · Autoridad de stock

PREGUNTA BLOQUEANTE: si M09 todavía no existe cuando se construya M03, ¿quién es la autoridad que dice cuánto stock puede reservar M03 y ejecuta de forma atómica la reserva/liberación?

No son opciones decididas, solo delimitación del problema:

- adelantar la capacidad mínima de M09;
- cambiar el orden del roadmap;
- otra frontera explícita decidida por JP/liderazgo técnico.

No se acepta como salida: meter existencias en M01 o confiar en una cantidad enviada por quien llama.

## B-02 · ¿Qué constituye exactamente el total que paga el cliente?

PREGUNTA BLOQUEANTE: además de la suma de precios de las líneas, ¿M03 contempla en esta versión costo de entrega u otro cargo?

Relacionado:

- ¿hay entrega, recojo o ambos?
- si hay entrega, ¿su costo forma parte de M03?
- ¿un `product_item` cuyo precio efectivo sea nulo —«consultar precio» en M01— puede entrar al carrito/pedido online o debe impedirse la compra?

Esta pregunta decide qué ve y qué paga el cliente, por lo que no se responde dentro de la SPEC.

## B-03 · Referencia visible del pedido

PREGUNTA: ¿qué formato debe ver una persona como número/código del pedido?

ADR-016 exige separar UUID técnico y código humano, pero no fija el formato de M03.

No se propone uno porque lo verá el cliente.

## B-04 · Estados visibles del pedido

PREGUNTA BLOQUEANTE: ¿qué estados del pedido ve el cliente en esta primera versión y cuáles maneja el personal?

Como mínimo hay hechos separados:

- pedido creado;
- pago Yape pendiente;
- pago confirmado;
- reserva vigente;
- reserva vencida.

Pero no se define aquí cómo se convierten en un vocabulario visible del pedido.

## B-05 · Yape recibido después de vencer la reserva

PREGUNTA BLOQUEANTE Y PRIORITARIA: si el cliente realiza el pago Yape cuando la reserva ya venció y el stock fue liberado, ¿qué debe hacer el sistema y qué se le comunica al cliente?

Esta decisión involucra dinero ya entregado y disponibilidad ya liberada. La SPEC no puede escogerla.

ESTADO FINAL DEL DOCUMENTO: `DETENIDO / REQUIERE RESPUESTA DE JP`.

Una vez respondidas B-01, B-02, B-04 y B-05 —y B-03 si el número visible entra en esta entrega— se puede completar contrato, estados, endpoints y modelo de reserva sin inventar política de producto.