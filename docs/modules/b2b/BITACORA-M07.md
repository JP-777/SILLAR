# Bitácora de M07 — Solicitudes B2B y Especiales

Creado: 27/09/2026, America/Lima · Última modificación: 30/09/2026 · Última verificación:
30/09/2026 · Commit base verificado: `e839989432283c755edf7d4ae47b2c37697215ec`.

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

---

## 30/09/2026 · Paso 3, segundo tramo: el panel y los eventos de M01

**Rutas del panel** (`Endpoints/BandejaAdminEndpoints.cs`), grupo `/api/admin/b2b` con mínimo
`editor` y CSRF del panel; bajas solo `admin`; toda escritura audita con `module_code = 'b2b'` y
**nombra la fila** («La solicitud de volumen de «Colegio…» pasó a en revisión.»):

| Ruta | Qué hace |
|---|---|
| `GET /special-orders`, `GET /special-orders/{id}` | Bandeja con filtro por estado; detalle **con** notas internas |
| `PUT /special-orders/{id}/status` · `/notes` · `/relink` | Estado (regla 5), notas internas, reenlace a un producto **activo** |
| `DELETE /special-orders/{id}` | Baja lógica (`admin`) |
| `GET /institution-requests`, `GET /institution-requests/{id}` | Igual, para volumen |
| `PUT /institution-requests/{id}/status` · `/notes` | Estado y notas |
| `DELETE /institution-requests/{id}` | Baja lógica (`admin`) |
| `GET /quotes`, `GET /quotes/{id}` | Cotizaciones, **solo lectura**, con su precio de catálogo |

**Eventos de M01** (`Catalogo/ReaccionAlCatalogo.cs`), respaldados por `CatalogEvents.cs:40` y
`:45`: `ProductoActualizado` refresca la instantánea de las personalizaciones e **invalida solo las
cotizaciones `enviada`** cuya presentación cambió de precio (vía `VariantesDeAsync`), con un
motivo legible; `ProductoDesactivado` marca `pending_relink` y **no invalida nada**. Idempotente y
serializado por producto.

**Decisiones del tramo, reversibles editando código:**

- **`cerrada` y `rechazada` son finales**; «rechazada desde cualquiera» se lee como desde las tres
  primeras. Reabrir no está en la SPEC.
- **Las líneas «a consultar» no se evalúan** al invalidar, mientras E3b siga abierta. Un precio que
  pasa de valor a «a consultar» **sí** invalida: el precio cotizado ya no existe en el catálogo.
- **Invalidar no cambia el estado**: la cotización sigue `enviada` con `invalidated_at` y su motivo;
  lo que ve el cliente es `SigueValida = false`.

**Pruebas:** 49/49 (31 de antes + 18 nuevas). Los permisos de cada ruta se afirman sobre los
metadatos reales de los endpoints. **Sabotaje** en `evidencias/PASO3-PANEL-SABOTAJE-20260930.txt`:
Q1–Q6 (transiciones sin guarda, baja sin `admin`, invalidación en cualquier estado, reenlace sin
comprobar actividad, rutas públicas con política de panel, desactivar que invalida), los seis en
rojo; restaurado, 49/49.

**Sigue abierto:**

- **Crear cotizaciones**: la letra de serie (pregunta 3). Con ella, el ciclo `send` / `approve` /
  `payment` / edición de líneas / baja, que sin creación no tiene nada sobre lo que actuar.
- **E3b**, y el **umbral mayorista** (regla 4), que vive en `core.site_settings` y no tiene clave.
- **La mitad HTTP de los permisos y del CSRF** (401/403 reales, filtro CSRF): los metadatos no
  muestran los filtros; se cubre con e2e cuando M07 se despliegue (C9).
- **El límite de 5 por hora** sigue siendo provisional y configurable.

---

## 30/09/2026 · Paso 3, tercer tramo: el ciclo de cotizaciones

**Rutas** (grupo `/api/admin/b2b`): `POST /quotes` (crea en borrador desde una solicitud, con su
número), `PUT /quotes/{id}` (sustituye líneas, **solo en borrador**), `PUT /quotes/{id}/send`,
`PUT /quotes/{id}/approve`, `PUT /quotes/{id}/payment` (**admin**) y `DELETE /quotes/{id}`
(**admin**). `GET /quotes/{id}` devuelve además la evaluación mayorista. La auditoría nombra la
cotización por su número: «Cotización C-2026-0001 enviada.».

**Numeración** (`Cotizaciones/NumeradorDeCotizaciones.cs`): contador por serie y año con
`… RETURNING` dentro de la transacción que crea la cotización; se niega a trabajar fuera de ella.

**Decisiones del tramo, reversibles editando código:**

- **Crear una cotización pasa la solicitud a `cotizada`** si no lo estaba, y no se cotiza una
  `cerrada` o `rechazada`. La regla 5 gobierna los cambios manuales; este es el paso automático
  que el diagrama implica.
- **Enviar exige al menos una línea**; **aprobar exige que no haya caducado**.
- **Pago:** Yape exige código de operación; efectivo no; **tarjeta se rechaza** hasta M11 (regla 9).
- **El umbral no aplica descuentos**: informa `alcanza`, `no_alcanza`, `no_evaluable` o
  `configuracion_pendiente`. Se lee el texto del ajuste, no `Get<decimal>`, que daría 0 con
  `PENDIENTE_DEFINIR`.
- **Corrección de un fallo propio del tramo anterior:** el motivo de invalidación escribía «S/» a
  fuego. Ahora usa `currency_code` por `ISettingsReader`.

**Pruebas:** **69/69** (49 de antes + 20 nuevas), las 14 obligatorias incluidas. **Sabotaje** en
`evidencias/PASO3-COTIZACIONES-SABOTAJE-20260930.txt`: R1 (guarda de serie: `count(*)+1` sin
bloqueo), R2, R3, R4, R6 y R7 en rojo a la primera. **R5 y R5b, por separado, quedaron verdes**:
E3b está protegida dos veces (consulta y comparación) y romper una sola deja la otra en pie; **R5c,
las dos a la vez, en rojo**. Restaurado: 69/69.

**Sigue abierto:** C12 (fila del umbral en CORE) y C13 (nombre en `ICurrentAdmin`), ninguno
bloqueante; C9 (desplegar M07) y con él la mitad HTTP de permisos y CSRF; parada 3.5 (Diseño).

