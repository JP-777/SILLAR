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
| **Arranque con base vacía** | Sigue abierto mientras el arreglo no esté en `main`. Hoy una instalación nueva puede llegar al `42P01` antes de explicar que faltan migraciones. **Disparador:** antes de la primera instalación fuera de nuestras máquinas; se cierra solo después de probar el arranque contra una base realmente vacía y de integrar el arreglo en `main` |
| **Probar el aborto de la ADR-019 en vivo** | La lógica está probada, pero falta observar al host negándose a arrancar cuando `core.modules` declara activo un módulo ausente del binario. **Disparador:** la próxima vez que se toque el instalador |
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

## 15 · El mapa del negocio no tiene módulo dueño

**Qué pasa.** El alcance del MVP lo prometía: hasta el 26 de agosto de 2026, la frase de
`ARQUITECTURA_MODULAR.md:419` enumeraba «mapa» entre lo que la primera instalación cubre. Ese mismo
día se le quitó, y **la tabla que lo sustituye (`:431`) dice lo que este pendiente arregla**: sin
dueño. Porque **no está en el §9 de ningún SPEC**. Comprobado sobre
`docs/modules/` entero el 26 de agosto de 2026: las dos únicas apariciones de la palabra son una
metáfora (`catalog/README.md:8`, «el mapa, no el territorio») y un formato de imagen
(`core/ENTREGA-03B-MEDIOS.md:17`, «mapa de bits»).

**Prometido y sin dueño es la peor de las dos formas de no existir:** quien planifica lo da por
cubierto y quien construye no lo ve en su SPEC, así que nadie lo echa de menos hasta que la web está
en línea sin decir dónde está la tienda.

**Las dos candidaturas, y no se decide aquí:**

| | Si es… | Entonces vive en |
|---|---|---|
| **Identidad del sitio** | Un dato del negocio que se escribe una vez y casi nunca cambia, como `business_name` | `core.site_settings` |
| **Contenido editable** | Algo que el personal cambia desde el panel junto a banners y redes | **M02** |

**No es una elección de gusto:** decide quién puede editarlo, si desaparece al desactivar M02, y si
un negocio que compra solo el catálogo tiene mapa o no. La segunda pregunta es la que más pesa —un
mapa que se va al desactivar el CMS deja un hueco en la página de contacto.

**Y hay un precedente que se le parece y conviene mirar antes de decidir:** `whatsapp` vive en
`cms.social_links` y no en `site_settings`, **y el SPEC de M02 explica por qué** — «porque en el pie
aparece junto a las demás» (`docs/modules/cms/SPEC.md:200`), mientras el número de contacto del
negocio se queda en CORE. Mismo dato repartido según **dónde se enseña**, no según qué es.

**Disparador.** Antes de cerrar la Fase 1, **o** cuando alguien pregunte por qué la web no enseña
dónde está la tienda. Lo segundo va a pasar antes.

---

## 17 · De quién son las etiquetas visibles de `entityType` — **DISPARADOR CUMPLIDO**

**Estado: disparador cumplido, pendiente de trabajo dedicado.** No es una previsión: el umbral ya
se pasó y está medido.

**Qué pasa.** La pantalla de Auditoría traduce `entityType` al castellano con un mapa que vive en
el frontend de esa pantalla (`AuditPage.tsx`). Es lectura traduciendo lo que escribió otro: quien
sabe cómo se llama un `product_item` es M01, no la pantalla que lo lee.

**La medición.** Al hacerlo se inventariaron los tipos que hoy se escriben de verdad en el
repositorio: **20**, repartidos entre CORE (7), M01 (5), M02 (5) y M04 (3). El criterio acordado
era «si el mapa crece más allá de un puñado de entradas, la etiqueta pertenece a la escritura y no
a la lectura». **Veinte no es un puñado: el mapa nació ya pasado del umbral.** Y crece con cada
módulo nuevo, que además no tiene forma de enterarse de que debe venir aquí a añadirse.

**Por qué no se hizo entonces.** Por decisión explícita: mover la etiqueta a la escritura significa
añadir un campo a `AuditEntry` y tocar `Sillar.Core.Contracts`, que es una costura compartida y no
cabía en la Corrección 3. Se dejó el mapa para poder cerrar la fuga de identificadores, que era el
objeto de aquella corrección.

**Qué falta.** Decidir si la etiqueta viaja en `AuditEntry` desde el productor, y con ella el
vocabulario a su módulo. Mientras tanto el mapa funciona y degrada con honestidad: un tipo
desconocido se muestra con su código técnico, no con un «Desconocido» inventado.

**Disparador.** **Cumplido.** Entra en la revisión de pendientes previa a la división oficial.


---

## 18 · Diseño pendiente: superficies de plataforma y selector N:M

**Qué pasa.** Cuatro cosas que se resuelven en la misma reactivación de diseño:

**1 · El protocolo no tiene forma de encargar una superficie de plataforma.**
`PROTOCOLO-DISENO.md` §3 encarga pantallas a partir del §9 del SPEC de un módulo, y el pie no
está en el §9 de ninguno porque es de la plataforma. **No es que diseño no lo hiciera: es que
nadie podía pedírselo.** Mientras eso no se arregle, cualquier superficie de plataforma futura
cae en el mismo agujero. El arreglo de fondo es **una línea en `PROTOCOLO-DISENO` sobre
superficies de plataforma**, no un encargo suelto para este caso.

**2 · WhatsApp abre una conversación, no un perfil.** Es la única de las cinco redes que no
lleva a una página que se visita: lleva a escribir un mensaje. Enseñarla junto a Instagram y
TikTok, con el mismo tratamiento y el mismo verbo implícito, promete algo distinto de lo que
hace. Hay que comunicárselo a diseño para que lo resuelva, no resolverlo por cuenta propia.

**3 · El pie no tiene tratamiento visual.** Hoy es un borde superior y una lista de enlaces de
texto centrados. Es honesto —el enlace dice a dónde lleva— y deliberadamente sin iconos: no se
añade una dependencia de iconos por cinco enlaces, ni se dibujan a mano logotipos que son marcas
de otros. Pero es lo mínimo para que exista, no una decisión de diseño.

**4 · El selector de categorías N:M con principal no está diseñado.** No es un problema de implementación: falta decidir visualmente cómo se seleccionan varias categorías y cómo se distingue una como principal. Se resuelve antes de volver al paso 4 de M01; inventar la interacción desde código sería saltarse el paso 3.5.

**Disparador.** Cuando diseño se reactive. Los cuatro puntos se le pasan juntos, y el 1 antes que
los otros tres: sin él, el encargo de los otros tres vuelve a ser una excepción a mano.

El selector de categorías debe quedar resuelto antes de retomar el paso 4 de M01.

---

## 19 · El verde de main no está registrado

**Qué falta.** La puerta se ejecuta sobre el commit que va a integrarse. Si ese mismo
commit entra por fast-forward, `main` queda verde por construcción, pero la evidencia
debe quedar escrita: commit, fecha y resultado.

**Regla de integración.** Cada movimiento de `main` registra en `BITACORA.md` el commit
que entró y el verde que lo autorizó. No se añade otra corrida rutinaria solo porque el
mismo árbol cambió de nombre de rama.

**Disparador para correr `main` directamente.** El primer rojo cuyo verde registrado ya
no baste por un cambio del entorno, o un rojo en el que todas las señales del veredicto
estén limpias y aun así la atribución no cierre.

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
