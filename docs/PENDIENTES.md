# Pendientes

Lo que está decidido pero no hecho, y lo que está aplazado a propósito.

> **Por qué existe este archivo.** Se perdió trabajo una vez por tener esto solo en una
> conversación: al retomar M03 nadie recordaba que el SPEC de M04 ya estaba escrito y que había
> una corrección acordada sin aplicar. Una lista no evita el olvido; **evita redescubrirlo
> construyendo**, que es cuando cuesta caro.
>
> **Cada entrada lleva su disparador.** Un pendiente sin disparador es un deseo: nunca hay un día
> en que toque. Con disparador, alguien lo encuentra el día correcto.
>
> **Al resolver una entrada, se borra de aquí y se registra donde corresponda** —bitácora del
> módulo o `BITACORA.md`—. Este archivo no es un histórico: es lo que falta.

---

## 1 · `catalogHome` promete un catálogo que puede estar vacío

**Qué pasa.** Con M01 activo y **cero productos públicos**, la portada dice «Nuestra tienda — Mira
todo lo que tenemos publicado» y enlaza al catálogo. El visitante llega a una lista vacía.
`CatalogHomeSection` no consulta datos: devuelve un `EmptyState` fijo.

**Dónde.** `frontend/src/modules/catalog/routes.tsx:51-67`, y su declaración de estado en el
registro de la portada (`:58`, hoy `'con-contenido'` fijo).

**Por qué está aplazado.** Es cambio de comportamiento de un módulo cerrado, y se descubrió
mientras se cerraba M02. Meterlo ahí habría ensanchado un diff que tenía que quedarse estrecho.

**Disparador.** Unidad propia, en cuanto M02 quede cerrado formalmente.

**Nota que conviene no perder.** Que `catalogHome` pinte siempre es **lo que tapaba** el defecto
de la portada: mientras hubo un solo módulo publicable y ese módulo pintaba pasara lo que pasara,
el caso «activo y sin publicar» era inalcanzable. M02 no lo creó, lo hizo alcanzable.

---

## 2 · El footer público de plataforma no existe

**Qué pasa.** La costura `PublicFooterContribution` / `FOOTER_CONTRIBUTIONS` quedó **aprobada** en
la auditoría de M02, con el mismo patrón que `HOME_SECTIONS`. Pero no hay ningún `<footer>` de
página en `frontend/src/` — ni vacío. El único `<footer>` del árbol es el del cajón
(`shared/ui/patterns.tsx:81`), que es otra cosa. M02 tiene la API pública de Social Links y el
servicio listos (`modules/cms/services/socialLinks.ts`) y **sin montar**.

**Ojo con el tamaño.** No es «conectar Social Links a un footer»: es **construir el footer de
plataforma por primera vez**, con Social Links como su primer contribuyente.

**Y por qué nadie lo va a diseñar solo.** `PROTOCOLO-DISENO.md` §3 encarga pantallas a partir del
§9 del SPEC de un módulo. El footer no está en el §9 de ninguno, porque es de la plataforma. Con
el protocolo actual, **no le toca a nadie**.

**Disparador.** Cuando se decida montar Social Links en público. Avisar al equipo de diseño en ese
momento, no antes (§6: consume del mismo cupo).

---

## 3 · Tres copias de `StampReplicationColumns`

**Qué pasa.** El bucle que sella `origin_node` y `row_version` está duplicado:

    backend/Sillar.Core/Data/CoreDbContext.cs:133
    backend/Sillar.Modules.Catalog/Data/CatalogDbContext.cs:92

y CRM añade la tercera. `IReplicatedEntity` y `NodeIdentity` **ya viven en `Sillar.Shared`**; lo
que no está compartido es el sellado y el `MapReplication` de EF.

**Por qué está aplazado.** Extraerlo toca `Sillar.Shared` + CORE + Catalog a la vez: costura
compartida y regresión sobre dos módulos cerrados. No cabe dentro del Paso 2 de M04.

**Disparador.** La **cuarta** copia, **o** la primera vez que dos copias discrepen. Lo que ocurra
antes. La unidad que lo extraiga debe llevarse el sellado **y** revisar `MapReplication`; a medias
no.

**Contexto.** `Sillar.Shared/Replication/NodeIdentity.cs` ya anticipa esto en su docstring: vive
ahí «porque todo módulo con tablas replicadas escribe esta columna igual: catálogo, clientes,
existencias y ventas».

---

## 4 · M05a Servicios — puede no llegar a existir

**Qué pasa.** Los dos ejemplos que la arquitectura da de M05a —anillado e impresión— **ya
funcionan como productos de M01** (`scripts/demo/datos.mjs:238` y `:248`), con precio nulo → «A
consultar» y unidad libre. Y M01 fue diseñado para admitirlos: `Product.cs:71-72` dice que
`SaleUnit` es texto libre porque «una lista cerrada dejaría fuera a los restaurantes **y a los
servicios**».

**Qué falta para decidirlo.** Observación de mostrador, no análisis:
`GUIA-OBSERVACION-MOSTRADOR.md:63` — *«Servicios: fotocopias, anillado, impresiones. ¿Se cobran
igual que un producto? ¿Cómo se cuenta la cantidad?»*

**Disparador.** La visita al mostrador. Con esas respuestas se decide si M05a se construye, si
basta una extensión de M01, o si el requisito del PRD se resuelve en presentación.

**Detalle.** `docs/modules/services/DECISIONES-PREVIAS-M05a.md` — las dos decisiones de
arquitectura ya están cerradas ahí; solo falta ésta.

---

## 5 · M18 Campaña Escolar — sin SPEC

**Qué pasa.** Salió de M07 —donde `BITACORA.md:781` la había aparcado el 15 de agosto— porque no
es contenido publicado: es una **operación de temporada** con personal atendiendo listas, ofertas
aplicándose y agentes de IA previstos.

**Disparador.** Después de M03, en Fase 2. Antes hay que resolver una pregunta que decide el
modelo entero: **¿las líneas de una lista escolar son productos concretos del catálogo, o texto
genérico?** («1 cuaderno cuadriculado A4 de 100 hojas» no es un producto). Si son genéricas, el
patrón de instantáneas y reenlace de `featured_products` **no aplica**.

---

## 6 · `b2b.quotes` — la referencia polimórfica de fase 2

**Qué pasa.** Una cotización nace de `special_order_leads` **o** de `institution_requests`. Hoy se
resuelve con dos columnas nulables excluyentes y un `CHECK`. Funciona.

**Qué queda abierto.** Si aparecen más orígenes cotizables, dos columnas no escalan. **No se
decide ahora**: sin un tercer caso real sería inventar.

**Disparador.** El tercer origen cotizable, o el SPEC de `quotes` de fase 2.

**Detalle.** `docs/modules/b2b/SPEC.md` §4.3 y §10.

---

## 7 · ¿Está M11 Pagos en la fase correcta?

**Qué pasa.** `ARQUITECTURA_MODULAR.md` pone M11 en Fase 4 / «Futuro». Pero el negocio va a cobrar
**con tarjeta desde la web**, además de Yape con registro manual.

**Por qué importa y a quién.** No afecta a M07 —que solo registra el hecho del pago— sino a
**M03 Ventas Online**, que es MVP y es lo siguiente después de M04. Si el checkout de M03 tiene que
aceptar tarjeta desde el arranque, M11 no es futuro.

**Disparador.** Antes de escribir el SPEC de M03. No después.

---

## 8 · La etapa e2e produce falsos hallazgos por ruido de máquina

**Qué pasa.** Dos veces en una semana, la suite e2e ha fallado por el entorno y no por el código:

    entrega de M02   ERR_NETWORK_CHANGED, clasificado «TRANSITORIO OBSERVADO»,
                     sin causa raíz atribuida
    cierre de M02    catalogo.spec.ts:209 agotó 90 s esperando la API tras
                     reiniciar — había otro stack de Docker entero levantándose
                     en la misma máquina. En corrida limpia: 15,6 s

Dos no es coincidencia. Y el coste no es el tiempo perdido: es que **las dos veces alguien estuvo
a punto de perseguirlo como defecto**, y una tercera puede acabar «arreglando» código sano.

**Qué haría falta.** Que un fallo por entorno se distinga de uno de código **sin depender de que
alguien lea el log con criterio**: comprobar recursos antes de arrancar la suite, o que el arnés
distinga un timeout de arranque de un fallo de aserción y lo diga por su nombre.

**Disparador.** La tercera vez, o antes si alguien tiene una tarde. **Y mientras tanto, la regla
que ya funcionó dos veces:** ante un fallo masivo y raro en la etapa e2e, **medir la máquina antes
de sospechar del código**.

---

## Resueltos recientemente

*(se borran de arriba y se anotan aquí solo hasta que entren en la bitácora del módulo)*

- **La puerta no era reproducible.** `verificar.mjs` corría las pruebas del backend (`:126`) antes
  de la etapa e2e (`:157`), y dos pruebas de `ReactivacionRedSocialTests.cs:162` exigen la base
  `sillar_e2e`, que solo existe en la etapa 5. No podían correr nunca. El *394/394 con 0 omitidas*
  de la entrega de M02 salió de una corrida con la conexión apuntada a mano, no de la puerta.
  → En construcción: base efímera propia `sillar_verify_*` para la etapa 4.
