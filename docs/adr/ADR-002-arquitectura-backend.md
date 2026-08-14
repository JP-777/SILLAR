# ADR-002 — Arquitectura del backend: modular monolith

- **Estado:** Aceptada
- **Fecha:** 2026-08-14
- **Decide:** JP

## Contexto

El objetivo es que cada sistema —ventas, servicios, seguimiento, portal, contenido— sea desmontable y licenciable por separado. Eso exige fronteras reales entre módulos, no solo carpetas ordenadas. Las opciones eran:

1. **Modular monolith** — un proyecto por módulo, una solución, un despliegue.
2. **Módulos como plugins cargables en tiempo de ejecución** — ensamblados descubiertos por el host.
3. **Microservicios** — un servicio y una base de datos por módulo.

## Decisión

Se adopta **modular monolith**: un proyecto .NET por módulo dentro de una sola solución, un solo despliegue, con contratos explícitos entre módulos y un `DbContext` por módulo apuntado a su propio schema.

## Razones

- Da fronteras verificables en tiempo de compilación: si un módulo intenta acceder al dominio de otro, no compila. Eso es más fuerte que la disciplina humana y más barato que la red.
- La operación sigue siendo simple: un despliegue, un log, una transacción cuando hace falta. Para el tamaño de los negocios objetivo, microservicios multiplicarían el costo de hosting y monitoreo sin beneficio real.
- La activación por licencia es trivial: el host registra solo los módulos habilitados y los endpoints de los demás sencillamente no existen.
- Los plugins cargables en runtime dan una sensación de modularidad mayor, pero añaden complejidad de versionado, carga de ensamblados y depuración que no se justifica mientras el producto y sus módulos se publiquen juntos.
- Si algún módulo llegara a necesitar escalado independiente, extraerlo a servicio es viable porque las fronteras y los contratos ya existen.

## Consecuencias

**Positivas.** Refactorización segura. Despliegue y hosting baratos. Transacciones locales cuando se necesitan. Camino abierto hacia servicios independientes sin rediseño.

**Negativas.** Todos los módulos comparten proceso, versión de .NET y ciclo de publicación: no se puede actualizar un módulo aislado sin volver a desplegar el conjunto. Un módulo mal escrito puede degradar el rendimiento de los demás. Hace falta vigilancia activa contra los atajos —referenciar el `Domain` ajeno "solo esta vez" es lo que erosiona la arquitectura.

## Mecanismo de control

Cada módulo implementa `IModule` declarando código, versión y dependencias duras y blandas. El host valida el grafo al arrancar: si una dependencia dura está inactiva, el arranque falla con un mensaje explícito en lugar de degradarse en silencio.

Regla de referencias: un módulo solo puede referenciar `Sillar.Shared`, `Sillar.Core.Contracts` y los `Contracts` de sus dependencias declaradas. Conviene que esta regla se verifique en el pipeline de compilación, no solo por revisión.
