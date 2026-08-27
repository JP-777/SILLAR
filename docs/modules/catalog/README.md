# M01 · Catálogo

Para quien llega sin haber estado. Lo que hace el módulo, dónde acaba, de qué depende y qué se
decidió que no es obvio.

- **Código:** `catalog` · **Schema:** `catalog` · **Estado:** terminado (paso 5 cerrado)
- La especificación completa está en `SPEC.md`; el modelo de datos, en `DATOS.md`. Esto es el
  mapa, no el territorio.

---

## 1. Qué hace, y dónde se para

**Registra y publica qué vende el negocio:** marcas, categorías, productos, sus presentaciones,
sus imágenes y la búsqueda. Y la tienda pública que lo enseña.

**Dónde se para, que es lo que más se confunde:**

> **M01 describe el producto. No dice cuánto hay, ni dónde, ni a quién se le vende más barato.**

| Esto es de M01 | Esto no |
|---|---|
| Nombre, descripción, imágenes, marca | **Cuánto queda** → M09 Inventario |
| Precio de lista, y el propio de cada presentación | **Existencia por local** → M17 |
| Código y código de barras | **Precio por mayor, descuento a conocido** → M13 Mostrador |
| Unidad de venta, como texto libre | **Conversión paquete → unidad** → M09 |
| Categorías, y cuál es la principal | **Listas de útiles por grado y colegio** → M07 |

Esa frontera es lo que mantiene a M01 común a los dos productos de la familia. Un catálogo que
exigiera stock dejaría fuera a los restaurantes y a los servicios, que son clientes válidos de
M01 **solo**.

---

## 2. De qué depende, y quién puede depender de él

**Depende de CORE**, y de nada más:

- `IMediaStorage` para las imágenes. Las cuatro claves foráneas de M01 apuntan a
  `core.media_assets`, y por eso las tablas con imagen se replican como ella (ADR-018).
- `IAuditWriter`, `ICurrentAdmin`, `ISettingsReader`.

**Quién va a depender de M01:** M03 clientes, M09 inventario, M13 mostrador, M15 compras. Todos a
través de `Sillar.Modules.Catalog.Contracts`, nunca del `Domain` ni del `Data`.

### Lo que el contrato expone hoy

`ICatalogService`, cinco operaciones, todas devolviendo `ItemSnapshot` — que es la
**presentación**, no el producto, porque es el nivel que se cuenta y se cobra:

| Operación | Para qué existe |
|---|---|
| `ObtenerItemAsync(itemId)` | Resolver una línea de venta o de inventario |
| `BuscarPorCodigoAsync(codigo)` | La caja: se teclea o se lee un código |
| `BuscarAsync(texto, limite)` | Buscar para elegir, en cualquier pantalla que lo necesite |
| `VariantesDeAsync(productId)` | Enseñar las presentaciones de un producto |
| `ItemExisteYEstaActivoAsync(itemId)` | Validar antes de escribir en otro schema |

`ItemSnapshot` lleva: `ItemId`, `ProductId`, `ProductName`, `VariantValue`, `Code`, `Barcode`,
`Price`, `SaleUnit`.

### Lo que **no** expone, y conviene saberlo antes de pedirlo

**Ningún módulo lo ha consumido todavía.** Está implementado
(`Sillar.Modules.Catalog/Services/CatalogService.cs`) y registrado
(`CatalogModule.cs:86`), pero M02 va a ser el primer cliente real.

Mirándolo con ojos de quien va a consumirlo, el contrato está pensado para **el mostrador y el
inventario**, y ahí no le falta nada. Para un módulo de contenido le faltarían dos cosas:

- **El `slug`**, que es lo único con lo que se puede enlazar un producto desde una página.
- **La imagen**, que es lo que hace que un «producto destacado» sea algo más que una línea de
  texto.

**Ya no faltan: se decidieron con el caso delante.** M02 llegó pidiéndolos, y en vez de ampliar
`ItemSnapshot` —que es el congelado de la venta, y meterle campos de presentación web contamina
un registro transaccional— se expuso `ProductPickerItem` aparte, con
`BuscarParaSeleccionAsync` para elegir y `ObtenerParaSeleccionAsync` para releer.

Lleva más de lo que se había previsto mirándolo: además del slug y la imagen, **el precio ya
resuelto, la categoría efectiva, si está publicado y si sigue de alta**. Cuatro campos que
nadie vio al examinar el contrato desde fuera y que salieron de usarlo.

Y una asimetría que conviene conocer antes de leer el código: **buscar esconde las bajas y
releer las devuelve marcadas**. No se elige lo que está de baja, pero quien ya eligió necesita
distinguir «lo dieron de baja, puede volver» de «ya no existe» — que es lo que significa `null`,
y solo puede significar eso porque en SILLAR no hay borrado físico.

### Y antes de ampliar nada, la pregunta que cambia el diseño

Es fácil plantearlo como «cómo le pido productos a M01». **Casi siempre es la pregunta
equivocada.** La buena es:

> **¿Qué necesita M01 exponer para aportar su propia sección?**

Las dos formas se parecen y no son la misma. En la primera, otro módulo pide datos de productos,
decide cómo se ven y **depende de M01**. En la segunda, M01 aporta lo suyo y el otro no lo
conoce: es una dependencia menos, y la que desaparece sola cuando M01 no está instalado — que es
justo lo que el criterio de terminado exige de los dos.

**Y aquí hay que ser exacto sobre lo que hoy existe y lo que no**, porque son dos mecanismos
distintos y solo uno está construido:

| | Cómo se compone hoy |
|---|---|
| **El menú del panel** | Registro declarativo: cada módulo aporta su entrada y el armazón filtra por módulos activos. `layout/navigation.ts:46` y `:49`. **Este es el mecanismo bueno** |
| **La portada pública** | Un `if` con el código del módulo escrito a mano: `platform/PublicSite.tsx:19` pregunta `has('catalog')` y renderiza el enlace al catálogo desde el propio armazón |

O sea: para la portada, «cada módulo aporta su sección» **es la intención, no lo que hay**. Con
un solo módulo publicable el `if` era correcto y no había con qué generalizar.

**El segundo módulo con portada es el segundo caso**, y es entonces cuando toca extraer — igual
que `ImagePicker`, que se compuso en marcas y se extrajo en categorías. Si al llegar ahí se
añade un `if` más en `PublicSite`, la regla se convierte en silencio en «se copió en el
segundo, el tercero y el cuarto».

---

## 3. Las decisiones que no son obvias

Cada una con su porqué y dónde está escrito.

### La variante es invisible mientras haya una sola

Todo producto nace con **exactamente una** presentación, sin nombre, creada sola. Mientras solo
haya una, la palabra «variante» no aparece en ninguna pantalla: código, código de barras y precio
se editan como si fueran campos del producto — y lo son, de su variante única.

> **La tabla de presentaciones no aporta «variante»: aporta «más de una».**

Obligar a pensar en variantes para dar de alta un plato de menú es cargarle a todo el mundo la
complejidad de unos pocos. `SPEC.md` §4.2, y la interacción completa en
`ENTREGA-04D-VARIANTES-CATEGORIAS.md` §2.

### Un producto está en varias categorías, y una es la principal

N:M desde el principio, porque hay segundo y tercer caso: los conos son deporte **y** juguete;
una calculadora es tecnología **y** material del curso de matemáticas.

La principal da la miga de pan. **No da la URL**: la dirección pública es el slug del producto,
así que cambiar de categoría principal no rompe ningún enlace. `SPEC.md` §4.1.

**Y el respaldo, que es lo que casi nadie prevé:** si la categoría principal está desactivada, la
miga cae a otra activa del producto, sin promover nada en la base. `primary_category_id` se queda
como estaba, porque desactivar una categoría no es decidir cuál es la principal. Regla 6 del
`SPEC.md`.

### El slug no cambia solo

Se genera del nombre al crear, se corrige a mano, y **al renombrar el producto no se toca**.
Cambiarlo rompe los enlaces que ya circulen — que es exactamente lo que un catálogo público no se
puede permitir. Se edita aparte, a propósito, con la advertencia escrita al lado.

### Nulo no es cero

**Precio nulo es «a consultar». Precio cero es gratis.** No se confunden ni al editar ni al leer,
y la tienda explica **solo los dos casos raros**: si todo llevara nota, la nota dejaría de
significar algo.

### La tarjeta pública enseña el mínimo, y lo dice

Cuando las presentaciones cuestan distinto, la tarjeta —que no tiene selector— enseña el
**mínimo efectivo** con «Desde S/ X». Y si alguna es «a consultar», **toda la tarjeta lo es**:
«desde» promete una cota, y una presentación sin precio puede costar cualquier cosa.
`ItemPricing.ForCard`, con sus pruebas en `Sillar.Modules.Catalog.Tests/ItemPricingTests.cs`.

### Dos presentaciones sin código conviven

La unicidad de `code` no se viola con dos nulos, y la interfaz **lo dice con una frase** en vez
de marcarlo: dos casillas vacías seguidas parecen un olvido aunque no lo sean. No es un
conflicto, así que no se pinta como tal.

### Desactivar una categoría no actúa en cascada

Sus productos siguen ahí. El sistema **avisa con el recuento antes** y la persona decide. Ojo a
la diferencia con marcas, donde no hay recuento: ahí el SPEC no lo pide y contar habría sido
inventar trabajo.

---

## 4. Qué hay que saber para tocarlo

- **Las migraciones son la fuente de verdad del esquema** (ADR-009). El seed
  `database/modules/catalog/02_seed.sql` está **vacío a propósito** y tiene que seguir estándolo:
  un producto de ejemplo dentro del seed acaba instalado en casa de un cliente. Los datos de
  demostración viven en `scripts/demo/`.
- **Las dos colaciones no se confunden:** `core.es_ci` para identidad y unicidad —`ARTESCO` choca
  con `Artesco`—, `core.es_search` para lo que se busca. Y la búsqueda de texto no va por
  colación sino por el índice GIN con `spanish_stem`, que es lo que hace que «lapiz» encuentre
  «LÁPIZ» (`DATOS.md` §4).
- **PostgreSQL no admite expresiones regulares sobre colaciones no deterministas.** El `CHECK`
  del formato del slug lleva `COLLATE "C"` dentro por eso.
- **Una tabla que se replica no puede referenciar a una que no se replica** (ADR-018). Al
  escribir cualquier FK, comprobar que las dos están del mismo lado.

---

## 5. Cómo se comprueba que sigue en pie

El módulo tiene sus 17 criterios en `SPEC.md`, cada uno con la prueba que lo cierra. Los que
conviene conocer:

| Qué | Dónde |
|---|---|
| El recorrido entero, de crear a publicar | `e2e/tests/recorrido.spec.ts` |
| Presentaciones: el plumón, el cuaderno, la última activa | `e2e/tests/presentaciones.spec.ts` |
| La tienda pública: el cono, la búsqueda, el 404 | `e2e/tests/tienda.spec.ts` |
| Móvil a 390 px y teclado, todas las pantallas | `e2e/tests/movil-teclado.spec.ts` |
| **Desactivar no borra**, y el schema se elimina sin tocar `core` | `e2e/tests/zz-desmontaje.spec.ts`, `zz-instalacion.spec.ts` |
| Que ningún endpoint devuelva 500 contra una base real | `e2e/tests/api-traduccion.spec.ts` |

Ese último existe por una razón que conviene recordar: `GET /brands` devolvió 500 durante días
sin que nadie lo supiera, porque **las pruebas de lógica no tocan la base** y nada que solo se
rompa cuando EF traduce a SQL es visible para ellas.
