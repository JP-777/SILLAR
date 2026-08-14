# Backend

Solución .NET de SILLAR: monolito modular, un proyecto por módulo, un solo
despliegue (ADR-002).

## Estructura

```
Sillar.sln
├── Sillar.Shared/            IModule, validación del grafo, tipos de plataforma
├── Sillar.Shared.Tests/      pruebas del validador del grafo (xUnit)
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

### Pruebas

`Sillar.Shared.Tests` cubre el validador del grafo de módulos, que es lo que
decide si el host arranca o aborta: ciclos, dependencias duras hacia módulos
inexistentes, códigos que no sirven como nombre de schema y activaciones
incoherentes. Es lógica pura, sin base de datos ni host, así que corre en
milisegundos. Los nombres de las pruebas están en español a propósito: la salida
de `dotnet test` se lee como la lista de reglas que cumple el arranque.

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

### Salir del modo instalación durante el desarrollo

Los endpoints `/api/setup*` llegan en la siguiente entrega de CORE. Hasta
entonces, la fila de instalación se crea a mano:

```sql
INSERT INTO core.installation
    (singleton, business_name, installation_key, product_version, license_type, is_setup_complete)
VALUES
    (true, 'Instalación de desarrollo', gen_random_uuid(), '1.0.0', 'trial', true)
ON CONFLICT (singleton) DO UPDATE SET is_setup_complete = true;
```

Al reiniciar, el host sincroniza el catálogo y `GET /api/capabilities` responde.

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
