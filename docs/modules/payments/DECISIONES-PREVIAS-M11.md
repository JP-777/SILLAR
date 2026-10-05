# Decisiones previas — M11 Pagos

- **Módulo:** M11 Pagos
- **Código previsto:** `payments`
- **Producto:** SILLAR WEB
- **Fase:** 4
- **Estado:** Paso 1 · SPEC / decisiones / contratos / dependencias
- **Decisión JP-01:** 04/10/2026
- **Creación:** 04/10/2026, 17:24:29 -05:00 — America/Lima
- **Última modificación:** 04/10/2026 — America/Lima
- **Última verificación:** 04/10/2026 — America/Lima
- **Commit de código verificado:** `35647181891a9b78a7399d3b108d9a4415a0d48a`

Este documento conserva lo decidido antes de escribir producción y separa las decisiones de
producto de las decisiones arquitectónicas ratificadas por el Líder Técnico.

No autoriza código, migraciones, frontend, navegación, SDK ni integración real con proveedor.
**La primera migración sigue sin autorización.**

---

## 1. JP-01 — relación M11 / M09 — RESUELTA por JP el 04/10/2026

La condición histórica «M11 no antes que M09» **no sigue vigente**.

- M09 Inventario **no es dependencia dura** de M11.
- M09 Inventario **no es dependencia blanda** de M11.
- M09 Inventario **no es requisito previo de construcción** de M11 v1.
- M11 continúa dentro de SILLAR WEB.
- `inventory` no se añade a `HardDependencies` ni a `SoftDependencies`.
- Si en el futuro aparece un caso real que requiera inventario, se especificará como una integración
  nueva.

La condición anterior del líder «si JP decide que M11 espera al ERP» queda superada por esta
decisión posterior de JP y **no reabre JP-01**.

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

## 3. Decisiones arquitectónicas ratificadas por el Líder Técnico — 04/10/2026

### A-M11-01 · Autenticación / dependencia M04 — CERRADA

Cuando M11 use autenticación de cliente, **M04 es dependencia dura de M11**.

No se admite una dependencia transitiva oculta a través de M03: si M11 consume contratos de
autenticación o identidad de M04, esa dependencia se declara explícitamente.

### A-M11-02 · Ledger canónico y liquidación externa — CERRADA

**M03 conserva el ledger canónico de pagos.**

`OrderPayment` debe poder distinguir:

- **origen humano:** trío completo de atribución humana;
- **origen externo:** cero atribución humana + identidad externa/idempotente.

M11 **nunca escribe `sales.*`**.

No se inventa una persona, un administrador «Sistema» ni un identificador humano ficticio.

### A-M11-03 · Pago confirmado canónico — CERRADA

Existe **un solo concepto canónico de pago confirmado**.

Si el contrato necesita enriquecerse para representar pagos automáticos, se prefiere
**evolución/versionado explícito** antes que cambiar silenciosamente la semántica de
`PagoConfirmado`.

No se crean dos universos permanentes de pago manual y pago online.

### A-M11-04 · Atomicidad, inbox, idempotencia y reintentos — CERRADA

M11 usa **inbox durable** y la aplicación hacia M03 es **reintentable e idempotente**.

Las dos unicidades siguientes forman **UN MISMO mecanismo**:

- **M11:** `(provider, provider_event_id)`;
- **M03:** `external_settlement_id`.

**Condición del Líder Técnico:** ambas barreras deben probarse en ambas direcciones
**antes de la primera migración**.

Pruebas mínimas:

1. repetir el mismo `(provider, provider_event_id)` → M11 no duplica la recepción/aplicación;
2. dos eventos distintos que representen el mismo `external_settlement_id` → M03 no duplica el pago;
3. prueba obligatoria de caída:
   1. recibir webhook válido;
   2. persistir recepción;
   3. aplicar a M03;
   4. **simular caída antes de marcar el inbox como aplicado**;
   5. reentregar/reintentar;
   6. M03 deduplica por `external_settlement_id`;
   7. comprobar que el pago/saldo del pedido **NO se mueve dos veces**;
   8. finalmente marcar el inbox como aplicado.

Sin esa tercera prueba la idempotencia **no queda acreditada**.

### A-M11-05 · Replicación — CERRADA

La operación técnica de M11 es **local / no replicada**:

- intentos;
- inbox;
- reintentos;
- estado del proveedor.

El **pago comercial canónico de M03 es replicado**.

Esta clasificación no autoriza todavía la primera migración.

### A-M11-06 · Secretos, webhook y retención — CERRADA

Los secretos de proveedor y webhook viven **fuera de PostgreSQL**, siguiendo el patrón de
despliegue/entorno.

Para cada webhook:

- la firma se verifica **al recibir**;
- el resultado de verificación se registra;
- se conserva el **SHA-256** del cuerpo recibido;
- se conservan los metadatos normalizados necesarios;
- el **cuerpo crudo NO se conserva**.

**Consecuencia explícita:** SILLAR **no podrá reverificar posteriormente la firma exacta del
webhook**, porque el cuerpo original ya no estará disponible.

En una disputa, la evidencia disponible será el SHA-256 y los datos/resultados registrados en el
momento de recepción.

Esto es una decisión deliberada de arquitectura, no una omisión accidental ni una simple nota de
minimización.

---

## 4. Preguntas comerciales que continúan abiertas

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
| M04 Clientes | **Dura cuando M11 use autenticación de cliente — A-M11-01** |
| M09 Inventario | **Ninguna. JP-01 resuelta** |
| M01 Catálogo | Ninguna directa definida |
| M10 Reportes | Consumidor futuro; no dependencia de M11 |
| M13 POS | Ninguna |
| M14 Comprobantes | Ninguna |

---

## 6. Condiciones antes de la primera migración

Aunque A-M11-01 a A-M11-06 están cerradas, **la primera migración no está autorizada**.

Antes de solicitarla deben existir:

- diseño de las dos unicidades de A-M11-04;
- pruebas en ambas direcciones;
- prueba de caída entre «aplicar a M03» y «marcar inbox aplicado»;
- clasificación de las futuras tablas conforme a A-M11-05;
- campos normalizados y SHA-256 conforme a A-M11-06;
- contrato M03/M11 compatible con A-M11-02 y A-M11-03.

---

## 7. HOLD

A-M11-01 a A-M11-06 quedan registradas como **CERRADAS**.

No continuar a código, migraciones, frontend, navegación, SDK ni proveedor real sin nueva
autorización.
