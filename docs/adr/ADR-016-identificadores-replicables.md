# ADR-016 — Identificadores replicables

- **Estado:** Aceptada
- **Fecha:** 14 de agosto de 2026
- **Decide:** JP
- **Enmienda:** la convención de claves primarias de `CLAUDE.md`
- **Bloquea a:** M13, M14, M15, M16 — hay que resolverlo **antes** de construirlos

## Contexto

Desde la primera migración, todas las claves primarias son `integer GENERATED ALWAYS AS IDENTITY`. Fue correcto mientras existía una sola base de datos por instalación.

La ADR-015 introduce **nodos autónomos que crean registros sin conexión**: sucursales que venden con internet caído y sincronizan después. Dos sucursales generando ventas a la vez producirían la venta número 1.048 cada una, y al sincronizar chocarían.

No es un caso raro: es el funcionamiento normal del sistema.

## Alternativas

| Opción | Por qué se descarta o se elige |
|---|---|
| Rangos de numeración por nodo | Funciona hasta que alguien añade un nodo sin repartir rango, o un nodo agota el suyo. Falla en silencio y tarde |
| Clave compuesta nodo + secuencia | Correcta, pero contamina cada clave foránea y cada consulta del sistema con una segunda columna |
| **UUID v7** | Globalmente único sin coordinación, y **ordenado en el tiempo** |

### La que más se propone: meter la sucursal dentro del número

La idea es natural —la sucursal 3 numera `33459`, y al leer en general se ignora el primer dígito— y **resuelve de verdad la unicidad**. Se descarta por lo que cuesta:

| Problema | Por qué duele |
|---|---|
| El número **no dice dónde acaba el prefijo** | `33459` es la sucursal 3 con el 3459, y también la 33 con el 459. La regla vive fuera del dato, en la cabeza de quien lo lee |
| Techo de 9 sucursales, grabado en cada fila | Pasar de 9 obliga a reescribir claves primarias y todas sus foráneas. Eso no es migrar: es reconstruir |
| Alguien tiene que repartir los números | Una sucursal recién instalada no puede elegir el suyo sin preguntar, que es justo lo que se quería evitar |
| La topología queda dentro del dato | Renumerar, fusionar o cerrar un local obliga a tocar identificadores que ya viajaron a comprobantes emitidos |

**Y el beneficio que busca —poder leerlo— no aplica a esta columna, porque la clave no se lee nunca (regla 2).** La idea es buena; es la columna la que está equivocada. Se aplica entera al **código visible**, que sí se dicta por teléfono y sí lleva la sucursal delante: `V-03-000459`.

La sucursal, además, no se pierde: va en `origin_node` (regla 4), en su propia columna, donde se puede consultar, filtrar y agrupar. Dentro de la clave estaría escondida; fuera, está disponible.

UUID v4 se descarta por una razón concreta: al ser aleatorio, cada inserción cae en una página distinta del índice y destruye la localidad. En una tabla de ventas que crece todos los días eso se nota. **v7 lleva la marca de tiempo delante**, así que se inserta al final como un entero incremental y conserva el comportamiento del índice.

## Decisión

**Las tablas que se replican usan `uuid` v7 como clave primaria. Las que no se replican conservan `integer GENERATED ALWAYS AS IDENTITY`.**

No es una migración global: es una distinción por tabla, decidida en el SPEC de cada módulo con una sola pregunta.

> **¿Esta fila puede nacer en un nodo y tener que existir en otro?**

| Se replica → `uuid` v7 | No se replica → `integer` |
|---|---|
| Productos, categorías, existencias | Sesiones administrativas |
| Ventas y sus líneas | Registro de auditoría |
| Clientes | Configuración del sitio |
| Comprobantes | Activación de módulos |
| Movimientos de inventario, compras | Archivos y sus metadatos |

**Lo ya construido no se toca.** Ninguna tabla de CORE se replica: las sesiones son locales, la auditoría es del nodo, la configuración es de la instalación y los módulos activos también. Las 181 pruebas siguen valiendo.

La única excepción a revisar es `core.admin_users`: si el personal debe poder entrar en cualquier sucursal con la misma cuenta, esa tabla se replica y necesita `uuid`. **Queda pendiente de decidir en el SPEC de M16**, no antes.

## Reglas

1. El identificador lo **genera la aplicación**, no la base de datos. Un nodo sin conexión tiene que poder crear la fila entera antes de hablar con nadie.
2. **Ningún identificador se muestra al usuario.** Nadie lee un UUID en voz alta por teléfono. Los códigos visibles —número de venta, de comprobante, de pedido— son campos aparte, legibles y con su propia serie por nodo.

   Son **dos columnas con dos oficios**, y cada una hace bien el suyo:

   | | Clave primaria | Código visible |
   |---|---|---|
   | Ejemplo | `01a0043b-08e1-79d2-bbc7-332bacb20684` | `V-03-000459` |
   | Para quién | La máquina | La persona |
   | Lleva la sucursal | No — va en `origin_node` | **Sí, delante** |
   | Se puede dictar por teléfono | No | Sí |
   | Admite guiones, letras, ceros a la izquierda | No | Sí |
   | Se puede renumerar | Nunca | Sí, mientras no salte ni reinicie |

   Esta separación es la que permite que la clave sea fea y el número del ticket sea legible, en vez de una sola columna que hace mal las dos cosas. La serie por sucursal, además, **no es una preferencia: SUNAT exige serie propia por punto de emisión.**
3. Las claves foráneas entre tablas replicadas también son `uuid`. Mezclar tipos en una relación es el camino corto a un error de conversión silencioso.
4. Las tablas replicadas llevan además el nodo de origen y una marca de versión, que M16 necesita para ordenar los cambios.

## Consecuencias

**Positivas.** Un nodo sin conexión crea registros sin coordinarse con nadie y sincroniza cuando puede, que es el requisito. Sin rangos que repartir ni claves compuestas que arrastrar. Y como v7 va ordenado, los índices se comportan como con enteros.

**Negativas.** Dos convenciones de clave conviviendo, y por tanto una pregunta que responder en cada tabla nueva. Más bytes por fila y por índice. Y depuración más incómoda: un `uuid` en un mensaje de error no se recuerda ni se compara de un vistazo como un `42`.

**Lo que hace que valga la pena.** Esta decisión solo es barata ahora. Cambiar el tipo de las claves primarias con datos reales en varias sucursales no es una migración: es una reconstrucción. Es el mismo caso que la colación del clúster y el nombre del producto — la ventana está abierta y se cierra en cuanto se escriba la primera tabla de M13.

## Cambio en `CLAUDE.md`

La regla actual dice:

> `integer GENERATED ALWAYS AS IDENTITY` para claves primarias, nunca `SERIAL`

Pasa a decir:

> Claves primarias: `uuid` v7 generado por la aplicación en **tablas que se replican entre nodos**; `integer GENERATED ALWAYS AS IDENTITY` en las que no. Nunca `SERIAL`. Ante la duda, la pregunta es si esa fila puede nacer en un nodo y tener que existir en otro. Los identificadores nunca se muestran al usuario: los códigos visibles son campos aparte.
