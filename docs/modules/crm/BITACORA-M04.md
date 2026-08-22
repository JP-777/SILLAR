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

**Camino definido, y sin cambiar lo que ve el visitante:**

1. Registrarse con el correo de una ficha de baja **da exactamente la misma respuesta** que
   cualquier otro registro, y **se envía el correo de verificación igual**.
2. **Al verificar** —que es la prueba de que esa persona controla ese correo— **se crea la cuenta y
   se enlaza a la ficha existente**.
3. **La ficha sigue de baja.** No se reactiva sola: darla de baja fue una decisión del negocio y no
   la revierte quien se registra.
4. **Quien administra ve la solicitud** en la ficha, con su fecha. Reactivar es suyo.

> **Por qué no se reactiva sola:** verificar el correo prueba que controla el buzón, no que el
> negocio quiera volver a tenerlo de cliente. Y reactivar en silencio convertiría un formulario
> público en una forma de deshacer una decisión del negocio.

**Esto es una decisión de producto tomada por mí para desbloquear el esquema.** Es reversible —no
cambia ninguna columna, solo qué se hace con ellas—, pero conviene que se mire.

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
