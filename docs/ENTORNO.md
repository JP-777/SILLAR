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

Cuatro pasos, y **están en este orden porque cada uno falla distinto si falta el anterior**.
Vale lo mismo para `git worktree add` que para un clon nuevo.

```bash
# 1 · Dependencias de .NET. Sin esto, NETSDK1004 — ver el hallazgo 2.
dotnet restore backend/Sillar.sln

# 2 · Configuración local, con la identidad de este árbol ya calculada.
#    NO copies el .env de otra worktree, ni el .env.example a mano: los dos
#    traen la identidad de OTRO árbol. Ver el hallazgo 7.
node scripts/estrenar.mjs
#    Escribe el .env con las siete claves que identifican al árbol, derivadas
#    del nombre del directorio. Lo único que queda a mano son las contraseñas.

# 3 · Dependencias de node, propias de esta worktree. Nunca un enlace a otra:
#    ver el hallazgo 3, que es el que casi cuesta caro.
pnpm install --frozen-lockfile --dir frontend
pnpm install --frozen-lockfile --dir e2e

# 4 · El PostgreSQL de desarrollo. La puerta crea su base efímera dentro de él,
#    así que sin esto no pasa de la comprobación de entorno. Es OTRO stack que el
#    de la suite e2e, que se levanta solo — ver el hallazgo 8.
docker compose up -d db
```

**El paso que desapareció era el de la identidad e2e**, y no desapareció por olvido: la suite
ya no necesita que nadie edite nada. Deriva su identidad del directorio igual que el `.env` y
se la pasa a docker por el entorno del proceso. Ver el hallazgo 5.

Para ver la identidad de este árbol, o comprobar que no choca con la de otro:

```bash
node scripts/identidad.mjs
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
>
> **Y el 7 de septiembre de 2026 la lista cambió sin volver a estrenarse.** Los pasos 2 y 4 se
> reescribieron al derivar la identidad, y esta versión **no se ha verificado estrenando una
> worktree de verdad**: lo que sí está provocado, en un árbol de mentira, es cada uno de los
> dos comandos nuevos por separado, en sus dos vías —`estrenar.mjs` escribiendo y negándose a
> pisar un `.env` existente, `identidad.mjs` con choque y sin él—. Queda dicho aquí en vez de
> dejar que se lea como verificada: la próxima worktree es la que la comprueba, y si algo
> falta se añade en ese momento.

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

#### Y volvió a pasar el 7 de septiembre, por otra puerta: la tubería

La medición concurrente registró `rc=0` para una puerta que había escrito `FALLÓ en la etapa:
suite e2e` en pantalla. No era la puerta. Medido sobre la misma corrida fallida:

| Cómo se lanza | `$?` |
|---|---|
| `node scripts/verificar.mjs` | **1** |
| `node scripts/verificar.mjs 2>&1 \| tee registro.log` | **0** |

**`$?` de una tubería es el código del último comando, no del primero.** El 1 no se pierde:
sigue en `${PIPESTATUS[0]}` en bash, o se recupera con `set -o pipefail`. Pero un wrapper que
guarda el registro de la corrida —que es lo mínimo que hace cualquier wrapper— introduce una
tubería sin que nadie lo piense, y desde ese momento la puerta no puede volver a decir que no.

Es **la misma forma exacta** que el defecto de `kde-inhibit`, con otro mecanismo: algo que
envuelve la puerta convierte un rojo en un verde para quien lo lea con `$?`. Que la misma
enfermedad reaparezca por dos vías distintas en dos días es el argumento de que no es un
descuido, sino una propiedad de envolver comandos:

> **Todo lo que envuelve a la puerta hay que probarlo con una puerta que se sabe roja.**
> No con una verde: una verde no distingue un wrapper que propaga de uno que no.

Y la comprobación es de una línea, sin esperar a que falle nada de verdad:

```bash
bash -c 'false' ; echo "directo: $?"          # 1
bash -c 'false' | cat ; echo "tubería: $?"    # 0  ← si aquí sale 0, tu wrapper es ciego
```

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

**Las siete ramas se provocan solas, dentro de la puerta**, antes de la etapa 1. Si alguna
calla, la puerta **no arranca**:

```
FALLÓ en la etapa: veredicto
  2 de 7 ramas del veredicto no dispararon al provocarlas.
  La puerta no arranca: el aparato que dice de quién es un rojo está roto,
  y un veredicto roto engaña más de lo que cuesta un rojo.
```

No es una comprobación gratuita ni cara: las provocaciones son sintéticas y no hacen entrada ni
salida. Medido, el comando entero tarda ~95 ms, de los que ~75 son arranque de Node —que la
puerta ya paga— y ~25 la llamada a `journalctl`, que en el preflight no se hace.

**Y el comando sigue existiendo**, para lo que dentro de la puerta no cabe: enseñar lo que
escribe cada rama, y ejercitar las **sondas reales**, que sí dependen de la máquina.

```bash
SILLAR_VERIFY_AUTOPRUEBA_VEREDICTO=1 node scripts/verificar.mjs
```

Esa segunda mitad se queda fuera del preflight a propósito: al arrancar la puerta, la ventana
del diario tiene segundos, así que la sonda de suspensión responde «no pude» con toda la razón.
Dentro imprimiría esa alarma en **cada corrida sana**, y una alarma que suena siempre se deja de
leer — la misma enfermedad, por el otro extremo.

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

### 5 · La identidad de una worktree se deriva de su directorio

*7 de septiembre de 2026 · reescrito al derivarla. Antes se titulaba «La identidad e2e va
separada por worktree» y describía cómo separarla a mano.*

Cada árbol tiene su propio stack de desarrollo y su propio stack e2e, y **nada de lo que los
identifica se escribe**: sale del nombre del directorio, en `scripts/identidad.mjs:106`.

```bash
node scripts/identidad.mjs
```

enseña la identidad de este árbol, la de todos sus hermanos, y **avisa si dos comparten
offset**. El offset es un hash del sufijo (`scripts/identidad.mjs:83`), así que dos nombres
distintos pueden caer en el mismo número: pasa poco, no pasa nunca, y por eso hay una
comprobación en vez de una esperanza. Se arregla renombrando un directorio.

| Papel | Puerto | Quién lo pone |
|---|---|---|
| PostgreSQL de desarrollo | `55600 + offset` | `.env`, escrito por `scripts/estrenar.mjs:42` |
| API de desarrollo | `55700 + offset` | ídem |
| pgAdmin | `55800 + offset` | ídem |
| PostgreSQL de la suite e2e | `55900 + offset` | el entorno del proceso, `e2e/setup/env.ts:112` |
| API de la suite e2e | `56000 + offset` | ídem |
| Vite de la suite e2e | `56100 + offset` | ídem |

**El mapa es regular a propósito.** Antes no lo era —desarrollo en 55430 y e2e en 55432, dos de
distancia— y con dos de margen ningún desplazamiento cabe sin solaparse. Ahora el offset de un
árbol es el mismo número en los seis puertos: si su Vite e2e está en el 56120, su API de
desarrollo está en el 55720. El árbol base, el que se llama `SILLAR` a secas, se queda con el
offset 0, que es lo que permite citar puertos concretos en un documento sin mentir.

**Los dos stacks se identifican igual pero no se sirven igual, y la asimetría tiene motivo.**
El e2e no escribe la identidad en ningún archivo: el arnés se la pasa a `docker compose` por el
entorno del proceso, que **gana al `--env-file` en la interpolación** —medido el 6 de septiembre
de 2026 con `POSTGRES_PORT=59999 docker compose --env-file e2e/.env.e2e config`, que imprimió
`published: "59999"`—. El de desarrollo sí acaba en un archivo, porque lo levanta una persona
escribiendo `docker compose up -d` y ahí no hay ningún proceso en medio que pueda calcular
nada. Pero **el archivo no lo escribe la persona**: lo escribe `scripts/estrenar.mjs`.

Y en `e2e/setup/docker.ts:21-29` toda llamada a compose pasa por dos envoltorios que llevan ese
entorno puesto, en vez de repetirlo en diez sitios. Es la misma lección que metió la guarda
dentro de `composeDown()`: una precaución que hay que acordarse de repetir ya falló una vez.

**Qué había aquí antes.** Cinco valores en `e2e/.env.e2e` que cada worktree tenía que editar a
mano, sin commitear —«la identidad es de la worktree, no de la rama»—, más cuatro en `.env`. Y
el defecto no era que fueran nueve: era **cuál** se olvidaba.

> El quinto valor era el `Port=` de dentro de `ConnectionStrings__Default`, y era el que
> siempre se quedaba atrás. Nunca fue un quinto valor: era `POSTGRES_PORT` otra vez, duplicado
> dentro de una cadena. **Que fuese justo ése el olvidado era la señal de que no debía
> escribirse.** Un valor que aparece en dos sitios se olvida en uno.

Con dos añadidos que la lista vieja no nombraba y colisionan igual: `API_PORT` y `PGADMIN_PORT`.
Un árbol que seguía el documento al pie de la letra seguía chocando — y el 7 de septiembre de
2026 el contenedor `sillar_api` de una worktree **ya borrada** seguía ocupando el 5080.

> **Este hallazgo llegó a afirmar algo falso, y el mecanismo que describía era la causa.** Hasta
> el 5 de septiembre decía que tres worktrees tenían identidad propia. La tenían cuando se
> escribió y la perdieron después, porque la identidad vivía sin commitear a propósito —para
> que no viajara a `main`— y desaparece en cuanto alguien limpia el árbol o restaura
> `.env.e2e`. Un documento cuyo contenido caduca por el mismo procedimiento que documenta no se
> arregla actualizándolo. Estaba abierto como pendiente propuesto, el **20**, en
> `docs/PENDIENTES-CLASIFICACION.md`; se disuelve aquí, que era la decisión del líder: no
> ampliar la vigilancia, quitar la causa.

**El caso que lo demostró sin que nadie fallara.** `sillar-demo` copió `.env.example` y arrancó,
exactamente como el documento mandaba, y recreó el stack de desarrollo compartido sobre el
puerto de otro árbol. No hizo nada mal. Mientras la identidad se escriba a mano, cada worktree
nueva es una bomba, y quien la ceba es el documento.

**Y el 5 de septiembre dejó de ser un margen teórico.** Dos frentes lanzaron la puerta con
dieciséis segundos de diferencia. El segundo murió con `is already used` en el 55173 — que es
la parte inofensiva. La peligrosa no llegó a ocurrir por esos segundos: `composeDown()` lleva
`-v`, así que el que llega segundo **destruye el stack del primero a mitad de suite**,
contenedores y volumen, y la corrida ajena muere con un fallo que no se parece a su causa.

**Eso ya no puede pasar.** `composeDown()` mira de quién es el stack antes de destruirlo, por
la etiqueta que docker compose pone en cada contenedor
(`com.docker.compose.project.working_dir`), y **si es de otra worktree no lo toca**:

```
[e2e] NO se destruye el stack, porque no es de esta worktree.
  El stack e2e ya está en pie, y lo levantó OTRA worktree:
    /home/JP777/sillar-estreno
```

La guarda vive en `composeDown()` y no en quien la llama, y eso también costó una provocación:
la primera versión estaba en `global-setup` y **no servía** — `globalTeardown` se ejecuta igual
cuando `globalSetup` lanza, medido y no supuesto, y remataba el trabajo un segundo después. La
guarda va en la operación destructiva, no en uno de sus llamadores.

**Y una nota para quien lea el README de `e2e/`:** `e2e/README.md:18-21` presenta 55432/55081/55173
como *los* puertos de la suite. Era cierto cuando había una sola worktree, y hoy no lo es en
ninguna: los puertos salen del offset del árbol. Es el mismo patrón que `PENDIENTES.md` §14
describe —«la regla que era cierta porque solo había uno»— y por eso conviene leer aquel
párrafo como lo que era el día que se escribió, no como la lista vigente.

**Lo mismo vale, y esta vez sobre una decisión buena, para que `e2e/.env.e2e` esté
versionado.** Commitearlo fue correcto el día que se decidió: había una sola worktree, el
archivo no guardaba ningún secreto real —solo la contraseña de una base efímera que `down -v`
destruye—, y versionarlo era lo que hacía que la suite arrancara sin ceremonia en cualquier
máquina. Lo que caducó no fue el criterio: fue el mundo en el que se aplicaba. La segunda
worktree convirtió un archivo compartido en una identidad compartida. El archivo sigue
versionado, y ahora puede estarlo sin daño, porque ya no lleva identidad dentro: lo que
registra su cabecera no es «nos equivocamos», es «cambió el supuesto».

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

**Si la carpeta ya existe, lo que decide no es si se puede abrir: es quién es el dueño.** Y
esto costó un rojo, porque la primera versión de la guarda se equivocaba justo aquí. Daba por
hecho que un `chmod` denegado significaba «la creó docker como root». No: significa «no soy el
dueño». Si el dueño es el UID **1654** —el de la API—, la carpeta está **mejor** que si fuera
nuestra: el proceso que escribe dentro es su propietario. Es el caso normal de cualquier
worktree con corridas anteriores al arreglo.

Los tres estados y lo que hace el arnés con cada uno:

| Estado en el disco | Qué hace |
|---|---|
| No existe | La crea y la abre en escritura. Es el caso de una worktree nueva |
| Existe y es del UID 1654 | **La deja como está.** El que escribe dentro es el dueño |
| Existe y es de otro —`root`, típicamente— | Falla, dice de quién es y da el `sudo rm -rf` |

Lo cazó la puerta la primera vez que corrió con la guarda dentro de `sillar-fx`, cuya carpeta
era `1654:1654` desde el 2 de septiembre. Una guarda que bloquea lo que funciona es peor que
no tenerla: la que calla te deja seguir, ésta te para en falso.

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
- `.env.example` decía arriba del todo **cuáles eran las cuatro claves que identifican al
  árbol**, para que se editaran a mano. Eso duró hasta el 7 de septiembre de 2026: eran seis,
  no cuatro, y una de ellas era un puerto repetido dentro de una cadena. Ahora ese bloque dice
  otra cosa —`node scripts/estrenar.mjs`— y las escribe el guion. Ver el hallazgo 5.

  Vale la pena guardar la forma del error, porque no era pereza al contarlas: la lista estaba
  incompleta **en la dirección que no se nota**. Las cuatro nombradas rompían ruidosamente al
  chocar; las dos que faltaban, `API_PORT` y `PGADMIN_PORT`, solo chocan si el vecino levanta
  el perfil `full` o pgAdmin, que es a veces. Una lista se queda coja por sus casos raros.

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
| Configuración | `.env` de la raíz, escrito por `scripts/estrenar.mjs` | derivada del directorio, por el entorno del proceso (`e2e/setup/env.ts:112`) |
| Nombre de proyecto | `sillar` más el sufijo del árbol | `sillar_e2e` más el sufijo del árbol (`e2e/setup/env.ts:64`) |
| Servicios | `db` (`docker-compose.yml:2`) | `db` **y** `api`, perfil `full` (`:51,62`) |
| Base | `sillar_dev`, con el sufijo del árbol | `sillar_e2e` con el sufijo, y se destruye con su volumen al terminar |
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
