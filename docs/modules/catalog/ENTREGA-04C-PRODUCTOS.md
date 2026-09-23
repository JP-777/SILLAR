# M01 · Entrega 04C — Productos

**Refina el SPEC de M01 para su alcance.** Las reglas de datos y el contrato no se repiten:
están en el §7 y el §8 del SPEC y en `DATOS.md`.

Marcas fijó los patrones. Categorías fue el segundo caso y forzó la extracción. **Ésta es la
pantalla que el módulo existe para tener**, y por eso se parte.

---

## 1 · Qué entra y qué no, y por qué

El formulario de producto es el más grande de M01. Cabe entero en un ciclo solo a costa de
hacerlo mal, así que se corta por donde el propio SPEC ya lo corta:

**Entra:**

- `/admin/catalogo/productos` — listado con filtros, y ficha.
- **El producto con una sola variante**, que es la mayoría: código, código de barras y precio se
  editan **como si fueran campos del producto**, porque lo son de su variante única.
- Slug: generado del nombre, corregible a mano, y **nunca cambia solo al editar el nombre**.
- Imágenes desde la galería de CORE, con orden y principal.
- La altura explícita de los controles y los tamaños de `Pagination` e `Input` (§4).

**No entra, y va a 04D:**

- **La tabla de variantes múltiples** — el caso del plumón. Es una interacción que Diseño aún no
  ha dibujado, y es la parte que más se puede equivocar.
- **La asignación de categorías**, que espera al selector N:M.

Con eso, 04C cubre **el caso del restaurante y el del cuaderno** enteros, que son dos de los
criterios de cierre del SPEC.

---

## 2 · La regla que gobierna esta pantalla

> **La variante es invisible mientras haya una sola.**

El formulario muestra código, código de barras y precio como campos del producto, y un botón
discreto: *«Este producto viene en varias presentaciones»*. **En esta entrega ese botón puede no
existir todavía** —su contenido es 04D— pero **la forma de la pantalla tiene que ser la que
admita su llegada sin rehacerse**.

Nunca al revés: obligar a pensar en variantes para dar de alta un plato de menú es cargarle a
todo el mundo la complejidad de unos pocos.

**Y al crear un producto se crea su variante única, con `variant_value` nulo, sin que la persona
lo vea.**

---

## 3 · Los cuatro casos que hay que poder hacer

Salen del SPEC y son la prueba de que la pantalla sirve:

| | Qué prueba |
|---|---|
| **El restaurante** | Un plato **sin código, sin código de barras y sin precio**, publicado. Y que **la palabra «variante» no aparezca en ninguna pantalla** |
| **El cuaderno** | Un producto con `list_price`, y su variante única sin `price_override` |
| **El precio que no es número** | **Nulo = «consultar». Cero = gratis.** No se confunden en la interfaz, ni al editar ni al leer |
| **El slug** | Se genera del nombre, se corrige a mano, y **al renombrar el producto no cambia** |

El tercero es el que más fácil se hace mal: un campo numérico vacío y un cero se parecen
demasiado en un formulario.

---

## 4 · Lo que arrastra del sistema de diseño

Va aquí porque esta entrega lo va a chocar de frente, no porque toque ahora:

- **La altura explícita de los controles.** Ningún control con texto la declara hoy; es
  aritmética de relleno y tipo. La desalineación de 46 contra 38,5 entre botón y campo es
  **`line-height: 1.5` en `.ui-input` contra ninguna en `.ui-button`**. Tokens 28/36/46, que son
  casi lo que `Button` ya mide: **fija donde está, no recoloca.**
- **`Pagination` e `Input` con prop de tamaño.** El listado pagina, y el SPEC exige móvil. Hoy
  `Pagination` fija `sm` por dentro: botones de 26,8 px gobernando la navegación del catálogo.
- **Y la regla que lo explica**, que merece quedar escrita: *`sm` vale para un control repetido
  en fila densa con ratón **cuando existe otra manera de hacer lo mismo**.* Esa regla sola
  habría cazado la paginación.

---

## 5 · Verificación de esta entrega

- [ ] Prueba de extremo a extremo, **dos ejecuciones seguidas limpias**
- [ ] Los cuatro estados, no solo «con datos»
- [ ] **El caso del restaurante entero**, incluida la ausencia de la palabra «variante»
- [ ] Nulo y cero se distinguen al editar y al leer
- [ ] Renombrar un producto **no cambia su slug**
- [ ] Borrar una imagen desde la galería deja al producto sin ella, sin fallo de base
- [ ] El listado pagina, y en móvil se puede llegar a la página 2
- [ ] Teclado completo, foco visible, `axe` limpio en los dos temas **y en el proyecto de
      movimiento reducido**
- [ ] Con M01 desactivado: arranca, sin entrada de menú, sin ruta muerta, sin hueco

**Y la honestidad de siempre:** con marcas, categorías y productos, **algún criterio del SPEC ya
puede cerrarse entero.** Míralo uno a uno y marca **solo** los que estén completos. El del cono
y el de las variantes siguen esperando a 04D.

---

## 6 · Qué devolver

- Qué patrón te obligó a dudar.
- Si la forma de la pantalla admite la tabla de variantes sin rehacerse, o si al llegar 04D
  habrá que mover cosas.
- Qué criterios del SPEC cierras enteros, y cuáles quedan a medias y por qué.
- El informe de decisiones, con `REVERSIBLE` primero si alguna no lo es.

---

## 7 · Hallazgo posterior — una operación de imagen no sobrevive al cierre

*23 de septiembre de 2026.*

La puerta de cierre de M04 volvió a ejecutar el recorrido integral de M01 y encontró una
segunda carrera en la ficha de producto.

La protección existente cubría este orden:

1. asociar una imagen;
2. termina el `POST .../images`;
3. empieza el `GET` que recarga la ficha;
4. la persona guarda mientras ese `GET` sigue en vuelo;
5. el guardado cierra la ficha;
6. el `GET` viejo termina.

Ese caso ya estaba protegido con una generación de carga y una prueba que retiene el `GET`.

La puerta encontró otro orden:

1. empieza `POST .../images`;
2. la persona guarda mientras **el propio POST sigue en vuelo**;
3. el `PUT` del producto termina correctamente y la ficha se cierra;
4. recién después termina el POST antiguo;
5. su callback `onChanged()` intenta iniciar **un GET nuevo**;
6. ese GET volvía a escribir `editing` y reabría el cajón.

En la traza real, el `POST .../images` empezó antes del guardado y duró 256.5 ms. El
`PUT .../products/<id>` terminó antes que ese POST. Después apareció un nuevo
`GET .../products/<id>`, exactamente la secuencia que explicaba la reapertura.

La corrección separa dos conceptos:

- `generaciónFicha`: decide qué **carga** es la vigente;
- `sesiónFicha`: decide si el **formulario que originó el callback** sigue vivo.

`cerrarFicha()` invalida ambas. Un callback de una ficha ya cerrada no puede ni siquiera
iniciar una recarga nueva.

La nueva prueba
`Una asociación de imagen que termina después del guardado no reabre el cajón`
retiene deliberadamente el POST de asociación hasta que el guardado ya cerró la ficha.

**Barrera:** antes de la corrección, la nueva prueba quedó roja exactamente porque el drawer
volvió a aparecer. Después de añadir la sesión de ficha pasaron las tres pruebas de
`imagenes-asociadas.spec.ts`.

El recorrido integral que reveló el defecto volvió a pasar en **46.5 s**, por debajo de su
techo global de 60 s. No se restauró ningún timeout especial.
