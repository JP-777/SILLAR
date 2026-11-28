# ADR-004 — Licenciamiento por módulo y descubrimiento de capacidades

- **Estado:** Aceptada
- **Fecha de creación:** 2026-08-14
- **Fecha de última verificación:** 2026-11-27, America/Lima
- **Commit contrastado:** `6ea2d74fd4471d2ded81d3e104d740ac4ad19863` más cambios locales de separación de licencias; ver BITACORA.
- **Decide:** JP

## Contexto

El producto se venderá por módulos, con precio por módulo, y eventualmente con licencia o como producto completo. El sistema necesita saber qué módulos están habilitados y comportarse en consecuencia, sin que esa lógica se disperse por el código.

## Decisión

La activación de módulos vive en el schema `core` y se expone al frontend mediante un único endpoint de capacidades.

**Tablas en `core`:**

- `installation` — identidad del negocio instalado, clave de instalación, datos de licencia.
- `modules` — catálogo de módulos que el producto conoce: código, nombre, versión, dependencias duras y blandas.
- `module_licenses` — derecho local previo a la activación, provisionado desde el despliegue; el panel no lo concede.
- `module_activations` — elección funcional de qué está encendido. Solo surte efecto si existe licencia.
- `module_operation_permissions` — restricciones locales por cuenta y operación, bajo el techo de los roles.

**Comportamiento del backend:**

- Al arrancar, el host valida el grafo de dependencias. Si un módulo activo tiene una dependencia dura inactiva, el arranque **falla con un mensaje explícito**. No se degrada en silencio.
- Solo se registran los servicios y endpoints de módulos activos. Un módulo no licenciado no expone rutas: no responde 403, sencillamente no existe.

**Contrato con el frontend:**

- `GET /api/capabilities` es público y devuelve únicamente códigos y versiones de módulos activos.
- Nunca expone fechas de licencia, límites ni datos comerciales.
- El frontend arma menú, secciones de la home, rutas y enlaces del footer a partir de esa respuesta.

## Razones

- Concentra en un solo lugar la pregunta "¿qué está habilitado?", en vez de repartirla en condicionales por todo el código.
- Un módulo apagado desaparece de la interfaz sin dejar rutas muertas ni huecos visuales.
- Separa limpiamente lo comercial (licencia, vigencia, precio) de lo funcional (qué puede hacer la aplicación).
- Permite demostrar el producto activando y desactivando módulos, que es exactamente el argumento de venta.

## Consecuencias

**Positivas.** La modularidad se vuelve visible y demostrable. Añadir un módulo nuevo no obliga a tocar el frontend existente.

**Negativas.** El frontend depende de una llamada temprana a capacidades: hay que manejar su estado de carga y su fallo sin dejar la aplicación en blanco. Existe el riesgo de que alguien manipule la respuesta en el cliente para "ver" un módulo; por eso la autorización real vive siempre en el backend y las capacidades son solo una guía de presentación.

## Alcance actual

La integración de cierre WEB separa las tres decisiones: derecho de uso, activación y permiso de operación. La provisión manual usa `Sillar:License:Modules` y se registra atómicamente; una lista vacía no concede derechos. Su contrato detallado está en SPEC CORE §4.4a. Esta actualización aplica el mandato de JP del 26/11 y no elige condiciones comerciales. La acreditación de la integración se registra en BITACORA.

Se difiere a la fase de comercialización: la firma criptográfica del archivo de licencia, el control de vencimientos y los límites de uso. El esquema de datos ya los contempla para no tener que migrar después.
