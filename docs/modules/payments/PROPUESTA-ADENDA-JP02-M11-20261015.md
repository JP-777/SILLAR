# Propuesta de adenda JP-02 — M11 Pagos en línea

- **Fecha de propuesta:** 15/10/2026 — America/Lima
- **Alcance:** `DOCS-ONLY`
- **Base congelada:** `58bb93a6f3021490a14c9144afd31fc7c02458a0`
- **Documento destino 1:** `docs/modules/payments/SPEC.md`, blob
  `0fd11be9e042660af133d23180442ea2a12f5351`
- **Documento destino 2:** `docs/modules/payments/DECISIONES-PREVIAS-M11.md`, blob
  `51dc518174fac95b30f40a01e5ceb0d8f3bb3cd0`
- **Fuente JP-02:** `02_ACTA_JP02_M11_ORIGEN_DE_PAGO_20261014.md`, SHA-256
  `bed1464bc5c6cdf5248b678730a2654beba21717811113dfcb93fddd8e281342`
- **Estado:** propuesta para auditoría documental; todavía no integrada

> Este documento propone texto y ubicaciones exactas. No modifica los documentos destino, no crea
> contratos técnicos, no autoriza código o migraciones y no declara M11 aprobado o certificado.
> Los números de línea corresponden exclusivamente a los blobs indicados; las anclas textuales son
> la referencia obligatoria si las líneas cambian antes de integrar.

## 1. Objetivo y límites de la adenda

La adenda debe incorporar la decisión de producto JP-02 sin alterar las decisiones arquitectónicas
ya cerradas:

- todo pago comercial M03 declara exactamente `source = staff | provider`;
- `staff` exige atribución humana real y completa;
- `provider` exige los tres datos humanos nulos en conjunto y evidencia externa verificada;
- no existe tercer origen, atribución parcial ni trabajador ficticio;
- `source` —quién o qué confirmó— no es el medio de pago —cómo se pagó—;
- la aplicación del pago de proveedor es atómica e idempotente dentro de M03;
- M11 conserva la interacción técnica y nunca escribe `sales.*`;
- la decisión no fija interfaces, tablas, columnas, rutas, proveedor, SDK ni DDL.

No se reabren JP-01, A-M11-01..06, la propiedad del ledger, las dos barreras de idempotencia, la
clasificación de replicación, la retención del webhook ni la continuidad de Yape/efectivo en M03.

## 2. Matriz de diferencias frente a los documentos vigentes

| Tema | `SPEC.md` vigente | `DECISIONES-PREVIAS-M11.md` vigente | Diferencia que integra JP-02 |
|---|---|---|---|
| Discriminante | Habla de «origen humano» y «origen externo» | Misma formulación en A-M11-02 | Hacer explícito el dominio cerrado `staff | provider` |
| Rama `staff` | Dice «trío completo» | Dice «trío completo» | Exigir nombre congelado no vacío, ID local positivo y nodo de pertenencia no vacío, con acción autenticada/autorizada |
| Rama `provider` | Dice «cero atribución humana + identidad externa/idempotente» | Misma formulación | Exigir los tres datos humanos `NULL` en conjunto y evidencia externa verificada |
| Valores inválidos | Prohíbe un admin «Sistema» | Prohíbe persona/ID ficticios | Prohibir además tercer `source` y las seis combinaciones parciales del trío |
| Origen frente a medio | No está expresado | No está expresado | Aclarar que `source` es ortogonal al medio de pago |
| Atomicidad M03 | Habla de aplicación reintentable/idempotente | A-M11-04 habla de inbox, reintento e idempotencia | Exigir que deduplicación, registro o reconocimiento y consecuencia comercial formen una sola operación atómica M03 |
| Datos históricos | No está expresado | No está expresado | Exigir que un futuro diseño preserve las filas anteriores como `staff`, sin autorizar migración |
| Estado de JP-02 | §13 solo nombra JP-01 | Encabezado y HOLD solo nombran JP-01/A-M11 | Registrar JP-02 como ratificada-no-implementada y mantener HOLD |
| Pago tardío | Pregunta genérica abierta | Pregunta genérica abierta | Precisar que lo abierto es el tratamiento de origen `provider`; no reabrir el pago tardío humano de M03 |

JP-02 es compatible con A-M11-02 y A-M11-04: las precisa. No ratifica los nombres, endpoints o
modelos físicos de la SPEC histórica del 14/10.

## 3. Cambios propuestos en `SPEC.md`

### S-01 — Registrar JP-02 en los metadatos

**Ubicación exacta:** después de la línea 9, cuya ancla es:

```text
- **Creación:** 04/10/2026, 17:24:29 -05:00 — America/Lima
```

**Insertar:**

```markdown
- **Decisión JP-02:** 14/10/2026 — origen `staff | provider`, ratificada como decisión de producto
```

Al integrar, `Última modificación` y `Última verificación` deben reflejar la fecha real de integración.
No se propone fingir un nuevo «commit de código verificado»: la adenda es documental.

### S-02 — Sustituir la lista de invariantes por la versión consolidada

**Ubicación exacta:** reemplazar las líneas 75–86, desde `1. El importe autoritativo...` hasta
`12. El cuerpo crudo...`, bajo `## 4. Invariantes de Paso 1`.

**Texto propuesto:**

```markdown
1. El importe autoritativo se obtiene de M03, nunca del navegador.
2. Un redirect exitoso no constituye confirmación de pago.
3. La confirmación comercial requiere una señal verificable del lado servidor.
4. Una misma liquidación externa no puede producir dos pagos comerciales.
5. Todo pago comercial declara exactamente `source = staff | provider`.
6. `staff` exige atribución humana real y completa: nombre congelado no vacío, identificador local
   positivo y nodo de pertenencia no vacío.
7. `provider` exige los tres datos de atribución humana `NULL` en conjunto y evidencia externa
   verificada, incluida una identidad idempotente de liquidación.
8. No se admite un tercer `source` ni una atribución humana parcial.
9. M11 no inventa un usuario administrativo, un trabajador «Sistema» ni otro sustituto humano.
10. `source` identifica quién o qué confirmó el pago; no sustituye ni codifica el medio de pago.
11. M03 aplica el pago de proveedor de forma atómica e idempotente dentro del dueño del ledger.
12. Los estados del proveedor no forman parte de la máquina de estados del pedido.
13. M11 no escribe directamente en tablas de M03.
14. PAN y CVV no forman parte del modelo persistente de SILLAR.
15. Con M11 desactivado, M03 conserva Yape y efectivo.
16. El proveedor concreto y su SDK quedan fuera del Paso 1.
17. Los secretos de proveedor/webhook no viven en PostgreSQL.
18. El cuerpo crudo del webhook no se conserva.
```

### S-03 — Reemplazar la descripción implícita del origen

**Ubicación exacta:** reemplazar las líneas 99–103, desde `` `OrderPayment` debe distinguir: ``
hasta `- **origen externo:** ...`, inclusive. Conservar sin cambios la línea siguiente:
`M11 nunca accede al DbContext...`.

**Texto propuesto:**

```markdown
`OrderPayment` debe distinguir exactamente dos orígenes de confirmación:

| `source` | Atribución humana | Evidencia obligatoria |
|---|---|---|
| `staff` | Los tres datos presentes y válidos: nombre congelado, identificador local positivo y nodo de pertenencia no vacío | Acción administrativa autenticada y autorizada |
| `provider` | Los tres datos `NULL` en conjunto | Identidad de proveedor y operación verificadas, referencia externa e identidad idempotente persistida por M03 |

No existe un tercer `source`. Cualquier combinación parcial de atribución humana es inválida.
`source` no es el medio de pago: el origen declara quién o qué confirmó; el medio declara cómo se
pagó. No se inventa una persona, un administrador «Sistema» ni otro valor sintético para `provider`.
```

Los nombres físicos de los tres datos, de la evidencia y de cualquier restricción quedan fuera de
esta adenda. El nombre `OrderPayment` ya existe en el texto vigente y no se propone uno nuevo.

### S-04 — Hacer explícita la atomicidad dentro de M03

**Ubicación exacta:** reemplazar la línea 126, `La aplicación hacia M03 es **reintentable e
idempotente**.`, bajo `## 7. Inbox, atomicidad e idempotencia`.

**Texto propuesto:**

```markdown
La aplicación dentro de M03 es **atómica, reintentable e idempotente**. En una sola operación del
dueño del ledger, M03 revalida las reglas comerciales, deduplica la liquidación externa, registra o
reconoce el pago `source=provider` y aplica únicamente la consecuencia comercial autorizada. M11 no
divide esa operación ni escribe `sales.*`.
```

**Inserción adicional exacta:** después de la tabla de unicidades, tras la línea 133
``| M03 | `external_settlement_id` |``.

```markdown
La repetición de la misma liquidación debe recuperar el mismo resultado durable de M03. Un timeout o
una caída de M11 no autorizan a crear otra identidad ni a mover el pago o saldo por segunda vez.
```

### S-05 — Completar criterios previos a primera migración

**Ubicación exacta:** en `## 10. Criterios previos a primera migración`, reemplazar la viñeta de la
línea 205, `- origen humano y externo sin atribución falsa;`, por las siguientes viñetas:

```markdown
- dominio cerrado `source = staff | provider`, sin tercer valor;
- `staff` con atribución humana completa, real y autorizada;
- `provider` con atribución humana completamente nula y evidencia externa verificada;
- rechazo de toda atribución parcial o sintética;
- separación demostrada entre origen y medio de pago;
- aplicación atómica e idempotente dentro de M03;
- estrategia autorizada que preserve las filas anteriores como `staff`, si llega a existir migración;
```

**Inserción adicional exacta:** después de `- ambas unicidades;` en la línea 208:

```markdown
- pruebas legales e ilegales de ambas ramas de `source`, incluidas las seis combinaciones parciales
  del trío humano y un tercer valor;
```

La última frase de la sección —`Esto **no autoriza la migración**.`— se conserva sin cambios.

### S-06 — Precisar preguntas de producto abiertas

**Ubicación exacta:** reemplazar íntegramente las líneas 223–228 bajo `## 11. Preguntas de producto
abiertas`.

**Texto propuesto:**

```markdown
- medios online de v1 además de tarjeta, si alguno;
- tratamiento del pago confirmado por `provider` después del vencimiento, sin reabrir la política
  de pago tardío humano de M03;
- pagos parciales o múltiples;
- reembolsos y contracargos;
- comisión del proveedor;
- moneda operativa y su autoridad;
- proveedor, modalidad de integración, SDK y sandbox;
- cualquier ampliación futura hacia B2B/M07 u otros orígenes de pedido fuera de M03 v1.
```

Conservar `No se asume un valor por defecto.`. La lista registra decisiones: no selecciona proveedor,
SDK, modalidad, medio ni política.

### S-07 — Actualizar el estado sin levantar el HOLD

**Ubicación exacta:** reemplazar la línea 245, `JP-01 y A-M11-01 a A-M11-06 están cerradas y
registradas.`, bajo `## 13. Estado`.

**Texto propuesto:**

```markdown
JP-01, JP-02 y A-M11-01 a A-M11-06 están registradas. JP-02 está ratificada como decisión de
producto, pero no autoriza por sí sola modelo físico, contrato concreto, código ni migración.
```

Conservar sin cambios el `HOLD` de la línea 247.

## 4. Cambios propuestos en `DECISIONES-PREVIAS-M11.md`

### D-01 — Registrar JP-02 en los metadatos

**Ubicación exacta:** después de la línea 8:

```text
- **Decisión JP-01:** 04/10/2026
```

**Insertar:**

```markdown
- **Decisión JP-02:** 14/10/2026 — origen `staff | provider`
```

`Última modificación` y `Última verificación` se actualizan solo al integrar realmente la adenda.

### D-02 — Insertar la decisión de producto JP-02 como sección propia

**Ubicación exacta:** insertar después del separador de la línea 37 que cierra JP-01 y antes del
actual `## 2. Decisiones de Paso 1 que permanecen cerradas`.

**Texto propuesto:**

````markdown
## 2. JP-02 — origen de pagos M03 para M11 — RATIFICADA por JP el 14/10/2026

Todo pago comercial registrado por M03 declara exactamente:

```text
source = staff | provider
```

| Origen | Atribución humana | Evidencia obligatoria |
|---|---|---|
| `staff` | Nombre congelado no vacío, identificador local positivo y nodo de pertenencia no vacío; los tres presentes | Acción administrativa autenticada y autorizada |
| `provider` | Los tres datos humanos `NULL` en conjunto | Identidad de proveedor y operación verificadas, referencia externa e identidad idempotente persistida en M03 |

No se acepta un tercer `source`, una atribución humana incompleta ni un trabajador ficticio. Una
redirección del navegador o un webhook sin verificación criptográfica no constituyen evidencia de
pago.

`source` y medio de pago son conceptos distintos. `source` identifica quién o qué confirmó el pago;
el medio identifica cómo se pagó. JP-02 no elige ni añade un medio online concreto.

La aplicación comercial de un pago `provider` es atómica e idempotente dentro de M03. M11 conserva
intentos, recepción y evidencia técnica; nunca escribe `sales.*`, no actualiza pedidos por SQL ajeno
y no sustituye el ledger canónico.

Un futuro diseño autorizado deberá preservar las filas anteriores como `staff` y su atribución, pero
esta decisión no autoriza todavía contrato concreto, entidad, columnas, restricciones, DDL, migración
o código.

---
````

**Renumeración mecánica exacta:** después de insertar la sección, cambiar solo los encabezados
numéricos actuales:

- `## 2.` → `## 3.`;
- `## 3.` → `## 4.`;
- `## 4.` → `## 5.`;
- `## 5.` → `## 6.`;
- `## 6.` → `## 7.`;
- `## 7. HOLD` → `## 8. HOLD`.

Los identificadores D-M11 y A-M11 no se renumeran.

### D-03 — Precisar A-M11-02 sin cambiar su cierre

**Ubicación exacta:** dentro de `### A-M11-02 · Ledger canónico y liquidación externa — CERRADA`,
reemplazar las líneas 97–104, desde `` `OrderPayment` debe poder distinguir: `` hasta la prohibición
del identificador ficticio, inclusive.

**Texto propuesto:**

```markdown
Conforme a JP-02, `OrderPayment` debe poder distinguir exactamente `source = staff | provider`:

- `staff`: atribución humana completa y real, con acción autenticada y autorizada;
- `provider`: los tres datos humanos `NULL` en conjunto y evidencia externa verificada, incluida una
  identidad idempotente de liquidación.

No se admite un tercer origen, atribución parcial ni persona, administrador «Sistema» o identificador
humano ficticio. `source` no sustituye al medio de pago.

M11 **nunca escribe `sales.*`**.
```

### D-04 — Completar A-M11-04 con atomicidad M03

**Ubicación exacta:** reemplazar la línea 118 bajo `### A-M11-04`, que actualmente dice que M11 usa
inbox y la aplicación hacia M03 es reintentable/idempotente.

**Texto propuesto:**

```markdown
M11 usa **inbox durable**. M03 aplica el pago de proveedor en una operación **atómica, reintentable e
idempotente** dentro del dueño del ledger: deduplica la liquidación, registra o reconoce el pago
`source=provider` y aplica una sola vez la consecuencia comercial autorizada.
```

Conservar íntegramente las dos unicidades y las tres pruebas mínimas ya escritas.

### D-05 — Precisar las preguntas comerciales abiertas

**Ubicación exacta:** reemplazar las líneas 183–188 bajo la sección actualmente numerada `## 4`, que
pasará a `## 5` tras D-02.

**Texto propuesto:**

```markdown
- qué medios online incluye v1 además de tarjeta, si alguno;
- tratamiento del pago confirmado por `provider` después del vencimiento, sin reabrir el pago tardío
  humano ya decidido en M03;
- pagos parciales o múltiples;
- reembolsos y contracargos;
- tratamiento de la comisión del proveedor;
- moneda operativa y su autoridad;
- proveedor, modalidad de integración, SDK y sandbox;
- cualquier ampliación futura hacia B2B/M07 u otros pedidos fuera de M03 v1.
```

Conservar la frase `No se convierten en defaults técnicos.`.

### D-06 — Completar condiciones antes de primera migración

**Ubicación exacta:** en la sección actualmente numerada `## 6`, que pasará a `## 7`, insertar después
de la línea 220 `- contrato M03/M11 compatible con A-M11-02 y A-M11-03.`:

```markdown
- diseño del dominio cerrado `source = staff | provider` y sus invariantes legales/ilegales;
- estrategia autorizada para preservar filas anteriores como `staff` sin perder atribución;
- aplicación atómica e idempotente dentro de M03;
- pruebas de `staff` completo, `provider` con trío nulo, seis combinaciones parciales, tercer origen,
  trabajador ficticio, duplicados y concurrencia;
- prueba de que origen y medio de pago no se confunden.
```

**Reemplazar además la primera oración de la sección**, hoy en la línea 211:

```markdown
Aunque JP-01, JP-02 y A-M11-01 a A-M11-06 están registradas, **la primera migración no está
autorizada**.
```

### D-07 — Mantener el HOLD y registrar el alcance de JP-02

**Ubicación exacta:** en la sección renumerada `## 8. HOLD`, reemplazar la línea 226:

```text
A-M11-01 a A-M11-06 quedan registradas como **CERRADAS**.
```

**Por:**

```markdown
JP-01, JP-02 y A-M11-01 a A-M11-06 quedan registradas. JP-02 ratifica una decisión de producto y no
constituye aprobación de diseño físico, contrato concreto, código o migración.
```

Conservar sin cambios la prohibición final de continuar sin nueva autorización.

## 5. Criterios de aceptación de la integración documental

La adenda solo queda documentalmente bien integrada si se acredita todo lo siguiente:

1. Ambos documentos registran la fecha y el alcance exacto de JP-02.
2. Ambos usan exclusivamente `source = staff | provider` y rechazan un tercer valor.
3. `staff` exige los tres datos humanos presentes, reales y válidos.
4. `provider` exige los tres datos humanos `NULL` en conjunto; no se usa «Sistema» ni sustituto.
5. Cualquier combinación parcial queda declarada inválida.
6. La evidencia de `provider` es externa y verificada por servidor; redirect o payload no autenticado
   no confirman dinero.
7. Origen y medio de pago quedan expresamente separados.
8. La operación atómica/idempotente pertenece a M03; M11 no escribe `sales.*`.
9. Se preservan A-M11-04: `(provider, provider_event_id)`, `external_settlement_id` y prueba de caída.
10. Se preservan A-M11-05 y ADR-018: operación M11 local, pago M03 replicado, sin inventar referencias.
11. Se mantiene un solo concepto canónico de pago confirmado y el bus no se convierte en garantía
    durable.
12. La integración no introduce interfaces, tablas, columnas, rutas, proveedor, SDK ni DDL.
13. Las decisiones abiertas permanecen abiertas y sin defaults.
14. El HOLD y la frase «primera migración no autorizada» permanecen visibles en ambos documentos.

## 6. Criterios que deberá cubrir un diseño posterior autorizado

Estos criterios no son pruebas ejecutadas ni permiso para implementar:

| Caso | Resultado requerido |
|---|---|
| `staff` con trío completo y acción autorizada | Legal |
| `staff` con uno o más datos ausentes, vacíos o ID no positivo | Ilegal |
| `provider` con trío humano completamente nulo y evidencia verificada | Legal |
| `provider` con uno o más datos humanos presentes | Ilegal |
| Cualquiera de las seis combinaciones parciales del trío | Ilegal |
| Tercer `source` | Ilegal |
| Trabajador «Sistema», ID cero o nodo vacío | Ilegal |
| Redirect exitoso sin señal verificable del servidor | No aplica pago |
| Evento no firmado o firma inválida | No aplica pago |
| Mismo evento repetido | Una recepción/aplicación efectiva |
| Eventos distintos para la misma liquidación | Un pago comercial M03 |
| Reintentos concurrentes | Un pago comercial M03 |
| Caída tras aplicar en M03 y antes de marcar inbox | Reintento converge sin mover pago/saldo dos veces |
| Filas anteriores a un futuro cambio autorizado | Conservan atribución y semántica `staff` |
| M11 desactivado | M03 conserva Yape y efectivo manuales |

## 7. Decisiones pendientes

### 7.1 Producto — decide JP

- medios online de v1 además de tarjeta, si alguno;
- tratamiento de pagos `provider` después del vencimiento;
- pagos parciales o múltiples;
- reembolsos y contracargos;
- tratamiento de la comisión;
- moneda operativa y su autoridad;
- proveedor, modalidad, SDK y sandbox;
- cualquier ampliación futura hacia B2B/M07 u otros pedidos fuera de M03 v1.

### 7.2 Diseño técnico posterior — dueño M03 e Integración, con autorización

- representación física de `source` y de sus restricciones;
- evidencia externa mínima que M03 debe conservar;
- forma y alcance de la identidad idempotente de liquidación;
- operación pública y resultados, sin fijarlos en esta adenda;
- estrategia sobre datos existentes y reversibilidad cuando proceda;
- pruebas PostgreSQL, concurrencia, replicación y ADR-018.

Ninguno de estos puntos recibe un valor por defecto en esta propuesta.

## 8. Bloqueos y siguiente paso

- La adenda no puede aplicarse a los documentos canónicos sin autorización expresa posterior.
- La decisión JP-02 todavía no está materializada en M03; esta propuesta no autoriza hacerlo.
- Las decisiones de producto de la §7.1 bloquean cualquier diseño completo de cobro real.
- Proveedor, SDK, rutas, interfaces, tablas, columnas y DDL permanecen deliberadamente sin definir.

**Siguiente paso propuesto:** auditoría documental de esta propuesta. Si el dictamen y JP autorizan la
integración, aplicar exclusivamente los cambios S-01..S-07 y D-01..D-07 sobre los blobs verificados,
actualizar su trazabilidad documental y volver a validar, todavía sin implementación ni migración.
