# Expediente de preparación para integración operativa M03 ↔ M11

- **Versión de propuesta:** 0.1
- **Fecha:** 19/10/2026 — America/Lima
- **Base M11 revisada:** `c47ca28a387853cc4e7bfd81fd41ecfa9a7a37c1`
- **Estado:** propuesta técnica no ratificada
- **Autoridad:** `SPEC.md` y `DECISIONES-PREVIAS-M11.md` continúan siendo canónicos
- **Efectos:** ninguno; este expediente no autoriza código operativo, contratos públicos,
  persistencia, migraciones, proveedor, SDK, cobros ni QA

## 0. Resultado ejecutivo

El repositorio permite preparar la integración, pero todavía no contiene una costura M03 ↔ M11:

- `Sillar.Modules.Sales.Contracts` no ofrece consulta pagable ni aplicación idempotente;
- `sales.order_payments` sólo representa pagos humanos y exige siempre la atribución completa;
- M03 no tiene servicio de aplicación de pagos, aunque ya tiene entidad, política de transición y
  transacción local;
- `PagoConfirmado` sólo describe el caso manual y el bus interno no es durable;
- el proyecto Payments es un núcleo puro: no es todavía un `IModule`, no está en la solución ni en
  el host y no referencia contratos de M03, CORE o persistencia;
- P1–P16 sí aportan el núcleo reutilizable para identidades, replay, conflictos, `HOLD`, evidencia,
  trazabilidad e integridad, pero no contienen importe, moneda, pedido, inbox ni autenticación.

La propuesta es una entrega al menos una vez entre M11 y M03, con dos barreras durables:
`(provider, provider_event_id)` en M11 y `external_settlement_id` en M03. M03 aplica cada liquidación
en una transacción propia y devuelve el mismo resultado en un reintento. No se propone una
transacción distribuida ni se usa el bus interno como garantía.

## 1. Alcance y lenguaje de esta propuesta

Las palabras **debe** y **no debe** reflejan decisiones ya ratificadas o condiciones técnicas
necesarias para cumplirlas. Las palabras **propuesto** y **recomendado** no son decisiones de JP.

Este documento:

1. inspecciona M03, CORE, Shared y M11 en el estado real del repositorio;
2. propone una versión 1 del contrato sin implementarla;
3. diseña lógicamente el inbox sin fijar todavía una migración;
4. resuelve técnicamente el escenario de caída mediante reintento e idempotencia;
5. prepara la frontera provider-neutral;
6. separa decisiones de producto, arquitectura y seguridad;
7. deja una secuencia de implementación ejecutable cuando exista autorización.

## 2. Evidencia del repositorio real

### 2.1 M03 — contratos públicos

| Hallazgo | Evidencia | Consecuencia |
|---|---|---|
| La frontera pública existe en un proyecto separado | `backend/Sillar.Modules.Sales.Contracts/Sillar.Modules.Sales.Contracts.csproj` | El contrato M03 ↔ M11 debe vivir allí, no en Domain/Data de M03 |
| `PagoConfirmado` sólo lleva pedido, código e instante y se documenta como manual | `Sales.Contracts/Events/SalesEvents.cs:15-28` | No identifica pago, origen ni liquidación; requiere evolución explícita |
| Los métodos vigentes son `yape` y `efectivo` | `Sales.Contracts/PaymentMethod.cs:30-44` | Tarjeta requiere decisión y migración; origen y medio no deben fundirse |
| No existe contrato para consultar un pedido pagable | barrido de `Sillar.Modules.Sales.Contracts` | M11 no puede obtener importe autoritativo sin ampliar la frontera |
| No existe contrato para aplicar una liquidación | barrido de `Sillar.Modules.Sales.Contracts` | M11 no tiene hoy una llamada legítima hacia el ledger |

### 2.2 M03 — dominio y datos

| Hallazgo | Evidencia | Consecuencia |
|---|---|---|
| `OrderPayment` ya es el hecho comercial replicado | `Sales/Domain/OrderPayment.cs:27-121` | Debe evolucionar; no crear un segundo ledger en M11 |
| El pago actual exige tres datos humanos no nulos | `OrderPayment.cs:52-96`; `OrderPaymentConfiguration.cs:23-36,68-81` | Es incompatible con `source=provider`; requiere discriminante y checks condicionales |
| No existe `source`, proveedor ni liquidación externa | `OrderPayment.cs:27-121` | La atribución externa aún no es representable |
| No existe unicidad de liquidación | `OrderPaymentConfiguration.cs:95-107` | M03 aún no ofrece la segunda barrera de A-M11-04 |
| Importe y total usan `numeric(12,2)` | `OrderPaymentConfiguration.cs:49-52`; `OrderConfiguration.cs:52-55` | El contrato debe mantener precisión y acordar moneda |
| `OrderPayment` es replicado | implementación de `IReplicatedEntity` | La liquidación comercial viaja; el inbox M11 no debe viajar |
| La política `RegistrarPago` exige persona y parámetro `hayMercancia` | `Sales/Domain/OrderTransition.cs:78-126` | No puede reutilizarse sin separar pago humano de pago externo y resolver el caso operativo |
| No hay servicio que cree `OrderPayment` | ningún `OrderPayments.Add` fuera de pruebas | La aplicación idempotente debe ser una operación nueva de M03 |
| M03 ya usa transacciones locales reutilizables | `Sales/Data/TransaccionDeOperacion.cs` | Hay precedente para que M03 sea dueño de su atomicidad |
| M03 puede bloquear filas en PostgreSQL en otros módulos del repositorio | patrón `FOR UPDATE` en ServiceOrders | El contrato puede implementarse con serialización local sin tocar CORE |

Incompatibilidades documentales observadas, no corregidas por restricción de territorio:

- `OrderPayment.Method` todavía comenta «hoy solo yape», aunque el contrato admite efectivo;
- `SalesModule` comenta que `ICurrentAdmin` no entrega el trío completo, pero el contrato actual ya
  expone `DisplayName`, `AdminUserId` y `HomeNode`;
- M03 sigue afirmando que todo pago lo registra una persona, mientras las decisiones posteriores de
  M11 exigen origen externo sin atribución ficticia.

No son razones para modificar M03 desde este worktree. Son puntos de reconciliación obligatorios de
la futura entrega conjunta.

### 2.3 CORE y Shared

| Hallazgo | Evidencia | Uso o límite para M11 |
|---|---|---|
| `ICurrentAdmin` expone el trío humano | `Core.Contracts/ICurrentAdmin.cs` | Reutilizable para pagos manuales de M03; no se usa para inventar autor en M11 |
| `IAuditWriter` escribe auditoría administrativa de CORE | `Core.Contracts/IAuditWriter.cs` | No debe convertirse en inbox ni recibir cada webhook por defecto |
| `NodeIdentity` y sellado de replicación son compartidos | `Shared/Replication` y `Shared.Data/Replication` | Aplican al pago M03 replicado; las filas técnicas M11 quedan locales |
| El bus es en proceso, sin cola, reintentos ni orden garantizado | `Shared/Events/IEventPublisher.cs:15-19`; `InProcessEventBus.cs:10-16` | No puede cerrar el protocolo de pago ni recuperar una caída |
| El bus captura fallos de manejadores y continúa | `InProcessEventBus.cs:33-48` | Un evento no prueba que un consumidor aplicó o registró nada |
| El host sólo conoce módulos referenciados por `Sillar.Api` | `Sillar.Api.csproj:9-25` | Payments deberá agregarse al host cuando sea un módulo real |
| El grafo activa sólo módulos declarados y activos | `ModuleBootstrapper` / `IModule` | M11 debe declarar `core` y `sales` como dependencias duras |
| Existe precedente de secretos en variables de entorno | `SmtpEmailSender` y `SILLAR_SMTP_PASSWORD` | Útil como patrón mínimo; no decide el gestor de secretos del proveedor |

No se identifica una necesidad actual de modificar CORE. La persistencia, el trabajador y los
adaptadores deben vivir en M11; la aplicación comercial, en M03.

### 2.4 Estado real de M11

- `Sillar.Modules.Payments.csproj` no tiene referencias ni paquetes y declara núcleo puro.
- No existe `PaymentsModule`, `PaymentsDbContext`, proyecto Contracts, endpoint, worker o adaptador.
- El proyecto no aparece en `backend/Sillar.sln` ni en `Sillar.Api.csproj`.
- P1–P16 no asocian evidencia con pedido, importe o moneda.
- `VerifiedExternalPaymentEvidence` representa una precondición de firma ya verificada; no ejecuta
  criptografía.
- La canalización integrada falla cerrada, conserva conflictos y sostiene `HOLD` interproveedor.

## 3. Fronteras de autoridad

| Hecho | Autoridad propuesta | Prohibición correlativa |
|---|---|---|
| pedido, código, cliente, total, plazo y estado | M03 | M11 no recalcula ni escribe `sales.*` |
| recepción, firma, replay de evento, intento y reintento | M11 | M03 no interpreta payloads del proveedor |
| pago comercial y transición del pedido | M03 | M11 no declara un pago aplicado por sí mismo |
| secreto y algoritmo del proveedor | adaptador M11 + configuración segura | PostgreSQL no guarda secretos |
| cuerpo exacto durante verificación | memoria transitoria M11 | ninguna tabla o log conserva el body |
| evidencia posterior | M11: hash, resultado y metadatos normalizados | no se promete reverificación futura |
| atribución humana | M03/CORE, trío completo | evento provider tiene los tres campos humanos nulos |
| origen del pago | M03, `staff|provider` | no confundirlo con `yape|efectivo|card` |
| idempotencia de recepción | M11, `(provider, provider_event_id)` | no sustituye la barrera comercial |
| idempotencia comercial | M03, `external_settlement_id` vigente | no sustituye la recepción durable |

## 4. Propuesta contractual M03 ↔ M11

### 4.1 Identidad y versión

- **Nombre conceptual:** External Order Payments Contract.
- **Versión propuesta:** 1.
- **Ubicación futura:** `Sillar.Modules.Sales.Contracts`, bajo una carpeta/namespace de pagos.
- **Dirección de dependencia:** M11 referencia `Sales.Contracts`; M03 nunca referencia Payments.
- **Mecanismo:** llamadas de aplicación dentro del monolito modular, no HTTP interno.
- **Compatibilidad:** tipos V1 inmutables; un cambio semántico crea V2.

Los nombres C# siguientes son marcadores revisables, no código autorizado:

```text
IExternalOrderPaymentsV1
  GetPayableOrderAsync(PayableOrderQueryV1) -> PayableOrderResultV1
  ApplyVerifiedSettlementAsync(ApplyVerifiedSettlementV1) -> ApplySettlementResultV1
```

### 4.2 Consulta pagable

Entrada propuesta:

| Campo conceptual | Razón | Autoridad |
|---|---|---|
| código visible del pedido | hoy es la identidad que conoce el cliente | M03 |
| identidad del cliente autenticado | evita consultar o pagar pedidos ajenos | M04, validada por M03 |
| correlación técnica M11 | trazabilidad, no idempotencia comercial | M11 |

Salida exitosa propuesta:

| Campo | Uso |
|---|---|
| `OrderId` | identidad interna estable para aplicación posterior |
| `OrderCode` | diagnóstico legible y respuestas seguras |
| importe autoritativo | valor congelado de M03 |
| moneda autoritativa | bloqueada hasta decisión JP |
| vencimiento | informar intención; no equivale a reserva |
| clasificación de pagabilidad | M03 decide con estado actual |
| versión opaca de instantánea, si M03 la exige | detectar obsolescencia sin exponer `DbContext` |

Resultados mínimos propuestos: `FoundPayable`, `NotFoundOrNotOwned`, `NotPayable` y
`DecisionRequired`. La respuesta pública puede ocultar diferencias necesarias para evitar
enumeración; el contrato interno sí necesita diagnóstico estable.

La consulta no reserva importe, pedido ni existencias y no autoriza aplicar un pago futuro. M03
revalida todo al recibir la liquidación.

### 4.3 Aplicación de liquidación verificada

Entrada propuesta:

| Campo | Obligación |
|---|---|
| `OrderId` | no vacío; M03 vuelve a cargarlo y bloquearlo |
| `Provider` | normalizado; fuente externa, no medio de pago |
| `ExternalSettlementId` | clave de barrera comercial según A-M11-04 |
| importe liquidado | hecho informado por proveedor; M03 lo coteja con su autoridad |
| moneda liquidada | debe coincidir con la autoridad que JP determine |
| medio online normalizado | separado del origen; lista pendiente de JP |
| instante de confirmación del proveedor | hecho externo, distinto de recepción/aplicación |
| correlación durable M11 | localiza expediente técnico, no es la clave de M03 |

No se propone enviar el cuerpo, secreto, firma ni cabeceras crudas a M03. El `provider_event_id`
permanece como barrera de M11; M03 puede recibirlo sólo como diagnóstico si el contrato ratificado lo
necesita, nunca como sustituto de `external_settlement_id`.

Resultados mínimos propuestos:

| Resultado | Semántica | Mutación |
|---|---|---|
| `Applied` | M03 creó el pago exactamente una vez | sí, atómica dentro de M03 |
| `AlreadyApplied` | la misma liquidación y los mismos hechos ya existen | ninguna; éxito idempotente |
| `SettlementConflict` | la clave existe con pedido, proveedor o hechos incompatibles | ninguna; `HOLD` |
| `OrderNotFound` | no existe el pedido canónico | ninguna |
| `OrderNotPayable` | la política vigente impide aplicar automáticamente | ninguna o expediente manual, según decisión |
| `AmountMismatch` | importe externo no coincide con la política ratificada | ninguna; `HOLD` |
| `CurrencyMismatch` | moneda externa no coincide | ninguna; `HOLD` |
| `DecisionRequired` | caso comercial aún no ratificado | ninguna; `HOLD` |

Un resultado exitoso debe incluir `OrderPaymentId`, `OrderId`, el estado comercial resultante y si
queda pendiente operativo. `AlreadyApplied` devuelve el mismo `OrderPaymentId` que la primera
aplicación.

### 4.4 Invariantes de implementación en M03

1. Adquirir una exclusión transaccional por `external_settlement_id`.
2. Buscar primero un pago existente con esa clave.
3. Si existe y sus hechos coinciden, devolver `AlreadyApplied`.
4. Si existe y no coincide, devolver conflicto sin modificar nada.
5. Bloquear el pedido con `FOR UPDATE` antes de decidir e insertar.
6. Revalidar importe, moneda, estado y reglas comerciales dentro de la transacción.
7. Insertar `OrderPayment`, historia/estado aplicable y pendiente derivable en una sola transacción.
8. Confirmar antes de devolver `Applied`.
9. Mantener un índice único como última barrera, aunque exista bloqueo previo.
10. Nunca publicar un evento antes del commit.

El repositorio ya usa `pg_advisory_xact_lock` para idempotencia y `FOR UPDATE` para decisiones
serializadas. La implementación futura puede seguir esos patrones. El orden de bloqueos debe ser
estable: liquidación primero, pedido después, para evitar interbloqueos entre reintentos.

### 4.5 Evolución de `OrderPayment`

Cambios conceptuales necesarios, no autorizados todavía:

- añadir discriminante cerrado `staff|provider`;
- mantener los tres campos humanos presentes juntos sólo para `staff`;
- exigirlos nulos para `provider`;
- añadir proveedor e identidad de liquidación para `provider`;
- exigir campos externos nulos para `staff`;
- añadir unicidad de `external_settlement_id` conforme a la decisión vigente;
- separar medio de pago del origen;
- conservar `Amount`, `RegisteredAt`, `WasLate`, replicación y FK al pedido;
- migrar filas existentes explícitamente como `staff`, sin inferir personas nuevas.

La forma recomendada es un conjunto de `CHECK` condicionales que haga irrepresentables las mezclas:
ni media atribución humana ni pago provider con una persona ficticia.

### 4.6 Evolución de eventos

`PagoConfirmado` V1 no identifica la fila ni el origen. Cambiarlo silenciosamente incumpliría
A-M11-03. Se propone:

- diseñar un `PagoConfirmadoV2` aditivo con `OrderPaymentId`, pedido, instante y origen
  discriminado;
- emitir el mismo concepto para pagos manuales y externos, no dos universos permanentes;
- migrar consumidores antes de retirar V1;
- usar el evento sólo para consecuencias posteriores al commit.

M11 no debe decidir que M03 aplicó porque observó el evento. El retorno durable del contrato es la
fuente para cerrar el inbox; el bus actual no garantiza entrega.

## 5. Diseño lógico del inbox durable de M11

### 5.1 Objetivos

- registrar la verificación sin conservar cuerpo crudo;
- preservar todas las entregas, incluidas contradicciones;
- deduplicar el evento sin perder evidencia conflictiva;
- clasificar y retener `HOLD` antes de cualquier efecto;
- permitir múltiples trabajadores sin doble cierre;
- recuperar caídas y respuestas perdidas;
- demostrar la relación entre recepción, intento y resultado M03.

### 5.2 Registros lógicos propuestos

Los nombres son descriptivos y no constituyen nombres de tablas aprobados.

#### A. Receipt

Una fila por entrega observada, incluso si repite un evento:

- identificador técnico;
- proveedor obtenido de la configuración/ruta, no del payload;
- instante de recepción;
- SHA-256 de los bytes exactos;
- resultado de verificación, algoritmo y referencia no secreta de clave/verificador;
- identificadores normalizados extraídos, si la verificación permitió confiar en ellos;
- metadatos permitidos por lista blanca;
- resultado de admisión, rechazo o conflicto;
- ausencia estructural de body, PAN, CVV y secretos.

#### B. Inbox event

Una fila canónica por `(provider, provider_event_id)` verificado:

- identidad de evento y liquidación;
- vínculo a la primera y última evidencia relevante;
- pedido/intención correlacionados, cuando existan;
- clasificación actual y razón de `HOLD`;
- estado técnico de procesamiento;
- próximo intento y contador;
- token/generación y vencimiento de lease;
- último resultado M03 y `OrderPaymentId`, si existe;
- instantes de alta, actualización y cierre.

La unicidad no descarta una segunda entrega: Receipt la conserva. Si cambia cuerpo o liquidación, el
evento canónico pasa a conflicto/HOLD y no se sobrescribe con la versión nueva.

#### C. Processing attempt

Registro append-only por intento:

- inbox event;
- número y token de lease;
- inicio/fin;
- clasificación de resultado;
- código contractual devuelto por M03;
- correlación y `OrderPaymentId` cuando proceda;
- diagnóstico acotado y sin payload/secreto.

Permite distinguir «nunca se intentó», «M03 rechazó», «respuesta perdida» y «cerrado después de
replay» sin deducirlo de logs volátiles.

### 5.3 Unicidades y checks

| Barrera | Diseño lógico |
|---|---|
| recepción canónica | único `(provider, provider_event_id)` sólo sobre identidad verificada |
| aplicación comercial | único `external_settlement_id` en M03 según decisión vigente |
| receipt | PK propia; no se elimina una contradicción por `ON CONFLICT` |
| intento | identidad propia + referencia al inbox event |
| atribución | M11 provider no contiene campos humanos |
| evidencia | SHA-256 hexadecimal normalizado y verificación explícita |
| procesamiento | estados y razones cerrados; valor desconocido falla cerrado |

La identidad de un evento no verificado no debe participar como clave confiable. Las recepciones con
firma inválida se registran como evidencia de transporte con límites de retención, pero nunca crean
un candidato aplicable.

### 5.4 Recepción transaccional

1. Resolver proveedor por configuración controlada.
2. Aplicar límite de tamaño mientras se reciben bytes.
3. Verificar firma sobre los bytes exactos y calcular SHA-256.
4. Normalizar el mínimo permitido.
5. Abrir transacción M11.
6. Insertar Receipt.
7. Crear o bloquear el Inbox event de `(provider, provider_event_id)`.
8. Comparar liquidación y hash con todo lo conocido.
9. Ante contradicción, registrar evidencia y fijar `HOLD`.
10. Ante replay exacto, conservar la nueva recepción sin crear otro evento.
11. Ante evento nuevo válido, dejarlo listo sólo para la revisión autorizada.
12. Confirmar transacción y recién entonces responder al proveedor.

Si la transacción falla, se responde conforme al protocolo para provocar reentrega. Un `2xx` antes
de persistir perdería el evento y queda prohibido.

### 5.5 Concurrencia del trabajador

Patrón recomendado:

1. seleccionar trabajo vencido/listo en una transacción corta con bloqueo y `SKIP LOCKED`;
2. asignar lease con token aleatorio o generación monotónica y `lease_until`;
3. confirmar la transacción;
4. invocar M03 fuera de la transacción M11;
5. abrir una segunda transacción;
6. cerrar sólo si el token/generación todavía coincide;
7. si el trabajador murió, el lease expira y otro reintenta.

No debe mantenerse una transacción PostgreSQL abierta durante una llamada potencialmente lenta.
El token de fencing impide que un trabajador antiguo cierre el trabajo después de que otro tomó el
lease.

### 5.6 Reintentos

- `Applied` y `AlreadyApplied` cierran como éxito.
- conflicto, importe/moneda incompatibles o decisión pendiente quedan en `HOLD` terminal hasta
  intervención autorizada.
- fallos transitorios de base/proceso se reprograman con backoff y jitter; los valores concretos son
  operación/configuración, no decisión de dominio.
- validación, firma inválida o payload imposible nunca se reintentan hacia M03.
- una reactivación manual futura deja auditoría y no altera la evidencia original.
- el número máximo y política de alerta se definen con Infra antes de operar.

No se promete «exactly once». La propiedad alcanzable es entrega al menos una vez más aplicación
idempotente.

## 6. Escenario crítico: M03 aplicó y M11 no alcanzó a cerrar

### 6.1 Secuencia

```text
M11 inbox             trabajador              M03
   | claim + lease         |                    |
   |---------------------->|                    |
   |                       | Apply(settlement)  |
   |                       |------------------->|
   |                       |                    | lock settlement
   |                       |                    | lock order
   |                       |                    | insert payment
   |                       |                    | update/history
   |                       |                    | COMMIT
   |                       |<-------------------| Applied(payment_id)
   |                       X  caída             |
   | lease expira          |                    |
   | nuevo claim           |                    |
   |---------------------->| Apply(settlement)  |
   |                       |------------------->|
   |                       |                    | encuentra misma clave
   |                       |<-------------------| AlreadyApplied(same payment_id)
   | mark applied          |                    |
```

### 6.2 Por qué no duplica

M11 puede repetir la llamada porque M03 serializa por `external_settlement_id` y persiste esa clave
junto al pago comercial. El segundo intento no inserta ni vuelve a mover el estado: compara los
hechos inmutables y devuelve la fila existente.

La barrera requiere simultáneamente:

1. operación M03 transaccional;
2. exclusión concurrente por liquidación;
3. índice único durable;
4. respuesta idempotente con el mismo `OrderPaymentId`;
5. M11 tratando `AlreadyApplied` como éxito;
6. prueba que interrumpa exactamente entre el commit M03 y el cierre M11.

El bus interno no interviene en esta garantía. Una publicación perdida no puede causar un segundo
pago ni impedir que M11 cierre con la respuesta del contrato.

### 6.3 Colisiones incompatibles

Si la misma liquidación reaparece con otro pedido, proveedor, importe o moneda, M03 no devuelve
`AlreadyApplied`: devuelve conflicto y no muta. M11 preserva ambas evidencias y mantiene `HOLD`.

## 7. Arquitectura provider-neutral

### 7.1 Capas

1. **Transporte:** límites, cabeceras necesarias, respuesta al proveedor.
2. **Verificador específico:** firma, timestamp/nonce y selección de clave.
3. **Normalizador específico:** convierte payload verificado a un evento provider-neutral.
4. **Recepción durable M11:** Receipt + Inbox event.
5. **Núcleo P1–P16:** replay, evidencia, conflictos, riesgo y `HOLD`.
6. **Trabajador:** aplica sólo candidatos autorizados mediante el contrato M03 V1.

El dominio no referencia SDK. El adaptador puede usar uno después de su revisión de seguridad.

### 7.2 Verificación criptográfica

- proveedor determinado por endpoint/configuración, no por un campo autodeclarado;
- límite de tamaño antes de retener todo el body en memoria;
- firma calculada sobre bytes exactos, antes de normalizar;
- comparación constante o primitiva oficial del SDK;
- validación de timestamp/nonce si el protocolo lo ofrece;
- tolerancia temporal configurable y observable, no codificada como decisión universal;
- soporte de rotación con identificador no secreto de clave;
- body liberado después de hash, verificación y normalización;
- ningún log incluye body, firma completa, secreto, PAN o CVV.

`VerifiedExternalPaymentEvidence.FromVerifiedWebhook` sólo debe ser invocado por esta frontera
confiable. No debe enlazarse directamente al modelo HTTP.

### 7.3 Secretos

- fuera de PostgreSQL;
- inyectados desde entorno o gestor de secretos aprobado;
- nombres de configuración separados por proveedor y ambiente;
- rotación con solapamiento controlado;
- nunca devueltos por endpoints, auditoría o excepciones;
- pruebas futuras que busquen el secreto en logs, respuestas y filas.

El patrón SMTP del repositorio demuestra variables de entorno, pero no decide por sí solo si un
proveedor requiere un gestor dedicado.

### 7.4 Tres niveles de replay

| Nivel | Barrera |
|---|---|
| transporte | timestamp/nonce/firma del proveedor, cuando exista |
| evento M11 | `(provider, provider_event_id)` + comparación de cuerpo/liquidación |
| pago M03 | `external_settlement_id` + comparación de hechos comerciales |

Ninguna barrera sustituye a otra.

### 7.5 Respuesta al proveedor

El código HTTP y ventana de respuesta son específicos del proveedor. La regla común es responder
éxito sólo después de la recepción durable. La aplicación en M03 puede ser asíncrona; no es necesario
mantener abierta la petición del webhook hasta terminar el ledger.

## 8. Reutilización y brechas P1–P16

| Componente actual | Reutilización | Adaptación/falta |
|---|---|---|
| `PaymentSource` | invariante interno de origen | no puede ser tipo del contrato M03 porque invertiría dependencias |
| `StaffPaymentAttribution` | prueba la forma completa | M11 no debe administrar pagos staff; M03 ya tiene `StaffAttribution` |
| `VerifiedExternalPaymentEvidence` | valor de salida del verificador | faltan pedido, importe, moneda, tipo/estado del evento y correlación durable |
| `PaymentIdempotencyKeys` | identidades de evento/liquidación | no aporta unicidad persistente |
| clasificadores P2/P3 | detectar replay y contradicción | deben alimentarse desde evidencia durable, no sólo listas en memoria |
| intake P4 | candidato único por grupo | debe ejecutarse después de autenticación y bajo expediente durable |
| evidencia P5 | conserva declaraciones y contradicciones | la atestación real necesita adaptador confiable |
| riesgo P6 y cola P7 | `HOLD` y separación de carriles | el worker sólo consume el carril durable autorizado |
| auditoría/huellas P8+ | expediente reproducible | hash de contenido no equivale a firma ni auditoría persistente |
| inventario/conciliación | comparar lote e historia | necesita repositorio M11 que proyecte la historia completa |
| pipeline integral | decisión pura autoconsistente | no sustituye transacción, lease ni contrato M03 |
| casebook/comparison | revisión y diagnóstico | necesita retención y consultas operativas autorizadas |

Brechas completas:

- asociación segura pedido/cliente;
- importe y moneda;
- intentos de pago e interacción de checkout;
- proyecto/contratos M11 si una frontera pública llega a ser necesaria;
- módulo, DbContext, entidades y configuraciones;
- receptor HTTP y verificador;
- worker/lease/retry;
- llamada contractual a M03;
- observabilidad, retención y herramientas operativas;
- pruebas unitarias, integración, PostgreSQL y fallo inyectado;
- incorporación a solución, host, grafo, instalación y activación.

## 9. Decisiones mínimas para JP y autoridades técnicas

### 9.1 Producto — JP

| Decisión | Alternativas | Recomendación técnica no ratificada | Consecuencia |
|---|---|---|---|
| medios online v1 | tarjeta; tarjeta + otros | **tarjeta únicamente** | reduce estados y proveedor inicial; M03 añade un medio |
| moneda | PEN fija implícita; moneda explícita por pedido | **PEN v1 pero explícita en contrato/dato** | evita que un decimal sin unidad se vuelva deuda futura |
| pago parcial/múltiple online | exacto único; parciales; múltiples | **una liquidación por el total en v1** | simplifica cotejo e idempotencia; manual queda separado |
| pago después de vencimiento | rechazar; aplicar y reactivar; aplicar y dejar pendiente | **registrar el dinero una vez y dejar pendiente operativo hasta política autorizada** | evita negar fondos recibidos sin inventar mercancía |
| pedido cancelado/entregado | aplicar; rechazar; HOLD | **HOLD/manual** | las políticas actuales de M03 no autorizan cobro automático allí |
| reembolsos/contracargos | fuera de v1; automáticos; sólo evidencia/HOLD | **hechos separados y HOLD hasta decisión** | no mutar ni borrar el pago original |
| comisión | netear el pago; registrar bruto | **M03 registra bruto pagado por cliente** | la comisión no reduce la deuda saldada; contabilidad futura separada |

### 9.2 Arquitectura / líder técnico

| Decisión | Alternativas | Recomendación técnica no ratificada |
|---|---|---|
| alcance de `external_settlement_id` | global; por proveedor | **un solo proveedor en v1 y unicidad global vigente; segundo proveedor reabre la decisión** |
| nodo escritor de pagos online | cualquier nodo; nodo designado; coordinador central | **un nodo designado en v1** |
| versión del evento | mutar V1; V2 aditivo | **V2 aditivo y migración explícita** |
| cierre del inbox | evento; retorno de contrato M03 | **retorno idempotente del contrato** |
| concurrencia M03 | sólo índice; lock + índice | **lock transaccional + índice único** |
| dependencia M04 | nunca; sólo inicio cliente | **dura cuando exista inicio autenticado; webhook no depende de sesión** |

### 9.3 Por qué hace falta decidir el nodo escritor

`OrderPayment` se replica, pero un índice único sólo protege una base local. Dos instalaciones
desconectadas podrían aplicar la misma liquidación antes de replicar. La idempotencia global requiere
una de estas autoridades:

1. un único nodo recibe/procesa pagos online;
2. coordinación central disponible;
3. partición de propiedad demostrable por proveedor/liquidación.

Para v1 se recomienda un nodo designado y callbacks dirigidos allí. Sin esa decisión, la garantía
«una liquidación, un pago» no puede demostrarse en despliegue multinodo.

## 10. Secuencia futura de implementación

Nada de esta sección está autorizado por el expediente.

### Etapa 0 — ratificación documental

**Dependencias:** decisiones de §9.

**Archivos previstos:** SPEC/decisiones M11 y M03, ADR si se decide nodo escritor.

**Aceptación:** origen/medio separados; moneda, vencimiento, multiplicidad y alcance de liquidación
sin ambigüedad; autorización explícita de contrato.

### Etapa 1 — contrato M03 V1

**Dependencias:** Etapa 0.

**Archivos previstos:** nuevos tipos en `Sillar.Modules.Sales.Contracts/Payments/`; pruebas de
contrato en Sales.Tests.

**Aceptación:** proyecto Contracts sin EF; tipos versionados; resultados cerrados; M11 compila contra
la frontera sin conocer Domain/Data de Sales.

### Etapa 2 — aplicación idempotente M03

**Dependencias:** contrato V1 y autorización de migración M03.

**Archivos previstos:** `OrderPayment`, configuración, servicio de aplicación, registro DI,
migración y pruebas M03.

**Aceptación:** source condicional; atribuciones correctas; `external_settlement_id` único; lock +
índice; replay devuelve misma fila; conflicto no muta; pago/estado/historia atómicos.

### Etapa 3 — módulo y persistencia M11

**Dependencias:** autorización de primera migración y diseño físico revisado.

**Archivos previstos:** `PaymentsModule`, Data/Domain/Application, DbContext/factory/configuraciones,
migración; referencias Shared/Core.Contracts/Sales.Contracts/Shared.Data; solución y host.

**Aceptación:** schema `payments`; técnico local/no replicado; recepción/inbox/attempts; unicidad de
evento; body/secreto ausentes; módulo declara `core` y `sales`, no `inventory`.

### Etapa 4 — receptor y adaptador del proveedor

**Dependencias:** proveedor elegido, revisión de seguridad y secretos.

**Archivos previstos:** abstracción provider-neutral, adaptador específico, configuración y endpoint
webhook.

**Aceptación:** firma real; límites; hash exacto; rotación; recepción durable antes de éxito;
redirect incapaz de confirmar; corpus de payloads autorizado sin persistir body.

### Etapa 5 — trabajador y recuperación

**Dependencias:** Etapas 2–4.

**Archivos previstos:** claim/lease, worker, política de retry, adaptador al contrato M03 y
observabilidad.

**Aceptación:** dos workers no aplican dos veces; fencing; lease recuperable; `Applied` y
`AlreadyApplied` cierran; conflicto mantiene `HOLD`; no hay transacción abierta durante llamada.

### Etapa 6 — evento y consumidores

**Dependencias:** decisión de versionado.

**Archivos previstos:** evento V2 en Sales.Contracts, publicación posterior al commit y migración de
consumidores.

**Aceptación:** un concepto canónico; origen válido; fallar un manejador no altera el ledger ni el
inbox.

### Etapa 7 — verificación obligatoria previa a migración/operación

**Dependencias:** implementación completa y autorización QA.

**Verificaciones:**

1. mismo evento/mismo body concurrente → una recepción canónica;
2. mismo evento/body distinto → evidencia preservada + `HOLD`;
3. mismo evento/liquidación distinta → `HOLD`;
4. eventos distintos/misma liquidación → un `OrderPayment`;
5. caída después del commit M03 y antes del cierre M11 → retry + misma fila;
6. respuesta M03 perdida → mismo resultado;
7. dos workers → un cierre válido;
8. colisión interproveedor → `HOLD`;
9. importe/moneda incompatibles → cero mutaciones;
10. firma inválida → cero candidato;
11. raw body, PAN, CVV y secretos ausentes de DB/logs/respuestas;
12. M11 inactivo → M03 mantiene Yape y efectivo;
13. M09 ausente → M11 arranca si sus dependencias declaradas están activas;
14. multinodo conforme a la topología ratificada.

Sin la prueba 5 no se acredita idempotencia.

## 11. Matriz de archivos futuros

| Territorio | Archivo/carpeta previsto | Acción futura |
|---|---|---|
| M03 Contracts | `Sillar.Modules.Sales.Contracts/Payments/` | consulta y aplicación V1 |
| M03 Domain | `Domain/OrderPayment.cs` | source y liquidación externa |
| M03 Data | `OrderPaymentConfiguration.cs` | checks e índice único |
| M03 Application | nueva operación de pagos externos | lock, aplicación y resultado idempotente |
| M03 Events | `SalesEvents.cs` o archivo V2 | evolución explícita |
| M11 module | `PaymentsModule.cs` | manifiesto, DI y endpoints autorizados |
| M11 Data | `Data/` | receipts, inbox, attempts y migración futura |
| M11 Providers | `Providers/` | frontera y adaptador específico |
| M11 Application | `Application/` | recepción, claim, worker y retry |
| Host | `Sillar.Api.csproj` | referencia única del módulo |
| Solution | `Sillar.sln` | proyectos M11 y pruebas |

CORE no aparece como archivo a modificar porque sus contratos actuales bastan para módulo,
configuración, identidad de nodo, reloj compartido y auditoría administrativa. Cualquier cambio allí
debe justificarse con un caso que este análisis no encontró.

## 12. Riesgos residuales

- un proveedor puede no ofrecer identificador de liquidación con la semántica esperada;
- la no conservación del body impide reverificar una disputa;
- un hash prueba igualdad de bytes, no autenticidad por sí solo;
- una firma válida no prueba que el importe/pedido sea comercialmente aplicable;
- `external_settlement_id` global puede no ser portable a un segundo proveedor;
- replicación sin nodo escritor único puede duplicar pagos entre instalaciones;
- un evento en proceso no ofrece entrega durable;
- reembolsos y contracargos no caben correctamente como mutación del pago original;
- la política actual de transición M03 depende de `hayMercancia`, dato que M11 no posee ni debe
  obtener de M09;
- importes sin moneda explícita son ambiguos aunque hoy todos parezcan PEN.

## 13. Defectos M11 encontrados durante esta misión

No se encontró un defecto nuevo y demostrable en P1–P16 que justificara modificar código. Las
ausencias detectadas son capacidades operativas deliberadamente no autorizadas, no fallos del núcleo
puro. Por ello esta misión no añade clases ni crea un commit correctivo de código.

## 14. Condición de desbloqueo mínima

Para iniciar trabajo de contrato sin adelantar persistencia bastan:

1. decisión JP sobre moneda, importe completo/parcial y pago vencido;
2. ratificación técnica del alcance de `external_settlement_id` y nodo escritor;
3. autorización para modificar `Sillar.Modules.Sales.Contracts` y crear pruebas contractuales;
4. decisión de versionado de `PagoConfirmado`.

Para iniciar persistencia se añade autorización expresa de migraciones y diseño físico. Para iniciar
proveedor se añade elección de proveedor, revisión de seguridad, secretos y corpus de firma. Hasta
entonces, mantener `HOLD` y el núcleo puro es el único comportamiento correcto.
