# SPEC — CORE · Núcleo de plataforma

- **Código:** `core`
- **Schema:** `core`
- **Versión:** 1.0.0
- **Estado:** Aprobado
- **Fase:** 0 y 1 — es lo primero que se construye

---

## 1. Propósito

CORE es la base sobre la que se enchufan los demás módulos. Contiene la identidad de la instalación, el catálogo de módulos y su activación, los administradores del negocio, la autenticación, la configuración general del sitio, la gestión de archivos y la auditoría.

**No es vendible ni desmontable.** Está siempre presente y siempre activo. Si CORE no arranca, no arranca nada.

## 2. Valor comercial

CORE no se cobra por separado: es lo que convierte a SILLAR en un producto instalable en vez de un montón de código. Su función comercial es hacer visible y operable el modelo de negocio — que el cliente vea qué módulos tiene, qué le falta y qué podría añadir.

---

## 3. Dependencias

| Módulo | Tipo | Qué necesita | Si no está |
|---|---|---|---|
| — | — | Ninguna | No aplica |

**Módulos que dependen de CORE:** todos, con dependencia dura.

---

## 4. Modelo de datos — schema `core`

Convenciones vigentes: `integer GENERATED ALWAYS AS IDENTITY`, `timestamptz`, eliminación lógica con `is_active`, `created_at` con `DEFAULT now()`, `updated_at` por trigger, `CHECK` de texto no vacío en campos obligatorios.

### 4.0 Colaciones compartidas

La primera migración de CORE crea **dos** colaciones no deterministas. Viven en el schema
`core` porque las usarán varios módulos y CORE es dependencia dura de todos.

```sql
-- Identidad y unicidad: ignora mayúsculas, RESPETA tildes
CREATE COLLATION core.es_ci (
    provider      = icu,
    locale        = 'es-PE-u-ks-level2',
    deterministic = false
);

-- Búsqueda del usuario: ignora mayúsculas Y tildes
CREATE COLLATION core.es_search (
    provider      = icu,
    locale        = 'es-PE-u-ks-level1',
    deterministic = false
);
```

Son dos necesidades distintas y una sola colación no puede servir a ambas:

| Caso | `es_ci` (level2) | `es_search` (level1) |
|---|---|---|
| `LAPIZ` = `lapiz` | sí | sí |
| `lapiz` = `LÁPIZ` | **no** | sí |
| `José` = `Jose` | **no** | sí |
| `ñoño` = `NOÑO` | no | no |

**`es_ci` para identidad.** Se usa en `admin_users.email` y en cualquier campo con
restricción de unicidad. Ahí conviene respetar las tildes: `josé@ejemplo.pe` y
`jose@ejemplo.pe` son buzones distintos, y tratarlos como el mismo permitiría que alguien
bloqueara el registro de otro.

**`es_search` para búsqueda.** Se usa en los campos por los que el usuario busca —nombre de
producto, marca, palabras clave— porque nadie escribe las tildes al buscar. Es la que hace
que `lapiz` encuentre `Lápiz técnico HB`.

Nota sobre la ñ: en español la ñ es una letra propia, no una n con virgulilla, así que
**ninguna** de las dos colaciones iguala `ñ` con `n`. Es el comportamiento correcto.

El clúster ya viene con colación ICU `es-PE` y búsqueda de texto en español desde
`docker-compose.yml`.

### 4.1 `core.installation`

Identidad del negocio instalado y datos de licencia. **Contiene exactamente una fila.**

| Campo | Tipo | Nulo | Clave | Descripción |
|---|---|---|---|---|
| installation_id | integer | No | PK | Identificador |
| singleton | boolean | No | UNIQUE | Siempre `true`. Garantiza fila única |
| business_name | varchar(150) | No | | Nombre comercial del negocio |
| installation_key | uuid | No | UNIQUE | Identificador de la instalación, generado al instalar |
| product_version | varchar(20) | No | | Versión de SILLAR instalada |
| license_type | varchar(30) | No | | `trial`, `subscription`, `perpetual` |
| licensed_until | timestamptz | Sí | | Vencimiento. Nulo en licencia perpetua |
| is_setup_complete | boolean | No | | Marca el fin del modo instalación |
| created_at / updated_at | timestamptz | | | Auditoría |

**Restricciones:** `ck_installation_singleton CHECK (singleton)`, `uq_installation_singleton UNIQUE (singleton)`, `ck_installation_license_type CHECK (license_type IN ('trial','subscription','perpetual'))`, `ck_installation_business_name_not_empty`.

El par singleton/único es el truco que impide una segunda fila: como `singleton` solo puede valer `true` y es único, la tabla admite una fila y ninguna más.

### 4.2 `core.modules`

Catálogo de módulos que el producto conoce. **Se sincroniza desde el código al arrancar; nunca se edita a mano.** La fuente de verdad es la implementación de `IModule`; esta tabla es una proyección para poder consultarla desde el panel y desde SQL.

| Campo | Tipo | Nulo | Clave | Descripción |
|---|---|---|---|---|
| module_id | integer | No | PK | |
| code | varchar(40) | No | UNIQUE | `core`, `catalog`, `sales`… |
| display_name | varchar(80) | No | | Nombre visible |
| description | varchar(300) | **No** | | Qué hace, en lenguaje de negocio |
| version | varchar(20) | No | | Versión del módulo |
| is_core | boolean | No | | `true` solo para CORE. No se puede desactivar |
| display_order | integer | No | | Orden en el panel |
| created_at / updated_at | timestamptz | | | |

**Restricciones:** `ck_modules_display_name_not_empty`, `ck_modules_description_not_empty`, `ck_modules_display_order CHECK (display_order >= 0)`.

`description` es **obligatoria**. Esta tabla alimenta la pantalla donde el negocio ve sus
módulos y decide qué activar o qué comprar: un módulo sin descripción es una fila en blanco
en la pantalla que sostiene el argumento de venta.

### 4.3 `core.module_dependencies`

Proyección del grafo de dependencias, también sincronizada desde el código.

| Campo | Tipo | Nulo | Clave | Descripción |
|---|---|---|---|---|
| module_dependency_id | integer | No | PK | |
| module_id | integer | No | FK → modules | El que depende |
| depends_on_module_id | integer | No | FK → modules | Del que depende |
| kind | varchar(10) | No | | `hard` o `soft` |

**Restricciones:** `uq (module_id, depends_on_module_id)`, `ck_kind CHECK (kind IN ('hard','soft'))`, `ck_no_self CHECK (module_id <> depends_on_module_id)`.

### 4.4 `core.module_activations`

Qué está activo **en esta instalación**. Se separa de `modules` porque tienen dueños distintos: `modules` lo escribe el producto, `module_activations` lo escribe la licencia del cliente.

| Campo | Tipo | Nulo | Clave | Descripción |
|---|---|---|---|---|
| module_activation_id | integer | No | PK | |
| module_id | integer | No | FK UNIQUE | Un módulo, una activación |
| is_active | boolean | No | | Estado actual |
| activated_at | timestamptz | Sí | | Última activación |
| deactivated_at | timestamptz | Sí | | Última desactivación |
| expires_at | timestamptz | Sí | | Vencimiento del módulo, si aplica |
| notes | varchar(250) | Sí | | Nota administrativa |
| created_at / updated_at | timestamptz | | | |

### 4.5 `core.admin_users`

| Campo | Tipo | Nulo | Clave | Descripción |
|---|---|---|---|---|
| admin_user_id | integer | No | PK | |
| full_name | varchar(150) | No | | |
| email | varchar(150) | No | UNIQUE | Sirve de identificador de acceso |
| password_hash | varchar(255) | No | | BCrypt, factor ≥ 12 |
| role | varchar(20) | No | | `super_admin`, `admin`, `editor` |
| phone | varchar(30) | Sí | | |
| is_active | boolean | No | | Eliminación lógica |
| last_login_at | timestamptz | Sí | | |
| failed_login_count | integer | No | | Contador de intentos fallidos |
| locked_until | timestamptz | Sí | | Bloqueo temporal |
| created_at / updated_at | timestamptz | | | |

**Restricciones:** `ck_role CHECK (role IN ('super_admin','admin','editor'))`, `ck_failed_login_count CHECK (failed_login_count >= 0)`, textos obligatorios no vacíos.

**Índices:** `idx_admin_users_is_active`.

### 4.6 `core.admin_sessions`

| Campo | Tipo | Nulo | Clave | Descripción |
|---|---|---|---|---|
| admin_session_id | uuid | No | PK | Generado por la aplicación |
| admin_user_id | integer | No | FK → admin_users, ON DELETE CASCADE | |
| token_hash | varchar(255) | No | UNIQUE | **Hash** del token. Nunca el token |
| csrf_token_hash | varchar(255) | No | | Hash del token CSRF de la sesión |
| issued_at | timestamptz | No | | |
| last_seen_at | timestamptz | No | | Para la renovación deslizante |
| expires_at | timestamptz | No | | |
| revoked_at | timestamptz | Sí | | Cierre de sesión o revocación |
| ip_address | varchar(45) | Sí | | Admite IPv6 |
| user_agent | varchar(300) | Sí | | |

**Índices:** `idx_admin_sessions_user`, `idx_admin_sessions_expires_at`.

### 4.7 `core.site_settings`

| Campo | Tipo | Nulo | Clave | Descripción |
|---|---|---|---|---|
| site_setting_id | integer | No | PK | |
| setting_key | varchar(100) | No | UNIQUE | |
| setting_value | text | No | | |
| value_type | varchar(20) | No | | `text`, `number`, `boolean`, `url`, `email`, `json` |
| description | varchar(250) | Sí | | Para qué sirve |
| is_public | boolean | No | | **Por defecto `false`** |
| is_active | boolean | No | | |
| created_at / updated_at | timestamptz | | | |

`is_public` es el campo crítico: determina si el valor se expone en el endpoint público. El número de WhatsApp sí; una clave de correo saliente, jamás. Por eso el valor por defecto es `false` y publicar algo tiene que ser un acto deliberado.

### 4.8 `core.media_assets`

| Campo | Tipo | Nulo | Clave | Descripción |
|---|---|---|---|---|
| media_asset_id | integer | No | PK | |
| stored_name | varchar(120) | No | UNIQUE | Nombre **generado**. Nunca el original |
| original_name | varchar(255) | Sí | | Solo informativo, para mostrar |
| relative_path | varchar(300) | No | | Ruta dentro del volumen |
| mime_type | varchar(100) | No | | Tipo real verificado, no la extensión |
| size_bytes | bigint | No | | |
| width / height | integer | Sí | | Solo imágenes |
| alt_text | varchar(180) | Sí | | Accesibilidad |
| owner_module_code | varchar(40) | Sí | | Qué módulo lo subió. Texto, sin FK |
| checksum | varchar(64) | Sí | | SHA-256, para detectar duplicados |
| is_orphan | boolean | No | | Su módulo fue desinstalado |
| is_active | boolean | No | | |
| created_by | integer | Sí | FK → admin_users, ON DELETE SET NULL | |
| created_at / updated_at | timestamptz | | | |

`owner_module_code` se guarda como texto y sin clave foránea a propósito: el módulo puede desinstalarse y el archivo tiene que sobrevivir marcado como huérfano.

**Restricciones:** `ck_size_bytes CHECK (size_bytes > 0)`.
**Índices:** `idx_media_assets_owner_module_code`, `idx_media_assets_checksum`.

### 4.9 `core.audit_log`

| Campo | Tipo | Nulo | Clave | Descripción |
|---|---|---|---|---|
| audit_log_id | bigint | No | PK | |
| occurred_at | timestamptz | No | | |
| admin_user_id | integer | Sí | FK → admin_users, ON DELETE SET NULL | Nulo si fue el sistema |
| admin_user_email | varchar(150) | Sí | | **Snapshot**: sobrevive al borrado del usuario |
| module_code | varchar(40) | Sí | | Texto, sin FK |
| entity_type | varchar(60) | Sí | | `product`, `order`, `module`… |
| entity_id | varchar(60) | Sí | | Texto, porque no todas las claves son enteras |
| action | varchar(30) | No | | Ver lista abajo |
| summary | varchar(300) | Sí | | Descripción legible |
| ip_address | varchar(45) | Sí | | |

**Acciones:** `create`, `update`, `delete`, `activate`, `deactivate`, `login`, `login_failed`, `logout`, `setup`.
**Índices:** `idx_audit_log_occurred_at`, `idx_audit_log_admin_user_id`, `idx_audit_log_module_code`.

El correo se guarda como snapshot por la misma razón que el pedido guarda el nombre del producto: un registro de auditoría que pierde la identidad de quien actuó no sirve de nada.

### 4.10 Relaciones internas

```
modules 1 ─── 1 module_activations
modules 1 ─── N module_dependencies (module_id)
modules 1 ─── N module_dependencies (depends_on_module_id)
admin_users 1 ─── N admin_sessions
admin_users 1 ─── N media_assets      (created_by, opcional)
admin_users 1 ─── N audit_log         (opcional)
```

No hay relaciones cruzadas con otros schemas. CORE no conoce a nadie; todos lo conocen a él.

### 4.11 Datos semilla

`database/modules/core/02_seed.sql` inserta la configuración base, toda con `PENDIENTE_DEFINIR` donde corresponda datos del negocio:

`business_name`, `main_message`, `whatsapp_number`, `contact_email`, `contact_phone`, `business_address`, `business_reference`, `google_maps_url`, `business_hours`, `currency_code` (`PEN`), `currency_symbol` (`S/`).

Marcar como públicos únicamente: `business_name`, `main_message`, `whatsapp_number`, `contact_email`, `contact_phone`, `business_address`, `business_reference`, `google_maps_url`, `business_hours`, `currency_code`, `currency_symbol`.

**El seed no crea usuarios.** El primer administrador se crea en el modo instalación, con una contraseña que elige la persona. Nunca una credencial por defecto.

---

## 5. Contrato público — `Sillar.Core.Contracts`

Lo único que los demás módulos pueden ver de CORE.

```csharp
public interface IModuleRegistry
{
    bool IsActive(string moduleCode);
    IReadOnlyList<ActiveModule> GetActive();
}

public interface ISettingsReader
{
    string?  Get(string key);
    T?       Get<T>(string key);
    IReadOnlyDictionary<string,string> GetPublic();
}

public interface ICurrentUser
{
    int?    AdminUserId { get; }
    string? Email       { get; }
    string? Role        { get; }
    bool    IsInRole(string role);
}

public interface IMediaStorage
{
    Task<MediaAsset> SaveAsync(Stream content, string originalName, string ownerModuleCode, CancellationToken ct);
    Task<bool>       DeleteAsync(int mediaAssetId, CancellationToken ct);
    string           GetPublicUrl(int mediaAssetId);
}

public interface IAuditWriter
{
    Task WriteAsync(AuditEntry entry, CancellationToken ct);
}
```

**Eventos publicados:** `ModuleActivated`, `ModuleDeactivated`, `SettingChanged`.
**Eventos consumidos:** ninguno.

---

## 6. Endpoints

### Públicos

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/capabilities` | Producto, versión y lista de módulos activos con su versión. **No expone datos de licencia.** |
| GET | `/api/settings/public` | Solo las configuraciones con `is_public = true` |

Ejemplo de respuesta de capacidades:

```json
{
  "product": "SILLAR",
  "version": "1.0.0",
  "modules": [
    { "code": "core",    "version": "1.0.0" },
    { "code": "catalog", "version": "1.0.0" },
    { "code": "cms",     "version": "1.0.0" }
  ]
}
```

### Autenticación

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/admin/auth/login` | Devuelve cookie de sesión y token CSRF |
| POST | `/api/admin/auth/logout` | Revoca la sesión en base de datos |
| GET | `/api/admin/auth/me` | Usuario actual y su rol |

### Administración — requieren sesión

| Método | Ruta | Rol mínimo |
|---|---|---|
| GET | `/api/admin/modules` | admin |
| POST | `/api/admin/modules/{code}/activate` | super_admin |
| POST | `/api/admin/modules/{code}/deactivate` | super_admin |
| GET | `/api/admin/settings` | admin |
| PUT | `/api/admin/settings/{key}` | admin |
| GET · POST | `/api/admin/users` | super_admin |
| PUT · DELETE | `/api/admin/users/{id}` | super_admin |
| GET | `/api/admin/sessions` | super_admin |
| DELETE | `/api/admin/sessions/{id}` | super_admin |
| POST · GET | `/api/admin/media` | editor |
| DELETE | `/api/admin/media/{id}` | admin |
| GET | `/api/admin/audit` | super_admin |

### Instalación — solo con `is_setup_complete = false`

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/setup/status` | Si la instalación está pendiente |
| POST | `/api/setup` | Datos del negocio y primer `super_admin` |

Cuando la instalación se completa, estas rutas **dejan de responder**.

---

## 7. Arranque del host

Es la parte más delicada de CORE. La secuencia importa.

```
1. Descubrir todas las implementaciones de IModule presentes en la solución.
2. Validar el grafo en memoria: sin ciclos, sin dependencias duras hacia módulos inexistentes.
   → Si falla, ABORTAR con mensaje explícito. Es un error de compilación lógica.
3. Conectar a la base de datos.
   → Si el schema core no existe o is_setup_complete = false: MODO INSTALACIÓN.
     Solo se exponen /api/setup*. Fin del arranque.
4. Sincronizar core.modules y core.module_dependencies desde el código.
5. Crear la fila de module_activations que falte, inactiva por defecto. CORE siempre activo.
6. Leer las activaciones. Para cada módulo activo, verificar que sus dependencias DURAS
   están activas.
   → Si falla, ABORTAR indicando qué módulo y qué dependencia. Nunca degradar en silencio.
7. Registrar servicios y endpoints ÚNICAMENTE de los módulos activos.
8. Migraciones: en desarrollo se aplican si la bandera lo permite; en producción, nunca.
```

El punto 6 merece énfasis: si Seguimiento está activo y Órdenes de Servicio no, el sistema **no arranca**. Prefiere caerse en el despliegue, donde alguien lo ve, a funcionar a medias en producción.

### Modo instalación

Base vacía o instalación sin completar: el API solo expone `/api/setup*` y el frontend muestra el asistente. Se piden el nombre del negocio, el tipo de licencia y el primer `super_admin` con su contraseña. Al terminar se crea la fila de `installation` con `is_setup_complete = true`, se registra un evento de auditoría con acción `setup` y el host se reinicia en modo normal.

---

## 8. Reglas de negocio

1. **CORE nunca se desactiva.** El endpoint lo rechaza; `is_core = true` lo protege.
2. Un módulo **no se activa** si alguna de sus dependencias duras está inactiva.
3. Un módulo **no se desactiva** si otro módulo activo depende de él de forma dura.
4. Debe existir **al menos un `super_admin` activo**. La operación que dejaría cero se rechaza.
   *Implementada en la entrega 2, pero inalcanzable con las rutas actuales: la regla 5 la cubre antes. Ver `ENTREGA-02-AUTENTICACION.md` §7.2.*
5. Un usuario **no puede desactivarse ni borrarse a sí mismo**.
6. Contraseñas con BCrypt, factor de trabajo ≥ 12. Nunca en claro, nunca en registros de log, nunca en respuestas del API.
7. Sesión de 8 horas de inactividad con renovación deslizante. Cerrar sesión **revoca la fila**, no basta con borrar la cookie.
8. Tras 5 intentos fallidos, la cuenta se bloquea 15 minutos. El contador se reinicia con un acceso correcto.
9. El mensaje de error de acceso es **siempre el mismo**, exista o no la cuenta. Nunca revelar si un correo está registrado.
10. `is_public` en configuración vale `false` por defecto. Publicar es un acto deliberado.
11. Los archivos se guardan con nombre generado; el original solo se conserva para mostrarlo.
12. Se valida el tipo real del archivo, no la extensión, y se aplica límite de tamaño.
13. Al desinstalar un módulo, sus archivos **no se borran**: se marcan como huérfanos.
14. Toda acción administrativa que modifica datos se registra en auditoría.
15. Los registros de auditoría **no se editan ni se borran** desde el API.

---

## 9. Criterios de aceptación

- [ ] El schema `core` se crea con migraciones y se elimina con `99_drop.sql` sin afectar a otros
- [ ] `core.es_ci` existe: `'LAPIZ' = 'lapiz'` verdadero, `'José' = 'Jose'` falso
- [ ] `core.es_search` existe: `'lapiz' = 'LÁPIZ'` verdadero
- [ ] El seed es idempotente: ejecutarlo dos veces no duplica ni falla
- [ ] Con la base vacía, el sistema entra en modo instalación y solo expone `/api/setup*`
- [ ] Completada la instalación, las rutas de instalación dejan de responder
- [ ] `core.installation` rechaza una segunda fila
- [ ] Activar un módulo con dependencia dura inactiva se rechaza con mensaje claro
- [ ] Desactivar un módulo del que otro depende se rechaza con mensaje claro
- [ ] Arrancar con un módulo activo cuya dependencia dura está inactiva **aborta el arranque**
- [ ] `/api/capabilities` responde sin sesión y no filtra información de licencia
- [ ] `/api/settings/public` devuelve solo las marcadas como públicas
- [ ] La cookie de sesión es `httpOnly`, `Secure` y `SameSite=Strict`
- [ ] Una petición que modifica datos sin token CSRF válido se rechaza
- [ ] Cerrar sesión invalida el token también del lado del servidor
- [ ] Tras 5 intentos fallidos la cuenta queda bloqueada
- [ ] El mensaje de error de acceso es idéntico con correo existente e inexistente
- [ ] Ninguna respuesta del API contiene `password_hash`
- [ ] No se puede dejar el sistema sin `super_admin` activo
- [ ] Subir un archivo con nombre malicioso lo almacena con nombre generado y seguro
- [ ] Subir un archivo cuyo contenido no coincide con su extensión se rechaza
- [ ] Todas las acciones administrativas quedan registradas en auditoría
- [ ] Todos los endpoints documentados en Swagger
- [ ] El panel funciona correctamente en escritorio

---

## 10. Fuera de alcance

| Qué | Dónde va |
|---|---|
| Segundo factor de autenticación | Fase posterior |
| Recuperación de contraseña por correo | Fase posterior, requiere envío de correo |
| Permisos granulares por módulo | Cuando exista un caso real que lo pida |
| Firma criptográfica de la licencia y control de vencimiento | Fase 5, comercialización |
| Cuentas de clientes finales | M08 Portal del Cliente |
| Envío de correo | Módulo propio, cuando algún módulo lo necesite |
| Almacenamiento externo de archivos | Implementación alternativa de `IMediaStorage`, sin cambios en módulos |
