# CORE · Entrega 06 — Zona horaria inicial y auditoría obligatoria de exportaciones

**Creación:** 25/10/2026, America/Lima
**Base:** `58bb93a6f3021490a14c9144afd31fc7c02458a0`, sobre los candidatos `a47cead`, `f7ae2ad` y `dfbfaae`
**Escrito por:** Claude Code D, por encargo de Chat 3 vía JP
**Estado:** candidata, **sin certificar** ni integrar. Ninguna migración aplicada.

Refina el `SPEC.md` de CORE dentro de su alcance; el SPEC ya recoge la acción `export` (§4.9) y las
reglas 16 y 17 (§8).

---

## 1 · Lo que JP ratificó (25/10/2026)

1. **America/Lima es la zona horaria inicial** de la instalación, **sin considerarla verificada**.
2. **Toda exportación se audita**, **sin almacenar el contenido exportado**.

Nada más. Esta entrega no amplía ninguna de las dos.

## 2 · Cómo queda en CORE

**Zona horaria** — `IInstallationTimeZoneResolver`:

- sin valor en `installation.timezone_iana` → America/Lima, origen `ApprovedInitial`;
- con un valor que el runtime resuelve → ese, origen `ConfiguredSetting`;
- con un valor inválido → sin zona; **no cae a la inicial**;
- siempre `Unverified`.

**Exportaciones** — `IExportAudit`, acción `export`:

- se registra antes de entregar; sin registro, no se entrega;
- metadatos mínimos:
  - autor tomado de `ICurrentAdmin` (id y correo);
  - `module_code`;
  - tipo de exportación en `entity_type`;
  - identificador uuid v7 generado por CORE en `entity_id`;
  - resumen compuesto por CORE con formato y número de filas;
- `occurred_at` e `ip_address` los pone el escritor de auditoría;
- ningún campo de texto libre: ni filtros, ni nombre de archivo, ni contenido.

Los productores son opcionales (`AddCandidateInstallationTimeZone()`, `AddCandidateExportAudit()`):
`CoreServices` no los registra.

## 3 · Pendiente

- **Verificación autoritativa de zonas.** No existe autoridad. `InstallationTimeZoneVerification.Verified`
  e `IanaZoneOutcome.IanaCanonicalVerified` siguen reservados y nada los produce.
- **Migración `CoreAuditExport`** (`20261023192531`), generada y **no aplicada**. Hasta aplicarla, el
  CHECK `ck_audit_log_action` rechaza `export` y toda exportación sale `AuditUnavailable`. Su `Down`
  aborta si ya hay filas `export`.
- **Clave de la zona** (`installation.timezone_iana`): nombre, quién la escribe y visibilidad. La
  zona inicial no necesita fila ni semilla.
- **Registro de los productores en el host**: puerta de JP.
- **Hueco previo, fuera de esta entrega:** `email_send` existe en `AuditAction` pero no figura en la
  lista de acciones del §4.9.
