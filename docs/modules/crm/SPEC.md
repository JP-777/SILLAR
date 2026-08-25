# SPEC — M04 Clientes y Contacto

- **Código:** `crm`
- **Schema:** `crm`
- **Versión:** 1.0.0
- **Estado:** Borrador · listo para construir
- **Fase:** MVP · segundo módulo del árbol comercial

> **El código y el schema son la misma palabra**, como en `core` y en `catalog`. El borrador
> inicial proponía código `customers` y schema `crm`; se unificó porque **el código viaja** —a
> `/api/capabilities`, a las rutas `/api/admin/<código>/…`, a `HOME_SECTIONS`— y hoy nadie tiene
> que preguntarse si los dos coinciden. Romper esa regularidad no falla: a partir de ese día hay
> que mirarlo cada vez. El nombre legible va en el título del módulo, «Clientes», que es el que ve
> una persona.

---

## 1. Qué es M04, y qué no es

**Es el dueño del cliente**: quién es, cómo se le contacta, dónde se le entrega, y **su cuenta para
entrar a la tienda**.

**No es** pedidos —eso es M03—, ni facturación, ni catálogo.

**Y no toca `core.admin_users`.** El personal y la clientela son dos poblaciones distintas y no se
mezclan.

## 2. La regla que gobierna el módulo entero

> **M04 tiene su propia identidad, sus propias sesiones y su propio esquema. No reutiliza nada de
> la identidad de CORE.**

Va escrito aquí porque **lo contrario va a parecer la opción barata durante todo el módulo**. El
dato que lo cierra está medido, no supuesto:

> Hoy lo único que impediría a un cliente entrar al panel sería **no tener uno de los tres roles de
> administración**, y lo que garantiza eso es un `CHECK` de base de datos
> (`ck_admin_users_role`, `AdminUserConfiguration.cs:14`). **Una barrera de una sola capa, en el
> sitio más lejano posible del código que autoriza.**

Con identidad separada ese agujero no llega a existir. No es preferencia de diseño: es la razón de
ser de esta decisión.

### Lo que sí se reutiliza, y por qué se puede

**`SessionTokens`**, que vive en `Sillar.Core.Contracts` desde que M01 fue su segundo caso real. Es
criptografía pura —crear un testigo, calcular su SHA-256, compararlo en tiempo fijo— **sin ninguna
tabla detrás**. Usarlo no ata a M04 a nada de CORE.

**Lo que no se reutiliza** es `SessionAuthenticationHandler`, que es interno a `Sillar.Core` y lee
`core.admin_sessions`. M04 tiene el suyo, contra `crm.customer_sessions`.

## 3. Dos cookies, dos esquemas, y es la capa que faltaba

**Un cliente y un administrador pueden tener sesión a la vez en el mismo navegador, y es el caso
normal**: el dueño enseñando su propia web con el panel abierto, alguien del personal comprobando
cómo se ve una ficha desde fuera. Con una sola cookie, entrar en la tienda **cerraría el panel** —
y peor, parecería que se cayó la sesión.

Pero el argumento de fondo es el del §2:

> **La cookie separada es la segunda capa.** Con dos esquemas de autenticación registrados, **la
> tienda no acepta la credencial del panel y el panel no acepta la del cliente**, y eso lo decide
> el mecanismo, no un dato en una tabla.

**Cada grupo de rutas acepta solo su esquema.** Y se afirma con prueba: **una petición al panel con
la cookie de cliente tiene que ser rechazada.**

### El renombrado de la cookie del personal

`sillar_session` no dice de quién es. Las dos pasan a decirlo, y **nacen a la vez** para que nadie
tenga que deducirlo:

| | Cookie |
|---|---|
| Personal | `sillar_panel` |
| Clientela | `sillar_tienda` |

**Radio del cambio, comprobado antes de decidirlo:** la constante en `SessionCookie.cs:9`, un
literal en `scripts/demo/seed-demo.mjs:58`, y dos líneas de documentación
(`backend/README.md:289`, `docs/modules/core/ENTREGA-02-AUTENTICACION.md:61`). El arnés `e2e/` no
la menciona, y el frontend no puede: es `httpOnly`. **Cierra la sesión de todo el mundo una vez, y
eso es gratis.**

Las dos conservan lo que ya no se discute: `HttpOnly`, `Secure`, `SameSite=Strict`, sin `Max-Age`,
y en la base **solo el SHA-256 del testigo, nunca el original**, con su hash CSRF al lado.

## 4. La distinción que estructura los datos: ficha y cuenta no son lo mismo

Hay dos altas: **el cliente se registra en la web**, y **el personal crea una ficha** para quien
compra en el mostrador o llama por teléfono. La forma de que eso no sea un lío es no confundir dos
cosas:

| | Qué es | Cómo nace |
|---|---|---|
| **La ficha** | Los datos de una persona: nombre, contacto, documento, direcciones, notas | La crea el personal, **o** la crea el registro |
| **La cuenta** | Credenciales para entrar a la tienda | **Solo** el registro propio, o una invitación |

**Una ficha puede existir sin cuenta.** Es el cliente del mostrador: el negocio sabe quién es y no
tiene por qué tener contraseña. **Una cuenta no puede existir sin ficha.**

**Si alguien se registra con un correo que ya tiene ficha sin cuenta, se enlaza a la existente**, no
se crea una segunda. **Y esa unión lleva prueba**, porque es el caso que en producción ocurre antes
de lo que nadie espera.

**El personal no crea contraseñas.** Si quiere dar acceso a una ficha, **invita**: enlace de un
solo uso, con caducidad, que la persona usa para poner la suya. Nadie teclea la contraseña de otro
ni se la dicta por teléfono.

---

## 5. Los datos

### `crm.customers` — la ficha

**Replica** (ADR-018): clave **`uuid` v7 generada por la aplicación**, más `origin_node` y
`row_version`.

> **Prohibido referenciar a `core.admin_users`**, aunque sea lo primero que uno escribiría para
> guardar quién dio de alta a un cliente. `admin_users` **no replica** —comprobado: en `core` solo
> replica `media_assets`— y una tabla que viaja no puede referenciar a una que se queda. Es el caso
> exacto que la ADR-018 nombra: **no salta ninguna violación de clave foránea**, porque cada base es
> coherente por dentro; el síntoma aparece después y en otro sitio.
>
> **Si hace falta saber quién la creó, se guarda una instantánea del nombre, no una clave foránea.**

Lleva: nombre, correo, teléfono, **documento opcional** —tipo y número, DNI o RUC—, `is_active`,
las marcas de tiempo, y **las notas internas del negocio**.

> **Las notas no salen nunca.** Ni en la tienda, ni en ningún endpoint público, ni en el propio
> perfil del cliente. **Y se afirma con una prueba, no con un comentario:** una nota interna que se
> filtra es de las cosas que cuestan un cliente.

El correo es único ignorando mayúsculas y respetando tildes: **colación `core.es_ci`**, la misma
que ya usa la identidad en el resto del producto. **No `core.es_search`**, que ignora tildes: con
ella `josé@` y `jose@` serían la misma cuenta, y eso no es un fallo de búsqueda — son dos personas
compartiendo acceso.

> **La colación se ocupa de la caja y de la forma Unicode; del resto no se ocupa
> nadie.** Con `es_ci` el índice no distingue mayúsculas, y **tampoco distingue la
> misma tilde escrita de dos formas** —`é` como un carácter o como `e` más acento
> combinado—: ICU normaliza al construir la clave de colación, así que las dos
> formas chocan en el índice único. *Comprobado por efecto contra la colación real
> del proyecto: 11 bytes contra 12, y el índice rechaza la segunda.*
>
> **Lo que sí distingue es el espacio**, que los teclados de móvil pegan solos:
> `a@x.com` y `a@x.com ` son dos correos para el índice, y se ven idénticos.
>
> **Se normaliza al guardar igualmente: recortar y unificar la forma Unicode**
> (`Normalize(NormalizationForm.FormC)`). El recorte es una garantía; la
> normalización a NFC ya no lo es, pero se mantiene porque **la base almacena la
> última forma escrita, no la canónica** —un `UPDATE` con otra forma equivalente
> cambia los bytes guardados sin que la colación lo considere un cambio— y sin ella
> el valor almacenado depende de qué teclado lo escribió.

### `crm.customer_addresses` — las direcciones de entrega

**Replican, con las mismas reglas.** No es preferencia: **si la ficha viaja y la dirección no, la
referencia se queda apuntando a otra cosa** sin que salte ninguna violación, y el síntoma sería el
mismo que el del catálogo sin fotos — un cliente sin direcciones en el otro nodo.

Una puede ser la preferida.

### `crm.contact_messages` — mensajes del formulario de contacto

M04 también es dueño de la captación del formulario de contacto, como establece
`ARQUITECTURA_MODULAR.md`. Esta tabla es local a WEB y **no replica**: la ficha del
cliente forma parte de los datos compartidos, pero la captación pertenece a lo
que ADR-017 deja exclusivamente del lado WEB.

Un mensaje puede existir sin ficha, por lo que `customer_id` es opcional. Si se
conoce o posteriormente se vincula a una ficha, la FK interna apunta a
`crm.customers`. El mensaje conserva además el nombre y los medios de contacto
recibidos en el formulario como datos propios: no depende de que la ficha cambie.

Lleva nombre, correo opcional, teléfono opcional, asunto opcional, mensaje,
baja lógica y marcas de tiempo. Debe existir al menos correo o teléfono.

No lleva estado de atención en esta primera entrega: lectura, asignación o
resolución pertenecen al comportamiento de la bandeja y se decidirán cuando
exista esa pantalla, no dentro del esquema por anticipado.

### `crm.customer_sessions` — las sesiones

**No replican**, igual que las del personal: una sesión es de la máquina donde se abrió.

### `crm.customer_tokens` — invitaciones, verificación y recuperación

**No replican.** Un solo uso, con caducidad, y **solo el hash del testigo en la base**. Un token que
se guarda en claro es un enlace de restablecimiento guardado en claro.

> **Y esa asimetría hay que declararla, no dejarla en pie sin nombre:** *el testigo es del nodo que
> lo emitió; la ficha viaja, el enlace no.* Hoy no molesta —cada instalación tiene su dirección—
> pero **es exactamente la clase de accidente que parece una garantía** hasta el día que alguien
> monte dos nodos detrás del mismo nombre y un enlace de recuperación deje de valer según a qué
> máquina caiga la petición.

### Reglas transversales

- **Contraseñas con BCrypt, factor 12 o más.** Nunca en registros ni en respuestas.
- **Restablecer la contraseña cierra todas las sesiones abiertas de ese cliente.** Es lo que
  convierte «recuperar» en recuperar de verdad: si alguien entró con la contraseña vieja, cambiarla
  tiene que echarlo. **Cuesta una línea ahora y es incómodo después**, porque habría que decidirlo
  con sesiones vivas delante.
- **Baja lógica siempre.** Un cliente dado de baja conserva su ficha, y los pedidos de M03 la
  conservan por instantánea aunque la ficha cambie después.
- `timestamptz`, `CHECK` para reglas de valor, `created_at`/`updated_at` con su trigger.

---

## 6. El acceso público no se protege como el del panel

`LockoutPolicy` bloquea **cinco intentos por cuenta durante quince minutos**, sin nada por IP
(`LockoutPolicy.cs`), y su propio comentario explica por qué no cuenta por IP: en un local todo el
personal comparte la salida a internet. **Es sensato para cinco personas. En un formulario público
es un arma:** cinco intentos contra el correo de otro y le cierras la cuenta un cuarto de hora.

M04 necesita otra política:

- **Nada de bloqueo duro por cuenta.** **Espera creciente** tras cada fallo: frena a quien prueba y
  no deja a nadie fuera.
- **Límite por IP además del de cuenta.** La clientela también comparte salida —un colegio, un
  locutorio—, así que el límite por IP tiene que ser generoso y **no puede ser el único**.
- **La respuesta no cambia según exista la cuenta o no.** Ni al entrar, ni al registrarse, ni al
  recuperar. Si cambia, cualquiera averigua qué correos están registrados probándolos.

> **Y el tiempo también responde.** Si el camino «no existe» tarda 5 ms y el camino «existe pero la
> contraseña falla» tarda los ~300 ms del BCrypt, **el reloj dice lo que el mensaje calla.** El
> camino de la cuenta inexistente tiene que costar lo mismo.

---

## 6 bis. El envío de correo

M04 es el primero que usa la capacidad nueva de CORE. **Verificación al registrarse** y
**recuperación de contraseña**, las dos con enlaces de un solo uso y caducidad corta.

**Sin verificar se puede entrar y mirar. Comprar, no** — eso lo exigirá M03, y M04 se lo dice por
contrato. El motivo es concreto: si el correo no está verificado, **el aviso de su pedido no llega
a ninguna parte**.

### El paquete y la configuración

**MailKit**, séptimo paquete en `backend/Directory.Packages.props`, con el motivo escrito al lado:
el `SmtpClient` de la BCL está obsoleto y no lleva bien STARTTLS moderno. **Aprobado por regla 2.**

**Servidor, puerto y remitente en `core.site_settings`.** Son configuración del negocio.

**La contraseña, por entorno. Nunca en la base.** `is_public=false` la escondería del sitio público
**pero no del panel**: cualquiera que abriera Configuración la vería. Y un valor en claro en una
tabla es un valor en claro en cada volcado.

> **La pantalla de Configuración no puede callarlo.** Si enseña servidor, puerto y remitente y no
> hay campo de contraseña, el primero que llegue pensará que falta algo. **Que diga que vive en el
> entorno, con el nombre exacto de la variable.** Un hueco explicado es configuración; un hueco
> mudo es un error que alguien va a intentar arreglar.

**Lo descartado, con su condición para volver:** cifrar la contraseña con una clave de instalación
**no es mala idea, es prematura** —resuelve que el panel pueda ponerla sin que un volcado la
delate, pero exige gestión de claves, y hoy instalar ya es un paso técnico con `.env`—. **El día
que SILLAR tenga un instalador para alguien que no abre un terminal, ésa es la salida buena.**

### Quién puede cambiarlos: `super_admin`, los tres

Hoy `SettingsEndpoints.cs:28` exige `admin` para editar cualquier ajuste, y `super_admin` solo para
cambiar la visibilidad pública (`SiteSettingService.cs:95`). **El grupo de correo entero —servidor,
puerto y remitente— exige `super_admin`.**

La razón no es que el remitente sea la cara del negocio, que también:

> **Quien nombra el servidor recibe la contraseña.** El cliente SMTP se autentica **contra el
> servidor que diga el ajuste**, con el secreto que sale del entorno. Un `admin` que apunte el
> servidor a una máquina suya **no rompe el envío: se lleva la credencial.**

El precedente encaja: el producto ya distingue «editar un valor» de «cambiar el alcance de un
valor». **Esto es lo mismo un grado más arriba.**

**Y la auditoría de estos tres guarda el valor anterior y el nuevo.** En un ajuste normal basta
saber que alguien lo tocó; aquí **el valor anterior es la prueba de a dónde se dejó de enviar**.

### El correo de prueba

**Un remitente que el servidor no autoriza no falla al guardarlo: falla después**, cuando alguien
esperaba su enlace de recuperación. Un servidor mal escrito, igual.

> **La pantalla ofrece enviar un correo de prueba, y el resultado se ve ahí mismo.** Un ajuste de
> correo que no se ha ejercitado es una suposición.

**No es obligatorio para guardar** —a veces se configura antes de que el servidor exista— pero
**queda escrito si nunca se probó**, porque ése es el estado que engaña.

### Cuando el envío falla

**Enviar no es parte de la transacción.** El correo sale después de que el hecho ocurra; si falla se
registra y se sigue. **Un SMTP caído no puede impedir que alguien se registre.** El precedente está
en el bus de eventos: *un manejador roto no puede deshacer lo que ya ocurrió*.

**Sin cola y sin reintentos**, y **declarado con esas palabras en el `<remarks>` del contrato**, en
`Sillar.Core.Contracts` —no en la implementación—, que es donde mira quien vaya a construir encima.
Es lo que hace que el `<remarks>` de `IEventPublisher` le sirva hoy a M02. **Declarar lo que no se
promete vale tanto como declarar lo que sí:** el día que haga falta una cola, que quien la escriba
sepa que nadie prometió lo contrario.

**Al visitante, siempre la misma respuesta**, que no afirma el envío ni la existencia de la cuenta.
Es la otra mitad del §6: **que el servidor SMTP acepte el mensaje tampoco es que haya llegado**, así
que «te hemos enviado un enlace» es una promesa que no podemos cumplir.

**El fallo se le enseña a quien administra**, que es el único que puede hacer algo, y es como el
negocio se entera de que su correo lleva tres días sin salir.

**El rastro va a la auditoría, sin contenido:** destinatario, tipo, resultado y momento. **El cuerpo
no** — ahí viajan enlaces de restablecimiento, y un registro que guarda uno es una puerta abierta
con fecha.

---

## 7. Lo que M04 le da a los demás

Un contrato en `Sillar.Modules.Crm.Contracts`, escrito para el uso que ya sabemos que viene, que es
M03:

- **Quién es el cliente de esta sesión.**
- **Los datos de un cliente para poner en un pedido** —nombre, documento y dirección elegida—
  sabiendo que M03 **guardará instantánea**: un pedido conserva a dónde se envió aunque el cliente
  se mude después.
- **Si su correo está verificado**, que es lo que decide si puede comprar.

> **Este contrato no está cerrado hasta que M03 lo estrene.** En M01, mirarlo desde fuera dio dos
> carencias; **usarlo dio cuatro más** —precio, publicación, categoría y la baja—, ninguna prevista.
> Un contrato no se cierra: se estrena.

## 8. Las pantallas

**En la tienda:** registrarse, entrar, recuperar, y el perfil —datos, direcciones, y más adelante
sus pedidos.

**En el panel:** la lista de clientes con búsqueda, y la ficha con datos de contacto y direcciones,
notas internas, **estado de la cuenta** —activa, invitada sin usar, bloqueada, de baja, con desde
cuándo— **y un hueco para el historial de pedidos que M03 rellenará**.

> **Ese hueco se declara, no se improvisa.** Hoy dice que no hay módulo de pedidos instalado, y lo
> hace **pidiendo el contrato al contenedor y comprobando si vino** —no preguntando al registro de
> módulos—. Es la decisión escrita en el `<remarks>` de `IModuleRegistry`, y su primer uso fuera de
> M02: **el registro responde según la foto de las activaciones; el contenedor, según lo que de
> verdad se puede llamar.**

Y lo de siempre: los cuatro estados de cada pantalla, ningún color escrito a mano, teclado con foco
visible, `axe` limpio en los dos temas y con movimiento reducido, y con M04 desactivado ni entrada
de menú ni ruta muerta.

---

## 9. Criterios de cierre

Se cierra cuando **todos** se pueden enseñar funcionando, no descritos:

- [ ] Un visitante se registra, recibe el correo de verificación y verifica
- [ ] Un cliente sin verificar entra y ve su perfil; el estado dice que le falta verificar
- [ ] Recuperar la contraseña funciona, y **el enlace no sirve dos veces**
- [ ] Se entra desde dos navegadores, se restablece en uno **y el otro deja de valer**
- [ ] Registrarse con el correo de otra cuenta **escrito con un espacio al final** choca con el
      índice único, no crea una segunda
- [ ] La respuesta de recuperar **es idéntica** con un correo registrado y con uno que no existe
- [ ] Los intentos fallidos **retrasan sin bloquear**, y una cuenta ajena no se puede dejar fuera
- [ ] El personal crea una ficha sin cuenta e **invita**; la persona pone su contraseña con el enlace
- [ ] Alguien se registra con el correo de una ficha existente **y se enlaza a ella**, sin duplicar
- [ ] Un cliente añade dos direcciones y cambia la preferida
- [ ] Un cliente de baja **sigue en la ficha** y no puede entrar
- [ ] **Las notas internas no aparecen en ninguna respuesta pública** — afirmado por prueba
- [ ] Ninguna contraseña aparece en registros ni en respuestas — afirmado por prueba
- [ ] **Una petición al panel con la cookie de cliente es rechazada** — afirmado por prueba
- [ ] Con el módulo de pedidos ausente, la ficha **enseña su hueco explicado** y nada falla
- [ ] Se desinstala M04 y **CORE y el catálogo siguen enteros**; se reinstala y arranca
- [ ] Sin la capacidad de correo configurada, **registrarse sigue funcionando** y quien administra
      ve que el envío falló
- [ ] Todos los endpoints en Swagger, con ejemplos copiables
- [ ] **La puerta pasa dos veces seguidas** (`node scripts/verificar.mjs`)

---

## 10. Orden de construcción

**Por el esquema y la identidad, no por las pantallas.** Ahí es donde un error cuesta una
migración, y las tres decisiones que la cambian ya están tomadas: el código `crm`, las dos cookies
con sus dos esquemas, y las direcciones replicando.
