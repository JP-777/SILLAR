# CORE · Entrega 2 — Instalación y autenticación

- **Módulo:** `core` · **Estado:** Aprobado
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
- Se rechaza si contiene el correo o el nombre del usuario.
- Sin caducidad ni rotación obligatoria.

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

- Al iniciar sesión se genera un token CSRF de 32 bytes. Su hash va a `csrf_token_hash`; el token en claro se devuelve **en el cuerpo de la respuesta**, nunca en una cookie.
- El frontend lo envía en la cabecera `X-CSRF-Token` en todo método que no sea `GET`, `HEAD` u `OPTIONS`.
- `GET /api/admin/auth/csrf` lo devuelve de nuevo para la sesión activa, por si el frontend se recarga.
- La comparación se hace **en tiempo constante**.
- Sin token válido: **403**.

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

Se implementa `ICurrentUser` del contrato de CORE. Ningún módulo consulta `core.admin_users` directamente.

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
3. Un usuario no puede desactivarse ni cambiarse el rol a sí mismo.
4. Al crear un usuario, la contraseña la fija el `super_admin` y pasa la política. No hay contraseñas generadas ni enviadas por correo mientras no exista envío de correo.
5. Cambiar el rol o desactivar a alguien revoca sus sesiones activas de inmediato.
6. Ninguna respuesta incluye `password_hash`.

---

## 8. Criterios de aceptación

**Instalación**

- [ ] Con la base recién migrada, `GET /api/setup/status` responde `setupRequired: true`
- [ ] `POST /api/setup` crea instalación y `super_admin`, y devuelve 201
- [ ] Tras la instalación, ambas rutas responden **404**
- [ ] Dos `POST /api/setup` simultáneos crean una sola instalación
- [ ] Una contraseña de 11 caracteres se rechaza; una de 12 se acepta
- [ ] Se rechaza una contraseña que contenga el correo del usuario

**Sesión**

- [ ] La cookie llega con `HttpOnly`, `Secure`, `SameSite=Strict` y sin `Max-Age`
- [ ] `admin_sessions.token_hash` no contiene el token en claro
- [ ] Una petición autenticada dentro del minuto siguiente **no** vuelve a escribir `last_seen_at`
- [ ] Una sesión con más de 8 horas de inactividad se rechaza
- [ ] Una sesión con más de 7 días desde `issued_at` se rechaza aunque haya actividad reciente
- [ ] `logout` marca `revoked_at`, y reutilizar esa cookie devuelve 401

**CSRF**

- [ ] Un `POST` sin `X-CSRF-Token` devuelve 403
- [ ] Un `POST` con un token CSRF de otra sesión devuelve 403
- [ ] Un `GET` sin token CSRF funciona con normalidad

**Login**

- [ ] Correo inexistente y contraseña incorrecta devuelven la misma respuesta 401
- [ ] La diferencia de tiempo entre correo existente e inexistente es despreciable
- [ ] Cinco fallos bloquean la cuenta 15 minutos
- [ ] Contraseña correcta con cuenta bloqueada devuelve **423**; contraseña incorrecta con cuenta bloqueada devuelve **401**
- [ ] Un acceso correcto reinicia `failed_login_count`
- [ ] Un usuario con `is_active = false` no puede entrar
- [ ] `PERSONA@EJEMPLO.PE` entra igual que `persona@ejemplo.pe`

**Usuarios**

- [ ] No se puede desactivar al último `super_admin` activo: 409
- [ ] Un usuario no puede desactivarse a sí mismo
- [ ] Desactivar a alguien revoca sus sesiones de inmediato
- [ ] Cambiar la propia contraseña revoca las demás sesiones y conserva la actual
- [ ] Ninguna respuesta del API contiene `password_hash`

**General**

- [ ] `setup`, `login`, `login_failed`, `logout` y los cambios de usuario quedan auditados
- [ ] Todos los endpoints documentados en Swagger
- [ ] `backend/README.md` ya no documenta el `INSERT` manual

---

## 9. Fuera de alcance

| Qué | Cuándo |
|---|---|
| Recuperación de contraseña por correo | Cuando exista envío de correo |
| Segundo factor | Fase posterior |
| Permisos granulares por módulo | Cuando exista un caso real |
| Sesiones de clientes finales | M08 Portal del Cliente |
| Limitación de peticiones por IP | Cuando el sistema esté publicado en internet |
