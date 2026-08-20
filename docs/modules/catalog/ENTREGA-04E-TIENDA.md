# M01 · Entrega 04E — La tienda pública

**Va antes que 04D** porque aquella espera a que Diseño entregue la tabla de variantes. Ésta no
espera a nadie: está diseñada y **usa los componentes que acabas de construir**, que es lo que
convierte su «hecho» provisional en verificado.

Refina el SPEC para su alcance. Las reglas de datos y el contrato no se repiten.

---

## 1 · Alcance

Las tres rutas públicas: **`/catalogo`**, **`/catalogo/:categoria`** y **`/producto/:slug`**.

Los públicos devuelven **solo** `is_active AND is_public`. **Un producto despublicado responde
404, no 403**: no se filtra información sobre lo que existe.

**Los datos de prueba se crean por API**, como ya hiciste con el recuento de categorías. La
pantalla de administración de variantes no existe todavía y no hace falta que exista para
probar esto.

---

## 2 · Los seis problemas que ya están resueltos en el diseño

No hay que inventarlos, hay que construirlos:

### El producto sin foto no deja hueco

El cuadrado lo ocupa **el nombre corto en grande con su categoría encima**. Misma altura,
cuadrado lleno: la mezcla se lee como variedad, no como catálogo a medio llenar. En la ficha
pasa a **16:9**, porque sin nada que encuadrar un cuadrado grande solo estira la página.

### El precio, con sus tres estados

| | Cómo se comporta |
|---|---|
| Un número | Normal. **No lleva nota** |
| **Cero** | **Como precio**: grande, negrita, con una afirmación debajo |
| Vacío | **A consultar**: pequeño, apagado, y dice explícitamente que no es gratis |

Que solo los dos casos raros se expliquen es lo que hace que funcione.

### La miga que dice otra cosa de la que venías

El cono está en «Deporte» y «Juguetes», y la miga **usa solo la principal**. Quien navega
Juguetes entra y lee «Deporte».

**La versión corta es la de por defecto.** La larga —«‹ Volver a Juguetes» más la frase que
explica— es el añadido cuando hay origen, y **el origen se pierde al recargar**. Quien llega
desde un enlace compartido no tiene ninguno, y así es como se comparte un producto aquí.

**Y el caso que ya resolviste en el backend:** si la categoría principal está desactivada,
`ChooseTarget` cae a otra activa del producto. **Aquí es donde eso se ve por primera vez** —
compruébalo de punta a punta.

### Las variantes, invisibles mientras haya una

Con una sola, la palabra no aparece. Con dos o más, el bloque **se titula con lo que varía** —
«Color de la tinta»— y nunca con la palabra «variante».

**Y la frase del precio sale del dato:** si todas coinciden, «cuestan lo mismo»; si alguna trae
`price_override`, cambia sola y cada opción muestra su importe. Escrita a mano, miente el día
que alguien ponga un precio distinto.

### Buscar y no encontrar

Resultados en vivo. **El término buscado se queda en el campo** — corregir es más rápido que
reescribir. Y las dos ausencias no son la misma:

- **`NoResults`** para «no hay resultados para *tornillo*»: sin acción principal, porque el
  arreglo ya está en pantalla y competiría con el campo.
- **`EmptyState`** para una categoría sin productos todavía: no es error de nadie, y **no
  promete fecha**.

### Móvil primero

Los filtros son **`FilterChip` envueltos, no barra lateral**. En móvil los controles van a `lg`;
**en escritorio ceden a `sm`**, porque se pulsan con ratón. Y la paginación tiene que poder
usarse con el pulgar: es la única forma de llegar a la página 2.

**Cualquier cambio de filtro vuelve a la página 1.**

---

## 3 · Lo que estrena de verdad

`FilterChip`, `NoResults` y la nota de `EmptyState` **no los usa ninguna pantalla todavía**. Ésta
es la primera. Si alguno no encaja al montarlo, **eso es el caso real** y vale más que la
especificación: dilo.

---

## 4 · Verificación

- [ ] Prueba de extremo a extremo, **dos ejecuciones seguidas limpias**
- [ ] **El caso del cono entero:** un producto en dos categorías aparece en el listado de las
      dos, y su miga usa solo la principal
- [ ] **Buscar `lapiz` devuelve `LÁPIZ`** — cierra la mitad que le faltaba al criterio de
      `ARTESCO`/`Artesco`
- [ ] Producto con la categoría principal desactivada: la miga cae a otra activa
- [ ] Un producto despublicado responde **404**
- [ ] Cero y vacío se distinguen sin leer la letra pequeña
- [ ] Rejilla con productos con y sin foto, sin parecer rota
- [ ] Paginación usable a 390 px, y cambiar de filtro vuelve a la página 1
- [ ] Teclado completo, foco visible, `axe` limpio en los dos temas **y con movimiento reducido**
- [ ] Con M01 desactivado: las tres rutas públicas desaparecen y **el home no renderiza la
      sección vacía — no la renderiza en absoluto**

## 5 · Qué devolver

- **Qué criterios del SPEC cierras enteros.** Con el cono y la búsqueda, deberían ser dos más.
- Si alguno de los tres componentes nuevos no encajó al montarlo.
- Qué patrón te obligó a dudar.
- Nada marcado que no esté ejecutado.
