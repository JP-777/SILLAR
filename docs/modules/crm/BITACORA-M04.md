# Bitácora de M04 Clientes

Lo que se decide mientras se construye este módulo, con su porqué. **Se vuelca en
`docs/BITACORA.md` al cerrar**; hasta entonces vive aquí para que la común tenga un solo escritor.

---

## La ventana del esquema, y cuándo se cierra

**Abierta desde el 21 de agosto de 2026 hasta la primera instalación de M04 en cualquier entorno.**

> **Mientras M04 no esté instalado en ningún sitio, la migración inicial se reescribe. Desde la
> primera instalación, solo se añade.**

Es la misma regla que se escribió para M02, y está aquí con fecha porque **quien lea esto dentro de
tres meses necesita saber en cuál de los dos días está**. Antes de esa línea, equivocarse en una
columna cuesta una tarde; después, una migración con datos dentro.

Eso no es permiso para ir deprisa. **Es permiso para no ir con miedo**, que no es lo mismo: hay que
ir con cuidado igual, pero sin pagar por adelantado el precio de un error que hoy todavía se
deshace.

---

## Decisiones del esquema

### El índice único del correo cubre **todas** las filas, incluidas las de baja

**La alternativa era un índice parcial** —único solo entre fichas activas—, y se descarta porque
**rompe la regla del SPEC §4**: si puede haber dos fichas con el mismo correo, «se enlaza a la
existente» deja de ser determinista y hay que decidir a cuál.

Pero eso abre el caso que el SPEC no cerraba, y que **no es raro**: sin borrado físico, **todo el
que se dé de baja conserva su correo para siempre**. Con el índice completo, esa persona chocaría al
registrarse — y recibiría el mensaje genérico, porque la respuesta no puede revelar que la cuenta
existe. **Un callejón sin salida disfrazado de mensaje neutro.**

**Camino definido, y depende de qué significaba la baja — porque no todas significan lo mismo:**

1. Registrarse con el correo de una ficha de baja o bloqueada **da exactamente la misma respuesta**
   que cualquier otro registro, y **se envía el correo de verificación igual**. La diferencia entre
   los dos estados no se filtra en esta pantalla.
2. **Al verificar** —que es la prueba de que esa persona controla ese correo— **se crea la cuenta y
   se enlaza a la ficha existente**, en los dos casos.
3. **De ahí en adelante, los dos estados se separan:**
   - **De baja + verifica → la ficha se reactiva.** «De baja» no es una decisión del negocio contra
     esa persona: es que dejó de estar activa (limpieza, inactividad, o ella misma lo pidió). Volver
     y demostrar que controla su buzón es exactamente lo que revierte ese estado, y es el caso
     común, no el raro.
   - **Bloqueada + verifica → no entra.** Ahí sí hubo una decisión del negocio, y un formulario
     público no la deshace. **La solicitud queda registrada en la ficha, con su fecha**; reactivar
     sigue siendo de quien administra.
4. **Un estado que carga dos significados obliga a elegir el peor comportamiento para los dos** —por
   eso la separación, no un tercer estado nuevo: ya existían los dos, solo estaban tratados como uno.

> **Por qué «de baja» sí se reactiva sola y «bloqueada» no:** verificar el correo prueba que
> controla el buzón, no que el negocio quiera volver a tenerlo de cliente — y eso solo es un
> problema cuando «de baja» *significa* que el negocio no lo quiere. Para una ficha simplemente
> inactiva no significa eso, y negarle la reactivación automática es un callejón sin salida para
> el caso normal, disfrazado de cautela para el caso raro.

**Al reactivar por verificación, queda en la auditoría:** qué ficha, cuándo, y que fue por
verificación de correo, no por mano de nadie — es un cambio de estado que ninguna persona
autorizó, así que tiene que poder leerse después.

**Decisión de producto — cerrada.** La versión anterior de este párrafo trataba «de baja» y
«bloqueada» como un solo estado sin reactivación automática, marcada explícitamente para revisión.
Esta es esa revisión: la reactivación automática aplica solo a «de baja»; «bloqueada» se queda
exactamente como estaba.

No implementes todavía la auditoría descrita en ese texto. Eso pertenece a Paso 3.

### El documento es único **entre las fichas que lo tienen**

Índice parcial, donde no es nulo. Dos clientes con el mismo DNI es un error de datos, no un caso.

**Y choca con el alta manual**, que es lo que hay que resolver en la pantalla y no en la base:

> **La ficha avisa antes de guardar: «ya existe una ficha con ese documento», con el enlace a
> ella.** Un choque de índice único no es un mensaje para una persona — es lo mismo que ya se
> resolvió en marcas y categorías, donde el 409 nombra lo que estorba en vez de decir «conflicto».

El índice sigue estando, porque la pantalla es una comodidad y la base es la garantía.

### El testigo de un solo uso se consume en **una sola operación**

Un testigo usado **no se borra** —se perdería el rastro— así que lleva su marca de usado. Y ahí hay
una carrera real: dos clics seguidos en el mismo enlace, o el enlace abierto en dos pestañas.

**Leer y luego escribir no vale**: los dos leen «sin usar» y los dos siguen adelante.

```sql
UPDATE crm.customer_tokens
   SET used_at = now()
 WHERE customer_token_id = @id
   AND used_at IS NULL
   AND expires_at > now()
```

**Se actúa solo si esa operación afectó a una fila.** Cero filas significa «ya estaba usado o
caducó», y las dos son la misma respuesta para quien lo abrió.

**Y se verifica por el efecto, no leyendo el código:** dos peticiones a la vez con el mismo testigo,
**una funciona y la otra no.**

### `contact_messages` entra en la migración inicial

`ARQUITECTURA_MODULAR.md` ya asignaba `contact_messages` a M04 y declaraba su
FK interna hacia `customers`. Su ausencia del SPEC era una omisión, no una
decisión de retirarla.

Entra ahora, mientras la ventana de esquema sigue abierta, en vez de convertir
una tabla conocida desde arquitectura en una migración posterior.

No replica: ADR-017 deja la captación del lado WEB. `customer_id` es opcional
porque un mensaje puede llegar antes de que exista una ficha.

### La búsqueda por nombre quita diacríticos antes del stemming

`core.es_search` expresa correctamente que un nombre se busca sin exigir
mayúsculas ni tildes, pero PostgreSQL 16 no permite LIKE/ILIKE sobre esa
colación no determinista y la configuración `spanish` de texto completo no
elimina diacríticos: «Peña» produce `peñ` y «pena» produce `pen`.

CRM crea `crm.spanish_unaccent`, copia de `pg_catalog.spanish` con el
diccionario `unaccent` delante de `spanish_stem`. Así la búsqueda conserva
stemming español y además ignora diacríticos.

La extensión `unaccent` es compartida y no se elimina al desinstalar CRM;
la configuración `crm.spanish_unaccent` sí pertenece al módulo y desaparece
con su schema.
