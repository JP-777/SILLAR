# Antes de empezar un módulo

> **Esto no es una lista de tareas. Son las preguntas que salen baratas el
> primer minuto y caras la tercera semana.**
>
> Existe porque tres cosas de esta clase vivían en `docs/PENDIENTES.md`, que es
> una lista titulada *lo que falta* — y ahí nadie las lee el día que sirven. Una
> condición sobre el próximo módulo no es un pendiente: es algo que hay que
> tener delante **al empezar**, no algo que alguien tenga que acordarse de hacer.

Creado: 5 de septiembre de 2026 · Última verificación: 6 de septiembre de 2026 ·
Commit verificado: `d617688`.

---

## 1 · ¿Esta regla es cierta porque el mundo es así, o porque hoy solo hay uno?

Se hace **al escribir la regla**, que es cuando cuesta diez segundos.

En una sola semana aparecieron tres defectos con la misma forma, y ninguno se
parecía a los otros hasta que se pusieron en fila. **Las tres citas son
históricas: describen el código tal como estaba a finales de agosto de 2026, y
los tres están corregidos.** Quien abra hoy `PublicSite.tsx` no encontrará el
defecto — encontrará el arreglo.

| Dónde | La regla | Cierta hasta |
| --- | --- | --- |
| `platform/PublicSite.tsx` | `secciones.length === 0` decidía «aquí no hay nada» | que un módulo pudiera estar **activo y sin publicar** |
| `zz-desmontaje.spec.ts`, `movil-teclado.spec.ts` | «no queda rastro de M01», preguntado por la subcadena **«Productos»** | que otro módulo usara esa palabra con toda la razón |
| `zz-instalacion.spec.ts` | una comprobación **de plataforma** con nombre de módulo | que hubiera un módulo cuyo nombre no salía en el título |

Las tres eran **correctas el día que se escribieron**. Las tres dejaron de serlo
por lo mismo: contaban con que solo hubiera un módulo publicable, un módulo con
pantallas, un módulo con cuerpos en Swagger.

**Y no fue casualidad de M02: M02 fue el primer segundo módulo.** La cuenta va a
subir. M04 trae la segunda identidad —dos cookies, dos esquemas de
autenticación—, M07 el segundo consumidor del contrato de M01, y M18 va a usar la
palabra «Productos» en sus etiquetas. **Cada uno es el primer segundo de algo.**

**Tres señales de que estás delante de una:**
contar elementos para deducir un estado, en vez de preguntarle a cada uno;
afirmar una ausencia por una palabra, en vez de por lo que identifica al sujeto;
y ponerle a algo transversal el nombre del único que lo usa hoy.

*(Viene de `PENDIENTES.md` §14, que era una lección y no un pendiente.)*

---

## 2 · ¿Alguna vez he visto a esta barrera decir que no?

**Una barrera que calla no se distingue de una barrera que funciona.** Si nunca
ha disparado, no sabes que funciona: sabes que está callada.

Cuatro casos reales, y los cuatro llevaban tiempo:

| La barrera | Por qué no protegía |
| --- | --- |
| El inhibidor de suspensión | Estaba escrito. La receta estaba mal: en este equipo manda KDE PowerDevil |
| La detección de suspensión | Estaba escrita. Preguntaba al diario **cinco horas en el futuro** — `toISOString()` da UTC y `journalctl` lee local |
| `ReactivacionRedSocialTests` | Dos pruebas escritas. Exigían una base que en su etapa no existía: **no podían correr nunca** |
| La guarda de `.media-e2e` | Recién escrita. **Paraba en falso**: confundía «no soy el dueño» con «la creó root» |

**La regla:** una barrera nueva **se provoca una vez a propósito y se observa
disparar, y también dejar pasar**, antes de darla por puesta.

Las dos direcciones importan. Una barrera que calla te deja seguir; una que para
en falso te para. Son la misma enfermedad — no haberla provocado.

**Y la prueba que comprueba la barrera también se rompe a propósito.** No basta con ver verde su autoprueba: se introduce deliberadamente el defecto que esa autoprueba debería detectar y se comprueba que ella misma se pone roja. Una barrera sin esa falsificación puede estar protegiendo solo la versión sana de sí misma.

**Y una barrera que solo se puede provocar levantando medio sistema es una
barrera que nadie va a provocar.** Por eso la decisión se escribe aparte y pura,
sin disco ni red, y el llamador solo la consulta.

---

## 3 · ¿La guarda está en la operación, o en quien la llama?

**Poner la guarda en un llamador protege de ese llamador. Ponerla en la operación
protege de todos, incluidos los que todavía no existen.**

Es fácil razonarlo al revés, porque el llamador es donde se entiende la intención
y la operación es donde solo se ve el mecanismo. Pero **la intención se duplica y
el mecanismo no**.

Se descubrió provocando, no razonando: una guarda puesta en `global-setup` dejaba
morir el stack ajeno igual, porque `globalTeardown` corre también cuando
`globalSetup` lanza y llamaba a la operación destructiva sin preguntar.

---

## 4 · ¿Lo he comprobado, o me lo ha dicho algo que responde sin fallar?

**Preguntar por otra vía.** Una herramienta que falla se ve; una que responde mal
no. Casos reales, todos cazados comprobando el mismo hecho por un camino
distinto:

| Lo que respondía | Lo que era |
| --- | --- |
| `journalctl --since` con fecha UTC | Cero líneas siempre — pedía el diario en el futuro |
| `find -newermt` | «Cero archivos de hoy» sobre un directorio con 67 |
| Un comentario del propio repositorio | Afirmaba que `globalTeardown` solo corre si `globalSetup` terminó. Llevaba meses siendo falso |
| El veredicto de la puerta | Señaló el refactor cuando la causa era que la base no estaba en su puerto |
| Una búsqueda que no encontró nada | «La tabla no existe». Estaba en `PENDIENTES.md` §11 bajo otro encabezado, y una clasificación del propio repositorio ya la citaba por su número |
| Una búsqueda que **sí** encontró algo | Cuatro documentos emparejaron `es_ci`. Ninguno contenía la regla — solo su aplicación |

El tercero merece atención: **una afirmación escrita en el repositorio es una
respuesta como cualquier otra, y se comprueba igual.** No tiene ningún estatus
especial por estar en un comentario.

Y los dos últimos son los más traicioneros, porque son la misma herramienta
fallando en las dos direcciones.

**Una búsqueda que no encuentra nada es una respuesta igual de falible, y encima
convincente:** el «no hay» se siente como un hecho comprobado cuando solo es un
término que no coincidió. La otra vía casi siempre existe — allí era abrir la
sección que otro documento ya citaba por su número.

**Y un acierto es peor, porque se siente doblemente comprobado:** hay resultados,
hay ficheros, hay líneas. Pero emparejar un término no es leer un documento. Los
cuatro ficheros existían, contenían la palabra, y ninguno contenía la regla.

**Ni el fallo ni el acierto de una búsqueda son una respuesta. Abrir el fichero lo
es.**

---

## 5 · Si el módulo escribe auditoría: nombra la fila, no la clase

**Un resumen debe decir a *cuál*, no solo *qué clase de cosa*.**

«Alta de un enlace social.» obliga a desplegar el detalle y llevarse un
identificador a otra pantalla. «Alta de la red social Instagram.» se lee de un
vistazo, y el identificador deja de hacer falta casi siempre.

**El módulo nuevo nace ya nombrando la entidad concreta.** Los productores que ya
existen se ponen al día cuando se toquen por otra razón: **no hay barrido**, y
encargarlo sería justo lo que este punto evita.

Y no se confunda con quitar un identificador técnico que se estaba presentando
—eso es otra dirección—: aquí se **añade** el nombre humano de la fila.

*(Viene de `PENDIENTES.md` §16, cuyo disparador es literalmente que alguien lo
lea al empezar un módulo.)*

---

## 6 · Si el módulo va a traer una dependencia

**Una biblioteca es una dependencia: no entra sin decidirlo**, y se toma la idea
antes que el paquete — casi siempre lo que gusta de una biblioteca son unas pocas
decisiones que se pueden copiar sin heredar su superficie.

**Las dos cosas que ordenan la lista, y valen más que la lista:**

**La conclusión.** El movimiento se hace **con la plataforma**: View Transitions,
`@starting-style`, transiciones de `display`, esqueletos de CSS. Cubren los casos
aparecidos y degradan solos. `prefers-reduced-motion` se impone **una vez**, en
`frontend/src/shared/styles/base.css:82`.

**La grieta.** Esa protección es CSS, así que **no alcanza a lo que anima por
JavaScript**. Una biblioteca que mueva cosas desde JS tiene que respetar la
preferencia por su cuenta y reaccionar a sus cambios; si no lo hace, no hay forma
de arreglarlo desde fuera.

Sin esas dos, seis «noes» sueltos invitan a discutir el séptimo caso por caso.
Con ellas, la séptima propuesta se resuelve en un minuto.

**Lo ya evaluado y descartado**, para que nadie lo reabra sin saber que se estudió:

| Qué | Por qué no |
| --- | --- |
| **Morphicons** | Ignora `prefers-reduced-motion` por defecto: hay que corregirlo en cada uso, y un día se olvida |
| **Sileo** | **El artefacto publicado no lleva el texto de la licencia** — esa sola basta, y es la que se transfiere a cualquier otra. Además: colores propios, y la animación retrasa la acción |
| **Auragradients** | No es una biblioteca: es una técnica de menos de diez líneas de CSS |
| **react-loading-skeleton** | Se reproduce con poco CSS. Si se escribe en un rato, no es una dependencia |
| **Motion** | Arranca con `prefers-reduced-motion` desactivado. Mismo motivo que Morphicons |
| **AutoAnimate** | Estuvo cerca: aporta un FLIP difícil a mano. Cae porque solo consulta la preferencia al inicializar y, al animar por JavaScript, la protección global de CSS no lo alcanza |

**Y los saneadores de SVG**, por otra vía: sanear SVG exige una biblioteca con
historial de evasiones, y un SVG es XML que puede contener scripts servidos desde
el mismo origen que el panel. Si algún día hace falta vectorial, la salida es
servir los medios desde otro origen, no sanear.

**Alcance de esta lista.** Los informes que la sustentan viven en
`SILLAR-DISENO/investigacion/`, **fuera del repositorio** (`PROTOCOLO-DISENO` §7).
Así que estas entradas no son un resumen incompleto: son **el único registro
dentro del repositorio de decisiones cuyo sustento está fuera**. La lista recoge
lo verificable aquí dentro. **Disparador para completarla:** la próxima vez que
alguien proponga una dependencia y haya que ir a mirar si ya se estudió.

*(Viene de `PENDIENTES.md` §11, «Bibliotecas evaluadas y descartadas».)*

---

## 7 · Antes del primer `CREATE TABLE`

Dos condiciones que solo son gratis **antes** de escribir la primera migración.
No se responden aquí: se enrutan.

**¿Tu módulo guarda texto en columnas con colación no determinista?**

`core.es_ci` y `core.es_search` se definen en CORE y **atan a todos los módulos**.
Son no deterministas, y sobre las columnas que las llevan **PostgreSQL rechaza
`LIKE`, `ILIKE` y las expresiones regulares**. La salida obligatoria es
`COLLATE "C"` **en la expresión**, con índice trigram que use **esa misma
expresión**.

→ Qué compara cada una: `docs/modules/core/SPEC.md:44-72`.
→ La salida en uso, como ejemplo trabajado: `docs/modules/crm/DATOS.md:102`.

**Esta regla no está recogida como regla en ningún otro sitio; hoy este documento
es el único.** Cuando exista ADR-020 se traslada allí y esta sección vuelve a
enrutar.

Cuesta un minuto saberlo; costó un bloqueo a mitad de `CrmInitial`, descubierto
porque PostgreSQL se negó — no porque nadie lo previera.

**¿Tu módulo tiene tablas replicadas?**
`uuid` v7 generado por la aplicación, `origin_node`, `row_version`, y **una tabla
replicada no puede referenciar a una no replicada**. → Ver ADR-016 y ADR-018
**antes** de escribir la primera migración. Una migración equivocada aquí no se
corrige barata.

## 8 · Antes de correr nada

La lista de estreno de worktree, los recursos que se comparten en la máquina y el
turno de la puerta están en `docs/ENTORNO.md` y en `docs/DIVISION-DE-TRABAJO.md`.
No se repiten aquí: **un dato en dos sitios es un dato que va a discrepar.**
