# ADR-008 — Un repositorio por instalación, separado del producto

- **Estado:** Aceptada
- **Fecha:** 2026-08-14
- **Decide:** JP

## Contexto

Bajo el modelo de una instancia por cliente (ADR-001), cada negocio que instala SILLAR tiene su propia base de datos, su identidad visual, sus datos iniciales y su configuración de despliegue. Había que decidir dónde vive ese material.

La primera versión de la estructura lo colocaba en una carpeta `clients/` dentro del repositorio del producto. Eso plantea dos problemas:

1. **Acceso.** El repositorio del producto lo verá el equipo de desarrollo. No todos sus integrantes tienen por qué acceder a la información comercial de los clientes: qué contrató cada uno, sus datos de contacto, sus listas de precios.
2. **Confusión de propósito.** Tener clientes dentro del repositorio del producto invita justamente al error que la arquitectura intenta evitar: que funcionalidad de un cliente se filtre al núcleo porque "total, está en la misma carpeta".

## Decisión

El repositorio de SILLAR contiene **únicamente el producto**. Cada instalación vive en su propio repositorio privado.

```
sillar                       el producto: módulos, documentación, arquitectura
sillar-cliente-<negocio>     una instalación concreta
```

El repositorio de una instalación contiene:

```
marca/          tema visual: variables de color, tipografía, logo, favicon
datos/          seed propio: categorías, servicios, configuración, contenido inicial
despliegue/     configuración de su instalación, sin secretos
requisitos/     documentación del cliente: PRD, actas, acuerdos
README.md       ficha del negocio, módulos contratados, estado
```

Nunca contiene código de módulos. Si un cliente necesita algo que ningún otro necesitaría, se resuelve en este orden: **configuración → opción del módulo → módulo aparte**. Escribirlo dentro de un módulo existente no es una opción.

## Razones

- Permite dar acceso al producto sin dar acceso a los clientes, y acceso a un cliente sin dar acceso a los demás.
- Hace que la frontera entre producto y encargo sea física, no solo una convención documentada.
- Cada instalación puede versionarse a su propio ritmo: un cliente puede quedarse en una versión anterior del producto sin bloquear a nadie.
- Encaja con el modelo de despliegue: si cada cliente tiene su instancia, que tenga también su repositorio es coherente.

## Consecuencias

**Positivas.** Control de acceso por cliente. Frontera clara. El repositorio del producto puede llegar a abrirse o compartirse con terceros sin exponer a nadie.

**Negativas.** Más repositorios que administrar. Un cambio que afecte a producto y cliente a la vez exige dos commits en dos sitios. Hace falta documentar qué versión del producto usa cada instalación, porque ya no es evidente por estar en el mismo árbol.

## Material previo

La documentación generada antes de este giro —requisitos en PDF, diccionario de datos, modelo entidad-relación, scripts iniciales, prototipo visual y el registro de conversaciones— **no entra a este repositorio**. Se conserva archivada fuera de él y se usa como insumo para redactar los SPEC de los módulos.

Ese trasvase es deliberado: lo que era "lo que pidió un negocio" pasa a ser "lo que hace este módulo". Los requisitos no se pierden, cambian de naturaleza.
