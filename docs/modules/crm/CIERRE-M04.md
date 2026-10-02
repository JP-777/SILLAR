# M04 Clientes — CERRADO

**Fecha de cierre:** 2 de octubre de 2026, America/Lima
**Versión cerrada:** `1.2.0` (`backend/Sillar.Modules.Crm/CrmModule.cs:62`)
**Commit de `main` sobre el que se registra:** `c1dcc5bd42a069eaf0e1259e71f54d605264759e`
**Escrito por:** Claude Code D, como escritor único de costuras, a petición de Chat 2 vía JP.

Este documento **reúne** los tres hitos del módulo. No sustituye a ninguno de los registros
históricos, que se conservan sin editar:

- `CIERRE-M04-PROPUESTA.md` sigue siendo lo que fue: una **propuesta** de cierre del 26/09/2026,
  pendiente de aprobación en su momento. No se reescribe como si hubiera sido un cierre.
- `SPEC.md`, `BITACORA-M04.md`, `CONTRATO-SNAPSHOT.md`, `CONTRATO-IDENTIDAD.md` y las
  evidencias de `evidencias/` siguen siendo la fuente de cada hito.

---

## Hito 1 — El módulo original

| | |
|---|---|
| **Candidato** | `9f9015b4186a55f9f305222b320477e79aea7028` |
| **Criterios** | 19/19 con evidencia (`CIERRE-M04-PROPUESTA.md`, tabla de los 19 criterios) |
| **Puertas canónicas** | Dos consecutivas sobre el mismo SHA: **6/6 PASS + 6/6 PASS** (`evidencias/M04-PUERTA-1-20260926.txt:7`, `evidencias/M04-PUERTA-2-20260926.txt:7`) |
| **Registro e integración documental** | `711bfba7cf3be80baa146b44e79ddf7a633d695d` («acredita doble puerta y propone cierre M04») |

Lo que ese hito dejó escrito es una **propuesta**. El cierre formal es este documento.

## Hito 2 — Contrato 1.1.0: instantánea para recojo

Se añade a `ICustomerSnapshotReader` una segunda variante, sin dirección
(`backend/Sillar.Modules.Crm.Contracts/ICustomerSnapshotReader.cs:41`):

```csharp
Task<CustomerOrderContactSnapshot?> GetForOrderAsync(
    Guid customerId,
    CancellationToken cancellationToken);
```

- **Permite un pedido sin dirección** (recojo): devuelve los datos del cliente sin exigir que
  tenga ninguna dirección.
- **No vuelve opcional la dirección del contrato con entrega.** La variante 1.0.0 sigue intacta y
  sigue exigiendo `Guid customerAddressId` (`ICustomerSnapshotReader.cs:28`). Son dos
  métodos, no un parámetro opcional: quien entrega a domicilio sigue sin poder pedir una
  instantánea sin dirección.
- Detalle del contrato: `CONTRATO-SNAPSHOT.md`.

**Certificación local** sobre `37c0fbfcc6162adca9eb4604884d077c27662ab9` (candidata `4848f18`
más el arreglo del arnés H03), por el frente B en el equipo de JP. Evidencia publicada en
`aa3852d2572e965f973ae5eb3dfa210612ffe04b`, rama `qa/m04-37c0fbf-evidencias`
(`docs/modules/crm/evidencias/QA-B-M04-INDICE-20260930.md` en ese commit). Ese commit **no está
en `main`**; se cita por su SHA.

| Comprobación | Resultado |
|---|---|
| Puerta canónica | **6/6 PASS**, `rc=0` |
| E2E | **166/166**, 0 omitidas |
| `[M04-CICLO]` (`e2e/tests/zz-z-m04-ciclo.spec.ts:302`) | **PASS** |

Los intentos rojos previos sobre `4848f18` están declarados en esa misma evidencia, no se ocultan.

## Hito 3 — Versión 1.2.0: identidad mínima del cliente

| | |
|---|---|
| **Candidata** | `defebafe207b75da08c35a5907880f97abfab320`: un solo commit sobre `37c0fbf` con el contrato, su implementación, sus pruebas, el registro DI y `Version = "1.2.0"` |
| **Evidencia integrada** | `c1dcc5bd42a069eaf0e1259e71f54d605264759e` (`evidencias/QA-M04-1.2.0-INDICE-20261002.md`) |

| Comprobación | Resultado |
|---|---|
| Puerta canónica | **6/6 PASS**, `rc=0` (`evidencias/QA-M04-1.2.0-PUERTA-20261002.txt`) |
| Backend | **479/479** |
| E2E | **166/166** |
| **Total** | **645 PASS · 0 FAIL · 0 SKIPPED** |
| `OMITIDAS_ESPERADAS` | vacía |

**Delta contractual**, los tres métodos ejecutados **contra PostgreSQL real en esa misma
corrida** (índice de la evidencia, sección «Delta contractual»):

| Método | Ejecutado contra PostgreSQL |
|---|---|
| `ICustomerSnapshotReader.GetForOrderAsync(customerId, ct)` (`ICustomerSnapshotReader.cs:41`) | **SÍ** |
| `ICustomerIdentityReader.GetAsync` (`ICustomerIdentityReader.cs:24`) | **SÍ** |
| `ICustomerIdentityReader.GetManyAsync` (`ICustomerIdentityReader.cs:36`) | **SÍ** |

Detalle del contrato de identidad: `CONTRATO-IDENTIDAD.md`.

---

## El hueco histórico: ciclo de esquema frente a ciclo de módulo

**Registrado el 27/09/2026.**

**Qué pasó.** El cierre propuesto había aceptado, para el criterio 16 («Se desinstala M04 y
**CORE y el catálogo siguen enteros**; se reinstala y arranca», `CIERRE-M04-PROPUESTA.md:37`),
evidencia de un **ciclo de esquema**: `Test12_eliminar_schema_crm_no_toca_core_ni_catalog` y
`Test13_reinstalar_crminitial_sobre_schema_limpio_funciona`
(`backend/Sillar.Modules.Crm.Tests/CrmPersistenceTests.cs:333` y `:414`). El significado real
del criterio, a la luz del criterio de terminado de `CLAUDE.md`, era un **ciclo de módulo**:
desactivar desde la plataforma, desinstalar, reinstalar y activar, comprobando lo que ve una
persona y que el resto sigue en pie.

**Cómo se corrigió**, sin reescribir ningún «cerrado» ni ningún «propuesto» histórico:

1. `docs/PENDIENTES.md` §28 (`:473`) registró la distinción entre las dos evidencias.
2. D añadió `e2e/tests/zz-z-m04-ciclo.spec.ts`, el ciclo real del módulo, con sus controles
   negativos.
3. B acreditó ese ciclo en local sobre `37c0fbf` (hito 2: `[M04-CICLO]` PASS).
4. La suite final de M04 1.2.0 volvió a pasar completa (hito 3: 645 PASS, 0 omitidas).

**Lo que esto significa, y lo que no.** La evidencia anterior del criterio 16 era
**incompleta**; eso no demuestra que el producto fuera defectuoso. Cuando el ciclo real se
escribió y se ejecutó, el módulo lo superó. **Lo que se corrigió fue la evidencia del criterio,
no el producto.**

---

## Consecuencias

- **M03 deja de estar bloqueado por M04.** El contrato que pidió (instantánea sin dirección) está
  en `main` y certificado.
- M07 dispone de `ICustomerIdentityReader` para su bandeja.
- Lo que queda abierto fuera de M04 no se resuelve aquí: C15 (M01), las costuras de M03/M07 y la
  corrección de CORE siguen en sus colas.
