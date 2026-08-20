# design-sync — SILLAR UI

Sincroniza `frontend/src/shared/ui/` (el kit de componentes compartido) al
proyecto **SILLAR UI** en claude.ai/design. Primera importación: 2026-08-18.

## Cómo está montado (importante para re-sync)

- **No hay build de librería.** `frontend/` es la app completa (Vite,
  `vite build` produce un bundle de aplicación, no una librería con
  `.d.ts`). El conversor corre en **modo "entry sintetizado"**: lee
  `src/shared/ui/{index,patterns,Gallery}.tsx` directamente vía ts-morph.
- **Junction necesaria.** El conversor necesita resolver `frontend/` como si
  fuera un paquete instalado bajo `node_modules` (para que `cfg.tokensPkg` y
  la resolución de `PKG_DIR` funcionen). Se creó una junction de Windows:
  `frontend/node_modules/sillar-frontend → frontend/` (self-link, como en un
  monorepo con workspace). **Si falta tras un clon nuevo, recreala**:
  ```
  New-Item -ItemType Junction -Path frontend\node_modules\sillar-frontend -Target frontend
  ```
  (en Linux: `ln -s .. frontend/node_modules/sillar-frontend`). Sin esto,
  `package-build.mjs` no encuentra `PKG_DIR` y falla con `[NO_DIST]` sin
  siquiera intentar el synth-entry.
- **`cfg.componentSrcMap` es obligatorio aquí**, no opcional: los 16
  componentes viven repartidos en solo 3 archivos (`index.tsx`,
  `patterns.tsx`, `Gallery.tsx`), así que la heurística de "un archivo por
  componente" del conversor solo acierta con `Gallery` por casualidad (el
  archivo se llama igual que el componente). Sin el mapa, 15/16 componentes
  pierden su JSDoc y agrupación.
- **`cfg.dtsPropsFor` también es obligatorio**: como los `*Props` son
  `interface`s no exportadas (privadas al archivo), ts-morph no puede
  resolverlas desde el entry sintetizado y cae al stub
  `[key: string]: unknown`. Los 16 props bodies están escritos a mano en
  `config.json`, transcritos de las interfaces reales — si el código fuente
  cambia una prop, hay que actualizar el `dtsPropsFor` a mano también.
- **Generics perdidos**: `Table<T>` y `Gallery<T>` son componentes
  genéricos; `dtsPropsFor` no preserva `<T>` (ver ASSUMPTION en
  `lib/dts.mjs:364`), así que sus `.d.ts` usan `any` donde el código real
  usa el genérico. Esto es una limitación conocida del override, no un bug.

## Overrides de tarjeta (portales)

`ConfirmDialog`, `Drawer` y `Toasts` usan `createPortal(document.body)` y
necesitan `cardMode: "single"` (ver `cfg.overrides` en config.json) — sin
esto, `[GRID_OVERFLOW]`/`[RENDER_THIN]` porque el contenido escapa de la
celda de la tarjeta.

## Known render warns (triaged, benignos)

- `Spinner.ConEtiqueta` se ve idéntico a la variante `lg` de `Spinner.Tamanos`
  — la prop `label` es solo para lectores de pantalla (`sr-only`), no tiene
  efecto visual. Esperado.

## Alcance

**18 componentes: los 17 de `frontend/src/shared/ui/` más `ModuleCard`.**

`src/shared/ui/` (17, auto-descubiertos por el synth-entry): Button, Input,
Field, Alert, Card, Badge, Switch, EmptyState, Spinner, Drawer, ConfirmDialog,
Toasts, Table, Pagination, FailureAlert, Gallery, **ThemeToggle** (nuevo,
2026-08-18 — interruptor claro/oscuro, sin props, usa `useTheme` con
`useState`/`useLayoutEffect` y `localStorage`, no Context — no necesita
`cfg.provider`). `useFocusTrap`, `useToasts`, `useEscape`, `useTheme` son
hooks, no componentes — correctamente excluidos.

### `ModuleCard` es el caso especial, y se queda donde está

Vive en `src/modules/core/components/ModuleCard.tsx`, **fuera de `cfg.srcDir`**.
Entra por tres entradas de config, las tres necesarias:

- `componentSrcMap` — para que se descubra como componente y se le asigne
  grupo/JSDoc (por casualidad su ruta produce el grupo correcto: `core`).
- `dtsPropsFor` — igual que el resto, porque `ModuleCardProps` no está
  exportada.
- **`extraEntries: ["./src/modules/core/components/ModuleCard.tsx"]`** — esta
  es la que de verdad lo mete en el bundle. `componentSrcMap` por sí solo
  **no** lo logra: el entry sintetizado solo hace `export *` de los archivos
  bajo `cfg.srcDir` (`src/shared/ui/`), así que sin `extraEntries` el
  componente se "descubre" para tipos y doc pero `window.SillarUI.ModuleCard`
  queda `undefined` en tiempo de ejecución — falla silenciosamente como
  `[BUNDLE_EXPORT]` + `[RENDER] root empty` en el validador. **Si algún día se
  agrega un segundo componente fuera de `src/shared/ui/`, necesita su propia
  entrada en `extraEntries` además de `componentSrcMap`/`dtsPropsFor`.**
- **`[EXPORT_COLLISION]` esperado y benigno**: el build imprime que
  `ModuleCard.tsx` "colisiona" con el paquete principal, porque en modo
  synth-entry `exported` se precarga con TODOS los nombres descubiertos
  (línea `if (src.synthEntry) for (const c of src.components) exported.add(c.name)`
  en `package-build.mjs`) antes de escanear `extraEntries` — se compara
  consigo mismo, no con una definición real distinta. No accionable mientras
  ningún preview importe `ModuleCard` desde la ruta del archivo en vez de
  `'sillar-frontend'` (los nuestros no lo hacen).
- **`cardMode: "column"`** en `cfg.overrides.ModuleCard` — la historia
  `Estados` compone 4 tarjetas en grid de 2 columnas (536px), más ancho que
  una celda normal → `[GRID_OVERFLOW]` sin el override.

**No se mueve a `src/shared/ui/` para que encaje.** Solo lo usa la pantalla de
módulos: no hay segundo caso real, y moverlo sería la abstracción por si acaso
que prohíbe `CLAUDE.md`. Lo que se extiende es esta config, no el árbol de
archivos. Si algún día un segundo módulo necesita la tarjeta, entonces sí se
sube a `shared/` — y entonces esta nota sobra.

Es además la vista previa que más importa de las dieciocho: la tarjeta con su
interruptor es la que define el producto.

**Su `dtsPropsFor` incluye `restartsAutomatically`**, que es de la respuesta de
`/api/admin/modules` y no del módulo en sí — el diálogo de confirmación elige
la frase del reinicio a partir de ese dato, no de una suposición.

## Re-sync risks

- **`/design-login` puede autorizar una cuenta distinta a la del proyecto
  pinneado.** El 18 de agosto, tras un `/design-login`, `get_project` devolvió
  404 y `list_projects` vino vacío para el `projectId` ya guardado en
  `config.json` — la sesión estaba autorizada contra otra cuenta. Un segundo
  `/design-login` (vinculando la cuenta correcta) lo resolvió sin tocar el
  `projectId`. Si un re-sync empieza con el proyecto pinneado devolviendo 404,
  **antes de crear un proyecto nuevo**, pedirle a la persona que confirme con
  qué cuenta está conectada en claude.ai/design.
- **POR CONFIRMAR en el próximo re-sync: ¿las capturas de graduación son solo de tema
  claro?** Al re-afirmar los componentes se dijo que el claro «es el único que capturamos». Si
  es así, **un componente roto en oscuro graduaría bien igual**. No es riesgo de enviar algo
  roto —en la aplicación `axe` corre en los dos temas, ver `e2e/fixtures/themes.ts:65-66`— sino
  de que Design proponga algo que se ve bien en la vista previa y falle en oscuro. Sin
  confirmar: mirar `package-capture.mjs` la próxima vez y, si se confirma, anotarlo aquí como
  limitación conocida.
- **Los tokens cambian sin que el bundle se entere.** El 18 de agosto
  `tokens.css` ganó `--link` y `--on-danger` —dos roles nuevos que salieron de
  fallos de contraste reales— y el bundle siguió sirviendo la versión anterior
  durante horas, con `--primary` haciendo de color de enlace y su 1.88:1
  dentro. **Tras tocar `tokens.css`, re-sincronizar**, y comprobarlo con
  `rg -- "--link" ds-bundle/`: si el rol nuevo no aparece, no se regeneró.
- Si `frontend/src/shared/ui/` gana un componente nuevo, revisar si necesita
  entrada en `componentSrcMap` y `dtsPropsFor` — la heurística automática
  probablemente fallará igual que con los 16 actuales (mismo problema de
  "varios componentes por archivo").
- Si cambia una prop de un componente existente, `dtsPropsFor` NO se
  actualiza solo — hay que editarlo a mano en `config.json` o el contrato
  que ve el agente de diseño queda desactualizado silenciosamente.
- La junction (`frontend/node_modules/sillar-frontend`) no se versiona
  (vive dentro de `node_modules/`, ignorado) — recrearla en cada clon nuevo
  antes de re-sincronizar.
- Ningún componente usa Context/Provider hoy. Si alguno lo necesita en el
  futuro, hay que añadir `cfg.provider`.
