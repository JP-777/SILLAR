# ADR-018 — Una tabla que se replica no puede referenciar una que no

- **Estado:** Aceptada
- **Fecha:** 15 de agosto de 2026
- **Decide:** JP
- **Enmienda:** la tabla de clasificación de la ADR-016
- **Bloquea a:** la primera migración de M01, que ya está escrita y hay que corregir

## Cómo apareció

Al revisar la migración inicial de M01 aparecieron cuatro columnas así:

```
brands.logo_id                 integer   →  core.media_assets.id
categories.image_id            integer   →  core.media_assets.id
product_images.media_asset_id  integer   →  core.media_assets.id
product_items.image_id         integer   →  core.media_assets.id
```

Cada una es correcta por separado. La ADR-016 clasificó los archivos como **no replicables**, así que `core.media_assets` conserva su clave entera, y una FK hacia ella es entera.

El fallo está en juntarlas con la ADR-017, que dice que **el catálogo sí se replica**. Una fila de producto viaja del ERP a la web llevando `image_id = 42`, y en el otro nodo el 42 es otro archivo o no es nada. **La tienda en línea recibe el catálogo sin fotos, y sin error.**

El error de clasificación es de la ADR-016, no de la implementación.

## La regla que faltaba

> ### Una tabla que se replica no puede referenciar una que no se replica.

La dirección importa y no es simétrica:

| Origen | Destino | ¿Vale? | Por qué |
|---|---|---|---|
| Se replica | Se replica | **Sí** | La fila destino existe en los dos nodos |
| **No** se replica | Se replica | **Sí** | La fila origen se queda en su nodo, y su destino está ahí |
| **Se replica** | **No se replica** | **No** | La fila viaja y su referencia se queda. Apunta a otra cosa, o a nada |

La tercera fila es el error, y no avisa: no hay violación de clave foránea, porque cada base es internamente coherente. Solo hay un catálogo sin imágenes.

Es una regla **comprobable**: al escribir cualquier FK, mirar si las dos tablas están en el mismo lado de la línea.

## Decisión

**1. `core.media_assets` se replica.** Clave `uuid` v7 generada por la aplicación, más `origin_node` y `row_version`, igual que las tablas del catálogo.

**2. La clave es el nombre del archivo.** CORE ya genera nombres así:

```
media/2026/08/019fff83-a5d5-74b0-9e3b-7b9a0d6d273d.png
                        ↑ nibble de versión: 7
```

Ya son `uuid` v7. Lo único local era la clave entera de la fila, que resultaba ser un **segundo identificador para lo mismo**. Se elimina: el `id` de la fila y el nombre del archivo pasan a ser el mismo valor. Un archivo encontrado en el disco se rastrea hasta su fila sin buscar nada, y al revés.

**3. Las FK del catálogo hacia medios pasan a `uuid`.** Son las cuatro de arriba.

**4. Lo demás de CORE no cambia.** Sesiones, auditoría, configuración y activación de módulos siguen siendo enteras y locales. Son del nodo por naturaleza, y ninguna tabla replicada las referencia.

## La consecuencia que hay que ver ahora

**Replicar la fila no replica el archivo.** Una fila de medios sin sus bytes al otro lado es una imagen rota, que es peor que una imagen ausente.

M16 tendrá que mover **dos cosas de naturaleza distinta**: filas y archivos. Es una carga nueva que antes no tenía, y hay que preverla en su diseño: mientras el archivo no haya llegado, la fila existe pero no se puede mostrar, y la interfaz tiene que saber decirlo.

Se anota aquí y se resuelve en el SPEC de M16, no antes.

## La segunda aplicación de la regla, que ya se ve venir

`core.admin_users` es entera y local. Una venta registra **quién la hizo**, y las ventas se replican.

La ADR-016 lo dejó como «pendiente de decidir en el SPEC de M16, según si el personal debe poder entrar en cualquier sucursal». Con esta regla ya no es una preferencia: **si la venta se replica y referencia al usuario, `core.admin_users` se replica.** No hay tercera opción, salvo guardar el nombre del vendedor como dato snapshot y renunciar a la FK.

Se decide **antes de M13**, no en M16.

## Cómo se aplica

**Editando la migración inicial de CORE, no añadiendo una nueva.** Es legítimo por una razón concreta y temporal:

- **No existe ninguna instalación desplegada.** La única base con datos es la de desarrollo, y sus cinco archivos son de prueba.
- Convertir la clave primaria de una tabla con FK dependientes es una migración larga y frágil, y aquí no hay nada que conservar.

Se rehace el clúster con `docker compose down -v` y se aplican las migraciones desde cero. **Es la última vez que esto vale**: en cuanto haya una instalación con datos de un negocio, las migraciones vuelven a ser solo-añadir.

Es el mismo trato que se le dio a la colación del clúster, y por el mismo motivo: la ventana está abierta y se cierra sola.

## Consecuencias

**Positivas.** Desaparece un identificador redundante. El catálogo replicado llega con sus imágenes. Y queda una regla verificable que evita la siguiente ocurrencia del mismo error, que ya se sabe cuál es.

**Negativas.** Se toca CORE, que la ADR-016 daba por intocado, y hay que revisar sus pruebas de medios. M16 crece: mover archivos no es mover filas. Y la migración de M01, que ya estaba escrita y compilaba, hay que corregirla.

## Corrección a la ADR-016

Su tabla de clasificación pone «Archivos y sus metadatos» en la columna de lo que **no** se replica. Es falso desde la ADR-017. Pasa a la columna de la izquierda, con `uuid` v7.

El resto de la ADR-016 sigue vigente: el criterio, las cuatro reglas y el rechazo de v4 y de los prefijos por sucursal no cambian.
