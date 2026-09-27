# Escaladas — M05a

- **Creación / última modificación / última verificación:** 28/09/2026 · America/Lima
- **Base verificada:** `e839989432283c755edf7d4ae47b2c37697215ec`
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
