# Propuesta de clasificación de `PENDIENTES.md`

*4 de septiembre de 2026 · leído contra `3b6806d` · revisado el 5 de septiembre contra
`b53e5ee`, que es el árbol al que se integra*

> **Qué cambió entre las dos lecturas.** `main` cerró la 2 —el footer existe— y abrió la 18.
> Las dos entradas están reclasificadas abajo, y el recuento de la cabecera es el nuevo. Todas
> las citas de archivo y línea se volvieron a comprobar contra `b53e5ee` y siguen en su sitio;
> ninguna otra entrada cambió de texto.
>
> **Y tres entradas se movieron después de clasificarlas, por trabajo de este mismo frente.**
> Están señaladas en su fila, pero conviene saberlo antes de leer la tabla: **la 3, la 8 y la
> fila del `42P01` de la 13** ya no dicen lo que decían el 4 de septiembre. Ninguna se cierra
> aquí —eso sigue siendo del líder— pero la propuesta que se fusione tiene que ser la de abajo
> y no la de la primera lectura.

**Qué es esto.** Una lectura ordenada de las 18 entradas de `docs/PENDIENTES.md` y de las
sublistas de la 11 y la 13, agrupadas por lo único que decide qué hacer con ellas: si siguen
vivas, si un hecho posterior ya las resolvió sin que nadie fuera a tacharlas, y si tienen
disparador.

**Qué NO es.** No cierra nada, no borra nada, no renumera nada y **no le inventa disparador a
ninguna**. Sí **propone abrir una**, la 19 del grupo F, y solo porque el líder la encargó con
su contenido y su disparador: no salió de clasificar. Un pendiente sin disparador se marca como tal y vuelve al líder; ponerle uno
plausible sería justo el error que `PENDIENTES.md` advierte en su cabecera, con el agravante de
que quedaría escrito como si alguien lo hubiera decidido.

**Quién la aplica.** No este frente, y ya no por el mismo motivo: el footer cerró el 4 de
septiembre, y `PENDIENTES.md` pasó a ser del líder. Los grupos **C** y **D** los aplica él.
Este archivo es material para esa aplicación, no la aplicación.

**Lo que sí se ha hecho aquí:** ir a mirar. Seis entradas afirmaban algo comprobable contra el
árbol —dos filas de la 11, dos de la 13, la 2 y la 3— y se comprobó una por una, citando archivo y
línea en la fila que le corresponde. **Tres de las seis ya no eran ciertas**, y las tres estaban
escritas como si lo fueran. El resto de las entradas no afirma nada verificable: dice cuándo
tocaría mirarlas, y eso no se comprueba leyendo código.

De las seis, la de la 2 ya no es una comprobación sino historia: lo que el 4 de septiembre eran
cinco ficheros sin commitear en `sillar-footer` está hoy en `main`, y la entrada es una lápida.
Quedan cinco vivas, y las tres falsas siguen siendo tres.

---

## A · Resueltas por hechos posteriores — nadie fue a tacharlas

Ninguna se cierra aquí. Se señala qué habría que verificar antes de cerrarlas, y con qué se
comprueba.

| | Entrada | Lo que dice | Lo que hay hoy |
|---|---|---|---|
| **11** | `.design-sync/config.json` no incluye a `ModuleCard` | «16 de 17 vistas previas listas» | **Está incluido.** `.design-sync/config.json:10`, `:28` y `:32`. Coincide con el cierre que la propia 13 registra el 18 ago —«son **18 componentes** […] más `ModuleCard`»— así que la fila de la 11 quedó atrás cuando se cerró la de la 13 |
| **11** | Falta `E2E_KEEP_STACK` | «una variable que conserve la base levantada al fallar» | **Existe y está documentada.** `e2e/setup/global-teardown.ts:23-25`, `e2e/setup/env.ts:57` y `e2e/README.md:49` |
| **13** | Tu `.env` local está desfasado | «le faltan `API_PORT` y `MEDIA_PATH`» | **Los dos están.** Pero la condición general no: `.env.example` declara hoy cuatro claves que el `.env` local no tiene —`Sillar__PublicBaseUrl`, las dos de `Sillar__Proxy__*` y `SILLAR_SMTP_PASSWORD`—. **La entrada tal como está escrita está resuelta; el problema que describía, no.** Reescribirla o cerrarla y abrir otra es decisión del líder |

**Y una observación sobre la 11 que no es una fila suelta.** Su primera fila —«Resincronizar
el sistema de diseño»— remite a «la tabla de arriba», y esa fila **está tachada como cerrada
el 18 de agosto** en la 13. La 11 dice además que es «el que bloquea el paso 4». O el bloqueo
se levantó y nadie lo anotó, o sigue en pie por una razón que ya no es la que la fila dice.
Dos de las seis cosas de la 11 resultan estar hechas: conviene releerla entera antes que fila
a fila.

---

## B · Vivas, con disparador — no hay nada que decidir, solo que llegue el día

Ordenadas por cercanía del disparador, que es el único orden que sirve para una lista así.

> Dos filas de esta tabla —la **8** y la **3**— se atendieron el 5 de septiembre, después de
> escribirla. Se dejan donde estaban en vez de moverlas al grupo A: **el grupo A es para lo que
> resolvió un hecho ajeno sin que nadie fuera a mirarlo**, y esto es lo contrario, trabajo hecho
> a propósito y a la vista. Mezclarlas borraría justo esa diferencia.

| | Entrada | Disparador | Estado del disparador |
|---|---|---|---|
| **17** | Etiquetas visibles de `entityType` | «Entra en la revisión de pendientes previa a la división oficial» | **CUMPLIDO**, y así está escrito. 20 tipos medidos. Es la más madura de la lista |
| **8** | Falsos hallazgos por ruido de máquina | «La tercera vez» | **CUMPLIDO Y SOBREPASADO, y atendido el 5 sep** — ver abajo |
| **18** | El pie se entregó sin pasar por diseño | «Cuando diseño se reactive» | Vivo, y **nace del cierre de la 2**: entregar el footer no cerró el agujero que la 2 advertía, así que se separó en entrada propia en vez de cerrarse con ella. Bien hecho: es lo contrario de lo que le pasó a la 11 |
| **15** | El mapa del negocio no tiene módulo dueño | «Antes de cerrar la Fase 1, o cuando alguien pregunte por qué la web no enseña dónde está la tienda» | Vivo. La segunda mitad del disparador puede saltar cualquier día |
| **16** | Los resúmenes de auditoría no nombran la fila concreta | «El próximo módulo que escriba auditoría nace ya nombrando la entidad» | Vivo, y es de los que no piden barrido: se cumple solo si quien escribe el próximo módulo lo lee |
| **9** | Cómo llegó `cms` a la base del MVP | «La próxima fila de módulo que aparezca sin que nadie la escriba» | Vivo, en vigilancia. El rastro ya está armado para la próxima |
| **4** | M05a Servicios puede no llegar a existir | «La visita al mostrador» | Vivo. Depende de una visita, no de código |
| **3** | Tres copias de `StampReplicationColumns` | «La cuarta copia, **o** la primera vez que dos copias discrepen» | **Disparador NO cumplido, y aun así hecho el 5 sep.** Al clasificar se comprobó que seguían siendo **tres** con los cuerpos idénticos salvo `clock` / `_clock`. Se extrajo igual, como **adelanto explícito**, en `refactor/sellado-replicacion`; el motivo está en `BITACORA.md` §7. **Que la fila se cierre o se reescriba es del líder**, y conviene que quede escrito que no la cerró su disparador |
| **5** | M18 Campaña Escolar sin SPEC | «Después de M03, en Fase 2» | Vivo, lejos |
| **6** | `b2b.quotes` polimórfica | «El tercer origen cotizable, o el SPEC de `quotes` de fase 2» | Vivo, lejos |

### La 8 merece un párrafo aparte

Su disparador era «la tercera vez». Van **cuatro**, y la cuarta es de otra clase: la primera
que **sobrevive a la protección** que se usaba contra las anteriores. Las cuatro están
inventariadas y con su forma de reconocerlas en `docs/ENTORNO.md`, que no existía cuando se
escribió la 8.

Eso cambiaba la entrada de sitio, y el 4 de septiembre aquí se escribió que la mitad que
faltaba —«que el arnés lo diga por su nombre», sin depender de que alguien lea el log con
criterio— **no estaba hecha**. El 5 de septiembre se hizo: la puerta escribe un veredicto
debajo del `FALLÓ`, con la evidencia en que se apoya, y **calla cuando no puede atribuirlo**.
Está en `fix/puerta-atribuye-rojo`.

**La 8 se cierra con eso, y la mitad que yo daba por perdida no lo estaba.** Aquí se escribió
que lo que faltaba —saber si el mismo rojo sale en `main`— no se podía responder desde dentro
de una corrida y costaba otra entera. **La premisa era falsa.** Solo cuesta una corrida si se
responde corriendo, y no hace falta: por la regla 4 de la división nada entra en `main` sin
pasar la puerta en su rama, y solo Integración fusiona. `main` está verde **por construcción**,
y se sabe con qué commit y cuándo se comprobó. Lo que falta no es una corrida: es que ese hecho
quede escrito. Sale como entrada propia, la **19**, más abajo.

Y una quinta ocurrencia, del mismo día, que conviene anotar porque la produjo este frente: una
corrida sobre un árbol limpio dio 4 fallos de 126 por tener otra puerta corriendo a la vez en
otra worktree. No es una causa nueva —es la 1 de `docs/ENTORNO.md`, la máquina ocupada— pero sí
la primera vez que la provoca **la propia división en dos frentes**, que es el escenario para el
que se está preparando todo esto. De ahí salió la detección de saturación del veredicto.

---

## C · Vivas y SIN disparador — vuelven al líder

**Esto es lo que hay que mirar de esta clasificación.** Una entrada sin disparador no tiene un
día en que toque, así que no se hace nunca y tampoco se descarta nunca: ocupa sitio y no avanza.
Ninguna se cierra ni se le inventa fecha.

| | Entrada | Por qué está aquí |
|---|---|---|
| **11** | *El conjunto* | Lo dice ella misma: «**Sin disparador definido para el conjunto**». Con dos de sus seis filas ya hechas (grupo A), lo que queda es más pequeño de lo que aparenta y quizá sí admita uno |
| **11** | El selector de categorías N:M no existe | Bloqueado en diseño: «pide pasar antes por el paso 3.5». Un bloqueo no es un disparador — dice qué falta, no cuándo mirarlo |
| **11** | `BUILD_CONFIGURATION=Debug`: ¿alcanzable en la imagen de producción? | Es una **pregunta de hecho**, no trabajo. Se responde leyendo `backend/Dockerfile` y citando línea; cuesta minutos. No se responde aquí porque responderla es hacerla, no clasificarla |
| **11** | `:focus-visible` en diálogo con clic de ratón | De las que solo se resuelven en un navegador de verdad. Es de lo de JP |
| **13** | Arranque con base vacía revienta con `42P01` | Estaba aquí por no tener cuándo. **El líder le subió el rango el 5 sep y está arreglado** en `fix/arranque-base-vacia`, verificado arrancando contra una base vacía. Sale del grupo C: ya no le falta disparador, le falta que alguien decida si la fila se cierra |
| **13** | Probar el aborto de la ADR-019 en vivo | «Es lo único que la decisión promete» y sigue sin probarse |
| **13** | La búsqueda no encuentra por prefijo | Medido y descrito con precisión. Es trabajo de tamaño conocido y **sin fecha**: toca los tres servicios de M01 |
| **13** | El paquete lleva código de módulos no licenciados | Declarado «asunto de licenciamiento, no de arquitectura». El disparador natural sería el día que se decida el licenciamiento, pero **eso no está escrito** y no lo escribo yo |
| **13** | `MultipleCollectionIncludeWarning` | «Molestia declarada, no defecto», con la cota medida. Es candidata a **descartarse**, no a hacerse — pero descartar también es decidir |
| **13** | Verificación visual del panel y repaso visual de Swagger | Las dos piden un navegador. Es de lo de JP, y la 13 ya lo dice |
| **13** | Borrar `docs/BITACORA-SESION-2026-08-14.md` | El archivo sigue ahí. Es un borrado de un archivo, no un trabajo: sin disparador porque no lo necesita, solo que alguien lo haga |
| **13** | Tipografía y logo · Dominio · Nombres comerciales · Datos de Bsale | Cuatro cosas de negocio, ninguna con fecha. Las tres primeras son de JP; la cuarta cuelga de la guía de observación, igual que la 4 |

---

## D · No son pendientes, y conviene que se note

| | Qué es | Qué hacer con ello |
|---|---|---|
| **14** | Una **lección**, y lo declara: «no lleva disparador porque no hay nada que hacer — hay algo que mirar» | Dejarla. Pero está en una lista titulada «lo que falta», donde nadie la va a leer el día que sirve. Su sitio natural sería `BITACORA.md` o la cabecera de `ARQUITECTURA_MODULAR.md`. **Es una propuesta, no una decisión** |
| **1, 2, 7, 10, 12** | Lápidas: número conservado, contenido borrado, resolución en `BITACORA.md` §7. La **2** se sumó el 4 de septiembre y trae la mejora: además de la lápida, dice **qué parte de lo que advertía sigue viva** y dónde —en la 18— | Quedarse como están. La razón está escrita en cada una y es buena: hay referencias externas y un número que cambia de dueño es peor que un hueco |
| **11** | La tabla de bibliotecas descartadas | Lo dice ella: «no es un pendiente: está aquí para no volver a investigarlas». Es una decisión de arquitectura razonada y **está en el archivo equivocado** — un pendiente se borra al resolverse, y esto no se debe borrar nunca |
| **13** | Las seis filas tachadas | Se conservan por una razón explícita: «son de otros, y recortar el registro de alguien no es de quien lo mueve». **Esa razón caducó**: el traslado fue hace semanas y lo tachado ya está registrado donde corresponde. Sacarlas devolvería la 13 a un tamaño legible |
| **13** | «Aplazados por decisión, no pendientes» | Retención de auditoría, vectoriales, permisos granulares, vencimiento de licencias, marca blanca. Correcto donde está y bien etiquetado |

---

## E · Lo que apareció al clasificar, y no estaba en ninguna entrada

Ninguna de estas cuatro es un pendiente todavía. Se dejan aquí porque salieron de leer el
archivo entero de una sentada, que es algo que no se vuelve a hacer en meses.

1. **La 13 se ha vuelto un cajón.** Diecinueve filas de origen y naturaleza distintos —defectos,
   preguntas de negocio, verificaciones humanas, cosas cerradas— bajo un título que solo dice
   de dónde vinieron. Casi todo el grupo C sale de ella. Partirla por naturaleza haría visible
   lo que hoy tapa el volumen; fundirla con el resto la haría desaparecer.

2. **La misma cosa aparece en la 13 cerrada y abierta a la vez.** «Verificación visual del
   panel completo» está tachada —«Cerrado el 18 ago»— y siete filas más abajo «Verificación
   visual del panel» dice «Sigue pendiente: es lo único que separa a CORE de estar verificado
   de punta a punta». Las dos en la misma tabla. Puede que sean cosas distintas con nombres casi
   iguales, o puede que una se cerrara sin borrar la otra; **desde fuera no se distingue**, y
   eso ya es motivo suficiente para arreglarlo.

3. **Nada distingue lo que es de JP de lo que es de un agente.** Tres filas del grupo C
   necesitan un navegador o una decisión de negocio, y están mezcladas con trabajo de código
   entre las mismas filas. `CLAUDE.md` sí separa esas dos cosas —«Lo que es de JP»—, y la lista
   no.

4. **La cabecera pide una cosa que la lista no cumple.** «Cada entrada lleva su disparador. Un
   pendiente sin disparador es un deseo.» Doce entradas del grupo C no lo tienen. La regla es
   buena; lo que falta es el momento en que se aplica, que sería al escribir cada entrada.

---

## F · Entradas nuevas que se proponen abrir

Dos, y ninguna sale de clasificar lo que había: salen de cerrar la 8 y de un incidente entre
frentes. Se escriben aquí con la forma que tendrían en `PENDIENTES.md` para que fusionarlas sea
copiar, no redactar.

### 19 · El verde de `main` no está registrado en ninguna parte

**Qué pasa.** Con dos frentes, la pregunta cara ante un rojo es «¿esto lo rompí yo o venía
roto?». El veredicto de la puerta responde lo que puede saber solo desde dentro de una corrida
—suspensión, saturación, firmas de entorno conocidas, si la rama toca siquiera el ámbito de la
etapa—, y para lo que queda hay una respuesta que **no cuesta ninguna corrida**: por la regla 4
de la división, nada entra en `main` sin pasar la puerta en su rama, y solo Integración
fusiona. **`main` está verde por construcción.** El problema es que ese hecho no está escrito
en ningún sitio: hoy nadie puede decir con qué commit se comprobó, ni cuándo, sin reconstruirlo
a mano del historial.

**Qué haría falta.** Que Integración anote **en cada fusión** las tres cosas que convierten el
verde en un hecho consultable:

- el **commit** de la rama sobre el que corrió la puerta,
- la **fecha** en que corrió,
- y el **resultado** —`TODO EN VERDE`, o qué se admitió a pesar de qué.

Coste: una línea por integración. Corridas: **cero**.

**Por qué la fecha y no solo el commit.** Porque lo que caduca no es el árbol, es el entorno. Un
verde de hace tres semanas sobre el mismo commit no dice nada sobre una máquina a la que le
cambió KDE, Docker o el kernel entre medias — y cuatro de las cinco causas ambientales
inventariadas en `docs/ENTORNO.md` son exactamente de esa clase.

**Disparador.** El **primer rojo cuyo origen el registro de verde no baste para resolver**. Dos
formas de que eso ocurra, y conviene reconocerlas:

- el registro es **más antiguo que el cambio de entorno** que se está investigando, o
- las cuatro señales del veredicto **vuelven limpias** y el rojo sigue ahí.

Ese día, y solo ese, se paga la corrida de `main` — y se paga sabiendo por qué, que es
distinto de pagarla por costumbre.

---

### 20 · La identidad E2E se pierde sola, y el aislamiento depende de que no se pierda

**Qué pasa.** Cada worktree necesita su propia identidad para correr la suite —
`COMPOSE_PROJECT_NAME`, `POSTGRES_PORT`, `API_PORT`, `FRONTEND_PORT` y el `Port=` de
`ConnectionStrings__Default`, que **no se deduce de los otros cuatro**—. Vive en
`e2e/.env.e2e`, que **sí está versionado** y trae los valores de la worktree principal. Una
segunda worktree lo modifica y **no commitea el cambio**, para que su identidad no viaje a
`main` y se la lleve puesta la siguiente.

Y ahí está el defecto: **lo que no se commitea se pierde solo.** Un `git checkout --`, un
`git stash`, un `git clean`, restaurar el árbol tras un merge — cualquiera de esas cosas
devuelve el archivo a los valores compartidos, en silencio y sin que nadie lo pida. La worktree
sigue funcionando igual de bien **hasta que dos frentes corren a la vez**, que es justo cuando
no hay nadie mirando.

**No es un defecto de la documentación.** `docs/ENTORNO.md` §5 describía el mecanismo con
precisión y aun así se quedó falso: afirmaba que `sillar-footer` tenía identidad propia, que la
tuvo, y que la perdió por este mismo mecanismo. **El documento caducó por lo que el documento
describe.** Corregirlo no arregla nada: volverá a caducar.

**Qué se ha hecho ya, y qué no.** El 5 de septiembre `composeDown()` pasó a mirar de quién es
el stack antes de destruirlo, por la etiqueta `com.docker.compose.project.working_dir` que pone
docker compose. Eso **mitiga la consecuencia peor** —que un frente destruya el stack del otro a
mitad de suite— y convierte una destrucción silenciosa en una parada con nombre. **No resuelve
el defecto:** la identidad sigue perdiéndose sola, los puertos siguen chocando, y el segundo
frente sigue sin poder correr.

**Direcciones posibles, ninguna decidida.** Se apuntan para que quien decida no tenga que
redescubrirlas, no como propuesta:

- **Derivarla del árbol** en vez de escribirla: que `e2e/setup/env.ts` calcule proyecto y
  puertos a partir de la ruta de la worktree cuando `.env.e2e` no los fije. Colisión imposible
  por construcción, y nada que perder porque nada que guardar. A cambio, los puertos dejan de
  ser predecibles y hay que leerlos en cada corrida.
- **Sacarla del árbol**: un archivo por worktree fuera del control de versiones —
  `.env.e2e.local`, ignorado— que `git` no pueda restaurar. Sigue habiendo que crearlo a mano.
- **Dejarla donde está y detectar la pérdida**: que el arnés avise cuando la identidad de esta
  worktree coincide con la de otra. Es lo más barato y lo menos ambicioso.

**Disparador.** El siguiente frente que se añada — el tercero. Con dos, la colisión es una
molestia que la guarda de `composeDown()` convierte en una espera. Con tres, el segundo y el
tercero se bloquean entre sí sin que ninguno de los dos sea el que integra, y la espera deja de
ser una espera para convertirse en una cola sin turno.

**Y una advertencia sobre este disparador**, que no es del tipo que se cumple solo: no salta al
crear la worktree, salta la primera vez que dos de los tres quieren correr la puerta a la vez.
Puede pasar semanas sin saltar y luego saltar tres veces en una tarde.
