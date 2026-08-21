# Bitácora de SILLAR

Registro vivo para continuar el trabajo. Los documentos de `docs/` dicen **qué** construir; esto dice **con qué criterio** y **qué toca ahora**.

Si retomas el proyecto sin haber estado en la conversación: lee las secciones 1 a 4 antes de decidir nada.

**Última actualización:** 18 de agosto de 2026 · **M01 pasos 1–3 cerrados · arnés `e2e/` en pie · un solo chat**

---

## 1. Estado

| | |
|---|---|
| Fundación F-01 a F-08 | Completa |
| CORE — backend | Completo. `media_assets` replicable. **221 pruebas** en la solución (132 + 54 + 35) |
| CORE — pantallas | Completo, **verificado a mano** y ahora cubierto por `e2e/` |
| **M01 Catálogo** | **Pasos 1 a 3 cerrados.** Esquema, contrato y API verificados en vivo. Toca el paso 4, interfaz |
| M02 en adelante | Sin empezar |
| SILLAR ERP (M13–M16) | **Nuevo producto.** En descubrimiento |

Entorno: PostgreSQL 16 en Docker con colación ICU `es-PE`. Backend en `:5080`, frontend Vite en `:5173` con proxy a `/api` y `/media`.

**CORE terminado en siete entregas** (1, 2, 2.1, 3, 3b, 4a, 4b): base de datos, API y panel. Doce ADR.

**Cambio de rumbo del 14 de agosto.** Aparece un segundo producto. Lo construido pasa a llamarse **SILLAR WEB**; el nuevo es **SILLAR ERP**, y **SILLAR** queda como la plataforma y la familia.

| | SILLAR WEB | SILLAR ERP |
|---|---|---|
| Dónde vive | Nube, una instancia por cliente | Máquina del negocio |
| Internet | Necesario | **Casi obligatorio.** Sin conexión = modo degradado |
| Clientes | Navegador | Envoltura de escritorio y red local |
| Base de datos | Réplica de lo compartido | **El mando** |

Comparten código y módulos; no comparten instalación. Decisiones en ADR-014 a 017 — **la ADR-013 quedó sustituida** y **la ADR-015 quedó enmendada en su sección de sincronización**.

**Lo que gobierna el ERP:** el host modular corre dentro del negocio con PostgreSQL local; la envoltura de escritorio existe por el hardware —impresión de tickets y cajón de dinero—, no por estética; **las existencias son por sucursal**; y **el mando es uno solo y explícito** — el ERP si existe, la nube si el cliente solo tiene la WEB. Comprar el ERP es un traspaso de mando supervisado, no una activación de módulos.

**SILLAR ERP está aparcado** hasta terminar lo que está en curso en SILLAR WEB. La ADR-017 existe para no perder lo decidido, no para construirlo: deja cinco puntos abiertos, y el primero —si las sucursales siguen siendo nodos autónomos— decide el tamaño de M16.

**Siguiente paso del ERP, cuando se retome: observar el mostrador.** Ver `GUIA-OBSERVACION-MOSTRADOR.md`. Sin esas mediciones no se especifica nada: un flujo de trabajo no se puede entrevistar, hay que verlo. Se puede hacer en paralelo, porque no consume tiempo de desarrollo.

**Del 16 al 18 de agosto el proyecto se planificó en dos chats que se turnaban**, Backend y Frontend. **Se deshizo el 18** — el motivo y lo que costó están en la §7, y merece leerse antes de volver a partir el trabajo en dos cabezas. `PROTOCOLO-DOS-CHATS.md` queda retirado, conservado sin editar; lo que sobrevive está repartido en `CLAUDE.md` y `PROTOCOLO-DISENO.md`. Sigue vigente lo que nunca dependió de haber dos chats: **el diseño es referencia, nunca origen**, y **ningún proveedor escribe en el repositorio**.

**Lo que hay que hacer antes del paso 4** está en la §5, en el bloque heredado. Lo que bloquea de verdad es **resincronizar el sistema de diseño**: lo diseñado para M01 se hizo contra tokens que ya no son los vigentes, y construir contra tokens viejos es rehacerlo dos veces.

**Después: paso 4 de M01.** Sirve a los dos productos, así que en cada campo se siguen preguntando las dos cosas: *¿tendría sentido en un negocio que solo tiene la web?* y *¿esto le cierra la puerta al mostrador?*

---

## 2. El criterio

Reglas de decisión del proyecto. Una duda nueva se resuelve con estas, no improvisando otras.

**No se construye una abstracción hasta que existe un segundo caso real.** Se ha usado para *no* construir: `Result`/`Error` sin endpoint que los use, recuento de referencias entre módulos y archivos, marca blanca del panel.

**Fallar ruidosamente antes que degradarse en silencio.** El host aborta si un módulo activo tiene una dependencia dura inactiva. Un 403 de CSRF no se reintenta. A medias es peor que parado, porque nadie lo mira.

**Lo específico de un cliente nunca entra al producto.** Ni en código, ni en semilla, ni en nombres. Primero configuración, luego opción del módulo, luego módulo aparte.

**La documentación describe lo que debe existir, no lo que existe.** Un documento de entrega no da por hecho que lo anterior está construido: lo comprueba.

**Cuando código y documento discrepan, gana el documento y se avisa.** Y si el código tenía razón, se corrige el documento explicando por qué. Ha pasado varias veces y siempre mejoró el documento.

**Lo barato ahora es carísimo después.** Renombrar el producto antes de escribir código costó una tarde. La colación se fijó al crear el clúster.

**En la interfaz: ningún «Ha ocurrido un error» y ningún botón «Aceptar».** Un conflicto es una frase que dice qué lo impide y qué hacer; un botón nombra la acción que ejecuta.

**Cuando una convención se puede convertir en error de compilación, se convierte.** `confirmLabel` sin valor por defecto hace imposible terminar con un «Aceptar» por descuido. Vale más que cualquier revisión.

**Los mensajes de error del API son texto de interfaz.** Desde que la interfaz muestra la frase del servidor, acortarla a algo técnico degrada el panel sin tocar el frontend. Se redactan para quien administra su negocio.

---

## 3. Decisiones que se rompen sin querer

| Decisión | Por qué |
|---|---|
| Una instancia por cliente | Ninguna tabla lleva `tenant_id`; el aislamiento es físico |
| Un schema por módulo | Desinstalar es soltar un schema |
| FK cruzada solo en dependencia dura | Las blandas van en `database/integrations/`, **nunca** en una migración |
| Migraciones EF como fuente de verdad | Seeds e integraciones siguen siendo SQL a mano |
| Dos colaciones | `es_ci` respeta tildes para identidad; `es_search` las ignora para búsqueda |
| Token CSRF derivado | Estable en la sesión → varias pestañas, y sobrevive al reinicio del host |
| `installation_key` no sale del servidor | Uso criptográfico. Todo uso externo va por un valor derivado |
| Activar un módulo reinicia el host | El enrutamiento se construye al arrancar |
| `is_orphan` = desinstalado, no desactivado | Desactivar es reversible y pasa en cada demostración |
| **Activo en la base + ausente del binario = abortar** | Es un despliegue defectuoso, no una desinstalación. Inactivo + ausente sí es huérfano (ADR-019) |
| El `Dockerfile` no enumera módulos | Copia todo y `.dockerignore` excluye por patrón. Una lista a mano se queda corta sola |
| **El tema tiene tres niveles, no dos** | `:root` con los valores base, `@media (prefers-color-scheme: dark)` protegido con `:not([data-theme="light"])`, y `[data-theme="dark"]` explícito. Sin elección del usuario **no se pone atributo**: un atributo puesto de más le gana siempre al medio |
| SVG rechazado | Se ejecuta en el mismo origen del panel y puede pedir el token CSRF |
| Panel con marca SILLAR | Es lo que se demuestra al vender |
| Baja lógica en todo | Lo borrado deja huecos en banners y pedidos que lo referencian |
| Sin desactivación en cascada | El sistema nombra el obstáculo; la persona ordena |
| Módulos de demostración imposibles en producción | Dos barreras de compilación: `ProjectReference` condicionada a `Debug` y todo el contenido bajo `#if DEBUG`. En `Release` la DLL ni existe. La comprobación de ejecución es la tercera, pero es **configuración** y no basta sola |
| `localStorage` solo para preferencias de interfaz | La prohibición es para la sesión y el CSRF. El tema se guarda en `sillar:theme`, y solo cuando el usuario elige |
| SILLAR es la plataforma; WEB y ERP son los productos | Comparten código y módulos, no instalación |
| M13 depende de M14 de forma **blanda** | Sin Comprobantes, el punto de venta vende igual. Es lo que lo hace vendible fuera de Perú |
| Existencias **por sucursal**, no globales | Dos locales sin conexión no pueden vender la misma última unidad si cada uno descuenta de lo suyo |
| El conteo global es una **vista**, no una bolsa | Informa, no manda. De ahí no se descuenta nunca: se vende siempre de una sucursal concreta |
| Solo se suma lo **conectado** | El error va siempre hacia abajo: quedarse corto se corrige con una llamada; prometer de más se corrige delante del cliente |
| Preguntar no es comprar | La consulta a otro local no aparta nada. Separar y trasladar son actos del comprador, y son documentos |
| **M17 no aporta «sucursal», aporta «más de una»** | Sin él hay una sola ubicación y M09 funciona igual. M09 no sabe qué es una sucursal |
| Se cuenta y se cobra la **variante**, no el producto | 3 verdes y 0 azules no pueden sumar «hay 3». M09 y M13 apuntan a `product_items` |
| **Lo que se replica no referencia a lo que no se replica** | La fila viaja y la referencia se queda. No hay error de FK: solo un catálogo sin fotos (ADR-018) |
| Borrar un medio en uso: `SET NULL` donde cabe, `CASCADE` donde no | `RESTRICT` sacaría a la interfaz una violación de clave foránea, que es el error genérico prohibido |
| La galería avisa **sin contar** | «Si esta imagen está en uso, desaparecerá de donde esté». No ser silencioso no es lo mismo que ser exacto |
| La variante tampoco aporta «variante»: aporta **«más de una»** | Todo producto nace con una, sin nombre e invisible. La palabra no aparece hasta que hay dos |
| Un solo nodo manda en cada momento | Ningún nodo se declara mando a sí mismo porque dejó de oír al otro. Retomar el mando es un acto humano |
| Réplica ≠ respaldo | La réplica copia fielmente también el borrado accidental de doscientos productos. Hacen falta las dos |
| Se replica lo compartido, no el esquema entero | M01, M04 y M09. La web no carga caja ni compras; el ERP no carga banners ni captación |
| `uuid` v7 en tablas que se replican | Siguen naciendo filas en dos sitios: pedidos en la web, ventas en el mostrador. **Antes de M13** |

---

## 4. Hábitos que salieron de errores reales

- **Los avisos del arranque se leen.** Dos líneas ignorables delataron que la base ordenaba `ñandú` después de `zapato`.
- **Verificar el efecto observable, no que el mecanismo actúe.** El límite de tamaño funcionaba y la respuesta devolvía 500 en vez de 413.
- **Patrones de `.gitignore` anclados a la raíz.** `media/` sin anclar se tragó código fuente.
- **`tasklist /FI "PID eq <pid>"` antes de matar nada.** Matar por puerto tumbó Docker Desktop entero. Para liberar un puerto, cambiar el puerto.
- **Serialización de binarios explícita.** `Guid.ToByteArray()` habría dado tokens distintos en Windows y en Arch.
- **Cada entrada de `onlyBuiltDependencies` exige justificación en el commit.** La lista corta es la defensa.
- **Una lista escrita a mano de lo que hay que copiar se queda corta sola.** El `Dockerfile`
  enumeraba proyecto por proyecto y nunca se enteró de M01: `dotnet` avisó con un
  «Skipping project … because it was not found», el build terminó con éxito y el contenedor
  arrancó diciendo «Módulos descubiertos: 1». Un módulo entero desaparecido, sin un solo error.
  Se sustituyó por copiar todo y excluir por nombre en `.dockerignore`: así el próximo módulo
  entra solo y lo que se enumera es la excepción, no la regla.
- **Nunca canalizar SQL ni secretos por PowerShell.** `Get-Content x.sql | docker compose exec` convierte el texto a la página de códigos de la consola y las tildes llegan como `?`; y un hash BCrypt entre comillas dobles pierde todo lo que va detrás de cada `$`, porque PowerShell lo lee como variable. **Las dos veces el comando dijo que fue bien y escribió basura.** La forma segura es `docker compose cp` del archivo y luego `psql -f` dentro del contenedor.
- **El «modo oscuro» del navegador no es `prefers-color-scheme`.** Opera y Chrome tienen un forzado que aplica un filtro encima del tema claro. Verificar con eso da hallazgos falsos: hay que mover el tema del sistema operativo o emularlo desde las herramientas de desarrollo.
- **Una config que se lee correcta no prueba que el artefacto la contenga.** `componentSrcMap`
  hacía a `ModuleCard` descubrible para tipos y documentación, y **el bundle seguía sin
  incluirlo**: el entry sintetizado solo empaqueta lo que está bajo `src/shared/ui/`, así que
  `window.SillarUI.ModuleCard` quedaba `undefined`. La config se leía bien y el componente no
  llegaba; se arregló con `extraEntries`. Es la regla de **verificar el efecto observable y no
  que el mecanismo actúe**, y aquí se cumplió sola: lo destapó contar las entradas de
  `renderHashes` —16 donde debía haber 17—, no releer el `config.json`. Cuando se acuerde una
  comprobación, que sea un recuento o un `rg`, nunca «está bien puesto en la configuración».
- **`opacity` nunca sobre un contenedor con texto. Es regla, no anécdota.** Atenuar un
  contenedor atenúa su texto, y el contraste no sobrevive: `.mod.is-off` al 62 % y
  `tr[data-dimmed]` al 55 % dieron 2.4:1 y 2.14:1. **Van dos veces**, las dos corregidas en su
  instancia y las dos reaparecidas en otro componente — porque estaba anotado como lección y no
  como regla. Lo atenuado se marca con **borde, fondo o insignia con texto**, nunca bajando la
  opacidad de algo que se lee. Si hace falta atenuar de verdad, se atenúa un elemento sin texto
  dentro (un icono, una miniatura). Misma familia que las dos reglas de abajo: un valor que
  sirve para una cosa deja de servir cuando se aplica a otra.
- **Una protección redundante deja de parecer redundante el día que la otra falla.** Al mover
  `SILLAR-DISENO/` fuera del repositorio, la línea del `.gitignore` quedaba de sobra y se dejó
  puesta como red. **Se cobró el mismo día:** el agente de investigación siguió escribiendo en
  la ruta vieja —nadie le avisó del movimiento— y la carpeta reapareció dentro con sus informes.
  No entró a git porque la red seguía ahí. Quitar una protección porque «ya no hace falta» es
  apostar a que nada vuelva a la situación anterior.
- **El umbral del indicador de carga es 1000 ms, no 200.** Las dos cifras existen y hay que
  saber cuál gana: el material de **Diseño dice 200 ms**, la **investigación de usabilidad dice
  que por debajo del segundo no va nada**, y **gana la investigación** (ENTREGA-04A §4). Con 200
  ms casi toda petición acabaría enseñando indicador, que es lo contrario de lo que el umbral
  busca. Está escrito aquí porque el número equivocado sigue vivo en un documento archivado, y
  alguien lo va a reintroducir leyéndolo de buena fe.
- **Un detector mal parametrizado da el mismo verde que uno bueno.** `api-traduccion.spec.ts`
  llamaba a `lookup` con `?code=`, y el parámetro es `codigo`: la petición moría en la guarda de
  vacío **sin ejecutar la consulta**, que era justo lo que venía a probar. Igual pasó con la
  aserción de movimiento reducido, que falló por un regex mío y no por la aplicación. Una prueba
  que no toca lo que cree tocar es peor que no tenerla: ocupa su sitio.
- **`sm` vale para un control repetido en fila densa con ratón, y solo cuando existe otra
  manera de hacer lo mismo.** Las dos mitades cuentan. `Pagination` fijaba `sm` por dentro y
  dejaba la navegación del catálogo en botones de 26,8 px: repetido no era, denso tampoco, y
  **para llegar a la página 2 no hay segunda vía**. Un filtro sí puede ser pequeño —se quita
  desde su etiqueta o limpiando todos—, pero pequeño no es inalcanzable: el mínimo táctil sigue
  valiendo. Esta regla sola habría cazado la paginación antes de escribirla.
- **Una prueba afirma sobre datos que ella creó, y los encuentra por su identidad, no por su
  posición.** La prueba del precio buscaba sus productos «en la primera página» y dejó de
  funcionar en cuanto otra spec llenó el catálogo. Con 74 pruebas y subiendo, de ahí salen los
  arneses intermitentes. La siembra puede compartir estado —el catálogo real tampoco se vacía—
  pero **la disciplina va en el lado de la afirmación**: se busca por nombre, no por posición.
  Y si cada prueba trabaja sobre lo suyo, no hay nada que restaurar después.
- **Las dos vueltas enteras compran justo esto.** Los dos fallos de 04E pasaban al correr su
  spec sola y fallaban en la suite completa. Una ejecución por spec no los habría visto nunca,
  así que el coste de correr las 74 dos veces no es burocracia: es lo único que separa «esta
  prueba pasa» de «esta prueba pasa junto a las demás».
- **Una regla de estado que cambia SOLO el fondo es la que rompe el contraste.** Es la forma
  concreta del fallo anterior, y sale del censo: de las cinco reglas de `:hover` que oscurecen
  el fondo, cuatro **suben el color del texto a la vez** —`.ui-button--ghost`,
  `.ui-drawer__close`, `.ui-table__sortable`, `.ui-button--secondary`— y por eso ninguna falla.
  La única que cambiaba el fondo **sin tocar el texto** era `.ui-table tbody tr:hover`, y fue la
  única que dio 4.2:1. Al escribir un `:hover`, `:focus` o `:active`: si toca el fondo y no el
  texto, hay que comprobar el par a mano — `axe` mira el reposo.
- **Un fallo que solo aparece en hover, foco o activo es invisible para una pasada estática.**
  `axe` mide una foto, no un comportamiento — el contraste de `--text-subtle` sobre
  `--bg-sunken` **solo existe con el ratón encima**. Es la tercera vez que la puerta mide el
  reposo y se le escapa el estado: antes fueron el anillo congelado —contraste perfecto, cero
  información— y las tres transversales que escaneaban antes de que cargara la tabla.
- **Un dato que informa una decisión tiene que llegar antes de la decisión, no en su
  respuesta.** Van dos: `restartsAutomatically`, que el diálogo necesita antes de activar, y
  `productCount`, que existía **solo en la respuesta de la baja** — es decir, cuando ya se
  había elegido. Un recuento que llega después no informa, informa el pasado. La comprobación
  es una pregunta: *¿esto se lee para decidir? Entonces viaja en el listado.*
- **Un rol semántico nombra un significado, no una apariencia.** En cuanto un segundo uso
  quiere el mismo color por otro motivo, se parte. **Van tres:** `--primary`, que era fondo de
  botón y color de enlace (se partió en `--link`); `--danger`, que era texto y fondo (de ahí
  `--on-danger`); y `success`, que dice «va bien» y también «es la que ubica». El síntoma es
  siempre el mismo: el tono que sirve para una combinación no aguanta la otra.
- **Un token validado contra un fondo hay que validarlo contra todos los fondos del sistema.**
  `--text-subtle` cumplía 5.3:1 sobre `--bg-raised`, donde se comprobó, y daba **4.2:1 sobre
  `--bg-sunken`** — que es el fondo de una fila con el ratón encima, y también el de las cajas
  hundidas. No es un color nuevo: es el mismo en un sitio donde nadie lo miró.
- **Antes de dar tono oscuro a un color semántico, mirar si alguien lo usa de fondo.** Un
  semántico sirve de texto y de fondo, y esas dos necesidades **no piden el mismo tono**: el que
  se lee sobre fondo oscuro no aguanta texto blanco encima. Pasó con `--primary` (se partió en
  `--link`) y **volvió a pasar tres días después con `--danger`** (de ahí `--on-danger`). La
  comprobación es `rg "var\(--<token>\)" --glob '*.css'` y ver cuáles son `background`.
- **Una prueba que espera a que «la pantalla sea visible» puede estar mirando el hueco.** Tres
  pruebas transversales pasaban sin escanear nada: `main` es visible con el spinner puesto y la
  tabla vacía. Se descubrió porque una de ellas era intermitente. **Una prueba intermitente es
  la única señal de que otra estable puede estar vacía** — antes de silenciarla, mirar qué mide.
- **Una carpeta que un documento declara «fuera» tiene que estar fuera, no solo ignorada.**
  `PROTOCOLO-DISENO.md` §7 dice que `SILLAR-DISENO/` vive fuera del repositorio, y estaba
  dentro de la raíz, sin rastrear **y sin ignorar** — con los HTML y los `_ds_bundle.js` que
  exporta Claude Design, que es justo lo que el §1 prohíbe que entre. Nada fallaba: un solo
  `git add -A` habría metido el código de diseño en el repositorio del producto.
  **El `.gitignore` no bastaba**, y esa fue la lección real: protege del commit y de `rg`, que
  respeta el ignore, pero **no de `find`, de `grep -r` ni de abrir la ruta directa**. Una
  protección que funciona con una herramienta y no con otra es peor que ninguna, porque no se
  sabe cuál se aplicó. Se movió a `C:\SILLAR-DISENO` el 18 de agosto y el ignore se dejó
  puesto como red (`.gitignore:69`).
- **Un campo nulo puede significar dos cosas.** `admin_user_id` nulo era «cuenta eliminada» y también «intento de acceso con un correo inexistente». Confundirlas habría hecho que la auditoría mintiera sobre gente que solo se equivocó al teclear.
- **Una media query escrita antes de su regla base no aplica nunca.** Misma especificidad, gana
  la última. `layout.css` declaraba `.ly-sidebar { display: none }` dentro de un
  `@media (max-width: 860px)` colocado en la línea 16, y la regla base en la 29: **el CSS decía
  «esconder en móvil» y no escondía nada**. El panel llevaba roto en móvil sin que nadie lo
  supiera, porque nadie mira una hoja de estilos para comprobar que una regla que está escrita
  además se aplica. Se ve leyendo el `display` calculado, no el archivo. Y al arreglarlo salió
  la segunda mitad: el `display: none` que declaraba **tampoco era lo correcto**, porque no hay
  menú en la barra superior y esconder la lateral deja el panel sin ninguna forma de navegar.
- **Una `url` vacía es un hueco, no una ausencia.** Dar de baja un archivo dejaba la fila de
  galería del producto con `url: ""`, que en la ficha se pinta como imagen rota; el `?? ""`
  parecía defensivo y era justo lo contrario. **Lo que no se puede leer no se devuelve**: se
  filtra en la proyección. La fila se queda en la base —CORE no escribe en el schema del
  catálogo— pero deja de viajar.
- **Un estado vacío solo existe antes de que nadie cree nada, y eso es orden, no suerte.** Las
  tres pruebas de «todavía no hay nada» pasaban porque `catalogo`, `categorias` y `productos`
  van antes alfabéticamente que casi todo lo que siembra. En cuanto nacieron
  `medios-compartidos` y `presentaciones`, la de productos empezó a fallar **solo en la suite
  entera** — sola seguía verde. Están juntas en `aa-vacios.spec.ts`: si algo depende de correr
  primero, que se lea en el nombre del fichero.
- **Buscar por identidad también en la siembra.** `q=Torta` con `.find(p => p.name.includes(...))`
  cogía «Torta hereda consultar», de otra spec: la prueba despublicaba un producto y preguntaba
  por otro. El slug es la identidad; el nombre es una coincidencia. Es la misma regla de «no
  depender de la posición», aplicada a la búsqueda por API en vez de a la fila de una tabla.
- **Declarar lo que NO se promete vale tanto como declarar lo que sí.** El mismo día salieron
  tres accidentes de implementación que parecían garantías: el bus despacha los handlers en
  serie, el frontend de M01 manda las peticiones de presentaciones en fila, y el
  `if (isDeactivating)` hacía que emitir un evento dependiera de cuántas presentaciones tuviera
  el producto. **El primero era inofensivo porque está declarado** — el `<remarks>` de
  `InProcessEventBus` dice que no hay orden garantizado, así que quien construya encima sabe que
  se lo está jugando. Los otros dos no lo estaban, y uno era un hueco. El `<remarks>` del bus es
  el modelo a copiar.
- **Una comprobación que modifica lo que mide no comprueba: contamina.** Y su pariente: **una
  prueba que presupone el estado que se investiga no lo descarta** — mirar si una variable está
  definida *ahora* no dice nada de si lo estaba entonces, y peor, tocar el entorno para
  averiguarlo destruye la respuesta.
- **Un arreglo sobre la causa equivocada deja el caso con aspecto de cerrado.** Es peor que no
  arreglar nada, porque quita las ganas de seguir buscando. De ahí que convenga escribir «causa
  sin establecer» cuando lo es, aunque el síntoma ya no esté.
- **Una corrección se verifica con el mismo rigor que el fallo que corrige.** Lo que llega
  etiquetado como arreglo entra sin que nadie lo mire, que es exactamente cuando no debería.
- **Una vía cerrada no es una vía sin explorar.** «No encontramos nada» y «esto no pudo grabarse
  nunca» llevan al mismo sitio y valen distinto: la segunda gradúa la confianza, la primera deja
  la duda abierta para siempre. Cerrar una vía vale tanto como abrirla, y hay que decir cuál de
  las dos cosas se hizo.
- **La mitad no probada de una tanda probada es la que menos se mira**, precisamente porque la
  tanda salió bien. Al romper una emisión a propósito, tres de cuatro pruebas se pusieron rojas y
  la cuarta se dio por buena — vivía en otro archivo y **ninguna de sus aserciones se había visto
  fallar**. Hubo que romper el segundo archivo aparte. Una tanda no está verificada hasta que
  cada aserción ha fallado una vez.
- **Una conducta escrita para un caso y no para su simétrico es la forma del hueco.** Se buscó el
  simétrico de la baja —¿reactivar avisa?— porque editar una presentación no avisaba y su
  simétrico, desactivarla, sí. Esta vez no había hueco: reactivar pasa por `UpdateAsync`, que
  emite sin condición (`ProductService.cs:463`). **Pero la sospecha valía igual**, y ahora hay
  una prueba que lo fija en vez de una lectura que lo supone.
- **Una vía de investigación puede estar cerrada por no haberse grabado nunca, no por haberse
  perdido.** Los registros de PostgreSQL del contenedor **sí** cubren la ventana del incidente
  —llegan al 20/08 06:31— y aun así no contienen nada: `log_connections`, `log_disconnections` y
  `log_statement` están en `off`/`none`, así que una conexión que escribe una fila sin error no
  deja rastro. Distinguirlo importa: no se perdió la prueba, no se tomó.
- **El historial de PowerShell no ve lo que hacen los agentes.** PSReadLine solo graba consolas
  interactivas, y las herramientas de los dos agentes lanzan `powershell.exe -NonInteractive`,
  que no escribe historial — comprobado: la última escritura del archivo es del 20/08 19:17 UTC,
  once horas antes del incidente, pese a haber corrido decenas de comandos desde entonces.
  **Buscar ahí solo puede encontrar lo que tecleó una persona.**
- **Un contrato está cerrado cuando un uso nuevo no lo amplía.** El de selección de M01 se dio
  por cerrado con las tres respuestas de su primer consumidor, y el **segundo uso** —releer para
  refrescar un snapshot— le añadió un campo: `IsActive`, porque `null` estaba significando dos
  cosas a la vez, «lo dieron de baja» y «ya no existe», que piden respuestas distintas. Estaba
  cerrado contra **un** uso, no cerrado. Barato hoy porque nadie lo consume todavía; el mes que
  viene, no.
- **Y de paso, el dato que M01 cerró sin poder tener:** mirar el contrato desde fuera dio dos
  carencias —slug e imagen—; **usarlo de verdad ha dado cuatro más**: precio, publicación,
  categoría y ahora la baja. Ninguna se vio al mirarlo. Mirar un contrato sirve para no
  equivocarse en lo que hay; **no sustituye al primer cliente**.
- **Demostrar que un mecanismo puede producir el efecto no demuestra que produjera éste.** Al
  provocar el fallo del `.env` salió un mecanismo real —y contrario a lo que se creía— y se dio
  por explicado el incidente con él. No podía serlo: la fila la escribió un binario que declara
  `cms`, y **el de `main` no lo declara** — lo dijo el propio arranque al reiniciar
  (`ModuleSynchronizer.cs:165`). Hay que comprobar que el caso concreto pasa por el mecanismo,
  no solo que el mecanismo existe. Tercera de la familia, junto a «un detector mal parametrizado
  da el mismo verde» y «provocar un fallo distinto da el mismo rojo».
- **Gana el `.env` del árbol del binario, no el del directorio desde el que se lanza.**
  `DotEnv.Load` prueba `AppContext.BaseDirectory` primero y solo mira el directorio de trabajo si
  ahí no hay ninguno, y la búsqueda **sube por el árbol**. Comprobado provocándolo: ejecutando
  desde una carpeta con su propio `.env`, cargó el del proyecto igualmente. Así llegó un módulo
  en construcción a la base de la demostración —el 21 de agosto— sin que nadie se enterara en
  días. **La carga era muda**, y una configuración equivocada se ve exactamente igual que una
  correcta: levanta, conecta y funciona, contra la base de otro. Ahora el arranque dice de qué
  archivo cargó y a qué base apunta, sin la contraseña.
- **El worktree aísla lo que está en git; todo lo de fuera sigue compartido.** La base de datos,
  el puerto 5080, `MEDIA_PATH` y las variables de entorno no los separa nadie. Es lo que hace que
  dos ramas «aisladas» se pisen sin tocar un solo archivo del repositorio.
- **Un valor derivado cambia cuando cambia su entrada, no cuando cambia la fila.** Formulación
  de M02, y es la regla que gobierna cualquier snapshot: quien copia datos de otro módulo no
  necesita saber en qué tabla vive cada campo, necesita enterarse cuando lo que copió deja de
  ser cierto. De ahí que M01 emita `ProductoActualizado` al cambiar sus **presentaciones** o sus
  **categorías**, no solo al editar la fila del producto.
- **Un arreglo bueno hace visible un hueco que llevaba ahí desde siempre.** El contrato atómico
  de 04D —que el `PUT` del producto acepte código y precio cuando hay una sola presentación—
  destapó que el otro camino estaba mudo: **el mismo cambio observable emitía evento o no según
  cuántas presentaciones tuviera el producto**. Con una, por el `PUT`, que emitía; con tres, por
  `items/{itemId}`, que no. Nadie de fuera puede deducir esa diferencia, y no era una decisión
  de diseño: el `if (isDeactivating)` cubría el caso raro y dejaba fuera el frecuente —cambiar
  un precio.
- **Una precisión que abarata un problema hay que medirla antes de creérsela.** Se propuso emitir
  solo por los productos «cuya principal se desactiva y tienen otra activa», suponiendo un
  conjunto pequeño. Medido con la regla real de `ChooseTarget` (`Breadcrumb.cs:29`) contra los
  veinte productos de demostración, **cambian su categoría efectiva casi todos los de la
  categoría**: 4 de 4 en Oficina, 3 de 3 en Cuadernos. Faltaban dos casos —quien la tiene de
  principal y **no** tiene otra activa pasa a no tener ninguna, que también es un cambio; y quien
  la tiene de secundaria siendo la elegida porque su principal está inactiva.
- **Antes de arreglar lo que un detector señala, comprobar que el detector mira donde debe.** Un
  detector de desbordamiento listó cuatro pantallas rotas a 390 px por su tabla: eran falsos
  positivos, porque una tabla dentro de una caja con `overflow-x: auto` **no desborda la
  página**, se desplaza. Y el mismo día, un detector de ejemplos de Swagger dio cero con los
  dieciséis ejemplos ya puestos, porque los buscaba en el `requestBody` y con `$ref` viven en
  `components.schemas`. Las dos veces la lista tenía muy buena pinta. **Un detector mal
  parametrizado da el mismo rojo que uno bueno**, igual que daba el mismo verde.
- **Una búsqueda en vivo enseña el catálogo entero hasta que llega su respuesta, y la URL
  cambia antes que la respuesta.** Afirmar «esta tarjeta está» justo después de teclear el
  filtro puede estar mirando el render de antes: con la suite llena, una de las dos tarjetas
  buscadas aparecía en el listado sin filtrar y la otra no. Y esperar a que la URL lleve el `q`
  **no arregla nada**, porque la URL se actualiza al teclear y la petición va detrás —el
  recuento seguía en 12, una página entera sin filtrar, durante diez segundos.
  Lo que sí vale: **entrar ya filtrado** cuando lo que se afirma es «esto está en la tienda», y
  dejar el teclear para la prueba que va de teclear. El backend estaba bien: `q` con un número
  que no existe devuelve 0, comprobado contra el API antes de tocar nada.
- **Una carrera se mide esperando, no muestreando.** «Tras navegar el foco no está en el
  contenido» falló una vez en la suite entera: `toHaveURL` pasa en cuanto cambia el historial y
  `RouteFocus` mueve el foco en un efecto, que corre después. Entre las dos cosas hay un hueco
  que crece con la máquina cargada, y `page.evaluate` lo miraba una sola vez ahí dentro. **Un
  fallo intermitente en una aserción de estado casi siempre es esto**, y la primera hipótesis
  —que el `main` se remontaba— era falsa: vive en `AdminShell.tsx:90` y no se remonta al
  navegar. Comprobarlo antes de arreglar nada ahorró tocar cuatro páginas por una suposición.
- **Una aserción que da cero también hay que verla fallar una vez.** Corolario de «afirma la
  propiedad, no el elemento», y el tercer caso del mismo día:
  `expect(getByRole('radiogroup')).toHaveCount(0)` sobre un `fieldset`, que no toma ese rol
  solo, contaba cero hubiera radios o no. Una aserción de ausencia es indistinguible de una mal
  escrita **mientras pase**: la única forma de saber cuál es cuál es romperla a propósito y ver
  si el mensaje sale.

---

## 5. Pendientes

| Pendiente | Estado |
|---|---|
| ~~**`CategoryService.cs:147` devuelve 500**~~ | **Cerrado en 04B.** Materializar antes de proyectar, con su prueba. Y de ahí salió `api-traduccion.spec.ts`, que llama a cada endpoint una vez contra una base real: era el punto ciego de «las pruebas de lógica no tocan la base» |
| ~~Verificación visual del panel completo~~ | **Cerrado el 18 ago.** El arnés absorbió las nueve secciones salvo tres juicios humanos de unos cinco minutos, y dos de ellos se hacen sobre la galería de capturas sin levantar nada. Ver `VERIFICACION-VISUAL-CORE.md` |
| ~~Resincronizar el sistema de diseño~~ | **Cerrado el 18 ago.** El bundle lleva `--link` y `--on-danger`, y son **18 componentes**: los 17 de `src/shared/ui/` —`ThemeToggle` entró en esta pasada— más `ModuleCard`. Design ya ve los tokens vigentes |
| ~~Sin decidir: qué precio enseña la tarjeta con variantes de precios distintos~~ | **Decidido y hecho en 04D (20 ago).** Enseña el **mínimo efectivo** —contando lo que se hereda, no solo los `price_override`— y lo dice con «Desde S/ 4,90». **Y si alguna presentación es «a consultar», toda la tarjeta lo es**, porque «desde» promete una cota y una presentación sin precio puede costar cualquier cosa. `ItemPricing.ForCard`, con las dos proyecciones —`ProductService.cs:94` y `CategoryService.cs:124`— probadas por separado |
| Arranque con base vacía | Revienta con `42P01` en crudo en vez de decir «faltan las migraciones». Es la primera pantalla que vería quien instale en una clienta |
| **Probar el aborto de la ADR-019 en vivo** | La función pura está probada; que el host **se niegue a arrancar** no. Es el efecto observable, y es lo único que la decisión promete |
| **La búsqueda no encuentra por prefijo** | `plainto_tsquery` exige la palabra entera: medido contra la base de demostración, `plum` → 0 y `plumon` → 1; `lapi` → 0 y `lapiz` → 1; `cuad` → 0. En un buscador donde se teclea a mano —y sobre todo en un selector que filtra mientras escribes— **está vacío casi todo el rato**, hasta que se termina cada palabra. El diagnóstico aparente era «une los términos con AND», que también es cierto (`cuaderno plumon` → 0) pero es lo que la gente espera de un buscador. Es conducta heredada de toda la búsqueda de M01 —`ProductService`, `CategoryService` y `CatalogService` usan la misma— así que cambiarla es su propio trabajo, no un arreglo suelto |
| Repaso visual de Swagger | Junto con la verificación del panel: las dos piden un navegador. **Los cuerpos de ejemplo ya no son parte de esto**: los dieciséis están puestos y probados (`zz-instalacion.spec.ts:44`) |
| **Un visitante anónimo provoca peticiones a `/api/admin/`** | Visitar la tienda **sin sesión** deja cuatro 401 en consola: la aplicación pide `/admin/auth/me` y `/admin/auth/csrf` al arrancar en **cualquier** ruta (`SessionProvider.tsx:45` y `:53`). No es un fallo de seguridad —son 401 manejados a propósito con `allowUnauthorized`— pero es trabajo inútil en cada visita pública y ensucia la consola de quien mire. Nadie lo había visto porque **ninguna prueba visitaba la tienda sin sesión**. Sin tocar: es el arranque de CORE, y no pedir sesión en rutas públicas podría perderla al navegar de la tienda al panel sin recargar. **Lo hereda cualquier módulo que añada pantallas públicas** —M02 el primero—, así que conviene decidirlo antes de que se lo saque su puerta de cero errores como si fuera suyo |
| Verificación visual del panel | Sigue pendiente: es lo único que separa a CORE de estar verificado de punta a punta |

| Tu `.env` local está desfasado | Le faltan `API_PORT` y `MEDIA_PATH`, que sí están en `.env.example`. Sin ellos, `docker compose --profile full up -d` no levanta el API |
| Borrar `docs/BITACORA-SESION-2026-08-14.md` | Cumplió su función —traspasar contexto entre sesiones— y lo durable ya está en la ADR-012 y en las entregas. Dos bitácoras confunden cuál es la bitácora |
| Tipografía y logo de SILLAR | La paleta está validada; lo demás no |
| Dominio del producto | Sin registrar |
| Nombres comerciales de las ediciones | Pendientes. No bloquean: son etiquetas de venta, no identificadores de código |
| Datos administrativos de Bsale | Certificado, costo, volumen, series y correlativos. Preguntas 7 a 10 de la guía de observación |

Aplazados por decisión, no pendientes: retención de auditoría, vectoriales en medios, permisos granulares, vencimiento de licencias, marca blanca.

### Cerrado sin resolver: cómo llegó `cms` a la base del MVP (21 ago)

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

### Defecto abierto: la auditoría enseña identificadores (18 ago)

`AuditPage.tsx:71` pinta `entry.entityId` en crudo, y desde la ADR-018 los medios —y también
las sesiones— llevan `uuid`. La columna «Entidad» acaba mostrando
`01a016da-5b2e-722b-…` a la vista, contra la regla de `CLAUDE.md` de que **los identificadores
nunca se muestran al usuario**. No es raro: **cada acceso** deja una entrada con el `uuid` de la
sesión, así que la pantalla está llena.

Está codificado como defecto conocido en `e2e/tests/transversal.spec.ts:113`, con `test.fail`:
no cuesta un rojo permanente, y **si alguien lo arregla la prueba empieza a fallar** y obliga a
venir a borrar la marca. Se prefirió eso a exentar la pantalla del recorrido, que lo habría
escondido.

**Lo que falta es la decisión de producto, no el arreglo:** la auditoría necesita identificar
la fila exacta y a la vez no puede enseñar el identificador. Las salidas plausibles son un
código corto derivado, mostrarlo solo al desplegar el detalle, o aceptar que la auditoría es
una pantalla forense y documentar la excepción. Ninguna es obvia y las tres son baratas.

### Heredado al fusionar los chats (18 ago)

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

**Bibliotecas evaluadas y descartadas** (18 ago, informes en `C:\SILLAR-DISENO\investigacion\` — **fuera del repositorio**, ver `PROTOCOLO-DISENO.md` §7). Se anotan aquí para no volver a investigarlas sin tener que abrir esa carpeta:

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

---

### Riesgo abierto: el cajón del producto tras asociar una imagen (20 ago)

**Observado una vez**, en una vuelta de la suite entera: en `recorrido.spec.ts`, pulsar «Guardar
cambios» justo después de asociar una imagen dejó el cajón abierto **y sin ningún aviso**. No se
ha vuelto a reproducir en seis intentos, ni aislado ni acompañado.

La carrera existe y se puede señalar: asociar una imagen recarga la ficha con el cajón abierto
(`ProductsPage.tsx:197` llama a `abrirFicha`), así que hay un momento en que el formulario se
está re-renderizando. **Lo que no está demostrado es que esa sea la causa** — la prueba pulsa
decenas de milisegundos después de ver la miniatura, que es algo que una persona no hace.

Lo hecho: la prueba espera a que la recarga termine, y afirma sobre los avisos de **toda la
página** y no solo del cajón, porque un fallo puede avisar por un mensaje flotante que vive
fuera. Si vuelve a pasar, dirá más que la primera vez.

Lo no hecho, y a propósito: no se ha tocado el producto. Arreglar una carrera que no se sabe
reproducir es cambiar código por una hipótesis, y quedarse sin la única señal que hay.

---

## 6. Verificación manual pendiente

Está en **`VERIFICACION-VISUAL-CORE.md`**, que es su único sitio: aquí solo se resumía qué
mirar y allí se dice cómo. Quedan **unos cinco minutos** de trabajo humano —tres juicios que un
modelo no puede afirmar— y dos de los tres se hacen sobre `e2e/screenshots/index.html` sin
levantar nada. El arnés absorbió todo lo demás el 18 de agosto: 24 pruebas, dos ejecuciones
seguidas limpias.

---

## 7. Registro

**19 ago · los tres criterios huérfanos, y lo que el armazón nunca tuvo.** 74 pruebas, dos vueltas limpias. **Nueve criterios de cierre marcados**: entran el del schema y el de la idempotencia.

**Los tres se comprueban haciéndolos**, que es la única forma que vale: el schema se desinstala de verdad —y se comprueba fila por fila que `core` sigue entero, porque las cuatro FK de M01 apuntaban a `core.media_assets`— y **se vuelve a crear**, que es la otra mitad del criterio. La idempotencia se prueba corriendo el seed dos veces y **comparando estados**, no la ausencia de excepciones. La prueba vive en `zz-instalacion.spec.ts`, con el prefijo puesto a propósito: Playwright ordena por nombre y desinstalar el catálogo antes de tiempo dejaría sin base a todas las specs de M01.

**Swagger queda a medias y se dice cuál mitad.** Se afirma que las 18 rutas están documentadas y que **ninguna operación se queda sin resumen**. Lo que no se afirma es la calidad de los ejemplos: que un cuerpo de ejemplo sea representativo es juicio, no comparación.

**Y lo que la tienda no heredó resultó que el armazón tampoco tenía.** `main` lo cazó `axe`; enumerando lo que `axe` no mira aparecieron **tres más, y faltaban en las dos mitades**: el título del documento era el mismo estático en las trece pantallas, el foco se quedaba en el menú al cambiar de ruta, y no había enlace de salto. Resueltos en sitio compartido — el título lo pone `PageContainer`, que ya recibía uno, así que el panel entero quedó cubierto sin tocar ninguna pantalla.

**El arreglo del foco tenía un fallo que solo se ve ejecutando:** con una bandera booleana de «primera vez», `StrictMode` invoca el efecto dos veces en desarrollo, la primera la consume y la segunda cree que hubo navegación — así que robaba el foco nada más cargar y rompía el recorrido con Tab desde el principio. Lo destaparon dos pruebas de otras specs, no una relectura. Se corrigió comparando **la ruta anterior**, que sobrevive a la doble invocación.


**19 ago · M01 entrega 04E: la tienda pública.** Las tres rutas —`/catalogo`, `/catalogo/:categoria`, `/producto/:slug`— con 65 pruebas de extremo a extremo en dos proyectos, dos vueltas limpias. **Suben a siete los criterios de cierre marcados**: entran el del cono y el de la búsqueda, que cierra la mitad que le faltaba al de `ARTESCO`.

**Estrena los tres componentes de 04D-bis, y encajaron los tres.** `FilterChip` para los filtros envueltos, `NoResults` para «no hay resultados para *tornillo*» —sin acción principal, porque el arreglo ya está en pantalla— y la nota de `EmptyState` para la categoría vacía. Ninguno pidió cambios al montarlo, que era la pregunta.

**Un hallazgo real de accesibilidad:** las páginas públicas **no tenían landmark `main`**. El panel sí lo tiene desde el armazón; la tienda nace fuera de él y nadie lo había puesto. Lo cazó `axe-core` con `landmark-one-main` al montar la primera pantalla, no una revisión.

**Tres frases que salen del dato y no de una cadena escrita a mano**, que es lo que evita que mientan al cambiar los datos: la nota del precio de las opciones —«todas cuestan lo mismo» solo si de verdad coinciden—, la miga que cae a otra categoría activa cuando la principal se desactiva, y la versión larga de la miga, que **solo aparece si hay origen y no coincide** con lo que se muestra.

**Y dos fallos míos en las pruebas, del mismo tipo que ya tienen regla:** conté `.ti-nophoto` sin esperar a que la rejilla cargara —medir el hueco—, y la prueba del precio se fiaba de que sus productos estuvieran en la primera página, cosa que dejó de ser cierta en cuanto otra spec llenó el catálogo. Las dos pasaban solas y fallaban en la suite completa.


**19 ago · M01 entrega 04C: productos.** 54 pruebas de extremo a extremo en dos proyectos, dos ejecuciones seguidas limpias. **Cinco criterios de cierre del SPEC quedan marcados**, cada uno con la prueba que lo respalda; los otros doce siguen abiertos y se dice por qué.

**La regla que gobierna la pantalla, y dónde vive.** «La variante es invisible mientras haya una sola» se resuelve **en la capa de servicios**, no en el formulario. Al crear, el API ya lo tenía pensado: `code` y `barcode` viajan como campos del producto y el servidor los pone en la variante que crea solo. Al **editar** no —el contrato del producto no los lleva— así que hay dos peticiones. Esa costura la absorbe `productsService.update`, y si la absorbiera la pantalla, **cada pantalla futura tendría que volver a saber que existe una variante**.

**La forma admite 04D sin rehacerse.** Los tres campos de la variante única viven juntos en su propio bloque y el formulario ya distingue una variante de varias: con más de una enseña una frase y no los toca. Cuando llegue la tabla, sustituye ese bloque y nada de alrededor se mueve.

**Lo que arrastraba el sistema de diseño, aplicado:** la altura de los controles pasa a ser **token declarado** (`--alto-control-sm/md/lg`) en vez de aritmética de relleno y tipo — un botón medía 38,5 y un campo 46 por un `line-height` que solo tenía uno de los dos. Y `Pagination` deja de fijar `sm` por dentro: la navegación del catálogo estaba en botones de 26,8 px, imposibles de acertar en móvil.

**La regla que lo explica, y que sola habría cazado la paginación:** *`sm` vale para un control repetido en fila densa con ratón, **y solo cuando existe otra manera de hacer lo mismo**.* «Siguiente» no cumple ninguna de las dos: no hay segunda vía para llegar a la página 2.

**Y la prueba de la altura no afirma un número mágico**, afirma la propiedad: que el campo y el botón de la misma fila midan lo mismo. Falló a la primera —39 contra 36— porque el relleno del campo se comía el mínimo, que era exactamente el desajuste que el token venía a quitar.


**18 ago · M01 entrega 04B: categorías, y el detector de traducción.** 39 pruebas de extremo a extremo, dos ejecuciones seguidas limpias. 221 de backend.

**Lo primero fue contar, no arreglar.** `brands` llevaba días devolviendo 500 y `categories` tenía el mismo defecto: dos casos y ninguna idea de cuántos más había. La respuesta es **27 endpoints en M01, 26 sanos, 1 roto** — el ya conocido. No había una tercera sorpresa.

De ahí sale **`e2e/tests/api-traduccion.spec.ts`**, que no es una suite: llama a cada endpoint una vez y afirma que no devuelve 500. Cubre el punto ciego exacto de «las pruebas de lógica no tocan la base» — **lo que solo se rompe cuando EF traduce a SQL es invisible para ellas**. Dos detalles que lo hacen valer:

- **Llama a los listados con sus filtros puestos**, no pelados. `category`, `brand`, `q`, `page`, `pageSize`: el `.Where(métodoDeInstancia)` se cuela justo ahí, que es la familia que motivó el detector. Sin parámetros, esas ramas no se ejecutarían nunca y el verde sería falso.
- **Una segunda prueba afirma que la lista tiene 27 rutas distintas**, para que no envejezca en silencio cuando alguien añada un endpoint y no lo vigile.

Escribiéndolo apareció otro despiste del mismo tipo: `lookup` recibe `codigo`, no `code`, así que la primera versión caía en la guarda de vacío sin ejecutar la consulta. Un detector mal parametrizado da el mismo verde que uno bueno.

**Categorías es el segundo caso real, y por eso aquí se extrae.** `services/media.ts` y `LogoPicker` salieron de M01 a `src/shared/media/` como `gallery.ts` e `ImagePicker`. Se miraron de cerca antes: la única diferencia era la etiqueta del campo —«Logotipo» contra «Imagen»—, que vive en el formulario. Eran la misma cosa.

**Dónde categorías se aparta de marcas, a propósito:** aquí **sí** se cuenta. La regla 9 pide avisar cuántos productos se quedan sin la categoría, y en marcas se decidió no contar. No es incoherencia: el SPEC pide una cosa en un sitio y la contraria en el otro, y las dos tienen su motivo escrito.

**Confirmado que la baja NO promueve otra categoría como principal** (`CategoryService.DeactivateAsync` solo cambia `IsActive`; nada toca `Product.PrimaryCategoryId` en `Product.cs:52`, y nadie escucha `CategoriaDesactivada`). Design había propuesto promover a la siguiente: contradice la regla 9 y, sobre todo, cambiaría la miga de pan de N productos en silencio — lo mismo que la regla 3 prohíbe para el slug, y por el mismo motivo.

**Dos correcciones que salieron de revisar lo de 04A:** `useDelayedFlag` no garantizaba permanencia mínima, así que una respuesta de 1010 ms enseñaba el indicador 10 ms — el umbral solo movía el parpadeo a la ventana siguiente. Y al añadirla **bajé el umbral a 200 ms sin darme cuenta de que la ENTREGA-04A §4 dice «por debajo de un segundo, nada»**; lo destapó la prueba que ya cubría esa conducta, no una relectura.

**18 ago · M01 paso 4, entrega 04A: marcas.** Primer módulo aparte de CORE que se monta a sí mismo en el frontend. 7 pruebas nuevas en `e2e/`, 31 en total, dos ejecuciones seguidas limpias.

**Lo que importa no es la pantalla, son los cuatro defectos que destapó**, y tres de ellos llevaban semanas ahí:

- **`GET /api/admin/catalog/brands` devolvía 500 desde el paso 3.** `BrandService.ListAsync` proyectaba con `.Select(Project)`, un método de **instancia**, y EF Core aborta la consulta. Nunca se vio porque **las pruebas del módulo son de lógica pura y no tocan la base**: la primera pantalla que llamó al endpoint lo encontró en el primer intento. La lección ya estaba escrita —«verificar el efecto observable»— y esta vez el efecto observable era *que la pantalla cargue*.
- **`CategoryService.cs:147` tiene el mismo defecto, idéntico.** No se tocó: categorías está fuera del alcance de esta entrega y nada de ella depende del endpoint. Se arregla con su entrega, y con su prueba.
- **La fila atenuada de la tabla diluía el texto hasta 2.14:1.** `opacity: .55` sobre `tr[data-dimmed]`. **Es el mismo fallo que ya obligó a quitar el `opacity` de `.mod.is-off`**, cometido en otro componente. Afectaba también a usuarios y medios; nadie lo había visto porque ninguna prueba había atenuado una fila con `axe` mirando. Ahora la fila se marca con un borde y el estado lo dice la insignia, que lleva texto.
- **El `<header>` del Drawer era un segundo landmark `banner`.** Un `<header>` que no cuelga de `article`/`aside`/`main`/`nav`/`section` se mapea a `banner`, y el armazón ya tiene el suyo. Afectaba a cualquier panel lateral, incluido el de usuarios de CORE.

**Un cambio de mensaje decidido con JP**, no incidental: el 409 de marca repetida distinguía nombre de slug, pero **repetía la grafía que la persona acababa de teclear**. Ahora consulta la que ya existe y las enseña las dos: *«Ya existe una marca llamada «Faber-Castell». Los nombres no distinguen mayúsculas, así que «FABER-CASTELL» y «Faber-Castell» son la misma marca.»* Sin ver la otra, no se entiende qué choca — en pantalla no hay dos nombres iguales.

**Lo que faltaba en `src/shared/`: `useDelayedFlag`.** El indicador de carga no debe aparecer por debajo del segundo. Un spinner que entra y sale hace que una respuesta rápida se perciba como lenta, y además mueve el contenido dos veces.

**18 ago · el arnés cierra la verificación de CORE, y lo que encontró al escribirlo.** De 6 pruebas a 24. La guía manual baja de 30–40 minutos a unos cinco, y CORE queda **verificado de punta a punta** — el pendiente más viejo del proyecto, abierto desde el 14 de agosto.

Tres defectos reales, ninguno visible sin ejecutar:

- **El `<input type="file">` de la galería no tenía etiqueta.** Impacto **crítico** en axe-core. Al ser `sr-only` está oculto a la vista pero **no al lector de pantalla**, así que quien navega con uno llegaba a un campo mudo. Una línea: `aria-label`.
- **`--danger` de noche rompía el botón de peligro.** Se lo di como color de *texto* al arreglar el contraste en oscuro, pero `.ui-button--danger` lo usa de *fondo* bajo texto blanco: 2.68:1. **Es el mismo error de doble uso que ya había obligado a partir `--primary` en `--link`, cometido otra vez tres días después.** De ahí sale `--on-danger`, y la regla general: antes de dar tono oscuro a un semántico, mirar si alguien lo usa de fondo.
- **La auditoría enseña identificadores.** Ver §5: es el único que queda abierto, porque lo que falta es una decisión de producto, no un arreglo.

Y un cuarto que era del propio arnés y es el que más conviene recordar: **tres pruebas transversales estaban pasando sin mirar nada.** Esperaban a que `main` fuera visible, que se cumple con el spinner todavía puesto y la tabla vacía, y escaneaban el hueco. Solo se notó porque la de auditoría pasaba unas veces sí y otras no — si hubiera sido consistentemente verde, habría seguido mintiendo. **Una prueba intermitente no es un incordio: es la única señal de que otra prueba estable puede estar vacía.** Corregido esperando a `networkidle` y a que no quede spinner.

Dos decisiones de forma que conviene imitar. **`test.fail` para el defecto conocido**, en vez de exentar la pantalla del recorrido: exentarla lo habría escondido, y así queda escrito en código, sin rojo permanente, y avisa si alguien lo arregla. Y **los ficheros de apoyo de medios no existen**: se pasan como buffers en memoria a `setInputFiles`, así que el binario de 5 MB no entra al historial de git ni hay que limpiar nada del disco.

**18 ago · se deshace el trabajo en dos chats, dos días después de montarlo.** Lo que importa no es que se fusionaran, es **por qué se probó y por qué no funcionó**, que es lo que evita repetirlo dentro de tres módulos.

**Por qué se montó, y sigue siendo una razón buena.** Dos chats daban un **segundo lector**. No era jerarquía: una decisión de datos tomada mirando una pantalla concreta optimiza para esa pantalla, y el módulo tiene que servir a dos productos y a clientes que no existen todavía. Quien validaba veía lo que desde dentro no se ve, porque no había estado en la conversación donde se decidió.

**Por qué se deshizo: el cartero era el cuello de botella.** No fue una molestia de proceso, produjo daño medible:

- **Dos lecturas incompatibles del mismo `Dockerfile`**, sobre qué excluye `.dockerignore` en los módulos de demostración. Ninguno de los dos podía preguntarle al otro: solo escribirle a JP y esperar. De ahí salió la regla que sí se conserva —**toda afirmación sobre código cita archivo y línea**— pero el problema de fondo era estructural, no de rigor.
- **Mensajes truncados dos veces** al copiar bloques largos entre chats. Trabajo perdido sin que nadie se enterara en el momento, que es la peor forma de perderlo.

**Por qué la mensajería directa entre agentes no lo arregla**, aunque exista y sea tentadora. Dos razones, y la segunda es la que decide: los agentes **viven dentro de la sesión que los creó y mueren con ella**; y sobre todo, **SILLAR no es dos equipos paralelos, es uno en serie con validación** — se decide, se reporta, se valida reversibilidad, pasa el testigo. La mensajería directa resuelve «dos equipos que no se pisan». Aquí el problema era el contrario: no sobraba coordinación, faltaba inmediatez en una cadena que ya era secuencial.

> **El criterio para la próxima vez.** Partir el trabajo en dos cabezas paga cuando hay **dos flujos que avanzan a la vez y se estorban**. No paga cuando hay uno solo que avanza por pasos: ahí cada frontera entre cabezas es una espera, y las esperas se pagan en mensajes copiados a mano.

**Lo que se pierde y no se sustituye solo:** ese segundo lector. Queda en su lugar algo más frágil —el mismo agente revisándose— y por eso el `REVERSIBLE No` pasa a ser lo único que protege, se escribe **primero**, y la pregunta del validador hay que hacérsela en voz alta: *¿esto lo necesita el módulo, o lo necesita esta pantalla?* Está en `CLAUDE.md`, «El lector que ya no está». Conviene recordar que los tres casos de los que este proyecto se salvó por poco —colación, claves primarias, nivel de variante— los cazó alguien mirando desde fuera de donde se decidió.

**Seis pendientes heredados** quedaron sin registrar porque Frontend no escribía en esta bitácora. Están en la §5.

**14 ago · F-08 y entrega 4a.** Decidida la identidad del panel (`MARCA.md` §6) y la regla de que `installation_key` no sale del servidor — se detectó que la fase 5 tendería a meterla en el archivo de licencia, poniendo la clave del CSRF en manos del cliente.

De Claude Code se aprobaron: el estado de reconexión fuera de React, la recarga completa al reconectar, las tres barreras de `Sillar.Modules.Demo` —dos de compilación— y `describe()` reutilizando la frase del servidor.

**Consecuencias anotadas:**

- La recarga completa pierde un formulario a medias en otra pestaña. Cuando haya formularios largos, avisar antes de recargar.
- `describe()` convirtió los mensajes del API en texto de interfaz. Y mostró un código donde 4a pedía nombre y enlace: por eso el 409 pasa a llevar `blockedBy` estructurado, corregido en el §0 de 4b antes de que el patrón se repita.

**15 ago · corregida la topología del ERP (ADR-017).** Internet pasa a ser casi obligatorio y el trabajo sin conexión queda como modo degradado. Con eso cae el modelo de satélites con proyección parcial y entra **mando y copia**.

La aportación que hay que conservar: **«respaldo» estaba nombrando dos cosas distintas** —la réplica en caliente, que protege contra «el mando no responde ahora», y el volcado, que protege contra «el mando se perdió»— y ninguna sustituye a la otra. Y una segunda: **la copia de la web no rescata al mostrador**, porque si el mostrador pierde internet tampoco alcanza la nube. Al mostrador lo rescata su base local. Son dos protecciones para dos caídas distintas.

Segunda vez que una premisa sobre el ERP resulta falsa —la ADR-013 murió por lo mismo—. Por eso la 017 deja lo abierto marcado como abierto en vez de completarlo por simetría.

**Añadido el conteo global sobre las existencias por sucursal**, y con él **M17 Sucursales** (`branches`). Suma visible de todos los locales, para que cuando aquí se acabe se sepa que la solución está en otro sitio.

Yo había propuesto arrastrar la antigüedad de cada réplica y reservar en cada consulta. JP corrigió las dos, y las dos correcciones son mejores:

- **Solo se suma lo conectado**, con un aviso que dice qué falta. Más simple, y sobre todo **el error va siempre hacia abajo**: el sistema puede quedarse corto, nunca prometer de más. Es el criterio de fallar ruidosamente aplicado a un número.
- **Preguntar no es comprar.** La mayoría de quien pregunta no lleva. Reservar en cada consulta llenaría los otros locales de apartados fantasma. La consulta es libre; separar y trasladar nacen de la intención del comprador y son documentos explícitos.

Y quedó resuelto lo que estaba abierto: el concepto de sucursal **no va en CORE ni en M09, va en su propio módulo**. La formulación que lo hace desmontable: *no aporta «sucursal», aporta «más de una»*. La dependencia va de M17 a M09 y nunca al revés — un negocio de un solo local no debe ver la palabra en ninguna pantalla.

**18 ago · el arnés `e2e/` en pie, y lo que encontró en su primera vuelta.** Playwright con puerta de cero errores de consola y `axe-core` en los dos temas. Seis pruebas verdes, dos ejecuciones seguidas limpias.

**Lo que justifica el esfuerzo entero está en lo que encontró al primer intento**, y son defectos reales que nadie podía haber visto antes: el tema oscuro **nunca había sido alcanzable**, así que su contraste jamás se había podido comprobar. Salieron cuatro:

- `.mod.is-off` bajaba la opacidad al 62% y con eso el texto caía por debajo de 4.5:1.
- `--success` y `--warning` estaban justos en claro y **rotos en oscuro**: solo sus variantes de fondo tenían anulación oscura, el color de primer plano no.
- `--primary` se usaba a la vez como fondo de botón y como color de enlace en texto — 8.4:1 en un sitio, **1.88:1** en el otro. Se partió en un rol nuevo, `--link`.
- Un salto de jerarquía de encabezados, `h3` bajo `h1`.

Dos decisiones suyas que conviene imitar. **`GET /api/setup/status` pasa a responder siempre**, en vez de filtrar su 404 en la prueba: ese 404 no era ruido del arnés, es lo que recibe el navegador de cualquier usuario en cada carga de página. Filtrarlo en el test habría escondido un problema real. Y **`duringExpectedOutage()`** como única válvula al cero-errores, en lugar de relajar la regla: un reinicio real y un 409 provocado no se pueden observar sin que el navegador anuncie el fallo de red. Es física, no descuido.

**Consecuencia que hay que atender antes de construir nada:** los tokens cambiaron **después** de que Claude Design produjera las pantallas de M01. Hay que resincronizar el sistema de diseño antes de implementarlas.

**16 ago · paso 3 de M01 cerrado.** Contrato, seis eventos, API pública y de administración, todo con CSRF y auditoría, barrera en `Editor`. 221 pruebas, 0 advertencias.

Dos piezas de CORE salieron a `Sillar.Core.Contracts` porque M01 fue **su segundo caso real**: `IAuditWriter` con sus tipos y `AdminRole`, más el filtro de CSRF. Una de ellas llevaba el comentario que lo predecía —«se mueve al contrato en cuanto M01 lo necesite»—, que es la regla del segundo caso funcionando como debe: se anota dónde va a hacer falta y se mueve cuando pasa, no antes.

La lógica pura vive aparte y se prueba sin base: generación de slug, detección de ciclos, precio efectivo, el mensaje de la regla 8 y la miga de pan con su respaldo.

**Verificado en vivo contra imagen reconstruida y base limpia**, no solo con pruebas unitarias: el caso del restaurante, el del plumón, el del cuaderno, el del cono, la unicidad de la regla 4 con mensajes por restricción, la 6, la 8 bloqueando y desbloqueando, la 9 contando sin cascada, el respaldo de la miga, `lapiz` → `LÁPIZ` por el endpoint real, el tope de `pageSize`, la colisión de marca por mayúsculas, el 403 sin CSRF y el rechazo de un medio inexistente.

**16 ago · paso 2 de M01 cerrado, y la ADR-018 aplicada.** `core.media_assets` replica con clave `uuid` v7 acuñada una sola vez en `MediaStorage.SaveAsync` —el mismo valor en la fila y en el disco—, las cuatro FK del catálogo pasan a `Guid`, y las migraciones de CORE se refundieron en una sola, que es lo que la ADR-018 autorizaba por no haber instalación desplegada. Las 181 pruebas siguen en verde (132 + 49).

Dos aciertos de Claude Code que conviene imitar:

- **`TryAddNodeIdentity` en `Sillar.Shared.Replication`**, creado solo cuando CORE se convirtió en el segundo módulo que lo necesitaba. Es la regla del segundo caso real aplicada bien: no existía cuando solo lo usaba el catálogo.
- **Verificó ejecutando, no aplicando.** Base desde cero, migraciones, e `INSERT` reales dentro de una transacción revertida para comprobar que el `CHECK` del slug rechaza `Faber-Castell` y acepta `artesco`. «La migración se aplicó sin error» no demuestra que una restricción haga lo que dice: hay que intentar violarla.

**15 ago · paso 2 de M01 al 80%, y el error que destapó (ADR-018).** El proyecto, las seis entidades, las seis configuraciones y la migración inicial están escritos y compilan sin advertencias. Faltan `02_seed.sql`, `99_drop.sql`, diccionario y ER.

Al revisar la migración apareció que las cuatro FK hacia `core.media_assets` eran `integer` — correctas según la ADR-016 y **rotas** según la ADR-017: el catálogo se replica y los medios no, así que el catálogo llegaría a la web sin fotos y sin error. **El fallo era de mi tabla de clasificación**, no de la implementación. De ahí sale la regla que faltaba, ya en la §3.

Se resolvió con algo que ya existía sin verse: los nombres de archivo que genera CORE **ya son `uuid` v7**, así que la clave entera de la fila era un segundo identificador para lo mismo. Ahora son el mismo valor.

Dos consecuencias anotadas: **M16 tendrá que mover archivos además de filas** —una fila de medios sin sus bytes es una imagen rota, peor que una ausente—, y **`core.admin_users` cae bajo la misma regla** en cuanto las ventas registren quién vendió. Se decide antes de M13, no en M16.

De Claude Code se aprueba todo, y una aportación que no era mía: **PostgreSQL no admite expresiones regulares sobre colaciones no deterministas**. El `CHECK` del formato del slug se crea sin protestar y después ningún `INSERT` funciona. Resuelto con `COLLATE "C"` dentro del propio `CHECK`. Eso se descubre ejecutando, no leyendo.

**15 ago · revisión del repositorio completo.** Higiene correcta: `.env` ignorado, `/media/` ignorado y anclado a la raíz, nada sensible rastreado, ningún archivo de cliente. Se corrigieron tres desfases reales:

- El `CLAUDE.md` había **perdido dos reglas del frontend** —prohibición de `localStorage`/`sessionStorage` y colores solo desde `tokens.css`— que estaban decididas en F-08 y no llegaron al archivo. Recuperadas, junto con la de «ningún error genérico, ningún botón Aceptar».
- El catálogo de módulos y el roadmap **no conocían el ERP**: faltaban M13 a M17 y M09 seguía en fase 4.
- Aplicada la regla de claves `uuid` v7 de la ADR-016, que es lo que bloqueaba la primera migración de M01.

**15 ago · SPEC de M01, y las dos decisiones estructurales que trajo.** Yo había propuesto una sola categoría por producto y variantes fuera de alcance. Los dos casos que dio JP tumbaron las dos:

- **Conos = deporte y juguete; calculadora = tecnología y curso de matemáticas.** Segundo y tercer caso real, así que **N:M entra ahora**, con categoría principal para la ruta y la miga de pan. Anotado para no forzarlo después: «curso de matemáticas» no es una categoría del mismo tipo que «deporte», y una lista de útiles por grado y colegio **no es una categoría** sino un conjunto con dueño y vigencia — va a M07.
- **Plumón verde y azul: todo igual salvo el código.** JP propuso un producto con varios códigos y característica elegida en la venta. Correcto en la intención, incompleto en un punto: **con un solo nivel, M09 cuenta un número y la tienda con 3 verdes y 0 azules diría «hay 3»** — justo lo que la ADR-017 prohíbe. Se resuelve con `product_items`: el producto guarda lo compartido, la variante guarda lo que varía. Nada se duplica.

La pregunta que separa las dos cosas y que hay que reusar: **¿puedo quedarme sin esta variante teniendo las otras?** Sí → variante, con código y existencia propios. No → característica de venta, un texto en la línea, y eso sí se aplaza a M13 porque no cambia ninguna clave.

Se aplica el mismo patrón que M17, y conviene que se note: **la tabla de variantes no aporta «variante», aporta «más de una»**. Todo producto nace con una, sin nombre e invisible; la palabra no aparece en pantalla hasta que existe la segunda.

**Entrega 4b cerrada, y con ella CORE.** Un solo patrón nuevo, la galería. Claude Code encontró al verificar que la marca de «cuenta eliminada» en auditoría se apoyaba en `admin_user_id` nulo, que también es lo que llega en un acceso fallido con correo inexistente: la auditoría habría acusado de cuenta borrada a quien solo escribió mal su correo. Corregido.

**20 ago · 04D cerrada, y el paso 4 con ella.** La tabla de presentaciones, el control N:M de
categorías y el precio de la tarjeta. Catorce de diecisiete criterios del SPEC marcados; los
tres que quedan son de cierre —la asociación de imágenes, los ejemplos de Swagger y el repaso
de móvil y teclado.

Lo que encontró la entrega no estaba en su alcance, y es lo que más valió:

- **El armazón llevaba roto en móvil desde que existe.** Una media query escrita antes de su
  regla base no aplica: el CSS declaraba esconder la barra lateral a 860 px y no escondía nada.
  Lo cazó la comprobación de 390 px de la tabla de presentaciones, que no iba a eso. Y el
  arreglo no fue aplicar lo que el CSS decía, porque **no hay menú en la barra superior**:
  esconderla habría dejado el panel sin ninguna forma de navegar. Pasa a fila envuelta.
- **`Button` era `type="submit"` por defecto**, que es el defecto del HTML y nadie lo había
  declarado. «Añadir presentación», dentro del `<form>`, guardaba el producto entero: el
  síntoma era un producto chocando consigo mismo por su propio código.
- **La galería devolvía filas de archivos dados de baja con la `url` vacía.** Un `?? ""` que
  parecía defensivo y pintaba un hueco.

Los tres tienen la misma forma: **algo que estaba escrito y no se ejecutaba nunca**. Es la
misma familia que el `GET /brands` con 500 desde el 16, y la razón de que el detector de
traducción exista. Lo que no se ejecuta no está probado, aunque esté escrito con cuidado.

**20 ago · el MVP, y lo que encontró recorrerlo entero.** Dos días para enseñar SILLAR
funcionando. Nada de módulos nuevos: CORE + M01, que es lo que hay.

Lo que faltaba no era una pantalla, era **la costura entre pantallas**, y solo apareció al hacer
el camino seguido:

- **En la ficha pública, las presentaciones no se podían elegir.** Eran una lista de solo
  lectura, y el precio grande era el de la primera: tres importes distintos debajo y un número
  arriba que no se movía. La entrega 04E daba por hecho que «el precio grande sigue a la
  selección» y **no había selección**. Ahora son botones de radio de verdad —flechas, estado
  leído en voz alta y foco visible sin escribir ninguna de las tres cosas— y el importe la
  sigue.
- **Ninguna prueba lo cazaba, y ninguna estaba mal.** `tienda.spec.ts` afirmaba que el bloque se
  titula con lo que varía y que cada opción muestra su importe, que es lo que 04E pedía. Nadie
  había escrito «y se puede elegir una», porque nadie lo había intentado. **Un recorrido no es
  la suma de sus pantallas**: `e2e/tests/recorrido.spec.ts` existe para eso, y es la única
  prueba de la suite que vale por lo que enlaza y no por lo que aísla.

Y una decisión sobre los datos: **el catálogo de demostración no va en el seed del módulo.**
`database/modules/catalog/02_seed.sql` está vacío a propósito (SPEC §6.9) y tiene que seguir
estándolo — un producto de ejemplo dentro del seed acaba instalado en casa de un cliente. Va en
`scripts/demo/seed-demo.mjs`, que siembra **por API**: pasa por la validación, por el CSRF, por
la generación del slug y por la comprobación de contenido de las imágenes, así que además
verifica el camino en vez de esquivarlo. Las veinte imágenes **se generan en memoria**, que es
la misma regla que ya seguía el arnés: un binario commiteado por una demostración entra al
historial para siempre.

**20 ago · M01 Catálogo terminado.** Los diecisiete criterios del SPEC, cerrados con su prueba.
Es el primer módulo del árbol comercial que se cierra entero.

**M01 Catálogo queda terminado — se instala y se desinstala sin romper nada del resto del
sistema.**

Los tres que faltaban, y lo que costó cada uno:

- **La asociación de imágenes.** Faltaba la mitad contraria: que quitar la asociación **no**
  borre el archivo. `imagenes-asociadas.spec.ts` lo prueba con dos productos sobre la misma foto,
  que es la condición en que el defecto dolería — el segundo se quedaría sin imagen sin que nadie
  tocara nada suyo.
- **Los ejemplos de Swagger.** No había **ninguno**: 39 rutas, 16 cuerpos, cero ejemplos, y el
  generador rellenando cada campo con `"string"`. Swashbuckle **no lee `<example>` de un `record`
  posicional** —comprobado poniéndolo y viendo que seguía en cero—, así que hizo falta un filtro.
  Y una decisión: **el módulo declara datos (`ISchemaExamples`), Api construye el filtro.** Un
  módulo de dominio no tiene por qué conocer Swashbuckle, y si mañana se cambia de generador no
  se toca ningún módulo. Api los recoge por reflexión, sin nombrar a nadie, igual que ya hacía
  con los XML.
- **El repaso de móvil y teclado.** Encontró dos defectos reales que no eran de ninguna pantalla:
  **la portada pública no tenía `<main>`** —así que el enlace de salto apuntaba a un
  `#contenido` inexistente— y **`main` solo recibía `tabindex` cuando `RouteFocus` actuaba**, que
  a propósito no actúa en la carga inicial: el salto no funcionaba justamente la primera vez, que
  es cuando se usa. Ahora el `tabindex` está en el marcado.

**Y un hallazgo que no es de M01, anotado para que no se pierda:** visitar la tienda **sin
sesión** deja cuatro 401 en la consola. La aplicación pide `/admin/auth/me` y `/admin/auth/csrf`
al arrancar en cualquier ruta (`SessionProvider.tsx:45` y `:53`), los maneja a propósito con
`allowUnauthorized`, y no se ve nada roto. Nadie lo había visto porque **ninguna prueba visitaba
la tienda sin sesión**. Un visitante anónimo no debería provocar peticiones a `/api/admin/`, pero
quitarlas es tocar el arranque, que es de CORE.

Sobre el contrato: **`ICatalogService` sigue sin un solo consumidor real.** Mirado con ojos de
M02, no le falta nada para el mostrador ni para el inventario, y le faltan dos cosas para
contenido — el `slug`, que es lo único con lo que se enlaza, y la imagen. **No se han añadido**:
la regla del segundo caso sigue en pie y M02 va a ser el primer caso. Queda escrito en
`docs/modules/catalog/README.md` §2 para que se decida con él delante.
