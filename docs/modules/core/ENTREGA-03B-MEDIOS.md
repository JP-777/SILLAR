# CORE · Entrega 3b — Gestión de medios

- **Módulo:** `core` · **Estado:** Aprobado
- **Refina:** `SPEC.md` §4.8 (`media_assets`), §5 (`IMediaStorage`), §6 (endpoints) y §8 (reglas 11, 12, 13)
- **Decide sobre:** ADR-011

Última entrega de CORE. Con ella el módulo queda completo y el trabajo pasa al frontend.

**Alcance:** subida, listado, consulta y baja de archivos; validación de contenido; servido estático; marcado de huérfanos.

CORE es el único que toca el disco (ADR-011). Ningún módulo posterior implementa su propia subida de archivos: piden `IMediaStorage` y se acabó.

---

## 1. Formatos y límites

**Decisión: solo imágenes de mapa de bits — `image/jpeg`, `image/png`, `image/webp` — con un máximo de 5 MB.**

`Media:MaxSizeBytes` y `Media:AllowedTypes` van en configuración, pero no son un punto de extensión para el cliente: son constantes que conviene poder ajustar sin recompilar.

### Por qué no se admite SVG

Es la parte que hay que entender antes de aceptar un logo vectorial «porque pesa menos».

Un SVG es un documento XML que puede contener scripts. Servido desde la ruta estática, se ejecuta **en el mismo origen que el panel de administración**. La cookie de sesión es `httpOnly`, así que ese script no puede leerla — pero no lo necesita: el navegador la adjunta sola. Y desde el mismo origen puede pedir `GET /api/admin/auth/csrf`, que desde la entrega 2.1 devuelve un token válido y estable para la sesión activa. Con la cookie que viaja sola y el token que se pide, un SVG subido por cualquiera con rol `editor` puede ejecutar escrituras autenticadas con los permisos de quien lo mire.

Sanear SVG requiere una biblioteca y una lista de permitidos que hay que mantener; es una superficie de ataque conocida por sus evasiones. Se rechaza.

**Si más adelante hace falta vectorial**, hay dos salidas reales, y ninguna es sanear: servir los medios desde un origen distinto —un subdominio propio, que anula el argumento anterior—, o servirlos con `Content-Disposition: attachment`, que impide la ejecución pero también impide mostrarlos. La primera es la buena y encaja con el despliegue por instalación. Queda fuera de alcance hasta que exista el caso.

---

## 2. Validación

El orden es deliberado: se rechaza lo barato antes de gastar disco.

```
1. Tamaño declarado contra el límite. Rechazo antes de leer el cuerpo.
2. Lectura a un archivo temporal, con tope duro de bytes.
3. Tipo REAL del contenido por sus bytes iniciales, no por la extensión ni por
   el Content-Type que envió el cliente.
4. Si el tipo real no está en la lista → 415, y se borra el temporal.
5. Dimensiones, si es imagen.
6. Nombre generado, movimiento del temporal a su ruta definitiva, fila en
   core.media_assets.
```

**El paso 1 se aplica también en Kestrel** (`MaxRequestBodySize`). Sin eso, alguien que anuncie 2 GB consigue que el servidor los reciba antes de que ninguna validación opine.

El paso 3 es la regla 12 del SPEC. La extensión y el `Content-Type` los controla quien sube; los bytes iniciales, no. Un `.png` cuyo contenido es otra cosa se rechaza con **415**, no se guarda «por si acaso».

El temporal se borra en todos los caminos de fallo, incluida la excepción. Escribir primero en temporal y mover después evita que un fallo a mitad deje un archivo incompleto en la carpeta pública.

### El nombre en disco

Se genera. Nunca el original (SPEC §8.11), que solo se conserva en `original_name` para mostrarlo en el panel.

```
relative_path = <aaaa>/<mm>/<identificador-generado>.<extensión canónica>
```

La extensión sale del **tipo real detectado**, no de la que traía el archivo. El reparto por año y mes evita el directorio con cincuenta mil entradas, que es donde el sistema de archivos y cualquier listado empiezan a sufrir.

El nombre generado es la defensa contra el recorrido de rutas: da igual que alguien suba `../../etc/passwd`, porque ese texto nunca llega al sistema de archivos.

---

## 3. Duplicados

Se calcula el SHA-256 y se guarda en `checksum`. Si coincide con un archivo activo, **la subida se acepta igualmente** y la respuesta incluye `duplicateOf` con el identificador del existente, para que el panel pueda avisar.

No se reutiliza la fila existente. Reutilizarla haría que dos módulos apuntasen al mismo archivo y que dar de baja uno rompiera al otro, y evitarlo exige un recuento de referencias que hoy no existe y que nadie ha pedido. Detectar y avisar cubre el caso real —alguien sube dos veces la misma foto— sin introducir esa maquinaria.

---

## 4. Endpoints

### `POST /api/admin/media` · rol `editor`

`multipart/form-data` con el archivo, `altText` opcional y `ownerModuleCode` obligatorio.

Requiere token CSRF como cualquier escritura. `multipart` no lo exime.

`ownerModuleCode` se valida contra los módulos que el producto conoce; uno desconocido → 400. Se guarda como texto y sin FK, a propósito (SPEC §4.8): el módulo puede desinstalarse y el archivo tiene que sobrevivir.

Respuesta 201:

```json
{
  "mediaAssetId": 42,
  "url": "/media/2026/08/01J9X4Q7VZ8K.webp",
  "originalName": "logo tienda.webp",
  "mimeType": "image/webp",
  "sizeBytes": 184320,
  "width": 800,
  "height": 600,
  "duplicateOf": null
}
```

### `GET /api/admin/media` · rol `editor`

Listado paginado, con la paginación de `Sillar.Shared`. Filtros: `ownerModuleCode`, `isOrphan`, `mimeType`, `from`, `to`. Orden descendente por fecha.

### `DELETE /api/admin/media/{id}` · rol `admin`

Baja **lógica**: `is_active = false`. El binario se conserva.

Es coherente con la convención del proyecto —nunca borrado físico en tablas de negocio— y evita el fallo obvio: un archivo referenciado desde un banner o un producto desaparece y deja el hueco. La purga física del disco es una operación de mantenimiento y queda fuera de alcance.

El archivo dado de baja **deja de servirse** por la ruta estática. Si siguiera accesible, la baja no significaría nada.

### `GET /media/{ruta}` — ruta estática, pública

Fuera del API (ADR-011). Cabeceras obligatorias:

| Cabecera | Valor | Por qué |
|---|---|---|
| `Content-Type` | El almacenado | Nunca el adivinado del nombre |
| `X-Content-Type-Options` | `nosniff` | Impide que el navegador reinterprete el contenido |
| `Cache-Control` | Larga duración, `immutable` | El nombre es único e irrepetible: el contenido de una ruta nunca cambia |

---

## 5. Huérfanos

`is_orphan` marca los archivos cuyo módulo **ya no existe en el producto**, no los de un módulo desactivado.

La distinción importa. Desactivar es reversible y ocurre a diario en una demostración: marcar huérfanos al desactivar dejaría el panel lleno de avisos falsos, y al reactivar habría que deshacerlos. Un módulo desinstalado —ausente del catálogo que el código declara— sí deja sus archivos sin dueño para siempre.

El marcado se hace en la sincronización de `core.modules` del arranque (SPEC §7, paso 4): los `owner_module_code` que no correspondan a ningún módulo conocido pasan a `is_orphan = true`; los que vuelvan a aparecer, a `false`.

Los archivos huérfanos **no se borran** (SPEC §8.13). Se listan para que alguien decida.

---

## 6. `IMediaStorage`

Se implementa el contrato del SPEC §5 sin cambiarlo. `GetPublicUrl` devuelve la ruta relativa bajo `/media/`, nunca una ruta del sistema de archivos: es lo que permite que una implementación futura para S3 devuelva otra cosa sin que ningún módulo se entere.

`DeleteAsync` hace la baja lógica, igual que el endpoint.

**Eventos:** ninguno. Nadie los consume y el SPEC no los declara.

---

## 7. Respaldo

El ADR-011 lo anota como su consecuencia negativa y es el error clásico: se respalda la base de datos, se olvida el volumen, y al restaurar aparece un catálogo entero sin imágenes.

`backend/README.md` gana un apartado que diga qué carpeta hay que respaldar junto con la base y que ambas cosas se restauran juntas. Es documentación, pero es la que evita el desastre que nadie ve hasta el día que restaura.

---

## 8. Criterios de aceptación

Leyenda: **[x]** verificado por HTTP · **[~]** verificado por pruebas o por estructura del código, no por HTTP.

**Subida y validación**

- [x] Un archivo mayor que el límite se rechaza con 413 sin escribirse en disco
- [x] Un `.png` cuyo contenido no es PNG se rechaza con 415
- [x] Un `.svg` se rechaza aunque su contenido sea SVG válido
- [x] Un archivo válido se guarda con nombre generado; `original_name` conserva el enviado
- [x] Un nombre con `../` o con caracteres hostiles no afecta a la ruta de destino
- [x] La extensión en disco sale del tipo real detectado, no de la enviada
- [x] `width` y `height` se registran para las imágenes
- [x] Un fallo a mitad de la subida no deja archivos temporales ni ficheros incompletos
- [x] `POST` sin token CSRF devuelve 403 también en `multipart`
- [x] Un `ownerModuleCode` desconocido devuelve 400
- [x] Subir dos veces el mismo contenido crea dos filas y la segunda indica `duplicateOf`

**Servido y baja**

- [x] La ruta estática responde con el `Content-Type` almacenado y `X-Content-Type-Options: nosniff`
- [x] Un archivo dado de baja deja de servirse por la ruta estática
- [x] La baja no borra el binario
- [x] Un `editor` puede subir y listar, pero no dar de baja

**Huérfanos**

- [~] Desactivar un módulo **no** marca sus archivos como huérfanos
- [x] Un `owner_module_code` que ya no corresponde a ningún módulo conocido queda marcado al arrancar
- [x] El filtro `isOrphan` los devuelve
- [x] Un módulo que reaparece devuelve sus archivos a `is_orphan = false`

**General**

- [~] `IMediaStorage` funciona desde otro módulo sin acceder a `core.media_assets`
- [x] `create` y `delete` de medios quedan auditados
- [x] Todos los endpoints documentados en Swagger
- [x] `backend/README.md` documenta el respaldo del volumen

**Los dos [~], y por qué.** Ambos necesitan un segundo módulo, y solo existe CORE. El primero está garantizado por estructura: `MarkOrphanMediaAsync` compara únicamente contra los módulos que declara el código y **no contiene ni una sola referencia a activaciones** —comprobado sobre el propio archivo—, así que desactivar no puede marcar nada. El segundo se apoya en que el contrato `IMediaStorage` no expone la entidad ni el `DbContext`: `Sillar.Core.Contracts` no referencia `Sillar.Core`, de modo que un módulo que use el contrato no tiene forma de llegar a `core.media_assets` aunque quisiera.

---

## 8b. Cierre

- **Pruebas:** 181 en verde (49 en `Sillar.Shared.Tests`, 132 en `Sillar.Core.Tests`).
- **Sin migración:** `core.media_assets` ya existía completa desde `CoreInitial`.
- **Sin paquetes nuevos:** la detección de tipo y la lectura de dimensiones se escribieron a mano para los tres formatos.

### Lo que se comprobó a mano

| Comprobación | Resultado |
|---|---|
| PNG 3×2 y WebP 64×48 | 201, con tipo real y dimensiones correctas |
| Nombre en disco | Generado; `original_name` conserva `logo.png` |
| SVG con `<script>` dentro | 415 |
| `.png` con cabecera `MZ` | 415 |
| Archivo de 6 MB | 413, y Kestrel corta el cuerpo antes de leerlo |
| `ownerModuleCode` inventado | 400 |
| `multipart` sin token CSRF | 403 |
| Nombre `../../../etc/passwd.png` | Guardado como `019fff83-af54-…png`; nada fuera de `media/` |
| Mismo contenido dos veces | Dos filas, la segunda con `duplicateOf` apuntando a la primera |
| Ruta estática | `Content-Type: image/png`, `nosniff`, `immutable`; 67 bytes servidos |
| Tras la baja | La ruta responde 404 y el binario sigue en disco |
| `editor` | Sube y lista; al dar de baja recibe 403 |
| Carpeta `.tmp` | Vacía tras todos los rechazos y aciertos |
| Huérfanos | 2 marcados al arrancar con el módulo ausente; 0 tras devolverles su módulo |

### Decisiones tomadas durante la implementación

1. **La ruta estática es un endpoint propio, no `UseStaticFiles`.** El §4 exige que un archivo dado de baja deje de servirse, y eso necesita consultar `is_active`, que el middleware de archivos estáticos no sabe hacer. De paso, el `Content-Type` sale de la fila en lugar de adivinarse del nombre. Busca por `stored_name`, que ya tenía índice único, así que no hizo falta migración.
2. **El 413 hay que traducirlo.** Kestrel corta el cuerpo y lanza `BadHttpRequestException`; sin capturarla, el cliente recibía un 500 que parecía un fallo del servidor. El límite ya funcionaba —el archivo nunca llegó al disco—, pero la respuesta mentía.
3. **`image/*` no se admite como tipo declarado en Swagger.** Hay que listar los tres concretos; el comodín hacía fallar el arranque.
4. La carpeta temporal vive dentro de la misma raíz de medios para que mover el archivo terminado sea un renombrado dentro del mismo volumen. Entre volúmenes distintos, «mover» se convierte en copiar y borrar, y deja de ser atómico.

---

## 9. Fuera de alcance

| Qué | Cuándo |
|---|---|
| SVG y vectoriales | Cuando exista el caso y un origen separado para servirlos |
| Miniaturas y transformaciones | Cuando M01 o M02 las necesiten de verdad |
| Purga física del disco | Operación de mantenimiento, sin caso hoy |
| Recuento de referencias entre módulos y archivos | Cuando exista un segundo caso real |
| Almacenamiento externo (S3, Blob) | Implementación alternativa de `IMediaStorage`, sin tocar módulos |
| Reemplazar el contenido de un archivo existente | Se sube uno nuevo; el nombre inmutable es lo que permite cachear para siempre |
