# Scripts de base de datos

La base de datos se instala **por módulo**, no de una sola vez. Qué módulos se instalan
depende de lo que el cliente tenga licenciado.

**Las tablas las crean las migraciones de EF Core**, no scripts SQL (ADR-009). Cada módulo
lleva sus migraciones y su tabla de historial `__migrations` dentro de su propio schema.
Aquí vive solo lo que las migraciones no cubren.

## Estructura

```
modules/<codigo>/02_seed.sql     datos mínimos para que el módulo funcione
modules/<codigo>/99_drop.sql     desinstala el módulo (DROP SCHEMA ... CASCADE)
integrations/<a>_<b>.sql         FK opcional entre dos módulos con dependencia blanda
integrations/<a>_<b>_drop.sql    elimina esa FK y anula referencias huérfanas
```

## Colación y búsqueda en español

El clúster se crea con **proveedor ICU y locale `es-PE`**, y con
`default_text_search_config = pg_catalog.spanish`. Ambas cosas se fijan en `docker-compose.yml`.

Esto importa más de lo que parece. Con la colación `C` que traen las imágenes sin locale,
PostgreSQL ordena así:

```
Ana  acuarela  avión  nube  zapato  árbol  ñandú     <- incorrecto
acuarela  Ana  árbol  avión  nube  ñandú  zapato     <- ICU es-PE
```

Las palabras con tilde y con ñ quedaban después de la Z. En un catálogo en español eso
arruina cualquier listado alfabético.

**La colación se fija al inicializar el clúster.** Cambiarla después obliga a volcar y
recargar toda la base, así que no se toca sin una migración planificada.

### Dos colaciones, dos necesidades

El módulo CORE crea dos colaciones no deterministas en su schema:

| Colación | Fuerza ICU | Ignora | Para qué |
|---|---|---|---|
| `core.es_ci` | level2 | mayúsculas | Identidad y unicidad: correos, claves |
| `core.es_search` | level1 | mayúsculas y tildes | Campos por los que el usuario busca |

Una sola no sirve para ambas cosas. En búsqueda hace falta que `lapiz` encuentre `LÁPIZ`,
porque nadie escribe tildes al buscar. En un correo con restricción de unicidad **no**
conviene: `josé@ejemplo.pe` y `jose@ejemplo.pe` son buzones distintos.

La ñ es letra propia del español, así que ninguna de las dos la iguala con la n.

## Reglas

1. Un script solo toca **su propio schema**.
2. Todos los scripts son **idempotentes**.
3. FK cruzada permitida solo en dirección de una dependencia **dura**.
4. Dependencia **blanda**: columna nullable, sin FK, con datos snapshot. La FK va en
   `integrations/` y **nunca** dentro de una migración: el otro schema puede no existir.
5. El `99_drop.sql` de un módulo ejecuta primero los `_drop.sql` de sus integraciones.

## Instalar un módulo

```
1. Aplicar sus migraciones     dotnet ef database update --context <Modulo>DbContext
2. Ejecutar su 02_seed.sql
3. Ejecutar las integraciones cuyos dos módulos estén instalados
```

En producción las migraciones **nunca** se aplican solas al arrancar: es un paso explícito
del despliegue.

## Orden de instalación

```
core → catalog → cms → crm → sales → services → b2b → tracking → portal → inventory
```

Luego las integraciones, solo de los pares en que ambos módulos estén instalados.
