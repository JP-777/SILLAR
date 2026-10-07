# Entradas de ledger que produce el Paso 1 documental de M06

- **Creación:** 7 de octubre de 2026 · America/Lima
- **Última verificación:** 7 de octubre de 2026 · America/Lima
- **Rama:** `m06-tracking-pre-spec`, desde `main d26f28a0439a9ac72dbedcc097dd8731b37a27c9`

## Por qué están aquí y no en el archivo de siempre

El ledger es `docs/integracion/REGISTRO-PARALELIZACION.md` y **existe solo en la rama
`m05b-service-orders-spec`** (`312cd0dc`); en `main` todavía no está. Esta rama nace de `main`, así que
crear ahí un archivo con solo mis entradas produciría **dos copias divergentes del mismo ledger** y un
conflicto garantizado al converger — justo lo que un registro de costuras compartidas no debe causar.

Así que las entradas van escritas **en el formato exacto del archivo**, listas para copiarse tal cual
cuando las dos ramas converjan. Y el hecho mismo queda registrado como la tercera entrada.

**Ninguna de las tres es un bloqueo inventado.** Las tres tienen causa, archivo afectado y consecuencia
observable. Las causas usadas son solo las cuatro admitidas: `COSTURA_COMPARTIDA`,
`CONTRATO_INEXISTENTE`, `RECURSO_PUERTA_EXCLUSIVA` y `DECISION_PENDIENTE`.

---

## 2026-10-07 · M05b / M06 — la máquina de estados como dato legible

- **Fecha:** 2026-10-07 — America/Lima.
- **Frentes:** M05b / M06.
- **Causa:** `CONTRATO_INEXISTENTE`.
- **Hecho:** el contrato ratificado de M05b (`312cd0dc` §6.2) publica estado actual, historial y la operación de transición, pero **no publica la lista de estados vigentes ni qué transiciones son legales desde cada uno**.
- **Qué tuvo que esperar:** el diseño del tablero de M06. Un tablero necesita saber qué agrupaciones pintar y qué arrastres ofrecer; sin esos dos datos, la única forma de pintarlo es escribir los estados y los movimientos dentro de M06, que es **una segunda máquina de estados** y exactamente lo que la frontera ratificada prohíbe.
- **Contrato o recurso compartido causante:** `Sillar.Modules.ServiceOrders.Contracts`; concretamente la ausencia de una lectura de la máquina de estados y de las transiciones permitidas.
- **Archivos/costura afectados:** `docs/modules/service-orders/SPEC.md` §6.2; `docs/modules/tracking/SPEC.md` §6.2 huecos C1 y C2, §9.1 y §12.
- **Consecuencia:** el Paso 1 de M06 se entrega **sin poder cerrarse**: las agrupaciones del tablero y la legalidad de los arrastres quedan marcadas `CONTRATO_PENDIENTE_DE_MATERIALIZAR`. M06 no propone la firma: la forma la decide M05b, que es el dueño de su máquina.
- **Posible punto de desacople, sin decidirlo:** que el contrato de un módulo dueño de una máquina de estados publique la máquina como **dato de lectura** y no solo la operación, para que cualquier consumidor pueda representarla sin duplicarla.

## 2026-10-07 · M05b / M06 — transición concurrente desde un tablero

- **Fecha:** 2026-10-07 — America/Lima.
- **Frentes:** M05b / M06.
- **Causa:** `DECISION_PENDIENTE`.
- **Hecho:** `TransitionAsync(serviceOrderId, toStatus, cancellationToken)` no recibe el estado que el llamante creía vigente. Dos personas con el tablero abierto ven `in_progress`; una arrastra a `ready` y la otra a `cancelled`. Las dos transiciones son legales por separado, así que M05b acepta las dos y **la segunda gana sin que nadie sepa que hubo conflicto**.
- **Qué tuvo que esperar:** el diseño del arrastre de M06 y sus criterios de aceptación de concurrencia.
- **Contrato o recurso compartido causante:** `IServiceOrderTransitions` y la semántica de concurrencia de la operación autoritativa.
- **Archivos/costura afectados:** `docs/modules/service-orders/SPEC.md` §6.2; `docs/modules/tracking/SPEC.md` §6.2 hueco C7 y §9.5.
- **Consecuencia:** M06 no puede ofrecer arrastre concurrente seguro, y **no lo resuelve reintentando**: reintentar sobre un estado que ya cambió repite el problema. Queda marcado y sin decidir; no se diseña una solución en M06 porque la operación es de M05b.
- **Posible punto de desacople, sin decidirlo:** que las operaciones autoritativas de transición acepten el estado esperado, de modo que un consumidor con vista desactualizada reciba un rechazo en vez de provocar un cambio que nadie pidió.

## 2026-10-07 · M05b / M06 — el ledger compartido vive en una rama y no en `main`

- **Fecha:** 2026-10-07 — America/Lima.
- **Frentes:** M05b / M06.
- **Causa:** `COSTURA_COMPARTIDA`.
- **Hecho:** `docs/integracion/REGISTRO-PARALELIZACION.md` existe en `m05b-service-orders-spec` y no en `main`. Dos frentes documentales en paralelo tienen que escribir en el mismo archivo desde ramas que no se ven.
- **Qué tuvo que esperar:** la actualización del ledger por parte de M06. No esperó al diseño: esperó a tener un sitio donde escribir sin producir dos copias divergentes.
- **Contrato o recurso compartido causante:** el propio archivo de ledger como recurso compartido entre frentes.
- **Archivos/costura afectados:** `docs/integracion/REGISTRO-PARALELIZACION.md`; `docs/modules/tracking/LEDGER-M06-PENDIENTE-DE-TRANSCRIBIR.md`.
- **Consecuencia:** las entradas de M06 se entregan transcribibles en lugar de aplicadas. **El registro no se pierde**, pero queda un paso manual al converger, y ese paso se puede olvidar.
- **Posible punto de desacople, sin decidirlo:** que el ledger llegue a `main` en cuanto exista, y que cada frente añada **solo entradas nuevas al final**, que es la forma de archivo compartido que se fusiona sin conflicto.

---

## Lo que NO se registró, y por qué

Para que el ledger siga sirviendo hay que decir también qué no entró:

| Candidato descartado | Por qué no es una entrada |
|---|---|
| «M06 esperó a que M05b cerrara su Paso 1» | **Ya está registrada** el 2026-10-06 como `CONTRATO_INEXISTENTE`. Repetirla infla el ledger sin añadir un hecho |
| «M06 no puede escribir código todavía» | Es el orden de producto que JP decidió —M05b → M06 → M08—, y ya está registrado el 2026-10-06 como `DECISION_PENDIENTE` entre M06 y M08. Una secuencia deliberada no es una espera causada por una costura |
| «La puerta canónica es un recurso exclusivo» | Cierto en general, pero **este trabajo no la usó**: es documental y no ejecutó nada. Registrarlo sería convertir coincidencia temporal en dependencia, que es lo que el propio ledger prohíbe |
| «Falta el contrato de M06 para M08» | No es una espera: es una decisión **deliberadamente abierta** hasta que M08 se abra (`SPEC.md` §6.5) |
