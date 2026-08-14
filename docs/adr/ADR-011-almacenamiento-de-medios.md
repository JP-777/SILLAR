# ADR-011 — Medios en disco local con volumen

- **Estado:** Aceptada
- **Fecha:** 2026-08-14
- **Decide:** JP

## Contexto

Varios módulos suben archivos: imágenes de productos, banners, iconos de servicios, fotos de trabajos destacados, el logo del negocio. Había que decidir dónde se guardan y quién se encarga.

## Decisión

Los archivos se guardan en **disco local, en un volumen de Docker**, y los gestiona un servicio único del módulo CORE. Ningún módulo maneja archivos por su cuenta.

## Razones

- Encaja con una instancia por cliente: cada instalación tiene sus archivos junto a su base de datos, sin cuentas ni servicios externos.
- No añade costo ni credenciales por instalación. Pedirle a una librería de barrio que abra una cuenta de almacenamiento en la nube para poder subir la foto de un cuaderno es fricción comercial absurda.
- Encaja con el despliegue en contenedores que ya se decidió en la ADR-006.
- Que CORE sea el único que toca el disco evita que cada módulo invente su propia validación de archivos, que es donde aparecen los agujeros.

## Diseño

- Los metadatos viven en `core.media_assets`; en disco solo está el binario.
- El nombre en disco **se genera**, nunca se usa el nombre original que envió el usuario. Es la defensa contra recorrido de rutas y contra nombres con caracteres hostiles.
- Se valida tipo real del archivo, no solo la extensión, y se aplica un límite de tamaño.
- Cada archivo registra qué módulo lo subió. Al desinstalar un módulo, sus archivos **no se borran**: quedan marcados como huérfanos y se listan en el panel para que alguien decida.
- Las imágenes se sirven por una ruta estática dedicada, no a través del API.

## Consecuencias

**Positivas.** Sin dependencias externas ni costo variable. Simple de respaldar: es una carpeta. Validación centralizada en un solo punto del código.

**Negativas.** **El volumen tiene que estar en la copia de seguridad**, y es el error clásico: se respalda la base de datos y se olvidan los archivos, con lo que al restaurar aparece un catálogo entero sin imágenes. Además, esto descarta hostings de disco efímero como Azure App Service en su modalidad estándar, que era una de las opciones de despliegue contempladas al principio; habrá que desplegar en algo con volumen persistente.

## Ruta de escape

El acceso al disco vive detrás de `IMediaStorage`, en el contrato de CORE. Añadir una implementación para S3 o Azure Blob más adelante es escribir una clase y cambiar el registro del servicio, sin tocar ningún módulo.

Esto contradice ligeramente la regla de no crear abstracciones hasta tener un segundo caso. Se acepta la excepción porque el segundo caso está identificado —un cliente que exija despliegue en un hosting sin volumen persistente— y porque el costo de la interfaz aquí es de unas pocas líneas.
