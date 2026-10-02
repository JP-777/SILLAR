# Contrato de identidad mínima del cliente — M04

**Creación:** 30 de septiembre de 2026 — America/Lima
**Última modificación:** 30 de septiembre de 2026 — America/Lima
**Última verificación:** 30 de septiembre de 2026 — America/Lima
**Commit verificado:** el de este documento, en la rama `integration/crm-identidad-cliente`, sobre `main` = `e839989432283c755edf7d4ae47b2c37697215ec`
**Destinatario:** M07 Solicitudes B2B (bandeja del panel)

Costura independiente. No sustituye al `SPEC.md` de M04, que no se toca. El código es la fuente de
verdad: `backend/Sillar.Modules.Crm.Contracts/ICustomerIdentityReader.cs`.

---

## 1 · Para qué existe

Los DTO del panel B2B guardan `CustomerId`, pero una persona no reconoce un identificador, y el
diseño no debe inventar un nombre ni un correo. M07 necesita **quién es** el cliente, no un snapshot de
pedido.

**Por qué no se reutiliza `ICustomerSnapshotReader`.** Esa interfaz es la instantánea de pedido de M03:
exige `customerAddressId` y cuenta, y se congela al comprar. Forzarla aquí obligaría a inventar una
dirección, y mezclaría dos propósitos en un solo contrato.

## 2 · Firma exacta

Espacio de nombres `Sillar.Modules.Crm.Contracts`, proyecto `Sillar.Modules.Crm.Contracts`.

```csharp
public interface ICustomerIdentityReader
{
    Task<CustomerIdentity?> GetAsync(Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, CustomerIdentity>> GetManyAsync(
        IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken);
}

public sealed record CustomerIdentity(Guid CustomerId, string FullName, string Email, string? Phone);
```

| Situación | `GetAsync` | `GetManyAsync` |
| --- | --- | --- |
| Ficha activa, con o sin cuenta | identidad | presente |
| Ficha inexistente | `null` | ausente |
| Ficha de baja o bloqueada | `null` | ausente |
| Id repetido | — | una sola entrada |
| Colección vacía | — | diccionario vacío, sin consulta |

- **Solo lectura.** No hay ningún método de escritura.
- **No pide dirección ni cuenta.** Una ficha de mostrador también tiene identidad.
- **Dato vigente, no congelado.** Cada lectura devuelve el estado actual de la ficha.
- **No expone** notas internas, documento, dirección, estado de la cuenta ni nada de la
  autenticación.

## 3 · Uso desde M07

Solo desde el backend. En la rama de M07 (`m07-b2b-sobre-main`, `7d002d0`), `Sillar.Modules.B2B.csproj`
ya referencia `Sillar.Modules.Crm.Contracts` y nada más de CRM. El frontend de M07 recibe el dato ya proyectado en sus DTO; nunca llama a CRM.

```csharp
// Listado: una sola consulta para toda la página.
var ids = solicitudes.Select(s => s.CustomerId).ToArray();
var identidades = await identity.GetManyAsync(ids, ct);

var fila = identidades.TryGetValue(s.CustomerId, out var cliente)
    ? new { cliente.FullName, cliente.Email }
    : null; // inexistente o inactiva: el DTO lo dice, no lo inventa
```

Cómo presenta M07 una ficha que ya no está activa es decisión de M07 y de su diseño. El contrato
solo garantiza que no la devuelve.

## 4 · Pruebas

`backend/Sillar.Modules.Crm.Tests/CustomerIdentityReaderTests.cs` (12 casos) y
`CustomerProfileContractTests` con el record nuevo. En el diagnóstico no canónico: **108/108** en
`Sillar.Modules.Crm.Tests`.

Sabotajes (`evidencias/IDENTIDAD-SABOTAJES-20260930.txt`):

| Sabotaje | Pruebas en rojo |
| --- | --- |
| Individual sin la guarda de ficha activa | 2 |
| Por lotes sin la guarda de ficha activa | 1 |
| El record gana `InternalNotes` | 3 |
| Por lotes sin `Distinct()` | 0 — **no es una guarda**: `IN (...)` ya devuelve cada fila una vez |
