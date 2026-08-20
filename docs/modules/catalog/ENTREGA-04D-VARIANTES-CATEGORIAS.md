# M01 · Entrega 04D — Variantes y categorías

**La última del paso 4**, y la que más criterios cierra: cinco de los diez que siguen abiertos
esperan a esta pantalla.

Refina el SPEC para su alcance. Las reglas de datos y el contrato no se repiten.

---

## 1 · Alcance

- **La tabla de presentaciones** en el formulario de producto: pasar de una a varias, y volver.
- **La asignación de categorías** con el control N:M y su principal.
- **De propina, la asimetría del contrato** — ver §5.

---

## 2 · El momento, que es casi toda la entrega

La persona lleva un rato escribiendo código, código de barras y precio **como campos del
producto**. Pulsa el botón. Diseño lo resolvió así, y el orden importa:

1. **Los valores se quedan donde estaban**, convertidos en la primera fila.
2. **Un aviso `role="status"`** lo dice con palabras, **sin robar el foco**.
3. **Entrada de 180 ms y el cursor en la segunda fila**, que convierte el aviso en instrucción.

Y el argumento que fija el orden, que conviene entender antes de construirlo:

> **Lo que impide leerlo como pérdida no es el movimiento, es que los valores estén ahí.** Un
> campo con «PLU-ART-PG» dentro no se lee como borrado, se anime o no. Con movimiento reducido
> no queda nada de la animación: si la seguridad dependiera de ella, **para esas personas no
> habría seguridad**.

**Comprobación obligatoria: el momento tiene que funcionar entero con `prefers-reduced-motion`
activo.** Ya tienes el proyecto de Playwright que lo emula.

## 3 · La vuelta atrás cambia de nombre

El mismo botón, dos textos, según si volver destruye algo:

| Segunda fila | Botón |
|---|---|
| Vacía | **«Volver a una sola presentación»** |
| Con algo escrito | **«Quitar la última presentación»** |

Es la regla de la casa aplicada a un botón que cambia de significado con el estado.

## 4 · Ninguna celda de precio en blanco

Donde no hay precio propio, la celda dice **de qué hereda y con qué valor**: «Hereda S/ 4.90»,
«Hereda: a consultar», «Hereda: gratis». **Y es pulsable**, para pasar a precio propio con el
heredado ya cargado.

Sin eso, heredar un número y heredar un «a consultar» se ven igual, y no son lo mismo para quien
vende.

### Y una pregunta abierta que aparece aquí por primera vez

**La tarjeta del listado público no tiene selector de variante, pero enseña un precio.** Hoy es
el de la primera.

> **¿Qué enseña cuando tres presentaciones cuesten distinto?**

«Desde S/ 5,50» es lo honesto. Diseño resolvió el precio grande de la ficha —sigue a la
selección— pero **la tarjeta no tiene selección que seguir**.

**Decídelo y dilo**, porque es la primera entrega en la que se puede crear un producto con
precios distintos y por tanto la primera en la que la tarjeta puede mentir.

## 5 · Las tres frases, y la que no lo es

| Cuándo | Qué comunica |
|---|---|
| Se desactiva la última presentación activa | No puede quedarse sin ninguna. **La salida es desactivar el producto**, y la frase lo propone |
| El código ya existe | Único **en toda la instalación**: el choque puede ser con otro producto, y hay que decir con cuál |
| **Dos presentaciones sin código** | **No es un conflicto.** Ni marca ni asterisco: una frase que dice que así está bien, porque dos casillas vacías seguidas parecen un olvido aunque no lo sean |

## 6 · En móvil deja de ser tabla

Una tarjeta por presentación. Se pierde comparar de un vistazo; **no se pierde ningún campo ni
ninguna acción**. La cabecera se queda en el ordinal, porque el eje ya titula el campo de
dentro.

---

## 7 · Dos cosas de sistema, y una puede que no exista

### `Table` es de solo lectura — compón a mano

No tiene noción de fila con campos, ni de fila en conflicto, ni de columna cuyo control cambia
según el valor. **Compón la rejilla a mano, y anota que es el caso uno.**

Se extrae cuando aparezca el segundo, igual que `ImagePicker`: se compuso en marcas, se extrajo
en categorías. **Una `Table` editable es cara para construirla sobre una predicción.**

### El nombre accesible sin etiqueta visible — compruébalo antes

Diseño perdió la asociación entre etiqueta y campo al usar `Input` suelto dentro de una fila.
**Pero tú confirmaste que `Button` e `Input` reenvían las props que no conocen**, así que puede
que `aria-label` ya funcione y el hueco sea de su herramienta.

**Compruébalo citando archivo y línea antes de añadir nada.**

## 8 · La asimetría del contrato

Ahora sí. El alta acepta `code` y `barcode` como campos del producto y la edición no, y por eso
hacen falta dos peticiones.

**Un `PUT` de producto que acepte los campos de la variante única cuando hay exactamente una las
convierte en una sola, atómica.** La regla que hace falta —«solo cuando hay una»— se escribe
sola ahora que tienes delante el caso de varias, que es por lo que se aplazó hasta aquí.

Y hay que decidir qué pasa si llegan esos campos **con varias presentaciones**: rechazar con una
frase, o ignorarlos. Rechazar parece mejor: ignorar en silencio es cómo se pierde una edición.

---

## 9 · Verificación

Los cinco criterios que esperan a esta entrega:

- [ ] **El caso del plumón:** tres presentaciones de color, un nombre, una descripción, un
      precio y **tres códigos de barras distintos**; cada uno resuelve a la suya
- [ ] **El caso del cuaderno:** dos presentaciones con `price_override` distinto conviven con el
      `list_price` del producto
- [ ] **Dos presentaciones con `code` nulo conviven** sin violar la unicidad, y la interfaz no
      sugiere que falte algo
- [ ] Desactivar la última activa **propone desactivar el producto**
- [ ] **Borrar un archivo usado por marca, categoría, presentación y producto**: los tres
      primeros quedan sin imagen, el cuarto pierde esa fila de galería, y **ninguna operación
      falla con un error de base**

Y lo de siempre:

- [ ] **El momento del §2, entero, con movimiento reducido activo**
- [ ] Prueba de extremo a extremo, dos vueltas limpias
- [ ] Teclado completo, foco visible, `axe` limpio en los dos temas y en el proyecto de
      movimiento reducido
- [ ] Móvil a 390 px: la tabla se convierte en tarjetas sin perder campos ni acciones

## 10 · Qué devolver

- **Cuántos criterios del SPEC cierras enteros.** Con esta entrega deberían quedar muy pocos
  abiertos, y los que queden son de cierre.
- Tu decisión sobre el precio de la tarjeta cuando las presentaciones cuestan distinto.
- Si `aria-label` ya funcionaba.
- Qué patrón te obligó a dudar.
