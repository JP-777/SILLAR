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

**Qué falta para decidirlo.** Observación de mostrador, no análisis. La pregunta que lo bloquea es
`GUIA-OBSERVACION-MOSTRADOR.md:70`, añadida el 25 de agosto de 2026 bajo «El precio» precisamente
porque faltaba:

> *«El precio de un servicio —anillado, impresiones—. ¿Sale de una lista fija o de la cabeza de quien
> atiende? ¿De qué depende: hojas, tamaño, color, tapa? ¿Hay tramos por cantidad o es lineal?»*

La de `:63` —«¿se cobran igual que un producto? ¿cómo se cuenta la cantidad?»— sigue en pie y es
complementaria: aquélla pregunta por el mostrador, ésta por el precio.

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

**Detalle.** `docs/modules/b2b/SPEC.md`, tabla `b2b.quotes` del §4 —donde vive `ck_quotes_origen`—
y la última fila del §10.

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

**Ver también.** La §6 de `BITACORA.md`, «Verificación manual pendiente», **no está en esta lista y
no debe estarlo**: no es trabajo aplazado sino unos cinco minutos de juicio humano que ninguna prueba
puede dar, y su sitio es `VERIFICACION-VISUAL-CORE.md`.

**Disparador.** La tercera vez, o antes si alguien tiene una tarde. **Y mientras tanto, la regla
que ya funcionó dos veces:** ante un fallo masivo y raro en la etapa e2e, **medir la máquina antes
de sospechar del código**.

---


---

## 9 · Cerrado sin resolver: cómo llegó `cms` a la base del MVP (21 ago 2026)

El 21 de agosto apareció una fila del módulo `cms` en `core.modules` de `sillar_dev`, la base de
la demostración. La escribió el sincronizador de módulos de un binario que declara `cms`, o sea
el de M02, apuntando a la base equivocada.

**Estado: mecanismo probado, causa sin establecer. Contenido pero no resuelto.**

- **Contenido:** la fila se borró con el host parado, y el arranque volvió limpio. No hacía daño
  —la pantalla de módulos itera sobre el binario, no sobre las filas (`ModuleActivationService.cs:88`),
  así que nunca hubo tarjeta rota— pero dejaba un aviso permanente en cada arranque, y un aviso
  permanente sobre algo que no se va a arreglar entrena a la gente a ignorar los avisos.
- **Mecanismo probado:** una variable `ConnectionStrings__Default` heredada del entorno gana
  sobre el `.env`, en silencio. M02 lo demostró. **Pero demostrar que un mecanismo puede producir
  el efecto no demuestra que produjera éste.**

**Por qué cada vía está cerrada** — que no es lo mismo que no haber dado nada:

| Vía | Por qué se cierra |
|---|---|
| ¿Lo escribió nuestro binario? | **No pudo.** `main` nunca declaró `cms`: `git log -S "cms"` da tres commits y ninguno añade un módulo —documentación, un ejemplo en Markdown y un `WHERE nspname IN (…)`—, y hoy no hay ningún `"cms"` en C# |
| ¿Cayó `DotEnv` al directorio de trabajo? | **No.** El `.env` de M02 existía veintitantas horas antes de la fila, así que la primera búsqueda —la del árbol del binario— encontraba el suyo |
| Los registros de PostgreSQL | **Cubrían la ventana y no contenían nada.** `log_connections`, `log_disconnections` y `log_statement` estaban en `off`/`none`: una conexión que escribe una fila sin error no dejaba rastro. **No se perdió la prueba: no se tomó.** Corregido para la próxima |
| El historial de PowerShell | **No puede verlo.** PSReadLine solo graba consolas interactivas, y los dos agentes lanzan `powershell.exe -NonInteractive`. Cero coincidencias de `ConnectionStrings`, y la última escritura del archivo es de once horas antes del incidente pese a decenas de comandos por medio |
| El entorno de M02 hoy | Sin ninguna variable así definida, ni de usuario ni de máquina. La consola de aquel día ya no existe |

Lo que queda hecho: el registro de conexiones encendido en desarrollo
(`docker-compose.yml:22-25`) y el arranque diciendo de qué `.env` cargó, a qué base apunta y qué
claves le ganó el entorno. **La próxima vez habrá rastro.**

Y una propuesta sin hacer: que cada árbol ponga su propio `Application Name` en la cadena de
conexión. Desde el anfitrión todas las conexiones llegan con la misma IP de pasarela
—`172.18.0.1`—, así que el origen no distingue procesos; el nombre de aplicación sí lo haría, y
sale gratis.

**Disparador.** La próxima fila de módulo que aparezca sin que nadie la escriba. **Esa vez sí habrá
rastro**: el registro de conexiones quedó encendido en desarrollo (`docker-compose.yml:22-25`) y el
arranque dice de qué `.env` cargó, a qué base apunta y qué claves le ganó el entorno.

---

## 10 · Defecto abierto: la auditoría enseña identificadores (18 ago 2026)

`AuditPage.tsx:71` pinta `entry.entityId` en crudo, y desde la ADR-018 los medios —y también
las sesiones— llevan `uuid`. La columna «Entidad» acaba mostrando
`01a016da-5b2e-722b-…` a la vista, contra la regla de `CLAUDE.md` de que **los identificadores
nunca se muestran al usuario**. No es raro: **cada acceso** deja una entrada con el `uuid` de la
sesión, así que la pantalla está llena.

Está codificado como defecto conocido en `e2e/tests/transversal.spec.ts:131-132`, con `test.fail`:
no cuesta un rojo permanente, y **si alguien lo arregla la prueba empieza a fallar** y obliga a
venir a borrar la marca. Se prefirió eso a exentar la pantalla del recorrido, que lo habría
escondido.

**Lo que falta es la decisión de producto, no el arreglo:** la auditoría necesita identificar
la fila exacta y a la vez no puede enseñar el identificador. Las salidas plausibles son un
código corto derivado, mostrarlo solo al desplegar el detalle, o aceptar que la auditoría es
una pantalla forense y documentar la excepción. Ninguna es obvia y las tres son baratas.

**Y el cruce que faltaba en todas partes, descubierto el 25 de agosto de 2026 leyendo mal la salida
de la suite.** `transversal.spec.ts:132` es

```ts
test.fail(true, 'Defecto abierto: AuditPage.tsx:71 pinta entityId en crudo');
```

y **es el único `test.fail` de toda la suite** — comprobado sobre `e2e/tests/` entero, donde tampoco
hay ningún `.skip`, `.only` ni `.fixme`.

El reporter de Playwright lo marca con **`x`**, igual que un fallo de verdad, pero **lo cuenta como
pasada**. Así que una corrida puede decir «0 failed» con dos `x` a la vista y estar bien. Eso explica
por qué un «todo en verde» podía convivir con un defecto abierto, y **cuesta media hora de
desconcierto cada vez que alguien lo ve por primera vez** — que es exactamente lo que pasó ese día.


**Disparador.** **No hay uno para decidir**, y conviene decirlo en vez de inventarlo: lo que falta es
una decisión de producto que nada fuerza. Lo que sí existe es **el disparador al revés** — el día que
alguien lo arregle, `transversal.spec.ts:132` **empieza a fallar** y le obliga a venir aquí a borrar
la marca. Es la razón por la que se prefirió `test.fail` a exentar la pantalla del recorrido.

---

## 11 · Heredado al fusionar los chats (18 ago 2026)

Seis cosas quedaron a medias cuando se retiró el chat de Frontend. **Ninguna estaba escrita en
ningún sitio** —por la regla de un solo escritor, Frontend no tocaba esta bitácora— así que
esto es lo único que las sostiene.

| Pendiente | Qué falta exactamente |
|---|---|
| **Resincronizar el sistema de diseño** | Ya está en la tabla de arriba. **Es el que bloquea el paso 4**: los tokens cambiaron *después* de que Claude Design produjera las pantallas de M01 |
| **`.design-sync/config.json` no incluye a `ModuleCard`** | 16 de 17 vistas previas listas. El componente vive en `modules/core/components` y la config solo apunta a `src/shared/ui`. **Lo que se extiende es la config, no el árbol de archivos**: mover el componente sería la abstracción por si acaso que prohíbe `CLAUDE.md`, y no hay segundo caso real |
| **El selector de categorías N:M con principal no existe** | Y no es problema de código: **nadie ha dibujado** cómo se ve elegir varias categorías y marcar una como principal. Bloquea parte del paso 4 de M01 y pide pasar antes por el paso 3.5 |
| **`BUILD_CONFIGURATION=Debug`: ¿es alcanzable en la imagen de producción?** | La regla de proceso ya se fijó —toda afirmación sobre código cita archivo y línea—; **la pregunta de hecho sigue abierta**. Se responde citando, no de memoria, que es justo como se falló la primera vez |
| **Falta `E2E_KEEP_STACK`** | Cuando la suite de `e2e/` falla, el stack se desmonta y hay que reproducir el fallo desde cero para mirarlo. Una variable que conserve la base levantada al fallar |
| **`:focus-visible` en diálogo, con clic de ratón** | Comprobar en un navegador de verdad si el anillo nativo se pinta cuando el foco cae en un elemento **distinto** del que se clicó. De las que no se resuelven leyendo |

**Bibliotecas evaluadas y descartadas** (18 ago, informes en `SILLAR-DISENO/investigacion/`, carpeta hermana de ésta — **fuera del repositorio**, ver `PROTOCOLO-DISENO.md` §7). Se anotan aquí para no volver a investigarlas sin tener que abrir esa carpeta:

| | Por qué no |
|---|---|
| **Morphicons** | Ignora `prefers-reduced-motion` por defecto: hay que corregirlo en cada uso, y un día se olvida. El efecto se reproduce en unas líneas |
| **Sileo** | Colores propios, **la animación retrasa la acción**, y **el artefacto publicado no lleva el texto de la licencia**. Lo último basta solo: no se vende software con una dependencia cuya licencia no se puede señalar |
| **Auragradients** | No es una biblioteca: es una técnica de menos de diez líneas de CSS. Y no va en el panel |
| **react-loading-skeleton** | Se reproduce con poco CSS. Misma regla que tumbó a Auragradients: **si se escribe en un rato, no es una dependencia** |
| **Motion** | Arranca con `prefers-reduced-motion` **desactivado**. Es el motivo exacto por el que cayó Morphicons |
| **AutoAnimate** | Aporta un FLIP que sí es difícil a mano, así que estuvo cerca. Cae por dos cosas: **solo consulta la preferencia al inicializar y no escucha los cambios posteriores**, y al animar por JavaScript **la protección global de CSS no lo alcanza** — no se puede corregir desde fuera |

**La conclusión que ordena las seis, y que es lo que hay que conservar: el movimiento se hace
con la plataforma.** View Transitions, `@starting-style`, transiciones de `display` y
esqueletos de CSS cubren los casos que han aparecido, y **degradan solos** a cambio instantáneo
donde no hay soporte. `prefers-reduced-motion` se impone una vez en la hoja base
(`base.css:66-74`) y reacciona en caliente.

**La única grieta, y conviene tenerla escrita:** esa protección global es CSS, así que **no
alcanza a lo que anima por JavaScript**. Una biblioteca que mueva cosas desde JS tiene que
respetar la preferencia por su cuenta *y* reaccionar a sus cambios — y si no lo hace, no hay
forma de arreglarlo desde fuera. Es lo que descartó a AutoAnimate teniendo lo único que de
verdad costaba a mano.

**Disparador.** **Sin disparador definido para el conjunto.** Dos de las seis lo tienen dentro: el
selector de categorías N:M pide pasar antes por el paso 3.5 de diseño, y `E2E_KEEP_STACK` se paga
sola la primera vez que haya que reproducir un fallo de la suite desde cero. Las otras cuatro no lo
tienen, y **la tabla de bibliotecas descartadas no es un pendiente**: está aquí para no volver a
investigarlas.

---

## 12 · Riesgo abierto: el cajón del producto tras asociar una imagen (20 ago 2026)

**Observado dos veces**, las dos en una vuelta de la suite entera: en `recorrido.spec.ts`,
pulsar «Guardar cambios» justo después de asociar una imagen deja el cajón abierto **y sin ningún
aviso**. Entre una y otra no se reprodujo en seis intentos, ni aislado ni acompañado.

La segunda vez fue el 21 de agosto, **justo después de envolver `page.goto` para que espere al
armazón**. Eso cambió el ritmo de toda la suite, así que no se puede afirmar que sea la misma
carrera: puede serlo, o puede ser una interacción nueva. Lo que sí cambia es el estado del
riesgo — **dos apariciones ya no son una anécdota**, y toca investigarlo con la siguiente que
salga en vez de esperar a que se repita.

La carrera existe y se puede señalar: asociar una imagen recarga la ficha con el cajón abierto
(`ProductsPage.tsx:197` llama a `abrirFicha`), así que hay un momento en que el formulario se
está re-renderizando. **Lo que no está demostrado es que esa sea la causa** — la prueba pulsa
decenas de milisegundos después de ver la miniatura, que es algo que una persona no hace.

Lo hecho: la prueba espera a que la recarga termine, y afirma sobre los avisos de **toda la
página** y no solo del cajón, porque un fallo puede avisar por un mensaje flotante que vive
fuera. Si vuelve a pasar, dirá más que la primera vez.

Lo no hecho, y a propósito: no se ha tocado el producto. Arreglar una carrera que no se sabe
reproducir es cambiar código por una hipótesis, y quedarse sin la única señal que hay.

**Disparador.** **La tercera aparición.** Dos ya no son una anécdota, pero tampoco son un caso
reproducible, y arreglar una carrera que no se sabe reproducir es cambiar código por una hipótesis.
La prueba ya está preparada para que la próxima diga más que las dos anteriores.

---

## 13 · Los sueltos que venían de la bitácora

Estaban en la tabla de su §5. Se traen enteros, **con los tachados incluidos**: son de otros, y
recortar el registro de alguien no es de quien lo mueve.

> **Y una nota de forma que importaba más de lo que parece:** en el original había **una línea en
> blanco en medio de la tabla** (`BITACORA.md:474`), así que las seis últimas filas —el `.env`
> desfasado, el dominio, la tipografía, los datos de Bsale— **no se renderizaban como tabla en
> ningún visor de Markdown**. Estaban escritas y no se veían. Reenganchadas al mover.

| Pendiente | Estado |
|---|---|
| ~~**`CategoryService.cs:147` devuelve 500**~~ | **Cerrado en 04B.** Materializar antes de proyectar, con su prueba. Y de ahí salió `api-traduccion.spec.ts`, que llama a cada endpoint una vez contra una base real: era el punto ciego de «las pruebas de lógica no tocan la base» |
| ~~Verificación visual del panel completo~~ | **Cerrado el 18 ago.** El arnés absorbió las nueve secciones salvo tres juicios humanos de unos cinco minutos, y dos de ellos se hacen sobre la galería de capturas sin levantar nada. Ver `VERIFICACION-VISUAL-CORE.md` |
| ~~Resincronizar el sistema de diseño~~ | **Cerrado el 18 ago.** El bundle lleva `--link` y `--on-danger`, y son **18 componentes**: los 17 de `src/shared/ui/` —`ThemeToggle` entró en esta pasada— más `ModuleCard`. Design ya ve los tokens vigentes |
| ~~Sin decidir: qué precio enseña la tarjeta con variantes de precios distintos~~ | **Decidido y hecho en 04D (20 ago).** Enseña el **mínimo efectivo** —contando lo que se hereda, no solo los `price_override`— y lo dice con «Desde S/ 4,90». **Y si alguna presentación es «a consultar», toda la tarjeta lo es**, porque «desde» promete una cota y una presentación sin precio puede costar cualquier cosa. `ItemPricing.ForCard`, con las dos proyecciones —`ProductService.cs:94` y `CategoryService.cs:124`— probadas por separado |
| Arranque con base vacía | Revienta con `42P01` en crudo en vez de decir «faltan las migraciones». Es la primera pantalla que vería quien instale en una clienta |
| **Probar el aborto de la ADR-019 en vivo** | La función pura está probada; que el host **se niegue a arrancar** no. Es el efecto observable, y es lo único que la decisión promete |
| **La búsqueda no encuentra por prefijo** | `plainto_tsquery` exige la palabra entera: medido contra la base de demostración, `plum` → 0 y `plumon` → 1; `lapi` → 0 y `lapiz` → 1; `cuad` → 0. En un buscador donde se teclea a mano —y sobre todo en un selector que filtra mientras escribes— **está vacío casi todo el rato**, hasta que se termina cada palabra. El diagnóstico aparente era «une los términos con AND», que también es cierto (`cuaderno plumon` → 0) pero es lo que la gente espera de un buscador. Es conducta heredada de toda la búsqueda de M01 —`ProductService`, `CategoryService` y `CatalogService` usan la misma— así que cambiarla es su propio trabajo, no un arreglo suelto |
| ~~El nombre del negocio se pedía dos veces y el público se quedaba atrás~~ | **Cerrado el 21 ago.** La instalación escribe también el ajuste `business_name` (`SetupService.cs`), con el nombre que ya obliga a teclear. Antes iba solo a la fila de instalación y el ajuste —el que lee la tienda— se quedaba en `PENDIENTE_DEFINIR`: **un sitio recién instalado salía sin nombre.** La base de la demostración, ya instalada, **no se arregla sola**: se corrigió a mano con el nombre de su propia instalación |
| **El paquete que recibe un cliente lleva código de módulos que no ha licenciado** | Aunque no se rendericen: el armazón importa los componentes y la navegación de todos los módulos, así que entran al `bundle`. **Ya pasaba con el menú** (`layout/navigation.ts:46`), y la costura de la portada (`platform/homeSections.ts`) no lo empeora — es el mismo mecanismo. Es asunto de **licenciamiento, no de arquitectura**: el día que se decida, se decide para el menú y para la portada a la vez. Lo que hay que evitar entretanto es resolverlo a medias en uno de los dos |
| **`MultipleCollectionIncludeWarning` en el selector de productos** | `CatalogService.SeleccionAsync` proyecta dos colecciones —los precios de las presentaciones y las categorías— en un mismo `Select`, y EF avisa de que puede multiplicar filas. **Medido: es un factor constante y acotado, no crece con el catálogo.** El peor producto realista de una librería —6 presentaciones × 3 categorías— pide 18 filas en vez de 9; con los datos de hoy el máximo es 3. Y la consulta lleva `Take(50)`, así que el techo es 50 × (presentaciones × categorías) pase lo que pase. **Molestia declarada, no defecto.** Lo que no se ha medido es si EF parte la consulta o hace el producto cartesiano de verdad: eso pide leer el SQL generado, y no cambia la cota |
| Repaso visual de Swagger | Junto con la verificación del panel: las dos piden un navegador. **Los cuerpos de ejemplo ya no son parte de esto**: los dieciséis están puestos y probados (`zz-instalacion.spec.ts:44`) |
| ~~Un visitante anónimo provoca peticiones a `/api/admin/`~~ | **Cerrado el 21 ago.** «Quién soy» responde ahora 200 con `null` escrito —`AllowAnonymous`, porque preguntarlo sin sesión no es un error— y el token CSRF solo se pide cuando hay sesión. **No se cambió cuándo se pregunta sino qué se responde**: hacerlo solo en el panel obligaría a volver a preguntarlo al navegar de la tienda al panel sin recargar, y ahí sí se pierde la sesión. Y el criterio de cierre fue **quitar las tres válvulas** que lo descontaban, no que el número bajara | Visitar la tienda **sin sesión** deja cuatro 401 en consola: la aplicación pide `/admin/auth/me` y `/admin/auth/csrf` al arrancar en **cualquier** ruta (`SessionProvider.tsx:45` y `:53`). No es un fallo de seguridad —son 401 manejados a propósito con `allowUnauthorized`— pero es trabajo inútil en cada visita pública y ensucia la consola de quien mire. Nadie lo había visto porque **ninguna prueba visitaba la tienda sin sesión**. Sin tocar: es el arranque de CORE, y no pedir sesión en rutas públicas podría perderla al navegar de la tienda al panel sin recargar. **Lo hereda cualquier módulo que añada pantallas públicas** —M02 el primero—, así que conviene decidirlo antes de que se lo saque su puerta de cero errores como si fuera suyo |
| Verificación visual del panel | Sigue pendiente: es lo único que separa a CORE de estar verificado de punta a punta |
| Tu `.env` local está desfasado | Le faltan `API_PORT` y `MEDIA_PATH`, que sí están en `.env.example`. Sin ellos, `docker compose --profile full up -d` no levanta el API |
| Borrar `docs/BITACORA-SESION-2026-08-14.md` | Cumplió su función —traspasar contexto entre sesiones— y lo durable ya está en la ADR-012 y en las entregas. Dos bitácoras confunden cuál es la bitácora |
| Tipografía y logo de SILLAR | La paleta está validada; lo demás no |
| Dominio del producto | Sin registrar |
| Nombres comerciales de las ediciones | Pendientes. No bloquean: son etiquetas de venta, no identificadores de código |
| Datos administrativos de Bsale | Certificado, costo, volumen, series y correlativos. Preguntas 7 a 10 de la guía de observación |

Aplazados por decisión, no pendientes: retención de auditoría, vectoriales en medios, permisos granulares, vencimiento de licencias, marca blanca.

---

## Resueltos recientemente

*(se borran de arriba y se anotan aquí solo hasta que entren en la bitácora del módulo)*

- **La puerta no era reproducible.** `verificar.mjs` corría las pruebas del backend (`:126`) antes
  de la etapa e2e (`:157`), y dos pruebas de `ReactivacionRedSocialTests.cs:162` exigen la base
  `sillar_e2e`, que solo existe en la etapa 5. No podían correr nunca. El *394/394 con 0 omitidas*
  de la entrega de M02 salió de una corrida con la conexión apuntada a mano, no de la puerta.
  → En construcción: base efímera propia `sillar_verify_*` para la etapa 4.
