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
