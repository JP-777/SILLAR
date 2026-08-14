# ADR-009 — Migraciones EF Core por módulo

- **Estado:** Aceptada
- **Fecha:** 2026-08-14
- **Decide:** JP
- **Enmienda:** ADR-003

## Contexto

La ADR-003 estableció un schema PostgreSQL por módulo con scripts `01_schema.sql` escritos a mano. Al llegar al momento de construir el backend aparece la pregunta que quedó abierta: si el esquema se escribe a mano y además hay modelos de Entity Framework Core, **¿cuál de los dos manda?**

Mantener ambos sincronizados a mano es una fuente permanente de errores: alguien añade una columna al modelo de C#, olvida el script, y el fallo aparece en tiempo de ejecución contra una base real.

## Decisión

**Las migraciones de EF Core son la fuente de verdad del esquema.** Cada módulo tiene su propio `DbContext`, sus propias migraciones y su propia tabla de historial dentro de su schema.

```csharp
modelBuilder.HasDefaultSchema("catalog");
// historial de migraciones del módulo, dentro de su schema
options.UseNpgsql(cs, o => o.MigrationsHistoryTable("__migrations", "catalog"));
```

Esto encaja con el modelo modular mejor de lo que parece a primera vista: cada módulo lleva su propio historial en su propio schema, así que **instalar un módulo es aplicar sus migraciones y desinstalarlo es soltar su schema**, incluido su historial. No queda rastro.

## Qué sigue escribiéndose a mano

Las migraciones no cubren todo. Se mantienen como SQL escrito a mano:

| Qué | Dónde | Por qué |
|---|---|---|
| Scripts de integración entre módulos | `database/integrations/<a>_<b>.sql` | Son claves foráneas condicionales entre dos schemas que pueden o no coexistir. EF Core no modela eso. |
| Datos semilla | `database/modules/<código>/02_seed.sql` | Idempotentes, legibles y revisables. El sembrado de EF Core es más opaco y difícil de auditar. |
| Extensiones de PostgreSQL | Migración con `MigrationBuilder.Sql(...)` | `pg_trgm` y `unaccent` para la búsqueda del catálogo. |
| Índices especializados | Migración con `MigrationBuilder.Sql(...)` | Índices GIN de trigramas y similares que el generador no produce. |

## Aplicación de migraciones

- **En desarrollo:** el host puede aplicarlas al arrancar, controlado por una bandera de configuración.
- **En producción: nunca automáticamente.** Se aplican con un comando explícito durante el despliegue. Una migración que se ejecuta sola en el arranque es un incidente esperando ocurrir, sobre todo con varias instalaciones en distintas versiones.

## Consecuencias

**Positivas.** Modelos y esquema no pueden desincronizarse. El flujo es el estándar de .NET, así que cualquier desarrollador que se sume lo reconoce. Instalar y desinstalar módulos queda bien definido. El historial por schema evita que los módulos se pisen.

**Negativas.** Menos control directo sobre el SQL generado; hay que revisar cada migración antes de darla por buena. Las claves foráneas entre schemas exigen configuración explícita en el `DbContext`, porque EF no las descubre solo. Y hay que vigilar que nadie meta en una migración una referencia a un schema que quizá no exista: las dependencias blandas siguen prohibidas dentro de las migraciones y solo viven en los scripts de integración.

## Efecto sobre la ADR-003

Todo lo demás de la ADR-003 sigue vigente: un schema por módulo, las reglas de claves foráneas duras y blandas, los scripts de integración con su contraparte de desinstalación y la anulación de referencias huérfanas.

Lo único que cambia es **quién crea las tablas**: ya no `01_schema.sql`, sino las migraciones del módulo. La carpeta `database/modules/<código>/` conserva el seed y el script de desinstalación.
