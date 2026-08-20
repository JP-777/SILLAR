# SILLAR · Arnés end-to-end

Prueba el sistema entero —backend y frontend juntos— contra un stack docker
efímero que la propia suite levanta, siembra, instala y destruye. Ningún
`page.route()` sustituye al servidor real salvo que la prueba lo diga
explícitamente (y siempre para simular una condición de carrera, nunca para
evitar tocar el backend). El objetivo, tal como se pidió: poder verificar la
interfaz sin depender de que alguien la mire a mano, y que corra sin
supervisión en cada cambio.

Este documento nació como traspaso entre dos chats. Desde el 18 de agosto hay
uno solo (`docs/PROTOCOLO-DOS-CHATS.md`, retirado), así que ya no traspasa
nada — se queda porque sigue siendo lo único que explica el arnés de un
vistazo a quien no lo construyó.

## Qué hace falta tener antes de correrlo

- **Docker Desktop corriendo.** La suite levanta su propio stack (`sillar_e2e`:
  base de datos + API), completamente aparte de `sillar_dev` — otro nombre de
  proyecto, otros puertos (55432/55081/55173), otro volumen. Los dos pueden
  estar arriba a la vez sin pisarse.
- **`dotnet ef` instalado** (`dotnet tool install --global dotnet-ef` si no lo
  tienes) — lo usa `setup/migrate.ts` para aplicar las migraciones de CORE y
  Catalog contra la base efímera.
- **Dependencias instaladas**: `pnpm install` en `e2e/` y en `frontend/` (la
  suite arranca el Vite de `frontend/` como su propio servidor; no lo trae
  consigo).
- Los navegadores de Playwright: `pnpm exec playwright install chromium` si es
  la primera vez en esta máquina.

Lo que la suite **NO necesita** que tengas levantado: ni `docker compose up`
de desarrollo, ni la API, ni el frontend. Todo eso lo levanta ella —ver
«Qué levanta la suite sola» más abajo— y lo destruye al terminar, éxito o
fallo.

## Cómo correr

```bash
cd e2e
pnpm test                    # la suite entera
pnpm test tests/modulos.spec.ts   # un solo archivo de specs
pnpm test -g "El 409 al desactivar"   # una sola prueba, por nombre
pnpm test:headed              # con el navegador visible, útil para depurar
pnpm report                   # abre el último reporte HTML de Playwright

# Deja el stack EN PIE al terminar, para mirar un fallo en vez de
# reproducirlo. No se activa sola al fallar: globalTeardown no recibe los
# resultados, y adivinarlo dejaría stacks vivos sin que nadie lo pidiera.
E2E_KEEP_STACK=1 pnpm test    # bash;  en PowerShell: $env:E2E_KEEP_STACK=1
pnpm stack:down               # y esto lo tira después
```

Cada corrida completa —incluida construir la imagen Docker en Debug— tarda
entre 40 y 60 segundos con la imagen ya en caché de Docker; bastante más la
primera vez, mientras Docker descarga las imágenes base del SDK y del
runtime de .NET.

## Inventario, un archivo por línea

### Configuración y entorno
- **`.env.e2e`** — el único `.env` de este repositorio que se versiona: nombre
  de proyecto docker, puertos, credenciales de una base efímera que no guarda
  nada entre corridas. Explica en su propia cabecera por qué está commiteado.
- **`playwright.config.ts`** — un solo worker (el stack es compartido y con
  estado), `globalSetup`/`globalTeardown`, y el `webServer` que arranca el
  Vite de `frontend/` apuntado a la API del stack e2e vía `SILLAR_API_ORIGIN`.
- **`tsconfig.json`** — `lib` incluye `DOM` (los fixtures llaman
  `document.documentElement` dentro de `page.evaluate`).
- **`package.json`** / **`pnpm-lock.yaml`** — dependencias: `@playwright/test`,
  `@axe-core/playwright`, `typescript`, `@types/node`. Nada de frontend ni de
  backend entra aquí.

### `setup/` — todo lo que levanta y destruye el entorno
- **`env.ts`** — lee `.env.e2e` a mano y exporta lo que Node necesita fuera de
  Docker: `API_URL`, `FRONTEND_URL`, `CONNECTION_STRING`, rutas.
- **`shell.ts`** — `run()`/`runCapture()`/`sleep()`: los tres primitivos sobre
  los que está escrito todo lo demás en `setup/`.
- **`docker.ts`** — `composeUpDb`, `composeBuildAndUpApi` (perfil `full`,
  `--build`, imagen `sillar_e2e-api` en Debug con los módulos de demostración),
  `composeDown`, `composeExec`, `waitDbHealthy`.
- **`migrate.ts`** — `migrate()` aplica las migraciones de CORE y Catalog
  (`dotnet ef database update`) contra la base efímera; `seed()` corre los dos
  `02_seed.sql` del producto, sin datos de negocio.
- **`api.ts`** — credenciales `E2E_ADMIN`, y las llamadas HTTP crudas del
  arranque: `waitApiReady`, `completeSetup`, `login` (reintenta mientras la
  respuesta sea 404: es la señal de que todavía responde el proceso viejo, en
  modo instalación, que nunca montó `/api/admin/auth/login`), `activateModule`.
- **`global-setup.ts`** — el guion completo: stack abajo por si quedó uno
  vivo, base de datos arriba, migraciones, seeds, imagen Debug arriba,
  instalación, y el grafo de módulos de mentira montado (`demo_catalog` y
  `demo_sales` activos; el resto se deja tal como nace, para tener las cuatro
  variantes de tarjeta sin que ninguna prueba tenga que montarlas ella misma).
- **`global-teardown.ts`** — construye la galería de capturas y tira el stack
  entero (`down -v`). Playwright lo garantiza incluso si algo falló.
- **`gallery.ts`** — recorre `screenshots/<spec>/*.png`, empareja
  `--claro`/`--oscuro` por nombre de paso, y escribe `screenshots/index.html`.

### `fixtures/` — lo transversal a cualquier prueba
- **`base.ts`** — el `test` que hay que importar en vez del de
  `@playwright/test`: falla si la consola tuvo cualquier error, sin
  excepción por defecto. Exporta `duringExpectedOutage(page, fn)`, la única
  válvula: pausa esa vigilancia mientras dura `fn`, para las dos situaciones
  donde un fallo de red es la prueba en sí, no un defecto — el reinicio real
  del contenedor y un 409 provocado a propósito. Fuera de esas ventanas el
  cero sigue siendo cero.
- **`themes.ts`** — `themeRecorder(page, spec)`: por cada paso relevante,
  fuerza el tema (sin transiciones CSS de por medio, para que axe no
  fotografíe un color a mitad de camino), corre axe-core, cierra con una
  captura, y siempre vuelve a claro antes de devolver el control. Fuerza el
  atributo directamente, sin pasar por el interruptor real ni por
  `prefers-color-scheme` — para eso está `tema.spec.ts`.
- **`auth.ts`** — `loginAsE2eAdmin(page)`: abre sesión por API, sin pasar por
  el formulario. Lo comparten los dos specs.

### `tests/` — 24 pruebas, todas las secciones de `VERIFICACION-VISUAL-CORE.md` salvo tres juicios humanos
- **`modulos.spec.ts`** — seis pruebas, los criterios `[ ]` "requiere
  navegador" de
  `docs/modules/core/ENTREGA-04A-PANTALLAS-MODULOS-USUARIOS.md` §4: las
  cuatro variantes de tarjeta, CORE sin interruptor (no uno deshabilitado),
  el enlace de una tarjeta bloqueada a quien la bloquea, el diálogo de
  confirmación diciendo la verdad sobre el reinicio (los dos casos, real y
  simulado), el 409 al desactivar, y el ciclo completo de activación con
  reinicio de contenedor de verdad.
- **`tema.spec.ts`** — una prueba: con el sistema operativo en oscuro
  (`page.emulateMedia`), elegir tema claro con el interruptor real tiene que
  dejar la interfaz clara de verdad, no solo el atributo `data-theme` puesto
  — se comprueba el color de fondo computado. Guarda el aviso del equipo de
  diseño sobre la cascada de tres niveles de `tokens.css`.
- **`transversal.spec.ts`** — lo que vale en todas las pantallas a la vez:
  ninguna dice «Ha ocurrido un error», ningún botón se llama «Aceptar»,
  ninguna enseña un `uuid`. Recorre las siete rutas del panel. Contiene el
  **único defecto abierto** del arnés, marcado con `test.fail`: la auditoría
  sí enseña identificadores.
- **`sesion-csrf.spec.ts`** — dos pestañas del mismo contexto escribiendo
  alternadamente (A, B, A, B, A) sin que ninguna reciba un 403. Es la prueba
  de la ADR-012.
- **`teclado.spec.ts`** — `Tab` sin perder el foco en Módulos y en Usuarios,
  foco atrapado dentro del diálogo, y `Escape` que cierra **sin ejecutar la
  acción**. El número de saltos se calcula: al acabar el documento el foco
  pasa a la barra del navegador y volvería legítimamente al `body`.
- **`configuracion.spec.ts`** — los `PENDIENTE_DEFINIR` destacados y
  contados, y el interruptor de publicación deshabilitado **con su razón**
  para el rol `admin`. Usa el segundo usuario que siembra `global-setup.ts`.
- **`medios.spec.ts`** — los tres rechazos de subida **distintos entre sí**
  (no solo presentes), el duplicado que avisa sin fallar, y el aviso de baja
  sin recuento. Los ficheros van como buffers en memoria: ninguno existe en
  disco ni en git.

## Capturas y galería

Cada paso relevante de cada prueba deja dos archivos en
`screenshots/<spec>/NN-paso--claro.png` y `--oscuro.png`. Al terminar la
corrida (pase o falle), `global-teardown.ts` genera
`screenshots/index.html`: ábrelo directamente en el navegador — es HTML
estático con las imágenes referenciadas al lado, sin servidor de por medio —
para repasar de un vistazo qué mostró cada paso en los dos temas.

`pnpm report` abre, aparte, el reporte propio de Playwright
(`playwright-report/`): traza, video y captura de cualquier prueba que haya
fallado, más los archivos que cada prueba adjunte (por ejemplo
`errores-de-consola.txt` cuando la vigilancia de `base.ts` encuentra algo).

Ninguna de las dos carpetas se versiona (`.gitignore` de la raíz), ni
`.media-e2e/` ni `test-results/`: son salida de cada corrida, no parte del
producto.

## Qué levanta la suite sola

Base de datos, migraciones, seeds, imagen de la API en Debug con los módulos
de demostración, instalación con un super_admin propio (`E2E_ADMIN` en
`setup/api.ts`), y un grafo de módulos con las cuatro variantes de tarjeta ya
montadas. Todo efímero: `global-teardown.ts` corre `docker compose down -v`
al final, éxito o fallo, así que no queda ni un volumen atrás.

Lo único que la suite no levanta es el frontend en sí — lo hace Playwright a
través de su propio `webServer` (Vite, apuntado a la API efímera), que es
justo lo que hace posible que la cookie de sesión viaje sin fricción de CORS,
igual que en desarrollo.

## Qué NO cubre todavía

- **El modo instalación en sí** — pantalla del asistente, validaciones del
  formulario. Fuera de alcance a propósito; `global-setup.ts` lo completa por
  API, sin pasar por esa pantalla.
- **Tres juicios humanos**, que no se automatizan porque no son afirmaciones
  sobre el DOM: si dos estados de tarjeta se distinguen **entre sí** en
  oscuro (axe mide texto contra fondo, no un estado contra otro), si la
  espera del reinicio resulta razonable en una demostración, y si las frases
  suenan a persona. Están en `docs/VERIFICACION-VISUAL-CORE.md`.
- **Que las entradas de menú de un módulo aparezcan al activarlo.** Hoy no es
  afirmable: los módulos de demostración son solo de backend y
  `layout/navigation.ts:42` monta únicamente `coreNavigation`. Se afirmará
  con M01, el primer módulo con interfaz.
- **La pantalla de login en sí** — `modulos.spec.ts` abre sesión por API
  (`page.request.post`, sin pasar por el formulario) porque ninguno de sus
  criterios trata del login y visitarlo de más solo añadía un 401 esperado a
  la cuenta de errores de consola. Cuando exista un spec de sesión, esa es la
  prueba que debe ejercer el formulario de verdad.
- **Cualquier módulo real** (Catalog incluido): el grafo que arma
  `global-setup.ts` es enteramente `Sillar.Modules.Demo`, que solo compila y
  solo se monta en Debug + `Modules:IncludeDemoModules` — nunca en la imagen
  de producción. Sirve para las cuatro variantes de tarjeta; no prueba nada
  del comportamiento propio de Catalog.

## Notas para quien siga

- Los tokens de color de `shared/styles/tokens.css` tienen ahora un rol
  nuevo, `--link` (texto de enlace/acción sobre fondo neutro, distinto de
  `--primary` como fondo de botón): axe-core encontró que reusar `--primary`
  para las dos cosas daba 8.4:1 como fondo de botón y 1.88:1 como texto en
  modo oscuro, porque son necesidades distintas que el token no distinguía.
  Si agregas un color nuevo, compruébalo en los dos temas — no basta con
  mirarlo en claro.
- `duringExpectedOutage` existe porque el navegador anuncia en consola
  *cualquier* respuesta HTTP de error, la maneje la aplicación con elegancia
  o no — incluidos los 409 y los fallos de red que la propia prueba provoca a
  propósito. Si un spec nuevo necesita provocar un fallo esperado, esa es la
  herramienta; no bajes la guardia del resto de la prueba para lograrlo.
- `tokens.css` resuelve el tema en tres niveles, no dos: `:root` (claro,
  base), `@media (prefers-color-scheme: dark)` guardado con
  `:not([data-theme="light"])` (el sistema, mientras nadie elija), y
  `[data-theme="dark"]` sin guardar (la elección explícita, gane lo que gane
  el sistema). `useTheme.ts` no pone `data-theme` mientras no haya una
  elección de la persona: si lo pusiera siempre, el medio nunca tendría
  ocasión de decidir nada. Cualquier tema nuevo que se agregue tiene que
  respetar esta forma, no solo añadir un `[data-theme="algo"]` suelto.
