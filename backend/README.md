# Backend

Solución .NET de SILLAR: monolito modular, un proyecto por módulo, un solo
despliegue (ADR-002).

## Estructura

```
Sillar.sln
├── Sillar.Shared/            IModule, validación del grafo, tipos de plataforma
├── Sillar.Shared.Tests/      pruebas del grafo y la paginación (xUnit)
├── Sillar.Core.Tests/        pruebas de autenticación, CSRF y configuración (xUnit)
├── Sillar.Core.Contracts/    lo único que los demás módulos ven de CORE
├── Sillar.Core/              módulo núcleo: dominio, datos, migraciones, endpoints
└── Sillar.Api/               host: descubre módulos y registra solo los activos
```

Referencias permitidas:

```
Sillar.Shared          → (nada)
Sillar.Core.Contracts  → Sillar.Shared
Sillar.Core            → Sillar.Shared, Sillar.Core.Contracts
Sillar.Api             → los tres
```

Un módulo solo puede referenciar `Sillar.Shared`, `Sillar.Core.Contracts` y los
`Contracts` de sus dependencias declaradas. **Nunca** el `Domain` ni el `Data`
de otro módulo.

`Directory.Build.props` fija framework, nulabilidad y documentación XML para
todos. `Directory.Packages.props` fija las versiones de paquetes: un módulo
referencia el paquete sin versión.

## Requisitos

- .NET SDK 10
- PostgreSQL 16 levantado con `docker compose up -d` desde la raíz
- `.env` en la raíz del repositorio, copiado de `.env.example`

La cadena de conexión sale de `.env` (`ConnectionStrings__Default`). El host la
carga al arrancar y las herramientas de EF Core también, así que la credencial
vive en un solo sitio.

## Comandos

```bash
dotnet tool restore                  # instala dotnet-ef (una vez por máquina)
dotnet build Sillar.sln
dotnet test  Sillar.sln              # pruebas: no necesitan base de datos
dotnet run --project Sillar.Api      # http://localhost:5080, Swagger en /swagger
```

También se puede levantar el sistema entero en contenedores, como se despliega:

```bash
docker compose --profile full up -d --build   # base de datos + API
```

El servicio `api` queda fuera del arranque por defecto para no estorbar el ciclo
con `dotnet run`.

## Recuperar una instalación que no arranca

El host **aborta el arranque** si un módulo activo tiene una dependencia dura
inactiva (SPEC §7, paso 6). Es deliberado: prefiere caerse donde alguien lo ve a
funcionar a medias. El log lo dice con nombres:

```
crit: SILLAR no puede arrancar.
Las activaciones de esta instalación son incoherentes:
  · El módulo 'sales' está activo, pero su dependencia dura 'catalog' no lo está.
    Actívala o desactiva 'sales'.
```

**La trampa está en cómo se sale de ahí.** El endpoint que arreglaría esto vive
en el host, y el host no arranca; la única vía es SQL. Cualquiera de las dos
salidas sirve, y la elección depende de qué quería el negocio:

```sql
-- Opción A: activar la dependencia que falta.
UPDATE core.module_activations
   SET is_active = true, activated_at = now(), deactivated_at = NULL
 WHERE module_id = (SELECT module_id FROM core.modules WHERE code = 'catalog');

-- Opción B: desactivar el módulo que la reclama.
UPDATE core.module_activations
   SET is_active = false, deactivated_at = now()
 WHERE module_id = (SELECT module_id FROM core.modules WHERE code = 'sales');
```

Para ver el estado completo antes de decidir:

```sql
SELECT m.code, m.display_name, a.is_active
  FROM core.modules m
  LEFT JOIN core.module_activations a USING (module_id)
 ORDER BY m.display_order, m.code;
```

Después, arrancar de nuevo. El arranque vuelve a validar el grafo y dirá si
queda algo.

**Cuándo puede pasar esto.** Con el API funcionando, no debería: el endpoint de
activación valida con la misma función que el arranque, relee el estado antes de
confirmar, y desde la entrega 3b toma un `pg_advisory_xact_lock` que impide que
dos administradores simultáneos confirmen cambios que juntos rompen el sistema.
Los caminos que quedan son editar `core.module_activations` a mano, restaurar un
respaldo tomado de otra versión del producto, o desplegar una versión que ya no
incluye un módulo que estaba activo.

## Respaldo — las dos cosas, siempre juntas

**La base de datos y la carpeta de archivos se respaldan y se restauran juntas.**
No es una recomendación: es la consecuencia negativa que anota el ADR-011 y el
error clásico del que avisa. Se vuelca la base, se olvida la carpeta, y al
restaurar aparece un catálogo entero sin una sola imagen.

```bash
# Respaldar
docker compose exec -T db pg_dump -U postgres sillar_dev > respaldo.sql
cp -r media/ respaldo-media/

# Restaurar
docker compose exec -T db psql -U postgres -d sillar_dev < respaldo.sql
cp -r respaldo-media/. media/
```

La carpeta se configura en `Media:RootPath` y, en contenedor, se monta desde
`MEDIA_PATH` del `.env`. Es una carpeta del host y no un volumen con nombre
justamente para esto: respaldarla es copiar algo que se ve en el explorador de
archivos, y así el respaldo se hace de verdad.

Nunca entra al repositorio: son datos de una instalación, no del producto.

## Medios

- Solo **JPEG, PNG y WebP**, con un máximo de 5 MB (`Media:MaxSizeBytes`).
- **El tipo se decide por los bytes iniciales**, nunca por la extensión ni por el
  `Content-Type` que envía el cliente: los dos los elige quien sube el archivo.
  Un `.png` cuyo contenido es otra cosa se rechaza con 415.
- El límite se aplica además en Kestrel, para que un cuerpo enorme no llegue
  siquiera a recibirse.
- **SVG se rechaza, y no debe reintroducirse con un saneador.** Un SVG es XML con
  scripts que, servido desde `/media`, se ejecuta en el mismo origen que el
  panel: la cookie viaja sola y el token CSRF se puede pedir. La salida, si algún
  día hace falta vectorial, es servir los medios desde otro origen.
- El nombre en disco se genera; el original solo se guarda para mostrarlo. Es lo
  que hace inofensivo un nombre con `../`.
- La baja es lógica: el binario se conserva, pero deja de servirse.
- `is_orphan` marca los archivos de módulos **desinstalados**, no desactivados, y
  se recalcula al arrancar. Desactivar un módulo no toca nada.

## Activar y desactivar módulos

El enrutamiento se construye al arrancar (SPEC §7): escribir la fila de
activación no hace aparecer ni desaparecer rutas en el proceso vivo. Por eso
`POST /api/admin/modules/{code}/activate` **detiene el host después de
responder** y lo relanza el orquestador.

- `Modules:RestartAfterActivation` gobierna la conducta. En desarrollo vale
  `false`: el proceso sigue en pie y la respuesta dice `restart: required`.
  El servicio `api` de `docker-compose.yml` lo pone en `true`.
- **El contenedor del API necesita `restart: unless-stopped`.** Sin eso, activar
  un módulo apaga el sistema y no lo vuelve a encender. Ya está puesto; si
  alguien toca ese servicio, es lo primero que hay que conservar.
- Las sesiones y los tokens CSRF sobreviven al reinicio: viven en base de datos
  y se derivan de `installation_key` (ADR-012). Quien active un módulo encuentra
  su sesión intacta al reconectar.
- La validación del endpoint es **la misma función** que usa el arranque
  (`ModuleGraph.ValidateActivations`), y el estado resultante se relee de la base
  y se vuelve a validar antes de confirmar la transacción. Si el host no
  arrancaría con ese estado, la operación se deshace y no se escribe nada.
- La transacción toma un `pg_advisory_xact_lock` sobre una clave constante,
  **antes de leer nada**. Validar dentro de la transacción protege contra un
  cambio malo, pero no contra dos cambios buenos que juntos son malos: con dos
  administradores operando a la vez, cada uno ve su propia instantánea, ambos se
  aprueban y el resultado no arranca. El bloqueo los serializa y se libera solo
  al terminar la transacción.

### Pruebas

`Sillar.Shared.Tests` cubre el validador del grafo de módulos, que es lo que
decide si el host arranca o aborta: ciclos, dependencias duras hacia módulos
inexistentes, códigos que no sirven como nombre de schema y activaciones
incoherentes.

`Sillar.Core.Tests` cubre la lógica de autenticación: la secuencia de inicio de
sesión, la política de contraseñas, la vigencia de las sesiones, la jerarquía de
roles y los tokens.

Ambos son lógica pura, sin base de datos ni host, así que corren en
milisegundos. Los nombres de las pruebas están en español a propósito: la salida
de `dotnet test` se lee como la lista de reglas que el sistema garantiza.

Dos de esas pruebas vigilan un **orden**, no un cálculo, y por eso conviene no
tocarlas a la ligera:

- `El_senuelo_no_se_calcula_cuando_la_cuenta_existe` y su pareja para el correo
  desconocido. Sin ese cálculo señuelo, la respuesta a un correo no registrado
  llega mucho antes que la de uno real, y ese margen revela qué correos existen.
- `Con_la_contrasena_incorrecta_y_la_cuenta_bloqueada_devuelve_401_y_no_423`.
  La contraseña se verifica **antes** de mirar el bloqueo: quien no la sabe
  recibe siempre el mismo 401, y solo quien sí la sabe recibe el 423 con una
  explicación útil.

El código sigue compilando si alguien invierte ese orden. Lo único que cambia es
que el formulario de acceso empieza a contar qué cuentas existen.

Hay una tercera del mismo tipo:
`Ninguna_secuencia_de_operaciones_permitidas_deja_el_sistema_sin_arrancar`.
Recorre **todos** los estados de activación alcanzables sobre un grafo de seis
módulos y comprueba que el validador del arranque acepta cada uno. Vigila que el
endpoint de activación no pueda persistir un estado que impida arrancar — un
fallo ahí deja la instalación muerta, porque el host se detiene justo después y
solo se recupera por SQL.

### Migraciones

Las migraciones de EF Core son la fuente de verdad del esquema (ADR-009). Cada
módulo lleva las suyas y su historial `__migrations` dentro de su propio schema.

```bash
# aplicar
dotnet ef database update --project Sillar.Core --startup-project Sillar.Api

# ver el SQL sin ejecutarlo
dotnet ef migrations script --project Sillar.Core --startup-project Sillar.Api

# crear una nueva (y revisarla a mano antes de darla por buena)
dotnet ef migrations add <Nombre> --project Sillar.Core --startup-project Sillar.Api
```

En desarrollo el host puede aplicarlas al arrancar:

```bash
Sillar__Database__ApplyMigrationsOnStartup=true dotnet run --project Sillar.Api
```

**En producción nunca se aplican solas.** La bandera se ignora fuera de
Development y queda constancia en el log.

## Arranque

El host ejecuta siempre la misma secuencia (SPEC de CORE §7):

1. Descubre las implementaciones de `IModule` en los ensamblados publicados.
2. Valida el grafo en memoria: códigos, dependencias y ausencia de ciclos.
   Si falla, **aborta**: es un error de cómo está escrito el producto.
3. Conecta con la base. Sin schema `core` o sin instalación completada, entra en
   **modo instalación** y no monta ninguna ruta de negocio.
4. Sincroniza `core.modules` y `core.module_dependencies` desde el código.
5. Crea la activación que falte. CORE siempre activo.
6. Comprueba que las dependencias duras de lo activo estén activas.
   Si falla, **aborta** diciendo qué módulo y qué dependencia.
7. Registra servicios y rutas **solo** de los módulos activos. Un módulo
   inactivo no devuelve 403: su ruta no existe.

### Completar la instalación

Con la base recién migrada, el host arranca en modo instalación y solo responde
`/api/setup*`:

```bash
curl -X POST http://localhost:5080/api/setup -H 'Content-Type: application/json' -d '{
  "businessName": "Negocio de desarrollo",
  "licenseType": "trial",
  "admin": {
    "fullName": "Nombre Apellido",
    "email": "persona@ejemplo.pe",
    "password": "una contraseña larga"
  }
}'
```

Devuelve 201 y **el host se detiene** para volver a arrancar en modo normal: en
Docker el contenedor reinicia solo; con `dotnet run`, se relanza a mano. A partir
de ahí `/api/setup*` responde 404 y funciona el resto del API.

La instalación no abre sesión. Se entra después:

```bash
curl -X POST http://localhost:5080/api/admin/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"persona@ejemplo.pe","password":"una contraseña larga"}'
```

## Sesión y CSRF

- Cookie `sillar_panel`: `HttpOnly`, `Secure`, `SameSite=Strict`, sin `Max-Age`.
  Muere al cerrar el navegador; la autoridad sobre la vigencia es la fila de
  `core.admin_sessions`. **`Secure` también en desarrollo**: los navegadores
  tratan `localhost` como contexto seguro. Un problema de sesión en local nunca
  es por esto.
- Toda petición que no sea `GET`, `HEAD` u `OPTIONS` exige la cabecera
  `X-CSRF-Token` con el token que devolvió el login. Sin ella, 403.
- **`GET /api/admin/auth/csrf` es idempotente**: devuelve siempre el mismo token
  para la misma sesión y no invalida nada. El token se deriva de la identidad de
  la sesión por HMAC (ADR-012), así que el frontend puede pedirlo cuando le
  convenga y desde tantas pestañas como haga falta, sin coordinarse. **Un 403
  significa una sola cosa: no tienes permiso.** No hay que reintentar.
- El token CSRF **no rota dentro de una sesión**. Si se filtrara, la salida es
  cerrar la sesión: eso hacen `logout` y el cambio de contraseña.
- `core.installation.installation_key` es el origen de la clave CSRF, además de
  identificar la instalación. **No se expone en ninguna respuesta del API y no se
  rota a la ligera**: cambiarla obliga a todas las sesiones vivas a volver a
  pedir su token.
- Sesión de 8 horas de inactividad, tope absoluto de 7 días, y `last_seen_at`
  solo se reescribe si tiene más de un minuto.
- Contraseñas con BCrypt. El factor de trabajo se configura en
  `Sillar:Security:PasswordWorkFactor` y **no puede bajar de 12**: el host se
  niega a arrancar si se intenta.

## Añadir un módulo

1. Proyecto `Sillar.Modules.<Nombre>` con `Contracts/`, `Domain/`, `Data/`,
   `Endpoints/` y su clase `<Nombre>Module : IModule`.
2. Referencias: `Sillar.Shared`, `Sillar.Core.Contracts` y los `Contracts` de
   sus dependencias duras declaradas.
3. `ProjectReference` desde `Sillar.Api`. **Ese es el único sitio** donde el
   host se entera de que existe; el descubrimiento hace el resto.
4. Su `DbContext` con `HasDefaultSchema("<código>")` y su historial
   `__migrations` dentro de ese schema.
5. Su seed y su `99_drop.sql` en `database/modules/<código>/`.

Todo módulo declara `core` entre sus dependencias duras. El arranque lo exige.

## Convenciones que vigila el código

- Nombres de base de datos en `snake_case`; restricciones con `pk_`, `fk_`,
  `uq_`, `ck_`, `idx_`. Se declaran explícitamente en cada configuración de
  entidad, incluidos los índices de claves foráneas que EF crearía con su
  propio nombre.
- `integer GENERATED ALWAYS AS IDENTITY` mediante `UseIdentityAlwaysColumn()`.
- `timestamptz` en toda fecha, mapeado a `DateTimeOffset`.
- `created_at` con `DEFAULT now()`; `updated_at` la escribe el trigger
  `core.set_updated_at()`, creado en la migración porque EF no genera triggers.
- Dos colaciones compartidas, creadas por la migración de CORE y usables por
  cualquier módulo, que depende de CORE de forma dura:

  | Colación | Nivel | Iguala | Para |
  |---|---|---|---|
  | `core.es_ci` | level2 | mayúsculas | identidad y unicidad: correos, claves |
  | `core.es_search` | level1 | mayúsculas y tildes | lo que el usuario busca |

  No confundirlas: con `es_search`, `josé@ejemplo.pe` y `jose@ejemplo.pe` serían
  el mismo buzón. Ninguna iguala la ñ con la n. Y ojo: PostgreSQL no admite
  `LIKE` sobre columnas con colación no determinista, así que la búsqueda por
  patrón necesita `pg_trgm`, no la colación.
- Un valor por defecto en columna se declara junto a `ValueGeneratedNever()`:
  sin eso, EF omite el valor cuando coincide con el de C# y acabaría guardando
  el de la base. Es la trampa clásica con `is_active = false`.
