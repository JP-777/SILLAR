# Bitácora de M07 — Solicitudes B2B y Especiales

Creado: 27/09/2026, America/Lima · Última modificación: 29/09/2026 · Última verificación:
29/09/2026 · Commit base verificado: `e839989432283c755edf7d4ae47b2c37697215ec`.

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

**Puerta canónica sobre `0fd8dad`** (originales en `/var/tmp/sillar-m07-puerta-20260927-133459/`,
copias en `evidencias/PUERTA-*-20260927.*`):

| Corrida | Resultado |
|---|---|
| 1 · 13:57 → 14:29 | **rc=1, de entorno.** Etapas 1–5 PASS. La 6 no llegó a ejecutar pruebas: el `dotnet publish` de la imagen e2e agotó sus reintentos de NuGet («no data was received for 60000ms»). Diez minutos después, `api.nuget.org` respondía 200 desde el equipo y desde Docker. SHA-256 `c6c38217…acef5d` |
| 2 · 14:39 → 15:24 | **6/6 PASS, rc=0.** e2e **159/159, 0 fallidas, 0 inestables, 0 omitidas**. Las 19 pruebas de M07 corren en la etapa 5. SHA-256 `8a090413…c09591`; informe de Playwright `c46c5f9c…0f23b9a7` |

**Hallazgo para Integración (C11), OBSERVADO en la corrida 2:** la memoria disponible bajó a
**1218 MiB** (14:58:04, vigía a 1 GiB), y el e2e tardó **39,2 min** frente a unos 21 en corridas
anteriores. Lo que la llenaba no era la API: eran **nodos de MSBuild reutilizables** que la propia
puerta deja vivos —más de veinte procesos de 160–220 MiB, lanzados en las etapas 3–5 y durante el
e2e—, porque nada desactiva la reutilización de nodos (sin coincidencias de `nodeReuse`, `MSBUILDDISABLENODEREUSE` ni `build-server` en `scripts/verificar.mjs` ni en `e2e/setup/`). **DEDUCIDO, no demostrado:** encaja con la
presión de memoria de la puerta roja de M02. Efecto pedido: que la puerta no acumule nodos de
MSBuild entre etapas.

**Pendiente del ciclo:** paso 3 (API) sobre este esquema; la creación de cotizaciones espera a la
letra de serie. Después, parada 3.5.

---

## 29/09/2026 · Paso 3, primer tramo: el lado del cliente

**Construido** (`backend/Sillar.Modules.B2B/Solicitudes`, `Endpoints/SolicitudesClienteEndpoints.cs`):
`POST /api/b2b/special-orders`, `POST /api/b2b/institution-requests`, `GET /api/b2b/my-requests` y
`GET /api/b2b/quotes/{quoteNumber}`. Todas con la política de cliente y el CSRF **del contrato de
M04**, sin autenticación propia. El cliente sale siempre de la sesión.

**Decisiones del tramo, todas reversibles editando código:**

- **Límite por cuenta, no por IP**, compartido por los dos tipos, y consumido **después** de validar
  (una petición inválida no gasta cupo). Valores por configuración (`B2b:LimiteSolicitudes:*`), 5
  por hora por defecto hasta que alguien decida otros: son un parámetro comercial.
- **Una personalización solo nace de un producto activo y publicado** de M01; si no, 409.
- **El cliente no ve cotizaciones en `borrador`**: todavía no se le han enviado.
- **Sin campo de título** en la respuesta de «mis solicitudes»: la SPEC fija su forma (§5) y un
  campo pedido por una pantalla es un hallazgo, no una corrección.

**Pruebas:** 31/31 en verde (19 de antes + 12 nuevas). **Sabotaje** en
`evidencias/PASO3-SABOTAJE-20260929.txt`: límite por clave común (P1), nota interna filtrada (P2) y
cotización sin filtrar por cliente (P3) ponen su prueba en rojo; restaurado, 31/31.

**Sin puerta de este tramo todavía:** la máquina tiene memoria justa y la ventana de QA de M04
tiene prioridad. **Pendiente:** rutas de administración; creación de cotizaciones, bloqueada por la
letra de serie (pregunta 3); manejadores de eventos de M01 (refresco y caducidad, E3b).

