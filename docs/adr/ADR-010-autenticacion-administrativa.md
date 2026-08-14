# ADR-010 — Autenticación administrativa por cookie httpOnly

- **Estado:** Aceptada
- **Fecha:** 2026-08-14
- **Decide:** JP

## Contexto

El panel de administración lo usa personal del negocio: la dueña, un encargado, quizá un practicante. Gente que no es técnica, que trabaja desde el mostrador y que va a dejar la sesión abierta. Había que decidir cómo se autentica.

Opciones evaluadas: cookie httpOnly de sesión, JWT guardado en el frontend, o un proveedor externo de identidad.

## Decisión

**Cookie httpOnly, `Secure`, `SameSite=Strict`**, con sesión respaldada en base de datos.

## Razones

- Una cookie `httpOnly` no es accesible desde JavaScript. Si algún día se cuela un XSS —y en un panel que muestra contenido editable por el usuario, es un riesgo real— el atacante no puede robar la sesión. Con un token en el frontend, sí.
- El panel y la API viven en el mismo despliegue, así que no hay complicaciones de dominios cruzados. La objeción habitual contra las cookies aquí no aplica.
- La sesión se puede **revocar de verdad**. Con JWT autocontenido, cerrar sesión del lado del servidor obliga a montar una lista de revocación, que es exactamente el estado en base de datos que el JWT pretendía evitar.
- Un proveedor externo añade costo y dependencia por instalación. En un producto que se vende a negocios pequeños, eso es fricción comercial difícil de justificar.

## Diseño

- La sesión vive en `core.admin_sessions`. Se guarda el **hash** del token, nunca el token.
- Duración: 8 horas de inactividad, con renovación deslizante. La jornada de un negocio cabe en una sesión.
- Contraseñas con **BCrypt**, factor de trabajo 12 o más. Nunca en claro, nunca en registros de log.
- Bloqueo temporal tras varios intentos fallidos, contados por cuenta.
- Protección CSRF obligatoria: token por sesión en las peticiones que modifican datos. Es la contrapartida de usar cookies y no es opcional.
- El cierre de sesión revoca la fila; no basta con borrar la cookie del navegador.

## Consecuencias

**Positivas.** Superficie de ataque menor. Revocación inmediata. Se puede listar y cerrar sesiones activas desde el panel, algo que un negocio con personal rotando agradece.

**Negativas.** Cada petición autenticada consulta la sesión en base de datos; con el volumen de un panel de administración es irrelevante, pero es una consulta más. Hay que implementar CSRF correctamente, que es donde suelen aparecer los errores. Y si algún día el frontend se sirve desde otro dominio, habrá que revisar la configuración de la cookie.

## Fuera de alcance por ahora

Segundo factor, recuperación de contraseña por correo, inicio de sesión con proveedores sociales y permisos granulares por módulo. Los roles arrancan con los tres ya definidos —`super_admin`, `admin`, `editor`— y se afinan cuando exista un caso real que lo pida.

**Nota:** esta decisión cubre únicamente a los administradores. La autenticación de clientes finales pertenece al módulo M08 Portal del Cliente y es un problema distinto, con otras restricciones.
