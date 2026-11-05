# M03 · Costura con la autoridad monetaria de CORE

**Creación:** 04/11/2026, America/Lima
**Encargo:** P1-POST-G6-R10-E6.1 (Chat 3), con D1–D8 y N1–N5 ratificadas por JP
**Estado:** candidata, **sin acreditación física**. HOLD MAIN vigente.
**Contraparte:** CORE `ENTREGA-07-MONEDA-DE-LA-INSTALACION.md` §10–§12 (E5.2, `05938966`)

## Qué pone M03

| Pieza | Dónde | Qué hace |
|---|---|---|
| Adaptador de lectura | `Pedidos/AutoridadDeMoneda.cs` → `AutoridadDeMonedaDeCore` | Devuelve la moneda solo si `IInstallationCurrencyAuthority` dice `Accredited`. Cualquier otro estado da `null`, y el resultado es `MonedaSinAutoridad` (503), como en D20-A. Nunca lee `currency_code`, `currency_symbol` ni la caché |
| Contrato interno | `IAutoridadDeMoneda.MonedaDeVentaAsync` | Ahora es asíncrono, porque la lectura de CORE lo es. Lo consumen `CreadorDePedidos` y los dobles de prueba. `SinAutoridadDeMoneda` queda como ausencia explícita y deja de estar registrada |
| Barrera | `Data/BarreraMonetariaDeVentas.cs` | `MonetaryAuthorityLock.AcquireShared()` dentro de la transacción del pedido, con `lock_timeout` de 5 s solo para esa sentencia (luego se restaura el anterior) |
| Sonda | `Pedidos/SondaDeRegistrosMonetarios.cs` | `IMonetaryRecordsProbe`. Hace `EXISTS` sobre **todo** `sales.orders`. Responde `None` o `Present`; `Unknown` ante cualquier fallo o si hay una transacción ya abierta en su contexto. La cancelación se propaga. No pide el bloqueo |
| Registro | `SalesModule.cs` | `IAutoridadDeMoneda → AutoridadDeMonedaDeCore` y `IMonetaryRecordsProbe → SondaDeRegistrosMonetarios`, las dos *scoped* |
| Motivo nuevo | `MotivoDeNoCreacion.MonedaEnCambio` | El bloqueo no llegó a tiempo porque CORE estaba cambiando la moneda. 503 con una frase propia |

## Orden en `CreadorDePedidos`

1. Rechazo rápido. Una primera lectura de la moneda, **solo para rechazar**: sin moneda no se abre ninguna transacción (D20-A). Esa moneda no se usa.
2. Cliente y artículos. Lecturas de M04 y M01, fuera de la transacción, como antes.
3. `BEGIN`, o unirse a la transacción del llamador.
4. **Bloqueo compartido**: el primer bloqueo de la transacción. Si vence (`55P03`), devuelve `MonedaEnCambio` y deshace.
5. **Lectura auténtica** de la moneda, con el bloqueo puesto. Si no está acreditada, devuelve `MonedaSinAutoridad` y deshace.
6. Líneas, asiento, número (`OrderCodeAllocator`), inserción, `COMMIT`. El bloqueo se suelta solo.

## Escritores de `sales.orders`

- Solo `CreadorDePedidos` inserta, y es el único que congela una moneda.
- `VencimientoDePlazos` cambia el estado de pedidos que ya existen y no toca la moneda.
- Lo vigilan las pruebas de `CosturaMonetariaTests` (sección «escritores»).

## Lo que no está acreditado

- La exclusión mutua con PostgreSQL físico. Las pruebas de `CosturaMonetariaPersistenciaTests` están preparadas y **no se han ejecutado**.
- La composición del Host. `NegativeCommerceIntegration` sigue declarando `SinAutoridadDeMoneda`, singleton, y no conoce la sonda; ver el inventario de pendientes del expediente R10-E6.1.
