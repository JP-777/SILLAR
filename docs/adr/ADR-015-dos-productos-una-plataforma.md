# ADR-015 — Dos productos sobre una plataforma

- **Estado:** Aceptada
- **Fecha:** 14 de agosto de 2026
- **Decide:** JP
- **Sustituye a:** ADR-013
- **Enmendada por:** ADR-017 — la fila «Internet» de la tabla comparativa y la sección «Sincronización — M16». Internet resultó ser **casi obligatorio**; el trabajo sin conexión es un modo degradado, no el modo normal. El resto de esta decisión sigue vigente.

## Por qué se sustituye la ADR-013

La ADR-013 concluyó que no había dos productos sino dos ediciones sobre una misma instalación y una misma base de datos. **La premisa era falsa:** se dio por hecho que ambos serían aplicaciones web sobre un servidor.

El ERP es una **aplicación de escritorio que funciona sin internet**, con base de datos interna del negocio, varias máquinas en red local y sucursales autónomas. Nada de eso puede compartir instalación con una web en la nube.

Lo que sí sobrevive del análisis anterior: catálogo, existencias y clientes son los mismos datos cuando un negocio tiene ambos. La diferencia es que **la sincronización deja de ser un error de diseño evitable y pasa a ser inherente al requisito**.

## Decisión

**SILLAR es una plataforma con un solo código fuente. Sobre ella se publican dos productos con topologías de despliegue distintas.**

| | **SILLAR WEB** | **SILLAR ERP** |
|---|---|---|
| Qué es | Servicio web multiservicio para vender productos y prestar servicios | Sistema de gestión del negocio |
| Dónde vive | Servidor en la nube, una instancia por cliente | Máquina del propio negocio |
| Internet | Necesario | **No necesario para operar** |
| Base de datos | En la nube | Interna del negocio |
| Clientes | Navegador, público e interno | Envoltura de escritorio y navegadores de la red local |
| Se vende | Suscripción | Licencia o suscripción |
| Módulos | M01–M08, M10–M12 | CORE, M01, M04, M09, M13, M14, M15, M16 |

**Comparten el código, no la instalación.** Un módulo escrito una vez sirve a los dos productos; lo que cambia es dónde corre y qué se activa.

## Topología del ERP

```
   Sucursal                          Nube (si el negocio la contrata)
 ┌────────────────────────┐        ┌──────────────────────────┐
 │  Host .NET + Postgres  │◄──────►│  SILLAR WEB              │
 │  (una máquina del      │  sync  │  (satélite de catálogo)  │
 │   negocio)             │        └──────────────────────────┘
 │      ▲        ▲        │
 │      │        │        │◄──────► otras sucursales
 │  escritorio  red local │  sync
 └────────────────────────┘
```

- **El host modular de ASP.NET Core corre dentro del negocio**, con PostgreSQL local. Es la misma solución, desplegada en otro sitio.
- **La envoltura de escritorio** presenta la interfaz React ya construida con ícono propio, sin barra de navegador, y aporta lo que el navegador no puede: **impresión en crudo para tickets y apertura del cajón de dinero**. Esa es la razón técnica de que sea escritorio y no una pestaña; el resto es preferencia.
- **Las demás máquinas de la tienda** entran por la red local contra ese mismo host.
- **Cada sucursal es autónoma**: su servidor, su base, y vende aunque se caiga internet, la central o ambas.

## Sincronización — M16

> **Enmendada por la ADR-017.** Lo que sigue describe satélites con proyección parcial, un
> modelo pensado para desconexión frecuente. El modelo vigente es **mando y copia**: una sola
> base manda, la web conserva una réplica de lo compartido y nadie toma el mando por su
> cuenta. Se conserva el texto porque la parte de existencias por sucursal sigue valiendo.

Un módulo nuevo, `sync`, con dos papeles: **nodo central** y **satélite**. Una sucursal es un satélite. **Una instalación web también es un satélite** — recibe catálogo y existencias, devuelve pedidos. Un solo mecanismo para los dos casos.

**El ERP es la fuente de verdad de catálogo, precios y existencias.** El flujo baja hacia los satélites y solo sube lo que se origina en ellos: ventas, pedidos, movimientos. Una sola dirección por tipo de dato, así que no hay conflicto de edición posible.

### Las existencias son por sucursal, no globales

Es la decisión que elimina de raíz el problema más feo de la multisucursal. Si el stock fuera una bolsa común, dos sucursales sin conexión podrían vender la misma última unidad y no habría forma honesta de reconciliarlo.

Con existencias por local, **cada sucursal solo descuenta de lo suyo y el conflicto no existe**. Los traslados entre locales son documentos explícitos, no un efecto secundario. La central agrega para informes.

La instalación web se ata a **una** ubicación de existencias — el almacén que atiende los pedidos en línea. Publicar la suma de todas las sucursales sería prometer lo que no se puede despachar.

## Consecuencias

**Positivas.** Un solo código y un solo esfuerzo de mantenimiento. CORE entero se reutiliza sin tocar: usuarios, roles, sesiones, configuración, auditoría, medios, activación y licencias. El sistema de diseño y los componentes también. Y el ERP encaja con la ADR-001 llevada a su extremo: una instancia por cliente, aislamiento físico, sin `tenant_id` en ninguna tabla.

**Negativas.** Hay que construir y mantener la sincronización, que es la parte donde fallan todos los sistemas de este tipo. El despliegue del ERP ocurre en máquinas que no controlas: actualizar exige un mecanismo propio. Y aparece una clase de error que la web no tenía — el nodo desincronizado, que funciona bien y muestra datos viejos.

**Consecuencia que obliga a otra decisión.** Varios nodos creando registros sin conexión rompen la convención de claves enteras autogeneradas. Se resuelve en la **ADR-016**, y hay que resolverlo **antes** de construir M13.

## Sobre la licencia

Debe validarse **sin internet**: archivo firmado, con vencimiento y periodo de gracia. La ADR-004 ya lo previó como opción; con el ERP pasa a ser obligatorio. Un negocio no puede quedarse sin poder vender porque no hay señal.

## Nombres

Decididos:

| Nombre | Qué es |
|---|---|
| **SILLAR** | La plataforma y la familia. No se vende: se vende lo que se construye con ella |
| **SILLAR WEB** | El producto web. Todo lo construido hasta hoy y lo que falta de M01 a M12 |
| **SILLAR ERP** | El producto de gestión. Escritorio, sin internet, base interna |

En código no cambia nada: los espacios de nombres siguen siendo `Sillar.*` porque pertenecen a la plataforma, que es lo compartido. Los nombres de producto son etiquetas comerciales.
