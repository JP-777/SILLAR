# Decisiones previas de M05a Servicios — Vitrina

Dos decisiones **cerradas** y una **abierta**, tomadas antes de escribir código.

La abierta es la incómoda, así que va dicha de frente: **puede que M05a no llegue a existir como
módulo.** No es una duda de diseño que se resuelva pensando más — es una pregunta que solo se
contesta mirando el mostrador. Lo que sí está cerrado son las dos decisiones de arquitectura, para
que el día que se decida construirlo no haya que volver sobre ellas.

---

## 1 · CERRADA — M05a y M05b no comparten schema

`docs/ARQUITECTURA_MODULAR.md:60-61` les daba a los dos el schema `services`:

```
| M05a | Servicios — Vitrina  | services | CORE                          | MVP     |
| M05b | Servicios — Órdenes  | services | M05a (dura), M04 (blanda)     | Fase 2  |
```

Es **residuo del diseño anterior**, de cuando M05 era un solo módulo y se partió en dos sin repartir
el schema.

### Choca con tres cosas, y solo la tercera la cierra

Las dos primeras son de manual y por sí solas no habrían bastado:

- **`CLAUDE.md`, reglas 1 y 2 de módulos:** cada módulo tiene *su propio* schema y su propio
  `DbContext` con `HasDefaultSchema`, y solo escribe en el suyo. Con schema compartido, la segunda
  regla no se puede ni formular.
- **ADR-009:** cada módulo lleva su historial `__migrations` **dentro de su propio schema**. Dos
  módulos en `services` comparten historial y dejan de poder instalarse por separado.

**La que la cierra es el criterio de terminado**, que es el que se vende:

> Un módulo está terminado cuando **se puede instalar y desinstalar sin romper nada del resto del
> sistema**.

Y la desinstalación de un módulo es literalmente esto —`database/modules/cms/99_drop.sql:11`:

```sql
DROP SCHEMA IF EXISTS cms CASCADE;
```

Con schema compartido, **`99_drop.sql` de M05a destruye las tablas de M05b**, y el de M05b las de
M05a. No es que el criterio fuera difícil de cumplir: **sería imposible**. Y el fallo no avisa —el
`DROP` no da error, simplemente se lleva por delante datos de un módulo que sigue instalado.

**Decisión:** M05a se queda con `services`; **M05b pasa a `service_orders`**.

Arrastra tres cosas más, todas aplicadas en el commit `581ff5e`: la FK de M06 pasa a apuntar a
`service_orders.service_orders`, aparece el proyecto `Sillar.Modules.ServiceOrders`, y
`database/modules/service_orders/` tiene su propia carpeta con su propio `99_drop.sql` — que es el
motivo entero del reparto.

---

## 2 · CERRADA — M05a no replica, **con disparador**

Claves `integer GENERATED ALWAYS AS IDENTITY`, sin `origin_node` ni `row_version`. Las mismas dos
citas que sostienen la decisión equivalente en M07:

- **`services` no está entre los módulos del ERP** — `CLAUDE.md:13` los lista:
  `CORE, M01, M04, M09, M13–M17`.
- **No es uno de los tres datos compartidos de la ADR-017**, que nombra exactamente «catálogo,
  clientes y existencias» (`ADR-017-mando-y-respaldo.md:36`).

### Y aquí va el disparador, porque esta decisión se apoya en algo no comprobado

**Depende de que los servicios no se cobren en el mostrador.** Eso hoy **no está comprobado**: se ha
supuesto.

Si la observación revela que sí —que alguien cobra un anillado en la caja como cobra un cuaderno—,
entonces el día que exista **M13 Punto de Venta** el ERP necesitará conocer los servicios, y esta
tabla pasaría a ser compartida. **Pasar de `integer` a `uuid` con datos dentro es exactamente la
migración cara que la ADR-016 existe para evitar.**

> **Disparador:** al escribir el SPEC de **M13**, o antes si la observación de mostrador lo confirma.

Es el mismo tratamiento que la ADR-016 le dio al único caso que dejó abierto
(`ADR-016-identificadores-replicables.md:61`):

> «La única excepción a revisar es `core.admin_users`: si el personal debe poder entrar en cualquier
> sucursal con la misma cuenta, esa tabla se replica y necesita `uuid`. **Queda pendiente de decidir
> en el SPEC de M16**, no antes.»

Misma forma: se decide lo razonable hoy, se nombra la condición que la invalidaría, y **se ata a un
documento concreto** para que la revisión ocurra el día que toca y no cuando alguien se acuerde.

---

## 3 · ABIERTA — ¿existe M05a como módulo?

### Los dos ejemplos que la arquitectura da ya funcionan como productos de M01

No es una conjetura: están en el catálogo de demostración, construidos y funcionando.

- **Anillado** — `scripts/demo/datos.mjs:238`: *«Anillado espiral por documento hasta 100 hojas»*,
  con `precio: null` y `unidad: 'Por documento'`.
- **Impresión** — `scripts/demo/datos.mjs:248`: *«Impresión láser blanco y negro por hoja A4»*, con
  `precio: null`.

`precio: null` es **«a consultar»**, que en M01 es un estado de primera clase y no un hueco:
*«Nulo = "consultar precio"»* (`docs/modules/catalog/SPEC.md:192`), y *«Cero no es lo mismo que nulo:
cero es gratis»* (`:206`).

### Y M01 fue diseñado para admitirlos — no es un accidente

Es la parte que convierte esto de coincidencia en indicio. El docstring de `SaleUnit`, en
`backend/Sillar.Modules.Catalog/Domain/Product.cs:71-72`:

> «Texto libre y no una lista cerrada. Un plato no se vende «por unidad», y **una lista cerrada
> dejaría fuera a los restaurantes y a los servicios**.»

Quien escribió M01 **ya tenía los servicios delante** y decidió que cupieran. La misma frase está en
el §1 del SPEC de M01: vendible solo *«como catálogo de exhibición sin venta […] una lista de
productos con precio a consultar»* (`docs/modules/catalog/SPEC.md:21`).

**Y las modalidades también están resueltas.** Un servicio con variantes —anillado con tapa
transparente o con tapa dura, impresión a una cara o a dos— es lo mismo que un producto con
presentaciones: `Product.VariantLabel` (`Product.cs:83`) nombra el eje, `ProductItem.VariantValue`
(`ProductItem.cs:32`) el valor, y `ProductItem.PriceOverride` (`:59`) el precio que se sale de la
norma.

### La pregunta

> **¿Qué hace M05a que M01 no haga ya?**

Si la respuesta es **nada**, entonces el requisito del PRD —que los servicios permanentes no queden
escondidos— es **de presentación**: una categoría, un bloque en portada, un filtro. Y M05a no debería
existir como módulo, porque un módulo que solo aporta una vista de datos que ya están es un schema,
un `DbContext`, un `IModule` y un ciclo de instalación a cambio de nada.

**No la puedo contestar yo ni la puede contestar JP desde el escritorio.** Está en el mostrador, y ya
está formulada en la guía de observación —`docs/GUIA-OBSERVACION-MOSTRADOR.md:63`:

> «Servicios: fotocopias, anillado, impresiones. **¿Se cobran igual que un producto? ¿Cómo se cuenta
> la cantidad?**»

> **Disparador:** la visita al mostrador. Con esas respuestas se decide entre tres salidas —M05a se
> construye como módulo, basta una extensión de M01, o el requisito se resuelve en presentación— y
> las tres son aceptables. La que no lo es es construirlo sin haber preguntado.

### Una nota sobre el orden, porque afecta al ROADMAP

M05a está en la **posición 6** del ROADMAP, dentro de la Fase 1. Si la respuesta llega tarde, lo que
pasa no es que se retrase M05a: es que **se construye antes de saber si hace falta**. Por eso la
anotación quedó en `docs/ROADMAP_MODULAR.md:69` junto a su fila, y no solo aquí.

---

```
DECIDÍ        M05b pasa a schema propio `service_orders`; M05a se queda con `services`
DESCARTÉ      Mantener el schema compartido de ARQUITECTURA_MODULAR.md:60-61
POR QUÉ       El 99_drop.sql de cualquiera de los dos destruye las tablas del otro, sin dar error
REVERSIBLE    Sí hoy — ninguno de los dos módulos existe todavía

DECIDÍ        M05a no replica: integer identity, sin origin_node ni row_version
DESCARTÉ      uuid v7 replicable «por si acaso»
POR QUÉ       services no está en los módulos del ERP (CLAUDE.md:13) ni en los tres de ADR-017:36
REVERSIBLE    No con datos dentro. Por eso lleva disparador atado al SPEC de M13

DECIDÍ        Dejar abierta la existencia de M05a en vez de cerrarla en un sentido u otro
DESCARTÉ      Darla por buena porque está en el ROADMAP; y descartarla por parecer redundante
POR QUÉ       Los dos ejemplos ya funcionan en M01 y M01 fue diseñado para admitirlos, pero eso es indicio, no respuesta. La respuesta está en el mostrador
REVERSIBLE    Sí — no se ha construido nada
```
