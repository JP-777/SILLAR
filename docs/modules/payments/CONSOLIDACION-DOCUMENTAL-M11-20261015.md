# Consolidación documental M11 Pagos en línea — 2026-10-15

- **Módulo:** M11 Pagos en línea (`payments`)
- **Alcance de este hito:** `DOCS-ONLY`; reconciliación y preparación, sin implementación
- **Base obligatoria y verificada:** `origin/main` `58bb93a6f3021490a14c9144afd31fc7c02458a0`
- **Tree verificado:** `319b88aad723179c1f10a8351f755994170a6fe5`
- **Estado de este documento:** propuesta lista para auditoría; no es adenda canónica
- **Autoridad final:** JP; coordinación documental: Chat 3

> Este documento no modifica ni sustituye `SPEC.md` o `DECISIONES-PREVIAS-M11.md`, no autoriza
> migraciones ni código y no declara M11 aprobado o certificado. Separa hechos vigentes, una decisión
> de producto ratificada pero todavía no integrada —JP-02— y propuestas que requieren ratificación.

## 1. Resultado ejecutivo

1. `main` ya contiene íntegramente JP-01 y A-M11-01 a A-M11-06. Los blobs de los dos documentos
   canónicos coinciden con los de la rama histórica `docs/m11-paso1-jp01`, aunque esa rama no es
   ancestro de `main`: `58bb93a` reintegró los documentos por contenido.
2. El mensaje del commit base declara expresamente que **JP-02 no fue incorporada**. Su ausencia en
   los documentos canónicos no es rechazo: el acta adjunta, posterior al contenido fechado el
   04/10/2026, está ratificada como decisión de producto y pendiente de integración documental.
3. La semántica general de JP-02 ya está implícita en A-M11-02: origen humano con atribución completa
   frente a origen externo sin atribución humana y con identidad idempotente. Faltan el discriminante
   cerrado `source = staff | provider`, las condiciones completas de cada rama, la nulabilidad conjunta
   del trío humano y la exigencia expresa de atomicidad dentro de M03.
4. La realidad de M03 todavía es anterior a JP-02: `OrderPayment` y su configuración exigen siempre
   el trío humano; no hay `source`, identidad de proveedor, clave única de liquidación ni contrato
   público para aplicar un pago externo. `SalesModule` solo publica `ICustomerOrderHistory`; este
   contrato no decide pagabilidad ni aplica liquidaciones.
5. La SPEC histórica del 14/10 aporta escenarios, amenazas y candidatos de contrato útiles, pero sus
   tablas, endpoints, interfaces, nombres y política de replicación no están ratificados. En particular,
   su propuesta de replicar intentos/liquidaciones M11 contradice A-M11-05: la operación técnica M11 es
   local/no replicada y el pago comercial M03 sí se replica.
6. No se localizó en `Sillar.Core.Contracts` el supuesto filtro `same-origin`. Se localizaron un filtro
   CSRF para escrituras autenticadas por cookie y dos usos frontend de `credentials: 'same-origin'`;
   ninguno acredita el contrato mencionado por el relevo.

## 2. Inventario, integridad y precedencia

### 2.1 Barrera de arranque e integridad del expediente

| Evidencia | Identidad | Resultado |
|---|---|---|
| `origin/main` | commit `58bb93a6f3021490a14c9144afd31fc7c02458a0` | Coincide con la base obligatoria |
| Tree de `origin/main` | `319b88aad723179c1f10a8351f755994170a6fe5` | Coincide con el expediente |
| Rama de trabajo | `docs/m11-payments-consolidacion-20261015` | Parte del commit obligatorio |
| `00_INSTRUCCIONES_CODEX_B_M11.md` | SHA-256 `4f917b511c1ceae857fe37513797ef63fc5e5aab4b4705021b4d9b16a9707ca2` | `OK` |
| `01_SPEC_HISTORICA_M11_20261014.md` | SHA-256 `e77819ebd069cf66450220b865e442877b8c8d8dc32cedf4107e15a10dd3bd9b` | `OK` |
| `02_ACTA_JP02_M11_ORIGEN_DE_PAGO_20261014.md` | SHA-256 `bed1464bc5c6cdf5248b678730a2654beba21717811113dfcb93fddd8e281342` | `OK` |
| `03_MATRIZ_HISTORICA_FRENTES_20261014.md` | SHA-256 `a584eef7ae074c7d37800442df87be1ccd78707d9debdb0720f1c9b6968c1704` | `OK` |

`git fetch origin` no pudo actualizar `FETCH_HEAD` porque el gitdir compartido está montado en modo
solo lectura. La referencia local `origin/main`, el commit, el tree y los blobs esperados sí pudieron
verificarse. No se usa esa limitación para afirmar frescura remota posterior al commit recibido.

### 2.2 Regla de precedencia usada

1. Una decisión expresa y posterior de JP prevalece dentro de su ámbito exacto.
2. Para todo lo no modificado por esa decisión, prevalecen los documentos canónicos presentes en la
   base obligatoria de `main`.
3. Una rama histórica con blobs idénticos aporta trazabilidad, no una autoridad distinta.
4. Los artefactos históricos de Library/expediente aportan antecedentes. Una propuesta no adquiere
   autoridad por estar fechada después ni por aparecer en una matriz de trabajo.
5. El código actual se usa para medir la brecha de implementación; no invalida por sí solo una
   decisión de producto posterior y tampoco se reinterpreta como si ya la implementara.

### 2.3 Matriz de fuentes y estado

| Fuente | Blob / commit / SHA-256 | Clasificación y precedencia |
|---|---|---|
| `origin/main` | commit `58bb93a6f3021490a14c9144afd31fc7c02458a0` | Base canónica de esta consolidación |
| `docs/modules/payments/SPEC.md` | blob `0fd11be9e042660af133d23180442ea2a12f5351` | Canónica en `main`; JP-01 y A-M11-01..06 cerradas; JP-02 ausente |
| `docs/modules/payments/DECISIONES-PREVIAS-M11.md` | blob `51dc518174fac95b30f40a01e5ceb0d8f3bb3cd0` | Canónica en `main`; misma conclusión |
| `docs/PENDIENTES.md` §25 | blob `2555da639ade9a40783af98cc3513f95bfe4dae7` | Decisión JP-01 cerrada: M11 no espera a M09 |
| `docs/ARQUITECTURA_MODULAR.md` | blob `f43e1502a1953197c0dc2f0236f4db20e8c5a54c` | M11 depende duro de M03; reglas de contratos y schemas |
| `docs/ROADMAP_MODULAR.md` | blob `0b207a30104c9c813442b02e5ceb0f4038a13d16` | M11 está en SILLAR WEB, Fase 4, después de M10 |
| ADR-016 / ADR-017 / ADR-018 | blobs `7e86acb04791052f2be9e20f8e524278d1ba534d` / `6bf912731eb430f45165dc99dea39c08f14600ad` / `5f19d0243e865eff6ad685751a2546594a4133df` | Aceptadas; gobiernan identidad, replicación y referencias cruzadas |
| `docs/modules/sales/SPEC.md` / `DECISIONES-VIGENTES-M03.md` | blobs `6156206db795850849c22c192f5eac8f45d373aa` / `09372722dadcb235c034caf1b59d43828ef8407b` | Canónicas M03; pago manual y política humana previa a JP-02 |
| `Sales.Contracts`: historial / estado / medio / eventos | blobs `1b723f6156abcf85900ce07199a5e470812c9d09` / `a97060d01127eddbf0fc7937a3b55fd203143ded` / `1e8b7d3052709f6b6b02c7e5e53d100515a8f6ae` / `c35c802d4ce924a0dbb6aa0881947fc19b6730d8` | Realidad pública M03: no existe contrato de liquidación externa |
| M03 `OrderPayment` / configuración / `SalesModule` | blobs `11d080c8923307af105f52acb2f7d7061224e781` / `d3a17ef6a9194325bd286cd9f50e2ed9e57b9153` / `37d5bfe17abf4ad837ff4d798478567c33b14d63` | Implementación leída para medir la brecha; no modificada |
| `Core.Contracts/CsrfEndpointFilter.cs` | blob `3c69dba6380fc927de5d4955d7a06c88a0d8bd35` | Filtro CSRF localizado; no es un filtro `same-origin` |
| Rama `docs/m11-paso1-jp01` | commit `196b9c6deb206bc2ed91662f3517347eef65dc10`; tree `830d1bdf84cb5184508f46436ed7cff1498d9495` | Histórica. No es ancestro de `main`, pero sus dos blobs M11 son idénticos a los canónicos |
| Library: SPEC histórica 14/10 | SHA-256 `e77819ebd069cf66450220b865e442877b8c8d8dc32cedf4107e15a10dd3bd9b` | Propuesta histórica; útil como inventario, no ratificada como SPEC vigente |
| Library: acta JP-02 14/10 | SHA-256 `bed1464bc5c6cdf5248b678730a2654beba21717811113dfcb93fddd8e281342` | **Ratificada-no-integrada**; autoridad limitada al origen `staff|provider` y sus invariantes |
| Library: matriz de frentes 14/10 | SHA-256 `a584eef7ae074c7d37800442df87be1ccd78707d9debdb0720f1c9b6968c1704` | Mixta: organización histórica y recomendaciones; no ratificar por silencio |
| Commit base de la SPEC histórica | `4a3fd1ef12fc2226bad185f003acea9168b9a4c9` | Existe en el repositorio; antecedente, no base actual |

Los encabezados de `SPEC.md` y `DECISIONES-PREVIAS-M11.md` aún declaran verificación contra
`35647181891a9b78a7399d3b108d9a4415a0d48a` y fecha 04/10/2026. Esta consolidación contrasta además
la realidad M03 presente en la base obligatoria del 15/10, sin reescribir esos encabezados.

### 2.4 Reconciliación de contradicciones y propuestas históricas

| Tema | Fuente histórica | Fuente vigente / posterior | Resultado |
|---|---|---|---|
| M11 espera a M09 | SPEC histórica lo marcaba pendiente | JP-01 en `main` | **Cerrado:** no espera y no declara `inventory` |
| M01/M04 como dependencias transitivas | SPEC/matriz histórica | A-M11-01 y tabla canónica | **Corregido:** M01 no tiene dependencia directa definida; M04 es dura si M11 consume su autenticación/identidad. No se admite dependencia transitiva oculta |
| Intentos y liquidaciones M11 replicables | SPEC histórica | A-M11-05 | **Contradicción resuelta a favor de `main`:** operación técnica M11 local/no replicada; pago comercial M03 replicado |
| Tablas, columnas, interfaces y endpoints concretos | SPEC histórica | SPEC canónica evita fijarlos | **Propuesta no ratificada**; no se arrastra como diseño decidido |
| Un solo proveedor en v1 | SPEC histórica | `main` solo deja proveedor/SDK fuera del Paso 1 | **Propuesta no ratificada** |
| Body crudo no persistido | SPEC histórica y `main` | A-M11-06 | **Cerrado y alineado**, incluida la pérdida de reverificación posterior |
| Origen humano/externo | A-M11-02 | JP-02 | **JP-02 precisa y cierra la semántica** `staff|provider`; falta integrarla |
| Pago siempre humano en M03 | documentación/código M03 anteriores | JP-02 posterior | **Brecha documental/técnica**, no permiso de implementación |
| D04 y D06 de la matriz histórica | entonces recomendaciones | JP-01 y JP-02 | D04 quedó ratificada por JP-01. D06 quedó ratificada solo en su principio de origen y atomicidad; el contrato exacto continúa abierto |

## 3. JP-01 — reconciliación

### 3.1 Ya está en `main`

- M11 permanece en SILLAR WEB y Fase 4.
- M09 no es dependencia dura, blanda ni requisito previo de construcción de M11 v1.
- No se añade `inventory` a dependencias.
- M11 no reintroduce reserva ni autoridad sobre existencias.
- Si aparece un caso futuro real con inventario, se especificará como integración nueva.

La decisión aparece en ambos documentos M11 y en `docs/PENDIENTES.md` §25. No hay materia pendiente
de JP-01 ni razón documental para reabrirla.

### 3.2 Texto futuro

No hace falta nueva semántica. Una futura adenda solo debería conservar la trazabilidad de JP-01 y
evitar reintroducir las frases históricas «M11 no antes que M09» o «M01/M04 por transitividad».

## 4. JP-02 — reconciliación

### 4.1 Ya está implícito o explícito en `main`

- A-M11-02 distingue origen humano, con atribución completa, de origen externo, sin atribución humana
  y con identidad externa/idempotente.
- M03 conserva el ledger canónico; M11 no escribe `sales.*` ni usa el `DbContext` de M03.
- Está prohibido inventar una persona o un administrador «Sistema».
- A-M11-04 exige aplicación reintentable e idempotente y deduplicación M03 por
  `external_settlement_id`.
- A-M11-03 conserva un único concepto de pago comercial confirmado y exige evolución/versionado
  explícito si cambia el contrato `PagoConfirmado`.
- M03 conserva Yape y efectivo aunque M11 esté desactivado.

### 4.2 Falta en los documentos canónicos

- El nombre y dominio cerrado del discriminante: `source = staff | provider`, sin tercer valor.
- `staff`: nombre congelado no vacío, ID local positivo y nodo de pertenencia no vacío, los tres
  presentes, más acción administrativa autenticada/autorizada.
- `provider`: los tres campos humanos `NULL` en conjunto, sin sustitutos, y evidencia externa
  verificada con identidad de proveedor, referencia de operación y clave idempotente persistida en M03.
- Prohibición explícita de cualquiera de las seis combinaciones parciales del trío humano.
- La aplicación del pago como operación **atómica e idempotente dentro de M03**.
- Preservación de filas anteriores como `staff` cuando exista un diseño de migración autorizado.
- Aclaración de que `source` es procedencia de confirmación y no un medio de pago.

### 4.3 Brecha comprobada contra M03 actual

| Evidencia actual | Consecuencia |
|---|---|
| `PaymentMethod.All` contiene `yape` y `efectivo` | M03 conserva sus medios manuales; no hay método online ratificado en el contrato |
| `OrderPayment` exige `RegisteredBy`, `RegisteredByAdminUserLocalId` y `RegisteredByAdminUserHomeNode` | Solo representa `staff`; no puede representar legalmente `provider` |
| La configuración marca los tres campos como requeridos y sus `CHECK` exigen valores humanos | JP-02 no está materializada en modelo/DDL |
| No hay `source`, identidad de proveedor ni `external_settlement_id` | Falta la discriminación y la segunda barrera de idempotencia |
| `OrderTransitionPolicy.RegistrarPago` rechaza `quien == null` | La política actual presupone confirmación humana |
| `ICustomerOrderHistory` es el único contrato público de pedido registrado por M03 | No ofrece lectura de pagabilidad ni aplicación de liquidación |
| `PagoConfirmado` solo lleva pedido, código y fecha | Es un aviso mínimo, no memoria durable ni contrato de aplicación |
| `SalesModule.MapEndpoints` solo monta rutas del cliente | No existe operación pública M03 para liquidación externa |

Esta brecha requiere diseño y autorización posteriores del dueño M03 e Integración. No habilita a
editar M03 desde este frente documental.

### 4.4 Semántica propuesta para una futura adenda — no aplicada aquí

Una futura adenda a los dos documentos canónicos debería decir, sin fijar todavía nombres de columnas
o interfaces:

> Todo pago comercial M03 declara exactamente un origen de confirmación: `staff` o `provider`.
> `staff` exige el trío humano completo y real. `provider` exige el trío humano completamente nulo y
> evidencia externa verificada, incluida una identidad idempotente de liquidación. No existe tercer
> origen ni atribución parcial o ficticia. M03 aplica la liquidación de proveedor de forma atómica e
> idempotente; M11 conserva la interacción técnica y nunca escribe `sales.*`. Esta semántica no
> autoriza por sí sola DDL, migración, contrato concreto ni código.

La adenda deberá distinguir también `source` de `method`: `staff|provider` responde **quién o qué
confirmó**; Yape, efectivo o un futuro medio online responde **cómo se pagó**.

## 5. Contrato conceptual M03 ↔ M11

Los nombres siguientes describen operaciones, no interfaces, DTOs, rutas ni firmas ratificadas.

### 5.1 Secuencia y responsabilidades

| Paso | Dueño | Contrato conceptual | Invariantes |
|---|---|---|---|
| 1. Obtener pedido pagable e importes | M03 | Resolver un pedido accesible al cliente y devolver una instantánea autoritativa de pagabilidad | M03 decide propiedad, total, vencimiento, estado y si ya existe pago; pedido ajeno e inexistente no se distinguen hacia el cliente; M11 no recalcula ni acepta importe del navegador |
| 2. Crear intención técnica | M11 | Persistir una intención local y pedir al adaptador del proveedor una operación | Usa la instantánea M03; no escribe `sales.*`; referencia e idempotencia nacen en servidor; política de intentos simultáneos espera decisión sobre pagos parciales/múltiples |
| 3. Recibir y verificar evento | M11 | Leer los bytes exactos, calcular SHA-256, verificar autenticidad con secreto externo a PostgreSQL y normalizar solo evidencia necesaria | Redirect/cliente no confirma; un evento no auténtico no produce efecto comercial; body crudo se descarta |
| 4. Deduplicar evento | M11 | Reclamar de forma transaccional la unicidad `(provider, provider_event_id)` en el almacén técnico local | Un ganador procesa; concurrentes observan el mismo resultado; no se inventa ID de webhook para una observación por consulta al proveedor |
| 5. Derivar liquidación externa | M11/adaptador | Convertir evidencia verificada en una identidad estable de liquidación y validar pedido/referencia/importe/moneda | La identidad procede del proveedor y debe sobrevivir a eventos distintos; formato y namespacing exactos no están ratificados |
| 6. Solicitar aplicación idempotente | M03 | En una única operación transaccional, revalidar, deduplicar la liquidación, insertar o reconocer el pago y resolver su consecuencia comercial | `source=provider`, trío humano nulo, evidencia externa; M03 es la única autoridad que muta su ledger/pedido |
| 7. Confirmar resultado o reintentar | M11 | Guardar localmente el resultado estable de M03 o programar reintento | Solo se marca aplicado después de respuesta durable M03; una caída en la ventana crítica converge por la barrera M03 |

### 5.2 Instantánea pagable propuesta — no ratificada

El contrato de lectura necesitará, como mínimo semántico:

- identidad interna del pedido para comunicación servidor-servidor y código visible para trazabilidad;
- identidad del cliente o comprobación de propiedad efectuada dentro de M03;
- importe autoritativo;
- moneda cuando JP defina su autoridad;
- vencimiento y estado del pedido;
- indicador de pago previo o saldo, cuya forma depende de decidir parciales/múltiples;
- resultado explícito `pagable / no pagable / no encontrado`, sin filtrar existencia de pedido ajeno.

`ICustomerOrderHistory` no basta: es una lista de lectura y deliberadamente no expone pagos ni líneas.

### 5.3 Aplicación de liquidación propuesta — no ratificada

La operación M03 debería aceptar conceptualmente: pedido, proveedor, identidad estable de liquidación,
medio online ratificado, importe, moneda, instante de liquidación y evidencia normalizada suficiente.
Debe devolver un resultado estable que distinga al menos:

- aplicada por primera vez;
- ya aplicada —misma liquidación, mismo resultado—;
- inconsistencia permanente que exige revisión —pedido, importe, moneda o referencia—;
- fallo transitorio reintentable.

No se ratifican esos nombres ni su transporte. El diseño M03 deberá definir cómo se serializa o
compone `external_settlement_id` para que dos proveedores no colisionen, sin debilitar la unicidad
cerrada en A-M11-04.

### 5.4 Atomicidad requerida dentro de M03

En una sola transacción M03 debe reclamar la unicidad de liquidación, validar las reglas comerciales,
crear o reconocer el pago canónico y aplicar cualquier consecuencia autorizada sobre el pedido. La
respuesta «ya aplicada» debe ser recuperable después de un timeout. Un evento interno puede anunciar
el resultado, pero `InProcessEventBus` no es memoria durable ni garantía de entrega.

### 5.5 `same-origin` / `Core.Contracts`

**NO LOCALIZADO** como filtro o contrato vigente en `backend/Sillar.Core.Contracts`.

Evidencia concreta localizada:

- `CsrfEndpointFilter` exige `X-CSRF-Token` en escrituras autenticadas con cookie y exceptúa
  `GET/HEAD/OPTIONS`;
- `frontend/src/shared/http/client.ts` y `connection.ts` usan `credentials: 'same-origin'` al invocar
  `fetch`.

Eso no prueba un filtro backend `same-origin`. La futura iniciación de checkout con sesión deberá
usar la protección CSRF aplicable; un webhook de proveedor no se autentica con cookie administrativa
y requiere autenticidad propia del proveedor. La costura exacta continúa sin ratificar.

## 6. Máquina de estados técnica M11 — propuesta no ratificada

Los estados M11 no se añaden a `OrderStatus`. Para evitar que una sola etiqueta mezcle verdades
distintas, se proponen dos ejes técnicos coordinados.

### 6.1 Eje de intento/proveedor

| Estado conceptual | Significado | Salidas permitidas |
|---|---|---|
| `creado` | Intención local durable; todavía sin resultado confiable del proveedor | `pendiente`, `incierto`, `fallido` |
| `pendiente` | Proveedor aceptó la operación pero no hay liquidación confirmada | `confirmado_proveedor`, `fallido`, `cancelado`, `incierto` |
| `confirmado_proveedor` | Evidencia de liquidación verificada; todavía no implica pago aplicado en M03 | Aplicación/reintento en el eje de entrega |
| `fallido` | Proveedor afirma fallo definitivo y no hay evidencia de liquidación | Reintento de negocio solo según política; un éxito tardío verificado no se descarta |
| `cancelado` | Operación cancelada sin evidencia de liquidación | Igual cautela frente a evento monetario tardío |
| `incierto` | Timeout, respuesta ambigua o proveedor caído | Reconciliar; no crear otro cobro automáticamente |

Los nombres son ilustrativos. Un estado visible «pagado» solo procede de la verdad canónica de M03,
no de `confirmado_proveedor`.

### 6.2 Eje de recepción/aplicación

| Estado conceptual | Regla |
|---|---|
| `recibido` | Recepción duradera con hash y metadatos mínimos; el body exacto solo existe durante la verificación |
| `verificacion_fallida` | Terminal para efectos comerciales; registra el fallo de autenticidad sin aplicar dinero |
| `verificado_pendiente` | Evento auténtico reclamado por `(provider, provider_event_id)` |
| `cuarentena` | Auténtico pero no correlacionable o inconsistente; no se pierde ni se aplica a ciegas |
| `pendiente_aplicacion_m03` | Liquidación derivada y validada, todavía sin confirmación durable de M03 |
| `reintento` | Fallo transitorio; conserva contador/plan de reintento sin duplicar la liquidación |
| `aplicado` | M03 respondió aplicada o ya aplicada para la misma liquidación |
| `revision_requerida` | Inconsistencia permanente o decisión comercial pendiente |

La forma exacta de registrar webhooks no auténticos, sin conservar body crudo ni material secreto,
debe cerrarse con seguridad antes del diseño físico.

### 6.3 Eventos especiales

- **Duplicado exacto:** no crea otra recepción ni otra aplicación; devuelve/recupera el resultado de
  la primera recepción sin revelar detalles.
- **Fuera de orden:** un evento anterior no degrada una liquidación ya verificada. Si dos estados del
  proveedor se contradicen, se consulta/reconcilia con la autoridad del proveedor y se conserva la
  anomalía.
- **Tardío:** no se descarta por edad. Si prueba dinero recibido, entra en la política de pago tardío;
  esa política para origen `provider` sigue pendiente de JP.
- **Fallo después de éxito:** no borra un pago M03. Puede representar reverso/contracargo, cuyo modelo
  está abierto, y por ahora exige revisión.
- **No firmado o firma inválida:** cero efecto comercial, incluso si el payload parece correcto.
- **Timeout del proveedor:** estado incierto y reconciliación antes de permitir otra autorización.
- **Caída M03:** recepción y liquidación permanecen pendientes; reintento con la misma identidad.
- **Caída M11 tras aplicar en M03:** al reiniciar, M03 responde «ya aplicada» y M11 converge a aplicado.
- **M11 desactivado:** sus rutas/capacidad no deben estar disponibles; M03 conserva Yape y efectivo.

## 7. Idempotencia, concurrencia y replicación

### 7.1 Matriz de carreras

| Escenario | Barrera obligatoria | Resultado esperado |
|---|---|---|
| Misma notificación repetida secuencialmente | UNIQUE local `(provider, provider_event_id)` | Una recepción efectiva y un único intento de consecuencia |
| Misma notificación en workers concurrentes | Inserción/reclamo atómico del inbox | Un ganador; los demás leen el resultado ya reclamado |
| Eventos distintos para una misma liquidación | UNIQUE M03 sobre la identidad estable `external_settlement_id` | Un solo pago comercial |
| Dos workers aplican la misma liquidación | Operación transaccional M03 | Uno aplica; el otro obtiene «ya aplicada», no conflicto ambiguo |
| M03 confirma y M11 cae antes de marcar inbox | Barrera M03 + reintento con igual identidad | M03 no mueve saldo dos veces; M11 termina aplicado |
| Timeout sin saber si M03 confirmó | Consulta/reintento idempotente, nunca nueva identidad | Se recupera el mismo resultado |
| Doble pulsación / dos pestañas | Idempotencia de creación y política de intentos | No debe causar dos autorizaciones; la política exacta espera decisión de parciales/múltiples |

Las garantías deben estar en restricciones/transacciones durables, no en un `if` previo ni en memoria
del proceso. Antes de primera migración se mantienen obligatorias las pruebas de ambas unicidades y la
prueba de caída fijadas por A-M11-04.

### 7.2 Alcance por nodo y ADR-018

- La unicidad `(provider, provider_event_id)` vive en la operación técnica M11, clasificada como
  **local/no replicada**. Por tanto, la decisión vigente solo garantiza unicidad dentro de ese almacén
  técnico/nodo. Una topología futura con recepción activa en más de un nodo necesitaría una decisión
  explícita de enrutamiento o coordinación; no se presume unicidad global.
- El pago M03 es replicado y usa PK UUID v7, `origin_node` y versión conforme ADR-016.
- Una fila replicada de M03 **no puede tener FK** hacia una intención o inbox local M11: violaría
  ADR-018. Debe conservar evidencia externa autosuficiente/snapshot, no un puntero local.
- La dirección inversa —fila local M11 que referencia identidad replicada M03— no viola por sí sola
  ADR-018, pero la existencia de una FK física no está ratificada y debe respetar la dependencia dura.
- El trío humano de M03 continúa como snapshot sin FK a `core.admin_users`; JP-02 lo vuelve nulo en
  conjunto para `provider`, no lo sustituye por otro usuario.

## 8. Amenazas, seguridad y privacidad

| Amenaza | Control documental requerido | Riesgo residual / pendiente |
|---|---|---|
| Webhook falsificado | Verificación criptográfica sobre bytes exactos antes de efectos; adaptador y secreto ligados al proveedor/ruta | Algoritmo, ventana y rotación dependen del proveedor no elegido |
| Replay de webhook válido | Validar antigüedad/nonce si el protocolo lo ofrece y deduplicar por evento y liquidación | Firma válida por sí sola no evita replay |
| Spoofing o confusión de proveedor | No confiar en parámetro de ruta; seleccionar configuración autorizada y contrastar proveedor, comercio, referencia y operación | Namespacing exacto de liquidación pendiente |
| Manipulación del importe cliente | Ignorar importe/moneda/cliente enviados por navegador; obtenerlos de M03 y contrastarlos con evidencia del proveedor | Autoridad de moneda abierta |
| Manipulación de payload auténtico pero inconsistente | Comparar importe, moneda, referencia mercantil, pedido y estado antes de aplicar | Política de diferencias y parciales abierta |
| Secreto expuesto | Secretos fuera de PostgreSQL/repo/JS/logs; rotación, revocación y separación por entorno | Operación concreta se define con proveedor |
| Proveedor comprometido | Validaciones internas, límites, alertas, conciliación y capacidad de detener el adaptador sin detener M03 | Una firma válida ya no prueba corrección si la autoridad firmante fue comprometida |
| Logs y PII | Minimización; códigos seguros; sanitizar texto del proveedor; no PAN, CVV, token, secreto, firma ni body crudo en logs | Retención y acceso a evidencia aún deben definirse |
| PAN/CVV en SILLAR | Prohibición persistente; preferir captura alojada/tokenizada por proveedor cuando se elija | La selección de modalidad/proveedor requiere revisión específica |
| Evento no firmado | Registrar resultado de verificación y hash/metadatos mínimos, sin consecuencia comercial | Forma exacta de evidencia de rechazo pendiente |

### 8.1 Consecuencia forense de no conservar el body crudo

El SHA-256 permite comparar un cuerpo candidato futuro con lo recibido, pero **no reconstruye el
cuerpo ni prueba por sí solo su contenido**. Sin los bytes originales SILLAR no puede:

- volver a ejecutar la verificación con otra implementación;
- demostrar qué campo no normalizado traía el proveedor;
- corregir retrospectivamente una normalización defectuosa;
- reverificar la firma exacta tras rotación o disputa.

La evidencia disponible queda limitada al hash, metadatos normalizados, proveedor/evento/liquidación,
instante, resultado de verificación y resultado de aplicación. Como propuesta —no ratificada— conviene
registrar también versión del verificador y referencia no secreta de la clave usada, si el proveedor lo
permite. Nunca se compensa la pérdida filtrando body, firma o secretos a logs.

## 9. Matriz de aceptación y error documental

No fija rutas ni códigos HTTP definitivos.

### 9.1 Casos legales

| Caso | Resultado exigido |
|---|---|
| Pedido propio pagable | M03 entrega importe autoritativo; M11 crea/recupera intención sin aceptar importe cliente |
| Webhook válido y liquidación coherente | Una aplicación M03 con `source=provider`; trío humano nulo |
| Webhook válido duplicado | Mismo resultado, cero pago adicional |
| Dos eventos, una liquidación | Un único pago M03 |
| Pago manual Yape/efectivo | Continúa como `source=staff` con trío completo, aun sin M11 |
| Caída después de aplicar y antes de marcar inbox | Reintento converge a «ya aplicada» |
| Proveedor temporalmente caído | Intento incierto/reintentable; M03 y medios manuales continúan |
| M03 temporalmente caído | Liquidación pendiente durable; no se pierde ni se declara aplicada |
| Evento tardío auténtico | Se conserva y sigue política explícita; no se descarta silenciosamente |
| Orden invertido | No degrada un estado monetario confirmado; contradicción se reconcilia |
| M11 desactivado | Sin superficie M11; M03 conserva Yape/efectivo y sus contratos propios |

### 9.2 Casos ilegales o de revisión

| Caso | Resultado exigido |
|---|---|
| Pedido ajeno o inexistente | Sin filtración de existencia hacia cliente; no crea intención |
| Importe/moneda manipulados por navegador | Se ignoran como autoridad; discrepancia no aplica pago |
| Firma ausente, inválida o proveedor desconocido | Cero efecto comercial; evidencia mínima del rechazo |
| Evento auténtico con pedido/referencia/importe/moneda incoherente | Cuarentena/revisión, nunca aplicación ciega |
| `source=staff` con trío incompleto | Rechazo por invariant/constraint |
| `source=provider` con algún dato humano | Rechazo; no se tolera atribución sintética |
| Tercer valor de `source` | Rechazo |
| Evento de fallo posterior a pago aplicado | No borra el pago; revisión hasta decidir reversos/contracargos |
| Pedido vencido/cancelado/entregado con dinero recibido | No perder evento ni dinero; resolver según política JP pendiente para proveedor |
| Reintento con misma liquidación y datos distintos | Inconsistencia visible; no actualizar silenciosamente el pago previo |

### 9.3 Validaciones futuras antes de una primera migración

- casos legales e ilegales de las dos ramas de `source`, incluidas combinaciones parciales;
- duplicados secuenciales y concurrentes en ambas barreras;
- timeout y caída en la ventana M03-aplicado / inbox-no-marcado;
- evento tardío, invertido, no firmado y firma válida con datos incoherentes;
- proveedor y M03 caídos por separado;
- desactivación de M11 sin regresión de Yape/efectivo;
- prueba PostgreSQL de restricciones y atomicidad cuando exista autorización;
- acreditación ADR-016/018 sin FK desde pago replicado hacia operación local M11;
- ausencia de PAN, CVV, secretos, firmas y body crudo en persistencia, respuestas y logs.

## 10. Decisiones pendientes de JP

No se adopta ningún default.

| Decisión | Estado reconciliado | Qué bloquea |
|---|---|---|
| Medios online v1 | **Parcialmente cerrada:** tarjeta pertenece a M11; cualquier medio adicional y su representación exacta siguen abiertos | Método M03, UX y adaptador |
| Pago de proveedor tras vencimiento | **Abierta para `provider`.** M03 ya permite registrar pago tardío humano, pero eso no decide aplicación automática externa, revisión o devolución | Resultado de aplicación y estados operativos |
| Pagos parciales o múltiples | **Abierta** | Saldo, intento activo, unicidad y contrato M03 |
| Reembolsos y contracargos | **Abierta** | Modelo de reversos, estados, evidencia y conciliación |
| Comisión del proveedor | **Abierta** | Si afecta importe comercial, asiento separado o solo conciliación; M11 no debe alterar total M03 por defecto |
| Moneda operativa y autoridad | **Abierta** | Snapshot pagable, validación de webhook y liquidación |
| Proveedor, SDK, modalidad y sandbox | **Abierta** | Firma, replay, tokenización, rutas, límites, número de proveedores y adaptador real |
| B2B/M07 u otros pedidos fuera de M03 v1 | **Fuera del alcance actual**, no dependencia M11 v1; cualquier ampliación requiere decisión nueva | Contratos y fronteras de un alcance posterior |

También requieren cierre técnico por el dueño M03/Integración, sin ser defaults de este documento:

- forma exacta y namespacing de `external_settlement_id`;
- contrato/DTO y resultados de aplicación;
- restricciones y estrategia compatible con datos existentes;
- manejo de pedido cancelado o entregado cuando ya llegó dinero;
- retención, acceso y versión de evidencia normalizada;
- política de reconciliación por consulta cuando no existe `provider_event_id` de webhook.

## 11. Contratos propuestos no ratificados — resumen

- lectura M03 de una instantánea pagable y autorizada;
- creación/recuperación idempotente de intención técnica M11;
- verificación provider-neutral de eventos y normalización de evidencia;
- deduplicación local por `(provider, provider_event_id)`;
- derivación estable y namespaced de liquidación externa;
- aplicación transaccional/idempotente M03 con resultado recuperable;
- consulta/reconciliación de proveedor sin fabricar IDs de webhook;
- estados técnicos separados de `OrderStatus`;
- evidencia forense normalizada adicional —versión de verificador/referencia de clave— si el proveedor
  lo admite.

No se ratifican `IOnlinePaymentOrderPort`, `IPaymentGateway`, `IWebhookVerifier`, rutas HTTP, DTOs,
tablas o nombres de columnas de la SPEC histórica.

## 12. Bloqueos y siguiente hito

### Bloqueos para implementación, no para esta consolidación

- JP-02 todavía no está integrada en los documentos canónicos.
- M03 no representa todavía origen `provider` ni ofrece operación pública idempotente.
- Las decisiones de producto de la §10 continúan abiertas.
- Proveedor/SDK y sus reglas criptográficas no están seleccionados.
- No existe autorización para DDL, código, migraciones, frontend, navegación, proveedor real o pruebas
  de infraestructura.
- El supuesto filtro `same-origin` de `Core.Contracts` está **NO LOCALIZADO**.

### Siguiente hito propuesto

Auditoría de esta consolidación por Chat 3/colíder y decisión de JP sobre las preguntas abiertas.
Después, solo con autorización expresa, preparar una adenda documental que integre JP-02 en
`SPEC.md` y `DECISIONES-PREVIAS-M11.md`, todavía sin implementar ni autorizar la primera migración.
