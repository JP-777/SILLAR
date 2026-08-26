# Decisiones previas de M07 Solicitudes B2B y Especiales

Lo que se decidió **antes de escribir una línea de código**, con su porqué. El SPEC dice qué se
construye; esto dice **por qué se construye así y qué se descartó por el camino**.

Existe separado del SPEC porque una parte de lo de aquí **no cabe en una especificación**: una
hipótesis refutada y una decisión revertida no son requisitos, son historia de cómo se llegó. Y
borrarla haría que dentro de tres meses alguien volviera a proponer lo mismo con los mismos
argumentos.

---

## 1. Dos tablas y no una — la hipótesis que refutó el negocio

**Lo que se propuso primero:** una sola tabla de solicitudes con un campo `request_type`. Es la
solución obvia: los dos casos comparten cliente, descripción, estado y notas, así que parecen el
mismo formulario con una etiqueta distinta.

**Lo que la refutó no fue el análisis, fue el negocio.** Los ejemplos reales no se parecen:

| | Personalización | Volumen |
|---|---|---|
| Qué cambia | **La especificación** | **El precio, por la cantidad** |
| Cantidad | Irrelevante; puede ser **una** | Es el dato central |
| ¿Existe en catálogo? | **Siempre** parte de algo que existe | **Puede no existir** |
| Ejemplo | «un arreglo del Día de la Madre pero con otro peluche» | «100 cordones para el desfile» |
| Quién es el destinatario | El que pide | **Puede ser otro**: un profesor pide por su colegio |

Con una sola tabla, `quantity` es obligatorio para la mitad de las filas y sin sentido para la otra;
`product_id` es obligatorio para una mitad e imposible para la otra; y `institution_name` y
`contact_person` quedan nulos en todas las de personalización. **Cuatro columnas que significan cosas
distintas según el valor de una quinta** — que es la forma que este proyecto ya ha desmontado tres
veces.

**También se descartó separar por persona natural / institución.** Suena natural y es el corte
equivocado: con ese criterio, **una persona pidiendo al por mayor no tendría sitio**, y ese caso es
real —alguien organiza una fiesta y pide 50 bolsas de dulces sin ser ningún colegio.

**El corte correcto no es quién pide, es qué se pide**, y quedó escrito como la regla 3 del SPEC para
que se pueda volver a aplicar:

> ¿lo que pide existe tal cual en la tienda? → No: personalización · Sí, en cantidad que cambia el
> precio: volumen · **Sí, en cantidad normal: no es una solicitud, es el carrito de M03**

---

## 2. La dependencia sobre M04 se cerró blanda y se revirtió a dura

**Se escribe como reversión explícita, no se arregla en silencio.**

La primera versión de estas decisiones cerró M04 como **blanda**, siguiendo lo que decía
`ARQUITECTURA_MODULAR.md:63`: *«M07 │ Solicitudes B2B y Especiales │ `b2b` │ M04 (blanda)»*. Era la
lectura correcta del documento, y el documento estaba mal.

**Se corrigió al saber que toda solicitud exige cuenta.** Y entonces es exactamente la misma frase
que ya había convertido en dura la de M03 el 21 de agosto de 2026 (`ARQUITECTURA_MODULAR.md:100`):

> «la cuenta obligatoria para comprar lo convierte en duro: **si hay que tener cuenta, el módulo que
> guarda al cliente tiene que estar**»

Con «comprar» cambiado por «solicitar», vale entera.

**Y M01 nunca estuvo declarada**, que es el error más llamativo de los dos: el formulario de
solicitud vive dentro de la ficha del producto, así que sin M01 no hay ni dónde pulsar. Estaba
ausente de la tabla desde que la tabla existe.

Las dos correcciones están aplicadas en el commit `581ff5e`, y la línea 63 hoy dice
**«M01 (dura), M04 (dura)»**.

**Lo que se lleva por delante:** al ser duras, las FK hacia `crm.customers` y `catalog.products` van
**dentro de la migración de M07**. `database/integrations/b2b_crm.sql` **sale del plan** — una
dependencia dura no se desmonta por separado, y un script de integración existe justamente para
poder desmontarla. Es el mismo movimiento que M03 hizo con `sales_crm.sql`.

---

## 3. La lista escolar sale de M07 hacia M18

`docs/BITACORA.md:781` la había aparcado aquí el 15 de agosto de 2026, dentro de la entrada del SPEC
de M01:

> «una lista de útiles por grado y colegio **no es una categoría** sino un conjunto con dueño y
> vigencia — va a M07»

**La primera mitad de esa frase sigue siendo cierta; la segunda no.** Aparcarla en M07 fue correcto
el día que se escribió —hacía falta un sitio que no fuera M01, y M07 era el único candidato—, pero
con el alcance de M07 ya escrito se ve que no cabe.

**Por qué no cabe.** M07 recibe una solicitud, la cotiza y cierra. La campaña escolar es una
**operación de temporada**: personal atendiendo listas durante semanas, ofertas aplicándose por
volumen o por colegio, y agentes de IA previstos para atender el pico. No es contenido publicado ni
es una bandeja de encargos: **es un modo de trabajo con su propia duración**.

Metida en M07, obligaría a que un módulo de cotizaciones supiera de campañas, vigencias y ofertas — y
el negocio que compra M07 para cotizar encargos no quiere nada de eso.

**M18 Campaña Escolar** entró al catálogo de módulos y a la Fase 2 en el mismo commit `581ff5e`.

---

## 4. El patrón de referencia más instantánea, por tercera vez

`b2b.special_order_leads` usa exactamente la misma forma que ya está en el árbol:

| Uso | Dónde | ¿Refresca o congela? | Estado |
|---|---|---|---|
| 1 | `cms.featured_products` — `ProductId` nulable + `ProductName` + `ProductSlug` (`backend/Sillar.Modules.Cms/Domain/FeaturedProduct.cs:5-7`) | **Refresca** | **Construido** |
| 2 | `sales.order_items` — el pedido conserva nombre y precio del momento (`CLAUDE.md:119`) | **Congela** | Especificado |
| 3 | `b2b.special_order_leads` | **Refresca** | Este SPEC |

**Tres usos son un patrón, no una coincidencia**, y ya no hace falta volver a razonarlo cada vez:
referencia viva para poder releer, copia para que la fila siga significando algo cuando el original
desaparezca, y una marca —`pending_relink`— para que alguien lo arregle a mano cuando toque.

### Pero son dos variedades, y elegir mal por parecido es el riesgo entero de la tabla

La forma es la misma; **lo que se hace con la copia cuando el original cambia, no**:

| | **Refresca** | **Congela** |
|---|---|---|
| Al cambiar el producto | Relee y sustituye la copia | **No vuelve a mirar** |
| Para qué existe la copia | Que la fila siga significando algo si el original desaparece | **Guardar lo que pasó** |
| Marca de reenlace | Sí — `pending_relink` | No: no hay nada que reenlazar |
| Ejemplo de por qué | Un destacado con el nombre viejo enseña al público algo que ya no existe | **Un pedido conserva a dónde se envió aunque el cliente se mude** |

`b2b.special_order_leads` **es de los que refrescan**, y por eso lleva `pending_relink`: la solicitud
sigue viva y el personal necesita el producto de hoy para cotizar. `b2b.quote_lines` **es de los que
congelan** —`catalog_price_at_quote` es el precio de aquel día y no se toca nunca—, lo que deja a M07
usando **las dos variedades a la vez**, cada una donde toca.

> **El cuarto caso tiene que elegir variedad antes que forma.** La pregunta no es «¿copio los
> campos?» sino **«¿esta fila describe algo que sigue pasando, o registra algo que ya pasó?»**. Lo
> primero refresca; lo segundo congela. Copiar la forma sin hacerse esa pregunta es cómo un registro
> histórico acaba reescribiéndose solo.

**Y M07 no tiene que escribir nada nuevo para consumirlo.** Lo que necesita ya es público en
`Sillar.Modules.Catalog.Contracts`:

- `ProductPickerItem` — el registro que devuelve buscar y releer un producto
  (`backend/Sillar.Modules.Catalog.Contracts/ProductPickerItem.cs`)
- `ProductoActualizado` y `ProductoDesactivado`
  (`backend/Sillar.Modules.Catalog.Contracts/Events/CatalogEvents.cs:40` y `:45`)

Se amplió el contrato de M01 para su primer consumidor, que fue M02. **M07 es el segundo, y lo usa
tal cual.** Esa es la prueba de que la ampliación se hizo al nivel correcto: si M07 necesitara
añadirle campos, es que se había diseñado para una pantalla y no para el módulo.

---

## 5. Lo que decidió el modelo de la cotización

Dos decisiones que no son obvias y que el SPEC recoge como reglas, con su razón aquí:

**Los dos precios de `quote_lines` no se pueden fundir.** `unit_price` es lo que se cobra —con
descuento mayorista— y `catalog_price_at_quote` es la referencia contra la que se detecta que el
catálogo se movió. Con un solo campo, **o no se puede descontar o no se puede caducar**: si se guarda
el precio cobrado, cualquier descuento se lee como «el catálogo cambió» y la cotización caduca al
nacer.

**El umbral mayorista es un importe y vive fuera de M07.** Veinte arreglos y veinte cordones son
**400 soles contra 80**: contar unidades trata igual dos cosas que el negocio trata distinto. Y vive
en `core.site_settings` porque tiene que poder cambiarse sin tocar código — un umbral comercial
dentro de una migración es un despliegue cada vez que el negocio cambia de idea.

---

## 6. Lo que se dejó fuera a propósito

**La API de WhatsApp.** Un trabajador manda la cotización desde su propio WhatsApp. No hay
integración, no hay credenciales que custodiar y no hay nada que se rompa el día que Meta cambie
algo. Es la decisión más barata de las de este documento y probablemente la que más disgustos ahorra.

**Los eventos publicados.** Ninguno en la 1.0.0. **Un contrato no se cierra, se estrena:** M07 no
tiene hoy consumidor que justifique publicar nada, y un evento «por si acaso» es una promesa que
alguien tendrá que mantener sin saber para quién.

**La caducidad por tiempo.** No existe. Solo se caduca por precio, y `invalidated_reason` dice cuál.
Una cotización que caduca a los treinta días obliga a explicarle al cliente por qué ya no vale algo
que no ha cambiado.

---

```
DECIDÍ        Dos tablas separadas por «qué se pide», no una con campo de tipo
DESCARTÉ      Tabla única con request_type; y separar por natural/institución
POR QUÉ       Lo refutó el negocio: con el corte por persona, alguien pidiendo al por mayor no tiene sitio
REVERSIBLE    No sin migración una vez haya datos — hoy sí, la ventana del esquema sigue abierta

DECIDÍ        M04 y M01 duras, con las FK dentro de la migración de M07
DESCARTÉ      M04 blanda con script de integración b2b_crm.sql
POR QUÉ       Toda solicitud exige cuenta; misma frase que ya movió a M03 el 21 ago
REVERSIBLE    Sí en el papel; no una vez exista la migración

DECIDÍ        Ninguna tabla replica: integer identity, sin origin_node ni row_version
DESCARTÉ      uuid v7 replicable
POR QUÉ       b2b no está en los módulos del ERP (CLAUDE.md:13) ni en los tres compartidos (ADR-017:36)
REVERSIBLE    No con datos dentro — es la migración cara que la ADR-016 existe para evitar

DECIDÍ        Mover la lista escolar de M07 a M18
DESCARTÉ      Mantenerla aquí, como la aparcó BITACORA.md:781
POR QUÉ       Es operación de temporada, no una bandeja de encargos; obligaría a M07 a saber de campañas
REVERSIBLE    Sí — es documentación, nada construido

DECIDÍ        Dos columnas de precio en quote_lines
DESCARTÉ      Una sola
POR QUÉ       Con una, o no se puede descontar o no se puede caducar
REVERSIBLE    Sí hoy; no tras la migración inicial
```
