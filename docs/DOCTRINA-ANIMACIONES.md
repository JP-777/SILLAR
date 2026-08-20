# Doctrina de animaciones

Cuándo SILLAR se permite mover algo, y cuándo no. Gobierna a los tres equipos —quien construye,
quien diseña y quien investiga— y a los módulos que todavía no existen.

> **Aviso sobre este documento.** Recoge lo que está **decidido y verificable en el
> repositorio**: los tokens, la regla de movimiento reducido, y los criterios que salieron de
> construir el paso 4 de M01. Si en alguna conversación se acordó algo que aquí no está, no se
> perdió por descarte: es que no llegó a escribirse. Se añade cuando aparezca, con su caso.

---

## 1. La pregunta que decide

Antes de animar cualquier cosa:

> **¿Qué pasa si esta animación no ocurre?**

- **Si la respuesta es «nada, se ve peor»** → es una animación **expresiva**. Puede entrar, con
  las condiciones del §3.
- **Si la respuesta es «la persona no se entera de algo»** → **el movimiento no puede ser quien
  se lo cuente.** Hay que decirlo con palabras, con posición o con estado, y la animación pasa a
  ser el acompañamiento de algo que ya funciona sin ella.

La segunda es la que importa, y tiene un caso concreto detrás. En la tabla de presentaciones de
M01, pasar de una presentación a varias parecía necesitar una animación que explicara que nada
se había perdido. No la necesitaba:

> **Lo que impide leerlo como pérdida no es el movimiento, es que los valores estén ahí.** Un
> campo con «PLU-ART-PG» dentro no se lee como borrado, se anime o no.

Y de ahí sale la prueba que hay que hacerse siempre:

> **Con `prefers-reduced-motion` activo no queda nada de la animación. Si la seguridad dependiera
> de ella, para esas personas no habría ninguna.**

---

## 2. Lo que no se negocia

Estas tres no dependen del caso ni del gusto.

### 2.1 Toda animación respeta `prefers-reduced-motion`

No es opcional: hay gente a la que el movimiento le produce mareo. Está aplicado globalmente en
`frontend/src/shared/styles/base.css:82`, y **no apaga el cambio: lo sustituye** por un fundido
corto de opacidad. Apagarlo del todo deja saltos secos que se leen como fallo.

**La consecuencia que hay que tener presente:** esa regla le pone un fundido de opacidad a *todo*
elemento. Por eso **ningún control puede depender de desaparecer visualmente para dejar de
recibir eventos** — eso se apoya en `disabled`, `hidden`, `inert` o en desmontarlo, nunca en la
opacidad.

El arnés lo comprueba: `e2e/` corre la suite entera **también** con la preferencia activa
(proyecto `chromium-movimiento-reducido`).

### 2.2 Una animación que retrasa una acción es un defecto

En el mostrador, una transición de 300 ms repetida doscientas veces al día es un minuto perdido.
Quien trabaja con el sistema ocho horas no ve una animación bonita: ve una espera.

Es también el motivo por el que se descartó una biblioteca entera (Sileo, bitácora §7).

### 2.3 Nada de opacidad sobre texto

Un texto al 55 % de opacidad no cumple contraste, y ha vuelto dos veces en componentes
distintos. El estado se dice con **borde, insignia con texto o palabra**, nunca bajando la
opacidad de algo que hay que leer.

---

## 3. Cuándo se permite una animación expresiva

Cumplidas las tres de arriba, una animación expresiva entra si:

1. **No es la única forma de enterarse.** El §1.
2. **No retrasa nada.** El §2.2. En la práctica: la acción ocurre y la animación la acompaña,
   nunca al revés.
3. **Dura lo que dice el token, no lo que le parezca a quien la escribe.** Ver §4.
4. **Se puede quitar sin rehacer la pantalla.** Si al desactivarla queda un hueco, un salto o un
   estado ambiguo, la pantalla dependía de ella y hay que rehacerla, no ajustarla.

---

## 4. Las duraciones no se eligen, se leen

Están en `frontend/src/shared/styles/tokens.css:119`, y son tres porque hay tres tamaños de cosa
que se mueve:

| Token | Cuánto | Para qué |
|---|---|---|
| `--duracion-control` | 120 ms | Un botón, una casilla, un cambio de estado |
| `--duracion-mensaje` | 180 ms | Un aviso que aparece, una fila que entra |
| `--duracion-superficie` | 240 ms | Un cajón, un diálogo, algo que ocupa media pantalla |

**Salir es más rápido que entrar** (`--duracion-salida-*`): entrar tiene que dejarse ver, salir
solo tiene que quitarse de en medio.

Las curvas —`--curva-entrada`, `--curva-salida`— arrancan con tangente vertical para que el
movimiento **responda al instante** y frene al final. Una curva que arranca despacio se siente
como retraso, aunque dure lo mismo.

Con movimiento reducido, todo pasa a `--duracion-cambio-reducido` (80 ms) de opacidad y nada más.

**Un número escrito a mano en un `transition` es un defecto**, igual que un color escrito a mano.

---

## 5. Un indicador de espera no es una animación

Se rige por otra regla, y conviene no confundirlas:

- **Por debajo de un segundo no se muestra nada.** Un indicador que parpadea 200 ms hace la
  espera más larga, no más corta.
- **Cuando aparece, dice qué está esperando, con palabras visibles.** Un anillo girando sin texto
  no informa: quien lo ve sabe que algo pasa, no qué. Y con movimiento reducido el anillo se
  detiene — si el texto no está, no queda nada.

---

## 6. Cómo se comprueba

No de vista:

- El proyecto `chromium-movimiento-reducido` del arnés corre **todo** con la preferencia activa.
- `e2e/tests/transversal.spec.ts:164` afirma que con la preferencia el indicador se detiene y
  **sigue diciendo qué espera**.
- Cualquier flujo que dependa de una animación tiene que tener su prueba **en los dos
  proyectos**. Si solo pasa en uno, el flujo depende del movimiento y eso es el defecto.
