# Decisiones previas de M03 Ventas Online

**Creación:** 21 de septiembre de 2026, 12:12:36 -05:00 — America/Lima
**Última verificación:** 21 de septiembre de 2026, 19:47:14 -05:00 — America/Lima
**Commit verificado:** `21b3897003aee4d9dde1b346be64dc2ca03ada70`
**Enmendado:** 26 de septiembre de 2026 — America/Lima, sobre `711bfba7cf3be80baa146b44e79ddf7a633d695d`
**Ampliado:** 27 de septiembre de 2026 — America/Lima, sobre `74ab0773a8ea1101b30a9888d132d79b88b7d8f4`

> ## ENMENDADO el 26 de septiembre de 2026 — no leer §1 y §2 como vigentes
>
> **Qué lo enmienda:** el encargo de producto del 26 de septiembre de 2026, recibido por el
> colíder a través de JP. Su texto vigente está en
> `docs/modules/sales/DECISIONES-VIGENTES-M03.md`.
>
> **Motivo:** M09 Inventario salió de la Fase 1 y pasó a SILLAR ERP
> (`docs/ROADMAP_MODULAR.md:122`). **Sin inventario no hay existencia que apartar**, así que
> SILLAR WEB v1 no tiene reserva de existencias. Las §1 y §2 de abajo hablan de una reserva de
> stock que el producto ya no tiene.
>
> **Qué queda desplazado, y qué lo sustituye:**
>
> | Este documento decía | Vigente desde el 26/09/2026 |
> |---|---|
> | §1 — al vencer la **reserva de stock** se libera el stock y el pedido no se cancela | No hay reserva. Al vencer el **plazo para pagar**, el pedido pasa a **Vencido**, deja de estar garantizado, **no se cancela por ese hecho** y se avisa al personal |
> | §2 — **48 horas** naturales configurables = plazo de la **reserva de stock** | **48 horas** naturales configurables = **plazo para pagar**. El mismo número; otro objeto. **No es una reserva** |
> | §1 — los dos estados distintos son **reserva** y **pedido** | Los dos estados distintos son **hecho de pago** y **estado del pedido** |
>
> **Lo que sobrevive sin cambio:** la §3 —dependencias duras de M01 y M04, y el pedido conserva
> snapshots—, y el criterio de §1 de que **vencer no es cancelar**, que el encargo del 26/09
> repite palabra por palabra sobre otro sujeto.
>
> **El texto de abajo no se ha tocado.** Se conserva porque registra con qué criterio se decidió
> el 21 de septiembre, y ese criterio sigue explicando por qué hoy vencer no cancela.

> ## AMPLIADO el 27 de septiembre de 2026 — el código visible del pedido
>
> **Decisión de JP, 27 de septiembre de 2026, America/Lima.** Comunicada por Chat 2 vía JP.
>
> ### El código visible de pedido de M03 es **`W-2026-0147`**
>
> **Composición:** **nodo delante, año y correlativo.**
>
> **Motivo, en palabras de la decisión:** alinear el formato visible con la **regla 2 de la
> ADR-016**, que separa las PK internas de los códigos legibles y establece **una serie visible por
> nodo**.
>
> **Qué desplaza.** Nada de este documento: el código visible no aparecía en él. Desplaza al
> **ejemplo obligatorio `2026-0147`** del encargo del 26/09 §5, que **deja de ser el formato
> autorizado**. Queda registrado aquí porque es donde se guarda la traza de las decisiones de
> producto de M03, y porque este documento ya lleva la enmienda del 26/09: las dos se leen juntas.
>
> **Trazabilidad de la decisión:**
>
> | Fecha | Qué se dijo | Estado |
> |---|---|---|
> | 26/09/2026 | Encargo §5: «código visible de pedido con año y correlativo: **ejemplo obligatorio `2026-0147`**» | **Desplazado** el 27/09 |
> | 26/09/2026 | Frente A escala el conflicto con la regla 2 de la ADR-016 y ofrece tres opciones, la segunda «`2026-0147` **con serie delante**, p. ej. `W-2026-0147`» (`ESCALADAS-M03.md` §b1) | Escalado, no decidido |
> | **27/09/2026** | **JP ratifica `W-2026-0147`** | **VIGENTE** |
>
> **Lo que esta decisión NO cambia:** la clave primaria sigue siendo **`uuid` v7** generada por la
> aplicación. **El identificador visible es una columna independiente**, y esa separación es
> precisamente lo que la regla 2 existe para sostener.
>
> **Lo que queda pendiente de concretar**, y no se presupone: la **continuidad** del correlativo, su
> **concurrencia** y el **cambio de año**. Están en `SPEC.md` §5.4 y `ESCALADAS-M03.md` §b1-bis.

---

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

## 2 · CERRADA — plazo de la reserva

**Valor:** **48 horas naturales por defecto, configurable por instalación**.

**Procedencia:** valor fijado por el **colíder por delegación expresa de JP el 21 de septiembre
de 2026**.

**Criterio:** el plazo debe cubrir la confirmación manual de Yape a lo largo de un fin de semana
sin inmovilizar stock durante varios días en temporada escolar.

La decisión de §1 se mantiene: al vencer el plazo se libera la reserva **sin cancelar
automáticamente el pedido**.

**Pendiente para el SPEC de M03:** cada instalación debe poder ajustar este valor. Este documento
fija la regla de producto; no define todavía el mecanismo técnico de configuración ni implementa
la expiración.

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

DECIDÍ        Reserva de stock de 48 horas naturales por defecto, configurable por instalación
PROCEDENCIA   Colíder, por delegación expresa de JP el 21 de septiembre de 2026
POR QUÉ       Cubre la confirmación manual de Yape durante un fin de semana sin inmovilizar stock durante días en temporada escolar
SPEC M03      Cada instalación debe poder ajustar el valor
```
