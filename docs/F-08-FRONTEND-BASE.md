# F-08 — Proyecto React base

- **Fase:** 0, fundación · **Estado:** Aprobado · **Versión 2**
- **Depende de:** CORE completo (entregas 1, 2, 2.1, 3 y 3b) · ADR-005 · ADR-010 · ADR-012 · `MARCA.md` §6
- **Sustituye a:** la versión 1, escrita antes de las entregas 2.1 y 3

> **Cambios respecto a la versión 1.** El token CSRF se deriva de `installation_key` (ADR-012), no de un secreto en configuración. Se decide la identidad del panel (`MARCA.md` §6), que era lo que bloqueaba esta entrega. Y se añade la **pantalla de reconexión**, que la decisión de reiniciar el host al activar un módulo (entrega 3 §1) convirtió en obligatoria.

Esta entrega da cara visible a lo construido: el asistente de instalación, el acceso al panel y el esqueleto que sabe qué módulos están activos.

**No incluye** las pantallas de administración de CORE —módulos, usuarios, configuración, auditoría, medios—. Esas son una entrega aparte del módulo, ahora que CORE ya tiene sus veinte rutas.

---

## 1. Stack y estructura

Vite + React + TypeScript, con **pnpm**. Sin librería de componentes de terceros: el sistema de diseño es propio y traerlo hecho obligaría a pelearse con sus tokens.

```
frontend/
├── index.html
├── vite.config.ts
├── package.json
└── src/
    ├── shared/
    │   ├── styles/      tokens.css, base.css
    │   ├── ui/          Button, Input, Field, Alert, Card, Badge, Switch, EmptyState, Spinner
    │   ├── http/        cliente HTTP, errores tipados
    │   └── hooks/       genéricos, sin dependencia de módulos
    ├── layout/          AdminShell, Sidebar, Topbar, PageContainer
    ├── capabilities/    CapabilitiesProvider, useCapability
    ├── session/         SessionProvider, useSession, RequireAuth, RequireRole
    ├── platform/        pantallas del producto: setup, login, reconexión, error
    ├── modules/         vacío por ahora; cada módulo traerá su carpeta y su routes.ts
    └── app/             App.tsx, router, arranque
```

`modules/` nace vacío a propósito. Que exista sin contenido comunica dónde va lo que viene.

---

## 2. Origen único en desarrollo

El frontend corre en `:5173` y el API en `:5080`. **No se resuelve con CORS: se resuelve con el proxy de Vite.**

```ts
server: { proxy: { '/api': 'http://localhost:5080', '/media': 'http://localhost:5080' } }
```

`/media` también, porque la ruta estática de la entrega 3b vive fuera del API y las imágenes tienen que verse en desarrollo.

Así el navegador ve un solo origen, la cookie de sesión viaja sin ceremonia y no hace falta configurar `Access-Control-Allow-Credentials` ni relajar `SameSite`. En producción ambos se sirven detrás del mismo dominio, de modo que desarrollo y producción se comportan igual.

Toda petición usa `credentials: 'same-origin'`.

---

## 3. Identidad del panel

Resuelto en `MARCA.md` §6. Lo que aplica aquí:

- El panel usa **siempre** el tema de SILLAR. El tema del cliente es para la web pública.
- «SILLAR» aparece discreto, en el pie de la barra lateral.
- El **nombre del negocio** va en la barra superior, tomado de `GET /api/settings/public`. Identifica el contexto de trabajo, no la marca del panel.
- El logo del cliente **no entra en el armazón**. Solo aparece dentro de las pantallas donde se gestiona, como un dato más.

Motivo práctico: el panel es lo que se demuestra al vender, y una captura no puede filtrar quién es el cliente.

### Tokens

`shared/styles/tokens.css` se extrae de `docs/sillar-design-system.html`: la escala de piedra, el acento, los semánticos, los radios, el espaciado y las variables de rol, incluido el bloque `[data-theme="dark"]`.

**Ningún componente lleva un color escrito.** Solo variables. Es la regla que sostiene toda la doble identidad.

Contrastes ya validados: texto principal 13.8:1, botón primario 6.1:1, borde de control 4.6:1. Un color nuevo exige verificarlo.

---

## 4. Cliente HTTP

Un único punto por el que pasa todo. Ningún componente llama a `fetch`.

1. Antepone `/api` y envía `credentials: 'same-origin'`.
2. Adjunta `X-CSRF-Token` en todo método distinto de `GET`, `HEAD` y `OPTIONS`, **incluido `multipart`** — subir un archivo es una escritura como cualquier otra.
3. Traduce los errores a tipos, no a cadenas: `Unauthorized`, `Forbidden`, `NotFound`, `Conflict`, `Locked`, `PayloadTooLarge`, `UnsupportedMediaType`, `ValidationFailed`, `Network`.
4. Ante **401**, limpia la sesión en memoria y redirige al login. Sin reintentos: la sesión murió.
5. Ante **423**, expone el momento de desbloqueo.
6. Ante **fallo de red**, distingue «el servidor no responde» de «el servidor respondió mal». Lo primero puede ser un reinicio en curso, y lo lleva la pantalla del §8.

**No hay reintento ante 403.** Con el token derivado de `installation_key`, el valor es estable durante toda la sesión y sobrevive incluso a un reinicio del host. Un 403 ya no significa «el token caducó»: significa que algo va mal de verdad, y esconderlo con un reintento solo retrasaría el diagnóstico.

---

## 5. Arranque de la aplicación

El orden importa y hay que respetarlo:

```
1. GET /api/setup/status
   → setupRequired: true  → asistente de instalación. Fin. No se pide nada más.

2. GET /api/capabilities
   → módulos activos y sus versiones.
   → Si falla, pantalla de error de plataforma. La aplicación NO puede continuar:
     sin saber qué módulos hay, no sabe qué montar.

3. GET /api/admin/auth/me
   → 200 → hay sesión, al panel.
   → 401 → no hay sesión, al login. Es el caso corriente, no un error.
```

El 401 del paso 3 no debe registrarse como fallo ni mostrar nada alarmante. Durante el arranque se muestra un estado de carga sobrio; nunca una pantalla en blanco.

---

## 6. Capacidades

```ts
const { has, modules, version, refresh } = useCapability();
if (has('catalog')) { /* … */ }
```

- Se consultan **una vez** al arrancar y se guardan en memoria.
- `refresh()` existe solo para la pantalla de reconexión del §8. Ninguna otra pantalla lo llama.
- El menú lateral y las secciones se construyen a partir de esta lista. **Nada escrito a mano.** Un módulo inactivo no aparece deshabilitado ni tachado: no aparece.
- Las capacidades son una **guía de presentación**, nunca un control de acceso. La autorización real vive en el backend, siempre.

---

## 7. Sesión

```ts
const { user, login, logout, isAuthenticated } = useSession();
```

- El usuario y el token CSRF viven **en memoria**. Ni `localStorage` ni `sessionStorage`: el token de sesión es una cookie `httpOnly` que JavaScript no debe tocar, y guardar el CSRF en almacenamiento del navegador desharía parte de esa protección.
- Al recargar se recupera con `GET /api/admin/auth/me` y `GET /api/admin/auth/csrf`.
- `RequireAuth` protege el panel; `RequireRole` las rutas con rol mínimo, respetando la jerarquía `super_admin > admin > editor`.
- Sin el rol necesario: pantalla de acceso denegado, no un menú roto.

---

## 8. Reconexión tras reinicio

Esta sección existe por la decisión de la entrega 3: **activar o desactivar un módulo detiene el host y lo relanza el orquestador.** Sin esta pantalla, el usuario ve una petición que falla y una aplicación rota.

### Antes de la operación

El panel **avisa**: la pantalla de módulos advierte que activar o desactivar reiniciará el sistema unos segundos y pide confirmación explícita. Nadie debe descubrirlo cuando ya está pasando.

### Durante

```
1. La petición de activación responde 200 ANTES de que el host se detenga.
2. El frontend entra en estado «reiniciando» y bloquea la interfaz.
3. Sondea GET /api/capabilities con reintentos espaciados: 1s, 2s, 3s, 5s, 5s…
4. Al primer 200: refresh() de capacidades y de la sesión, y vuelta al panel.
5. Pasados 60 segundos sin respuesta: mensaje de fallo con un botón de reintentar
   y la indicación de revisar que el servicio esté supervisado.
```

Reglas:

- El estado de reinicio es **global**, no local a la pantalla de módulos: si el usuario navega, la aplicación sigue sabiendo que el servidor no está.
- Durante el reinicio no se lanza ninguna otra petición. Fallarían todas y llenarían la consola de ruido.
- El mensaje es honesto y concreto: *«Aplicando el cambio. El sistema se está reiniciando, esto tarda unos segundos.»* Nada de barras de progreso falsas.
- **La sesión sobrevive**: vive en base de datos. Y el token CSRF también, porque se deriva de `installation_key` (ADR-012). Con el diseño anterior del CSRF, cada activación habría expulsado a todo el mundo — la decisión de la entrega 2.1 se paga aquí.

---

## 9. Pantallas incluidas

### Asistente de instalación

Una pantalla, tres bloques: datos del negocio, tipo de licencia, primer administrador.

- Los requisitos de la contraseña se muestran **antes** de escribir, no después de fallar. Doce caracteres mínimo.
- Indicador de fuerza y opción de mostrarla.
- Al terminar: mensaje de éxito y redirección al login. **No inicia sesión sola.**
- Si la instalación ya está hecha, redirección al login sin explicaciones raras.

### Acceso

- Correo y contraseña. Nada más.
- Credenciales incorrectas: mensaje único e idéntico en todos los casos, como manda el backend.
- Cuenta bloqueada (423): se muestra hasta cuándo. Es el único mensaje específico, y solo lo ve quien acertó la contraseña.
- Estado de carga en el botón; no se puede enviar dos veces.

### Panel, esqueleto

- `AdminShell` con barra lateral, barra superior y contenedor.
- Barra lateral construida desde las capacidades, con «SILLAR» discreto en el pie.
- Barra superior con el nombre del negocio, el usuario y el cierre de sesión.
- Una pantalla de inicio que muestra el estado de la instalación y los módulos activos. Provisional: la sustituirá la entrega de pantallas de CORE.

### Web pública

Contenedor vacío con un mensaje sobrio. No hay módulos que la construyan todavía; existirá con M02.

---

## 10. Componentes base

Los mínimos para estas pantallas: `Button` con sus variantes y tamaños, `Input`, `Field` con etiqueta, ayuda y error, `Alert`, `Card`, `Badge`, `Switch`, `EmptyState`, `Spinner`.

**No se construye ningún componente que esta entrega no use.** `Table` y los demás llegan con la pantalla que los necesite.

Todos accesibles con teclado, con foco visible, etiquetas asociadas y errores anunciados a lectores de pantalla.

---

## 11. Criterios de aceptación

Leyenda: **[x]** verificado · **[~]** verificado por lectura del código · **[ ]** requiere navegador, pendiente de revisión visual.

**Arranque**

- [~] Con la base recién migrada, se muestra el asistente de instalación
- [~] Completada la instalación, entrar de nuevo lleva al login
- [~] Si `/api/capabilities` falla, se ve una pantalla de error clara, no una en blanco
- [~] Un visitante sin sesión llega al login sin errores en consola

**Sesión**

- [ ] Tras iniciar sesión, recargar **mantiene** la sesión
- [ ] Cerrar sesión devuelve al login y el panel deja de ser accesible
- [x] Ni el token de sesión ni el CSRF aparecen en `localStorage` o `sessionStorage`
- [~] Un 401 en cualquier petición redirige al login sin bucles
- [~] Con la cuenta bloqueada, el login muestra el momento de desbloqueo

**Reconexión**

- [ ] Deteniendo el API a mano, la aplicación entra en estado «reiniciando» y no lanza más peticiones
- [ ] Al volver el API, se recupera sola sin recargar la página
- [ ] **La sesión sigue abierta tras el reinicio**
- [ ] **Una escritura después del reinicio funciona con el mismo token CSRF**
- [ ] Tras 60 segundos sin respuesta, mensaje de fallo con reintento manual

**Capacidades**

- [x] Con solo CORE activo, el menú no muestra entradas de otros módulos
- [x] No hay ninguna entrada de menú escrita a mano en el código

**Multipestaña**

- [ ] Con dos pestañas abiertas, ambas pueden escribir sin 403 de CSRF

**Diseño e identidad**

- [x] Ningún componente contiene un color literal
- [ ] Se ve correcto en tema claro y oscuro
- [~] «SILLAR» aparece en el pie de la barra lateral y el nombre del negocio en la superior
- [ ] Toda la interfaz es navegable con teclado y el foco siempre visible

**General**

- [x] `pnpm build` compila sin advertencias de TypeScript
- [x] `frontend/README.md` explica cómo levantarlo y cómo se compone el menú

---

## 11b. Cierre

- **Compilación:** `pnpm build` limpio. 68 módulos, 251 kB de JavaScript y 11,7 kB de CSS.
- **Dependencias:** React, React DOM, react-router-dom, Vite con su plugin, TypeScript y sus tipos. Ninguna biblioteca de componentes.

### Lo que se comprobó

| Comprobación | Resultado |
|---|---|
| `pnpm build` | Sin advertencias de TypeScript, con `strict` y `noUnusedLocals` |
| Colores literales en componentes | Cero en TSX; cero en `ui.css`, `layout.css`, `platform.css` y `base.css`. Solo `tokens.css` los tiene |
| `localStorage` / `sessionStorage` | Cero usos; las dos apariciones son comentarios que explican por qué no se usan |
| Entradas de menú escritas a mano | Ninguna: `MODULE_NAVIGATION` está vacío y el menú sale de las capacidades |
| Proxy de Vite a `/api` | `GET /api/capabilities` por `:5173` responde 200 con el JSON del backend |
| Proxy de Vite a `/media` | Alcanza la ruta estática (404 del archivo inexistente, no del proxy) |
| **La cookie sobrevive al proxy** | Login por `:5173` → 200, y `GET /me` con esa cookie → 200 con el usuario |

Lo último es lo que importa del §2: confirma que el origen único funciona y que no hará falta CORS.

### Lo que queda por mirar en un navegador

No puedo ver la interfaz, así que los criterios visuales y de interacción quedan sin marcar: tema claro y oscuro, navegación con teclado y foco, el flujo de reconexión en vivo, y las dos pestañas escribiendo a la vez. El código está escrito para cumplirlos —foco visible global, `aria-*` en los campos, `role="alertdialog"` en la superposición— pero eso se comprueba usándolo.

Para el de reconexión: entrar al panel, detener el API con `Ctrl+C`, ver aparecer la superposición, y relanzarlo.

### Decisiones tomadas durante la implementación

1. **Al recuperar la conexión se recarga la página entera**, en lugar de refrescar los proveedores en memoria. Tras un reinicio del host es lo más honesto: capacidades, sesión y configuración se releen de una vez y ningún estado viejo sobrevive al cambio que se acaba de hacer. El §8 pide `refresh()`; recargar lo cumple y además no deja rincones sin actualizar.
2. **`connection.ts` no importa React.** El almacén es un objeto con suscriptores, y `Wiring` conecta lo que solo React sabe hacer —navegar, recargar—. Es lo que permite que el cliente HTTP consulte el estado sin depender del árbol de componentes.
3. **`pnpm-workspace.yaml` con `allowBuilds: esbuild`.** pnpm 11 bloquea los scripts de instalación, que es lo correcto, y esbuild necesita el suyo para colocar su binario. Se autoriza ese y ninguno más.
4. Los requisitos de contraseña se reflejan en `platform/password.ts`, marcado como reflejo y no como segunda implementación: quien decide si una contraseña vale sigue siendo el backend.

---

## 12. Fuera de alcance

| Qué | Cuándo |
|---|---|
| Pantallas de módulos, usuarios, configuración, auditoría y medios | Entrega de pantallas de CORE, después de F-08 |
| Web pública con contenido | M02 Contenido Web |
| Marca blanca del panel | Fuera de alcance por decisión (`MARCA.md` §6) |
| Internacionalización | No prevista: el producto es en español |
| Pruebas de interfaz automatizadas | Cuando haya pantallas estables que merezcan protegerse |
| Conmutador de tema oscuro para el usuario | Los tokens ya lo soportan; llega con la pantalla de configuración |
