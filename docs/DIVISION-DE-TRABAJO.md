# División de trabajo

> **Este documento no entra en vigor hasta que la lista de Fase 1 esté cerrada.**
> Hasta entonces seguimos con un frente, y esto vive escrito e inactivo.

Creado: 5 de septiembre de 2026 · Última verificación: 6 de septiembre de 2026 ·
Commit verificado: `d617688`.

---

## 1 · Se divide por módulo, no por capa

**La arquitectura ya construyó la frontera.** Un módulo tiene su esquema, su
`DbContext`, sus endpoints, su carpeta de frontend y sus specs de e2e, y solo
habla con los demás por `Contracts`. Repartir por módulos hace que la división
coincida con una frontera que el compilador ya vigila.

Repartir por capa —uno el backend, otro el frontend— haría lo contrario: cada
módulo pasaría a ser un fichero compartido, y la regla de que ningún frente toca
lo mismo sería inaplicable desde el primer día.

## 2 · Territorios

| Frente | Composición | Territorio |
| --- | --- | --- |
| **A** | Claude Code, auditado por el líder | **M03 Ventas Online** — completo: backend, frontend y e2e |
| **B** | Chat 3 + JP | **M07 B2B** — completo. Su dependencia dura de M04 ya está en `main` |
| **Integración** | Chat 2 | `main`, la puerta, toda la costura compartida y los documentos compartidos. **Nada más**: con dos frentes, integrar es un trabajo |

## 3 · La costura tiene dueño, y eso no es una prohibición

Esto corrige un defecto que ya costó dos pendientes: §3 llevaba meses sin
arreglarse y el footer público no existía por la misma razón. La regla decía
*nadie toca esto*, y **lo que nadie puede tocar tampoco tiene quien lo arregle**.

Por eso la regla es positiva —esto tiene dueño— y no negativa.

    backend/Sillar.Shared/**            backend/Sillar.Core.Contracts/**
    backend/Sillar.Core/**              frontend/src/platform/**
    frontend/src/app/routes.tsx         frontend/src/app/App.tsx
    e2e/setup/**                        scripts/verificar.mjs
    docker-compose.yml                  .env.example
    database/integrations/**
    docs/BITACORA.md                    docs/PENDIENTES.md
    docs/ARQUITECTURA_MODULAR.md        docs/ROADMAP_MODULAR.md
    docs/ENTORNO.md                     docs/DIVISION-DE-TRABAJO.md
    docs/ANTES-DE-EMPEZAR-UN-MODULO.md

Un frente que necesita un cambio ahí **no lo hace: lo pide**, y lo pide
**describiendo el efecto observable que necesita**, no el cambio que ha
imaginado. Integración lo implementa o lo sube al líder.

**Nunca un retoque «rápido» de paso.** Eso es precisamente lo que la división
existe para impedir.

**Las peticiones de costura tienen prioridad sobre el trabajo propio de
Integración.** La regla 2 pone a Integración en el camino crítico de los dos
frentes: sin esta línea, un frente puede quedarse parado esperando una línea de
`Contracts` mientras Integración termina otra cosa suya.

**Y pedir no bloquea:** el frente sigue con lo demás mientras espera.

## 4 · Las ocho reglas

1. **Un módulo, un frente, durante toda la unidad.** No se alterna por commit.
2. **La costura la escribe Integración.** Los demás piden por efecto.
3. **Cada frente en su rama, con push temprano aunque esté en rojo.** Solo
   Integración fusiona a `main`.
4. **La puerta es el criterio de entrada.** Cada frente la pasa en su rama antes
   de pedir integración. *(Con la restricción física del §6: una a la vez.)*
5. **Dependencia dura = orden.** Un módulo no arranca hasta que aquello de lo
   que depende duro esté en `main`.
6. **Documentación:** la bitácora de un módulo la escribe su frente; los
   documentos compartidos son de Integración. Todos con fecha de creación, fecha
   de última verificación y commit verificado.
7. **Duda de arquitectura o de producto: se para y sube al líder.** No se
   resuelve por inferencia.
8. **Configuración de máquina jamás en un commit de producto.**

## 5 · El repaso de §14 aplicado a estas mismas reglas

*¿Esta regla es cierta porque el mundo es así, o porque hoy solo hay uno?*

**a) «Un escritor por fichero»** era cierta porque cada fichero tiene dueño
evidente por módulo. En la costura no lo tiene. → Por eso la regla 2 es positiva
(*tiene dueño*) y no negativa (*nadie toca*).

**b) «La puerta es el criterio»** era cierta con un frente. Con dos, un rojo
ajeno bloquea a los dos y cada frente paga el tiempo de las pruebas del otro sin
poder hacer nada. → Por eso §8 dejó de ser un pendiente cómodo y pasó a
requisito previo de esta división.

**c) «Nadie toca el mismo módulo»** era cierta porque hoy los módulos son
independientes. M07 depende duro de M04; M03 de M01 y M04. → Regla 5.

**d) «La documentación la escribe el agente»** era cierta con un agente. Con
dos, dos agentes escriben en `BITACORA.md` el mismo día. → Regla 6.

---

## 6 · Lo que dos frentes comparten en una sola máquina

**Dos frentes con territorios disjuntos en el repositorio siguen compartiendo la
máquina, y el inventario de lo que comparten no existía en ninguna parte.** Se
escribe aquí porque el 5 de septiembre de 2026 dos frentes lanzaron la puerta con
dieciséis segundos de diferencia: el aviso previo se dio sobre el fichero
compartido que se veía venir —`BITACORA.md`— y **el daño estuvo en el recurso
compartido que nadie nombró**, los puertos.

Medido el 5 de septiembre de 2026 sobre cinco worktrees:

| Recurso | Estado | ¿Se puede compartir? |
| --- | --- | --- |
| Almacén de pnpm (`~/.local/share/pnpm/store/v11`) | Compartido | **Sí, por diseño** |
| Navegadores de Playwright (`~/.cache/ms-playwright`) | Compartido | **Sí, por diseño** |
| `~/.dotnet/tools` | Compartido; la puerta se lo añade al PATH ella sola | **Sí, por diseño** |
| Puertos de desarrollo | Separados: 55430 / 55440 / 55442 | Ya separados |
| Puertos e2e (55173 / 55081 / 55432) | 4 de 5 worktrees los comparten | **No** |
| `COMPOSE_PROJECT_NAME` | El mismo `sillar_e2e` en esas cuatro | **No** |
| Contenedores y volúmenes e2e | Mismo nombre → destrucción mutua | **No** |
| CPU y memoria (13 GiB, sin swap) | Una puerta a la vez, medido | **No** |

**La línea que separa las dos mitades es la que importa:** lo que se comparte
**por diseño y se puede** frente a lo que se comparte **por descuido y no se
puede**. La primera mitad ahorra disco y tiempo; la segunda destruye corridas
ajenas.

### 6.1 · Una puerta a la vez

**Esto matiza la regla 4 y hay que decirlo en voz alta**, porque la regla dice
que cada frente pasa la puerta en su rama y no dice cuándo: **no cuando quiera.**
La máquina tiene 13 GiB sin swap y dos puertas simultáneas ya pusieron una
corrida en rojo con 46 falsos fallos.

**El turno lo da la máquina, no el acuerdo.** `verificar.mjs` toma un cerrojo
exclusivo al arrancar y lo suelta al terminar; si ya está tomado, se niega y dice
**quién lo tiene, desde qué worktree y desde cuándo** — que es exactamente el
diagnóstico que faltó el día del incidente.

Se hace así y no con un turno acordado porque **una regla cuyo incumplimiento es
invisible no protege: tranquiliza.** Un turno pedido a Integración solo se puede
cumplir acordándose de él, nadie puede observar que se ha violado hasta que ya se
violó, y bajo prisa —que es cuando pasa— se salta.

El cerrojo guarda **PID y hora de inicio**, así que un cerrojo huérfano —el de
una corrida muerta— se distingue de uno vivo y se puede romper diciéndolo. Eso
cubre además la mitad de §6.3 sin trabajo extra.

**El turno pedido a Integración se queda como la vía humana para cuando el
cerrojo no basta**, no como la regla.

### 6.2 · La identidad e2e se deriva, no se escribe

Cada worktree que corra la suite necesita identidad propia: nombre de proyecto de
compose y sus puertos. **Pero no se escribe a mano.**

Documentar cinco valores que hay que teclear en `e2e/.env.e2e` —un fichero
rastreado, que debe modificarse y no commitearse— no sería un procedimiento: sería
**un defecto con instrucciones de uso**. Un `git restore`, un merge o un cambio de
rama lo revierten en silencio, que es literalmente el pendiente §20.

Y la pista estaba dentro del propio problema. El valor que siempre se olvidaba
—el `Port=` dentro de `ConnectionStrings__Default`— **no es un quinto valor: es
`POSTGRES_PORT` otra vez**, duplicado dentro de una cadena en lugar de compuesto a
partir de él. Que fuese justo ese el que se olvidaba no era mala suerte: era la
señal de que estos valores no debían escribirse, sino derivarse.

**Cómo:** `e2e/setup/env.ts` ya tiene la forma correcta a medias —lee el fichero y
cae a un valor por defecto cuando falta—. Basta cambiar **a qué cae**: derivar el
sufijo del proyecto y el desplazamiento de puertos del propio directorio de la
worktree, y **componer** `ConnectionStrings__Default` a partir del puerto ya
resuelto en vez de leerlo entero.

Con eso, el fichero commiteado conserva su virtud —que correr no dependa de copiar
un ejemplo es parte del punto, y su docstring lo dice—, cada worktree es única sin
que nadie haga nada, el quinto valor deja de existir como cosa aparte, y **§20 se
disuelve en lugar de aplazarse**.

La guarda de `identidad.ts` se queda donde está, como barrera del caso residual.

**Excepción declarada de la regla 8, mientras dure.** La regla 8 dice que la
configuración de máquina no entra jamás en un commit de producto, y `e2e/.env.e2e`
está rastreado. Hasta que la identidad se derive, **este fichero es la excepción
declarada**, y por eso se modifica sin commitear. El día que se derive, el fichero
dejará de contener nada específico de una máquina y la excepción desaparece sola.
Se escribe porque un reglamento con dos reglas que se muerden enseña a elegir la
que convenga.

**Por qué el fichero se commiteó, y no fue un error.** Esa decisión era correcta
cuando había una worktree: hacía que correr la suite no dependiera de copiar un
ejemplo a mano. Es §14 otra vez —una regla que era cierta porque solo había uno—
y esta vez aplicada sobre una decisión buena, no sobre un descuido.

### 6.3 · Lo que deja atrás una corrida abortada

**El inventario tiene que incluir lo que deja una corrida interrumpida, no solo
lo que usa una sana.** Playwright levanta su `webServer` antes del
`global-setup`, así que una corrida que muere por *timeout* puede dejar **un Vite
huérfano escuchando en su puerto** sin que haya ninguna suite corriendo.

Quien se lo encuentre verá un puerto ocupado y a nadie usándolo, que es de las
cosas que más se tarda en entender. Antes de lanzar la puerta, y también al
terminarla, se comprueba: puerto libre, sin Vite suelto, sin restos de stack e2e.

---

## 7 · El registro de verde de `main`

**`main` está verde por construcción** —la regla 3 dice que solo Integración
fusiona, y la 4 que nada entra sin puerta— pero ese hecho no está escrito en
ninguna parte, así que la pregunta *«¿este rojo sale también en `main`?»* obliga
hoy a pagar una corrida entera para responderla.

**Integración anota en cada fusión: el commit, la fecha y el resultado de la
puerta que lo admitió.** Un rojo en una rama se contrasta entonces contra un
registro conocido en vez de contra una corrida nueva. Coste: una línea por
integración. Corridas: cero.

Se paga la corrida solo cuando el registro no basta: porque es más antiguo que el
cambio de entorno que se investiga, o porque las señales del veredicto vuelven
limpias y el rojo sigue ahí.

---

## 8 · Cuándo deja de estar en vigor

Un reglamento sin fecha de revisión es exactamente la regla que se sigue
obedeciendo después de dejar de ser cierta.

**Esta división se revisa cuando un frente termina su módulo.** En esa revisión se
vuelve a hacer la pregunta del §5 sobre estas mismas ocho reglas: *¿siguen siendo
ciertas, o eran ciertas porque el mundo era el de septiembre de 2026?*

El §5 aplica §14 una vez. Esto programa la siguiente.

---

## 9 · Lo que esta división no garantiza

Todo lo anterior dice qué posee cada frente, quién fusiona, cuándo corre la puerta
y cuándo se revisa. **Nada de eso dice qué no cubre**, y una de las dos cosas
sobre las que descansa el esquema no es lo que parece.

**Dos pares de ojos no son un control.** Una cita equivocada pasó por dos
revisores en la misma tarde y ninguno abrió el fichero. **Revisar no verifica;
abrir el fichero verifica.**

Tiene la forma del §2 de `ANTES-DE-EMPEZAR-UN-MODULO.md`: **una revisión que nunca
ha encontrado nada es indistinguible de una revisión que funciona.**

**En concreto, y dicho sobre el líder: que el líder audite al Frente A no
comprueba al Frente A.** La auditoría no es una puerta, no sustituye a la puerta,
y no adelanta su resultado. Un rojo es rojo aunque el líder haya aprobado el
diseño; un verde no lo es más porque lo haya leído.

**Lo peligroso no es que falle: es que da confianza en vez de tranquilidad.** Una
barrera que calla te deja seguir sabiendo que no sabes. Una auditoría que se da
por control te deja seguir **creyendo que sí**.

**Lo que sí verifica: la puerta, y abrir el fichero.** Nada más de esta lista lo
hace.

**Y aplicado a quien audita, porque si no es incómodo no sirve:** si las
auditorías del Frente A dejan de encontrar cosas, eso no es una buena señal — es
una barrera sin provocar. Ese día toca comprobar que se pueden encontrar, no
felicitarse.
