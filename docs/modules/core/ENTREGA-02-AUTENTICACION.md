# CORE · Entrega 2 — Instalación y autenticación

- **Módulo:** `core` · **Estado:** Cerrado (14/08/2026, commit `de4994a`)
- **Refina:** `SPEC.md` §6 (endpoints) y §8 (reglas)
- **Decide sobre:** ADR-010

Esta entrega retira el `INSERT` manual documentado en `backend/README.md` y deja el sistema utilizable por una persona real.

**Alcance:** instalación inicial, inicio y cierre de sesión, sesiones por cookie, protección CSRF, cambio de contraseña propio y administración de usuarios.

---

## 1. Instalación inicial

### `GET /api/setup/status`

Público. Responde `{ "setupRequired": true|false }`.

### `POST /api/setup`

```json
{
  "businessName": "Nombre del negocio",
  "licenseType": "trial",
  "admin": {
    "fullName": "Nombre Apellido",
    "email": "persona@ejemplo.pe",
    "password": "..."
  }
}
```

Crea en una sola transacción la fila de `installation` con `is_setup_complete = true` y el primer `super_admin`. Registra auditoría con acción `setup`.

**Reglas:**

1. Si `is_setup_complete = true`, ambas rutas responden **404**, no 403. Coherente con el criterio del SPEC: lo que no está disponible no existe.
2. Dos peticiones simultáneas no pueden crear dos instalaciones. La transacción más `uq_installation_singleton` lo garantizan; la segunda recibe 404.
3. `installation_key` se genera en el servidor. Nunca viene del cliente.
4. La respuesta **no** inicia sesión automáticamente. Devuelve 201 y el frontend redirige al login. Encadenar instalación con sesión abierta mezcla dos flujos que conviene mantener separados.

### Política de contraseñas

- **Mínimo 12 caracteres.** Sin exigir mayúsculas, dígitos ni símbolos.
- Se rechazan las que aparezcan en una lista corta de contraseñas comunes, incrustada en el código.
- Se rechaza si contiene el correo o el nombre del usuario. La comprobación se hace sobre los fragmentos del nombre y del correo de **cuatro o más caracteres**; los de tres o menos se ignoran.
- Sin caducidad ni rotación obligatoria.

**Sobre el umbral de cuatro caracteres.** Con un umbral de tres, un usuario llamado «Ana Quispe» no podía usar `mesa lampara ventana`, porque `ana` está contenido dentro de `ventana`. El umbral se fijó en cuatro durante la implementación: el apellido y el correo se siguen comprobando, y con doce caracteres de mínimo un nombre de tres letras nunca puede ser la contraseña entera. Es un límite de coincidencia por subcadena, no por palabra: `quispe2026quispe` se sigue rechazando.

Esto sigue la recomendación vigente del NIST: la longitud protege más que la composición, y forzar símbolos y rotaciones produce contraseñas peores y anotadas en un papel bajo el teclado. En una librería, ese papel existe.

---

## 2. Sesión

### La cookie

| Atributo | Valor |
|---|---|
| Nombre | `sillar_session` |
| `HttpOnly` | sí |
| `Secure` | sí |
| `SameSite` | `Strict` |
| `Path` | `/` |
| Caducidad | **cookie de sesión**, sin `Max-Age` |

Sin `Max-Age`: la cookie muere al cerrar el navegador y la autoridad real es la fila en base de datos. Si alguien deja el mostrador y cierra el navegador, la sesión se acaba en ese equipo.

**Nota para desarrollo:** `Secure` funciona en `http://localhost` porque los navegadores tratan localhost como contexto seguro. Si alguien ve un problema de sesión en local, **no es por `Secure`**; quitarlo sería un error.

### El token

- 256 bits de aleatoriedad criptográfica, codificados en base64url.
- En `core.admin_sessions.token_hash` se guarda su **SHA-256**, nunca el token.

SHA-256 y no BCrypt, deliberadamente. BCrypt es lento a propósito para resistir fuerza bruta contra secretos de baja entropía, que es lo que son las contraseñas. Un token de 256 bits aleatorios no es forzable, y aquí hace falta una búsqueda rápida en cada petición. Usar BCrypt sería pagar un coste alto sin ganar nada.

### Vigencia

- 8 horas de inactividad, con renovación deslizante.
- **Tope absoluto de 7 días** desde `issued_at`. Una sesión usada a diario no puede vivir indefinidamente.
- La renovación se escribe solo si `last_seen_at` tiene más de **1 minuto**. Sin ese umbral, cada petición sería una escritura.
- Al iniciar sesión se purgan las sesiones del usuario caducadas hace más de 7 días.

---

## 3. Protección CSRF

Es la contrapartida obligatoria de usar cookies. `SameSite=Strict` ayuda pero no basta.

**El token CSRF es determinista, derivado de la sesión** (ADR-012):

```
csrfToken = base64url( HMAC-SHA256( claveCsrf, admin_session_id ) )
```

- `claveCsrf` se deriva al arrancar de `core.installation.installation_key` mediante HKDF, con una etiqueta de contexto fija (`"sillar-csrf-v1"`). No se guarda en configuración ni en variables de entorno, y sobrevive a los reinicios porque su origen está en base de datos.
- Al crear la sesión se calcula el token y su SHA-256 va a `csrf_token_hash`, igual que antes. **La tabla no cambia.**
- El token en claro se devuelve **en el cuerpo de la respuesta** del login, nunca en una cookie.
- El frontend lo envía en la cabecera `X-CSRF-Token` en todo método que no sea `GET`, `HEAD` u `OPTIONS`.
- `GET /api/admin/auth/csrf` **recalcula el mismo token** para la sesión activa y lo devuelve. Es idempotente: llamarlo cien veces desde cinco pestañas devuelve siempre el mismo valor y no invalida nada.
- La verificación compara el SHA-256 del token recibido contra `csrf_token_hash`, **en tiempo constante**. Se compara contra la fila, no contra el HMAC recalculado, para que rotar `claveCsrf` invalide los tokens en vez de aceptarlos por accidente.
- Sin token válido: **403**.

**Por qué determinista y no aleatorio con rotación.** Un token aleatorio obliga a que `/csrf` emita uno nuevo cada vez, porque en base de datos solo vive el hash y el original es irrecuperable. Con dos pestañas abiertas, la segunda invalida a la primera y esta empieza a recibir 403 sin haber hecho nada mal. Derivarlo de `admin_session_id` elimina la carrera de raíz: el valor es una función de la sesión, no un estado que compita consigo mismo. Ver ADR-012 para las alternativas descartadas y el coste asumido.

**Consecuencia que hay que tener presente:** el token CSRF ya no puede rotarse dentro de una sesión. Si se filtrara, la única salida es cerrar esa sesión — que es exactamente lo que hace `logout`, y lo que hace el cambio de contraseña con las demás sesiones.

---

## 4. Inicio de sesión

### `POST /api/admin/auth/login`

```json
{ "email": "persona@ejemplo.pe", "password": "..." }
```

Respuesta 200:

```json
{
  "user": { "id": 1, "fullName": "…", "email": "…", "role": "super_admin" },
  "csrfToken": "…"
}
```

### Secuencia exacta

```
1. Buscar el usuario por correo (colación core.es_ci: no distingue mayúsculas).
2. Si NO existe → verificar la contraseña contra un hash señuelo fijo y devolver 401.
3. Verificar la contraseña con BCrypt.
4. Si es incorrecta → incrementar failed_login_count, auditar login_failed, devolver 401.
5. Si es correcta pero la cuenta está bloqueada → 423 con el momento de desbloqueo.
6. Si es correcta pero is_active = false → 401 genérico.
7. Reiniciar failed_login_count, crear la sesión, auditar login, devolver 200.
```

**El paso 2 no es opcional.** Sin ese cálculo señuelo, la respuesta para un correo inexistente llega mucho más rápido que para uno existente, y ese margen basta para averiguar qué correos están registrados.

**El hash señuelo se calcula al arrancar**, sobre un valor aleatorio y con el mismo factor de trabajo que la configuración tenga vigente. No es una constante incrustada en el código: si lo fuera con factor 12 y alguien subiera el factor a 13, el señuelo pasaría a tardar menos que una verificación real y el margen de tiempo volvería a abrirse por el otro lado.

**El orden de los pasos 3 y 5 tampoco.** Se verifica la contraseña *antes* de mirar el bloqueo, y solo entonces se devuelve 423. Así, quien no sabe la contraseña recibe siempre el mismo 401 y no descubre nada; quien sí la sabe recibe una explicación útil en lugar de un error opaco. Es el equilibrio entre no filtrar información y no dejar a la dueña del negocio adivinando por qué no entra.

### Bloqueo

- **5 intentos fallidos → 15 minutos de bloqueo.** El contador se reinicia con un acceso correcto.
- Se audita cada `login_failed` con el correo intentado, exista o no la cuenta.

**Compromiso asumido:** contar por cuenta permite que alguien bloquee deliberadamente la cuenta de otro fallando cinco veces. Se acepta porque el bloqueo es corto, queda auditado y la alternativa —contar por IP— falla en un local donde todo el personal comparte la misma salida a internet.

### `POST /api/admin/auth/logout`

Marca `revoked_at` y borra la cookie. **Revocar la fila es lo que cierra la sesión**; borrar la cookie solo limpia el navegador.

### `GET /api/admin/auth/me`

Devuelve id, nombre, correo y rol. Nunca el hash.

---

## 5. Autorización

Roles jerárquicos: `super_admin` > `admin` > `editor`. Exigir `admin` acepta también a `super_admin`.

Se implementa `ICurrentAdmin` del contrato de CORE. Ningún módulo consulta `core.admin_users` directamente.

---

## 6. Cambio de contraseña

### `POST /api/admin/auth/change-password`

```json
{ "currentPassword": "…", "newPassword": "…" }
```

- Exige la contraseña actual aunque haya sesión abierta.
- La nueva pasa la misma política.
- **Revoca todas las demás sesiones del usuario** y conserva la actual. Si alguien cambia su contraseña es porque sospecha, y dejar vivas las otras sesiones anula el gesto.
- Se audita.

---

## 7. Administración de usuarios

| Método | Ruta | Rol |
|---|---|---|
| GET | `/api/admin/users` | super_admin |
| POST | `/api/admin/users` | super_admin |
| PUT | `/api/admin/users/{id}` | super_admin |
| DELETE | `/api/admin/users/{id}` | super_admin |
| GET | `/api/admin/sessions` | super_admin |
| DELETE | `/api/admin/sessions/{id}` | super_admin |

**Reglas:**

1. `DELETE` es **desactivación lógica**: `is_active = false` y revocación de todas sus sesiones.
2. No se puede dejar el sistema sin `super_admin` activo. La operación que lo haría se rechaza con **409**.

   *Nota de implementación (entrega 2).* Esta guarda es **inalcanzable con las rutas actuales**. Quien ejecuta la operación es siempre un `super_admin` activo y solo se excluye del recuento al usuario afectado, así que el propio actor basta para que la comprobación pase. El único camino hasta cero es actuar sobre uno mismo, y eso lo corta antes la regla 3 con su propio 409. La conducta que exige el SPEC §8.4 se cumple, pero por la otra regla y con otro mensaje. La guarda se conserva a propósito: el día que exista traspaso de propiedad, un comando de mantenimiento sin sesión o una operación por lotes, será el único punto que impida quedarse sin nadie que pueda entrar.
3. Un usuario no puede desactivarse ni cambiarse el rol a sí mismo.
4. Al crear un usuario, la contraseña la fija el `super_admin` y pasa la política. No hay contraseñas generadas ni enviadas por correo mientras no exista envío de correo.
5. Cambiar el rol o desactivar a alguien revoca sus sesiones activas de inmediato.
6. Ninguna respuesta incluye `password_hash`.

---

## 8. Criterios de aceptación

**Instalación**

- [x] Con la base recién migrada, `GET /api/setup/status` responde `setupRequired: true`
- [x] `POST /api/setup` crea instalación y `super_admin`, y devuelve 201
- [x] Tras la instalación, ambas rutas responden **404**
- [x] Dos `POST /api/setup` simultáneos crean una sola instalación
- [x] Una contraseña de 11 caracteres se rechaza; una de 12 se acepta
- [x] Se rechaza una contraseña que contenga el correo del usuario

**Sesión**

- [x] La cookie llega con `HttpOnly`, `Secure`, `SameSite=Strict` y sin `Max-Age`
- [x] `admin_sessions.token_hash` no contiene el token en claro
- [x] Una petición autenticada dentro del minuto siguiente **no** vuelve a escribir `last_seen_at`
- [x] Una sesión con más de 8 horas de inactividad se rechaza
- [x] Una sesión con más de 7 días desde `issued_at` se rechaza aunque haya actividad reciente
- [x] `logout` marca `revoked_at`, y reutilizar esa cookie devuelve 401

**CSRF**

- [x] Un `POST` sin `X-CSRF-Token` devuelve 403
- [x] Un `POST` con un token CSRF de otra sesión devuelve 403
- [x] Un `GET` sin token CSRF funciona con normalidad

**Login**

- [x] Correo inexistente y contraseña incorrecta devuelven la misma respuesta 401
- [x] La diferencia de tiempo entre correo existente e inexistente es despreciable
- [x] Cinco fallos bloquean la cuenta 15 minutos
- [x] Contraseña correcta con cuenta bloqueada devuelve **423**; contraseña incorrecta con cuenta bloqueada devuelve **401**
- [x] Un acceso correcto reinicia `failed_login_count`
- [x] Un usuario con `is_active = false` no puede entrar
- [x] `PERSONA@EJEMPLO.PE` entra igual que `persona@ejemplo.pe`

**Usuarios**

- [~] No se puede desactivar al último `super_admin` activo: **se cumple por la regla 3**, con 409 y mensaje distinto. Ver la nota de §7.2
- [x] Un usuario no puede desactivarse a sí mismo
- [x] Desactivar a alguien revoca sus sesiones de inmediato
- [x] Cambiar la propia contraseña revoca las demás sesiones y conserva la actual
- [x] Ninguna respuesta del API contiene `password_hash`

**General**

- [x] `setup`, `login`, `login_failed`, `logout` y los cambios de usuario quedan auditados
- [x] Todos los endpoints documentados en Swagger
- [x] `backend/README.md` ya no documenta el `INSERT` manual

Leyenda: `[x]` verificado · `[~]` cumplido por otra vía, con nota.

---

## 9. Cierre

- **Commit:** `de4994a` en `main`, árbol limpio.
- **Pruebas:** 95 en verde.
- **Verificación:** los 26 criterios del §8, contra PostgreSQL 16 y por HTTP.

### Lo que se comprobó a mano

| Comprobación | Resultado |
|---|---|
| Tres `POST /api/setup` simultáneos | Una sola instalación; las otras dos, 404 |
| Atributos de la cookie | `path=/; secure; samesite=strict; httponly`, sin `Max-Age` |
| Token en base de datos | Cero filas con el token en claro; hash de 44 caracteres |
| Umbral de `last_seen_at` | No se reescribe tras tres peticiones dentro del minuto |
| 8 h de inactividad / 7 días desde `issued_at` | 401 en ambos casos |
| Cambio de contraseña | Revoca las otras sesiones, conserva la actual |
| `PERSONA@EJEMPLO.PE` | Entra igual |
| `password_hash` en respuestas | Ninguna respuesta lo menciona |
| Acciones auditadas | `setup`, `login`, `login_failed` (24), `logout`, `create` |

### Tiempos de login, medidos contra el servidor real

| Correo inexistente | Correo existente |
|---|---|
| 0.389805 s | 0.388669 s |
| 0.373170 s | 0.370832 s |
| 0.367123 s | 0.360723 s |

La diferencia queda por debajo del ruido de red. El señuelo del §4 cumple su función.

Dos pruebas vigilan la secuencia, porque el orden se puede romper sin que nada deje de compilar: una cuenta las llamadas al verificador —así, borrar el señuelo en un refactor rompe la prueba en vez de abrir una fuga que solo se detecta con cronómetro— y otra fija el orden entre los pasos 3 y 5.

### Decisiones tomadas durante la implementación

1. El CRUD de usuarios entró en esta entrega aunque el SPEC lo listaba aparte: comparte toda la maquinaria con la autenticación —BCrypt, validación de roles, la regla del último `super_admin`— y separarlo obligaba a construirlo dos veces.
2. El hash señuelo se calcula al arrancar, no es constante (§4).
3. El umbral de coincidencia en la política de contraseñas subió de tres a cuatro caracteres (§1).
4. La guarda del último `super_admin` se conserva pese a ser inalcanzable hoy (§7.2).

### Corrección pendiente de aplicar — CSRF (entrega 2.1)

La implementación de la entrega 2 emite un token CSRF aleatorio y lo rota en cada llamada a `/csrf`, lo que hace que con dos pestañas abiertas la primera empiece a recibir 403. **El §3 de este documento ya recoge el diseño corregido** (token derivado por HMAC, ADR-012); el código todavía no.

Mientras no se aplique, sigue vigente la mitigación del `backend/README.md`: reintentar una vez ante un 403 de CSRF. Al aplicarla, esa recomendación se retira del README.

Criterios de aceptación de la corrección:

- [ ] Dos llamadas consecutivas a `GET /api/admin/auth/csrf` devuelven el mismo token
- [ ] Un token obtenido antes de otra llamada a `/csrf` sigue siendo válido
- [ ] Un token CSRF de otra sesión sigue devolviendo 403
- [ ] El token de una sesión revocada ya no sirve
- [ ] Reiniciar el proceso no invalida los tokens de las sesiones vivas
- [ ] `csrf_token_hash` sigue sin contener el token en claro

---

## 10. Fuera de alcance

| Qué | Cuándo |
|---|---|
| Recuperación de contraseña por correo | Cuando exista envío de correo |
| Segundo factor | Fase posterior |
| Permisos granulares por módulo | Cuando exista un caso real |
| Sesiones de clientes finales | M08 Portal del Cliente |
| Limitación de peticiones por IP | Cuando el sistema esté publicado en internet |
