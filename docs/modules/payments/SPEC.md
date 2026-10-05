# SPEC — M11 Pagos

- **Código:** `payments`
- **Schema conceptual:** `payments`
- **Versión prevista:** 1.0.0
- **Estado:** Borrador · Paso 1
- **Fase:** 4
- **Producto:** SILLAR WEB
- **Creación:** 04/10/2026, 17:24:29 -05:00 — America/Lima
- **Última modificación:** 04/10/2026 — America/Lima
- **Última verificación:** 04/10/2026 — America/Lima
- **Commit de código verificado:** `35647181891a9b78a7399d3b108d9a4415a0d48a`

> Este documento cubre decisiones, contratos, dependencias y criterios previos.
> No autoriza producción, migraciones, frontend, navegación, SDK ni integración real con proveedor.
> **La primera migración no está autorizada.**

---

## 1. Propósito

M11 añade pago en línea a pedidos que ya pertenecen a M03 Ventas.

M11 encapsula la interacción técnica con una futura pasarela, sus intentos, confirmaciones externas,
autenticidad, inbox, idempotencia y recuperación ante reintentos o fallos.

No se convierte en dueño del pedido, catálogo, cliente, inventario, comprobante fiscal ni de los
medios manuales resueltos por M03.

---

## 2. Fronteras

| Responsabilidad | Dueño |
|---|---|
| Pedido, líneas y total comercial | M03 |
| Yape manual | M03 |
| Efectivo manual | M03 |
| Ledger y hecho comercial de pago confirmado | **M03** |
| Intento e interacción de pago online | M11 |
| Inbox y reintentos del proveedor | M11 |
| Verificación de firma del webhook | M11 |
| Estado técnico del proveedor | M11 |
| Estado del pedido | M03 |
| Inventario y reservas | No M11; M09 no condiciona M11 v1 |
| Comprobantes electrónicos | M14 |

M11 **nunca escribe `sales.*`**.

---

## 3. Dependencias

| Módulo | Tipo | Estado |
|---|---|---|
| CORE | Dura / plataforma | Cerrada |
| M03 Ventas | Dura | Cerrada |
| M04 Clientes | **Dura cuando M11 use autenticación de cliente** | A-M11-01 ratificada |
| M09 Inventario | **Ninguna** | JP-01 resuelta |
| M01 Catálogo | Ninguna directa definida | — |
| M10 Reportes | Ninguna hacia M10 | Consumidor futuro |
| M13 POS | Ninguna | ERP |
| M14 Comprobantes | Ninguna | ERP |

No se admite dependencia transitiva oculta.

### Resolución JP-01

M11 **no espera a M09**. M09 no es dependencia dura, blanda ni requisito previo de construcción.

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
11. Los secretos de proveedor/webhook no viven en PostgreSQL.
12. El cuerpo crudo del webhook no se conserva.

---

## 5. Contrato M03 ↔ M11

M03 conserva el **ledger canónico**.

Conceptualmente M11 necesita una frontera pública con M03 para:

- obtener una representación pagable del pedido con importe autoritativo;
- aplicar idempotentemente una liquidación externa ya confirmada.

`OrderPayment` debe distinguir:

- **origen humano:** trío completo de atribución;
- **origen externo:** cero atribución humana + identidad externa/idempotente.

M11 nunca accede al `DbContext` o dominio interno de M03 y nunca escribe `sales.*`.

---

## 6. Pago confirmado canónico

Existe **un solo concepto canónico de pago confirmado**.

Si `PagoConfirmado` necesita enriquecerse para representar correctamente pagos automáticos, se
prefiere **evolución/versionado explícito** antes que cambiar silenciosamente su semántica.

No se crean dos universos permanentes de pago manual y pago online.

El bus interno puede notificar consecuencias, pero no constituye la memoria durable ni la garantía
de aplicación de un cobro externo.

---

## 7. Inbox, atomicidad e idempotencia

M11 usa **inbox durable** para recepciones externas.

La aplicación hacia M03 es **reintentable e idempotente**.

Las dos unicidades son partes de **UN MISMO mecanismo**:

| Barrera | Unicidad |
|---|---|
| M11 | `(provider, provider_event_id)` |
| M03 | `external_settlement_id` |

### Pruebas obligatorias antes de primera migración

1. repetir el mismo `(provider, provider_event_id)` → una sola recepción/aplicación;
2. dos eventos distintos que resuelvan al mismo `external_settlement_id` → un solo pago M03.

Además:

1. webhook válido;
2. persistir recepción;
3. aplicar a M03;
4. **simular caída antes de marcar inbox como aplicado**;
5. reentregar/reintentar;
6. M03 deduplica;
7. comprobar que el pago/saldo del pedido **NO se mueve dos veces**;
8. finalmente marcar inbox como aplicado.

**Sin esta prueba la idempotencia no queda acreditada.**

---

## 8. Replicación

La operación técnica de M11 es **local / no replicada**:

- intentos;
- inbox;
- reintentos;
- estado del proveedor.

El pago comercial canónico de M03 es **replicado**.

La clasificación está cerrada, pero no se fijan todavía tablas, PK, columnas ni índices.

---

## 9. Secretos, webhook y evidencia

Los secretos de proveedor y webhook viven **fuera de PostgreSQL**.

Al recibir un webhook:

1. se verifica la firma;
2. se registra el resultado de verificación;
3. se conserva el **SHA-256 del cuerpo recibido**;
4. se conservan metadatos normalizados suficientes;
5. el **cuerpo crudo NO se conserva**.

### Consecuencia explícita

SILLAR **no podrá reverificar posteriormente la firma exacta del webhook**, porque el body original
ya no estará disponible.

En una disputa, la evidencia disponible será:

- SHA-256 del cuerpo;
- identificadores y metadatos normalizados;
- resultado de verificación registrado al recibir;
- resultado de procesamiento/aplicación registrado.

La pérdida de capacidad de reverificación posterior es una decisión conocida y aceptada, no una
omisión accidental.

---

## 10. Criterios previos a primera migración

Antes de pedir autorización para la primera migración deben poder acreditarse:

- dependencia M04 explícita cuando se use autenticación de cliente;
- ninguna escritura M11 sobre `sales.*`;
- origen humano y externo sin atribución falsa;
- evolución/versionado explícito de `PagoConfirmado` si requiere enriquecimiento;
- inbox durable;
- ambas unicidades;
- prueba de caída entre aplicar a M03 y marcar inbox;
- operación M11 local/no replicada;
- pago comercial M03 replicado;
- secretos fuera de PostgreSQL;
- firma verificada al recibir;
- SHA-256 y metadatos normalizados persistidos;
- body crudo ausente.

Esto **no autoriza la migración**.

---

## 11. Preguntas de producto abiertas

- medios online de v1 además de tarjeta, si alguno;
- pago después del vencimiento;
- pagos parciales o múltiples;
- reembolsos y contracargos;
- comisión del proveedor;
- moneda operativa y su autoridad.

No se asume un valor por defecto.

---

## 12. Fuera de alcance

Código, migraciones, frontend, navegación, SDK, proveedor concreto, credenciales reales, webhook
real, POS, inventario, reservas y comprobantes electrónicos.

`NAV_READY M11` **no se emite**.

---

## 13. Estado

JP-01 y A-M11-01 a A-M11-06 están cerradas y registradas.

**HOLD:** no continuar a implementación o migración hasta nueva autorización.
