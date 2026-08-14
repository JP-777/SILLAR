# Scripts de base de datos

La base de datos se instala **por módulo**, no de una sola vez. Qué módulos se instalan
depende de lo que el cliente tenga licenciado.

## Estructura

```
modules/<codigo>/01_schema.sql   crea el schema y sus tablas
modules/<codigo>/02_seed.sql     datos mínimos para que el módulo funcione
modules/<codigo>/99_drop.sql     desinstala el módulo
integrations/<a>_<b>.sql         FK opcional entre dos módulos con dependencia blanda
integrations/<a>_<b>_drop.sql    elimina esa FK y anula referencias huérfanas
```

## Reglas

1. Un script solo toca **su propio schema**.
2. Todos los scripts son **idempotentes**.
3. FK cruzada permitida solo en dirección de una dependencia **dura**.
4. Dependencia **blanda**: columna nullable, sin FK, con datos snapshot. La FK va en `integrations/`.
5. El `99_drop.sql` de un módulo ejecuta primero los `_drop.sql` de sus integraciones.

## Orden de instalación

```
core → catalog → cms → crm → sales → services → b2b → tracking → portal → inventory
```

Luego las integraciones, solo de los pares en que ambos módulos estén instalados.
