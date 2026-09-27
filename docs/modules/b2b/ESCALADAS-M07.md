# Escaladas de M07 — cola abierta

Creado: 26/09/2026, America/Lima · Última modificación: 27/09/2026 · Última verificación: 27/09/2026 ·
Commit base comprobado: `e839989432283c755edf7d4ae47b2c37697215ec` (hasta el 26/09, `711bfba`).

> ### Estado al 27/09/2026 — encargo `B_M07_B2B.md`
>
> | | Estado |
> |---|---|
> | E1 | **Abierta** — pregunta 1 del encargo, abajo |
> | E2 | ~~Resuelta por JP: toda consulta de precio es autenticada~~ |
> | E3 | ~~Resuelta por JP: la línea se ata a la presentación, con snapshot~~ → queda **E3b** |
> | E4 | ~~Resuelta por JP: fotos privadas aplazadas~~ |
> | E5 | Resuelta **en parte** (ADR-016, excepción del 27/09). **Letra y unicidad entre nodos abiertas** — pregunta 3 |
> | E9 | **Abierta** — pregunta 1 |
> | E10 | **Nueva**: límite del `DROP SCHEMA … CASCADE` arbitrario — pregunta 2 |
> | C5 | ~~Cumplida: la barrera de fronteras está en `main`~~ |
> | C6 | **Auditada y protegida en los scripts soportados** (`C6-AUDITORIA-M07.md`); piden turno los archivos de M01/M04 (C7) |


Lo que frente B **no decide solo**, con las opciones vistas y lo que sigue mientras tanto. Cada
entrada dice si su respuesta se deshace editando un archivo o si toca datos, migraciones o claves
(`CLAUDE.md`, «Autonomía»). **Pedir no bloquea:** el resto del módulo sigue.

Numeración estable: una entrada resuelta se tacha y conserva su número.

---

## E1 — **(abierta, 27/09)** ¿Dónde vive la consulta de un producto «a consultar»? · **NO REVERSIBLE una vez migrado**

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

## ~~E2~~ — **Resuelta por JP el 27/09: sí, autenticada.** Antecedente: ¿La consulta «a consultar» exige sesión? · reversible en código, **no** si se abre anónima con datos

- **Lo que rige hoy:** «**Públicos — todos con sesión de cliente, ninguno anónimo**» (`SPEC.md`
  §6) y la regla 1. Se mantiene hasta que el líder disponga otra cosa.
- **Opciones** (las mismas que frente A en su §d, para que las dos escaladas se lean igual):
  1. La acción lleva a **entrar o registrarse** y vuelve a la consulta. Coherente con la SPEC.
  2. La acción lleva al **contacto sin cuenta de M04**. Contradice que el flujo sea de M07.
  3. **Endpoint anónimo** con limitación por IP. Exige `customer_id` nulable —cambio de esquema—,
     infraestructura contra abuso que la SPEC dice no necesitar, y reabre por qué M04 es dura.
- **Recomendación:** la 1.
- **Alineación con A:** mismo texto de opciones; la frontera se envía a Chat 2 (C3).

## ~~E3~~ — **Resuelta por JP el 27/09: presentación.** Antecedente: ¿Una línea de cotización se ata al producto o a la presentación? · **NO REVERSIBLE una vez migrado**

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

## ~~E4~~ — **Resuelta por JP el 27/09: aplazada.** Antecedente: La foto de referencia del cliente sería pública · reversible hoy

- **Hecho:** `IMediaStorage` solo sabe devolver rutas públicas bajo `/media/`
  (`backend/Sillar.Core.Contracts/IMediaStorage.cs:5-8`). No hay medios privados.
- **Opciones:** (1) **sacar la foto de la 1.0.0** y quitar `reference_image_id`; (2) aceptarla
  pública —un `uuid` v7 no es un secreto: lleva la hora dentro—; (3) pedir a CORE medios privados,
  que es costura grande.
- **Recomendación:** la 1. Se describe con palabras, y la foto llega por WhatsApp como hoy.

## E5 — **(resuelta en parte, 27/09)** Formato de `quote_number`

- **Hecho:** la SPEC pide «legible, con serie de nodo delante» (`SPEC.md` §4) sin formato. Frente
  A tiene su propio conflicto con la ADR-016 (su escalada §b1).
- **Por qué importa:** un número dictado por teléfono no se reformatea. **No bloquea** hasta la
  migración de `quotes`, pero se pide antes de escribirla, y conviene que A y B no respondan
  distinto a la misma regla.

## E9 — **(abierta, 27/09)** ¿Desde dónde se abre una solicitud de volumen? · **puede tocar el esquema**

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
- ~~**C5**~~ **(cumplida el 27/09)** — **La barrera de fronteras en `main`.** El paso 2 espera a ver en `main`
  `frontend/scripts/fronteras-frontend.mjs`, `frontend/tests/fronterasFrontend.test.mjs` y su
  llamada en la etapa 1 de `scripts/verificar.mjs`. En `711bfba` no existe ninguna de las tres.
- **C6 — Riesgo futuro, no reproducido: desinstalar a mano M01 o M04 dejaría a M07 sin sus FK, y podrían no volver al reinstalar.**
  M07 no está instalado en `main`; la consecuencia es deducida. Solo desinstalación manual, no la desactivación administrativa.
  `database/modules/catalog/99_drop.sql:61` y `database/modules/crm/99_drop.sql:24` usan `CASCADE`;
  el aviso de catálogo no nombra a `b2b` (`catalog/99_drop.sql:43`), y la migración de M07 no se
  repite al reinstalar la dependencia. `e2e/tests/zz-instalacion.spec.ts:114` hace justo esa
  desinstalación. **Efecto pedido:** que el aviso nombre a `b2b` y que ninguna prueba posterior
  corra con M07 sin sus FK. Detalle y pregunta de plataforma en `PLAN-DE-PRUEBAS-M07.md` §5.

---

## Añadido el 27/09/2026

### E3b — ¿Caduca una cotización `enviada` si una presentación «a consultar» pasa a tener precio? · reversible en código

- **Contexto:** con E3 resuelta, una línea con `item_id` y `catalog_price_at_quote` nulo es una
  presentación «a consultar» cuyo precio puso el personal (`SPEC.md` §4, enmienda 27/09).
- **Opciones:** (1) no caduca —no había precio de catálogo contra el que comparar—; (2) caduca,
  porque el catálogo «se movió».
- **Recomendación:** la 1. Es lógica del manejador de eventos, no esquema: **no bloquea nada**
  hasta el paso 3.

### E10 — El `DROP SCHEMA … CASCADE` arbitrario elude cualquier guarda · afecta a toda la plataforma

- **Hecho reproducido:** `evidencias/C6-SQL-DESPUES-20260927.txt`, sección «LÍMITE».
- **Opciones:** (1) aceptarlo como límite documentado —los scripts soportados sí rechazan—;
  (2) un `EVENT TRIGGER … ON sql_drop` de CORE que aborte si se pierde una FK de dependencia dura.
  Exige superusuario, es de CORE y cambia la política de desmontaje de toda la plataforma.
- **Recomendación:** la 1 ahora; la 2 solo si JP la quiere, y por Integración.

### Preguntas del encargo, sin decisiones inventadas

1. **Presupuestos sin producto de catálogo y presentaciones sin precio publicado.**
   - **Lo que ya está resuelto y construido:** en una cotización, una línea **libre** (`item_id`
     nulo) y una línea de **presentación «a consultar»** (`item_id` presente,
     `catalog_price_at_quote` nulo) caben y se distinguen sin ambigüedad; en las dos, el precio
     cobrado lo pone el personal. **Nada de esto toca carrito ni pago de M03.**
   - **Lo que sigue abierto:** *de dónde nace la petición del cliente* en los dos casos. Para «a
     consultar», E1 (tabla propia recomendada, sin ser origen de cotización en la 1.0.0). Para
     volumen sin producto, E9 (dónde está el formulario). Las dos pueden añadirse después con una
     migración aditiva (columna o tabla nuevas), así que no bloquean la base de datos de hoy.
2. **SQL directo con M07 instalado.** Hoy los `99_drop.sql` soportados de M01 y M04 **rechazan**,
   con y sin `ON_ERROR_STOP`, y no borran nada. Límites: E10 y los otros tres de
   `C6-AUDITORIA-M07.md` §5.
3. **Letra de serie de las cotizaciones y unicidad entre nodos.** **No está decidida.** La
   excepción de la ADR-016 fija la convención —nodo delante, año, correlativo, reinicio anual, sin
   huecos por rollback, contador por fila de serie— pero deja la letra de M07 «todavía por decidir»
   (`docs/adr/ADR-016-identificadores-replicables.md:126-127`). Registrado para JP y el líder
   técnico. **Bloquea solo la creación de cotizaciones** (paso 3); la tabla ya tiene
   `quote_number` único.

### Peticiones de costura nuevas (a Integración, por archivo y por efecto)

- **C7 — Guardas en scripts de M01 y M04.** `database/modules/catalog/99_drop.sql` y
  `database/modules/crm/99_drop.sql` llevan en esta rama la guarda de C6. Son de módulos cerrados:
  **se pide el turno de esos dos archivos**. Efecto: desinstalar M01/M04 con un dependiente duro
  instalado se rechaza sin borrar nada.
- **C8 — Proyectos de M07 en `backend/Sillar.sln`.** Añadidos en esta rama para que la puerta
  compile y pruebe M07 (etapas 3 y 5). Se pide el turno de la `.sln`.
- **C9 — Desplegar M07.** Que `Sillar.Api` referencie `Sillar.Modules.B2B` y que la etapa 4 de
  `scripts/verificar.mjs` y `e2e/setup/migrate.ts` apliquen sus migraciones. **No está en esta
  rama.** Efecto que hay que acompañar en el mismo cambio: el e2e instala por `/api/setup`, que
  migra todos los módulos declarados, así que `e2e/tests/zz-instalacion.spec.ts:114` —que
  desinstala el catálogo— **chocará con la guarda C7** y tendrá que desinstalar M07 antes y
  reinstalarlo después. Es el comportamiento correcto, no un fallo.
- **C10 — Residuo de una instalación rechazada.** Si Integración quiere que un módulo rechazado no
  deje su schema vacío, el cambio es en `Sillar.Shared.Data` (crear el historial dentro de la
  transacción, o limpiarlo al fallar). Hoy es un límite documentado, no un fallo.
- **C11 — La puerta acumula nodos de MSBuild.** Observado el 27/09 en la puerta de M07: más de
  veinte nodos reutilizables vivos (~4 GiB) durante el e2e, memoria disponible mínima 1218 MiB y un
  e2e casi el doble de lento. Efecto pedido: que `scripts/verificar.mjs` no deje nodos de MSBuild
  vivos entre etapas. Detalle en `BITACORA-M07.md`.
