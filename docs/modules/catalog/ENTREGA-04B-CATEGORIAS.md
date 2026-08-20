# M01 · Entrega 04B — Categorías

**Refina el SPEC de M01 para su alcance.** Las reglas de datos y el contrato no se repiten:
están en el §7 y el §8 del SPEC y en `DATOS.md`.

Marcas cerró bien y dejó los patrones fijados. Esta entrega es **el segundo caso real**, y eso
cambia lo que hay que hacer: donde marcas declaró, categorías extrae.

---

## 0 · Antes de la pantalla — ¿cuántos endpoints nunca se han ejecutado?

`GET /api/admin/catalog/brands` devolvía 500 desde el 16 de agosto y nadie lo supo hasta que
una pantalla lo llamó. `CategoryService.cs:147` tiene el mismo defecto. **Van dos, y no
sabemos cuántos hay.**

La causa está en una regla correcta con un punto ciego: *«las pruebas de lógica no tocan la
base de datos»*. Nada que solo se rompa **cuando EF traduce a SQL** es visible para ellas. La
verificación del paso 3 cubrió todo lo difícil —el restaurante, el plumón, el cono, la
unicidad, el 403 sin CSRF— y se saltó «listar marcas», porque listar no parecía arriesgado.

**Empieza contestando la pregunta, no arreglando un caso.** Recorre los endpoints de M01 y
comprueba cuáles se han ejecutado alguna vez contra una base real. Lo barato es una
comprobación que llame a cada uno una vez y afirme que **no devuelve 500** — no es una suite de
pruebas, es un detector de traducción, y habría cazado los dos.

Devuelve **la cifra** antes de seguir: cuántos endpoints tiene M01, cuántos están cubiertos y
cuántos no. Si aparecen más de dos rotos, para y dilo: cambia lo que hay que hacer después.

`CategoryService.cs:147` se arregla aquí, ahora que entra en alcance y puede llevar su prueba.
Tu decisión de no tocarlo en 04A fue correcta.

---

## 1 · La extracción — esto es lo que hace distinta a esta entrega

En 04A decidiste que M01 declarara su propio `services/media.ts` en vez de importar de CORE o
subirlo a `shared/`. **Fue lo correcto**: un módulo nunca importa de otro, y no había segundo
caso.

**Categorías es el segundo caso.** Así que aquí toca extraer, no copiar:

- `services/media.ts`
- `components/LogoPicker.tsx` — **este es el que más importa**, y no estaba en la duda que
  planteaste: productos también lo va a necesitar, así que serán tres.

Si al mirarlo de cerca resulta que **no son la misma cosa** —que categorías necesita algo que
marcas no, o al revés—, eso también es una respuesta válida: dilo y se quedan separados. Lo que
no vale es copiar y seguir, porque entonces «se generaliza en el segundo caso» se convierte en
silencio en «se copió en el segundo, el tercero y el cuarto», y al extraer hay que fusionar
tres versiones que ya divergieron.

---

## 2 · Lo que tiene categorías y marcas no tenía

- **Es un árbol.** Categorías anidadas: cómo se ve la jerarquía, cómo se elige el padre al
  crear, y qué pasa al mover una rama. El SPEC no obliga a resolver la navegación completa del
  árbol; sí a no mentir sobre él.
- **La regla 9, que es el conflicto de verdad:** desactivar una categoría **no desactiva sus
  productos y no actúa en cascada.** El sistema avisa cuántos quedan sin esa categoría y la
  persona decide. Ojo a la diferencia con marcas: **aquí sí hay recuento**, porque el SPEC lo
  pide explícitamente. En marcas decidiste no contar y era correcto; aquí lo contrario.
- **Ciclos.** La lógica de detección ya está probada del paso 3. Lo que falta es qué ve alguien
  que intenta hacer padre de A a su propio hijo: una frase que diga qué lo impide.

Y lo de siempre: los cuatro estados, ningún color escrito a mano, teclado con foco visible,
`axe` limpio en los dos temas, y con M01 desactivado ni entrada de menú ni ruta muerta.

---

## 3 · Dos cosas que arrastra 04A

**El `opacity` sobre texto va por la segunda vez.** Primero `.mod.is-off` al 62 %, ahora la fila
atenuada al 55 % con 2.14:1. Las dos veces se corrigió la instancia y volvió en otro
componente. Tu arreglo —borde, porque el estado ya lo dice una insignia con texto— generaliza:
**escríbelo como regla** junto a la de `--link` y `--on-danger`, que es de la misma familia.
Una lección anotada sin regla no ha impedido nada las dos veces anteriores.

**Y una pregunta sobre `useDelayedFlag`:** ¿retrasa solo la aparición a los 200 ms, o también
garantiza un **mínimo de permanencia** una vez que aparece? Sin lo segundo, una respuesta que
llega a los 210 ms enseña el indicador 10 ms, que parpadea peor que no mostrarlo. Si falta, es
aquí donde se añade.

---

## 4 · Verificación de esta entrega

- [ ] La cifra del §0, respondida
- [ ] `CategoryService.cs:147` arreglado **con prueba que lo cubra**
- [ ] Prueba de extremo a extremo de la pantalla, dos ejecuciones seguidas limpias
- [ ] Los cuatro estados, no solo «con datos»
- [ ] Desactivar una categoría con productos **avisa con el recuento** y no actúa en cascada
- [ ] Intentar crear un ciclo da una frase que dice qué lo impide
- [ ] Teclado completo, foco siempre visible, `axe` limpio en los dos temas
- [ ] Con M01 desactivado: arranca, sin entrada de menú, sin ruta muerta, sin hueco

**Y la honestidad de siempre:** di qué criterios del SPEC quedan a medias y **no marques
ninguna casilla** que no esté entera. Con marcas y categorías hechas, alguno puede estar cerca
de cerrarse — el de `ARTESCO`/`Artesco` sigue esperando su otra mitad, que es la búsqueda.

---

## 5 · Qué devolver

- La cifra del §0, primero.
- Si `media.ts` y `LogoPicker` se extrajeron, o por qué no eran la misma cosa.
- Qué patrón te obligó a dudar, si alguno.
- El informe de decisiones, con `REVERSIBLE` primero si alguna no lo es.
