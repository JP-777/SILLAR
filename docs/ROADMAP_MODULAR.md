# SILLAR — Roadmap Modular

**Versión:** 2.0 — reemplaza al roadmap BD → Backend → Frontend
**Fecha:** 14 de agosto de 2026

---

## Cómo cambia la forma de trabajar

El roadmap anterior avanzaba por capas: primero toda la base de datos, luego todo el backend, luego todo el frontend. Con la arquitectura modular eso deja de tener sentido, porque un módulo no está terminado hasta que atraviesa las tres capas y se puede activar y desactivar.

El nuevo roadmap tiene **una fase de fundación** y luego **un ciclo idéntico por módulo**.

### El ciclo de módulo (5 pasos)

Cada módulo recorre siempre los mismos cinco pasos, en orden:

| Paso | Nombre | Entregable | Dónde se hace |
|---|---|---|---|
| **1** | SPEC | Especificación: propósito, dependencias, tablas, contrato, endpoints, eventos, criterios de aceptación | **Este chat** |
| **2** | DATOS | Diccionario del módulo, ER del módulo, `01_schema.sql`, `02_seed.sql`, `99_drop.sql` | Se diseña aquí, se implementa en **Claude Code** |
| **3** | API | Proyecto del módulo, `DbContext`, modelos, DTOs, endpoints, validaciones, Swagger | **Claude Code** |
| **4** | UI | Componentes, páginas, rutas, servicios HTTP, integración con capacidades | **Claude Code** |
| **5** | CIERRE | Documentación del módulo, prueba de montaje y desmontaje, entrega | **Este chat** |

**Criterio de cierre innegociable, igual para todos los módulos:** el módulo se instala y se desinstala sin romper nada del resto del sistema. Si al desactivarlo aparece un enlace roto, una ruta muerta, un hueco visual o un error en el arranque, el módulo no está terminado.

Se mantiene la regla anterior: **no se pasa al siguiente módulo hasta que JP confirme el cierre.**

---

## Fase 0 — Fundación

Es la única fase que no es un módulo. Sin ella no se puede empezar.

| ID | Tarea | Estado |
|---|---|---|
| **F-01** | Repositorio, estructura de carpetas, `.gitattributes`, `.gitignore` | Pendiente |
| **F-02** | Entorno Docker Compose con PostgreSQL 16 funcionando en Windows | Pendiente |
| **F-03** | ADRs y arquitectura modular documentada | ✅ Completado |
| **F-04** | Material previo archivado **fuera del repositorio** (PRD, diccionario, ER, scripts, prototipo) | ✅ Completado |
| **F-05** | `CLAUDE.md` y plantilla de especificación de módulo | ✅ Completado |
| **F-06** | Decisión del nombre del producto: **SILLAR** (ADR-007) | ✅ Completado |
| **F-07** | Solución .NET base: `Api`, `Shared`, `Core`, contrato `IModule`, orquestador de módulos | Pendiente |
| **F-08** | Proyecto React base: `shared`, `layout`, `capabilities`, composición de rutas | Pendiente |

F-06 quedó resuelto antes de escribir código, que era justamente el objetivo: renombrar ahora costó una sustitución de texto.

---

## Fase 1 — MVP comercial · primera instalación

Módulos que entran en la primera entrega, en orden de construcción. El orden respeta las dependencias y prioriza lo que el PRD marca como núcleo: banners, productos y carrito.

| Orden | Módulo | Por qué va aquí |
|---|---|---|
| 1 | **CORE** | Todo lo demás se enchufa aquí. Incluye licencias, activación, settings y usuarios admin. |
| 2 | **M01 Catálogo** | Base del negocio. Incluye la búsqueda con `pg_trgm` y `unaccent` que exige el PRD. |
| 3 | **M02 Contenido Web** | Banners: prioridad número uno declarada por la cliente. Es independiente, se puede construir en paralelo. |
| 4 | **M04 Clientes** | Necesario para que Ventas tenga a quién asociar el pedido. |
| 5 | **M03 Ventas Online** | Carrito y pedidos: prioridad número tres. Requiere M01 y aprovecha M04. |
| 6 | **M05a Servicios (vitrina)** | El PRD insiste en que los servicios permanentes no queden escondidos. |
| 7 | **M07 Solicitudes B2B** | Colegios, empresas y pedidos especiales: parte del valor diferencial del negocio. |

Al cerrar la Fase 1, la primera instalación tiene su web completa y el producto cuenta con siete módulos vendibles.

---

## Fase 2 — Operación de servicios

| Orden | Módulo | Notas |
|---|---|---|
| 8 | **M05b Servicios (órdenes)** | Órdenes de servicio reales. **No existe en el diseño previo, se construye desde cero.** |
| 9 | **M06 Seguimiento** | Historial de estados y tablero kanban. Es la trazabilidad que pidió la cliente en la entrevista. |

Estos dos módulos son, comercialmente, el diferenciador más fuerte del producto: casi ningún sistema de este segmento ofrece seguimiento de trabajos personalizados.

---

## Fase 3 — Portal del cliente

| Orden | Módulo | Notas |
|---|---|---|
| 10 | **M08 Portal del Cliente** | Cuentas, historial y consulta de estados. Muestra pedidos si M03 está activo y trabajos si M06 está activo. |

Introduce autenticación de clientes finales, distinta de la de administradores. Es el punto donde la seguridad deja de ser un asunto interno.

---

## Fase 4 — Integración y análisis

| Orden | Módulo | Notas |
|---|---|---|
| 11 | **M09 Inventario** | Movimientos de stock y sincronización con el sistema del negocio. Depende del acceso técnico al sistema actual del cliente. |
| 12 | **M10 Reportes** | Analítica sobre eventos publicados por los demás módulos. |
| 13 | **M11 Pagos** | Pasarela de pago en línea. |
| 14 | **M12 Asistente** | Chatbot con IA. Requiere definir alcance con precisión antes de estimar. |

---

## Fase 5 — Comercialización

Trabajo de producto, no de cliente.

- Firma criptográfica del archivo de licencia y control de vencimientos.
- Instalador o script de aprovisionamiento de una instancia nueva.
- Documentación de venta: qué hace cada módulo, qué requiere y cuánto cuesta.
- Entorno de demostración con módulos activables para mostrar en vivo.
- Definición del modelo comercial: licencia perpetua, suscripción o producto completo.

---

## Equivalencia con el roadmap anterior

Nada del trabajo previo se pierde. Se reubica:

| Módulo anterior | Dónde vive ahora |
|---|---|
| BD-01 Análisis de entidades | Absorbido en el catálogo de módulos de `ARQUITECTURA_MODULAR.md` |
| BD-02 Diccionario de datos | Paso 2 del ciclo de cada módulo — y así se completa lo que quedó a medias |
| BD-03 Modelo ER | Paso 2 del ciclo de cada módulo, más el mapa de dependencias |
| BD-04 Script SQL | Se parte en `01_schema.sql` por módulo |
| BD-05 Datos semilla | Se parte en `02_seed.sql` por módulo |
| BD-06 Optimización | Deja de ser global: la búsqueda entra en M01, los índices en cada módulo |
| BE-01 a BE-04 | Fundación F-07 |
| BE-05 Productos y categorías | Paso 3 de M01 |
| BE-06 Pedidos | Paso 3 de M03 |
| BE-07 Clientes | Paso 3 de M04 |
| BE-08 Documentación Swagger | Parte del paso 3 de todos los módulos |
| BE-09 Validaciones y errores | Fundación F-07, en `Sillar.Shared` |
| BE-10 Seguridad admin | Módulo CORE |
| BE-11 Deploy | Fase 5 |
| FE-01, FE-02 | Fundación F-08 |
| FE-03 a FE-08 | Paso 4 de sus módulos respectivos |
| FE-09 Panel admin | Se reparte: cada módulo trae su propia administración |
| FE-10 Responsive y UX | Criterio transversal del paso 4 de cada módulo |

Una consecuencia que conviene notar: **el panel administrativo deja de ser un módulo aparte**. Cada módulo trae su propia administración, porque si el panel fuera único, desmontar un módulo dejaría un menú roto en el panel.

---

## Reparto de trabajo

**En este chat:** especificaciones de módulo, decisiones de arquitectura y de producto, diccionario y modelo ER, documentación para el cliente, revisión de entregables, ADRs.

**En Claude Code:** creación de proyectos, scripts SQL, código de backend y frontend, migraciones, pruebas, refactorizaciones, depuración.

El artefacto puente es `CLAUDE.md` en la raíz del repositorio, más el `SPEC.md` del módulo en curso. Claude Code no necesita conocer la conversación: necesita el SPEC.
