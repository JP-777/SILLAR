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

**El turno lo da la máquina, no el acuerdo.** `verificar.mjs` toma un cerrojo exclusivo
compartido por las worktrees al arrancar y lo suelta al terminar. El cerrojo registra PID,
hora de inicio, dueño y worktree; si encuentra otro proceso vivo, la segunda puerta no
empieza. Un cerrojo huérfano se distingue de uno vivo y se recupera diciéndolo.

La exclusión no descansa en una sospecha. Hay **dos observaciones independientes de
concurrencia mala**: el 5 de septiembre dos puertas arrancaron con dieciséis segundos de
diferencia y apareció una cascada de 46 falsos fallos; el 8/9 de septiembre se repitió la
medición sobre el mismo commit `75f9316`, con identidades e2e separadas, y las dos puertas
concurrentes terminaron rojas. Un control serial posterior sobre ese mismo commit dio
verde 6/6.

Ese control serial es `n=1`: no demuestra que una puerta serial siempre sea verde.
Sí demuestra que el segundo rojo concurrente no era inevitable por el código.

Por eso la política es serializar. No se atribuye el fenómeno a RAM, CPU ni a otra causa
no medida: lo probado es que **la señal de dos gates simultáneos en esta máquina no es
fiable**, y que serializar cuesta menos que diagnosticar falsos rojos.

**Excepción deliberada para medir otra vez:** `SILLAR_VERIFY_PERMITIR_CONCURRENCIA`
acepta como valor **el motivo obligatorio de la medición**, no un booleano. Vacía o con
solo espacios se rechaza. Con un motivo válido la puerta continúa **sin tomar el cerrojo**,
no rompe, toma ni borra un cerrojo ajeno y deja el motivo visible en el reporte.

Bajo esa excepción, la inspección del cerrojo tiene tres estados:

1. **otra puerta viva** —incluye el caso conservador en que existe el cerrojo y no se puede demostrar que su PID ya murió—;
2. **ninguna puerta viva confirmada**;
3. **no se pudo inspeccionar**.

Los tres aparecen en la cabecera. El tercero no se disfraza de «nadie».
**Cualquier rojo ejecutado bajo esta excepción queda marcado como de atribución no limpia**,
porque la concurrencia fue autorizada deliberadamente. Si además la inspección quedó en el
tercer estado, el reporte dice también que no pudo mirar: son dos fuentes distintas de
incertidumbre.

La aparente asimetría es deliberada: **fail-closed protege una barrera que está en
servicio, no una barrera que el operador deshabilitó deliberadamente para medir**.
Regla general: si una inspección fallida alimenta una **decisión**, se detiene la decisión;
si solo alimenta un **mensaje**, se degrada el mensaje y se dice que no se pudo mirar.

El turno humano de Integración sirve para coordinación, no sustituye al cerrojo.
Si cambia el hardware, esta exclusión se revisa por medición usando la excepción
documentada, no por intuición.

### 6.2 · La identidad e2e se deriva, no se escribe

Cada worktree obtiene su identidad desde su propio árbol mediante `scripts/identidad.mjs`:
sufijo, proyecto de Compose, base y puertos salen de una misma derivación. La cadena de
conexión se compone a partir de esos valores; el puerto de PostgreSQL no vuelve a existir
como un segundo dato escrito manualmente dentro de la cadena.

Para desarrollo, `scripts/estrenar.mjs` genera el `.env` correspondiente a la worktree.
**No se copia `.env.example` a mano y no se copia el `.env` de una worktree vecina.**
La plantilla conserva configuración y secretos vacíos; `estrenar.mjs` escribe la identidad
calculada y deja los secretos para configuración local.

Para e2e la identidad se deriva sin exigir un fichero específico de máquina.

Esto elimina el defecto que sostenía el antiguo pendiente §20: una identidad que solo
existía como cambio local no commiteado y desaparecía con checkout, merge o limpieza.
**§20 se disolvió; no se abre como pendiente.**

La identidad separada evita colisiones de nombres y que una operación destructiva confunda
stacks. No vuelve segura la concurrencia del gate: esa pregunta se midió por separado y
su respuesta actual está en §6.1.

Las operaciones destructivas mantienen su propia defensa: antes de desmontar un stack
verifican su propiedad. Identidad distinta y comprobación de propiedad son capas separadas.

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
