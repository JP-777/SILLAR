# Contrato de instantánea de cliente — M04 · versión 1.1.0

**Creación:** 27 de septiembre de 2026 — America/Lima
**Última modificación:** 27 de septiembre de 2026 — America/Lima
**Última verificación:** 27 de septiembre de 2026 — America/Lima
**Commit verificado:** `31089971a0a9aa334d533adeacb8ec26a5bf30ff` (rama `integration/m04-contrato-snapshot`, sobre `main` = `e839989432283c755edf7d4ae47b2c37697215ec`)
**Destinatario:** Frente A (M03 Ventas Online)

Este documento **no sustituye al SPEC de M04** (`SPEC.md` §7, que se conserva sin editar). Fija la
forma exacta del contrato que M03 puede consumir y qué garantiza. El código es la fuente de verdad:
`backend/Sillar.Modules.Crm.Contracts/ICustomerSnapshotReader.cs`.

---

## 1 · Qué cambia y qué no

| | 1.0.0 | 1.1.0 |
| --- | --- | --- |
| `GetForOrderAsync(customerId, customerAddressId, ct)` → `CustomerOrderSnapshot?` | ✅ | ✅ **sin cambios**: firma, record y consulta idénticos |
| `GetForOrderAsync(customerId, ct)` → `CustomerOrderContactSnapshot?` | — | ✅ **nuevo** |
| `CustomerOrderContactSnapshot` | — | ✅ **nuevo** |
| `CrmModule.Version` | `1.0.0` | `1.1.0` |

**Por qué una sobrecarga y no una dirección opcional.** Hacer `Address` anulable en
`CustomerOrderSnapshot` convertiría el camino con entrega en permisivo sin avisar: un consumidor que
hoy lee `snapshot.Address.AddressLine` pasaría a recibir `null` donde antes el contrato le
garantizaba una dirección. Con dos métodos, **el tipo de retorno dice si hay dirección**, y el
compilador impide leer una dirección que no existe.

## 2 · Firmas exactas

Espacio de nombres `Sillar.Modules.Crm.Contracts`, proyecto `Sillar.Modules.Crm.Contracts`.

```csharp
public interface ICustomerSnapshotReader
{
    // Pedido CON entrega. Dirección obligatoria.
    Task<CustomerOrderSnapshot?> GetForOrderAsync(
        Guid customerId,
        Guid customerAddressId,
        CancellationToken cancellationToken);

    // Pedido SIN entrega (recojo en tienda). Desde 1.1.0.
    Task<CustomerOrderContactSnapshot?> GetForOrderAsync(
        Guid customerId,
        CancellationToken cancellationToken);
}

public sealed record CustomerOrderContactSnapshot(
    Guid CustomerId, string FullName, string Email, string? Phone,
    string? DocumentType, string? DocumentNumber, bool EmailVerified);

public sealed record CustomerOrderSnapshot(
    Guid CustomerId, string FullName, string Email, string? Phone,
    string? DocumentType, string? DocumentNumber, bool EmailVerified,
    CustomerOrderAddressSnapshot Address);

public sealed record CustomerOrderAddressSnapshot(
    Guid CustomerAddressId, string? Label, string AddressLine,
    string? District, string? Province, string? Department, string? Reference);
```

`DocumentType`, cuando existe, es `"dni"` o `"ruc"` (`ck_customers_document_type`).

## 3 · Cuándo devuelve `null`

Las dos variantes devuelven `null` **sin distinguir el motivo**. M03 no debe intentar deducirlo.

| Situación | Sin dirección | Con dirección |
| --- | --- | --- |
| Cliente inexistente | `null` | `null` |
| Ficha de baja (`deactivated_at`) | `null` | `null` |
| Ficha bloqueada (`blocked_at`) | `null` | `null` |
| Ficha sin cuenta (cliente de mostrador) | `null` | `null` |
| Dirección inexistente | — | `null` |
| Dirección de baja | — | `null` |
| Dirección de **otro** cliente | — | `null` |
| Cliente válido sin ninguna dirección | **instantánea** | `null` para cualquier `customerAddressId` |

**La variante con dirección nunca cae en la sin dirección.** Si la dirección no vale, devuelve
`null`; no devuelve los datos del cliente «por si acaso».

**La cuenta es obligatoria** en las dos, igual que en 1.0.0: comprar en línea la exige
(`ROADMAP_MODULAR.md`, fila de M03). `EmailVerified` refleja la cuenta; decidir si un correo sin
verificar puede comprar es regla de M03 (`SPEC.md` de M04 §6 bis: «comprar, no»).

## 4 · Lo que garantiza

- **Nada interno cruza la frontera.** `InternalNotes` no se selecciona y ningún record tiene
  propiedades de contraseña o notas (`CustomerProfileContractTests`).
- **Inmutable.** Los tres records solo tienen propiedades `init`: una instantánea no se puede
  modificar después de creada (`Las_instantaneas_no_se_pueden_modificar_despues_de_creadas`).
- **Es una foto, no una referencia viva.** Editar después el perfil o la dirección no altera una
  instantánea ya tomada; una lectura nueva devuelve el estado actual. **M03 debe guardar su propia
  copia** en su esquema (snapshot en tabla transaccional, `CLAUDE.md`).
- **Sin dependencias nuevas.** M04 no conoce a M03, y no hay ninguna FK nueva entre módulos.

## 5 · Uso desde M03

M03 declara a M04 como **dependencia dura** (`ARQUITECTURA_MODULAR.md`) y referencia **solo**
`Sillar.Modules.Crm.Contracts`: nunca `Sillar.Modules.Crm` ni su `Data` o `Domain`
(`CLAUDE.md`, regla 3 de módulos).

```csharp
// Recojo en tienda (M03 v1)
var cliente = await snapshots.GetForOrderAsync(currentCustomer.CustomerId!.Value, ct);
if (cliente is null)
{
    return Results.Conflict(/* la cuenta no puede comprar; frase que dice qué hacer */);
}
// Guardar en sales.orders: cliente.FullName, cliente.Email, cliente.Phone,
// cliente.DocumentType, cliente.DocumentNumber — copia, no referencia.

// Con entrega (cuando exista): la dirección es obligatoria
var conEntrega = await snapshots.GetForOrderAsync(customerId, customerAddressId, ct);
```

`ICurrentCustomer.CustomerId` (mismo proyecto de contratos) da el cliente de la sesión.

## 6 · Pruebas que lo sostienen

`backend/Sillar.Modules.Crm.Tests/CustomerSnapshotReaderTests.cs` (16 casos) y
`CustomerProfileServiceTests.Snapshot_para_M03_usa_solo_datos_publicables_del_cliente` (1.0.0, sin
tocar). Corren contra la base efímera de la puerta, etapa `[5/6]`.

**Falsificación:** cada guarda se rompió a propósito y su prueba se puso en rojo. Registro:
`evidencias/CONTRATO-1.1-SABOTAJES-20260927.txt`.

| Sabotaje | Pruebas en rojo |
| --- | --- |
| Sin dirección: se quita la guarda de ficha activa | 2 (baja, bloqueo) |
| Sin dirección: se quita la guarda de cuenta | 3 |
| Con dirección: la dirección deja de atarse al cliente | 1 (dirección ajena) |
| Con dirección: se acepta una dirección de baja | 1 |

## 7 · Compatibilidad — qué vuelve a correr y dónde

| Dónde | Qué |
| --- | --- |
| **M04, esta rama** | Las 112 pruebas de `Sillar.Modules.Crm.Tests`, incluidas `Test12`/`Test13` (borrar y reinstalar el **esquema** `crm` sin tocar `core` ni `catalog`); `e2e/tests/zz-z-m04-ciclo.spec.ts` (desactivar, desinstalar, reinstalar y activar el **módulo** en la aplicación, con controles negativos); y la puerta canónica completa |
| **M03, integración de A** | Que su proyecto referencia solo `Sillar.Modules.Crm.Contracts` (barrera de módulos); que el pedido de recojo usa la variante sin dirección y **guarda copia**; que un `null` produce un conflicto explicado y no un pedido; que un correo sin verificar no compra; y el ciclo instalar/desinstalar M03 sin romper M04 |
