# SPEC — M11 Pagos

- **Código:** `payments`
- **Schema conceptual:** `payments`
- **Versión prevista:** 1.0.0
- **Estado:** Borrador · Paso 1
- **Fase:** 4
- **Producto:** SILLAR WEB

> Este borrador cubre únicamente decisiones, contratos y dependencias. No autoriza producción,
> migraciones, frontend, navegación ni integración real con proveedor.

---

## 1. Propósito

M11 añade pago en línea a pedidos que ya pertenecen a M03 Ventas.

M11 encapsula la interacción técnica con una futura pasarela, sus intentos, confirmaciones externas,
autenticidad, idempotencia y recuperación ante reintentos o fallos.

No se convierte en dueño del pedido, catálogo, cliente, inventario, comprobante fiscal ni de los
medios manuales resueltos por M03.

---

## 2. Fronteras

| Responsabilidad | Dueño |
|---|---|
| Pedido, líneas y total comercial | M03 |
| Yape manual | M03 |
| Efectivo manual | M03 |
| Hecho comercial de pago confirmado | M03 |
| Intento e interacción de pago online | M11 |
| Integración con proveedor | M11 |
| Estado técnico del intento externo | M11 |
| Estado del pedido | M03 |
| Inventario y reservas | **No M11; M09 no condiciona M11 v1** |
| Comprobantes electrónicos | M14 |

---

## 3. Dependencias

| Módulo | Tipo | Estado |
|---|---|---|
| CORE | Dura / plataforma | Cerrada |
| M03 Ventas | Dura | Cerrada |
| M04 Clientes | Pendiente | A-M11-01 |
| M09 Inventario | **Ninguna** | Resuelta por JP el 04/10/2026 |
| M01 Catálogo | Ninguna directa definida | — |
| M10 Reportes | Ninguna hacia M10 | Consumidor futuro |
| M13 POS | Ninguna | ERP |
| M14 Comprobantes | Ninguna | ERP |

### Resolución JP-01

M11 **no espera a M09**.

M09 no es dependencia dura, no es dependencia blanda y no es requisito previo de construcción de
M11 v1. No se añade `inventory` a las dependencias de M11.

Un caso futuro que realmente necesite inventario se especificará como integración nueva.

---

## 4. Invariantes de Paso 1

1. El importe autoritativo se obtiene de M03, nunca del navegador.
2. Un redirect exitoso no constituye confirmación de pago.
3. La confirmación comercial requiere una señal verificable del lado servidor.
4. Una misma liquidación externa no puede producir dos pagos comerciales.
5. M11 no inventa un usuario administrativo para atribuir un pago automático.
6. Los estados del proveedor no forman parte de la máquina de estados del pedido.
7. M11 no escribe directamente en tablas de M03.
8. PAN y CVV no forman parte del modelo persistente de SILLAR.
9. Con M11 desactivado, M03 conserva Yape y efectivo.
10. El proveedor concreto y su SDK quedan fuera del Paso 1.

---

## 5. Contratos requeridos

Conceptualmente M11 necesita una frontera pública con M03 para:

- obtener una representación pagable del pedido con importe autoritativo;
- registrar idempotentemente una liquidación externa ya confirmada.

Los nombres de interfaces, DTO y campos definitivos siguen pendientes del Líder Técnico,
especialmente A-M11-02, A-M11-03 y A-M11-04.

M11 no accede al `DbContext` ni al dominio interno de M03.

---

## 6. Eventos

Debe existir un único concepto canónico de pago comercial confirmado.

La forma concreta de evolucionar el actual `PagoConfirmado` queda pendiente en A-M11-03. No se
cierra por inferencia.

El bus interno actual tampoco se declara mecanismo durable de entrega financiera; esa decisión
permanece en A-M11-04.

---

## 7. Datos

**No diseñados todavía.**

Antes de la primera migración deben resolverse como mínimo:

- A-M11-02;
- A-M11-04;
- A-M11-05;
- la parte persistente de A-M11-06.

No se fijan tablas, PK, índices ni clasificación de replicación antes de ese dictamen.

---

## 8. Arquitectura pendiente

- A-M11-01 autenticación / dependencia M04;
- A-M11-02 liquidación externa sin atribución humana falsa;
- A-M11-03 evolución de `PagoConfirmado`;
- A-M11-04 atomicidad, inbox, idempotencia y reintentos;
- A-M11-05 replicación;
- A-M11-06 secretos, webhook y retención.

Ninguna queda cerrada por este documento.

---

## 9. Preguntas de producto abiertas

- medios online de v1 además de tarjeta, si alguno;
- pago después del vencimiento;
- pagos parciales o múltiples;
- reembolsos y contracargos;
- comisión del proveedor;
- moneda operativa y su autoridad.

No se asume un valor por defecto.

---

## 10. Fuera de alcance de Paso 1

Código, migraciones, frontend, navegación, SDK, proveedor concreto, credenciales reales, webhook
real, POS, inventario, reservas y comprobantes electrónicos.

`NAV_READY M11` **no se emite todavía**.
