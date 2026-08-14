# ADR-004 — Licenciamiento por módulo y descubrimiento de capacidades

- **Estado:** Aceptada
- **Fecha:** 2026-08-14
- **Decide:** JP

## Contexto

El producto se venderá por módulos, con precio por módulo, y eventualmente con licencia o como producto completo. El sistema necesita saber qué módulos están habilitados y comportarse en consecuencia, sin que esa lógica se disperse por el código.

## Decisión

La activación de módulos vive en el schema `core` y se expone al frontend mediante un único endpoint de capacidades.

**Tablas en `core`:**

- `installation` — identidad del negocio instalado, clave de instalación, datos de licencia.
- `modules` — catálogo de módulos que el producto conoce: código, nombre, versión, dependencias duras y blandas.
- `module_activations` — qué está activo en esta instalación, vigencia y límites.

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

Se implementan las tablas, la validación del grafo, el registro condicional de módulos y el endpoint de capacidades.

Se difiere a la fase de comercialización: la firma criptográfica del archivo de licencia, el control de vencimientos y los límites de uso. El esquema de datos ya los contempla para no tener que migrar después.
