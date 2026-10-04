# Decisiones previas — M11 Pagos

- **Módulo:** M11 Pagos
- **Código previsto:** `payments`
- **Producto:** SILLAR WEB
- **Fase:** 4
- **Estado:** Paso 1 · SPEC / decisiones / contratos / dependencias
- **Decisión JP-01:** 04/10/2026

Este documento conserva lo decidido antes de escribir producción y separa expresamente las
decisiones de producto de las que siguen elevadas al Líder Técnico.

No autoriza código, migraciones, frontend, navegación, SDK ni integración real con proveedor.

---

## 1. JP-01 — relación M11 / M09 — RESUELTA por JP el 04/10/2026

La condición histórica «M11 no antes que M09» **no sigue vigente**.

Decisión formal:

- M09 Inventario **no es dependencia dura** de M11.
- M09 Inventario **no es dependencia blanda** de M11.
- M09 Inventario **no es requisito previo de construcción** de M11 v1.
- M11 continúa dentro de SILLAR WEB.
- `inventory` no se añade a `HardDependencies` ni a `SoftDependencies`.
- Si en el futuro aparece un caso real que requiera inventario, se especificará como una
  integración nueva.

Esta resolución no cambia que M09 pertenezca al ERP ni adelanta ninguna decisión sobre proveedor o
pasarela.

---

## 2. Decisiones de Paso 1 que permanecen cerradas

### D-M11-01
M11 permanece en SILLAR WEB, Fase 4, con código/schema conceptual `payments`.

### D-M11-02
M03 Ventas es dependencia dura de M11. M11 no se convierte en dueño del pedido ni escribe
directamente el schema `sales`.

### D-M11-03
Yape y efectivo continúan como cobros manuales de M03. La tarjeta llega con M11.

### D-M11-04
El importe autoritativo del pedido pertenece a M03. M11 no recalcula el carrito ni confía en un
importe enviado por el navegador.

### D-M11-05
El hecho de pago y el estado del pedido siguen separados. Los estados técnicos del proveedor no se
convierten en estados de pedido.

### D-M11-06
Un redirect o pantalla de éxito del navegador no confirma dinero. La confirmación comercial exige
una señal verificable del lado servidor.

### D-M11-07
Reintentos y webhooks deben ser idempotentes: una misma liquidación externa no puede producir dos
pagos comerciales.

### D-M11-08
SILLAR no almacena PAN ni CVV. Un proveedor que exigiera su manejo directo requeriría una decisión
específica de seguridad.

### D-M11-09
La integración del proveedor queda detrás de una frontera interna provider-neutral. No entra SDK
antes de elegir proveedor y revisar esa dependencia.

### D-M11-10
M11 no reintroduce reservas ni autoridad sobre existencias. M09 no condiciona M11 v1.

### D-M11-11
Paso 1 no toca navegación. `NAV_READY M11` solo se emite cuando el módulo llegue realmente a esa
costura compartida.

---

## 3. Decisiones de arquitectura — PENDIENTES DEL LÍDER TÉCNICO

No se cierran por inferencia:

- **A-M11-01:** autenticación y dependencia M04.
- **A-M11-02:** contrato M03 para liquidación externa sin atribución humana falsa.
- **A-M11-03:** evolución de `PagoConfirmado`.
- **A-M11-04:** atomicidad, inbox, idempotencia y reintentos entre M11 y M03.
- **A-M11-05:** clasificación de replicación antes de diseñar tablas.
- **A-M11-06:** secretos, autenticación de webhook y retención de payloads.

---

## 4. Preguntas comerciales que continúan abiertas

JP-01 no responde por arrastre:

- qué medios online incluye v1 además de tarjeta, si alguno;
- pago después del vencimiento;
- pagos parciales o múltiples;
- reembolsos y contracargos;
- tratamiento de la comisión del proveedor;
- moneda operativa y su autoridad.

No se convierten en defaults técnicos.

---

## 5. Dependencias de Paso 1

| Dependencia | Estado |
|---|---|
| CORE | Dura / plataforma |
| M03 Ventas | Dura |
| M04 Clientes | Pendiente A-M11-01 del Líder Técnico |
| M09 Inventario | **Ninguna. Resuelto por JP el 04/10/2026** |
| M01 Catálogo | Ninguna directa definida |
| M10 Reportes | Consumidor futuro; no dependencia de M11 |
| M13 POS | Ninguna |
| M14 Comprobantes | Ninguna |

Un orden de roadmap y una dependencia runtime son conceptos distintos. Para M09 JP resolvió ambos:
no hay requisito previo de construcción y tampoco dependencia dura o blanda.

---

## 6. HOLD parcial

El trabajo documental independiente puede continuar.

El HOLD aplica únicamente a las decisiones A-M11-01 a A-M11-06 y a las preguntas comerciales que
siguen abiertas.
