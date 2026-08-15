# CORE · Entrega 4b — Configuración, auditoría y medios

- **Módulo:** `core` · **Estado:** Aprobado
- **Depende de:** entrega 4a — reutiliza sus seis patrones
- **Cierra:** las pantallas de CORE. Después, M01

Tres pantallas sobre patrones ya fijados. **Si alguna necesita un patrón nuevo que no esté en el §0, es señal de parar y discutirlo**, no de inventarlo.

---

## 0. Corrección previa — el 409 y los nombres

En 4a, `describe()` muestra la frase del servidor tal cual. La decisión es buena y se mantiene: el backend ya redacta para leerse y duplicar el texto sería corregir solo una copia.

Pero produce esto:

> …estos módulos activos dependen de él: **demo_sales**

El usuario ve un código. La entrega 4a pedía nombre visible y enlace a la tarjeta, y el código no es ninguna de las dos cosas.

**La respuesta 409 lleva además datos estructurados:**

```json
{
  "message": "No puede desactivarse porque estos módulos activos dependen de él.",
  "blockedBy": ["sales", "tracking"]
}
```

La interfaz usa `blockedBy` para escribir los nombres visibles —que ya tiene, del listado de módulos— y enlazarlos a su tarjeta. Si no reconoce la forma, cae a `message`.

Es la división correcta: **el servidor explica el motivo, la interfaz hace lo único que solo ella puede hacer, que es enlazar.** Se corrige aquí porque 4b va a mostrar más conflictos y conviene que el patrón esté bien antes de repetirlo.

### Y una consecuencia que hay que dejar escrita

Con `describe()`, **los mensajes de error del backend son ahora texto de interfaz**. Dejan de ser un detalle interno: alguien que más adelante los acorte a algo técnico o telegráfico degradará la interfaz sin tocar el frontend ni enterarse.

Regla: los mensajes de error del API se redactan para que los lea una persona que administra su negocio, no un desarrollador leyendo un registro.

---

## 1. Configuración

Editar `core.site_settings`. Es la pantalla que quita llamadas de teléfono: el número de WhatsApp, el horario y la dirección se cambian aquí y no tocando código.

### Listado

Agrupado por área —negocio, contacto, redes, moneda—, no una lista plana de claves. La agrupación sale de un prefijo o de una tabla de correspondencia en el frontend; **no se añade una columna a la base de datos para esto**.

Cada entrada muestra la descripción, no solo la clave: `whatsapp_number` no le dice nada a nadie, «Número de WhatsApp para pedidos» sí.

El control se elige por `value_type`: texto, número, interruptor, url, correo. `json` se edita como texto con validación de sintaxis.

### Los valores sin definir

El seed deja `PENDIENTE_DEFINIR` en todo lo que depende del negocio. **La pantalla los destaca y los cuenta**: «Faltan 6 datos por completar». Es lo que convierte una instalación recién hecha en una lista de tareas en vez de en un formulario mudo.

### Quién puede qué

| Acción | Rol |
|---|---|
| Cambiar el valor | `admin` |
| Cambiar `is_public` | **`super_admin`** |
| Crear o borrar claves | **Nadie.** Nacen del seed o de la migración de su módulo |

La diferencia de rol tiene que **verse**. Publicar un dato es de otra naturaleza que corregir un teléfono: el interruptor de público va separado del valor, con su propia confirmación que dice qué implica —que ese dato será visible para cualquiera en la web pública, sin sesión—.

Un `admin` ve el estado público pero no puede cambiarlo, y el control aparece deshabilitado con la razón. Aquí sí se deshabilita en vez de ocultar: ocultarlo haría creer que el dato no es público.

### Lo que esta pantalla no ofrece

**Historial de valores.** La auditoría registra que alguien cambió una configuración, pero no el valor anterior ni el nuevo — decidido en la entrega 3, porque esta tabla albergará credenciales algún día y una auditoría inmutable sería un almacén de secretos en claro.

La pantalla no debe insinuar que ese historial existe.

---

## 2. Auditoría

Tabla de lectura. Densa, paginada, sin edición: la regla 15 del SPEC dice que no se edita ni se borra desde el API, y la pantalla no ofrece ninguna acción que sugiera lo contrario.

**Columnas:** fecha y hora, quién, módulo, acción, entidad, resumen.

**Filtros:** rango de fechas, usuario, módulo, acción. 50 por página, 200 como máximo.

**Cuando el usuario ya no existe** se muestra `admin_user_email`, el snapshot, con una marca de que la cuenta fue eliminada. Es la razón de que esa columna exista: un registro de auditoría que pierde la identidad de quien actuó no sirve para nada.

Solo `super_admin`. La entrada de menú no existe para los demás.

---

## 3. Medios

**Galería, no tabla.** Es el único patrón nuevo de esta entrega y está justificado: los medios son visuales y una tabla de nombres de archivo obliga a abrir cada uno para saber cuál es.

### Subida

Arrastrar y soltar, más un botón. La validación de tipo y tamaño se hace también en el cliente **como cortesía, no como control**: la del servidor es la que manda y la del cliente solo evita esperar a que suban 5 MB para nada.

Los tres desenlaces:

| Respuesta | Qué se muestra |
|---|---|
| **413** | El archivo supera el máximo de 5 MB |
| **415** | Formato no admitido. Se aceptan JPEG, PNG y WebP |
| **201 con `duplicateOf`** | **No es un error.** El archivo se subió, y se avisa de que ya existe una copia, con enlace a ella |

Ese último importa: la entrega 3b decidió detectar duplicados sin fusionarlos. Presentarlo como error contradiría la decisión y confundiría a quien acaba de subir algo correctamente.

Si el formato rechazado es SVG, el mensaje lo dice por su nombre en vez de repetir la lista. Alguien que sube un logo vectorial merece saber que es *ese* formato el que no entra.

### Galería

Miniatura, nombre original, tamaño, dimensiones, módulo dueño y fecha. Filtros por módulo, tipo, huérfanos y fechas. Acción de copiar la URL, que es lo que alguien va a querer hacer el 90% de las veces.

### Huérfanos

Se muestran destacados y **con explicación en la propia pantalla**: el módulo que subió estos archivos ya no está instalado. Sin esa frase, «huérfano» no significa nada para quien administra una librería.

No se borran (SPEC §8.13). La pantalla los lista para que alguien decida, y no ofrece ninguna acción de purga.

### Baja

Lógica, rol `admin`. El diálogo advierte de que el archivo **dejará de verse en la web** allí donde esté referenciado — que es la consecuencia real, no que se marque una columna.

Un `editor` sube y lista, pero no da de baja.

---

## 4. Criterios de aceptación

Leyenda: **[x]** verificado · **[~]** verificado por lectura del código · **[ ]** requiere navegador.

**Corrección previa**

- [x] El 409 devuelve `blockedBy` con los códigos
- [~] La interfaz muestra nombres visibles y enlaza a la tarjeta del módulo que bloquea
- [~] Sin `blockedBy` reconocible, cae a `message` sin romperse

Comprobado contra el servidor:

| Caso | `title` | `blockedBy` |
|---|---|---|
| Desactivar `demo_catalog` | «Catálogo de Productos (demostración)» no se puede desactivar porque otros módulos activos dependen de él. Desactívalos primero. | `["demo_sales"]` |
| Activar `demo_tracking` | «Seguimiento de Servicios (demostración)» necesita otros módulos que ahora mismo están inactivos. Actívalos primero. | `["demo_service_orders"]` |

El mensaje **ya no lleva los códigos embebidos**, y se basta solo para el caso de respaldo. `readCodes` comprueba la forma antes de fiarse: si el servidor cambia el contrato, la interfaz cae al mensaje en vez de romperse a mitad de pintar.

**Configuración**

- [x] Agrupada por área, con descripción visible y no solo la clave
- [x] Los `PENDIENTE_DEFINIR` se destacan y se cuentan
- [~] Un `admin` puede cambiar el valor pero no `is_public`, y ve por qué
- [~] Cambiar `is_public` pide confirmación que explica qué implica
- [x] No hay forma de crear ni borrar claves
- [x] La pantalla no insinúa que exista historial de valores

Las once claves del seed caen en los cuatro grupos, ninguna en «Otros», todas con descripción. El reparto vive en `settings.ts` y **no se añadió ninguna columna a la base de datos** para agruparlas.

**Auditoría**

- [x] Paginada, con los cuatro filtros
- [x] Ninguna acción de edición ni de borrado
- [x] Un registro de un usuario eliminado muestra el correo del snapshot
- [~] La entrada de menú no existe fuera de `super_admin`

**Medios**

- [ ] Arrastrar y soltar funciona, y el botón también
- [x] 413 y 415 dan mensajes distintos y concretos
- [x] El SVG rechazado se nombra por su formato
- [~] Un duplicado se presenta como aviso, no como error, con enlace al original
- [~] Los huérfanos se destacan y la pantalla explica qué significa
- [x] No existe ninguna acción de purga
- [~] Un `editor` sube y lista, pero no ve la acción de baja
- [~] El diálogo de baja advierte de que dejará de verse en la web

**Transversales**

- [x] Ningún patrón nuevo salvo la galería
- [x] Ningún «Ha ocurrido un error», ningún botón «Aceptar»
- [x] Ningún color literal
- [ ] Navegable con teclado, foco visible, diálogos que atrapan el foco
- [ ] Correcto en tema claro y oscuro

Los cuatro `confirmLabel` de la aplicación son «Cerrar sesión», «Desactivar usuario», «Dar de baja» y «Publicar dato» / «Dejar de publicar», más el de módulos que nombra el módulo. `shared/ui/` gana exactamente un archivo de patrón: `Gallery.tsx`.

---

## 4b. Cierre

- **Compilación:** `pnpm build` limpio; backend con 0 advertencias y 181 pruebas en verde.
- **Sin migración** y **sin dependencias nuevas.**

### Un fallo que encontré al verificar

La marca de «cuenta eliminada» se basaba en que `adminUserId` fuese nulo. Pero **un acceso fallido con un correo que no existe también llega así**: el backend audita el intento con el correo que se escribió y sin identificador. Con la lógica inicial, alguien tecleando mal su correo aparecería en la auditoría como una cuenta borrada.

Son dos situaciones distintas y confundirlas sería mentir:

- En un `login_failed`, el correo es lo que alguien escribió. Puede no haber existido nunca. **Sin marca.**
- En cualquier otra acción, para haberla hecho la cuenta tuvo que existir, así que si ya no está es que la eliminaron. **Con marca.**

Corregido y comprobado provocando un acceso fallido real.

### Lo que queda por mirar en un navegador

Arrastrar y soltar, el recorrido con teclado, los diálogos atrapando el foco y el tema oscuro.

```bash
cd backend  && Modules__IncludeDemoModules=true dotnet run --project Sillar.Api
cd frontend && pnpm dev
```

El panel queda con seis entradas de menú, filtradas por rol: Módulos y Configuración desde `admin`, Archivos desde `editor`, Usuarios y Auditoría solo para `super_admin`.

---

## 5. Fuera de alcance

| Qué | Cuándo |
|---|---|
| Historial de valores de configuración | Decidido que no. Ver §1 |
| Purga física de medios | Operación de mantenimiento, sin caso |
| Miniaturas generadas y recortes | Cuando M01 o M02 los necesiten |
| Exportar la auditoría | Cuando alguien lo pida de verdad |
| Retención de auditoría | Cuando exista una instalación con volumen real |
| Pruebas de interfaz automatizadas | Ahora sí conviene evaluarlo: los patrones quedan estables al cerrar 4b |
