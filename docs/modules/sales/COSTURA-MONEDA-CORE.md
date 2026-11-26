# M03 · Costura con la autoridad monetaria de CORE

**Creación:** 04/11/2026, America/Lima · **Revisión:** 06/11/2026 (R10-E13-B)
**Encargos:** P1-POST-G6-R10-E6.1 (D1–D8 y N1–N5) y P1-POST-G6-R10-E13-B (D1–D5 de R10-E13), ratificados por JP
**Estado:** candidata, **sin acreditación física**. HOLD MAIN vigente.
**Contraparte:** CORE `ENTREGA-07-MONEDA-DE-LA-INSTALACION.md` §10–§12 (E5.2) y §15 (E13-A, `IInstallationCurrencyTransactionalAuthority`)

## Qué pone M03

| Pieza | Dónde | Qué hace |
|---|---|---|
| Adaptador | `Pedidos/AutoridadDeMoneda.cs` → `AutoridadDeMonedaDeCore` | Dos lecturas. `MonedaDeVentaAsync` usa `IInstallationCurrencyAuthority` (conexión de CORE) y **solo sirve para rechazar** antes de abrir nada. `LeerEnTransaccionAsync` usa `IInstallationCurrencyTransactionalAuthority` sobre la transacción del pedido: es la que se congela. Nunca lee `currency_code`, `currency_symbol`, la caché ni tablas de CORE |
| Estados | `EstadoDeMonedaDeVenta` | `Acreditada`, `SinAutoridad` (CORE dice que no hay), `NoDisponible` (no se pudo leer: `Unavailable` o fallo), `UsoIndebido` (CORE rechazó la transacción por contrato; se registra como error, evento `31301`) |
| Barrera | `Data/BarreraMonetariaDeVentas.cs` | `MonetaryAuthorityLock.AcquireShared()` dentro de la transacción del pedido, con `lock_timeout` de 5 s solo para esa sentencia |
| Sonda | `Pedidos/SondaDeRegistrosMonetarios.cs` | `IMonetaryRecordsProbe` sobre **todo** `sales.orders`. `None`, `Present` o `Unknown`. No pide el bloqueo |
| Clasificador | `Pedidos/FallosDeInfraestructura.cs` | Qué fallo es «el bloqueo no llegó» (`55P03`) y cuál «no se puede ahora» (`IsTransient` de Npgsql), vengan **sin envolver o envueltos por EF Core** en `InvalidOperationException` |
| Registro | `SalesModule.cs` | Sin cambios desde E6.1: `IAutoridadDeMoneda → AutoridadDeMonedaDeCore` e `IMonetaryRecordsProbe → SondaDeRegistrosMonetarios`, *scoped*. Los dos lectores de CORE los registra CORE |

## Orden en `CreadorDePedidos`

1. **Rechazo rápido** con la conexión de CORE. Ventas todavía no retiene ninguna. Sin moneda acreditada no se abre nada (D20-A); si no se pudo leer, tampoco, pero el motivo es otro.
2. Cliente y artículos: lecturas de M04 y M01, fuera de la transacción. Después, **los ajustes**: plazo de pago y etiqueta de serie (`sales.order_series_label`), también fuera. La etiqueta se valida con la regla del numerador (`OrderCodeAllocator.EtiquetaValida`): sin ella no se abre transacción, no se toma el bloqueo y no se consume número. Queda congelada para ese pedido; el siguiente lee la vigente al empezar el suyo (R10-E13-D, R-1).
3. Transacción: propia en `READ COMMITTED` explícito, o la del llamador si su aislamiento declarado es `ReadCommitted` o sin especificar. Con otro, se rechaza **sin tomar el bloqueo** y sin tocarla.
4. **Bloqueo compartido**, primer bloqueo de la transacción.
5. **Lectura acreditada en la misma transacción y conexión** (`Database.CurrentTransaction.GetDbTransaction()`). CORE comprueba ahí aislamiento real, base y bloqueo concedido.
6. Líneas, asiento, número, inserción, `COMMIT`. Un fallo transitorio antes del `COMMIT` es `ServicioNoDisponible`; en el `COMMIT`, no.

**Desde el paso 4 hasta el 6 no se pide ninguna otra conexión al pool** (D-1, R10-E12). Ya no hay segunda llamada a `MonedaDeVentaAsync` bajo el bloqueo, y el numerador recibe la etiqueta como argumento: no consulta la caché de configuración, que al recargarse abre su propio ámbito y su propia conexión (R-1). Lo vigilan `SerieCapturadaAntesDelBloqueoTests`.

## Respuestas HTTP de `POST /api/sales/orders`

| Motivo | Cuándo | HTTP | Frase |
|---|---|---|---|
| `MonedaSinAutoridad` | CORE dice que no hay moneda acreditada | 503 | «Falta configurar la moneda en que cobra la tienda…» |
| `MonedaEnCambio` | El bloqueo no llegó en 5 s (`55P03`) | 503 | «La tienda está actualizando la moneda…» |
| `ServicioNoDisponible` | No se pudo leer la moneda, o la base no dio conexión o la cortó (fallo transitorio) al abrir, al tomar el bloqueo, al leer, al numerar o al guardar, siempre **antes** del `COMMIT` | 503 | «No pudimos confirmar tu pedido en este momento…» — no nombra la moneda |
| `CosturaMonetariaDefectuosa` | Uso fuera de contrato (aislamiento, bloqueo, base, transacción) | 500 | «No pudimos registrar tu pedido…» — ningún detalle interno |

Si el servidor cierra la sesión antes del `COMMIT`, Npgsql desecha la transacción y deshacerla al salir lanza `ObjectDisposedException`. `TransaccionDeOperacion` tolera solo ese caso, cuando el motivo que Npgsql guardó es una conexión rota, y lo registra con el evento `31303`: la causa original y su 503 se conservan. Cualquier otro fallo al deshacer se propaga (R10-E13-G, D-3).

En los cuatro casos el pedido no nace y no se consume número. Un fallo **en** el `COMMIT` no se traduce: no se sabe si el pedido quedó, y pedir que se vuelva a confirmar podría duplicarlo. Una transacción prestada nunca se confirma ni se deshace aquí: si queda abortada, la deshace su dueño.

## Escritores de `sales.orders`

- Solo `CreadorDePedidos` inserta, y es el único que congela una moneda.
- `VencimientoDePlazos` cambia el estado de pedidos que ya existen y no toca la moneda.
- Lo vigilan las pruebas de `CosturaMonetariaTests` (sección «escritores»).

## Lo que no está acreditado

- Nada de lo físico. `CosturaMonetariaPersistenciaTests` (14) y `UnaConexionBajoElBloqueoPersistenciaTests` (8, con los lectores reales de CORE y pools de 1 y 2 conexiones) están preparadas y **no se han ejecutado** en esta entrega.
- La primera acreditación concurrente y la ausencia de sonda se prueban por la API real (escenarios del expediente R10-E13-B), porque la escritura de CORE no tiene contrato público.
