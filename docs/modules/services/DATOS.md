# DATOS — M05a Servicios — Vitrina

- **Creación / última modificación / última verificación:** 28/09/2026 · America/Lima
- **Base verificada:** `e839989432283c755edf7d4ae47b2c37697215ec`
- **Estado:** implementado para revisión previa a paso 3.5

## Modelo

`services.service_entries` es la única tabla de negocio. M05a no replica: PK `integer GENERATED ALWAYS AS IDENTITY`, sin `origin_node` ni `row_version`.

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| id | integer identity always | no | PK técnica, nunca visible |
| name | text `core.es_search` | no | no vacío |
| slug | text `core.es_ci` | no | único y formato URL |
| short_description | text | sí | una descripción breve o completa es obligatoria |
| description | text | sí | una descripción breve o completa es obligatoria |
| price | numeric(12,2) | sí | nulo = consultar; cero = gratuito; nunca negativo |
| sale_unit | text | sí | texto libre |
| image_id | uuid | sí | FK dura a `core.media_assets`, `ON DELETE SET NULL` |
| image_alt_text | text | sí | obligatorio cuando hay imagen |
| publication_state | text | no | `draft`, `published`, `archived` |
| display_order | integer | no | no negativo |
| created_at / updated_at | timestamptz | no | `updated_at` mediante trigger propio |

No existe tabla de opciones: continúa pendiente de producto. No existe FK hacia M01, M05b ni M06.

## Ciclo

La migración crea exclusivamente `services`. `02_seed.sql` no introduce datos comerciales. `99_drop.sql` elimina exclusivamente `services`; nunca nombra `service_orders`.

## Contrato fotográfico

El snapshot incluye identidad de servicio, textos, precio, unidad, `MediaAssetId`, URL pública actual y texto alternativo. M05b deberá copiar esos valores. La URL no garantiza que el binario sobreviva a una baja de CORE: ver `CONTRATO-FOTOGRAFIA.md` y `ESCALADAS.md`.
