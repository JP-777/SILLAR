# Escaladas de M07 — cola abierta

Creado: 26/09/2026, America/Lima · Última verificación: 26/09/2026 ·
Commit base comprobado: `711bfba7cf3be80baa146b44e79ddf7a633d695d`.

Lo que frente B **no decide solo**, con las opciones vistas y lo que sigue mientras tanto. Cada
entrada dice si su respuesta se deshace editando un archivo o si toca datos, migraciones o claves
(`CLAUDE.md`, «Autonomía»). **Pedir no bloquea:** el resto del módulo sigue.

Numeración estable: una entrada resuelta se tacha y conserva su número.

---

## E1 — ¿Dónde vive la consulta de un producto «a consultar»? · **NO REVERSIBLE una vez migrado**

- **Punto de decisión.** JP ratificó el 26/09 que la acción de un producto con precio nulo
  «conduce a una solicitud de información gestionada por M07». La SPEC tiene dos formas de
  solicitud y la pregunta que las separa (`SPEC.md` §8, regla 3). **Ninguna encaja sin forzarla.**
- **Opciones:**
  1. **Meterla en `special_order_leads`.** Tiene `product_id` y la cantidad es opcional, así que
     cabe físicamente. Pero esa tabla significa «cambia la especificación» (`SPEC.md` §4); llenarla
     de «¿cuánto cuesta?» la vuelve un cajón de sastre y es la forma que `DECISIONES-PREVIAS-M07.md`
     §1 ya desmontó: una columna que significa cosas distintas según otra.
  2. **Meterla en `institution_requests`.** No cabe: `quantity` es obligatoria y `> 0`, e
     `institution_name` no admite nulo (`SPEC.md` §4). Habría que relajar las dos.
  3. **Tercera modalidad: una tabla propia** (producto de origen con instantánea que refresca,
     mensaje, cantidad opcional, estado). Es **cambio de datos y categoría semántica nueva**, y
     añade un cuarto renglón a la regla 3.
  4. **No registrarla en M07** y mandar al contacto de M04. Contradice la decisión del 26/09.
- **La consecuencia que no se ve a primera vista.** Si una consulta puede acabar en cotización,
  es **el tercer origen cotizable**, y ése es exactamente el disparador de `PENDIENTES.md` §6
  (`b2b.quotes` con dos columnas excluyentes y `ck_quotes_origen`). Entonces hay que decidir la
  referencia polimórfica **antes** de la primera migración, no después.
- **Recomendación:** la 3, y que **en la 1.0.0 la consulta no sea origen de cotización**: el
  personal responde el precio y, si el cliente quiere cantidad o cambios, abre una de las otras
  dos. Así no se reabre §6 sin necesidad.
- **Pregunta a JP, una sola:** ¿una consulta de precio puede convertirse directamente en
  cotización, sí o no?
- **Mientras tanto:** se construye todo lo demás; no se escribe esa tabla ni su pantalla (T5).

## E2 — ¿La consulta «a consultar» exige sesión? · reversible en código, **no** si se abre anónima con datos

- **Lo que rige hoy:** «**Públicos — todos con sesión de cliente, ninguno anónimo**» (`SPEC.md`
  §6) y la regla 1. Se mantiene hasta que el líder disponga otra cosa.
- **Opciones** (las mismas que frente A en su §d, para que las dos escaladas se lean igual):
  1. La acción lleva a **entrar o registrarse** y vuelve a la consulta. Coherente con la SPEC.
  2. La acción lleva al **contacto sin cuenta de M04**. Contradice que el flujo sea de M07.
  3. **Endpoint anónimo** con limitación por IP. Exige `customer_id` nulable —cambio de esquema—,
     infraestructura contra abuso que la SPEC dice no necesitar, y reabre por qué M04 es dura.
- **Recomendación:** la 1.
- **Alineación con A:** mismo texto de opciones; la frontera se envía a Chat 2 (C3).

## E3 — ¿Una línea de cotización se ata al producto o a la presentación? · **NO REVERSIBLE una vez migrado**

- **Lo que dice la SPEC:** `quote_lines.product_id → catalog.products` y «precio de lista».
- **Lo que dice M01:** el precio es de la presentación, `price_override ?? list_price`
  (`docs/modules/catalog/SPEC.md:400`); `catalog.product_items` es lo que se cobra (`:210`); el
  contrato por presentación es `ItemSnapshot.Price`
  (`backend/Sillar.Modules.Catalog.Contracts/ItemSnapshot.cs:22-34`).
- **Opciones:**
  1. **`item_id → catalog.product_items`**, con `catalog_price_at_quote` = `ItemSnapshot.Price`.
     La caducidad escucha `ProductoActualizado` —que M01 emite también al cambiar presentaciones
     (`CatalogEvents.cs:13-16`)— y relee con `VariantesDeAsync`.
  2. **Seguir en el producto** comparando `ProductPickerItem.Price`. Es precio de tarjeta, una cota
     «Desde» (`ProductPickerItem.cs:181-186`): cotizar la presentación cara dejaría la línea
     «cambiada» para siempre, y un cambio en otra presentación podría no verse.
- **Subpregunta, y es la de «a consultar»:** con la opción 1, una línea de un producto «a
  consultar» tiene `item_id` y **precio de catálogo nulo**. La regla 8 decía «nulo = no viene del
  catálogo»; ahora eso lo dice `item_id` nulo. **¿Si el catálogo publica después un precio para esa
  presentación, la cotización `enviada` caduca?** Recomendación: **no** — no había precio contra el
  que comparar, y lo cotizado fue criterio del personal.
- **Recomendación:** la 1, con esa subregla.
- **Mientras tanto:** las líneas libres (sin catálogo) no dependen de esto.

## E4 — La foto de referencia del cliente sería pública · reversible hoy

- **Hecho:** `IMediaStorage` solo sabe devolver rutas públicas bajo `/media/`
  (`backend/Sillar.Core.Contracts/IMediaStorage.cs:5-8`). No hay medios privados.
- **Opciones:** (1) **sacar la foto de la 1.0.0** y quitar `reference_image_id`; (2) aceptarla
  pública —un `uuid` v7 no es un secreto: lleva la hora dentro—; (3) pedir a CORE medios privados,
  que es costura grande.
- **Recomendación:** la 1. Se describe con palabras, y la foto llega por WhatsApp como hoy.

## E5 — Formato de `quote_number`

- **Hecho:** la SPEC pide «legible, con serie de nodo delante» (`SPEC.md` §4) sin formato. Frente
  A tiene su propio conflicto con la ADR-016 (su escalada §b1).
- **Por qué importa:** un número dictado por teléfono no se reformatea. **No bloquea** hasta la
  migración de `quotes`, pero se pide antes de escribirla, y conviene que A y B no respondan
  distinto a la misma regla.

## E9 — ¿Desde dónde se abre una solicitud de volumen? · **puede tocar el esquema**

- **Contradicción interna:** «Rutas públicas: ninguna» porque el formulario se abre desde la ficha
  (`SPEC.md` §7); pero una solicitud de volumen «puede no existir en el catálogo» (§4), y entonces
  no hay ficha.
- **Opciones:** (1) solo desde la ficha, y lo que no está en catálogo va por WhatsApp; (2) una
  ruta pública propia de M07; (3) desde el área de cuenta de M04, por superficie.
- **Consecuencia sobre datos:** con la 1, `institution_requests` necesitaría un `product_id`
  opcional que hoy no tiene. Por eso no se decide solo.

---

## Peticiones de costura a Chat 2 (efecto observable, no cambio imaginado)

- **C1 — Acción de M07 en la ficha de producto.** Que un módulo activo pueda aportar una acción a
  la ficha pública de M01 **sin que ninguno importe al otro**; que con M07 inactivo la acción no se
  pinte **y no quede hueco**. Hoy no hay superficie en la ficha (`frontend/src/platform/surfaceRegistry.tsx`
  se usa para portada y pie). Es la misma necesidad que frente A describió en su §d: podéis
  resolverlas con un solo mecanismo.
- **C2 — Montaje de M07.** Que `b2bNavigation` y las rutas de administración de M07 se monten
  como las de M02 (`frontend/src/layout/navigation.ts:48-52`, `frontend/src/app/routes.tsx`).
  Se pedirá con el nombre real cuando exista el código.
- **C3 — Texto de frontera idéntico en los documentos compartidos.** El texto obligatorio y el
  contrato permitido van en el informe de este turno para que Chat 2 lo lleve a A.
- **C4 — Dos discrepancias en `ARQUITECTURA_MODULAR.md`.** `:198` pone `quotes` en «fase 2» y la
  SPEC la trae en la 1.0.0; y la lista de FK de `:219-220` solo tiene las dos de `crm.customers`,
  sin las de `catalog` ni `core.media_assets` que la SPEC declara (`SPEC.md` §4, relaciones
  cruzadas). Según `CLAUDE.md`, gana la SPEC y se avisa. Pido que se alinee **después** de E3 y E4,
  que pueden cambiar esa lista.
- **C5 — La barrera de fronteras en `main`.** El paso 2 espera a ver en `main`
  `frontend/scripts/fronteras-frontend.mjs`, `frontend/tests/fronterasFrontend.test.mjs` y su
  llamada en la etapa 1 de `scripts/verificar.mjs`. En `711bfba` no existe ninguna de las tres.
