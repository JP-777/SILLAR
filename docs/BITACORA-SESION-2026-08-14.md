# Bitácora — Sesión de continuación del 14/08/2026

**Para:** el chat principal de SILLAR, al recuperar cuota.
**Motivo:** la conversación original se quedó sin tokens tras la entrega 2 de CORE. Esta sesión se abrió en otra cuenta con el `docs.rar` y el informe de Claude Code como único contexto.
**Resultado:** entrega 2 cerrada en documentación, una decisión de arquitectura nueva (ADR-012) y una corrección de backend pendiente de aplicar.

---

## 1. Punto de partida

Lo que se aportó a esta sesión:

- `docs.rar` con 18 archivos: `ARQUITECTURA_MODULAR.md`, `ROADMAP_MODULAR.md`, `MARCA.md`, `sillar-design-system.html`, ADR-001 a ADR-011, `modules/_PLANTILLA_SPEC.md`, `modules/core/SPEC.md` y `modules/core/ENTREGA-02-AUTENTICACION.md`.
- El informe de cierre de Claude Code sobre la entrega 2.
- Dos decisiones tomadas fuera del rar: el nombre **SILLAR** y la separación del trabajo de la primera instalación en un repositorio aparte.

Estado al empezar: F-01 a F-07 completadas, CORE con entregas 1 y 2 implementadas, commit `de4994a` en `main`, 95 pruebas en verde, F-08 sin empezar.

---

## 2. Qué se revisó del informe de Claude Code

El informe traía tres avisos. Se resolvieron así:

**2.1 · El 409 del último `super_admin` es inalcanzable.** Se confirma el análisis y la decisión de conservar la guarda. El razonamiento: quien ejecuta la operación es siempre un `super_admin` activo y solo se excluye del recuento al usuario afectado, así que el actor basta para que la comprobación pase; el único camino hasta cero es actuar sobre uno mismo, y eso lo corta antes la regla 3. Se documentó en `ENTREGA-02` §7.2 y se dejó una remisión en `SPEC.md` §8.4, para que quien lea solo el SPEC no crea que existe un camino de código que nunca se ejercita. En los criterios de aceptación, ese punto quedó como `[~]` —cumplido por otra vía— en lugar de `[x]`.

**2.2 · La rotación del token CSRF.** Se trató como un problema de diseño, no como una nota para el README. Ver §3 de esta bitácora.

**2.3 · El umbral de coincidencia de contraseñas subió de tres a cuatro caracteres.** Estaba en el código y no en el documento. Se incorporó a `ENTREGA-02` §1, con el caso que lo motivó («Ana Quispe» no podía usar `mesa lampara ventana` porque `ana` está dentro de `ventana`) y con la precisión de que es coincidencia por subcadena, no por palabra.

También se documentó una decisión que el informe mencionaba de pasada y no estaba en ningún sitio: **el hash señuelo del login se calcula al arrancar**, sobre un valor aleatorio y con el factor de trabajo vigente. Si fuera una constante incrustada con factor 12 y alguien subiera el factor, el señuelo tardaría menos que una verificación real y el margen de tiempo se reabriría por el otro lado.

---

## 3. Decisión nueva — ADR-012, token CSRF determinista

**El problema.** La entrega 2 genera un token CSRF aleatorio y almacena solo su SHA-256. Como el original no se puede recuperar, `GET /api/admin/auth/csrf` emite uno nuevo y anula el anterior. Con el panel abierto en dos pestañas, la segunda invalida el token de la primera y esta empieza a recibir 403. El panel de administración es justamente el tipo de aplicación que se usa en varias pestañas.

**Por qué no bastaba el reintento.** Convierte el 403 en un estado esperado del frontend: cada operación de escritura tendría que distinguir «403 de CSRF, reintenta» de «403 de permisos», y con peticiones cruzadas un reintento puede no bastar.

**Alternativas evaluadas.**

| Opción | Por qué se descartó |
|---|---|
| Rotación + reintento | Traslada al frontend un problema del backend, en el punto más sensible |
| Double-submit cookie | Un subdominio comprometido puede escribir cookies del padre, y SILLAR contempla un subdominio por instalación |
| Cifrado reversible del token | Introduce gestión de claves sin ganar nada frente al HMAC |

**Lo decidido.** `csrfToken = base64url(HMAC-SHA256(claveCsrf, admin_session_id))`, con `claveCsrf` derivada por HKDF de `core.installation.installation_key` y etiqueta `"sillar-csrf-v1"`. El token es constante durante la sesión, `/csrf` se vuelve idempotente, `csrf_token_hash` se conserva y **no hay migración**.

**Lo que se acepta a cambio.** El token no se puede rotar dentro de una sesión: si se filtra, hay que revocar la sesión. Y `installation_key` pasa a tener un uso criptográfico, así que no debe exponerse en respuestas del API ni rotarse a la ligera; rotarla invalida los tokens CSRF vivos. La etiqueta lleva versión para poder invalidarlos sin tocar la clave.

Se aplica en una **entrega 2.1**, antes de F-08, para que el frontend se construya contra la semántica definitiva.

---

## 4. Archivos modificados o creados en esta sesión

| Archivo | Cambio |
|---|---|
| `docs/modules/core/ENTREGA-02-AUTENTICACION.md` | Estado a *Cerrado* con commit; §1 política de contraseñas con el umbral de 4; §3 reescrita con el token derivado; §4 nota del señuelo calculado al arrancar; §7.2 nota del 409; §8 criterios marcados con leyenda; **§9 Cierre** nueva; *Fuera de alcance* pasó a §10 |
| `docs/modules/core/SPEC.md` | §8 regla 4 con remisión a la nota del 409 |
| `docs/ROADMAP_MODULAR.md` | Tabla nueva **CORE por entregas** (01 cerrada, 02 cerrada, 03 pendiente) |
| `docs/adr/ADR-012-token-csrf-determinista.md` | **Nuevo** |
| `docs/BITACORA-SESION-2026-08-14.md` | Este archivo |

La §9 de `ENTREGA-02` recoge la evidencia de verificación: los tres pares de tiempos de login medidos contra el servidor real, la tabla de comprobaciones manuales, las decisiones tomadas durante la implementación y los criterios de aceptación de la corrección 2.1.

---

## 5. Decisiones aplazadas

**Identidad del panel de administración** (pendiente de `MARCA.md` §6). Si el panel lleva marca SILLAR, la del negocio instalado o ambas. Es lo primero que se construye en F-08, así que deja de ser opcional en cuanto empiece el layout de administración. El asistente de instalación y el login sí pueden construirse antes: el instalador es del producto sin ambigüedad, porque en ese momento todavía no hay negocio configurado.

**Alcance de la entrega 03 de CORE.** Activación de módulos, `site_settings`, medios y auditoría consultable. Son los endpoints del SPEC §6 que siguen sin implementar. Conviene que existan antes de construir sus pantallas, o F-08 acabaría con interfaz contra una API imaginaria.

---

## 6. Plan acordado

1. **Claude Code:** entrega 2.1, la corrección de CSRF. Es pequeña, aislada y sin migración.
2. **Este chat / el chat principal:** escribir el documento de F-08 mientras tanto.
3. **Claude Code:** F-08 con ese documento, acotado a shell, tema, `useCapability()`, asistente de instalación, login y layout de administración vacío.
4. **Después:** entrega 03 de CORE, y solo entonces las pantallas de módulos, configuración y medios.

Nota técnica para F-08: con Vite en `:5173` y la API en `:5000`, `SameSite=Strict` funciona porque el sitio registrable es el mismo y el puerto no cuenta. Aun así conviene el proxy de Vite, para no pelear con CORS y credenciales.

---

## 7. Lo que sigue faltando en el repositorio de documentación

- `CLAUDE.md` no venía en el rar. Es el artefacto puente hacia Claude Code y conviene tenerlo a la vista al escribir prompts de handoff, sobre todo por las convenciones de pruebas que se añadieron en la entrega 2.
- No hay documento de entrega 01 de CORE en el rar; su estado en la tabla nueva del roadmap se dedujo del informe y conviene confirmarlo.
