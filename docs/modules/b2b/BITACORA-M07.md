# Bitácora de M07 — Solicitudes B2B y Especiales

Creado: 27/09/2026, America/Lima · Última modificación: 27/09/2026 · Última verificación:
27/09/2026 · Commit base verificado: `e839989432283c755edf7d4ae47b2c37697215ec`.

La escribe el frente B (`DIVISION-DE-TRABAJO.md`, regla 6). Criterio y hechos, no un diario.

---

## 27/09/2026 · Paso 1 reconciliado, paso 2 (datos) construido, C6 auditada

**Base.** Rama nueva `m07-b2b-sobre-main` desde `main` `e839989`, con los tres commits
documentales del 26/09 traídos por `cherry-pick` (sin merge, rebase ni force). La rama `m07-b2b`
queda intacta como historia. Worktree nueva `sillar-b2b` (offset 62): la vieja `sillar-m07` choca
con `sillar-footer` y no se ha levantado nada en ella. Se retiró `sillar-qa-m02-docs`, que era mía,
estaba limpia y chocaba con una worktree de A.

**Decisiones de JP aplicadas** (encargo `B_M07_B2B.md`): consultas autenticadas, fotos privadas
aplazadas, líneas de cotización por presentación con snapshot, convención de códigos sin letra.
En `SPEC.md` como enmiendas del 27/09; E1, E9, E3b, E10 y la letra quedan en `ESCALADAS-M07.md`.

**Construido:**

- `backend/Sillar.Modules.B2B` — módulo, dominio, `DbContext` en `b2b`, migración `B2bInitial`
  con las cinco FK duras y la guarda de instalación. **Sin endpoints todavía** (paso 3).
- `database/modules/b2b/02_seed.sql` (vacío, idempotente) y `99_drop.sql` (con guarda).
- Guarda de desinstalación en `catalog/99_drop.sql` y `crm/99_drop.sql` (turno pedido: C7).
- `backend/Sillar.Modules.B2B.Tests` — 19 pruebas, cada una en su base efímera.

**Lo que se aprendió, y conviene no repetir:**

1. **La guarda del panel mira lo activo; el borrado es SQL.** Proteger el panel no protege nada
   de la desinstalación.
2. **Npgsql y psql no ejecutan un script igual.** Npgsql para en el primer error; psql sin
   `ON_ERROR_STOP` sigue. Una guarda con el `DROP` fuera de su bloque pasaba verde en Npgsql. Se vio
   rompiéndola (S3), no razonándola.
3. **Una subcadena en un mensaje no prueba que la guarda disparó.** La de instalación nunca lanzó
   su mensaje (`22P02 malformed array literal`) y sus pruebas pasaban porque el error contenía el
   texto buscado. Se vio rompiéndola (S4). Las pruebas de error exigen ahora SQLSTATE y mensaje
   completo.
4. **EF crea el schema y su historial fuera de la transacción de la migración.** Una instalación
   rechazada deja `b2b` vacío. Límite documentado (C10), no fallo.

**Pendiente del ciclo:** paso 3 (API) sobre este esquema; la creación de cotizaciones espera a la
letra de serie. Después, parada 3.5.
