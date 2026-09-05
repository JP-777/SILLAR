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
>
> **Y lo que se puede arreglar no se escribe aquí como paso manual.** Un paso documentado se
> paga cada vez que alguien estrena un árbol; un arreglo se paga una vez. Dos de los hallazgos
> de abajo —el 6 y el 7— llegaron como pasos manuales y salieron como defectos del repositorio:
> están contados en pasado, con lo que hace el arreglo y cómo se reconoce si vuelve. Si al leer
> uno piensas «esto tendría que hacerlo el arnés», probablemente tengas razón.

---

## Estrenar una worktree

Cinco pasos, y **están en este orden porque cada uno falla distinto si falta el anterior**.
Vale lo mismo para `git worktree add` que para un clon nuevo.

```bash
# 1 · Dependencias de .NET. Sin esto, NETSDK1004 — ver el hallazgo 2.
dotnet restore backend/Sillar.sln

# 2 · Configuración local. NO la copies de otra worktree — ver el hallazgo 7.
cp .env.example .env
#    Y edita las cuatro claves que identifican al árbol: COMPOSE_PROJECT_NAME,
#    POSTGRES_PORT, el Port= de ConnectionStrings__Default y Sillar__Node__Code.
#    El propio .env.example las lista y dice por qué.

# 3 · Dependencias de node, propias de esta worktree. Nunca un enlace a otra:
#    ver el hallazgo 3, que es el que casi cuesta caro.
pnpm install --frozen-lockfile --dir frontend
pnpm install --frozen-lockfile --dir e2e

# 4 · Identidad de la suite e2e, si esta worktree va a correrla a la vez que otra.
#    e2e/.env.e2e SÍ está versionado: se modifica y NO se commitea — ver el hallazgo 5.

# 5 · El PostgreSQL de desarrollo. La puerta crea su base efímera dentro de él,
#    así que sin esto no pasa de la comprobación de entorno. Es OTRO stack que el
#    de la suite e2e, que se levanta solo — ver el hallazgo 8.
docker compose up -d db
```

Lo que **no** hay que hacer: crear `e2e/.media-e2e` a mano (lo hace el arnés, hallazgo 6),
añadir `~/.dotnet/tools` al `PATH` (lo hace la puerta), levantar el stack de la suite e2e a
mano (lo hace el arnés) ni volver a descargar los navegadores de Playwright: viven en
`~/.cache/ms-playwright`, que es del usuario y no del proyecto, así que una worktree nueva los
encuentra ya puestos —comprobado desde `sillar-estreno`, `chromium.executablePath()` resuelve a
`/home/JP777/.cache/ms-playwright/chromium-1234/…`—. **En una máquina nueva sí hacen falta**:
ahí es `pnpm exec playwright install` dentro de `e2e/`, y esta lista es de estrenar una
worktree, no una máquina.

> **Cómo se mantiene esta lista.** No dándola por buena porque esté escrita. La próxima
> worktree se estrena siguiendo **solo** esta lista, sin memoria y sin improvisar, y cada paso
> que falte se añade en ese momento. Es verificación por efecto aplicada a documentación, que
> es donde peor se aplica.
>
> **La última vez fue el 5 de septiembre de 2026**, en la worktree `sillar-estreno`, contra
> `a0b1765`. La lista tenía entonces cuatro pasos y le faltaba el quinto: la puerta murió en
> «FALLÓ en la etapa: entorno — El servicio PostgreSQL `db` no responde». Se añadió ahí mismo,
> que es la única forma de que una lista así no envejezca. De paso salió el defecto de
> `kde-inhibit` que está descrito más abajo.

---

## Antes de correr la puerta

```bash
node scripts/verificar.mjs
```

**Eso es todo, y no siempre fue así.** Hasta el 5 de septiembre de 2026 esta sección pedía
envolverla con dos inhibidores y un `PATH`:

```bash
# NO usar. Se documenta para que se reconozca si aparece en un guion viejo.
kde-inhibit --power systemd-inhibit --what=sleep:idle --why="SILLAR canonical gate" \
  env PATH="$PATH:$HOME/.dotnet/tools" node scripts/verificar.mjs
```

Esa línea tenía **dos** problemas, y el segundo es peor que el primero.

### El problema barato: era un paso manual

Y de los que se olvidan. Los dos motivos siguen siendo ciertos y están abajo, en los hallazgos
4 y 2, pero ya no hay que acordarse de ellos: **la puerta toma los inhibidores y arregla su
propio `PATH`**. Lo dice al arrancar, y dice también cuando *no* ha podido:

```
  ~/.dotnet/tools añadido al PATH de esta corrida.
  Suspensión bloqueada durante la corrida (2/2 inhibidores).
```

Si falta alguno —Windows, un Linux sin KDE— lo avisa y sigue: un bloqueo que no se pudo tomar
es un riesgo conocido, no un motivo para no correr las pruebas.

### El problema caro: `kde-inhibit` se tragaba el código de salida

**`kde-inhibit` no propaga el código de su hijo. Siempre devuelve 0.** Medido el 5 de
septiembre de 2026 sobre la misma puerta fallida, en la worktree `sillar-estreno`:

| Cómo se lanza | Código |
|---|---|
| `node scripts/verificar.mjs` | **1** |
| `systemd-inhibit … node scripts/verificar.mjs` | **1** |
| `kde-inhibit --power node scripts/verificar.mjs` | **0** |

La receta que esta misma sección recomendaba **convertía cualquier rojo en un cero** para
quien mirase `$?`. Nadie lo notó porque el veredicto se leía en la pantalla, donde el `FALLÓ
en la etapa` seguía saliendo. Habría mordido a la primera cosa que encadenara la puerta con
`&&` o la metiera en un guion.

Es una advertencia sobre las recetas de este archivo tanto como sobre `kde-inhibit`: **una
línea de comando documentada es código sin pruebas**. Ésta estuvo escrita dos días.

### Cómo se comprueba que el bloqueo está puesto

Con la puerta corriendo, desde otra terminal:

```bash
systemd-inhibit --list | grep SILLAR                             # el de systemd
qdbus6 --literal org.kde.Solid.PowerManagement \
  /org/kde/Solid/PowerManagement/PolicyAgent ListInhibitions     # el de PowerDevil
```

Al terminar, los dos quedan vacíos: la puerta los suelta en su `finally` y mata el grupo de
procesos entero, no solo al hijo —matar solo al hijo dejaba un `sleep` huérfano por corrida, y
también eso está medido—.

## Cuando la suite sale en rojo: ¿es mío o es la máquina?

**Lo primero ya no hay que hacerlo: lo hace la puerta.** Debajo del `FALLÓ en la etapa` escribe
un veredicto con la evidencia en que se basa. Tres formas:

```
ES DEL ENTORNO — el equipo se suspendió durante la corrida.
  <la línea del diario que lo dice>
  No toques el código. Vuelve a lanzarla; docs/ENTORNO.md, hallazgo 4.
```

```
NO PARECE TUYO — esta rama no toca nada de la etapa que falló.
  La etapa mira frontend/ y la rama no cambia nada ahí.
  Venía de main o de otro frente: devuélvelo en vez de investigarlo.
```

```
Sin veredicto: ninguna señal permite atribuirlo automáticamente.

Lo que NO se pudo comprobar (1):
  - suspensión: el diario no devolvió nada para la ventana pedida (desde 2026-09-05 13:41:02)
```

**La tercera importa tanto como las otras dos.** Un veredicto que siempre dice algo se deja de
leer; éste calla cuando no sabe, y por eso se le puede creer cuando habla. Sobre la suite e2e
nunca afirma de quién es —la rompe cualquier capa— y en su lugar manda al sitio donde está la
respuesta.

**Y «no lo sé» no es lo mismo que «no pude mirar».** Cada sonda responde una de tres cosas —lo
vi, miré y no había, o **no pude mirar y éste es el motivo**—, y el veredicto lista siempre sus
puntos ciegos. Antes devolvían todas lo mismo cuando no podían ejecutarse, y ahí se escondió
durante dos días el fallo de la zona horaria: la detección muerta y la detección «sin diario
que consultar» producían la misma nada. Está contado en `BITACORA.md` §4, «Una barrera que
calla no se distingue de una barrera que funciona».

**Las siete ramas se provocan con un comando**, no con un recuerdo:

```bash
SILLAR_VERIFY_AUTOPRUEBA_VEREDICTO=1 node scripts/verificar.mjs
```

Alimenta el veredicto con sondas de mentira, enseña lo que escribe cada rama y termina en 1 si
alguna calla. No levanta nada ni necesita base de datos.

**Por qué esto dejó de ser opcional.** «La puerta es el criterio» era cierta con un frente: si
está roja, es tuya. Con dos frentes un rojo ajeno bloquea a los dos, y cada frente paga el
tiempo de las pruebas del otro sin poder hacer nada. Distinguir «esto lo rompí yo» de «esto
venía roto» es lo que permite devolverlo en vez de investigarlo. Es el pendiente §8 convertido
en requisito previo de la división.

Lo que sigue siendo a mano, en este orden:

**1 · ¿Qué vio el navegador?** `e2e/test-results/` es lo primero que hay que abrir y lo último
que se mira, que es al revés de como debería ser. Hallazgo 9.

**2 · ¿Estás mirando el stack que crees?** Hay dos, salen del mismo `docker-compose.yml` y se
parecen. Hallazgo 8.

**3 · Y si el veredicto calló pero sospechas del entorno**, la pregunta directa al diario:

```bash
journalctl --since "<hora de inicio de la corrida>" | grep -iE 'will sleep now|PrepareForSleep'
```

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

### 6 · `e2e/.media-e2e`: quién crea la carpeta decide quién puede escribir en ella

*5 de septiembre de 2026 · ocurrido en `sillar-footer` el 4 de septiembre · **arreglado**, ver
abajo*

**El síntoma.** Once pruebas en rojo a la vez, todas con **HTTP 500 al subir un archivo**. El
resto de la suite, en verde.

**La causa.** `MEDIA_PATH=./e2e/.media-e2e` (`e2e/.env.e2e:27`) se monta en el contenedor como
`/data/media` (`docker-compose.yml:94`). Si esa carpeta **no existe** cuando arranca el
servicio, la crea docker, y la crea como `root`. El proceso de dentro no es root: la imagen
base define `app` con UID 1654 (`backend/Dockerfile:50-51`). Escribir dentro es imposible, y lo
único que se ve es un 500.

Y `.media-e2e` está en `.gitignore` (`.gitignore:49`), así que **una worktree nueva nunca la
hereda**: es exactamente el mismo patrón que el hallazgo 2 y que el 7 —algo no versionado que
no se hereda—, con la diferencia de que este no falla al arrancar sino a mitad, y en once
sitios que no se parecen entre sí.

**Cómo se diagnosticó, que es la parte cara.** Comparando el propietario con el de otra
worktree que sí funcionaba. Eso solo está a mano si hay otra worktree y si a alguien se le
ocurre mirar el propietario de una carpeta, que no es lo que uno mira cuando ve un 500.

**Qué se hizo en vez de escribir un paso manual.** El arnés la crea antes de levantar docker,
en `e2e/setup/global-setup.ts`, dos líneas más abajo del `mkdir` de `screenshots` que ya estaba
ahí. Se abre en escritura para todos y **no** se hace `chown`: cambiar el propietario a 1654
exige ser root y el arnés no lo es. Es aceptable en esta carpeta y solo en ésta —está fuera del
control de versiones, no contiene nada del producto y cada corrida la vacía.

**Si vuelve a pasar** —una carpeta que quedó de antes del arreglo, con el propietario malo— el
arnés falla al abrirla con un `EPERM` y **lo dice por su nombre**, con el `sudo rm -rf` que lo
arregla, en vez de dejar que reaparezca once pruebas más tarde.

### 7 · El `.env` de la raíz tampoco se hereda, y copiarlo del vecino es peor que no tenerlo

*5 de septiembre de 2026 · comprobado ejecutando la puerta sin `.env`, contra `b53e5ee` ·
**arreglado**, ver abajo*

**El síntoma.** La puerta ni arranca. Muere en `cadenaEfímera` antes de la etapa 1, así que no
sale por el «FALLÓ en la etapa: n» que uno espera.

**La causa.** `.env` está en `.gitignore` (`.gitignore:2`). Tercer caso del mismo patrón.

**El peligro, que es lo que hay que retener.** El remedio que se le ocurre a cualquiera es
copiar el `.env` de la worktree de al lado. Ése apunta a **su** PostgreSQL: la puerta crearía
su base efímera dentro de la instalación del otro árbol y el API competiría por el mismo
puerto. No falla — **funciona, en otro sitio**, que es bastante peor que fallar.

**Qué se hizo en vez de escribir un paso manual.** Dos cosas, y ninguna es documentación:

- `scripts/verificar.mjs` distingue los dos casos —no hay archivo, o el archivo está y le falta
  la clave—, nombra `.env.example` como remedio y avisa de lo del vecino. Se atrapa en el punto
  de llamada para que lo que se lea sea el remedio y no una traza de Node.
- `.env.example` lista arriba del todo **las cuatro claves que identifican al árbol** y van
  distintas en cada worktree: `COMPOSE_PROJECT_NAME`, `POSTGRES_PORT`, el `Port=` de
  `ConnectionStrings__Default` y `Sillar__Node__Code`.

**Una corrección al encargo que originó esto**, porque quedó escrito al revés: no era que
`.env.example` «no cubriera la raíz». Está en la raíz, versionado, y trae
`ConnectionStrings__Default` (`.env.example:55`). Lo que faltaba no era la plantilla: era que
el fallo la nombrara.

### 8 · Hay dos stacks, salen del mismo `docker-compose.yml` y se parecen

*5 de septiembre de 2026 · comprobado contra `b53e5ee`*

Un mismo `docker-compose.yml` levanta dos cosas distintas, y confundirlas hace perder el rato
de dos maneras: mirar los registros del que no falló, o creer que la suite está arriba cuando
lo que está arriba es la base de desarrollo.

| | **Desarrollo** | **Suite e2e** |
|---|---|---|
| Quién lo levanta | Tú, `docker compose up -d` | El arnés, en `global-setup.ts` |
| Configuración | `.env` de la raíz | `e2e/.env.e2e` (`e2e/setup/docker.ts:4`) |
| Nombre de proyecto | `COMPOSE_PROJECT_NAME`, `sillar` por defecto | `sillar_e2e` (`e2e/setup/env.ts:51`) |
| Servicios | `db` (`docker-compose.yml:2`) | `db` **y** `api`, perfil `full` (`:51,62`) |
| Base | `sillar_dev` | `sillar_e2e`, y se destruye con su volumen al terminar |
| Vida | La que tú le des | Una corrida, salvo `E2E_KEEP_STACK=1` |

**Lo que los distingue de un vistazo es el prefijo del contenedor**, que sale del nombre de
proyecto (`docker-compose.yml:4,61`):

```bash
docker ps --format '{{.Names}}\t{{.Ports}}'
```

**Por qué importa además de para no confundirse.** El `composeDown()` del arnés lleva `-p
sillar_e2e` y `-v`: destruye su stack entero, volumen incluido, y **no toca** el de desarrollo.
Esa separación es deliberada y es la razón de que la suite pueda ser destructiva sin miedo. Si
alguna vez los dos nombres de proyecto coinciden —por haber copiado un `.env` del vecino, ver
el hallazgo 7—, esa garantía se cae sin avisar.

Y una tercera cosa que no es ninguna de las dos: la puerta canónica crea **su propia** base
`sillar_verify_<timestamp>_<pid>` dentro del PostgreSQL al que apunte el `.env` de la raíz. Son
tres bases, no dos.

### 9 · `e2e/test-results/` sabe lo que pasó, y es lo último que se mira

*5 de septiembre de 2026 · comprobado contra `b53e5ee`*

Cuando una prueba de Playwright falla, lo que se lee es la aserción: «esperaba X, encontré Y».
Eso dice **qué** no cuadró, casi nunca **por qué**. Lo que lo dice está en disco y nadie lo
abre.

`e2e/test-results/` —salida por defecto de Playwright, ignorada en `.gitignore:50`— guarda una
carpeta por prueba fallida, y dentro:

| Qué | Para qué sirve |
|---|---|
| `error-context.md` | **El DOM de la página en el momento del fallo.** Lo que había, no lo que se esperaba |
| `trace.zip` | La corrida entera paso a paso, con `pnpm exec playwright show-trace <ruta>` (`playwright.config.ts:45`) |
| captura y vídeo | El estado final y cómo se llegó (`:49`, `:50`) |

**Lo que esto encontró y ninguna otra señal delataba.** Un bucle de remontaje: un componente
que se desmontaba y se volvía a montar sin parar. La aserción decía «no encuentro el elemento»
—que es lo mismo que dice un selector mal escrito, o una ruta que no carga, o media docena de
cosas más—. El `error-context.md` enseñaba el DOM y ahí se veía el ciclo. **Ninguna cantidad de
releer el test lo habría dado**, porque el test no era el problema.

**La regla.** Ante un rojo de e2e que no se entiende leyendo la aserción, `error-context.md`
va **antes** de releer el código, no después. Y se mira antes de relanzar la suite: la carpeta
se rehace en cada corrida, así que relanzar borra la prueba de lo que pasó.
