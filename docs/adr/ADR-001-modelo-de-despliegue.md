# ADR-001 — Modelo de despliegue: una instancia por cliente

- **Estado:** Aceptada
- **Fecha:** 2026-08-14
- **Decide:** JP

## Contexto

La plataforma dejará de ser un desarrollo a medida para un negocio concreto y pasará a ser un producto reutilizable en otros negocios, vendible por módulos, con licencia o como producto completo. Había que decidir cómo se sirve el sistema a cada cliente antes de escribir la primera tabla, porque la decisión condiciona el modelo de datos entero.

Opciones evaluadas:

1. **Una instancia por cliente** — cada negocio tiene su despliegue y su base de datos.
2. **Multi-tenant con un schema por cliente** — un despliegue, N clientes, tablas aisladas.
3. **Multi-tenant con `tenant_id` por fila** — un despliegue, tablas compartidas y filtradas.

## Decisión

Se adopta **una instancia por cliente**.

## Razones

- Encaja directamente con el modelo comercial previsto: licencia por instalación o venta del producto completo.
- Ninguna tabla necesita `tenant_id`, lo que mantiene el modelo de datos limpio y evita la clase de error más peligrosa del multi-tenant: una consulta sin filtro que expone datos de otro cliente.
- Las migraciones se aplican a una sola base de datos por cliente, sin coordinación entre inquilinos.
- Un cliente puede exigir su base de datos en su propia infraestructura, cosa habitual en negocios que ya tienen un sistema interno.
- El aislamiento de datos es total por construcción, no por disciplina de programación.

## Consecuencias

**Positivas.** Modelo de datos más simple. Aislamiento garantizado. Personalización por cliente sin afectar a los demás. El schema por módulo (ADR-003) queda libre de mezclarse con un schema por inquilino.

**Negativas.** El costo de hosting crece linealmente con el número de clientes. Actualizar a todos exige un proceso de despliegue repetible; se mitiga con contenedores y scripts de migración versionados. No hay economía de escala en infraestructura.

**Reversibilidad.** Migrar a multi-tenant por fila más adelante es caro: obliga a añadir `tenant_id` a todas las tablas y a revisar cada consulta. Migrar a multi-tenant por schema es considerablemente más barato, porque el aislamiento por schema ya forma parte del diseño. Si el modelo de negocio vira a SaaS puro, esa es la ruta de escape.

## Notas

La identidad del negocio instalado vive en `core.installation`. Esa tabla es el punto único que habría que extender si algún día se admite más de un negocio por instalación.
