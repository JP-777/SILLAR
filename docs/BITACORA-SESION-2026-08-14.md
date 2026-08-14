# Bitácora — Sesión de continuación del 14/08/2026

**Para:** el chat principal de SILLAR, al recuperar cuota.
**Motivo:** la conversación original se quedó sin tokens tras la entrega 2 de CORE. Esta sesión se abrió en otra cuenta con el `docs.rar` y el informe de Claude Code como único contexto.
**Resultado:** entrega 2 cerrada en documentación, ADR-012 nuevo, entregas 2.1 y 3 especificadas e implementadas, y la 3b especificada. Con la 3b, CORE queda completo y el trabajo pasa al frontend.

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
| `docs/CLAUDE.md` | Los documentos de entrega entran en la lista de lectura obligatoria, con su regla de precedencia; línea nueva en Seguridad sobre el token CSRF determinista |
| `docs/modules/core/ENTREGA-03-ACTIVACION-SETTINGS-AUDITORIA.md` | **Nuevo** |
| `docs/modules/core/ENTREGA-03B-MEDIOS.md` | **Nuevo** |

La §9 de `ENTREGA-02` recoge la evidencia de verificación: los tres pares de tiempos de login medidos contra el servidor real, la tabla de comprobaciones manuales, las decisiones tomadas durante la implementación y los criterios de aceptación de la corrección 2.1.

---

## 5. Entrega 2.1 — implementada

Commit `4c37cdc`. Los seis criterios verificados, incluido el del reinicio deteniendo el proceso de verdad.

Tres decisiones que tomó Claude Code durante la implementación y que conviene conocer:

1. **Los `Guid` se serializan en big-endian.** `Guid.ToByteArray()` invierte los tres primeros campos en arquitecturas little-endian, así que el orden por defecto depende de la plataforma. Sin corregirlo, la misma sesión habría producido tokens distintos en Windows y en Arch, y el síntoma —un 403 que solo aparece al cambiar de máquina— es de los que se persiguen por el lado equivocado. Importa porque el `CLAUDE.md` dice que el desarrollo alterna entre ambos.
2. **`CsrfTokenFactory` rechaza `Guid.Empty`**, para que construirlo antes de leer `core.installation` falle en el acto en vez de derivar tokens de una clave en blanco.
3. **`IsSetupPendingAsync` pasó a `ReadInstallationKeyAsync`** y devuelve la clave en lugar de un booleano. Ya era el único punto del arranque que leía `core.installation`.

Se verificó además que `installation_key` no aparece en ninguna respuesta ni en ningún DTO, propiedad que hay que mantener vigilada ahora que tiene uso criptográfico.

**Propiedad estructural que conviene tener escrita:** el token CSRF de una sesión revocada **sigue siendo derivable para siempre**, porque es una función de `admin_session_id`. Lo que lo invalida es que la sesión ya no pasa el filtro anterior. No es un defecto —el CSRF nunca fue autenticación— pero es fácil de malinterpretar leyendo el código suelto.

---

## 6. Entrega 3 — activación, configuración y auditoría

Especificada aquí e implementada en el commit `4fe76b4`, con 152 pruebas. Los medios se apartaron a una entrega 3b.

### La decisión de fondo

Activar un módulo **no puede surtir efecto en caliente**: el enrutamiento se construye al arrancar y solo registra módulos activos (SPEC §7). Se decidió que el host se detenga y lo relance el orquestador, igual que ya hacía `POST /api/setup`.

Se descartó el registro dinámico de endpoints —pelea con el enrutamiento de ASP.NET Core por una operación que se ejecuta dos veces al año— y también responder 200 pidiendo un reinicio manual, porque encender y apagar módulos en vivo es el argumento de venta y hacerlo por consola lo arruina.

Consecuencias que quedaron escritas: la respuesta debe salir antes de la parada; `Modules:RestartAfterActivation` la desactiva en desarrollo; el contenedor del API necesita `restart: unless-stopped`; y **el panel necesita una pantalla de «reiniciando, reconectando»**, que es trabajo de F-08.

Las sesiones y los tokens CSRF sobreviven al reinicio, porque unas viven en base de datos y los otros se derivan de `installation_key`. Con el diseño anterior del CSRF, cada activación habría expulsado a todo el mundo. La decisión de la 2.1 se pagó sola aquí.

### El riesgo que gobierna la entrega

Si el endpoint acepta un estado que el validador de arranque rechaza, el host se detiene inmediatamente después y **la instalación queda muerta, recuperable solo por SQL**. Por eso la validación del endpoint y la del arranque son la misma función. Claude Code fue más allá: escribe, **relee de la base** y valida lo releído antes de confirmar, para que lo validado sea exactamente lo que quedará guardado. Y añadió una prueba que recorre los 19 estados alcanzables de un grafo de seis módulos comprobando que el arranque acepta todos, con el número exacto como aserción.

### Corrección pendiente en la entrega 3

La validación dentro de la transacción protege contra un cambio malo, no contra **dos cambios buenos que juntos son malos**: con dos `super_admin` a la vez, uno puede desactivar M05b mientras el otro activa M06, cada operación válida por separado, y el estado resultante no arranca. Se añadió al §2 la regla del `pg_advisory_xact_lock` sobre una clave constante, con sus dos criterios. Improbable y barato de evitar; la consecuencia es instalación muerta.

### Otras decisiones de la entrega 3

- **Nada de desactivación en cascada.** Apagar Servicios no apaga Seguimiento: devuelve 409 nombrando quién bloquea y la persona ordena.
- Las claves de configuración **no se crean ni se borran desde el API**. Nacen del seed o de la migración del módulo que las necesita.
- **Cambiar `is_public` exige `super_admin`** aunque cambiar el valor solo exija `admin`. Publicar es de otra naturaleza que corregir un teléfono.
- **La auditoría no registra el valor de una configuración**, ni el nuevo ni el anterior. La tabla albergará algún día credenciales, y una auditoría que no se puede borrar sería un almacén de secretos en claro.
- Auditoría paginada, 50 por defecto y 200 como máximo.
- `expires_at` se devuelve pero no se evalúa: el control de vencimientos es de la fase 5.

### Hallazgos al implementarla

- **`GET /api/settings/public` no existía.** El SPEC §6 lo declara desde el principio y el documento de la entrega 3 dio por hecho que estaba implementado. Entró en la 3. Lección aplicable al resto: el SPEC dice qué debe existir, no qué existe.
- **El bus de eventos no tiene ningún manejador.** Es lo acordado —M10 lo consumirá—, pero conviene que se quede mínimo: publicar y nada más. Reintentos, colas o persistencia sin consumidor real serían infraestructura para clientes imaginarios.
- **Tres criterios no se pudieron verificar** porque solo existe CORE y CORE no se desactiva: el ciclo activar → reiniciar → ruta nueva no se puede recorrer por HTTP. Quedaron marcados como imposibles hasta M01. Alternativa propuesta, sin adelantar trabajo de producto: un `IModule` de mentira que exista solo en el proyecto de pruebas de integración. Si no se monta, esos tres criterios tienen que entrar explícitamente en la lista de M01 o se pierden.
- El criterio del contenedor quedó sin ejecutar: el CLI de Docker no era accesible desde esa máquina. Pendiente de comprobar con `docker compose --profile full up -d --build`.

---

## 7. Entrega 3b — medios

Especificada, sin implementar todavía. Cierra CORE.

**Decisión de formatos: solo `jpeg`, `png` y `webp`, máximo 5 MB. SVG rechazado.**

El razonamiento del SVG merece conservarse porque no es el habitual. Un SVG es XML que puede contener scripts y, servido desde la ruta estática, se ejecuta **en el mismo origen que el panel**. La cookie es `httpOnly`, pero eso no protege: el navegador la adjunta sola, y desde el mismo origen el script puede pedir `GET /api/admin/auth/csrf`, que desde la entrega 2.1 devuelve un token válido y estable. Es decir, cualquiera con rol `editor` podría ejecutar escrituras autenticadas con los permisos de quien mire la imagen. Sanear SVG exige una biblioteca con historial de evasiones. Si algún día hace falta vectorial, la salida buena es servir los medios desde un origen distinto, no sanear.

Otras decisiones del documento:

- **Validación por los bytes iniciales**, nunca por la extensión ni por el `Content-Type` del cliente. El límite de tamaño se aplica también en Kestrel, para que un cuerpo enorme no llegue a recibirse.
- **Nombre generado** con reparto `aaaa/mm`, y extensión derivada del tipo real detectado. Es la defensa contra el recorrido de rutas.
- **Duplicados: se detectan, no se fusionan.** Reutilizar la fila haría que dar de baja un archivo rompiera a otro módulo, y evitarlo exige un recuento de referencias que nadie ha pedido. La respuesta indica `duplicateOf` y el panel avisa.
- **La baja es lógica y el binario se conserva**, pero el archivo deja de servirse por la ruta estática.
- **`is_orphan` marca los archivos de módulos desinstalados, no de módulos desactivados.** Desactivar es reversible y ocurre en cada demostración; marcar huérfanos ahí llenaría el panel de avisos falsos.
- El `README` gana el apartado de respaldo del volumen, que es la consecuencia negativa que el ADR-011 anotó y el error clásico al restaurar.

---

## 8. Decisiones aplazadas

**Identidad del panel de administración** (`MARCA.md` §6). Si el panel lleva marca SILLAR, la del negocio instalado o ambas. **Bloquea F-08**: es lo primero que se construye. El asistente de instalación y el login pueden hacerse antes, porque en ese momento todavía no hay negocio configurado y son del producto sin ambigüedad.

**Política de retención de auditoría.** Se decidirá cuando exista una instalación con volumen real.

**Vectoriales en medios.** Requiere un origen separado para servirlos.

---

## 9. Plan

1. ~~Claude Code: entrega 2.1, corrección de CSRF.~~ Hecho, `4c37cdc`.
2. ~~Este chat: documento de la entrega 3.~~ Hecho.
3. ~~Claude Code: entrega 3.~~ Hecho, `4fe76b4`.
4. ~~Este chat: documento de la entrega 3b.~~ Hecho.
5. **Claude Code: entrega 3b, medios.** Con esto CORE queda completo.
6. Decidir la identidad del panel, que desbloquea F-08.
7. Este chat: documento de F-08, acotado a shell, tema, `useCapability()`, asistente de instalación, login, layout de administración y la pantalla de reconexión tras reinicio.
8. Claude Code: F-08.
9. Después: M01 Catálogo, arrastrando los tres criterios de activación que hoy no se pueden verificar.

Nota técnica para F-08: con Vite en `:5173` y la API en `:5000`, `SameSite=Strict` funciona porque el sitio registrable es el mismo y el puerto no cuenta. Aun así conviene el proxy de Vite, para no pelear con CORS y credenciales.

---

## 10. Pendientes de comprobación

- **`CLAUDE.md` de la raíz.** Claude Code borró `docs/CLAUDE.md` diciendo «como acordamos», y eso no se acordó. Lo más probable es que fuera un duplicado —el archivo va en la raíz y el zip de esta sesión lo incluía dentro de `docs/`—, pero hay que confirmar que la copia de la raíz conserva los dos añadidos: la línea de `ENTREGA-NN-*.md` en la lista de lectura y «No volver a introducir rotación» en Seguridad.
- **El hash `4c37cdc`** en el §9 de `ENTREGA-02`, y `4fe76b4` en el cierre de la entrega 3.
- **`docker compose --profile full up -d --build`**, para el criterio de reinicio del contenedor.
- **Entrega 01 de CORE**: no hay documento suyo en el material recibido; su estado en la tabla del roadmap se dedujo del informe.
