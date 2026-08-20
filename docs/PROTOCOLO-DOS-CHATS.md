> # ⚠ RETIRADO — 18 de agosto de 2026
>
> **Desde hoy hay un solo chat.** Este documento no rige. Se conserva sin editar, igual que
> una ADR sustituida: lo de abajo es lo que se decidió el 16 de agosto y estuvo vigente dos
> días, no lo que se hace ahora.
>
> **Por qué se retira.** No porque estuviera mal escrito — hizo su trabajo. Porque **el
> cartero era el cuello de botella**, y eso produjo daño real: dos lecturas incompatibles del
> mismo `Dockerfile` que ninguno de los dos chats podía resolver preguntándole al otro, y dos
> mensajes truncados al copiar bloques largos, con trabajo perdido sin que nadie se enterara
> en el momento.
>
> **Por qué no se arregló con mensajería directa entre agentes.** Existe la posibilidad
> técnica y se descartó: los agentes viven dentro de la sesión que los creó y mueren con ella,
> y sobre todo **SILLAR no es dos equipos paralelos, es uno en serie con validación**. La
> mensajería directa resuelve «dos equipos que no se pisan»; aquí el problema era el contrario.
>
> **Dónde fue a parar lo que sobrevive:**
>
> | Sección | Dónde está ahora |
> |---|---|
> | §1 Un solo escritor por archivo | Muere. Ya no hay a quién proteger |
> | §2 Quién decide qué | Muere como reparto. Sobrevive como **autocontrol** en `CLAUDE.md` |
> | §3 Bloques de mensaje | Muere. No hay a quién escribirle |
> | §4 El turno y los 5 pasos | Ya estaba en `ROADMAP_MODULAR.md`. Solo se rescató el **paso 3.5 · DISEÑO**, que únicamente existía aquí |
> | §5 Lo que sigue siendo de JP | `CLAUDE.md` |
> | §6 Proveedores y la frontera con `SILLAR-DISENO/` | `PROTOCOLO-DISENO.md` §7. Nunca tuvo nada que ver con los dos chats |
>
> **Lo que se pierde y no se sustituye:** el segundo lector. El §2 no era jerarquía, era que
> una decisión de datos tomada mirando una pantalla concreta optimiza para esa pantalla, y
> quien validaba no había estado en la conversación donde se decidió. Eso hoy depende de que
> el mismo agente se revise a sí mismo, que es más frágil. Ver `CLAUDE.md`, «El lector que ya
> no está».

---

# Protocolo de trabajo con dos chats

Desde el 16 de agosto el proyecto se planifica en **dos conversaciones que se turnan**:

| Chat | Cómo se llama | De qué responde |
|---|---|---|
| **Backend** | El principal, el que viene desde el inicio del proyecto | Arquitectura, datos, API, decisiones |
| **Frontend** | Abierto el 16 de agosto | Interfaz, flujo, verificación, sistema de diseño |

**Los dos leen este archivo antes de trabajar.** No existe para repartir trabajo: existe para
que dos conversaciones que no se ven no se pisen ni se contradigan.

**No trabajan a la vez. Trabajan por turnos.** Uno avanza, entrega el testigo, y se queda
quieto hasta que se lo devuelven. Dos chats escribiendo a la vez en el mismo repositorio es
la forma más rápida de perder trabajo sin enterarse.

---

## 1. La regla que sostiene todo lo demás

> ### Cada archivo tiene un solo escritor.

Dos chats editando el mismo documento es garantía de que uno pise al otro sin enterarse,
porque la entrega pasa por JP copiando archivos y el segundo en llegar gana.

| Archivo | Escribe |
|---|---|
| `docs/BITACORA.md` | **Solo Backend** |
| `docs/adr/*` | **Solo Backend** |
| `CLAUDE.md`, `ARQUITECTURA_MODULAR.md`, `ROADMAP_MODULAR.md` | **Solo Backend** |
| `docs/modules/<m>/SPEC.md`, `DATOS.md` | **Solo Backend** |
| `docs/modules/<m>/UI-*.md` | **Solo Frontend** |
| `docs/MARCA.md`, sistema de diseño, guías de verificación | **Solo Frontend** |
| `docs/PROTOCOLO-DISENO.md` y los diseños de cada módulo | **Solo Frontend** |
| `e2e/` y su documentación | **Solo Frontend** |

**La bitácora es de Backend, sin excepción.** Frontend no la edita: manda su entrada por el
canal del §3 y Backend la incorpora. Es el único registro común y no puede tener dos versiones.

---

## 2. Quién decide qué

**Frontend no toma decisiones que cambien el SPEC, el modelo de datos o un contrato.** Las
plantea. Si al construir una pantalla descubre que falta un campo, que un endpoint devuelve
poco, o que una regla no se puede cumplir tal como está escrita, **eso es un hallazgo, no una
corrección**: va a Backend, se decide allí, y si procede se enmienda el SPEC y se registra.

La razón no es jerarquía. Es que una decisión de datos tomada desde una pantalla concreta
optimiza para esa pantalla, y el módulo tiene que servir a dos productos y a clientes que
todavía no existen.

Al revés también: **Backend no decide cómo se ve algo.** Si el SPEC dice que un conflicto se
comunica con una frase, no dice con qué componente.

---

## 3. Cómo se hablan

No se ven. JP es el cartero, así que los mensajes tienen que ser **autocontenidos**: quien los
recibe no ha leído la conversación de enfrente.

Cuando un chat necesita algo del otro, termina su respuesta con un bloque así:

```
=== PARA EL CHAT FRONTEND ===
QUÉ NECESITO   Una frase.
POR QUÉ        El contexto mínimo para que se entienda sin leer nada más.
QUÉ TE DOY     Archivos, rutas, endpoints, decisiones ya tomadas.
QUÉ NO TOQUES  Lo que es del otro lado.
=== FIN ===
```

JP copia el bloque entero. No hace falta que lo resuma ni que lo explique.

**Cada bloque nombra los archivos del repositorio que hay que leer**, en vez de repetir su
contenido. El repositorio es la memoria compartida; el chat es solo el canal.

---

## 4. El turno — cómo se recorre un módulo

Un módulo se termina pasando el testigo, no repartiéndolo. **Solo un chat está activo a la
vez, y solo un Claude Code toca el repositorio.**

| Paso | Quién | Antes de seguir |
|---|---|---|
| **1 · SPEC** | Backend | **Frontend lo revisa** y responde una sola pregunta: *¿se puede construir la pantalla con esto?* Lo que falte, se añade **antes** de escribir el esquema |
| **2 · DATOS** | Backend | — |
| **3 · API** | Backend | El testigo pasa a Frontend con los endpoints y el contrato |
| **3.5 · DISEÑO** | JP en Claude Design, con Frontend | Las pantallas del §9 del SPEC, con sus estados. Ver `PROTOCOLO-DISENO.md` |
| **4 · UI** | Frontend | Cada hallazgo que toque datos o contrato **vuelve a Backend**, se decide, se corrige, y el testigo regresa |
| **5 · CIERRE** | Backend, con la verificación de Frontend | Montaje y desmontaje sin romper nada |

**La revisión del paso 1 es la que más ahorra.** Un campo que falta descubierto ahí cuesta una
línea; descubierto en el paso 4 cuesta una migración, y si ya hay datos de un cliente, cuesta
mucho más.

### El testigo

El bloque del §3 termina siempre diciendo qué pasa con el turno:

```
EL TURNO PASA A TI. Devuélvelo cuando <condición concreta>.
```

Mientras no lo tenga, un chat puede pensar, leer y preparar — pero **no entrega archivos ni
manda ejecutar nada**. Si cree que hace falta salirse de eso, lo pide.

Si algún día hace falta de verdad trabajar a la vez, la única forma segura es un
`git worktree` y una rama por chat, sin que ninguno toque los archivos del otro. No antes de
que exista una razón concreta.

---

## 5. Lo que sigue siendo de JP

- **Aprobar decisiones** que cambien arquitectura o alcance.
- **Aplicar los entregables** al repositorio y commitear.
- **Llevar los bloques** de un chat al otro.
- **Mirar lo que solo se puede mirar**: que algo se vea bien, que una frase suene a persona,
  que un flujo tenga sentido. Eso no se delega, pero se reduce: cuanto más cubra `e2e/`,
  menos veces hay que abrir el navegador a mano.

---

## 6. Proveedores — actores que no escriben

Además de los dos chats hay ayudas que **producen material pero no tocan el repositorio**:

| Actor | Qué aporta | Qué nunca hace |
|---|---|---|
| **Claude Design** | Pantallas, estados, prototipos | Entregar código al repositorio (`PROTOCOLO-DISENO.md` §1) |
| **Agente de investigación** *(Codex u otro)* | Cómo funciona una biblioteca, un hook o una animación; alternativas y costes | Decidir qué entra en SILLAR |

La distinción es una sola y vale para cualquier ayuda que se añada mañana:

> **Investigar es libre. Decidir tiene dueño.**

Un agente que no ha leído `CLAUDE.md`, los ADR y la bitácora **no puede decidir**: va a proponer
cosas razonables en general y equivocadas aquí — colores fuera de los tokens, componentes que
esquivan `src/shared/`, animaciones sin `prefers-reduced-motion`, dependencias que nadie pidió.
No es un defecto suyo: es que las restricciones de este proyecto viven en documentos que no ha
leído.

Su material entra por Frontend, que lo filtra contra el sistema de diseño. Si toca datos o
contratos, sigue el camino de siempre: Frontend lo plantea, Backend lo decide.

**Ningún proveedor escribe en el repositorio.** Solo los dos chats entregan archivos, y solo
uno de ellos por archivo.

### La frontera con el equipo de diseño

El equipo de diseño trabaja en una **carpeta aparte, fuera de este repositorio**:

```
SILLAR/                  ← producto. Backend y Frontend
SILLAR-DISENO/           ← diseño. Design, investigación y auxiliar
  referencias/           ← sitios, capturas y bibliotecas que recopila JP
  investigacion/         ← qué hace cada biblioteca, qué cuesta, alternativas
  propuestas/            ← lo que sale de Claude Design, por módulo
  ENTREGA/               ← lo único que cruza
```

**Backend y Frontend no leen `SILLAR-DISENO/`.** No es secretismo: es que ahí dentro hay
borradores, experimentos y bibliotecas descartadas, y un agente que los lea va a construir
sobre algo que nadie aprobó.

**El equipo de diseño no escribe en `SILLAR/`.** Ni siquiera para «dejar un ejemplo».

Cruza **solo lo que JP pone en `ENTREGA/` y entrega a mano**, y cruza como referencia, nunca
como código (`PROTOCOLO-DISENO.md` §1). La única excepción es `/design-sync`, que empuja
componentes del repositorio **hacia** Claude Design y nunca al revés.
