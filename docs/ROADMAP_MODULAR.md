# SILLAR — Roadmap Modular

**Versión:** 2.0 — reemplaza al roadmap BD → Backend → Frontend
**Fecha:** 14 de agosto de 2026

---

## Cómo cambia la forma de trabajar

El roadmap anterior avanzaba por capas: primero toda la base de datos, luego todo el backend, luego todo el frontend. Con la arquitectura modular eso deja de tener sentido, porque un módulo no está terminado hasta que atraviesa las tres capas y se puede activar y desactivar.

El nuevo roadmap tiene **una fase de fundación** y luego **un ciclo idéntico por módulo**.

**Entregas dentro de un módulo.** Un módulo grande se parte en entregas sucesivas, cada una
con su documento en `docs/modules/<código>/ENTREGA-NN-<nombre>.md` que refina el SPEC.
CORE va así: entrega 1, esqueleto y capacidades; entrega 2, instalación y autenticación.

### El ciclo de módulo (5 pasos)

Cada módulo recorre siempre los mismos cinco pasos, en orden:

| Paso | Nombre | Entregable | Dónde se hace |
|---|---|---|---|
| **1** | SPEC | Especificación: propósito, dependencias, tablas, contrato, endpoints, eventos, criterios de aceptación | **Este chat** |
| **2** | DATOS | Diccionario del módulo, ER del módulo, migraciones EF Core, `02_seed.sql`, `99_drop.sql` | Se diseña aquí, se implementa en **Claude Code** |
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
| **F-01** | Repositorio, estructura de carpetas, `.gitattributes`, `.gitignore` | ✅ Completado |
| **F-02** | Entorno Docker Compose con PostgreSQL 16 funcionando en Windows | ✅ Completado |
| **F-03** | ADRs y arquitectura modular documentada | ✅ Completado |
| **F-04** | Material previo archivado **fuera del repositorio** (PRD, diccionario, ER, scripts, prototipo) | ✅ Completado |
| **F-05** | `CLAUDE.md` y plantilla de especificación de módulo | ✅ Completado |
| **F-06** | Decisión del nombre del producto: **SILLAR** (ADR-007) | ✅ Completado |
| **F-07** | Solución .NET base: `Api`, `Shared`, `Core`, contrato `IModule`, orquestador de módulos | ✅ Completado |
| **F-08** | Proyecto React base: `shared`, `layout`, `capabilities`, composición de rutas | ✅ Completado |

F-06 quedó resuelto antes de escribir código, que era justamente el objetivo: renombrar ahora costó una sustitución de texto.

---

## Fase 1 — MVP comercial · primera instalación

Módulos que entran en la primera entrega, en orden de construcción. El orden respeta las dependencias y prioriza lo que el PRD marca como núcleo: banners, productos y carrito.

| Orden | Módulo | Por qué va aquí |
|---|---|---|
| 1 | **CORE** ✅ | Todo lo demás se enchufa aquí. Incluye licencias, activación, settings y usuarios admin. **Cerrado** — commit `73988ce`, 181 pruebas |
| 2 | **M01 Catálogo** ← *en curso* | Base del negocio. Incluye la búsqueda con `pg_trgm` y `unaccent` que exige el PRD. **SPEC v1.1 en revisión** |
| 3 | **M02 Contenido Web** | Banners: prioridad número uno declarada por la cliente. Es independiente, se puede construir en paralelo. |
| 4 | **M04 Clientes** | Necesario para que Ventas tenga a quién asociar el pedido. |
| 5 | **M03 Ventas Online** | Carrito y pedidos: prioridad número tres. Requiere M01 y aprovecha M04. |
| 6 | **M05a Servicios (vitrina)** | El PRD insiste en que los servicios permanentes no queden escondidos. |
| 7 | **M07 Solicitudes B2B** | Colegios, empresas y pedidos especiales: parte del valor diferencial del negocio. |

Al cerrar la Fase 1, la primera instalación tiene su web completa y el producto cuenta con siete módulos vendibles.

### CORE por entregas

CORE es demasiado grande para un solo ciclo de cinco pasos, así que se parte en entregas con su propio documento bajo `docs/modules/core/`.

| Entrega | Alcance | Estado |
|---|---|---|
| **01** | Esqueleto del módulo, `IModule`, sincronización de `core.modules`, `/api/capabilities` | ✅ Cerrada |
| **02** | Instalación, login, sesiones por cookie, CSRF, cambio de contraseña, CRUD de usuarios | ✅ Cerrada — commit `de4994a`, 95 pruebas |
| **02.1** | Token CSRF determinista derivado de la sesión (ADR-012) | ✅ Cerrada — commit `4c37cdc` |
| **03** | Activación de módulos, `site_settings`, auditoría consultable | ✅ Cerrada — commit `4fe76b4`, 152 pruebas |
| **03b** | Gestión de medios | ✅ Cerrada — commit `dd14431` |
| **04a** | Pantallas de módulos y usuarios | ✅ Cerrada — commit `005e8fb` |
| **04b** | Configuración, auditoría y medios en el panel | ✅ Cerrada — commit `73988ce`, 181 pruebas |

**CORE está cerrado.** Siete entregas, 9 tablas, 20 rutas, 181 pruebas y 6 entradas de menú filtradas por rol. Lo único pendiente es la **verificación visual del panel**, que Claude Code no puede hacer porque no ve la interfaz — la lista está en la §6 de la bitácora.

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
| 11 | **M10 Reportes** | Analítica sobre eventos publicados por los demás módulos. |
| 12 | **M11 Pagos** | Pasarela de pago en línea. |
| 13 | **M12 Asistente** | Chatbot con IA. Requiere definir alcance con precisión antes de estimar. |

**M09 Inventario sale de esta fase** y pasa a SILLAR ERP: sin existencias fiables no hay punto de venta que valga.

---

## SILLAR ERP — aparcado hasta cerrar lo que está en curso

Producto aparte sobre el mismo código (ADR-015, enmendada por la ADR-017). **No se empieza hasta que M01 esté cerrado**, y antes de escribir su primera línea hay dos cosas que hacer:

| Antes de M13 | Por qué |
|---|---|
| **Observar el mostrador** — `GUIA-OBSERVACION-MOSTRADOR.md` | Un flujo de trabajo no se puede entrevistar, hay que verlo. Sin esas mediciones no se especifica nada. Se puede hacer ya: no consume tiempo de desarrollo |
| **Datos administrativos de Bsale** | Certificado, costo, volumen, y sobre todo **series y correlativos en uso**: al migrar, la numeración no puede reiniciarse ni saltar. Preguntas 7 a 10 de la guía |

| Orden | Módulo | Notas |
|---|---|---|
| 1 | **M09 Inventario** | Cuenta contra `catalog.product_items`. Antes hay que decidir dónde vive el concepto de ubicación |
| 2 | **M13 Punto de Venta** | El dolor diario y medible. Incluye caja y turnos |
| 3 | **M14 Comprobantes** | Se enciende cuando el negocio lo necesita, no antes |
| 4 | **M15 Compras** | |
| 5 | **M17 Sucursales** | Solo cuando haya un segundo local real |
| 6 | **M16 Sincronización** | El último: su tamaño depende de si las sucursales son nodos autónomos o terminales, y eso sigue abierto (ADR-017 §Lo que queda abierto) |

**Bloqueo transversal:** la ADR-016 (claves `uuid` v7 en tablas replicadas) hay que aplicarla **antes** de la primera migración de M01, porque sus tablas se replican.

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
| BD-04 Script SQL | Se convierte en las migraciones EF Core de cada módulo |
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
