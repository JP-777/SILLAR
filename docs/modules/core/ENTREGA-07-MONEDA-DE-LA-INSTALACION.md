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
