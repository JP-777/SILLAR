# Protocolo de diseño

Desde el 18 de agosto, cada módulo se diseña **antes** de construirse su interfaz. El diseño
se hace en **Claude Design**, lo encarga JP, y entra como el **paso 3.5** del ciclo de módulo
(`ROADMAP_MODULAR.md`), justo antes de construir la interfaz.

Este documento dice qué es ese diseño, qué no es, y qué tiene que respetar.

---

## 1. La regla que evita el desastre

> ### El diseño es una referencia, no un origen.

Claude Design puede exportar HTML y código. **Ese código no entra al repositorio.** Si entra,
llega con colores escritos a mano y componentes propios que esquivan `src/shared/`, y en dos
módulos hay dos sistemas de diseño: el del repositorio y el de las exportaciones.

Lo diseñado se **implementa** con los componentes y los tokens que ya existen. Si algo del
diseño no se puede construir con lo que hay, eso es un componente nuevo en `src/shared/`
— una decisión, no un copiar y pegar.

---

## 2. Preparación — el sistema de diseño se sube, no se describe

**Claude Design vive en el navegador o en la aplicación de escritorio, no en la terminal.** Lo
que Claude Code aporta es `/design-sync`: mantiene un **proyecto de sistema de diseño** de
Claude Design sincronizado con la biblioteca de componentes **del repositorio**, componente a
componente.

La dirección importa y es al revés de lo que parece:

```
frontend/src/shared/  ──/design-sync──►  proyecto de sistema de diseño
   (la verdad)                             (lo que Design usa al diseñar)
```

**Sube lo nuestro para que lo diseñado nazca con nuestros componentes.** No baja código.
Combinado con el §1, el código va en un solo sentido y no hay forma de que se cuele por el
otro.

### De dónde salen las vistas previas

**De los componentes reales de `frontend/src/shared/ui`, no del mockup.**

`docs/sillar-design-system.html` se escribió antes que el código, para enseñar a qué iba a
parecerse el producto. Nunca fue código: usa su propia nomenclatura de clases —`.btn`,
`.field`, `.card`, `.module`, `.switch`— que **no coincide** con la de los componentes que
existen. Trocearlo sincronizaría a Claude Design una versión aspiracional que nadie construyó,
y todo lo diseñado encima nacería desalineado.

Lo único de ese archivo que sí es fuente de verdad son los tokens, y ya están en
`tokens.css` letra por letra.

El mockup queda como **referencia histórica**, no como origen. Las vistas previas se generan
desde los componentes reales, con su marcador `@dsCard`.

Lo que se sube, en este orden de importancia:

| Qué | De dónde | Por qué |
|---|---|---|
| Los componentes reales | `frontend/src/shared/ui` | Una vista previa por componente. La tarjeta de módulo con su interruptor es la que define el producto |
| Los tokens | `frontend/src/shared/styles/tokens.css` | Es la fuente de verdad del color. La paleta ya está validada para daltonismo y contraste |
| La marca | `docs/MARCA.md` | Sobre todo el §6: el panel lleva marca SILLAR, el negocio es contexto, el logo del cliente nunca va en el armazón |
| El mockup original | `docs/sillar-design-system.html` | **Solo como referencia.** No se trocea ni se sube: sus clases no son las del código |
| **Capturas del panel en funcionamiento** | Las que genera `e2e/` | No para el sistema de diseño —eso lo cubre la sincronización— sino para **pedir crítica**: contraste, microcopy, accesibilidad |

La sincronización cubre los componentes. Las capturas del arnés cubren otra cosa: cómo se ven
esos componentes **juntos y con datos reales**, que es donde aparecen los problemas que ningún
componente tiene por separado.

---

## 3. Qué se pide por módulo

No «diseña el catálogo». El SPEC ya dice qué pantallas hay —su §9— y qué reglas gobiernan cada
una. El encargo es esa lista, y para cada pantalla, **sus estados**:

| Estado | Por qué no se puede saltar |
|---|---|
| **Vacío** | Es la primera pantalla que ve un cliente nuevo. Un módulo recién instalado no tiene datos |
| **Con datos** | El caso normal |
| **Cargando** | Sin él, cada quien improvisa uno distinto |
| **Conflicto** | Aquí es donde se rompen las reglas del proyecto si nadie lo diseñó |

Y las cuatro combinaciones que no son opcionales: **claro y oscuro**, **móvil y escritorio**.

**El estado vacío es el que más se olvida y el que más vende.** «Todavía no hay productos.
Cuando agregues el primero aparecerá aquí» dice qué falta y qué hacer; una tabla en blanco no
dice nada.

---

## 4. Lo que el diseño tiene que respetar

Se le dice a Claude Design en cada encargo, porque no lo sabe:

1. **Todo el contenido en español.**
2. **Ningún «Ha ocurrido un error» y ningún botón «Aceptar».** Un conflicto es una frase que
   dice qué lo impide y qué hacer; un botón nombra la acción que ejecuta.
3. **Ningún color fuera de los tokens.** La paleta está validada; inventar un color la rompe.
4. **Ningún identificador a la vista.** Nada de `uuid` en pantalla: fuera se usa el slug,
   dentro el código del negocio.
5. **Lo que el módulo oculta, se oculta.** En M01, la palabra «variante» no existe mientras un
   producto tenga una sola. Si el diseño la muestra siempre, el diseño está mal.
6. **Teclado y foco visible.** Si un diálogo aparece en el diseño, aparece con su foco.
7. **El contraste validado no sobrevive a un degradado.** La paleta se comprobó entre colores
   planos. Un texto sobre degradado tiene que cumplir contra el **punto peor** del degradado,
   no contra su promedio. Si hace falta comprobarlo en dos sitios distintos, probablemente el
   texto no va ahí.

---

## 5. Bibliotecas de componentes y animaciones

Van llegando por separado, y entran por el mismo camino que todo lo demás:

- **Una biblioteca es una dependencia.** No entra sin decidirlo (`CLAUDE.md`, regla 2), y no
  entra por gustar: entra por resolver algo que lo que hay no resuelve.
- **Se toma la idea antes que el paquete.** Casi siempre lo que gusta de una biblioteca son
  tres detalles, y esos tres se construyen en `src/shared/` sin arrastrar el resto.
- **Toda animación respeta `prefers-reduced-motion`.** No es opcional: hay gente a la que el
  movimiento le produce mareo, y es una línea de CSS.
- **Una animación que retrasa una acción es un defecto.** En el mostrador, una transición de
  300 ms repetida doscientas veces al día es un minuto perdido.

---

## 6. Coste

Claude Design **consume del mismo cupo** que Claude Code y el chat. Diseñar las pantallas de un
módulo entero no es gratis y compite con el desarrollo del mismo día. Conviene hacerlo cuando
el paso 3 esté cerrado y antes de empezar el 4, que es justo cuando Claude Code está parado.

---

## 7. Proveedores — actores que no escriben

Hay ayudas que **producen material pero no tocan el repositorio**:

| Actor | Qué aporta | Qué nunca hace |
|---|---|---|
| **Claude Design** | Pantallas, estados, prototipos | Entregar código al repositorio (§1) |
| **Agente de investigación** *(Codex u otro)* | Cómo funciona una biblioteca, un hook o una animación; alternativas y costes | Decidir qué entra en SILLAR |

La distinción es una sola y vale para cualquier ayuda que se añada mañana:

> **Investigar es libre. Decidir tiene dueño.**

Un agente que no ha leído `CLAUDE.md`, los ADR y la bitácora **no puede decidir**: va a proponer
cosas razonables en general y equivocadas aquí — colores fuera de los tokens, componentes que
esquivan `src/shared/`, animaciones sin `prefers-reduced-motion`, dependencias que nadie pidió.
No es un defecto suyo: es que las restricciones de este proyecto viven en documentos que no ha
leído.

Su material se filtra contra el sistema de diseño antes de usarse. Si toca datos o contratos,
no se aplica: se plantea, se decide, y si procede se enmienda el SPEC y se registra.

**Ningún proveedor escribe en el repositorio.** Los archivos los entrega Claude Code.

### La frontera con el equipo de diseño

El equipo de diseño trabaja en una **carpeta aparte, fuera de este repositorio**:

```
C:\
├── SILLAR\              ← producto. Es el repositorio
└── SILLAR-DISENO\       ← diseño. Design, investigación y auxiliar
    ├── referencias\     ← sitios, capturas y bibliotecas que recopila JP
    ├── investigacion\   ← qué hace cada biblioteca, qué cuesta, alternativas
    ├── propuestas\      ← lo que sale de Claude Design, por módulo
    └── ENTREGA\         ← lo único que cruza
```

**Hermanas, no anidadas.** Estuvo dentro de `SILLAR/` hasta el 18 de agosto, sin rastrear y
sin ignorar: bastaba un `git add -A` para meter en el repositorio del producto los bundles que
el §1 prohíbe que entren. Se movió fuera, y la línea de `.gitignore` se dejó puesta como red.

**No se explora `SILLAR-DISENO/`; sí se leen los documentos que se entregan por su nombre.**
La diferencia importa y evita tener que razonarla cada vez: rebuscar en la carpeta es construir
sobre borradores que nadie aprobó; leer un informe que JP pide aplicar, nombrándolo, es leer
algo aprobado. Lo que sigue prohibido es entrar a mirar qué más hay.

**Desde `SILLAR/` no se explora `SILLAR-DISENO/`.** No es secretismo: ahí dentro hay borradores,
experimentos y bibliotecas descartadas, y construir sobre algo que nadie aprobó es peor que no
tenerlo.

**El equipo de diseño no escribe en `SILLAR/`.** Ni siquiera para «dejar un ejemplo».

Cruza **solo lo que JP pone en `ENTREGA/` y entrega a mano**, y cruza como referencia, nunca
como código (§1). La única excepción es `/design-sync`, que empuja componentes del repositorio
**hacia** Claude Design y nunca al revés.
