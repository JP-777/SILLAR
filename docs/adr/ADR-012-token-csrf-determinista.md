# ADR-012 — Token CSRF determinista derivado de la sesión

- **Estado:** Aceptada
- **Fecha:** 2026-08-14
- **Decide:** JP
- **Complementa:** ADR-010

## Contexto

La autenticación administrativa usa cookie `httpOnly` (ADR-010), lo que obliga a proteger contra CSRF con un token enviado en la cabecera `X-CSRF-Token`.

La entrega 2 lo implementó con un token aleatorio de 32 bytes generado al iniciar sesión, del que solo se almacena el SHA-256 en `core.admin_sessions.csrf_token_hash`. Esa decisión es correcta para el token de sesión y se arrastró al CSRF sin examinarla.

El efecto apareció al documentar la entrega: como el token en claro no se puede recuperar de la base de datos, `GET /api/admin/auth/csrf` no tiene más remedio que **emitir uno nuevo y anular el anterior**. Si el panel se abre en dos pestañas, la segunda invalida el token de la primera y esta empieza a recibir 403 sin haber hecho nada mal. El panel de administración es exactamente el tipo de aplicación que se usa en varias pestañas: la lista de pedidos en una, la ficha de producto en otra.

La mitigación provisional —reintentar una vez ante un 403 de CSRF— funciona, pero convierte el 403 en un estado esperado del frontend. Todo punto que escriba datos tendría que distinguir «403 de CSRF, reintenta» de «403 de verdad, no tienes permiso», y con peticiones cruzadas entre pestañas un solo reintento puede no bastar.

## Decisión

El token CSRF pasa a ser **determinista, derivado de la identidad de la sesión**:

```
claveCsrf = HKDF( core.installation.installation_key, info = "sillar-csrf-v1" )
csrfToken = base64url( HMAC-SHA256( claveCsrf, admin_session_id ) )
```

- El token es constante durante toda la vida de la sesión.
- `GET /api/admin/auth/csrf` recalcula el mismo valor. Es idempotente y no invalida nada.
- `csrf_token_hash` se conserva y se sigue escribiendo al crear la sesión. **Sin migración.**
- La verificación compara el SHA-256 del token recibido contra la fila, en tiempo constante.

## Alternativas evaluadas

**Rotación con reintento (lo implementado).** Coste cero de desarrollo. Se descarta porque traslada al frontend un problema de diseño del backend, y lo hace en el punto más sensible: el manejo de errores de toda operación de escritura.

**Double-submit cookie.** Segunda cookie legible por JavaScript cuyo valor debe coincidir con la cabecera; es lo que hacen Django y Angular. Elimina la carrera y prescinde de la columna en base de datos. Se descarta por dos razones: un subdominio comprometido puede escribir cookies del dominio padre, y SILLAR contempla despliegues con un subdominio por instalación, que es justo el escenario donde esa debilidad deja de ser teórica.

**Guardar el token cifrado de forma reversible.** Permitiría devolver siempre el mismo. Se descarta porque introduce cifrado reversible y gestión de claves para no ganar nada frente al HMAC, que resuelve lo mismo sin almacenar el secreto.

## Razones

- Elimina la carrera entre pestañas por construcción, no por reintento. El token deja de ser un estado mutable y pasa a ser una función de la sesión.
- No añade cookies ni superficie nueva.
- No toca el esquema de datos: la corrección es de generación, no de almacenamiento.
- Derivar la clave de `installation_key` evita un secreto más en configuración y sobrevive a los reinicios, que es lo que hace que las sesiones vivas no se rompan al reiniciar el proceso.

## Consecuencias

**Positivas.** `/csrf` se vuelve idempotente y el frontend puede pedirlo sin coordinación entre pestañas. El 403 vuelve a significar una sola cosa. La corrección cabe en una entrega pequeña y sin migración.

**Negativas.**

- El token CSRF **no se puede rotar dentro de una sesión**. Si se filtrara, la única salida es revocar la sesión. Se acepta: un token CSRF no es un secreto de larga vida, `SameSite=Strict` es una segunda barrera, y `logout` y el cambio de contraseña ya ofrecen esa salida.
- `installation_key` pasa a tener un uso criptográfico además de identificar la instalación. Queda anotado aquí porque afecta a qué se puede hacer con ese valor: **no debe exponerse en ninguna respuesta del API ni rotarse a la ligera.** Rotarla invalida los tokens CSRF de todas las sesiones vivas, que entonces recibirán 403 hasta volver a pedir `/csrf`.
- La etiqueta `"sillar-csrf-v1"` lleva versión a propósito: si algún día hace falta invalidar todos los tokens sin tocar `installation_key`, se sube a `v2`.

## Regla derivada: `installation_key` no sale del servidor

Desde el momento en que esa columna es material criptográfico, deja de ser un identificador que se pueda pasear. La regla, para que no haya que deducirla:

**`installation_key` no sale nunca del servidor.** Ni en una respuesta del API, ni en un registro de log, ni en el frontend, ni en un mensaje de error. Solo la leen el arranque y quien derive de ella.

**Todo uso externo utiliza un valor derivado con su propia etiqueta HKDF**, nunca la clave en bruto y nunca la clave de otro uso:

```
claveCsrf     = HKDF( installation_key, info = "sillar-csrf-v1" )
claveLicencia = HKDF( installation_key, info = "sillar-license-v1" )   ← fase 5
```

El motivo es la separación de dominios criptográficos. Dos usos que comparten clave se comprometen juntos: si el valor que firma las licencias fuera el mismo que valida los tokens CSRF, filtrar uno regalaría el otro. Con etiquetas distintas, el que se filtre no sirve para nada más.

La consecuencia práctica llega en la **fase 5**. Un archivo de licencia firmado necesita identificar la instalación, y la tentación será meter `installation_key` dentro. No se hace: se mete un identificador derivado con la etiqueta de licencia, que identifica igual de bien y no permite reconstruir nada.

Si algún día hace falta un identificador público de la instalación —para soporte, para telemetría, para lo que sea— se añade una columna aparte con ese propósito. Reutilizar esta sería confundir un secreto con un nombre.

## Alcance

Se aplica en la **entrega 2.1** de CORE, antes de F-08, para que el frontend se construya contra la semántica definitiva. El §3 de `ENTREGA-02-AUTENTICACION.md` ya recoge el diseño corregido.
