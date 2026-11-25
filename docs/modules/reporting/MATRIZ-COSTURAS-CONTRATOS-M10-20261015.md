# M10 Reportes — Matriz de costuras y contratos conceptuales M03 → M10

- **Módulo:** M10 · **Código:** `reporting` · **Producto:** SILLAR WEB
- **Carácter:** insumo documental para revisión de **Chat 2** y del **colíder**. **No es SPEC**, no
  autoriza implementar, no crea interfaces, DTOs, rutas, tablas ni pruebas. No declara M10 aprobado
  ni certificado.
- **Autor:** Claude C, Hito 2, encargo de Chat 3 transmitido por JP.
- **Fecha:** 15/10/2026 (America/Lima).
- **Base:** `58bb93a6f3021490a14c9144afd31fc7c02458a0` (HEAD comprobado; sin `fetch`).
- **Documento previo, sin modificar:** `docs/modules/reporting/PREPARACION-DOCUMENTAL-M10-20261015.md`,
  SHA-256 `318ef658f10b3962e1b8d391285cc217bfc7d0633801cc1e770853884f6b044b`.

**Regla de lectura.** Toda afirmación sobre el repositorio cita `ruta:línea` en la base. Lo marcado
**PROPUESTA** no está decidido; lo marcado **NO VERIFICADO** no se pudo comprobar en este hito.

---

## 0. Tres hechos nuevos que condicionan todo lo demás

El Hito 1 no los recogía. Se descubrieron al bajar a M03:

1. **En `main` nadie registra pagos.** La tabla `sales.order_payments` existe
   (`backend/Sillar.Modules.Sales/Migrations/20260930185029_SalesInitial.cs:211-237`), pero ningún
   código crea un `OrderPayment`: las únicas referencias fuera del dominio son su configuración EF y
   el `DbSet` (`Sillar.Modules.Sales/Data/SalesDbContext.cs:47`). M03 solo expone endpoints de cliente
   (`Sillar.Modules.Sales/Endpoints/PedidosDelClienteEndpoints.cs:38-70`). El bloqueo está escrito:
   «**Qué bloquea:** registrar pago, cancelar y cualquier cambio de estado por personal»
   (`docs/modules/sales/ESCALADAS-M03.md:740`), porque `ICurrentAdmin` no da el nombre ni el nodo que
   exige R-14 (`:724-753`).
   **Consecuencia para M10:** hoy cualquier cifra de cobros sería «0» **porque la operación no existe**,
   no porque nadie haya pagado. Eso es **no medible**, no «sin hechos» (§5).
2. **Hoy solo dos transiciones de pedido ocurren en el sistema:** creación en `pending_payment`
   (`Sillar.Modules.Sales/Pedidos/CreadorDePedidos.cs:151`) y vencimiento
   `pending_payment → expired` (`Sillar.Modules.Sales/Pedidos/VencimientoDePlazos.cs:78,102-116`).
   Los otros cinco estados son válidos en la base (`SalesInitial.cs:146`) pero ninguna operación los
   alcanza todavía.
3. **No hay moneda en ningún sitio.** Ni `orders` (`SalesInitial.cs:119-138`) ni `order_payments`
   (`:211-228`) tienen columna de moneda; los importes son `numeric(12,2)` sin unidad
   (`:133`, `:218`). La SPEC de M11 la deja abierta: «moneda operativa y su autoridad»
   (`docs/modules/payments/SPEC.md:228`).

---

## 1. Decisiones

### 1.1 Ratificadas por JP (transmitidas por Chat 3 en el encargo del Hito 2)

| ID | Decisión | Lo que obliga en este documento |
|---|---|---|
| **D-M10-01 = A** | La verdad viene de **contratos de lectura del dueño**. Eventos opcionales, solo para invalidar; nunca única fuente | No se pide a M03 publicar eventos. `PedidoCreado`/`PagoConfirmado` siguen sin emitirse (`Sales.Contracts/Events/SalesEvents.cs:8,25`) y no se solicita cambiarlo |
| **D-M10-02 = C** | **Agregados en vivo** en v1. Proyección persistida solo con justificación y nueva aprobación | M10 v1 no tiene tablas de hechos, checkpoints ni cursores. No se diseña ninguna |
| **D-M10-03 = A** | v1 limitada a **M03 Sales: pedidos y cobros**. M05b y M07 fuera | Solo hay costuras con M03 (y sus prerrequisitos en CORE) |

### 1.2 Abiertas

| ID | Tema | Estado |
|---|---|---|
| D-M10-04 | Vocabulario monetario | **ABIERTA.** Este documento usa «valor de pedidos» y «cobros registrados» como **propuesta** |
| D-M10-05 | Roles, umbral de privacidad, qué se audita | **ABIERTA** |
| D-M10-06 | Fecha del hecho (creación vs transición) | **ABIERTA** |
| D-M10-07 | Apertura de M10 frente a Infra QA §34 | **ABIERTA** (`docs/PENDIENTES.md:663`) |
| D-M10-08 | Identidad de la «SPEC V3.1» | **ABIERTA.** La SPEC del 14/10 es PROPUESTA; aquí no se le da categoría de V3.1 ni se usa como vigente |

### 1.3 Nuevas preguntas que aparecen en este hito (PROPUESTA, no decididas)

| ID | Pregunta | Opciones | Recomendación técnica |
|---|---|---|---|
| D-M10-09 | ¿El rango del reporte es inclusivo o semiabierto? | A. `[desde, hasta)` por instante · B. días completos `[día_inicio, día_fin]` en hora de Lima | **B en la interfaz, traducido a A por el dueño**: el usuario elige días; el dueño cuenta `[00:00 Lima del día inicio, 00:00 Lima del día siguiente al fin)`. Evita dobles conteos en el borde |
| D-M10-10 | ¿Cuentan los pedidos con `is_active = false`? | A. Excluirlos · B. Incluirlos con marca | **A**, igual que el historial del cliente (`Sillar.Modules.Sales/Pedidos/HistorialDePedidos.cs:67`). Hoy ningún código da de baja un pedido: **NO VERIFICADO** que exista ese camino |
| D-M10-11 | ¿Qué hace M10 mientras el registro de pagos no exista? | A. Ocultar la sección de cobros · B. Mostrarla como «no medible» con motivo | **B**: ocultarla haría pensar que el módulo no la tiene |

---

## 2. Contratos conceptuales M03 → M10

**Qué es esto y qué no es.** Se describe el **efecto observable** que M10 necesita y la
responsabilidad que recaería en M03. **No se proponen nombres de interfaz, DTOs, rutas ni firmas**:
eso es del dueño, con Chat 2. M10 **no** enumera clientes, **no** recorre `ICustomerOrderHistory`
(`Sales.Contracts/ICustomerOrderHistory.cs:42-45`, que es por cliente) y **no** accede a `sales.*`.

### 2.1 Lectura agregada de **pedidos**

| Aspecto | Lo mínimo que M10 necesita observar |
|---|---|
| **Entrada** | Un período (ver D-M10-09) y, opcionalmente, una granularidad temporal (día / semana / mes) y un filtro por estado |
| **Salida por intervalo** | Número de pedidos y suma de `total_amount` (`SalesInitial.cs:133`), **desglosados por estado** |
| **Fecha que ubica el pedido en el período** | La de **creación** (`created_at`, `SalesInitial.cs:137`; es lo que `HistorialDePedidos.cs:75` entrega como `PlacedAt`) — mientras D-M10-06 no diga otra cosa |
| **Estado** | El **estado actual** del pedido, declarado así en la respuesta (`estado_al_consultar`), no el estado que tenía al terminar el período (§3.3) |
| **Metadatos obligatorios** | Momento de la lectura según el dueño; zona horaria con que agrupó; si la fuente es **completa** para el período |
| **Nunca** | Códigos visibles, ids, datos del cliente (`SalesInitial.cs:126-130` los guarda: no deben salir), líneas individuales |

**Responsabilidad de M03:** definir la semántica (qué es «pedido» a efectos de reporte, qué hace con
`is_active`), agrupar en hora de Lima con su propia constante
(`Sillar.Shared.Data/Numbering/TransactionalSeriesAllocator.cs:19`), acotar el período máximo y
garantizar que el resultado sale de **una sola lectura coherente** (§6.2).

**Desglose por producto:** `order_lines.product_id` existe «solo para agrupar en informes»
(`docs/modules/sales/SPEC.md:354-357`). **No entra en el mínimo de v1**; se deja como extensión
posible del mismo contrato, a decidir después.

### 2.2 Lectura agregada de **cobros registrados**

| Aspecto | Lo mínimo que M10 necesita observar |
|---|---|
| **Entrada** | Período, granularidad, filtro opcional por medio de pago |
| **Salida por intervalo** | Número de pagos y suma de `amount` (`SalesInitial.cs:218`), desglosados por medio (`yape`, `efectivo`: `SalesInitial.cs:236`) y por **tardío / a tiempo** (`was_late`, `:224`) |
| **Fecha que ubica el cobro** | `registered_at` (`:223`), **nunca** la fecha del pedido |
| **Capacidad** | Un indicador explícito de si la fuente **puede** tener cobros hoy. Mientras no exista la operación de registro (§0.1), la respuesta debe decir «no medible» con motivo, y M10 no puede deducirlo de un conjunto vacío |
| **Nunca** | `registered_by`, `registered_by_admin_user_local_id`, `registered_by_admin_user_home_node` (`:220-222`), `reference` (`:219`), id del pedido o del pago |

**Responsabilidad de M03:** decidir si un pago sobre un pedido luego cancelado sigue contando
(hoy no hay reversos: §3.6), declarar la capacidad, y mantener la semántica cuando llegue JP-02 (§4).

### 2.3 Lo que M10 pondría de su parte

- Pedir el contrato al contenedor y degradar si no está (`CLAUDE.md:69`).
- No sumar pedidos y cobros en una misma cifra.
- No convertir ningún estado de §5 en `0`.
- No guardar nada de lo leído (D-M10-02 = C).

---

## 3. Criterios funcionales de los reportes

### 3.1 Período y zona

- El usuario elige días; la zona es **America/Lima**, constante de plataforma
  (`TransactionalSeriesAllocator.cs:19`), mostrada siempre junto al rango.
- Inclusivo o semiabierto: **D-M10-09**.
- Un período futuro o invertido se rechaza; un período mayor que el máximo del dueño se rechaza con
  una frase que dice el máximo. El máximo concreto: **pendiente**, lo fija M03 (§6).

### 3.2 Fecha del hecho

| Cifra | Fecha que la ubica | Estado |
|---|---|---|
| Pedidos creados / valor de pedidos | Creación del pedido | PROPUESTA, pendiente D-M10-06 |
| Cobros registrados | Registro del pago | PROPUESTA |
| Pedidos vencidos «en el período» | Fecha de la transición a `expired` (`order_status_changes.changed_at`, `SalesInitial.cs:256`) | **Requiere** que el contrato exponga transiciones agregadas. Fuera del mínimo si D-M10-06 = A |

### 3.3 Estado actual frente a estado en el momento

- Con D-M10-06 = A, un reporte de octubre consultado en diciembre muestra los pedidos de octubre
  **con el estado que tienen en diciembre**. Esto es correcto si se dice; es engañoso si no.
- La pantalla y el CSV deben rotular: «estado al consultar». **PROPUESTA de texto**, sujeta a STOP 3.5.
- «Estado al cierre del período» exige leer el historial (`order_status_changes`, `SalesInitial.cs:248-258`).
  Es otro contrato; no se pide en v1.

### 3.4 Valor de pedidos frente a cobro

| Cifra (vocabulario **PROPUESTO**, D-M10-04) | Qué suma | Por qué no es otra cosa |
|---|---|---|
| **Valor de pedidos** | `total_amount` de los pedidos del período | Incluye impagos, vencidos y cancelados salvo filtro. `total_amount` es la suma de líneas y nada más (`docs/modules/sales/SPEC.md:346`, R-15 `:941-943`) |
| **Cobros registrados** | `amount` de los pagos registrados en el período | Es dinero que una persona dijo haber recibido. **No** es comprobante ni contabilidad |

**Palabras prohibidas para estas cifras mientras D-M10-04 no se resuelva:** «ingresos»,
«facturación», «ventas», «ventas netas», «recaudación neta». Ninguna está acreditada por el modelo.

### 3.5 Pagos tardíos

- Un pago sobre un pedido vencido siempre puede registrarse (R-12, `docs/modules/sales/SPEC.md:747-756`)
  y queda marcado como hecho, no deducido (`Sillar.Modules.Sales/Domain/OrderPayment.cs:101-109`).
- Cuenta como cobro **en su fecha de registro**. Se puede mostrar el desglose tardío / a tiempo.
- Un pago tardío sin mercancía deja el pedido en `expired` (`SPEC.md:764-770`): el reporte verá un
  pedido vencido **y** un cobro. Ambas cosas son ciertas; no se «corrigen» entre sí.

### 3.6 Anulaciones y reversos — **no resueltos**

- No existe modelo de reverso, devolución, saldo a favor ni contracargo: «No hay saldo a favor ni
  devoluciones automáticas: son deuda con disparador propio» (`SPEC.md:754`); M11 las lista como
  abiertas (`payments/SPEC.md:226`).
- Por tanto, **un cobro registrado no se descuenta nunca** en v1, aunque el pedido se cancele.
  Cualquier cifra que diga lo contrario inventa datos.
- Pagos parciales o múltiples: el esquema admite varios pagos por pedido (FK no única
  `idx_order_payments_order_id`, `SalesInitial.cs:310-314`; R-12 habla de «al menos un pago»,
  `SPEC.md:776-777`), y M11 lo deja abierto (`payments/SPEC.md:225`). M10 suma pagos, no «pedidos
  pagados»: **«pedido pagado» no es una cifra defendible** hasta que M03 defina saldo.

### 3.7 Moneda — **pendiente**

Sin columna ni autoridad (§0.3). M10 no puede rotular un símbolo sin decisión. **PROPUESTA:** mostrar
el importe sin símbolo y con la nota «moneda no declarada por la fuente» hasta que M03/M11 la definan.

---

## 4. Compatibilidad con JP-02 / M11

**Qué hay en `main`:**

- M03 conserva el ledger canónico de pagos (`docs/modules/payments/SPEC.md:39,92`).
- `OrderPayment` deberá distinguir origen humano (trío de atribución) y origen externo (sin
  atribución humana, con identidad externa idempotente) (`payments/SPEC.md:99-102`).
- Un solo concepto de pago confirmado, sin «dos universos» (`:108-115`).
- Unicidad de M03 `external_settlement_id` (`:132-133`).

**Qué no está en `main`:** la adenda JP-02 (`source=staff|provider`). El commit `58bb93a` lo dice
expresamente («no incorpora adenda JP-02»), y la columna `source` **no existe** en `order_payments`
(`SalesInitial.cs:211-228`). Este documento **no la da por implementada**.

**Requisitos para el contrato de cobros (PROPUESTA):**

1. El origen (`staff` / `provider`), cuando exista, puede ser una **dimensión** del agregado. Hasta
   entonces, todos los cobros son de origen humano y el contrato no inventa el campo.
2. **Nunca** se expone: atribución personal, nombre del proveedor de pago si fuera sensible,
   identificadores externos (`external_settlement_id`, `provider_event_id`), secretos ni datos del cliente.
3. Comisiones, liquidaciones al comercio y desembolsos **no existen** en el modelo
   (`payments/SPEC.md:227` los deja abiertos). M10 no muestra «neto».
4. La semántica de «cobro registrado» debe ser la misma para ambos orígenes, o el contrato debe
   versionarse explícitamente (coherente con `payments/SPEC.md:112-113`).

---

## 5. Estados de salida

**Regla:** ningún estado distinto de «con datos» se representa como `0`.

| Estado | Cuándo | Quién lo determina | Ejemplo de efecto |
|---|---|---|---|
| **Con datos** | Fuente disponible, completa, con hechos | Dueño | Cifras |
| **Sin hechos** | Fuente disponible y **medible**, sin hechos en el período | Dueño | «No hubo pedidos entre el 1 y el 15 de octubre.» |
| **Fuente ausente** | M03 no instalado: el contrato no está en el contenedor (`ARQUITECTURA_MODULAR.md:334`) | M10 | Sección explicativa, sin cifras |
| **Fuente desactivada** | M03 desactivado; tras el reinicio del host (`Core.Contracts/Events/CoreEvents.cs:5-7`) se comporta como ausente | M10 | Igual que ausente. No se muestran datos anteriores: M10 no los guarda |
| **No medible** | Fuente presente pero la operación que produce el hecho no existe (cobros hoy, §0.1) | **Dueño**, por declaración explícita | «Los cobros todavía no se registran en el sistema.» |
| **Incompleto** | El dueño excluye parte del período o del conjunto (p. ej. máximo superado, si se acordara recortar en vez de rechazar) | Dueño | Cifras con aviso de cobertura |
| **Error de fuente** | La lectura falla o excede el tiempo | M10 | Frase que dice qué falló y qué hacer; el resto del reporte sigue |
| **Consistencia limitada** | Siempre, en consulta en vivo: la cifra vale para el momento de la lectura | — | Se muestra el momento de la lectura (§6.2) |

Textos: PROPUESTA, sujetos a STOP 3.5 y a la regla de `CLAUDE.md` sobre mensajes.

---

## 6. Viabilidad y coste de los agregados en vivo

**No se diseña física ni DDL.** Todo lo de esta sección son recomendaciones para M03 y Chat 2.

### 6.1 Volumen

- Una consulta agregada por período recorre los pedidos del período. Con índices adecuados el coste
  crece con el período, no con la historia total.
- **Índices hoy existentes en `sales.orders`:** `customer_id`, `payment_due_at`, `status`,
  `order_code` (`SalesInitial.cs:336-359`). **No hay índice por fecha de creación.**
- **En `sales.order_payments`:** `order_id`, `was_late` (`:310-321`). **No hay índice por
  `registered_at`.**
- **Recomendación NO aprobada:** índice por fecha de creación en pedidos y por fecha de registro en
  pagos, decididos y escritos por M03 en su propia migración, si las mediciones lo justifican.
  Ninguna cifra de volumen se ha medido en este hito.

### 6.2 Consistencia entre pedidos y cobros

- Si M10 hace dos llamadas (pedidos, luego cobros), un pago registrado entre ambas puede aparecer en
  una y no en la otra.
- **PROPUESTA:** que el dueño ofrezca ambas lecturas desde **una misma instantánea** (una sola
  transacción de lectura, nivel a elegir por M03) cuando se piden juntas, o que cada respuesta lleve
  su momento de lectura y la pantalla los muestre. La elección es de M03 + Chat 2.

### 6.3 Filtros

Mínimos: período, granularidad, estado (pedidos), medio y tardío (cobros). **Nada de filtros libres
ni SQL de usuario.** Filtros por cliente o por código de pedido: **fuera** (convierten el agregado en
detalle con PII).

### 6.4 Consultas largas, límites y paginación

- Período máximo y número máximo de intervalos por respuesta: impuestos por el dueño, en servidor.
- Un agregado por día de un año son ≤ 366 filas por dimensión: **no requiere paginación**. Si algún
  día se pide detalle, se reabre (y entonces el desplazamiento de `Sillar.Shared/Paging/PageRequest.cs:35`
  no sirve sobre datos que cambian).
- Tiempo de espera de la lectura: lo fija M10 al llamar; si se agota, estado «error de fuente».
- La pantalla no bloquea: muestra el estado «cargando» por sección (STOP 3.5).

---

## 7. Pruebas de aceptación conceptuales

**No se crean ni se ejecutan.** Indican qué tendría que demostrar el dueño (**D**) o M10 (**M**).

| # | Comprueba | Dado | Entonces | Quién |
|---|---|---|---|---|
| P01 | Privacidad del contrato de pedidos | Pedidos con nombre, correo y documento de cliente | La respuesta no contiene ninguno de esos valores, ni códigos ni ids (afirmado con prueba, no con comentario, como exige `docs/modules/b2b/SPEC.md:444-445` para `staff_notes`) | D |
| P02 | Privacidad del contrato de cobros | Pagos con atribución y referencia | Ningún campo de atribución ni `reference` en la respuesta | D |
| P03 | Lectura idempotente | Misma petición dos veces sin cambios en medio | Resultados idénticos | D |
| P04 | Lectura sin efectos | Cualquier lectura | Ninguna fila de `sales.*` cambia (`row_version`, `updated_at` intactos) | D |
| P05 | Suma de importes | 3 pedidos de 10,00 / 20,50 / 0,00 | Total 30,50, recuento 3 (el de 0,00 cuenta) | D |
| P06 | Borde del día | Pedido a las 23:59:59 y otro a las 00:00:00 hora Lima | Caen en días distintos | D |
| P07 | Borde del período | Según D-M10-09 | El pedido del límite se cuenta una sola vez | D |
| P08 | Estado actual | Pedido de octubre vencido en noviembre | El reporte de octubre lo muestra como `expired` y lo rotula «al consultar» | D, M |
| P09 | Pago tardío | Pago sobre pedido vencido | Cuenta en la fecha del pago y en el desglose «tardío» | D |
| P10 | Cobro no descontado | Pago registrado y luego pedido cancelado | El cobro sigue contando (no hay reverso) | D |
| P11 | No medible | Sin operación de registro de pagos | La respuesta de cobros dice «no medible», no lista vacía | D, M |
| P12 | Sin hechos | M03 presente, período sin pedidos | «Sin hechos», distinto de P11 y de P13 | D, M |
| P13 | Módulo ausente | M03 no instalado | M10 arranca, la sección dice «no disponible», ninguna cifra 0 | M |
| P14 | Módulo desactivado | M03 desactivado y host reiniciado | Igual que P13 | M |
| P15 | Error de fuente | El dueño lanza o agota el tiempo | Estado «error», resto del reporte intacto, ninguna excepción hacia el usuario | M |
| P16 | Período inválido | Inicio posterior al fin, o mayor que el máximo | Rechazo con frase que dice qué corregir | D, M |
| P17 | Sin enumeración | Revisión de dependencias de M10 | M10 no referencia `ICustomerOrderHistory` ni ningún `DbContext` de Sales | M |
| P18 | Sin moneda inventada | Cualquier importe | Ningún símbolo de moneda mientras §3.7 siga abierta | M |
| P19 | Vocabulario | Pantalla y CSV | Ninguna de las palabras prohibidas de §3.4 | M |
| P20 | Instantánea | Pago registrado entre la lectura de pedidos y la de cobros | Comportamiento según §6.2 (misma instantánea o momentos de lectura visibles) | D, M |

---

## 8. Matriz de costuras para Chat 2

**Ninguna la realiza Claude C.** Se listan para que Chat 2 decida, ordene y asigne.

| # | Costura | Dueño | Cambio deseado (efecto observable) | Evidencia existente | Riesgo | Decisión pendiente | Secuencia sugerida | Aprueba |
|---|---|---|---|---|---|---|---|---|
| **S0** | Contradicción documental: M10 «consume eventos» | Chat 2 + JP (docs canónicos) | Que `ARQUITECTURA_MODULAR.md:66,120,341` y `ROADMAP_MODULAR.md:124` dejen de decir que M10 se alimenta del bus | `PENDIENTES.md:544` («el bus nunca es el camino por el que viaja un hecho que alguien necesita»); D-M10-01 = A | Bajo técnico; alto de confusión: dos documentos canónicos se contradicen | Redacción | **1** — antes de la SPEC de M10 | JP, con revisión del colíder |
| **S1** | Atribución del personal disponible para M03 | CORE / Integración | Que M03 pueda obtener nombre visible y nodo de pertenencia del administrador | `ESCALADAS-M03.md:724-753` (§p, «ELEVADA») | Alto: toca CORE | Ya elevada por Chat 2; no es de M10 | **2** — prerrequisito de S2 | JP + Chat 2 |
| **S2** | Operación de registro de pago por el personal | M03 | Que existan pagos registrados en `sales.order_payments` | Tabla sin escritor (§0.1); diseño previsto en `DISENO-TECNICO-PROVISIONAL-M03.md:168` | Alto: dinero | No es de M10. Se señala solo porque sin ella los cobros son «no medibles» | **3** | JP + dueño M03 + Chat 2 |
| **S3** | Adenda JP-02 en el ledger (`source`) | M03 (+ M11) | Que `OrderPayment` distinga origen humano y externo | `payments/SPEC.md:98-102`; no está en `main` (mensaje de `58bb93a`) | Alto: migración de una tabla de dinero | Integración de JP-02 | **4** — antes de fijar el contrato de cobros, o el contrato nace sabiendo que versionará | JP + colíder + Chat 2 |
| **S4** | Lectura agregada de **pedidos** para reporting | M03 | §2.1 | `ICustomerOrderHistory` es por cliente (`ICustomerOrderHistory.cs:42-45`); sin índice por fecha (§6.1) | Medio: nuevo contrato en módulo cerrado; riesgo de filtrar PII si se proyecta la entidad | D-M10-06, D-M10-09, D-M10-10 | **5** — puede ir **antes** que S1–S3: no depende de pagos | JP + Chat 2 + dueño M03; audita colíder |
| **S5** | Lectura agregada de **cobros** para reporting, con «capacidad» | M03 | §2.2 | `OrderPayment.cs:27-121`; sin índice por `registered_at` | Alto: privacidad de atribución; semántica que cambia con S3 | D-M10-04, D-M10-11 | **6** — tras S2 (y preferiblemente S3); si se adelanta, debe declarar «no medible» | JP + Chat 2 + dueño M03; audita colíder |
| **S6** | Lectura coherente pedidos + cobros | M03 | §6.2 | — | Medio | Cuál de las dos opciones de §6.2 | Con S4/S5 | Chat 2 + dueño M03 |
| **S7** | Índices por fecha en `orders` y `order_payments` | M03 | §6.1 | `SalesInitial.cs:310-359` | Bajo; es migración de M03 | Solo si se mide necesidad | Con S4/S5 | Dueño M03 + Chat 2 |
| **S8** | Comentario caducado en `OrderPayment.Method` | M03 | Que `OrderPayment.cs:36` («Hoy solo `yape`») no contradiga el `CHECK` (`SalesInitial.cs:236`) ni `PaymentMethod.cs:44`, que admiten `yape` y `efectivo` | Las tres citas | Bajo: un comentario caducado «miente con más autoridad que el código» (`Sillar.Shared/Events/IEventPublisher.cs:11-12`) | Ninguna | Cuando M03 toque el archivo | Dueño M03 |
| **S9** | `ModuleActivated/Deactivated` publicados como `object` | CORE | Que un manejador tipado de esos eventos pueda recibirlos, o que se documente que no | `ModuleActivationService.cs:246`; `InProcessEventBus.cs:25` | Bajo hoy (sin manejadores) | ¿Es intencional? | Sin prisa; M10 no depende de esto | Chat 2 |
| **S10** | Montaje de M10 en el host: proyecto, capacidades, menú, ruta | Integración | Registrar el módulo `reporting` sin rutas muertas | `ARQUITECTURA_MODULAR.md:334` | Medio (puerta) | D-M10-07 (Infra QA §34) | **Última**, tras SPEC aprobada | JP + Chat 2 |

**No se pide:** que M03 publique `PedidoCreado`/`PagoConfirmado` (D-M10-01 = A lo hace innecesario
para M10), ni contratos de M05b/M07 (D-M10-03 = A).

---

## 9. Puerta formal

1. Este documento es **insumo** para Chat 2 y el colíder. No es SPEC y no autoriza costuras.
2. **Si Chat 2 objeta** el ownership (p. ej. que la agregación deba vivir en M10 y no en M03) o
   los requisitos de §2, **el diseño de la SPEC de M10 queda BLOQUEADO hasta decisión de JP**.
3. S0 debería resolverse antes de redactar la SPEC: escribirla contra una arquitectura que dice lo
   contrario dejaría dos fuentes canónicas en conflicto.
4. Mientras S2 no exista, cualquier SPEC de M10 debe tratar los cobros como «no medibles» y no
   prometer cifras.
5. Claude C queda a la espera de la auditoría documental y no avanza a la SPEC.
