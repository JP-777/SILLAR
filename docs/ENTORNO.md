# El entorno de la máquina de desarrollo

Lo que hay que saber de **la máquina** antes de correr la puerta, y los hallazgos que
costaron un diagnóstico entero para que el siguiente no los tenga que repetir.

> **Por qué existe este archivo.** Nada de lo que hay aquí es del producto: no cambia con
> ningún módulo y no lo cubre ningún SPEC. Vivía en un checkpoint fuera del repositorio,
> que es tanto como no existir — quien retoma el trabajo abre el repositorio.
>
> **Cada entrada dice contra qué se comprobó.** Fecha y commit. Un hallazgo de entorno
> caduca en silencio: la máquina se actualiza, KDE cambia de versión, se instala otra
> worktree. Sin la fecha nadie sabe si sigue siendo verdad; con ella, al menos se sabe qué
> hay que volver a mirar.
>
> **Esto no es `PENDIENTES.md`.** Aquí no hay trabajo aplazado: hay hechos de la máquina.
> Un hallazgo que pida cambiar el producto va allí, no aquí.

---

## Antes de correr la puerta

### La suspensión: `systemd-inhibit` no basta en este equipo

Una corrida de `node scripts/verificar.mjs` dura unos veinte minutos sin que nadie toque el
teclado, que es exactamente lo que la gestión de energía entiende como inactividad. Si la
máquina se suspende a mitad, el WiFi se desautentica y la suite muere con
`net::ERR_NETWORK_CHANGED` en pruebas que no tienen nada que ver — y el fallo *parece* del
producto.

**Envolver con `systemd-inhibit` a secas no protege.** Está comprobado que no: el inhibidor
estaba puesto en modo `block` y la máquina se suspendió igual. Quien gestiona los eventos de
energía aquí es **KDE PowerDevil**, que pide la suspensión por su cuenta sin pasar por el
inhibidor de systemd.

**La forma que sí protege** añade `kde-inhibit --power`, que se registra en el `PolicyAgent`
de PowerDevil:

```bash
kde-inhibit --power systemd-inhibit --what=sleep:idle --why="SILLAR canonical gate" \
  env PATH="$PATH:$HOME/.dotnet/tools" node scripts/verificar.mjs
```

Se comprueba, antes de dejarla correr sola, que **los dos** registros están puestos:

```bash
systemd-inhibit --list | grep verificar          # el de systemd
qdbus6 --literal org.kde.Solid.PowerManagement \
  /org/kde/Solid/PowerManagement/PolicyAgent ListInhibitions   # el de PowerDevil
```

El bloqueo muere con el comando: no queda ninguna configuración de energía cambiada que
alguien tenga que acordarse de volver a poner.

### El `PATH` de `dotnet-ef`

`dotnet ef` está instalado como herramienta global en `~/.dotnet/tools`, y **ningún archivo
de perfil añade esa carpeta al `PATH`**. Las etapas 4 y 6 lo necesitan
(`scripts/verificar.mjs:365`, `e2e/setup/migrate.ts:19`). De ahí el `env PATH=…` de la receta
de arriba; sin él la puerta muere en la etapa 4 a los veinte segundos con «command not
found», que no se parece en nada a lo que es.

---

## Hallazgos

### 1 · pnpm viene por corepack, y sí hay lockfile

*3 de septiembre de 2026 · comprobado contra `3b6806d`*

El `pnpm` del `PATH` no es un paquete instalado aparte: es el atajo de **corepack**.

```
$ readlink -f "$(which pnpm)"
/usr/lib/node_modules/corepack/dist/pnpm.js      # pnpm 11.24.0, corepack 0.34.7
```

Y **hay lockfile**, dos de hecho, los dos versionados:

```
frontend/pnpm-lock.yaml
e2e/pnpm-lock.yaml
```

No hay `package.json` en la raíz ni `pnpm-workspace.yaml`: son **dos paquetes
independientes**, no un monorepo. Por eso `pnpm install` se ejecuta dos veces, una en cada
carpeta, y por eso no existe ni puede existir un lockfile en la raíz.

**Qué se contaba mal.** El documento de migración `09_ENTORNO` decía que no había
`package-lock.json` y que `npm ci` no aplicaba. Las dos cosas son ciertas —el proyecto usa
pnpm, nunca npm— pero de ahí se sacaba la conclusión de que **no había instalación
reproducible**, y eso es falso: la hay, se llama `pnpm install --frozen-lockfile` y se
ejecuta en `frontend/` y en `e2e/`. Una verdad incompleta que apunta a la conclusión
contraria hace más daño que un error a secas.

**Lo que sí está flojo, y no es lo que decía aquel documento:** ningún `package.json`
declara `packageManager`, así que corepack no tiene la versión de pnpm fijada por el
repositorio. Hoy da 11.24.0 porque es lo que hay instalado en esta máquina, no porque el
proyecto lo pida. No se toca aquí: es un cambio de producto y tendría que ir por su cauce.

### 2 · Una worktree recién creada no compila hasta restaurar

*4 de septiembre de 2026 · reproducido contra `3b6806d`*

`bin/` y `obj/` están en `.gitignore` (`.gitignore:7-8`), así que una worktree nueva nace sin
ellos. Y `obj/project.assets.json` es lo que NuGet escribe al restaurar.

Reproducido en una worktree limpia, sin restaurar nada antes:

```
error NETSDK1004: Assets file '…/backend/Sillar.Core/obj/project.assets.json' not found.
Run a NuGet package restore to generate this file.
```

Tras un `dotnet restore backend/Sillar.sln` el mismo comando pasa de largo.

**Cuándo muerde y cuándo no.** La puerta completa **no** lo sufre: su etapa 3 hace
`dotnet build` (`scripts/verificar.mjs:401`), que restaura por su cuenta, y la etapa 4 va con
`--no-build` (`:368`) precisamente porque ya está construido. Lo sufre **quien lanza la suite
e2e por su cuenta** en una worktree recién creada, porque `setup/migrate.ts:19` llama a
`dotnet ef` sin haber pasado por ninguna compilación previa.

**Y hay un segundo tropiezo detrás del primero:** `.env` no se versiona (`.gitignore:2`), así
que una worktree nueva tampoco lo tiene. Una vez restaurado, el fallo siguiente ya no es
`NETSDK1004` sino «Falta la cadena de conexión `ConnectionStrings__Default`». Son dos pasos,
no uno.

**Qué hacer al crear una worktree:** `dotnet restore backend/Sillar.sln`, copiar `.env`
desde `.env.example` y rellenarlo, y `pnpm install` en `frontend/` y en `e2e/` — ver el
hallazgo 3, que explica por qué **propio** y no compartido, y el 5, por qué con identidad
distinta.

### 3 · `ERR_PNPM_UNSAFE_MODULES_DIR` — un cuasi-accidente, no una molestia

*4 de septiembre de 2026 · la disposición descrita ya está corregida en `sillar-footer`*

**El síntoma.** Un `pnpm install` en la worktree `sillar-footer` se negó a ejecutarse con
`ERR_PNPM_UNSAFE_MODULES_DIR`, diciendo que el directorio de módulos quedaba fuera de la raíz
del proyecto.

**La disposición que lo provoca**, que es lo que hay que reconocer y no el mensaje:

```
/home/JP777/sillar-footer/frontend/node_modules  ->  /home/JP777/sillar-fx/frontend/node_modules
/home/JP777/sillar-footer/e2e/node_modules       ->  /home/JP777/sillar-fx/e2e/node_modules
```

Es decir: **enlaces simbólicos a las dependencias de otra worktree.** Aparece con toda
naturalidad, porque `node_modules/` está en `.gitignore` (`.gitignore:14`) y una worktree
nueva nace sin dependencias; enlazar a las del vecino parece la forma barata de no instalar
dos veces.

**Por qué es un cuasi-accidente y no una molestia.** Un `pnpm install` escribe en el
directorio de módulos: instala lo que falta y **quita lo que sobra** según el lockfile de
*su* proyecto. A través de ese enlace, el destino de esa escritura no eran las dependencias
del footer sino **las de `sillar-fx`** — la worktree donde en ese momento se estaban corriendo
las puertas de certificación de la Corrección 3. La negativa de pnpm no fue un obstáculo: fue
lo único que se interpuso entre un comando rutinario y arrasar las dependencias de una corrida
en marcha, con un fallo que habría aparecido como un error de tipos o de módulo no encontrado,
en otra worktree, sin ninguna relación aparente con el comando que lo causó.

> No está probado que la escritura hubiera atravesado el enlace, porque pnpm no llegó a
> intentarlo. Lo que sí está establecido es cuál era el destino del enlace y qué hace
> `pnpm install` con un directorio de módulos.

**Qué hacer en su lugar: dependencias propias en cada worktree.** `pnpm install` en
`frontend/` y en `e2e/` de la worktree nueva, sin enlazar nada. El coste real es bajo: pnpm
guarda los paquetes una sola vez en un almacén global compartido
—`/home/JP777/.local/share/pnpm/store/v11`, el mismo para todas las worktrees— y lo que pone
en cada `node_modules` son enlaces a ese almacén, no copias. Se paga tiempo de instalación,
casi no se paga disco.

**Y la regla que queda:** un `node_modules` **nunca** es un enlace a otra worktree. Si al
entrar en una worktree `readlink node_modules` responde algo, eso se borra y se instala.

### 4 · PowerDevil: la cuarta causa ambiental, y es distinta de las otras tres

*3 de septiembre de 2026 · dos corridas de la puerta sobre `3b6806d`*

El pendiente §8 de `PENDIENTES.md` abrió este asunto —fallos de la etapa e2e causados por la
máquina y no por el código— con **dos** casos y el disparador «la tercera vez». Él no las
clasifica; la clasificación se fue haciendo después, corrida a corrida, y hasta ahora todas
eran de red:

| | Causa | Cómo se reconoce |
|---|---|---|
| 1 | Otro stack de Docker entero levantándose a la vez | Timeouts de arranque de la API; en corrida limpia, segundos |
| 2 | Suspensión S3 del equipo | La corrida tarda horas de reloj para veinte minutos de trabajo |
| 3 | Pérdida de WiFi/DNS | `Temporary failure in name resolution` al traer imágenes de `mcr.microsoft.com` |

**La cuarta no es ninguna de esas tres, y mezclarla las estropea.** El equipo se suspendió a
mitad de una corrida **que estaba protegida** con `systemd-inhibit --what=sleep:idle`, con el
inhibidor verificado en modo `block`:

```
17:11:27  systemd-logind: The system will sleep now!
17:11:27  NetworkManager: NetworkManager state is now DISABLED (ASLEEP)
17:11:27  kernel: wlan0: deauthenticating … (Reason: 3=DEAUTH_LEAVING)
17:11:27  kwin_wayland: Failed to delay sleep: The operation inhibition
                        has been requested for is already running
```

Resultado visible: `movil-teclado.spec.ts:179` en rojo con cinco
`net::ERR_NETWORK_CHANGED` de consola, nueve en la corrida entera, y 122 de 123 pruebas en
verde. **Se parece a la causa 3 y no lo es**: la red no falló por sí sola, la apagó la
suspensión. Y se parece a la causa 2 y tampoco lo es: aquélla se reconocía porque la corrida
duraba horas de reloj, y ésta cabe en su ventana normal porque la máquina despertó sola y la
suite siguió corriendo con la red cambiada debajo.

**Lo que la distingue de las tres:** es la única que **sobrevive a la protección**. Las otras
se evitan preparando la máquina; ésta se evitaba también, o eso se creía, y por eso costó dos
corridas y un diagnóstico entero descubrir que el remedio conocido no servía. El remedio real
está arriba, en «Antes de correr la puerta».

**Cómo reconocerla en el primer minuto**, sin volver a diagnosticarla:

```bash
journalctl --since "<hora de inicio de la corrida>" | grep -iE 'will sleep now|PrepareForSleep'
```

Si aparece algo, no se toca el código. Si no aparece nada y hay `ERR_NETWORK_CHANGED`, es la
causa 3 y se mira el WiFi.

### 5 · La identidad e2e va separada por worktree

*4 de septiembre de 2026 · comprobado contra `3b6806d` y contra `sillar-footer`*

La suite levanta su propio stack de Docker y su propio Vite, y **todo lo que lo identifica
sale de `e2e/.env.e2e`** (`e2e/setup/env.ts:51-64`). Son cinco valores, y el quinto es el que
se olvida:

| | Qué nombra | Dónde se usa |
|---|---|---|
| `COMPOSE_PROJECT_NAME` | El proyecto de Docker y el nombre de los contenedores | `docker-compose.yml:4,61` |
| `POSTGRES_PORT` | El puerto publicado de la base | `docker-compose.yml:34` |
| `API_PORT` | El puerto publicado de la API | `docker-compose.yml:86` |
| `FRONTEND_PORT` | El Vite que arranca Playwright | `e2e/playwright.config.ts:32` |
| **el `Port=` de `ConnectionStrings__Default`** | Por dónde entra `dotnet ef` a migrar | `e2e/setup/env.ts:64`, `e2e/setup/migrate.ts:19` |

**El quinto no se deduce de los otros cuatro.** Cambiar `POSTGRES_PORT` y dejar la cadena de
conexión apuntando al puerto viejo da un stack que levanta bien y unas migraciones que se
aplican **a la base de la otra worktree**. Los dos números se cambian juntos o no se cambia
ninguno.

**Los valores.** El archivo está versionado a propósito (`.gitignore:46`), y trae los de la
worktree principal. Una segunda worktree que necesite correr su suite lo modifica **sin
commitear el cambio**: la identidad es de la worktree, no de la rama.

```
sillar-fx, SILLAR, sillar-m02   sillar_e2e         55432 / 55081 / 55173
sillar-footer                   sillar_footer_e2e  55443 / 55091 / 55183
```

**Tres de las cuatro worktrees comparten identidad hoy.** No ha dado problemas porque no se
corren dos suites a la vez, pero el margen es ése: dos frentes que arranquen a la vez chocan
en 55173 y el segundo muere sin decir por qué.

**Y una nota para quien lea el README de `e2e/`:** `e2e/README.md:18-21` presenta 55432/55081/55173
como *los* puertos de la suite, frente a los de `sillar_dev`. Era cierto cuando había una sola
worktree. Es el mismo patrón que `PENDIENTES.md` §14 describe —«la regla que era cierta porque
solo había uno»— y por eso conviene leer aquel párrafo como lo que era el día que se escribió,
no como la lista vigente.
