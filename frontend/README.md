# Frontend

Panel de administración y web pública de SILLAR. React + TypeScript + Vite, con
**pnpm** (nunca npm).

## Levantarlo

```bash
pnpm install
pnpm dev        # http://localhost:5173
```

Hace falta el API corriendo en `:5080`. Si lo tienes en otro puerto:

```bash
SILLAR_API_ORIGIN=http://localhost:5081 pnpm dev
```

```bash
pnpm build      # comprueba tipos y compila a dist/
pnpm typecheck  # solo los tipos
```

## Un solo origen, sin CORS

El frontend corre en `:5173` y el API en `:5080`. **Eso no se resuelve con CORS:
se resuelve con el proxy de Vite**, que reenvía `/api` y `/media` al backend.

Así el navegador ve un único origen, la cookie de sesión viaja sin ceremonia y no
hay que relajar `SameSite` ni configurar `Access-Control-Allow-Credentials`. En
producción ambos se sirven tras el mismo dominio, de modo que desarrollo y
producción se comportan igual.

`/media` tiene su propia entrada porque la ruta estática vive fuera del API
(ADR-011); sin ella, las imágenes no se verían en desarrollo.

## Estructura

```
src/
├── shared/styles/   tokens.css, base.css
├── shared/ui/       componentes base
├── shared/http/     cliente, errores tipados, estado de conexión
├── layout/          armazón del panel
├── capabilities/    qué módulos están activos
├── session/         sesión, guardas por rol
├── platform/        pantallas del producto: instalación, acceso, reconexión
├── modules/         vacío: aquí van los módulos cuando existan
└── app/             composición y arranque
```

## De dónde sale el menú

**De `GET /api/capabilities`. No hay ninguna entrada escrita a mano.**

Cada módulo exportará su navegación en `routes.ts` y se registrará en
`layout/navigation.ts`. La aplicación muestra solo la de los módulos activos, y
filtra además por rol. Un módulo inactivo no aparece deshabilitado ni tachado:
no aparece.

Hoy `MODULE_NAVIGATION` está vacío porque no hay ningún módulo con interfaz. Es
lo correcto: el panel enseña «Inicio» y nada más.

Las capacidades son una **guía de presentación**, nunca un control de acceso.
Quien las manipule en el navegador solo consigue ver un menú que no lleva a
ninguna parte: la autorización real vive en el backend.

## Colores

**Ningún componente escribe un color.** Solo variables de rol, definidas en
`shared/styles/tokens.css` y extraídas de `docs/sillar-design-system.html`.

Es la regla que sostiene la doble identidad: el panel siempre con el tema de
SILLAR, la web pública con el del cliente, sin tocar un solo componente.
Comprobarlo es buscar `#` fuera de `tokens.css`.

El panel usa siempre el tema de SILLAR (`MARCA.md` §6): «SILLAR» discreto en el
pie de la barra lateral, el nombre del negocio en la superior. El logo del
cliente no entra en el armazón.

## Sesión

El usuario y el token CSRF viven **en memoria**. Ni `localStorage` ni
`sessionStorage`: el token de sesión es una cookie `httpOnly` que JavaScript no
debe tocar, y guardar el CSRF en el almacenamiento desharía parte de esa
protección — un XSS que no puede leer la cookie sí leería el almacenamiento.

Al recargar se recuperan con `GET /api/admin/auth/me` y `GET /api/admin/auth/csrf`.

**No hay reintento ante un 403.** Desde la entrega 2.1 el token CSRF se deriva de
la sesión y es estable, así que un 403 ya no significa «el token caducó»:
significa que algo va mal de verdad, y esconderlo con un reintento solo retrasa
el diagnóstico.

## Reconexión tras un reinicio

Activar o desactivar un módulo **detiene el host** y lo relanza el orquestador
(entrega 3 §1). Sin esto, el usuario vería una petición que falla y una
aplicación rota.

El estado de la conexión vive en `shared/http/connection.ts`, **fuera de React**.
Eso es deliberado y compra tres cosas:

1. El cliente HTTP lo consulta antes de cada envío, así que durante el reinicio
   no sale ninguna petición. No depende de que ninguna pantalla se acuerde.
2. Si el usuario navega, el estado sigue ahí: nunca estuvo montado en una
   pantalla que pudiera desmontarse.
3. Cualquier fallo de red, venga de donde venga, entra por el mismo sitio.

`ReconnectingOverlay` se monta **una sola vez, en la raíz**. Sondea
`/api/capabilities` con esperas de 1, 2, 3, 5 y 5 segundos, y a los 60 pide
intervención. Al volver el servidor recarga la página: tras un reinicio del host
es lo más honesto, porque ningún estado viejo sobrevive al cambio recién hecho.

**La sesión sobrevive**: vive en base de datos, y el token CSRF también, porque
se deriva de `installation_key` (ADR-012). Con el diseño anterior del CSRF, cada
activación habría expulsado a todo el mundo.

## Añadir un módulo

1. Carpeta en `src/modules/<código>/` con `components/`, `pages/`, `services/`,
   `types/` y `routes.ts`.
2. Registrar su navegación en `layout/navigation.ts` y sus rutas en
   `app/routes.tsx`, ambas condicionadas al módulo activo.
3. Nada de `fetch` suelto: la capa de servicios del módulo, sobre `shared/http`.
4. Un módulo **nunca importa** de otro módulo. Lo compartido vive en `shared/`.
