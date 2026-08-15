# ADR-013 — SILLAR es una plataforma; los productos son ediciones

- **Estado:** ~~Aceptada~~ · **SUSTITUIDA POR ADR-015**
- **Fecha:** 14 de agosto de 2026
- **Decide:** JP

> **Por qué se sustituye.** Esta decisión partía de que ambos productos serían aplicaciones
> web sobre un servidor. El ERP resultó ser una aplicación de **escritorio que funciona sin
> internet**, con base de datos interna y sucursales autónomas: nada de eso puede compartir
> instalación con una web en la nube. La conclusión —«no son dos productos, son dos
> ediciones»— es falsa. El análisis de que catálogo, existencias y clientes son los mismos
> datos sí se mantiene, y se resuelve con sincronización en la ADR-015.
>
> Se conserva sin editar, porque una decisión corregida a escondidas es peor que no tenerla.

## Contexto

El primer cliente encargó un segundo sistema: un punto de venta para el mostrador, en reemplazo de Bsale. Bsale funciona, pero es genérico, y el flujo de venta real exige pasos y rodeos que cuestan tiempo en cada operación.

La propuesta inicial fue separar el trabajo en dos productos de una misma familia: uno web —marketing, administración y venta en línea— y otro operativo, para gestionar el negocio.

## La objeción

Un punto de venta y una tienda en línea del mismo negocio **comparten catálogo, existencias y clientes**. Son los mismos productos, el mismo inventario y las mismas personas.

Dos productos con dos bases de datos reconstruyen exactamente el problema que el cliente sufre hoy: un sistema para el mostrador y otro para lo demás, sin hablarse, con inventarios que divergen a los tres días.

Y hay un argumento más fuerte: **la modularidad ya da lo que se buscaba con dos productos.** Vender solo el punto de venta o solo la web no exige dos productos — exige activar módulos distintos, que es precisamente lo que el sistema de licencias hace desde la entrega 3 de CORE.

## Decisión

**SILLAR es una plataforma. Lo que se vende son ediciones: conjuntos de módulos sobre el mismo núcleo, la misma instalación y la misma base de datos.**

Un punto de venta es, arquitectónicamente, **otra aplicación cliente contra la misma API**. No otro producto.

### Las dos ediciones

| Edición | Módulos | Para qué |
|---|---|---|
| **Comercial** *(nombre pendiente)* | CORE · M01 · M02 · M03 · M04 · M05a · M07 | Presencia, catálogo público, venta en línea, captación |
| **Operativa** *(nombre pendiente)* | CORE · M01 · M04 · M09 · M13 · M14 · M15 | Mostrador, existencias, compras, comprobantes |

**CORE, M01 Catálogo y M04 Clientes están en ambas.** Ahí está el valor: quien tenga las dos vende en mostrador y en línea contra el mismo inventario, sin sincronizar nada, porque no hay nada que sincronizar.

### Módulos nuevos

| ID | Módulo | Schema | Depende de |
|---|---|---|---|
| **M13** | Punto de Venta — incluye caja y turnos | `pos` | M01 (dura) · M04, M09, M14 (blandas) |
| **M14** | Comprobantes Electrónicos | `billing` | CORE (dura) |
| **M15** | Compras y Proveedores | `purchasing` | M01, M09 (duras) |

**M09 Inventario deja de ser fase 4.** Pasa a ser pieza central de la edición operativa: sin existencias fiables no hay punto de venta que valga.

**Caja no es un módulo aparte.** Apertura, cierre y arqueo viven dentro de M13. Separarlos sería modularizar por gusto: no existe un negocio que quiera control de caja sin punto de venta. Se aplica la regla de siempre — no hay abstracción sin segundo caso.

**M13 depende de M14 de forma blanda, no dura.** Si Comprobantes no está instalado, el punto de venta registra la venta sin documento fiscal. Es lo que permite venderlo fuera de Perú, donde el régimen de SUNAT no aplica, sin tocar una línea del punto de venta.

## Consecuencias

**Positivas.** Ningún renombrado y ningún código tocado: la decisión se expresa entera en documentación y en qué módulos se activan. Catálogo y existencias únicos por construcción, que es el problema que el cliente está pagando por resolver. Y una demostración más fuerte: el mismo sistema encendiendo bloques distintos según el negocio.

**Negativas.** Las dos ediciones comparten ciclo de versiones: no se puede publicar el punto de venta sin arrastrar el estado del resto. Y la instalación crece — más módulos activos, más superficie que mantener viva a la vez.

**Riesgo a vigilar.** La tentación de meter en M01 Catálogo cosas que solo el punto de venta necesita —códigos de barra, unidades de venta, precios por mayor— porque «total, es el mismo catálogo». Si es del catálogo, va en M01 y sirve a los dos. Si solo lo usa el mostrador, va en M13. La pregunta que decide: *¿tendría sentido en un negocio que solo tiene la web?*

## Sobre el nombre «ERP»

Se descarta. Un ERP incluye contabilidad, planillas, activos y producción. Lo que se va a construir es punto de venta, existencias, compras y comprobantes. Llamarlo ERP crea una expectativa que tardaría años en cumplirse y empuja a construir módulos que nadie pidió por hacer honor al nombre.

Los nombres comerciales de las ediciones quedan pendientes. No bloquean nada: son etiquetas de venta, no identificadores de código.
