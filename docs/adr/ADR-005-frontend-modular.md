# ADR-005 — Frontend modular en React

- **Estado:** Aceptada
- **Fecha:** 2026-08-14
- **Decide:** JP

## Contexto

El prototipo actual está organizado por tipo de archivo (`components/`, `pages/`, `data/`, `styles/`). Esa estructura funciona para una aplicación única, pero impide desmontar funcionalidad: el catálogo, el carrito y el contacto acaban entrelazados en los mismos archivos.

## Decisión

El frontend se organiza **por módulo**, en espejo con el backend, y compone sus rutas dinámicamente a partir de las capacidades activas.

```
src/
├── shared/          UI base, hooks genéricos, cliente HTTP, tipos comunes
├── layout/          header, footer, navegación, contenedores
├── modules/<código>/  { components, pages, services, types, routes.ts }
├── capabilities/    consume /api/capabilities, expone useCapability()
└── app/             composición de rutas y arranque
```

## Reglas

1. Un módulo **nunca importa** de otro módulo. Todo lo compartido vive en `shared/`. Si dos módulos necesitan lo mismo, sube a `shared/`.
2. Cada módulo exporta su `routes.ts`. La aplicación monta solo las rutas de módulos activos.
3. Menú, secciones de la home y enlaces del footer se construyen desde las capacidades. Nada escrito a mano.
4. Un módulo desactivado no deja rutas muertas, enlaces rotos ni huecos visuales.
5. Cada módulo tiene su propia capa de servicios HTTP. Nada de `fetch` suelto dentro de un componente.

## Razones

- Alinear frontend y backend por módulo hace obvio qué se entrega y qué se cobra.
- Facilita el trabajo con Claude Code: un módulo es una unidad de contexto acotada, con su especificación y sus fronteras.
- Evita el problema clásico de la organización por tipo de archivo: para quitar una funcionalidad hay que tocar diez carpetas.

## Consecuencias

**Positivas.** Desmontar un módulo es borrar su carpeta y su registro de rutas. El equipo puede trabajar módulo por módulo sin pisarse.

**Negativas.** Hay que decidir con criterio qué sube a `shared/`; si sube demasiado, `shared/` se convierte en un vertedero y vuelve el acoplamiento por la puerta trasera. Regla de contención: algo sube a `shared/` cuando lo usa un segundo módulo, no antes.

## Impacto sobre el prototipo actual

El prototipo migrado desde Stitch mantiene su valor como **referencia de experiencia de usuario y línea visual**, pero su código no se reutiliza tal cual. Se reconstruye por módulos consumiendo la API. Esto ya estaba previsto en el roadmap original, donde el prototipo quedó explícitamente congelado como referencia.
