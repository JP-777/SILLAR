# CORE · Fase A — costuras con M10, M11 y M12

- **Estado:** candidato, sin integrar. Nada de esto se registra en `CoreServices`.
- **Parte de:** `a47cead` (candidato reconciliado N.º 3).
- **Fecha:** 24/10/2026 — America/Lima

Este documento separa lo que CORE ya puede ofrecer, con hechos que existen, de lo que está
bloqueado. Lo bloqueado se queda bloqueado: aquí no se resuelve, se nombra.

---

## Lo que CORE ofrece

| Costura | Contrato | Productor (opcional) |
|---|---|---|
| Procedencia de la instalación (M10; sello para M11 y M12) | `IInstallationProvenance` | `AddCandidateInstallationProvenance()` |
| Identidad y acceso al Asistente (M12) | `IAssistantAccessAuthority` | `AddCandidateAssistantAuthority()` |
| Auditoría de exportación (M10, fail-closed) | `IExportAudit` | `AddCandidateExportAudit()` opt-in; `AuditAction.Export` no está admitida y `CoreAuditExport` sigue diferida |

La procedencia solo se emite con todos sus hechos: una instalación, con clave no vacía, con la
configuración inicial terminada, y con `Sillar:Node:Code` configurado e igual al nodo que el
proceso estampa en `origin_node`. El nodo por defecto (`principal`) no cuenta. Es procedencia
**observada**: no está firmada.

---

## Bloqueos

### M11 — origen autorizado de `OrderId` y evidencia persistida

**Bloqueado. No es de CORE, y M11 está en HOLD.**

- `OrderId` solo existe en los eventos de M03 (`Sillar.Modules.Sales.Contracts/Events/SalesEvents.cs:9`,
  `:26`, `:43`). Ningún contrato de M03 permite a otro módulo resolver un pedido y su importe
  autoritativo, que es lo que exige D-M11-04.
- `OrderPayment` no puede representar un pago de origen externo: `RegisteredBy` es obligatorio
  (`Sillar.Modules.Sales/Domain/OrderPayment.cs:59`) y `RegisteredByAdminUserLocalId` es un entero
  no anulable (`:71`). A-M11-02 exige cero atribución humana más identidad externa, y
  `external_settlement_id` no existe en el código.
- `DECISIONES-PREVIAS-M11.md:228`: no continuar a código sin nueva autorización.

**Qué lo desbloquea:** el propietario de M03 publica el contrato de pedido para cobro y la
evolución de `OrderPayment` (A-M11-02, A-M11-03), y llega la autorización de M11. CORE solo aporta
el sello de procedencia para la evidencia.

### M12 — control CAS de la política de cuotas (`policy_version`)

**Bloqueado. Sin semántica ratificada y sin dueño en CORE.**

- `policy_version` no aparece en ningún archivo del repositorio. La política de cuotas vivirá en el
  schema `assistant`, que es de M12: CORE no puede escribirla (regla 2 de módulos).
- La semántica transaccional del compare-and-swap no está ratificada.

### M12 — autorización y revocación de permisos administrativos

**Bloqueado por ADR-010.**

- Los permisos granulares por módulo están diferidos «hasta que exista un caso real»
  (`docs/adr/ADR-010-autenticacion-administrativa.md:41`; `Sillar.Core.Contracts/AdminRole.cs:5`).
- Lo que ya existe: el rol y el estado activo se leen de la base en cada petición
  (`Sillar.Core/Authentication/AdminSessionAuthenticationHandler.cs:99`, `:112`). Una revocación
  de rol o una desactivación surte efecto en la petición siguiente. Dentro de una misma petición,
  `ICurrentAdmin` es una foto tomada al autenticar.

**Qué lo desbloquea:** que JP decida si M12 es ese «caso real» de ADR-010 y con qué forma.

### M10 — más allá del sello

No hay SPEC de M10 en `docs/modules/`. El origen de los datos de cada informe —qué eventos o qué
lecturas— depende de él.

---

## Fase B — M10 (25/10/2026)

Decisiones de JP: **America/Lima como zona inicial, sin considerarla verificada**, y **auditoría
obligatoria de exportaciones, sin almacenar el contenido exportado**.

| Costura | Contrato | Productor (opcional) |
|---|---|---|
| Zona horaria de la instalación | `IInstallationTimeZoneResolver` | `AddCandidateInstallationTimeZone()` |
| Auditoría obligatoria de exportaciones | `IExportAudit` | `AddCandidateExportAudit()` |

- **Zona:** sin valor guardado → America/Lima (`ApprovedInitial`). Con un valor que el runtime
  resuelve → ese (`ConfiguredSetting`). Con un valor inválido → **sin zona**: no se cae a la inicial.
  Siempre `Unverified`: no hay autoridad de verificación.
- **Exportación:** se registra **antes** de entregar, y sin registro no se entrega. Metadatos
  mínimos: autor (de `ICurrentAdmin`), `module_code`, tipo de exportación en `entity_type`,
  identificador uuid v7 generado por CORE en `entity_id`, y un resumen compuesto por CORE con formato
  y filas. Ningún campo de texto libre.

### Bloqueos restantes

- **Migración `CoreAuditExport` sin aplicar.** Hasta que se aplique, el CHECK `ck_audit_log_action`
  rechaza `export` y toda exportación se rechaza como `AuditUnavailable`. La autorización física
  vigente no cubre CORE.
- **SPEC de CORE.** `docs/modules/core/SPEC.md:255` no lista `export`; falta la enmienda por
  entrega, como se hizo con `email_send`.
- **Clave de la zona** (`installation.timezone_iana`): nombre, quién la escribe y visibilidad siguen
  pendientes. La zona inicial no necesita fila ni semilla.
- **Registro en el host:** los productores son opcionales; integrarlos es la puerta de JP.
