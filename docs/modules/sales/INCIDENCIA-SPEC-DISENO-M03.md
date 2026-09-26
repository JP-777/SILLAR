# Incidencia — la SPEC original de Diseño de M03 (CERRADA)

> ## CERRADA el 26 de septiembre de 2026 — la SPEC llegó completa
>
> Llegó en una tercera entrega, **como archivo en la raíz del repositorio de trabajo**, avisada por
> JP. Completa: 897 líneas, **con su §7 y su §8**, cerrando en B-05 y «ESTADO FINAL DEL DOCUMENTO:
> `DETENIDO / REQUIERE RESPUESTA DE JP`». Diseño localizó y entregó las dos secciones que faltaban.
>
> Conservada íntegra en `SPEC-M03-ORIGINAL-DISENO-2026-09-23.md`, **idéntica byte a byte** a la
> recibida (`sha256` `3efbed1f35252f3f5c1ea6838a08ca2ff70ea0e9557352f931c3bbba91a6f22e`), sin
> cabecera ni aviso añadidos. La reconciliación con las decisiones del 26/09 está en `SPEC.md` §0.
>
> **Este documento no se borra.** Registra dos anuncios de entrega que no llegaron, y **la tercera
> funcionó por la vía que el §7 de abajo pedía**: un archivo localizable, no un adjunto anunciado.
> Un intento fallido sin registro es un intento que se repite.
>
> **Lo que el §5 dedujo, confirmado:** faltaban **dos** huecos y no uno. Un documento que salta de 7
> a 9 tenía un §8, y en la versión completa el §8 son las nueve reglas de negocio R-01 a R-09 — la
> sección de la que dependía todo lo demás.

---


**Creación:** 26 de septiembre de 2026 — America/Lima
**Última verificación:** 26 de septiembre de 2026 — America/Lima
**Commit verificado:** `7979574f953a5af192caa7da825136a9e8d5551c`

Este documento existe porque la misma pieza se ha anunciado dos veces y no ha llegado ninguna de
las dos. Registrar el intento fallido es lo único que impide que el tercero se parezca a los dos
primeros.

---

## 1 · Qué se anunció

El 26 de septiembre de 2026, el colíder comunicó vía JP: «**SE RECUPERÓ LA SPEC ORIGINAL DE DISEÑO
DEL 23/09/2026**» y «**te adjunto el archivo `SPEC-M03-ORIGINAL-DISENO-2026-09-23.md`**».

## 2 · Qué llegó

**El anuncio. No el archivo, ni su texto.**

El mensaje recibido contiene las seis instrucciones de la ronda y nada más. **No hay ningún
fragmento de la SPEC en él**: ni un encabezado, ni una tabla, ni una línea de sus §§.

## 3 · Cómo se comprobó — cinco vías, todas negativas

| Vía | Resultado |
|---|---|
| El texto del mensaje recibido | **No contiene SPEC.** Solo el anuncio y las instrucciones |
| `find` por nombre sobre `/home/JP777`, `/tmp` y el directorio de trabajo temporal, con los patrones `*SPEC*M03*` y `*ORIGINAL*DISENO*` | **Nada** |
| `docs/modules/sales/` en **los diez árboles de trabajo** de la máquina | Solo `DECISIONES-PREVIAS-M03.md` en cada uno. **Ningún SPEC** |
| `git fetch --all` y después `git log --oneline --all -- docs/modules/sales/SPEC.md` | **Vacío.** Y un barrido de los árboles de todos los commits desde el 22/09 buscando `sales/.*(SPEC|ORIGINAL|DISENO)` devuelve **un solo archivo: el mío**, `DISENO-TECNICO-PROVISIONAL-M03.md` |
| Los dos directorios temporales de sesión de la máquina | **Vacíos** |

Cinco vías independientes, y la primera no es una búsqueda: es leer el mensaje.

> **Por qué cinco y no una.** `docs/ANTES-DE-EMPEZAR-UN-MODULO.md` §4 avisa de que «una búsqueda
> que no encuentra nada es una respuesta igual de falible, y encima convincente». Con un anuncio
> explícito de adjunto en contra, una sola vía negativa no bastaba.

## 4 · La instrucción que no se puede cumplir, y no se simula

> «1. Conserva una copia literal de la SPEC recibida como documento histórico en
> `docs/modules/sales/`. No modifiques su texto original.»

**No hay texto original que conservar.** No se crea un archivo con ese nombre y el contenido vacío,
ni reconstruido, ni parafraseado: un archivo llamado `SPEC-M03-ORIGINAL-DISENO-2026-09-23.md` que no
contenga la SPEC del 23/09 sería **peor que su ausencia**, porque su ausencia se ve y su falsificación
no. El mandato original lo dice con esas mismas palabras: «no redactes otra SPEC final ni **afirmes
haberla leído cuando no la recibiste**».

## 5 · La incidencia del §7 · **NO OBSERVADA — recibida del colíder**

> «2. ATENCIÓN: la versión recibida parece incompleta. En §7, la frase de auditoría «Creación del
> pedido…» aparece interrumpida y el documento salta a §9.»

**Queda registrada tal cual, y con su procedencia marcada: es del colíder, no mía.**

**No he visto el §7 ni el §9, ni la frase «Creación del pedido…», ni el salto.** No puedo
confirmarlo ni desmentirlo, y **no reconstruyo nada de lo ausente**. Lo que sí se deduce de la
propia descripción, y conviene tener escrito para cuando llegue:

- La SPEC del 23/09 **tenía un §8** —los números no saltan de 7 a 9 en un documento que nadie
  cortó—, y ese §8 también falta.
- La `_PLANTILLA_SPEC.md` del repositorio y las SPEC de M01, M04 y M07 dirán qué va en cada
  posición. **No lo mirado todavía a propósito:** deducir el contenido del §8 desde la plantilla es
  exactamente reconstruir lo ausente. Se mira **cuando llegue el texto**, para cotejar, no para
  rellenar.
- Por tanto **lo que falta son dos huecos, no uno**: el final del §7 y el §8 entero.

## 6 · Estado de la reconciliación del punto 3

> «3. Prepara la reconciliación documental con `DECISIONES-VIGENTES-M03.md` del 26/09… Identifica
> explícitamente las afirmaciones históricas desplazadas.»

**Se separa en dos mitades, y una está hecha y la otra es imposible hoy.**

**Hecha, y ya en la rama desde `617bb28`.** Toda afirmación histórica desplazada **que vive en el
repositorio** está identificada con archivo y línea:

| Afirmación histórica desplazada | Dónde estaba | Qué la sustituye | Dónde consta |
|---|---|---|---|
| «liberar la **reserva de stock** no cancela el pedido» | `DECISIONES-PREVIAS-M03.md` §1 | Vence el **plazo para pagar** → Vencido, sin cancelar | Enmienda visible en la cabecera de ese documento |
| «**48 horas** naturales… plazo de la **reserva**» | idem §2 | 48 h configurables = **plazo para pagar**. Mismo número, otro objeto | idem |
| Los dos estados distintos son **reserva** y **pedido** | idem §1 | Son **hecho de pago** y **estado del pedido** | idem |
| «la tienda abre cobrando con **Yape y efectivo**» | `PENDIENTES.md:487-488` | El encargo nombra solo Yape, y **no resuelve el efectivo** | `MATRIZ-DIFERENCIAS-M03.md` §3 fila 4 · **abierta**, `ESCALADAS-M03.md` §c |
| `order_items.product_id → catalog.products` | `ARQUITECTURA_MODULAR.md:213` | Se vende la **variante**: `catalog.product_items` | `MATRIZ` §3 fila 10 · de Chat 2 |
| `order_statuses` como tabla, referenciada desde `orders` | `ARQUITECTURA_MODULAR.md:195`, `:214` | Siete estados fijos; una tabla de catálogo no replicada cruzaría la `ADR-018` | `MATRIZ` §3 fila 11 y §4 · de Chat 2 |

**Imposible hoy: la otra mitad.** Reconciliar **contra el texto de la SPEC** —qué §§ suyos quedan
desplazados por recojo, estados, Yape y «a consultar»— exige el texto. No se puede identificar una
afirmación desplazada dentro de un documento que no se tiene, y **suponer qué decía es inventarlo**.

**Qué será esa segunda mitad, en cuanto llegue el texto:** recorrer la SPEC del 23/09 §§ por §§
contra las nueve decisiones de `DECISIONES-VIGENTES-M03.md`, y por cada afirmación suya que quede
desplazada anotar la cita literal, qué la sustituye y por qué. Con enmienda visible y **sin tocar
su texto original**, igual que se hizo con `DECISIONES-PREVIAS-M03.md`.

## 7 · Cómo tiene que llegar para que llegue

El mandato original ya lo había previsto, y es la línea que se saltó las dos veces:

> «JP debe reenviarla **literalmente dentro del mensaje, no como adjunto**.»

Un adjunto anunciado no es un adjunto recibido. **El texto de la SPEC, pegado en el cuerpo del
mensaje**, es la única forma que ha demostrado funcionar en este canal — es como llegaron las nueve
decisiones vigentes del 26/09, y por eso están aplicadas.

Y si el texto viene con los dos huecos del §5, **se entrega igual**: una SPEC incompleta y marcada
como incompleta es trabajable; su ausencia no.

## 8 · Qué sigue bloqueado, y qué no

**Bloqueado, y sin cambio respecto a la ronda anterior:**

- **Paso 1 · SPEC.** No hay SPEC. No se aprueba ninguna definitiva mientras falten el fragmento de
  Diseño y las decisiones comerciales de `ESCALADAS-M03.md` §b1, §b2, §c y la modalidad de acceso
  de §d.
- **Paso 2 · DATOS.** Doblemente: sin SPEC, y sin la barrera de fronteras del frontend, **que sigue
  sin estar en `main`** — recomprobado en esta ronda tras `git fetch --all`.
- **Pasos 3, 3.5, 4 y 5.** Dependen de los anteriores.

**No bloqueado, y ya hecho:** la reconciliación contra el repositorio, el barrido de la `ADR-018`
sobre cada FK que M03 va a querer, el diseño técnico de lo no controvertido y el plan de pruebas.
Todo en la rama desde `617bb28`.

**Nada de código, migraciones, API ni interfaz. Nada fuera de `docs/modules/sales/`.**
