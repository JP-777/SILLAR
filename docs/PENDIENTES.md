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
> **Una rama no resuelve un pendiente.** Un pendiente se resuelve cuando el arreglo está en `main`, no cuando existe una rama que lo arregla.
>
> **Al resolver una entrada, se borra de aquí y se registra donde corresponda** —bitácora del
> módulo o `BITACORA.md`—. Este archivo no es un histórico: es lo que falta.

---

## 1 · ~~`catalogHome` promete un catálogo que puede estar vacío~~ — **resuelto el 3 de septiembre de 2026**

Se borra el contenido y se conserva el número, por el mismo motivo que el 12: se cita desde la
bitácora y renumerar rompería las referencias. La resolución está en `BITACORA.md` §7.

**Lo único que no era de este pendiente y sigue vigente:** `crmHome` declara `'con-contenido'`
fijo y **está bien que lo haga**. Lo que promete —«entra o crea una cuenta»— es cierto siempre;
no hay un listado detrás que pueda venir vacío. Arreglarlo «por analogía» sería convertir en
condicional algo que no depende de ningún dato.

---

## 2 · ~~El footer público de plataforma no existe~~ — **resuelto el 4 de septiembre de 2026**

Se borra el contenido y se conserva el número, por lo mismo que el 1 y el 12: se cita desde la
bitácora y renumerar rompería las referencias.

Existe `PublicLayout`, que envuelve las cuatro rutas públicas y monta el pie; `cmsFooter` es su
primer contribuyente y consume la API de Social Links que ya estaba lista y sin montar.

**Lo que este pendiente advertía y sigue vigente** está abajo, en el 18: el hueco de
`PROTOCOLO-DISENO` §3 no se cerró por entregar el footer. Se cerrará el día que el protocolo
diga quién encarga las superficies de plataforma.

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

**En esa misma visita se completan los datos administrativos de Bsale:** certificado, costo, volumen, series y correlativos —preguntas 7 a 10 de `GUIA-OBSERVACION-MOSTRADOR.md`. No necesita una salida separada: la evidencia es la misma visita.

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

## 7 · ~~¿Está M11 Pagos en la fase correcta?~~ — **resuelta el 26 de agosto de 2026**

Se decidió que no: M11 se queda en Fase 4 y M03 abre con Yape y efectivo. Está abajo, en «Resueltos
recientemente», con la restricción que deja para el SPEC de M03.

**El número no se reutiliza y los de abajo no se renumeran.** Hay referencias a estas entradas desde
`ARQUITECTURA_MODULAR.md` y desde los documentos de M07, y un número que cambia de dueño es peor que
un hueco: el hueco se ve, la referencia movida no.

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

## 10 · ~~Defecto abierto: la auditoría enseña identificadores~~ — **resuelto el 3 de septiembre de 2026**

Se borra el contenido y se conserva el número, como el 1 y el 12: renumerar rompería las
referencias. **El `test.fail` de `transversal.spec.ts` ya no existe**; esa prueba es una
afirmación normal. La resolución está en `BITACORA.md` §7.

---

## 12 · ~~Riesgo abierto: el cajón del producto tras asociar una imagen~~ — **resuelto el 3 de septiembre de 2026**

Se borra el contenido y se conserva el número: se cita desde `BITACORA.md` §7 y desde el propio
arnés, y renumerar el resto rompería esas referencias. La resolución está en `BITACORA.md` §7.

---

## 13 · Los sueltos que siguen vivos

Las filas cerradas que venían de la bitácora salen de aquí: su resolución ya está
registrada donde corresponde y `PENDIENTES.md` no es un histórico.

| Pendiente | Estado y disparador |
|---|---|
| **La búsqueda no encuentra por prefijo** | La decisión técnica ya está tomada: `COLLATE "C"` + trigram; no se vuelve a investigar desde cero. **Disparador:** la próxima unidad que toque servicios de M01, o antes de mostrar el catálogo a una clienta; lo que ocurra primero |
| **El paquete lleva código de módulos no licenciados** | Es una decisión de licenciamiento, no un arreglo aislado de navegación o portada. **Disparador:** cuando se decida el modelo de licencias; esa decisión llega al líder con la segunda clienta |
| **Conservar el stack e2e cuando haga falta diagnosticarlo** | `E2E_KEEP_STACK` solo se justifica cuando el desmontaje impida inspeccionar un fallo. **Disparador:** la primera vez que haya que reproducir desde cero un fallo e2e por falta de evidencia posterior |
| **Verificación humana de CORE** | JP revisa en una sola pasada el panel completo, Swagger y `:focus-visible` con ratón. Los cuerpos de ejemplo de Swagger ya están automatizados; aquí queda juicio visual. **Disparador:** antes de cerrar la Fase 1 |
| **Tu `.env` local está desfasado** | La formulación histórica ya no basta después de identidad derivada y `scripts/estrenar.mjs`, pero eso no autoriza a cerrar por analogía la condición general. **Disparador:** antes de reutilizar un `.env` existente en una instalación fuera de nuestras máquinas; ahí se decide si `estrenar.mjs` ya cubre el caso o si queda una regla explícita de sincronización |
| **Tipografía y logo de SILLAR** | La paleta está validada; lo demás queda para producto público. **Disparador:** antes de publicar el sitio |
| **Dominio del producto** | Sin registrar. **Disparador:** antes de publicar el sitio |
| **Nombres comerciales de las ediciones** | No bloquean código; son etiquetas de venta. **Disparador:** antes de publicar el sitio |

Aplazados por decisión, no pendientes: retención de auditoría, vectoriales en medios,
permisos granulares, vencimiento de licencias, marca blanca.

---

## 15 · ~~La ubicación del negocio está entregada, pero no se enseña~~ — **resuelta el 19 de septiembre de 2026**

Se conserva el número porque el cierre de Fase 1 ya la referencia. La premisa que le
daba nombre dejó de ser cierta: dirección, referencia, mapa y horario **se enseñan**,
y no en una superficie propia sino donde tenían que estar desde el principio —junto al
resto del contacto público.

La superficie que faltaba la aporta CORE como contribución al pie
(`frontend/src/modules/core/coreFooter.tsx:26-29`), con los valores leídos de los
ajustes públicos (`frontend/src/platform/publicContact.ts:35-50`) y pintados por un
único componente compartido (`frontend/src/platform/PublicContactDetails.tsx`), que es
el mismo que usa `/contacto`
(`frontend/src/modules/crm/pages/ContactPage.tsx:77-84`). Un negocio que compró
solamente el catálogo sigue teniendo pie con su dirección: era el supuesto que abrió
esta entrada y se cumple porque la contribución es de CORE, no del CMS.

La frase que dejó esta entrada sigue valiendo para la siguiente que aparezca:
*prometido y sin dueño es la peor de las dos formas de no existir*, y **entregado y sin
enseñar es peor todavía** — porque nadie lo echa en falta en la base de datos.

Entró en `main` con `294bb7944252f0b52d5c1873e54c76b16bb3157d`. Qué se publica, qué no
se publica y qué verde lo autorizó están en `BITACORA.md` §7.

---

## 17 · ~~El vocabulario de auditoría pertenece a cada módulo~~ — **resuelto el 10 de septiembre de 2026**

Se conserva el número porque ya forma parte de las referencias del cierre. La decisión quedó materializada en **cuatro contribuciones frontend**: CORE, M01, M02 y M04. La plataforma las compone sin conocer las etiquetas concretas y `AuditPage.tsx` dejó de ser propietario del vocabulario.

Con los cuatro módulos activos se conservan exactamente las mismas veinte asociaciones —CORE 7, M01 5, M02 5 y M04 3—. Si un módulo está desactivado, sus tipos históricos degradan deliberadamente al código técnico; los duplicados no tienen ganador silencioso y la señal de conflicto queda reservada al desarrollo.

La implementación y su costura de verificación entraron en `main` con `576fc7f3e843c0ce061ae72d030a8ce0781f3cd0`. La focal `test:audit-vocabulary` quedó incorporada a `[1/6]` antes del `typecheck`, pasó 14/14 y la puerta canónica completa pasó 6/6. La evidencia durable queda en `BITACORA.md`.
---

## 18 · El pie de plataforma no tiene tratamiento visual

**Esta entrada abrió con cuatro puntos y tres están cerrados.** Se dejan nombrados,
con lo que cerró cada uno, porque la entrada se cita entera desde la 2 y desde
`PENDIENTES-CLASIFICACION.md`, y porque el detalle importa: ninguno de los tres lo
cerró diseño.

**1 · El protocolo no tenía forma de encargar una superficie de plataforma. Cerrado el
20 de septiembre de 2026.** JP aprobó la opción A: corregir el protocolo ahora, en
lugar de pasar el encargo a mano y dejar el agujero abierto para la siguiente
superficie. Está escrito en `PROTOCOLO-DISENO.md` §3, «Superficies de plataforma». Era
el punto que bloqueaba a los otros tres, y por eso iba primero.

**2 · WhatsApp abre una conversación, no un perfil. Cerrado por producto.** Ya no se
enseña como una red más junto a Instagram y TikTok: aparece como **botón que nombra lo
que hace** y como **dato de contacto** al lado del teléfono y del correo
(`frontend/src/platform/PublicContactDetails.tsx:15-29`). Y tiene una sola fuente:
`whatsapp_number` de CORE manda, y el enlace heredado del CMS **deja de publicarse
cuando ese existe** en vez de competir con él
(`frontend/src/modules/cms/cmsFooter.tsx:69-76`). Dos destinos publicados que pueden
discrepar no son una redundancia: son un número equivocado esperando su turno.

**4 · El selector de categorías N:M con principal. Cerrado como decisión, no como
trabajo.** JP lo aprobó tal cual está el 20 de septiembre de 2026. Lo que hay hace lo
que la entrada pedía: varias categorías, una principal elegida solo entre las marcadas,
**promoción automática y anunciada** cuando se quita la principal
(`frontend/src/modules/catalog/components/CategoryAssigner.tsx:60-85`) y validación en
el backend, que rechaza una principal que no esté entre las asignadas
(`backend/Sillar.Modules.Catalog/Services/ProductService.cs:297` y `:525`). **Deja de
ser prerrequisito del paso 4 de M01.** Una sustitución visual futura sería una mejora,
no un bloqueo de Fase 1: la regla del paso 3.5 existe para que no se invente la
interacción desde código, y esta interacción ya está decidida y revisada.

**Lo que sigue vivo — 3 · el pie no tiene tratamiento visual.** Hoy es un borde
superior, una lista de enlaces de texto y una sección de contacto con estilos mínimos
(`frontend/src/platform/platform.css`). Es honesto —cada enlace dice a dónde lleva— y
deliberadamente sin iconos: no se añade una dependencia de iconos por cinco enlaces ni
se dibujan a mano logotipos que son marcas de otros. Pero es lo mínimo para que exista,
no una decisión de diseño. **Lo que cambió es que ahora se puede encargar**, que es
justamente lo que el punto 1 impedía.

**Disparador.** Cuando diseño se reactive, como superficie de plataforma y con los
módulos activos nombrados en el encargo: el mismo pie con solo CORE y con CORE más el
CMS no es la misma pantalla.

---

## 19 · ~~El verde de main no está registrado~~ — **resuelto el 10 de septiembre de 2026**

Se conserva el número para no romper referencias. La regla ya se aplicó a los dos movimientos posteriores al último registro: `d409c0e → 2d22ecf` y `2d22ecf → 576fc7f`.

Cada fast-forward queda acompañado en `BITACORA.md` por el commit probado y por el verde que lo autorizó. No se repitió una corrida únicamente porque el mismo árbol pasara a llamarse `main`: la evidencia pertenece al árbol integrado, no al nombre de la rama.
---

## 21 · H23 — el estado del sistema y lo que decían los registros no coincidieron una vez · **INCIERTO / DIFERIDO**

**Qué pasó.** Una discrepancia entre el estado efectivo del sistema y lo que indicaban
los registros. No se reprodujo después, y no se midió la causa.

**Por qué no dice «resuelto» ni «no reproducible».** «Resuelto» afirmaría una causa que
nadie comprobó. «No reproducible» suena a cerrado y es lo mismo con mejor cara: dice
que se buscó una vez, no que no exista. Lo único cierto es que quedó **incierto**, y
darlo por bueno sin volver a mirarlo es la barrera que calla de `BITACORA.md` §4 —
indistinguible de una que funciona hasta el día que hace falta.

**Disparador, y es explícito a propósito:** *reabrir inmediatamente si vuelve a
aparecer una discrepancia entre el estado efectivo del sistema y lo que indican los
registros.* No hace falta que sea el mismo síntoma ni el mismo componente. La segunda
ocurrencia es la que convierte una rareza en un defecto, y es además la que trae la
evidencia que la primera no dejó — por eso el disparador es la aparición, no una fecha.

**El número.** Se abre con el 21 porque el 20 se propuso y se disolvió sin llegar a
existir aquí (`PENDIENTES-CLASIFICACION.md` §F). Los números no se reutilizan.

**Y lo que esta entrada deja a la vista.** Hasta ahora «H23» no significaba nada dentro
de `docs/`: el catálogo de hallazgos H01–H29 se usó entero para cerrar la Fase 1 y
**ningún documento de este repositorio lo recoge** — los códigos solo aparecen en
código y pruebas, como `e2e/tests/h29-navegacion-estrecha.spec.ts` o
`scripts/verificar.mjs:2002`. Mientras eso siga así, esta entrada es la única forma de
volver a encontrar H23 el día que reaparezca, y queda para el líder decidir lo
general: o el catálogo entra a `docs/`, o cada hallazgo que no se cerró necesita su
entrada como esta.

---

## Resueltos recientemente

*(se borran de arriba y se anotan aquí solo hasta que entren en la bitácora del módulo)*

- **La auditoría ya no enseña identificadores.** (3 sep 2026) Era el 10. La columna «Entidad»
  muestra el tipo en castellano y el identificador completo vive en un `<details>` que se
  despliega a voluntad; el filtro «Usuario» pasó de pedir un número a un desplegable por nombre y
  correo, con los dados de baja incluidos. Había **una segunda fuga por la columna «Resumen»** que
  solo apareció al arreglar la primera. Registrado entero en `BITACORA.md` §7.

- **La portada ya no promete un catálogo vacío.** (3 sep 2026) Era el 1, y su disparador —el
  cierre formal de M02— se cumplió. `catalogHome` pregunta al mismo endpoint público que usa
  `/catalogo` si hay al menos un producto publicado, y declara `'cargando'`, `'vacio'` o
  `'con-contenido'` según la respuesta (`catalog/routes.tsx`). Los casos vacíos viven en
  `e2e/tests/aa-vacios.spec.ts`, que es el único momento de la suite en que el catálogo está
  de verdad vacío. Registrado entero en `BITACORA.md` §7.

- **El cajón del producto no se cerraba tras asociar una imagen.** (3 sep 2026) Era el 12, y su
  disparador —la tercera aparición— se cumplió en la puerta de la reconciliación. Ya no es un
  riesgo: la carrera está reproducida a voluntad y cerrada. **Una carga de la ficha que ya no es
  la vigente no puede escribir `editing`**, y las rutas que cierran el cajón invalidan lo que
  esté en vuelo antes de cerrarlo (`ProductsPage.tsx`). La prueba que la provoca —reteniendo la
  recarga con `page.route()` hasta después del guardado— está en
  `e2e/tests/imagenes-asociadas.spec.ts`. Registrado entero en `BITACORA.md` §7.

- **M11 Pagos se queda en Fase 4, y M03 no lo espera.** (26 ago 2026) La duda era si el checkout de
  M03 tenía que aceptar tarjeta desde el arranque. **No:** la tienda abre cobrando con **Yape y
  efectivo**, y la tarjeta llega con M11 sin adelantar nada de ella.

  **Lo que queda de esta decisión no es un pendiente, es una restricción del SPEC de M03**, y va
  escrita aquí hasta que ese SPEC exista:

  - **El pago se guarda como hecho consumado** —cuándo, método, referencia y **el nombre** del
    trabajador que lo registró—, **nunca una FK a `core.admin_users`**. Es exactamente lo que ya hace
    `b2b.quotes` y por el mismo motivo: esa tabla no se replica, y el dato que hace falta dentro de un
    año es **quién cobró**, que sobrevive a que la cuenta se dé de baja o se renombre.
  - **El estado del pago y el estado del pedido son dos cosas distintas.** Fundirlos obliga a rehacer
    la máquina de estados **con pedidos reales dentro** el día que entre la pasarela — y es el mismo
    defecto que M04 corrigió con «de baja» y «bloqueada»: **un estado que carga dos significados
    obliga a elegir el peor comportamiento para los dos.**

- **La puerta no era reproducible.** `verificar.mjs` corría las pruebas del backend (`:126`) antes
  de la etapa e2e (`:157`), y dos pruebas de `ReactivacionRedSocialTests.cs:162` exigen la base
  `sillar_e2e`, que solo existe en la etapa 5. No podían correr nunca. El *394/394 con 0 omitidas*
  de la entrega de M02 salió de una corrida con la conexión apuntada a mano, no de la puerta.
  → En construcción: base efímera propia `sillar_verify_*` para la etapa 4.
