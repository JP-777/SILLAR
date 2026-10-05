# Escaladas — M05a

- **Creación / última modificación / última verificación:** 28/09/2026 · America/Lima
- **Base verificada:** `e839989432283c755edf7d4ae47b2c37697215ec`; actualizada sobre `main` = `35647181891a9b78a7399d3b108d9a4415a0d48a` el 04/10/2026
- **Commit de implementación verificado:** `c30c9666534edd4ddaa570870f3e992fe2552c30`

## E1 · Supervivencia del binario fotográfico

- **Pregunta:** ¿qué fotografía y metadatos deben sobrevivir en el historial de M05b si CORE retira el medio?
- **Evidencia:** `IMediaStorage.GetPublicUrl` puede devolver nulo para un medio inactivo; copiar la URL no copia el archivo.
- **Alternativas:** retención lógica en CORE; copia binaria propiedad de M05b; snapshot textual sin garantía del binario.
- **Dueño:** JP y líder técnico con la futura SPEC de M05b.
- **Disparador:** antes de implementar persistencia de snapshots en M05b.

## E2 · Operaciones editoriales todavía abiertas

Opciones/presentaciones y restauración desde Archivado no se implementan. Requieren decisión de producto si se vuelven visibles.

## E3 · Costuras compartidas para Integración / frente D

Se solicita turno acotado para: referencia del host API al proyecto M05a, inclusión de proyectos en `backend/Sillar.sln`, descubrimiento modular, vocabulario de auditoría y futuras contribuciones de navegación. Este frente no modifica navegación ni UI antes de la parada 3.5.

**Actualización 04/10/2026 (sobre `main` = `3564718`).** Siguen pendientes y sin tocar:

- `backend/Sillar.sln`: los tres proyectos de M05a. Sin ellos la puerta no compila ni prueba M05a.
- `Sillar.Api.csproj`: el `ProjectReference` a `Sillar.Modules.Services`.
- `scripts/verificar.mjs` y `e2e/setup/migrate.ts`: las migraciones de M05a; `test:services` en la
  etapa 1.
- `platform/auditEntityVocabularies`: el vocabulario de `service_entry`.
- Navegación, rutas y portada: los cambios de `68a09aa` en `app/routes.tsx`,
  `layout/navigation.ts` y `platform/homeSections.ts` necesitan el turno de Integración.

Detalle en `BITACORA-M05a.md`, «Actualización sobre `main` = `3564718`».

**Aplicada el 04/10/2026** con el turno de navegación concedido por Chat 2 vía JP. Detalle en
`BITACORA-M05a.md`, «Turno de navegación y costuras», y las dos direcciones en
`evidencias/COSTURA-INDICE-20261004.md`. Sigue pendiente, fuera de E3, la guarda de dependientes
duros de `99_drop.sql`; su disparador es M05b.

## E4 · Control de concurrencia editorial

- **Discrepancia:** la SPEC prometía 409, recarga y ausencia de sobrescritura ante edición simultánea, pero `UpdateAsync` no recibe ni comprueba versión de edición.
- **Decisión:** opción B adoptada por el colíder el 29/09/2026: retirar esa representación del diseño actual.
- **Estado:** pendiente; no se añaden ahora versiones, ETag, bloqueos ni cambios de backend.
- **Disparador:** cuando el editor de vitrina tenga más de una persona utilizándolo simultáneamente.
- **Dueño:** producto y arquitectura de M05a.
