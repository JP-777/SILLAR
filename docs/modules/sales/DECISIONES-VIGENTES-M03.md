# Decisiones vigentes de M03 Ventas Online

**Creación:** 26 de septiembre de 2026 — America/Lima
**Última verificación:** 26 de septiembre de 2026 — America/Lima
**Commit verificado:** `711bfba7cf3be80baa146b44e79ddf7a633d695d`

**Procedencia:** encargo de producto del **26 de septiembre de 2026**, redactado por el colíder y
entregado por JP. Es la autoridad de producto de M03 y **prevalece sobre toda mención histórica**
a «reserva de 48 h» y a los valores anteriores.

**Lo que este documento no es.** No es el SPEC de M03, y no lo sustituye. El SPEC de M03 **no
existe todavía** y está bloqueado: la SPEC detenida que redactó el chat de Diseño no está en
`origin/main`, no está en ninguna rama del repositorio y **no llegó con este encargo**. Ver
`ESCALADAS-M03.md` §a.

**Discrepancia con el repositorio, registrada aquí a propósito.** Estas decisiones todavía no
están escritas en los documentos compartidos: `docs/PENDIENTES.md:487-488` sigue diciendo que
«la tienda abre cobrando con **Yape y efectivo**», y `docs/ARQUITECTURA_MODULAR.md` sigue
describiendo `sales.order_statuses` como tabla y `order_items.product_id` apuntando a
`catalog.products`. Esos documentos son de Integración: **no se editan desde aquí**, se piden.
La discrepancia completa, con cita de archivo y línea, está en `MATRIZ-DIFERENCIAS-M03.md`.

---

## 1 · Sin reserva de existencias en SILLAR WEB v1

**M09 Inventario pasó al ERP** (`docs/ROADMAP_MODULAR.md:122`). Sin inventario no hay existencia
que apartar.

Las **48 horas naturales, configurables por instalación**, son **plazo para pagar**, no stock
apartado. Cuando vence:

- el pedido pasa a **Vencido**;
- **deja de estar garantizado**;
- **no se cancela por ese hecho**;
- se **avisa al personal**.

**No se promete existencia garantizada en ninguna pantalla, estado ni notificación.**

## 2 · Yape con comprobación manual por una persona

**El hecho de pago y el estado del pedido son dos cosas distintas.** No se marca confirmado un
pago por la pantalla de agradecimiento ni por una declaración del cliente: lo confirma una
persona que lo comprueba.

> **Efectivo: pregunta de compatibilidad abierta.** `docs/PENDIENTES.md:487-488` registra que
> «la tienda abre cobrando con **Yape y efectivo**». El encargo del 26/09 nombra solo Yape y **no
> resuelve expresamente** el efectivo. Se conserva como pregunta a escalar —`ESCALADAS-M03.md`
> §c—: **no se autoriza ni se elimina en silencio una modalidad de cobro.**

## 3 · Solo recojo en tienda

No hay entregas a domicilio ni tarifas de envío en v1.

> **Consecuencia sobre un contrato existente.** `ICustomerSnapshotReader.GetForOrderAsync` exige
> un `customerAddressId` y devuelve una dirección no nulable
> (`backend/Sillar.Modules.Crm.Contracts/ICustomerSnapshotReader.cs:12-15` y `:27`). Un pedido
> de recojo en tienda no tiene dirección que congelar. Ver `ESCALADAS-M03.md` §e.

## 4 · Productos «a consultar»: fuera del carrito y del pago en línea

`list_price` nulo significa «consultar precio» (`docs/modules/catalog/SPEC.md:192` y `:206`), y
llega a M03 como `ItemSnapshot.Price == null`
(`backend/Sillar.Modules.Catalog.Contracts/ItemSnapshot.cs:26-28`).

- **Nulo es «a consultar». Cero es gratis, y cero sí se vende.** Esa confusión ya mordió una vez
  en la tarjeta pública (`backend/Sillar.Modules.Catalog.Contracts/ProductPickerItem.cs`, nota de
  `Price`).
- La acción **conduce a pedir información, que pertenece a M07**. M03 no implementa el módulo
  vecino.
- El contrato y las SPEC de M03 y M07 deben expresar **exactamente la misma frontera**.

## 5 · Código visible de pedido: año y correlativo

**Ejemplo obligatorio: `2026-0147`.** Nunca se muestra un identificador interno. La generación
debe **prevenir duplicados y concurrencia**.

> **Conflicto con la ADR-016, y es el caro de deshacer.** `ADR-016:66` y `CLAUDE.md` exigen que
> los códigos visibles **lleven la serie de su nodo delante** (`V-03-000459`). `2026-0147` no la
> lleva. Un código que ya se dictó por teléfono no se reformatea. **Se escala antes de escribir
> la primera migración** — `ESCALADAS-M03.md` §b1.

## 6 · Siete estados visibles, idénticos para cliente y personal

| Secuencia principal | | | | |
|---|---|---|---|---|
| Pendiente de pago | Pago por verificar | Preparando | Listo para recoger | Entregado |

Más **Vencido** y **Cancelado**. Siete en total.

- **Vencido no equivale a Cancelado.**
- **No se inventan transiciones comerciales que JP no haya aprobado.** Las que el encargo no fija
  están en `ESCALADAS-M03.md` §b.

## 7 · Recordatorio a las 24 horas — condicionado

**Si el envío de correo no está probado en producción, no se improvisa un mecanismo alternativo
ni se afirma entrega.** Queda como pendiente con disparador propio: **cuando el envío de correo
esté comprobado en producción.**

Hoy no lo está: el correo verificado de M04 se acreditó con **Mailpit, «solo para
desarrollo/pruebas»** (`docs/ROADMAP_MODULAR.md:67`). Por tanto
el recordatorio **no entra en v1**.

## 8 · Pago tardío

El personal **siempre** puede registrar un pago, aunque el pedido esté **Vencido**.

- **Si hay mercancía**, el pedido se reactiva según el flujo validado.
- **Si no la hay**, queda un **aviso operativo visible** para que alguien llame al cliente.
- **Nunca se acepta un pago sin resultado operativo o pendiente visible.**
- **No se implementa automáticamente saldo a favor ni devoluciones:** son deuda con disparador
  propio.

> «El flujo validado» no está escrito en ninguna parte del repositorio, y **a qué estado vuelve**
> un pedido reactivado no se deduce de nada cerrado. Escalado — `ESCALADAS-M03.md` §b2.

## 9 · Regla transversal

**Ningún texto, indicador ni estado promete mercancía garantizada sin inventario.** Las fechas de
pago no constituyen una reserva.

---

## Pendientes relacionados — son disparadores, no funciones a implementar ahora

| Pendiente | Disparador |
|---|---|
| **Saldo a favor** formal | Cuando el manejo manual deje de bastar |
| **Reserva de existencias** | Cuando el ERP tenga inventario |
| **Recordatorio de 24 h** | Cuando el envío de correo esté comprobado en producción |

Estos tres pertenecen a `docs/PENDIENTES.md`, que es de Integración. **No se editan desde aquí:**
se piden. Quedan escritos en este documento para que no se pierdan mientras tanto.

---

## Lo que esto desplaza

`DECISIONES-PREVIAS-M03.md` lleva la enmienda visible en su cabecera, con el texto histórico
conservado sin tocar. Sobrevive su §3 —dependencias duras de M01 y M04, y snapshots en el
pedido— y su criterio de que **vencer no es cancelar**, que este encargo repite sobre otro sujeto.
