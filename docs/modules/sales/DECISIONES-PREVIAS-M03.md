# Decisiones previas de M03 Ventas Online

Decisiones de producto que deben quedar escritas **antes de especificar M03**.

Este documento no sustituye al SPEC. Registra qué se decidió, qué se descartó y qué dato sigue
abierto para que el SPEC no tenga que reconstruir decisiones desde una conversación.

---

## 1 · CERRADA — liberar la reserva de stock no cancela el pedido

**Decisión de JP, 21 de septiembre de 2026:** cuando una reserva de stock llegue a su límite,
**se libera el stock reservado y el pedido no se cancela por ese solo hecho**.

Son dos estados distintos:

- la **reserva** responde a cuánto tiempo una existencia puede permanecer apartada;
- el **pedido** registra una intención/operación comercial y no desaparece ni pasa a cancelado
  únicamente porque haya vencido esa reserva.

Por tanto, el vencimiento de la reserva no equivale a una cancelación automática del pedido.
Cuando el flujo vuelva a necesitar existencias, tendrá que comprobar de nuevo la disponibilidad
que corresponda en ese momento.

**Lo que se descarta:** usar el vencimiento de la reserva como causa automática de cancelación.
Cancelar un pedido y devolver una existencia reservada son operaciones diferentes y deben tener
causas diferentes.

Esta decisión **no mueve el stock a M01**. M01 sigue describiendo qué se vende y
`catalog.product_items` sigue siendo la identidad de la variante; la cantidad disponible
pertenece a M09 Inventario, conforme al SPEC de M01 y a la arquitectura modular.

---

## 2 · ABIERTA — plazo de la reserva

Falta una decisión de JP:

> **¿Cuánto tiempo permanece reservado el stock antes de liberarse?**

El valor exacto está **PENDIENTE_DEFINIR**. No se deduce ni se elige por conveniencia técnica.

La decisión de §1 no depende de ese número: sea cual sea el plazo, al vencer se libera la
reserva **sin cancelar automáticamente el pedido**.

**Disparador:** este plazo debe quedar cerrado **antes de congelar el SPEC de M03 y antes de
implementar cualquier temporizador o expiración de reserva**.

---

## 3 · Dependencias ya cerradas de M03

M03 depende de:

- **M01 Catálogo — dura:** un pedido vende `catalog.product_items`.
- **M04 Clientes — dura:** comprar exige cuenta; la identidad de la clientela vive en M04.

El pedido conserva snapshots de los datos que deben describir lo ocurrido en el momento de la
compra; una modificación posterior del catálogo o del cliente no reescribe el historial.

---

```
DECIDÍ        Al vencer una reserva, liberar el stock sin cancelar automáticamente el pedido
DESCARTÉ      Convertir el vencimiento de la reserva en cancelación automática
POR QUÉ       Reserva y pedido representan estados distintos; liberar capacidad no equivale a cancelar una operación comercial
REVERSIBLE    Sí antes de implementar M03; después pasa a ser una regla de negocio observable

ABIERTO       Plazo exacto de la reserva de stock
NO INVENTAR   Debe decidirlo JP
DISPARADOR    Antes de congelar el SPEC de M03 o implementar la expiración
```
