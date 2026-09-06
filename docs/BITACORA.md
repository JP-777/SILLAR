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
- **Una aserción de ausencia necesita un ancla de su misma carga, no del armazón.** Envolver
  `goto` para esperar al armazón cierra la clase «página en blanco», pero **no la de contenido que
  llega por petición**: entre el armazón pintado y la respuesta del servidor sigue habiendo una
  ventana, más estrecha y por eso peor —falla una de cada veinte corridas y se acaba llamando
  prueba inestable. Censadas 73 aserciones de ausencia: **49 tienen ancla positiva a menos de seis
  líneas**; de las 24 restantes, la mayoría son un cajón que la prueba acaba de rellenar —anclado
  por la propia interacción— y **media docena son las de verdad**, sobre contenido asíncrono.
- **Una ruta absoluta en un documento no falla en otra máquina: no encuentra nada.** Y no
  encontrar nada se parece demasiado a que no haya nada. El diagrama de `PROTOCOLO-DISENO.md`
  llevaba `C:\` dentro, cuando lo que fija es **la relación** —dos carpetas hermanas—, no dónde
  viven. Es la misma familia que el `.env` que carga en silencio: **algo que solo funciona aquí y
  no lo dice**.
- **Un clasificador automático da falsos positivos en las dos direcciones, y hay que decirlo en
  las dos.** El mío marcó 24 aserciones sin ancla; al leerlas, la mayoría eran cajones anclados
  por la propia interacción y una —`configuracion:73`— estaba anclada con `toBeEnabled()`, que
  mi expresión no reconocía. Es fácil corregir el clasificador cuando exagera el trabajo; hay que
  corregirlo igual cuando lo esconde.
- **Un documento desactualizado no es neutral: fabrica errores.** El `ROADMAP_MODULAR.md` decía
  que la búsqueda de M01 iba con `pg_trgm` y `unaccent`. Es falso —va por `to_tsvector('spanish')`
  sobre índice GIN, porque el nombre lleva colación no determinista y PostgreSQL no admite esas
  operaciones sobre ella— y la cadena completa es la que importa: **estaba escrito, alguien lo
  leyó, lo repitió en un encargo, y hubo que corregir el mecanismo antes de implementar el
  selector de productos.** El argumento de que «ese documento no lo lee nadie» es falso por
  construcción: si de verdad no lo leyera nadie, no habría llegado a un encargo.
- **El momento que tiene tiempo no tiene navegador; el momento que tiene navegador no tiene
  tiempo.** Salió al buscar dónde vivía una animación de marca, y vale para cualquier cosa que
  quiera acompañar una espera. Medido:

  | Momento | Tiempo | Quién lo ve | Fases nombrables |
  |---|---|---|---|
  | Inicio de sesión | **903 ms** | Todo el mundo, cada día | Por debajo del umbral: no debe verse nada |
  | Reinicio por activación de módulo | 10–90 s | Casi nadie, y solo quien administra | El navegador no puede preguntar: no hay servidor con quien hablar |
  | Instalación | **17,2 s** | **Nadie: todavía no hay navegador** | Reales, pero ocurren antes de que exista interfaz |

  La instalación parecía la buena hasta que se lee cómo ocurre: **las migraciones crean la base que
  la API necesita para arrancar, y la API es quien serviría la pantalla** (`ModuleBootstrapper.cs:138`
  aborta sin base). Huevo y gallina. Moverlas dentro del arranque contradiría la ADR-009.
- **Un arreglo que parece correcto también hay que mirarlo por fuera.** No basta con que la causa
  encontrada fuera real: hay que comprobar que **lo que sale ahora** es lo que se quería. El
  arreglo del 401 tenía la causa bien —«quién soy» pedía autorización— y seguía sin funcionar,
  porque `Results.Ok(null)` y `Results.Json(null)` **no llegan a escribir el `null`**: los dos
  mandan `Content-Length: 0`, el cliente lo recibía como `undefined` y seguía pidiendo el token
  CSRF. Se vio con `curl -i`, no leyendo el código. **La firma dice qué devuelve el método; la
  cabecera dice qué recibe quien lo lee.**
- **No hay ancla para «no hay error»: lo que se puede esperar es el éxito.** Es la salida general
  de toda la familia de aserciones vacías. Una negación no tiene a qué agarrarse —cualquier
  instante antes de que la cosa aparezca la cumple— mientras que **el desenlace bueno sí se puede
  esperar**, y su fallo trae el motivo. Donde se quiera afirmar que algo no salió mal, se afirma
  que salió bien.
- **Lo que instalas para ver tampoco se mira solo: un diagnóstico se verifica.** Es la regla de
  «una corrección se verifica con el mismo rigor que el fallo que corrige», aplicada al
  instrumento. La línea que se puso para explicar por qué un cajón no se cerraba **tenía el mismo
  defecto que investigaba**, así que las dos veces que ocurrió la prueba murió sin decir nada.
- **Tres válvulas por la misma causa dejan de ser excepción y son una zona ciega.**
  `duringExpectedOutage` es la única forma de descontar errores de consola, y llegó a usarse en
  tres sitios por los mismos cuatro 401 — o sea **tres pruebas escritas para tolerar una
  regresión de esa área**. El criterio de terminado no era que el 401 desapareciera: era que las
  tres válvulas se pudieran quitar. Se quitaron.
- **La aserción de diagnóstico también puede ser vacua, y entonces el fallo se queda mudo.** Puse
  `expect(getByRole('alert')).toHaveCount(0)` antes de `expect(ficha).toBeHidden()` justamente
  para saber *por qué* un cajón no se cerraba. Corría antes de que el aviso pudiera renderizarse,
  así que pasaba siempre, y las dos veces que el cajón se quedó abierto la prueba murió en la
  línea siguiente sin decir nada. **Se pregunta por el desenlace y se exige el bueno**, en vez de
  negar el malo: así el mensaje trae el motivo.
- **`toBeHidden()` es vacuo igual que los demás, y peor porque parece que espera.** Medido, no
  supuesto: sobre una página en blanco, `toBeHidden()` **pasa** con un selector que no ha
  existido nunca — se cumple con el elemento ausente del DOM. Los dos creíamos lo contrario esta
  mañana. Va con las demás aserciones de ausencia, sin excepción.
- **Un `tbody` vacío no tiene cero filas.** `Table` pinta el estado vacío como una fila más
  (`patterns.tsx:279`), así que `tbody tr` da **1** con la tabla vacía. Tres pruebas exigían
  `toBe(0)` y pasaban: contaban antes de que la página pintara. **No habrían pasado nunca sobre
  una tabla pintada, ni vacía ni llena** — una aserción imposible que vivía de la vacuidad.
- **Una aserción de ausencia se cumple sola en una página que aún no ha pintado.** «Con M01
  desactivado el inicio no deja hueco» llevaba semanas en verde **y no afirmaba nada**: `goto()`
  vuelve antes que React, y tanto `toHaveCount(0)` como `not.toContainText` pasan sobre un `body`
  vacío. Se descubrió por accidente al romper una costura a propósito: la prueba seguía **verde
  con el archivo entero y roja en solitario**, y la diferencia era cuánto tardaba la página, no
  lo que enseñaba. **Antes de afirmar que algo no está, hay que esperar a que esté lo que sí
  debe estar.** Es la tercera cara de la misma regla: `main` visible no es pantalla cargada.
- **Playwright transpila sin comprobar tipos.** Un error de tipos en una spec **no sale por
  ningún lado**: la prueba corre igual. Uno llevaba días dentro, introducido al ampliar la
  comprobación de Swagger. Ahora hay `pnpm typecheck` en `e2e/`, que es la única forma de verlo.
- **Romper de más da el mismo rojo por otra causa.** Al comprobar que una prueba afirma algo, hay
  que quitar **exactamente** la línea de la que depende: quitar las tres emisiones de un archivo
  habría puesto en rojo las mismas pruebas sin decir cuál de ellas sostenía cada aserción. Es la
  misma familia que «provocar un fallo distinto del que se quiere probar da el mismo rojo», y por
  eso se aísla con un ancla única.
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

### Una barrera que calla no se distingue de una barrera que funciona

La entrada de arriba —la aserción de ausencia— resultó ser un caso de algo más grande. **Van
tres, y las tres tenían la misma forma:** algo escrito, en su sitio, que no podía disparar
nunca, y que por eso no se distinguía de algo escrito, en su sitio, que sí funcionaba.

| Qué estaba escrito | Por qué no podía disparar | Cómo se supo |
|---|---|---|
| El inhibidor de suspensión de la puerta | La receta envolvía con `kde-inhibit`, que **no propaga el código de salida** de su hijo: cualquier rojo salía como cero | Midiendo el código de salida de una puerta que se sabía roja, de las tres formas |
| La detección de suspensión del veredicto | `toISOString()` da UTC y `journalctl --since` lee local: pedía el diario **cinco horas en el futuro** y no volvía nada nunca | Comparando la cadena generada con `date` antes de fiarse de ella |
| Las dos pruebas de `ReactivacionRedSocialTests` | Exigían una base de datos que en su etapa todavía no existía: no podían correr | — |

Ninguna de las tres **fallaba**. Las tres **callaban**, que se parece mucho a estar bien.

**La pregunta que lo reconoce**, y sirve para cualquier barrera —una prueba, una aserción de
ausencia, un `CHECK`, un guard de arranque, un aviso de la puerta—:

> ¿Alguna vez he visto a esta barrera decir que no?

Si nunca ha disparado, lo que se sabe de ella no es que funcione: es que compila.

**La regla que sale de ahí.** Una barrera nueva **se provoca una vez a propósito y se observa
disparar**, antes de darla por puesta. Con una entrada sintética si hace falta; no vale
razonar que debería.

**Y donde se pueda, no se deja provocable: se provoca sola.** Es el paso que faltaba, y lo
señaló el líder: una barrera que solo dispara cuando alguien se acuerda de invocarla es
indistinguible de una que no funciona. Dejar la autoprueba como comando aparte habría
contradicho esta misma entrada en el commit que la introduce.

Así que las siete ramas del veredicto **se provocan dentro de la puerta**, antes de la etapa 1,
y si alguna calla la puerta no arranca. Cuestan unos milisegundos: son sintéticas y no hacen
entrada ni salida. Es la misma idea que `SILLAR_VERIFY_FORCE_FAIL=1`, que ya provocaba la
limpieza de la base efímera para verla ocurrir.

El comando sigue existiendo para lo que dentro no cabe —enseñar lo que escribe cada rama y
ejercitar las sondas reales—:

```bash
SILLAR_VERIFY_AUTOPRUEBA_VEREDICTO=1 node scripts/verificar.mjs
```

**Que la puerta compruebe su propio instrumental y no el producto es deliberado, y no es
nuevo:** `OMITIDAS_ESPERADAS` ya comprueba la contabilidad de la propia puerta, y
`SILLAR_VERIFY_FORCE_FAIL` existe para provocar su propia limpieza.

**El corolario, que es lo que cambió el código.** Una barrera que no puede mirar tiene que
decirlo, y decirlo distinto de «miré y no había nada». Las sondas del veredicto devolvían
`null` en los dos casos, así que el aparato que existe para decir por qué la puerta está rota
era indistinguible de estar averiado — y ahí fue exactamente donde se escondió el fallo de la
zona horaria. Ahora responden una de tres: lo vi, miré y no había, **no pude mirar y éste es
el motivo**. Callar sobre la máquina está bien; callar sobre la propia incapacidad de mirar,
no.

#### Y el reverso: una barrera que para en falso es la misma enfermedad

Al día siguiente de escribir todo lo anterior, la cuarta barrera de la serie apareció por el
otro extremo. La guarda de `e2e/.media-e2e` **bloqueaba una carpeta que funcionaba**: daba por
hecho que un `chmod` denegado significaba «la creó docker como `root`», cuando lo único que
significa es «no soy el dueño». Y si el dueño es el UID de la API, la carpeta está *mejor* que
si fuera nuestra — el proceso que escribe dentro es su propietario.

**El detalle técnico que lo hace no obvio, y que conviene tener a mano:** `chmod` exige ser el
dueño **con independencia del modo**. Una carpeta `drwxrwxrwx` que no es tuya sigue negándote
el `chmod`. Por eso el 777 de la worktree del otro frente no la habría salvado: la guarda le
habría caído igual en cuanto sincronizara.

Las dos formas son la misma enfermedad, y la enfermedad no es el sentido del error:

> **No haberla provocado antes de darla por puesta.**

La que calla te deja seguir con algo que no protege; la que para en falso te para con algo que
no está roto. En los dos casos lo que faltó fue verla decir que sí y verla decir que no.

**La corrección de fondo no fue el arreglo, fue poder provocarla.** La decisión salió a
`e2e/setup/medios.ts` como función pura —sin disco, sin docker, sin arnés—, y ahí los cuatro
estados se provocan en un segundo. Porque la razón real de no haberla provocado era que **no se
podía sin levantar el stack**, y una barrera que solo se puede provocar levantando medio
sistema es una barrera que nadie va a provocar. Si al escribir una guarda cuesta trabajo
provocarla, eso ya es el hallazgo: sepárala hasta que sea barato.

**Y una consecuencia para quien integra**, que sale de que este defecto pasara la revisión: se
comprobaron forma y territorio, no comportamiento. Una rama que introduce una barrera nueva no
se integra sin que la barrera **se haya observado disparar y dejar pasar**. Las dos mitades:
verla decir que no es la mitad que se recuerda; verla dejar pasar lo que debe es la que faltó
aquí.

#### Y el método que las cazó las tres: preguntar por otra vía

Las tres —el diario que no devolvía nada, la guarda que paraba en falso, y un
`find -newermt "today"` que dio **cero** archivos de un día en el que se habían escrito 67— no
fallaron. **Contestaron.** Sin error, sin excepción, con código de salida cero, y contestaron
mal. Ninguna herramienta avisó de nada.

El parecido no es que se equivocaran igual —la primera preguntaba por una ventana imposible, la
segunda leía bien un `EPERM` y lo interpretaba mal, la tercera filtró mal una fecha—. El
parecido es **la forma de la respuesta**:

> Un resultado demasiado limpio. Cero líneas. Cero archivos. Una negativa rotunda.

Un cero es una respuesta perfectamente válida y por eso no levanta sospecha, y es justo la que
da una herramienta a la que le has preguntado mal.

**Lo que las destapó fue siempre lo mismo, y no fue releer el código:** ir a por el mismo hecho
por una segunda vía, una que no compartiera el error.

| Lo que respondía mal | La segunda vía |
|---|---|
| `journalctl --since` con hora UTC | comparar la cadena generada con lo que imprime `date` |
| `chmod` denegado leído como «lo creó `root`» | mirar quién es el dueño de verdad, y contra qué UID corre la API |
| `find -newermt "today"` → 0 archivos | **enumerar** por fecha en vez de filtrar por fecha |
| un comentario que afirmaba que `globalTeardown` solo corre si `globalSetup` terminó | **ejecutarlo** y ver que también corre cuando lanza |

La cuarta fila añade un caso que no es una herramienta sino **un comentario del propio
repositorio**, y no cambia nada: una afirmación escrita sobre cómo se comporta el código es una
respuesta como cualquier otra, y se comprueba igual. Ésa además llevaba meses ahí.

**La regla, que es barata:** cuando una comprobación devuelva justo lo que hacía falta para no
tener que hacer nada —cero resultados, nada que revisar, todo en orden—, consíguelo una segunda
vez de otra manera antes de creértelo. Y si las dos vías no pueden equivocarse igual, mejor:
**fíate del que enumera antes que del que filtra**, porque enumerar enseña lo que hay y filtrar
solo enseña lo que sobrevivió a una condición que puede estar mal escrita.

#### Una guarda pertenece a la operación que protege, no a quien la llama

La barrera que impide destruir el stack e2e de otra worktree se escribió primero en
`global-setup`, que es donde estaba el problema *a la vista*: es lo que se ejecuta al arrancar
la suite. **Y no servía.** `globalTeardown` llamaba a la misma operación destructiva sin pasar
por ahí, y remataba el trabajo un segundo más tarde.

> Poner la guarda en un llamador protege de ese llamador. Ponerla en la operación protege de
> todos, **incluidos los que todavía no existen**.

Es fácil de razonar al revés, porque el llamador es donde se entiende la intención y la
operación es donde solo se ve el mecanismo. Pero la intención se duplica y el mecanismo no.

**Y no se descubrió razonándolo: se descubrió provocándola.** La guarda estaba escrita, era
correcta en su sitio, y el stack ajeno moría igual. Sin la regla de provocar en las dos
direcciones habría entrado en `main` como una barrera que no protege — la segunda en dos días.

#### Un pariente pequeño de lo mismo: citar de memoria

En el mismo tramo, un informe dio el identificador de un commit como `76e60b6` cuando era
`d2197bf`. Es la misma familia que las citas de `ENTORNO.md` que se habían desplazado once
líneas al editar el fichero que citaban: **una afirmación sobre el árbol escrita sin volver a
mirarlo**. `CLAUDE.md` ya lo dice para archivo y línea; vale igual para un hash, un recuento o
un nombre de rama. Si no lo acabas de leer, no lo escribas.

---

## 5. Pendientes

**Se mudaron a `docs/PENDIENTES.md`, y con ellos el criterio de que cada uno lleve su disparador.**

Estaban aquí, y aquí no se leían: cuatro pendientes vivos enterrados en la línea 457 de un documento
de novecientas, cuya propia cabecera dice que sirve para saber **con qué criterio** se decide y **qué
toca ahora** — narrativa, no lista de tareas. Uno llevaba desde el 18 de agosto sin que nadie lo
mirara, y se redescubrió por un `test.fail` en la salida de la suite, no leyéndolo.

Esta sección conserva su número porque hay referencias que apuntan aquí —§4 la nombra— y romperlas
al mudarse habría sido cambiar un problema por otro.

**La §6 de abajo no se mudó**, y es deliberado: no es trabajo aplazado, son unos cinco minutos de
juicio humano que ninguna prueba puede dar.

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

**21 ago · la puerta, su primer día.** `scripts/verificar.mjs` corrió seis veces y **falló cuatro**:

| Corrida | Qué la puso roja |
|---|---|
| 2 | El filtro de `homeSections`, roto a propósito para comprobar que la costura se afirma |
| 3 | Tres aserciones de `aa-vacios` que **no habrían pasado nunca sobre una tabla pintada** |
| 4 | Los cuatro 401 del visitante anónimo, en una prueba nueva |
| 5 | Los mismos 401, ya sin las válvulas que los descontaban |

**Una puerta que nunca se pone roja no está vigilando nada** — es la misma vacuidad de las
aserciones, un piso más arriba. Este registro queda escrito para el día que lleve dos semanas en
verde: entonces dirá si es que ya no hay fallos o si es que dejó de mirar.

**21 ago · la pared de sillares y la puerta de animaciones: no existen en este repositorio.**
Buscadas por nombre en `frontend/src`, `e2e/`, `docs/` y **en todo el historial**
(`git log -S "sillares"` no devuelve nada). No es que estén sin verificar: es que nunca entraron.
Vienen de otra conversación, igual que la doctrina de animaciones, que también hubo que escribir
desde cero cuando se pidió.

**Lo que sí existe y sí corre**: el proyecto `chromium-movimiento-reducido`, 9 pruebas sobre
`transversal.spec.ts`, en **cada** corrida de la puerta. Y afirma algo: quitando
`animation: none` de la regla global (`base.css:87`), se pone roja con el motivo escrito —«el
anillo sigue animándose con movimiento reducido: 0.7s»—. Restaurada y verde.

**2 sep · la reconciliación de `fase-a-b2b-m18` y `fx-eval`, y el conflicto que Git no marcó.**
Las dos ramas salieron del mismo commit (`82e39a6`) y trabajaron once días sin verse: una cerró
M04 Clientes, la otra arregló la portada, documentó M07 y metió M02 en el arnés. Git marcó tres
conflictos —la bitácora de M04 y los dos archivos de `e2e/setup/`— y los tres se resolvieron
conservando las dos intenciones, no eligiendo una rama.

**El que importaba no llevaba marcadores.** `fx-eval` adaptó dos pruebas para que, con M01
apagado, la portada se comprobara por la sección de M04 en vez de por el aviso «Todavía no hay
contenido publicado.». Es correcto. Pero se escribió contra la portada vieja, que decidía
contando módulos activos, y en `fase-a-b2b-m18` esa decisión pasó a tomarla el registro de
contribuciones (`homeContributions.tsx`): **el aviso sale si nadie declaró contenido, no si no
hay módulos**. `crmHome` estaba en `HOME_SECTIONS` sin declarar nada, así que al juntar las dos
ramas la portada habría pintado la sección de M04 **y encima el aviso de que no hay nada**. Dos
pruebas verdes, una portada que se contradice.

El arreglo es una línea —`useHomeContribution('con-contenido')` en `crmHome`, igual que
`catalogHome`— y el hallazgo es que **ninguna de las dos ramas estaba equivocada**: el defecto
nace de juntarlas. Un merge sin conflictos no significa que el resultado afirme algo cierto.

**Y el canario cantó, que es para lo que estaba.** `contenido.spec.ts` apaga M01 para llegar a
la portada vacía, porque `catalogHome` pinta siempre y la tapaba. Con M04 —que también pinta
siempre— el caso volvía a ser inalcanzable. **No se rebajó la afirmación: se apagan dos
módulos.** Cambiarla por «se ve la sección de M04» habría dejado sin cubrir el agujero que ese
archivo existe para vigilar, y la prueba habría seguido verde diciendo cada vez menos.

**Y la primera puerta se puso roja por el arnés, no por la reconciliación.** Con tres módulos
activos en vez de dos, el reinicio que provoca activar el último ya no cabía en los cuatro
segundos de reintento del login, y la primera prueba de la suite se comía el 500 del proxy de
Vite. `global-setup` declaraba «entorno listo» inmediatamente después de activar el módulo, sin
esperar a que el host volviera. **Se arregló esperando de verdad —`waitApiReady()` tras la última
activación— y no ampliando los reintentos de `auth.ts`**, que habría dejado a cada prueba
absorbiendo el arranque del arnés y la carrera intacta para el día que M03 sea el cuarto módulo.

**Y la primera versión de ese arreglo no servía, lo que enseñó dónde iba de verdad.** Se añadió
un `waitApiReady()` en `global-setup` sin mirar que `activateModule()` **ya lo llamaba**: la
segunda puerta falló exactamente igual, misma prueba y misma línea. El defecto no era que
faltara la espera, era que `waitApiReady()` **no distingue el proceso viejo del nuevo** —da por
buena la API en cuanto un `fetch` no lanza, y el host anterior sigue aceptando conexiones
mientras Docker no lo tumba—. Lo mismo que `global-setup` ya advertía sobre `/api/setup/status`,
tres líneas más arriba de donde se puso el parche.

La espera ahora **le pregunta a Docker qué ejecución está corriendo**, antes y después de
activar: `<container-id>:<State.StartedAt>` (`docker.ts`). Y vive dentro de `activateModule()`,
que es quien promete devolver el control con el host nuevo arriba, así que M03 la hereda sin
tener que acordarse de pedirla.

**Comprobado por separado antes de gastar otra puerta**, y la medición cambió el diseño: el
identificador del contenedor es **el mismo** antes y después —Docker reinicia el contenedor, no
lo recrea—, así que una identidad basada solo en él no habría detectado nada. Lo que cambia es
la marca de arranque, y entre una y otra pasaron **12,3 segundos**: el triple de los cuatro que
`auth.ts` reintentaba.

---

### El cajón que se reabría solo (pendiente 12, cerrado el 3 sep 2026)

Llevaba abierto desde el 20 de agosto con un disparador explícito —**la tercera aparición**—
porque dos veces no bastaban para arreglar una carrera que no se sabía reproducir. La tercera
salió en la puerta de la reconciliación, y con ella la causa entera.

**Lo que se veía.** Tras asociar una imagen a un producto y pulsar «Guardar cambios», el cajón
seguía abierto. El guardado **sí ocurría** y el aviso de éxito salía, así que el síntoma no
apuntaba al guardado: apuntaba a que el cajón no se cerraba. Intermitente, una de cada dos o
tres vueltas de la suite entera, nunca a voluntad.

**Lo que pasaba.** Asociar una imagen recarga la ficha con el cajón abierto, y esa recarga es un
`GET` que puede seguir en vuelo cuando el usuario guarda. La secuencia:

```
ImageList.add()  → onChanged()  → onSaved('')  → abrirFicha(id)  → GET en vuelo
                                    «Guardar cambios» → PUT → cerrar → setEditing(null)
                                                              ← llega el GET → setEditing(product)
```

El guardado cerraba, y la recarga vieja **volvía a abrir**. No es que el cierre fallara: es que
alguien escribía después.

**Por qué no se veía leyendo.** `ProductsPage` ya distinguía las dos ramas de `onSaved` y ambas
eran correctas por separado. Lo que no existía era el orden entre ellas.

**El arreglo.** Cada carga de la ficha se numera al empezar y solo la más reciente puede escribir
`editing`; las rutas que cierran el cajón para siempre —`onClose` y el guardado— invalidan lo que
esté en vuelo **antes** de cerrar. El contador es un `ref` y no un estado a propósito:
**comprobar `editing !== null` dentro de la promesa no habría servido**, porque esa closure ve el
`editing` del render en que se creó, que es justamente el viejo.

Lo que **no** cambió: asociar o quitar una imagen se sigue guardando en el acto, el cajón sigue
abierto y la ficha se sigue recargando. Lo único que desaparece es que una recarga vieja gane.

**La prueba provoca la carrera, no la espera.** `e2e/tests/imagenes-asociadas.spec.ts` retiene la
recarga con `page.route()` hasta después de que el guardado haya cerrado, y entonces la suelta.
Se comprobó que **falla contra el código anterior** —en esa aserción y solo en esa, con la
asociación, el guardado, el aviso y el cierre ya pasados— y que pasa tres veces seguidas con el
arreglo, junto a tres vueltas del `recorrido` completo.

**El hábito que deja.** El `waitForLoadState('networkidle')` que se había puesto en
`recorrido.spec.ts` quitaba la carrera **de la prueba** y dejaba intacta la del producto; su
propio comentario lo decía por escrito. Una espera añadida a una prueba para que deje de fallar
es una hipótesis sin verificar, y sobrevive hasta que alguien la lee en voz alta.

---

### La portada que prometía un catálogo vacío (pendiente 1, cerrado el 3 sep 2026)

**El síntoma.** Con M01 activo y cero productos públicos, la portada decía «Nuestra tienda —
Mira todo lo que tenemos publicado» y enlazaba al catálogo. El visitante llegaba a una lista
vacía. Y había un segundo daño, menos visible: esa sección declaraba `'con-contenido'` pasara lo
que pasara, así que **la portada no podía llegar nunca a su estado vacío** mientras M01
estuviera instalado.

**La causa.** `CatalogHomeSection` no consultaba nada. Era un `EmptyState` fijo con un
`useHomeContribution('con-contenido')` fijo debajo. Una sección que no pregunta no puede
responder.

**Por qué M02 lo volvió observable pero no lo creó.** Mientras hubo **un solo módulo publicable**
y ese módulo pintaba pasara lo que pasara, el caso «activo y sin publicar» era inalcanzable: no
existía una portada en la que M01 estuviera y no aportara. M02 trajo cuatro bloques que sí
dependen de datos, y con ellos la pregunta «¿y si nadie aporta?» — que es la que destapó que uno
de los aportes era una afirmación sin comprobar. El defecto llevaba ahí desde que se escribió la
sección; lo que faltaba era un segundo módulo que hiciera visible la diferencia entre *poder*
aportar y *aportar*.

**La solución.** La sección pregunta al **mismo endpoint público que usa `/catalogo`**
(`publicCatalog.products`), con `pageSize: 1`: `totalItems` dice si existe alguno sin traerse el
catálogo a la portada. Ni endpoint nuevo, ni servicio administrativo en la web pública. Y declara
lo que pinta:

| Estado | Declara | Pinta |
|---|---|---|
| Cargando | `'cargando'` | nada |
| Listo, sin productos | `'vacio'` | nada |
| Listo, con productos | `'con-contenido'` | la invitación |
| Error | `'con-contenido'` | la invitación |

**El estado de carga no necesitó ampliar ningún contrato.** `EstadoAporte` ya tenía `'cargando'`
y `useHomeState()` no afirma que la portada esté vacía mientras alguien siga esperando: el aviso
no puede parpadear. Es exactamente el agujero para el que se diseñó ese tercer valor, usado ahora
por segunda vez.

**El error cuenta como contenido**, por la regla que M02 ya había fijado: lo que se declara tiene
que coincidir con lo que se pinta. Si la consulta falla no sabemos si hay catálogo —lo normal es
que sí—, la sección se sigue pintando, y decir «todavía no hay contenido publicado» porque no
pudimos *preguntar* sería una segunda explicación del mismo hueco, con la falsa debajo.

**El armazón no se enteró.** `PublicSite` sigue componiendo contribuciones sin saber que existe
un módulo llamado catálogo. Quien sabe si tiene algo que aportar es M01, que es donde estaba el
dato.

**Las pruebas.** Los dos casos vacíos viven en `e2e/tests/aa-vacios.spec.ts`, que es el único
momento de la suite en que el catálogo está de verdad vacío —afirmarlo después de que alguien
publique un producto no lo comprueba, lo hace imposible—: que la portada no invita a un catálogo
que no existe, y que **con M01 activo** llega a su estado vacío cuando nadie más aporta. Esa
segunda es la única que cazaría un `catalogHome` que declarase contenido sin pintarlo: el canario
de `contenido.spec.ts` no puede, porque apaga el módulo y entonces la sección ni se monta. El
caso positivo está en `tienda.spec.ts`, que publica productos.

**Lo que no se tocó, y por qué.** `crmHome` también declara `'con-contenido'` fijo, y está bien:
lo que promete —«entra o crea una cuenta»— es cierto siempre, sin listado detrás que pueda venir
vacío. La diferencia es esa, no el patrón.

---

### La auditoría dejó de enseñar identificadores (pendiente 10, cerrado el 3 sep 2026)

**El problema.** La columna «Entidad» pintaba `entityType · entityId` en crudo, y desde la ADR-018
el identificador de un medio o de una sesión es un `uuid`. Como **cada acceso** deja una entrada
con el `uuid` de su sesión, la pantalla estaba llena de `01a016da-5b2e-722b-…`, contra la regla de
que los identificadores nunca se muestran al usuario. Y el filtro «Usuario» pedía un número, con
la ayuda «Su identificador numérico»: para buscar lo que había hecho alguien había que averiguar
antes su `adminUserId`. Un dato interno convertido en requisito para trabajar.

**La decisión de producto.** No se crea una excepción a la regla. Lo que se separa son dos cosas
que se estaban confundiendo: **presentar** es poner un identificador delante de quien no lo pidió;
**responder** es dárselo a quien despliega la fila que está investigando. La regla prohíbe lo
primero. La tabla no presenta ninguno; el detalle responde con el identificador entero, sin
acortar ni transformar. En la base, en la entrada y en el API no cambia nada.

**Y había una segunda fuga, que solo apareció al tapar la primera.** `MediaService` escribía
«Archivo subido para el módulo 'catalog': 019fff83-….png (image/png)». Ese nombre no es el que la
persona subió: es el identificador generado más la extensión (`MediaStorage.cs:56`), porque la
clave de un medio **es** el nombre del archivo. Así que arreglar la columna «Entidad» habría
dejado el mismo `uuid` a la vista por la columna «Resumen», y la prueba transversal habría seguido
en rojo señalando un sitio distinto del que se acababa de arreglar.

**Es la lección, más que el arreglo.** El defecto tenía dos puertas y el `test.fail` que lo
documentaba solo nombraba una. Mientras la prueba estuvo marcada, la segunda no podía descubrirla
nadie: una prueba que se sabe roja no informa de por qué está roja. Se buscaron todos los
resúmenes del backend que interpolan un identificador —evidencia conservada antes y después del
cambio— y `MediaService` era el único con un `uuid`.

**Traducción de tipos, y su honestidad.** `entityType` se traduce en el frontend de la pantalla,
con las palabras **que el panel ya usa**: `social_link` es «Red social» porque el menú dice «Redes
sociales», y `media_asset` es «Archivo» porque la pantalla se llama «Archivos». Inventar un
segundo vocabulario obligaría a traducir dos veces al leer. Un tipo desconocido se muestra **con
su código técnico tal cual**: ni «Desconocido», ni una traducción inventada. Lo feo y cierto avisa
de que falta algo; lo bonito y falso hace que nadie venga a arreglarlo.

**El mapa nació ya pasado de su umbral, y está medido.** Se inventariaron los tipos que hoy se
escriben de verdad: **20**. El criterio era «más de un puñado y la etiqueta pertenece a la
escritura». Veinte no es un puñado. No se movió porque implica tocar `Sillar.Core.Contracts`, una
costura compartida que no cabía aquí — pero se registró como **disparador cumplido**, no como
previsión futura (`PENDIENTES.md` §17).

**El filtro.** Un `<select>` con «nombre — correo», y `(inactivo)` para los dados de baja.
**Vienen todos**, y tiene que ser así: `AdminUserService.ListAsync` no filtra por `IsActive`, y lo
que alguien hizo antes de que le dieran de baja **sigue en el registro** — es justo lo que se va a
buscar. Si la lista falla, el desplegable se queda con «Todos»: no se vuelve al campo numérico,
porque un fallo al cargar una ayuda no es motivo para pedirle a nadie que se sepa un
identificador. El `adminUserId` sigue viajando en la consulta; lo que desaparece es la obligación
de conocerlo.

**Sin tocar `Table`.** `Column.render` ya devuelve `ReactNode`, así que el `<details>` cabe dentro
de la celda. Extender la costura compartida por una columna habría costado dos gates y no habría
comprado nada. `<details>` y no un botón propio: trae plegado, foco y teclado del navegador, y su
contenido plegado **no está en el texto renderizado** — la regla se cumple de verdad, no ocultando
con CSS.

**Por qué no se tocó el contrato.** `AuditEntry.entityId` y `AuditQuery.adminUserId` siguen igual,
el API igual, la base igual, sin migración y sin endpoint nuevo. Lo único que cambió del backend
es el texto de un resumen, y solo porque ese texto era otra vía de la misma fuga.

**Las pruebas.** `transversal.spec.ts` perdió su `test.fail` y afirma las dos mitades juntas:
ningún `uuid` en el texto de **toda la pantalla** —no solo de la columna—, la etiqueta legible en
«Entidad», y luego el identificador exacto al desplegar esa fila y su desaparición al replegarla.
Por separado cada mitad tiene una forma barata y falsa de pasar: esconder el dato del todo cumple
la primera, y no arreglar nada cumple la segunda. `medios.spec.ts` afirma que la entrada se sigue
registrando, que `entityId` sigue completo, que el resumen ya no lleva el identificador y que
sigue diciendo módulo y tipo. `auditoria.spec.ts` cubre el filtro, creando y dando de baja a su
propio administrador para no depender del orden de las specs.

**Y había una tercera puerta, encontrada auditando antes del gate.** `ContactMessageService`
escribía «Baja del mensaje de contacto **#42**.». No es un `uuid`: es la clave primaria entera de
la fila, repetida dentro del texto que se lee.

Que fuera un entero es justo lo que la hacía peligrosa. La regla de producto no habla de `uuid`,
habla de **identificadores internos**, y un `#42` es tan interno como un `019fff83-…` — solo que
**invisible para la prueba que vigila**: `transversal.spec.ts` busca la forma de un `uuid`, y un
número le pasa por delante sin que salte nada.

**Y no se puede arreglar ampliando la regex.** «Cualquier número» daría falsos positivos con
fechas, cantidades, precios y teléfonos, que son números legítimos y frecuentes en estas
pantallas. Convertir una heurística en definición de producto acabaría o con una prueba que grita
por todo, o —peor— con la regla recortada a lo que la regex sabe reconocer. Así que quedan
repartidas:

- **La regla de producto cubre todos los identificadores internos**, del tipo que sean.
- **La prueba transversal cubre la familia `uuid`**, que es la que puede reconocer sin
  ambigüedad, y no pretende reconocer todas las claves primarias posibles.
- **Cada productor con identificadores numéricos necesita su propia prueba semántica** cuando se
  detecta. La de éste vive en `crm-contact.spec.ts`, sobre el flujo de baja que ya existía:
  la entrada se sigue escribiendo, `entityId` sigue siendo el correcto, el resumen ya no lo
  contiene y sigue diciendo qué pasó.

La fila sigue identificándose por `EntityType` y `EntityId` y se consulta desplegando el detalle,
igual que en el caso de los medios. **Esto no cierra el pendiente §16**: aquel pide que el resumen
nombre la fila —«Baja del mensaje de Ana Quispe»—, y esto solo quita un identificador que no
debería haber estado. Direcciones distintas.


**5 sep · el sellado de replicación se extrae, y se extrae antes de tiempo a propósito.**
`StampReplicationColumns` estaba copiado **tres veces** —`CoreDbContext.cs`,
`CatalogDbContext.cs`, `CrmDbContext.cs`— con los cuerpos idénticos salvo el nombre del campo
inyectado (`clock` / `_clock`). El pendiente §3 le había puesto disparador: «la cuarta copia, **o**
la primera vez que dos copias discrepen». **Ninguna de las dos mitades se había cumplido**, y aun
así se extrae.

**Esto es un adelanto explícito, no un disparador cumplido**, y conviene que quede escrito con su
motivo porque el motivo es lo único que lo justifica: aquel disparador se escribió cuando un solo
equipo escribía las tres copias. Con dos frentes en paralelo la divergencia pasa de eventual a
probable —dos equipos en dos `DbContext` no se ven— y a la vez la extracción se vuelve imposible
de programar, porque toca `Sillar.Shared`, CORE y Catalog a la vez. El día que el disparador
saltara, ya no habría hueco para arreglarlo. Es el mismo patrón de los tres casos de los que este
proyecto se salvó por poco: baratos el día antes, imposibles el día después.

**Y al ir a extraerlo apareció que la divergencia ya había empezado.** El §3 hablaba de tres
copias del sellado, y son tres; pero del **mapeo** hay dos copias iguales —`Catalog` y `Crm`— y
una tercera de otra forma: CORE no tenía `MapReplication`, sino dos columnas a mano en
`MediaAssetConfiguration` y las otras dos delegadas en `AsCreatedAt`/`AsUpdatedAt`. El resultado
era el mismo, así que no rompía nada y por eso nadie lo vio. Discrepar en la forma es el paso
anterior a discrepar en el fondo.

**Dónde vive ahora, que no es donde se dijo.** No en `Sillar.Shared`. Ese proyecto declara en su
propio `.csproj` que «aquí no entra lógica de negocio ni acceso a datos» y no referencia EF Core;
meterlo allí obligaría a añadírselo, y con él lo heredarían los cinco proyectos que hoy lo
referencian sin tenerlo —entre ellos los tres `*.Contracts`, que son justo lo que un módulo puede
referenciar de otro—. EF Core acabaría cruzando el grafo entero de módulos por una utilidad de
cuatro columnas. Tampoco cabía en `Sillar.Core`: un módulo nunca referencia el `Data` de otro
(regla 3). Así que hay un proyecto nuevo, **`Sillar.Shared.Data`**, para infraestructura de
persistencia que es de la plataforma y no de ningún módulo.

**Comprobado que no cambia el esquema**, que es lo único que podía convertir un refactor en una
migración: `dotnet ef migrations has-pending-model-changes` responde «No changes have been made to
the model since the last migration» en los tres contextos.

**Y el sellado tiene por fin prueba propia.** De las tres copias, solo una estaba cubierta —la de
CRM, y por una prueba que toca la base—. Ahora hay cinco pruebas de lógica en
`Sillar.Shared.Data.Tests` que no abren ninguna conexión: el alta pone nodo, versión 1 y las dos
fechas; la modificación sube la versión y **no** reescribe origen ni fecha de alta; y ni una fila
sin cambios ni una borrada suben nada.

---

### 5 sep 2026 · Registros de superficie: comprobación estructural

Tras revisar el retoque del pie se comprobó, **sin convertirlo en una prueba**,
la colocación de las dos fábricas de estado de superficie. Dos `grep` sobre
`homeState.tsx` y `footerState.tsx` muestran exactamente dos llamadas a
`crearRegistroDeSuperficie()`: `const portada` y `const pie`, ambas a nivel de
módulo. Un segundo `grep` muestra que esos dos registros son los que alimentan
las exportaciones `AportesDePortada` / `useAporteDePortada` / `useHomeState` y
`AportesDeFooter` / `useAporteDeFooter` / `useFooterState`.

**Esto es una comprobación, no una prueba de comportamiento.** En esta forma el
error temido —crear el registro dentro de un render y luego exportar sus hooks—
no es representable: las exportaciones salen del ámbito del módulo. No se añade
Vitest ni un segundo runner. El disparador para decidir una infraestructura
unitaria de frontend será el primer comportamiento de frontend que no pueda
alcanzarse y observarse desde el navegador.

La regresión observable se coloca donde estaba el defecto real: `PublicLayout`.
La prueba E2E navega dentro de la SPA entre `/`, `/catalogo` y `/`, mantiene el
pie con contenido y cuenta las cargas de `/api/cms/social-links`. El arnés usa
Vite en desarrollo y la aplicación usa `StrictMode`, por lo que el doble ciclo
inicial de Effects puede producir más de una carga antes de navegar. Por eso la
prueba guarda esa cifra como línea base y exige **delta cero** durante los
cambios de ruta: lo que vigila es que cambiar de hijo no remonte al
contribuyente. **No** afirma que no haya rerenders, no fija cuántos Effects
ejecuta el entorno y no pretende probar la construcción interna del registro.

---

### 5 sep 2026 · Dos frentes, dieciséis segundos, y una escopeta apuntando al de al lado

**Incidente de coordinación, no de ejecución.** Los dos frentes recibieron a la vez el encargo
de traer `main` y relanzar. El aviso que se dio fue sobre el conflicto previsible en
`docs/BITACORA.md`; el que costó una corrida entera fue otro, y no se vio venir.

**La secuencia, leída del reloj:**

```
17:24:56  arranca la puerta del frente A
17:25:13  arranca el Vite del frente B en 55173
17:25:16  se crea sillar_e2e_db          (frente B)
17:27:20  se crea sillar_e2e_api         (frente B)
~17:32    la etapa 6 de A pide el 55173, lo encuentra ocupado y aborta
```

**Lo que se vio** fue `http://localhost:55173 is already used`: la corrida de A perdida, la de
B intacta. **Lo que no llegó a pasar por dieciséis segundos** es lo que importa: Playwright
arranca su `webServer` antes del `globalSetup`, así que A murió en el puerto **sin llegar a
tocar docker**. Si el orden hubiera sido el contrario, el `composeDown -v` de A —incondicional
hasta ese día— habría destruido el stack de B a mitad de suite, contenedores y volumen, y la
corrida de B habría muerto con un fallo que no se parece en nada a su causa.

**Tres cosas salieron de ahí, y solo una es el arreglo:**

1. **La guarda.** `composeDown()` mira de quién es el stack antes de destruirlo, por la
   etiqueta que docker compose ya pone en cada contenedor. No hizo falta inventar un marcador.
2. **Dónde iba la guarda.** La primera versión estaba en `global-setup` y **no servía**:
   `globalTeardown` se ejecuta igual cuando `globalSetup` lanza —medido, no supuesto— y
   remataba el trabajo un segundo más tarde. La guarda va en la operación destructiva, no en
   uno de sus llamadores. Se descubrió provocándola, que es la única razón por la que se
   descubrió.
3. **El defecto de fondo, que sigue abierto.** La identidad E2E de cada worktree vive **sin
   commitear a propósito**, para que no viaje a `main` — y por eso se pierde sola con cualquier
   `checkout`, `stash` o limpieza. `ENTORNO.md` §5 describía ese mecanismo con precisión y aun
   así se quedó falso: afirmaba que una worktree tenía identidad propia, que la tuvo y la
   perdió. **El documento caducó por lo que el documento describe.** Queda propuesto como
   pendiente 20; la guarda mitiga la consecuencia, no el defecto.

**Y una que no es de máquinas.** El aviso previo iba al fichero compartido que se veía venir.
El daño estaba en el recurso compartido que no se nombró: los puertos. Dos frentes en la misma
máquina comparten más de lo que comparte su código, y el inventario de lo que comparten no
existe en ninguna parte.
