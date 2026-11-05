# CORE · Entrega 07 — Moneda de la instalación (autoridad monetaria)

**Creación:** 04/11/2026, America/Lima
**Base:** `main` = `58bb93a6f3021490a14c9144afd31fc7c02458a0`
**Escrito por:** Claude Code D, por encargo de Chat 3 (P1-POST-G6-R10-E5), con D1–D8 ratificadas por JP
**Estado:** candidata, **sin certificar** ni integrar. Ninguna migración aplicada. HOLD MAIN vigente.

Refina el `SPEC.md` de CORE dentro de su alcance; el SPEC ya recoge la clave (§4.11), los dos
endpoints (§6) y la regla 18 (§8). La numeración salta 16 y 17 a propósito: las usa la candidata
`ENTREGA-06` (zona y exportación), que no está en `main`.

---

## 1 · Qué ofrece CORE

| Pieza | Dónde |
|---|---|
| Clave privada `installation.currency_iso4217` | `InstallationCurrency.SettingKey` |
| Lista cerrada y versionada | `SupportedCurrencies` (`sillar-iso4217-v1`: `PEN`, 2 decimales) |
| Validación exacta | `InstallationCurrency.Check` |
| Lector de autoridad para M03 | `IInstallationCurrencyAuthority.ReadAsync` |
| Puerto para que Ventas informe de pedidos | `IMonetaryRecordsProbe` (CORE no lo implementa) |
| Operación administrativa | `PUT /api/admin/settings/installation-currency` (super_admin) |
| Lectura administrativa | `GET /api/admin/settings/installation-currency` (admin) |
| Preparación | migración `CoreInstallationCurrency` + misma fila en `02_seed.sql` |

## 2 · Cómo se cumple cada decisión

| | Decisión | Cómo |
|---|---|---|
| D1 | Clave privada nueva | `is_public = false` al preparar, y forzado otra vez en cada escritura |
| D2 | Sin moneda al empezar; nada de `currency_code` | Nace en `PENDIENTE_DEFINIR` = `NotEstablished`. Ni la preparación ni el lector leen `currency_code` |
| D3 | Solo super_admin | Política del endpoint **y** comprobación en el servicio. La ruta genérica la rechaza (403), también en mayúsculas |
| D4 | Lista cerrada y versionada | §3 |
| D5 | Rechazos | Ausente, vacío, pendiente, malformado (sin recortar ni cambiar mayúsculas) y no admitido |
| D6 | Auditoría durable vinculada | §4 |
| D7 | Preparación idempotente sin moneda | `INSERT … ON CONFLICT DO NOTHING`; no toca un valor ya existente; `Down` no borra |
| D8 | `currency_code` sigue de presentación | Sin cambios ni en semilla ni en contrato público |

## 3 · La lista

- **Contenido:** `PEN`, 2 decimales. Es la única moneda que el producto maneja de punta a punta:
  importes en `numeric(12,2)`, `currency_symbol = S/`, textos y colación de Perú. Ninguna otra
  tiene formato, símbolo ni prueba.
- **Versión:** `sillar-iso4217-v1`. Una prueba fija el contenido exacto.
- **Mantenimiento:** añadir o quitar una moneda es decisión de JP y cambio de código que sube la versión.
- **Fuente:** los datos de PEN se contrastan en prueba con CLDR del runtime. La publicación oficial
  de la agencia de mantenimiento de ISO 4217 **no fue alcanzable** desde el entorno (403).

## 4 · Cambio y auditoría, o nada

1. Super_admin, valor de la lista, y el escritor de auditoría sobre **el mismo** `CoreDbContext`
   (si no, `AuditNotAtomic`).
2. Ventas dice que no hay pedidos (§5).
3. Transacción explícita; `SELECT … FOR UPDATE` de la fila; se relee el valor anterior.
4. Se cambia el valor y se prepara el registro con `AuditWriter.Stage`, **sin guardar**.
5. Un único `SaveChanges` y `Commit`. Si algo falla, se deshace todo y el contexto queda limpio.

El registro es `update` / `setting` / `installation.currency_iso4217`, con resumen de forma fija:
`Moneda de la instalación: anterior=<valor>; nueva=<código>; lista=<versión>.`

**Ese registro es el comprobante.** El lector solo acredita una moneda si el valor está en la lista
**y** el último registro de la clave dice ese mismo valor. Un valor escrito por fuera queda
`NotAttested`.

## 5 · STOP — bloqueo por pedidos existentes

Saber si hay pedidos es de M03, no de CORE: CORE no lee el schema de otro módulo. CORE deja el
puerto `IMonetaryRecordsProbe` y, **sin implementación registrada, no establece ni cambia la
moneda** (`409`, «todavía no hay forma de comprobar si existen pedidos»).

**Propuesta mínima para JP:**

1. M03 implementa `IMonetaryRecordsProbe` y lo registra cuando está activo.
2. Riesgo que queda: la consulta a Ventas va fuera de la transacción de CORE. Un pedido creado
   entre la consulta y el commit no se ve. Cerrarlo exige que M03 rechace crear pedidos sin moneda
   acreditada, o una serialización entre módulos; las dos cosas están fuera de CORE.
3. Decidir si «registros con importe» incluye también cotizaciones (M07) y órdenes de servicio
   (M05b), que guardan importes con la misma moneda implícita.

## 6 · Pendiente de validación física (PostgreSQL)

Nada de esto se ha ejecutado contra una base:

- aplicar `CoreInstallationCurrency` dos veces sin cambios;
- establecer `PEN` y leer `Accredited`;
- que un fallo en la escritura de la auditoría deshaga el cambio;
- el bloqueo `FOR UPDATE` con dos super_admin a la vez.

## 7 · Fuera de esta entrega

Ventas e `IAutoridadDeMoneda`, el host, CommerceBridge, M08, E2E, M10, M11 y M12. La pantalla de
configuración muestra la clave en el grupo genérico; guardarla desde ahí devuelve 403 con su
mensaje. Un control propio en la interfaz queda pendiente.

---

## 8 · Corrección E5.1 (sobre `61089ae`)

- **O-1 · Comprobante.** `Compose` podía escribir `anterior=valor-no-admitido` y el patrón no
  aceptaba minúsculas: un cambio legítimo desde un valor no reconocido quedaba `NotAttested`. El
  patrón admite ahora **exactamente** lo que `Compose` puede escribir como valor anterior —un
  código de tres letras, `PENDIENTE_DEFINIR` o `valor-no-admitido`— y nada más; es más estricto que
  el de E5, no más laxo.
- **O-2 · Igual no es acreditado.** Si el valor guardado ya es el pedido, la operación mira el
  último registro de auditoría. Si lo acredita: `Unchanged`, sin escribir. Si no: registra este acto
  explícito de super_admin (`Reattested`, `anterior=PEN; nueva=PEN`), con los mismos requisitos que
  un cambio —incluido el informe de pedidos—. Nunca responde «ya estaba» con la moneda `NotAttested`.
- **O-3 · Después del commit.** El bus en proceso se traga los fallos de los manejadores, pero deja
  pasar `OperationCanceledException`, y otro publicador podría lanzar cualquier cosa: la operación
  salía con excepción con la moneda ya guardada. Ahora `AnnounceAsync` invalida la caché e intenta el
  aviso una vez, con el token del llamador como el resto de la configuración, y **no lanza**: la
  respuesta dice lo que pasó en la base. El contrato de `IEventPublisher` y la semántica del aviso no
  cambian; ver §9.

## 9 · Propuesta a JP sobre el aviso (no implementada)

Con el diseño actual, un `SettingChanged` que no llega —cancelación tardía o publicador roto— se
pierde: queda en el log, no se reintenta. Hoy no hay ningún manejador de `SettingChanged` fuera de
CORE, así que no hay consecuencia. Si algún módulo llegara a depender de ese aviso para la moneda, las
salidas serían publicar con un token propio en vez del del llamador, o un aviso durable; las dos
cambian la semántica de eventos y quedan para decisión.

---

## 10 · Barrera transaccional CORE–Ventas (E5.2, sobre `5ce4e02`)

**Problema (R10-E6).** CORE consultaba los pedidos antes de abrir su transacción, y Ventas podía
leer y usar la moneda antes de confirmar un pedido: un pedido a medio crear no se veía, y un cambio
de moneda a medio confirmar tampoco.

**Contrato** — `Sillar.Core.Contracts.MonetaryAuthorityLock`:

| | |
|---|---|
| Identidad | `pg_advisory_xact_lock*(1397312588, 1)`: `Namespace = 0x53494C4C` (`'SILL'`), `CurrencyAuthority = 1` |
| Forma | Dos claves `int4`. Los demás bloqueos consultivos del producto usan una clave `bigint` (`ModuleActivationService.cs:77,143`; `hashtextextended` en `ServiceOrderApplicationService.cs:65` y `TrackingApplicationService.cs:485`); PostgreSQL no mezcla los dos espacios |
| Por instalación | Los bloqueos consultivos son de cada base, y en una base cabe una sola instalación (`ck_installation_singleton`). Ventas no necesita leer `core.installation` |
| Misma base | CORE y Ventas se conectan con la cadena `Default` (`CoreModule.cs:24,82`; `SalesModule.cs:22,87`) |
| Duración | Solo variantes `_xact_`: se sueltan al confirmar o deshacer |

**Orden de adquisición:**

| Quién | Modo | Orden dentro de su transacción |
|---|---|---|
| CORE, establecer o cambiar la moneda | `AcquireExclusive` | `BEGIN` → `SET LOCAL lock_timeout = '5s'` → **bloqueo** → `IMonetaryRecordsProbe` → `FOR UPDATE` de la fila → auditoría y cambio en un `SaveChanges` → `COMMIT` |
| Ventas, crear un pedido (pendiente) | `AcquireShared` | `BEGIN` → **bloqueo**, antes que cualquier otro → leer la moneda con `IInstallationCurrencyAuthority` → numerar → insertar → `COMMIT` |
| Sonda de Ventas | **ninguno** | Solo lee, sin `FOR SHARE` ni `FOR UPDATE` |

Sin sonda registrada, CORE responde sin abrir transacción ni pedir el bloqueo. Si el bloqueo no
queda libre en 5 s: `MonetaryLockBusy` (409, «Hay una venta en curso usando la moneda…»), sin cambios.

**Lo que se mantiene de E5.1:** roles, validación, auditoría en el mismo contexto, `Unchanged` solo con
comprobante, `Reattested`, `Accredited`/`NotAttested` y el aviso posterior al commit que no lanza.

**Pruebas sin PostgreSQL.** `BaseGuionada` sustituye conexión, transacción y comandos por dobles que
anotan cada paso en orden. Prueba el orden y la vuelta atrás, **no** la exclusión mutua.

## 11 · Escenarios físicos preparados (NO ejecutados)

Base PostgreSQL desechable con CORE migrado. Dos sesiones `psql`: **V** (hace de Ventas) y **C** (hace
de CORE, o la API con una sonda de prueba registrada **solo** en el arnés físico). En cada paso:

```sql
SELECT pid, locktype, classid, objid, objsubid, mode, granted
FROM pg_locks WHERE locktype = 'advisory';
-- Esperado mientras hay bloqueo: classid = 1397312588, objid = 1, objsubid = 2 (forma de dos claves).
```

**A · Ventas primero.**
1. V: `BEGIN; SELECT pg_advisory_xact_lock_shared(1397312588, 1);` y una fila de pedido sin confirmar.
2. C: `BEGIN; SET LOCAL lock_timeout = '5s'; SELECT pg_advisory_xact_lock(1397312588, 1);` → **espera**
   (`granted = false`).
3. V: `COMMIT;` → C obtiene el bloqueo; la sonda ve el pedido → `RecordsPresent`; C deshace.
4. Esperado: moneda sin cambiar, sin registro de auditoría nuevo, ningún bloqueo en `pg_locks`.

**A′ · Ventas retiene más de 5 s.** Como A, sin el paso 3 → C recibe `55P03` → `MonetaryLockBusy`; nada escrito.

**B · CORE primero.**
1. C: `BEGIN; SET LOCAL lock_timeout = '5s'; SELECT pg_advisory_xact_lock(1397312588, 1);` (sonda: sin pedidos).
2. V: `BEGIN; SELECT pg_advisory_xact_lock_shared(1397312588, 1);` → **espera**.
3. C: cambio y auditoría; `COMMIT;` → V obtiene el bloqueo y lee `Accredited` con la moneda nueva.
4. Esperado: V nunca lee el valor anterior después de haber obtenido el bloqueo.

**C · Dos CORE a la vez.** El segundo espera; tras el primero, lee `Unchanged` si el valor ya está acreditado.

**D · Liberación.** Tras `COMMIT` o `ROLLBACK` de cada sesión, `pg_locks` no muestra el bloqueo.

**E · Regla de la sonda.** Una sonda que pidiera el bloqueo compartido en su propia conexión se
quedaría esperando a C, y C esperando a la sonda: PostgreSQL no lo detecta como interbloqueo. Se comprueba
que la sonda real no lo pide (revisión de código y una prueba en M03).

## 12 · Riesgos para la implementación de Ventas

1. **El bloqueo compartido va primero**, antes de numerar (`OrderCodeAllocator`) y de insertar. Si va
   después, un pedido retendría filas propias mientras espera a CORE.
2. **La moneda se lee después de obtener el bloqueo**, con `IInstallationCurrencyAuthority` (lee la base,
   no la caché), y el pedido se rechaza si no está `Accredited`.
3. **La sonda no pide el bloqueo** (§11 E) y cuenta solo filas confirmadas. Falta decidir qué registros
   cuentan: estados de pedido, y si entran cotizaciones (M07) u órdenes de servicio (M05b).
4. **Agotamiento del pool.** CORE y Ventas comparten pool (misma cadena). CORE, con su conexión retenida,
   pide otra para la sonda; Ventas, con la suya, pide otra para leer la moneda. Con el pool agotado,
   ambos esperan conexión: hace falta tamaño de pool y tiempo de espera suficientes.
5. **Tiempo de espera propio en Ventas.** Si su `lock_timeout` vence, el pedido se rechaza con una frase,
   no con «error».
6. **Agrupadores de conexiones** en modo transacción (PgBouncer): las variantes `_xact_` funcionan; las de
   sesión, no. Nunca usar `pg_advisory_lock` con esta identidad.
7. **Misma base.** Si algún día CORE y Ventas usaran cadenas distintas, la barrera dejaría de existir sin
   avisar: conviene una prueba en M03 que lo vigile.

---

## 13 · E9 · O-1 — Alias de la clave monetaria bajo la colación ICU

**Condición.** `PUT /api/admin/settings/{key}` comprobaba la clave **pedida** con `OrdinalIgnoreCase`
y luego buscaba la fila con `core.es_ci`, ICU `es-PE-u-ks-level2` no determinista. Para esa colación son
iguales, además de las mayúsculas, los anchos completos, las variantes de compatibilidad y los
caracteres ignorables (guion suave, espacios y uniones de anchura cero, marca de orden, selectores de
variante). Un alias así superaba el filtro, encontraba la fila protegida y la cambiaba por la ruta
genérica: moneda alterada sin la operación de super_admin, y un registro genérico que dejaba la moneda
`NotAttested`.

**Reparación.** Tras recuperar la fila, y antes de tocar nada, se compara **la clave de la fila**
—la que guarda la base, única bajo `uq_site_settings_setting_key`— con `InstallationCurrency.SettingKey`
en ordinal. El filtro sobre la clave pedida se queda como atajo sin base. Las demás escrituras de
`site_settings` en CORE no aceptan claves de fuera: `SetupService` escribe la fija `business_name` en
modo instalación, y `InstallationCurrencyService` la exacta.

**Pruebas sin PostgreSQL** (`ClaveMonetariaColacionTests`): la base guionada devuelve la fila canónica
ante cualquier lectura, como haría PostgreSQL con un alias. 14 alias, con y sin cambio de visibilidad:
`403`, sin `UPDATE`, sin auditoría, sin aviso, sin transacción, sin `pg_advisory` y la entidad sin
cambios. Sin la guarda nueva, 12 de los 14 alias escriben.

**Reproducción física preparada (NO ejecutada).** PostgreSQL 16, clúster ICU `es-PE`, CORE migrado y la
clave preparada:

```sql
-- 1. ¿Qué alias da ICU por iguales? Una fila = alias efectivo.
SELECT a.caso, count(s.*) AS filas
FROM (VALUES
  ('guion suave',       U&'installation.curr\00ADency_iso4217'),
  ('anchura cero',      U&'installation.currency\200B_iso4217'),
  ('unión anchura cero',U&'\200Dinstallation.currency_iso4217'),
  ('marca de orden',    U&'\FEFFinstallation.currency_iso4217'),
  ('i ancho completo',  U&'\FF49nstallation.currency_iso4217'),
  ('dígitos ancho comp.',U&'installation.currency_iso\FF14\FF12\FF11\FF17'),
  ('s larga',           U&'in\017Ftallation.currency_i\017Fo4217'),
  ('mayúsculas',        'INSTALLATION.CURRENCY_ISO4217')
) AS a(caso, clave)
LEFT JOIN core.site_settings s ON s.setting_key = a.clave
GROUP BY a.caso ORDER BY a.caso;

-- 2. Unicidad: ningún alias puede existir como fila aparte (se espera 23505).
INSERT INTO core.site_settings (setting_key, setting_value, value_type)
VALUES (U&'installation.curr\00ADency_iso4217', 'USD', 'text');
```

3. Por la API, con sesión de `admin` y de `super_admin`: `PUT /api/admin/settings/<alias codificado en URL>`
   con `{"value":"USD","isPublic":true}` para cada alias efectivo del paso 1. Esperado: `403`; valor e
   `is_public` sin cambios; ningún registro nuevo en `core.audit_log` para la clave; la lectura sigue en
   el mismo estado; ningún bloqueo `advisory` en `pg_locks`.

## 14 · E9 · O-2 ratificada — Pedir la moneda que ya hay no es cambiarla

**Decisión de JP (adenda a R10-E9).** Si la moneda pedida ya está acreditada y coincide exactamente con la
vigente, la operación responde `200` (`Unchanged`) **aunque existan pedidos**. Sustituye, solo en ese
punto, lo dicho en §8 (O-2 de E5.1) y §10: hasta aquí, con pedidos se respondía `409` antes de mirar la fila.

**Cómo queda** (`InstallationCurrencyService.SetAsync`), con el orden de §10 intacto:

| Sonda (bajo el bloqueo) | Fila releída tras `FOR UPDATE` y último comprobante | Resultado |
|---|---|---|
| Sin sonda | — (no se abre transacción) | `409` `RecordsUnverifiable` |
| `Unknown` o falla | — (no se llega a la fila) | `409` `RecordsUnverifiable` |
| `None` o `Present` | igual al pedido **y** acreditado | `200` `Unchanged`: sin escritura, sin acreditación nueva, sin aviso; la transacción se deshace |
| `Present` | sin comprobante válido, o distinto del pedido, o sin fila | `409` `RecordsPresent`, sin escribir |
| `None` | sin comprobante válido | `200` `Reattested` (como en E5.1) |
| `None` | distinto del pedido | `200` `Changed` (como en E5.1) |
| `None` | sin fila | `409` `NotPrepared` |

«Exactamente» es ordinal: `pen` o `PEN ` guardados no coinciden con `PEN`. «Acreditada» es la regla de
§4 y §8: el último registro de la clave, con la forma exacta de `Compose`, dice ese mismo valor.

**Por qué la sonda sigue siendo obligatoria para `Unchanged`.** La adenda autoriza `Unchanged` cuando
existen pedidos, no cuando no se sabe si existen. Sin respuesta firme de Ventas la operación no sigue,
como hasta ahora; eso conserva sin cambios las negativas de E5 y E5.2 (sin sonda, sin tocar la base).

**Pruebas sin PostgreSQL** (`MonedaSinCambioConPedidosTests`, 20): `Unchanged` con y sin pedidos; orden
bajo la barrera (fila y comprobante leídos después del bloqueo y del `FOR UPDATE`); ni `UPDATE`, ni
`INSERT`, ni `COMMIT`, ni auditoría preparada, ni aviso; nueve variantes que escribirían, todas `409`;
idempotencia (tres peticiones, tres `Unchanged`, nada escrito); bloqueo ocupado sin decisión
adelantada; una vista vieja del contexto no da un `Unchanged` falso; un segundo super_admin lee lo
que confirmó el primero; si no se puede leer el comprobante no hay `Unchanged`. Volver a la semántica
anterior hace fallar 5; aceptar igualdad sin comprobante, otras 5.
`BarreraMonetariaTests`: la prueba «con pedidos se rechaza» mantiene `409`, sin escritura y bajo el
bloqueo, pero ahora llega a la fila.

**Reproducción física preparada (NO ejecutada).** Como en §11, con sesiones **V** y **C** y una sonda de
prueba registrada solo en el arnés físico que responda `Present`:

1. Moneda acreditada: `PUT /api/admin/settings/installation-currency {"value":"PEN"}` con sonda `None`
   → `200`, un registro `anterior=PENDIENTE_DEFINIR; nueva=PEN`.
2. Sonda `Present`. Repetir el mismo `PUT` tres veces → `200` con `Accredited`/`PEN` las tres; el número de
   filas de `core.audit_log` para la clave no cambia; `updated_at` de la fila no cambia; `pg_locks` sin
   bloqueo `advisory` al terminar.
3. Con V reteniendo `pg_advisory_xact_lock_shared(1397312588, 1)` más de 5 s, el mismo `PUT` → `409`
   `MonetaryLockBusy`: no se responde `Unchanged` sin la barrera.
4. En otra base recién migrada, sin ningún registro de la clave: `UPDATE` directo de la fila a `PEN`
   (queda `NotAttested`). Sonda `Present`, el mismo `PUT` → `409` `RecordsPresent`; sin registro nuevo.
5. Sonda `Present`, fila en `PENDIENTE_DEFINIR` → `409` `RecordsPresent`; sin cambios.
