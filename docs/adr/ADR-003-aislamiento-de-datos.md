# ADR-003 — Aislamiento de datos: un schema PostgreSQL por módulo

- **Estado:** Aceptada · **enmendada por ADR-009**
- **Fecha:** 2026-08-14
- **Decide:** JP

> **Enmienda (ADR-009):** las tablas ya no se crean con `01_schema.sql` escrito a mano, sino con
> migraciones de EF Core por módulo, cada una con su historial dentro de su propio schema.
> Todo lo demás sigue vigente: un schema por módulo, las reglas de claves foráneas duras y
> blandas, los scripts de integración y la anulación de referencias huérfanas.

## Contexto

El diseño previo (BD-01 a BD-04) produjo 17 tablas en un único schema `public` y un solo script de creación. Con el giro a producto modular, hacía falta decidir si la modularidad llega hasta la base de datos o se queda en el código.

## Decisión

Cada módulo posee **su propio schema PostgreSQL** y sus propios scripts de creación, semilla y desinstalación. Un módulo solo escribe en su schema.

## Reglas de claves foráneas

1. **Dependencia dura:** se permite FK entre schemas, declarada en el script del módulo dependiente y siempre en la dirección de la dependencia.
2. **Dependencia blanda:** prohibida la FK en el script base. La columna se crea nullable y se acompaña de datos snapshot suficientes para que el módulo funcione sin el otro. La FK se añade en un script de integración que solo se ejecuta si ambos módulos están activos.
3. Nunca una FK en dirección contraria a la dependencia declarada. Nunca ciclos.

Los scripts de integración viven en `database/integrations/<a>_<b>.sql`.

## Razones

- Desmontar un módulo se vuelve una operación real y limpia: no ejecutar su script, o ejecutar su `99_drop.sql`. Con un schema único, "desmontar" sería dejar tablas huérfanas.
- Permite entregar a un cliente solo lo que compró, sin tablas de módulos que no adquirió apareciendo en su base de datos.
- Hace visible el acoplamiento: una FK cruzada es un hecho declarado en el esquema, no un descubrimiento tardío.
- Mapea uno a uno con el `DbContext` por módulo del backend, mediante `HasDefaultSchema`.

## Consecuencias

**Positivas.** Desinstalación limpia. Acoplamiento explícito. Diccionario de datos y modelo ER naturalmente divididos por módulo, lo que además resuelve dos pendientes heredados: el diccionario BD-02 quedó incompleto y el ER BD-03 quedó desfasado respecto al script v2.

**Negativas.** Las consultas cruzadas exigen calificar el schema y son algo más verbosas. El orden de instalación importa y hay que respetarlo. Las dependencias blandas obligan a duplicar datos como snapshot, con el costo de consistencia eventual que eso implica —aceptado, porque en pedidos el snapshot histórico ya era deseable por sí mismo.

## Impacto sobre lo ya construido

Ninguna tabla se elimina. Se reparten así:

- `core` — `admin_users`, `site_settings`
- `catalog` — `categories`, `products`, `product_images`
- `cms` — `banners`, `promotions`, `featured_projects`, `social_links`
- `crm` — `customers`, `contact_messages`
- `sales` — `orders`, `order_items`, `order_statuses`
- `services` — `services`
- `b2b` — `special_order_leads`, `institution_requests`

Se conservan íntegras las decisiones previas: `timestamptz`, `GENERATED ALWAYS AS IDENTITY`, eliminación lógica con `is_active`, `CHECK` de validación, snapshots en `order_items` y `orders`, triggers de `updated_at` y la nomenclatura `snake_case` / `PascalCase` / `camelCase`.

## Pendiente derivado

El índice `idx_products_name` es B-tree y no sirve para búsquedas con `LIKE '%texto%'`. La búsqueda precisa exigida por el PRD requiere `pg_trgm` y `unaccent`, y se resuelve dentro del módulo M01 Catálogo, no como tarea global.
