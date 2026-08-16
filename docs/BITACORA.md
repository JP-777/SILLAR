# Bitácora de SILLAR

Registro vivo para continuar el trabajo. Los documentos de `docs/` dicen **qué** construir; esto dice **con qué criterio** y **qué toca ahora**.

Si retomas el proyecto sin haber estado en la conversación: lee las secciones 1 a 4 antes de decidir nada.

**Última actualización:** 15 de agosto de 2026 · **CORE completo · ERP aparcado · toca M01**

---

## 1. Estado

| | |
|---|---|
| Fundación F-01 a F-08 | Completa |
| CORE — backend | Completo. 9 tablas, 20 rutas, 181 pruebas (132 + 49). `media_assets` ahora replicable |
| CORE — pantallas | Completo. 6 entradas de menú filtradas por rol |
| **M01 Catálogo** | **Pasos 1 y 2 cerrados.** Esquema aplicado y verificado contra PostgreSQL real. Toca el paso 3, API |
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

**Siguiente paso ahora: M01 Catálogo**, primer módulo del ciclo completo de cinco pasos. Sirve a los dos productos, así que en cada campo se preguntan dos cosas: *¿tendría sentido en un negocio que solo tiene la web?* y *¿esto le cierra la puerta al mostrador?*

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
| SVG rechazado | Se ejecuta en el mismo origen del panel y puede pedir el token CSRF |
| Panel con marca SILLAR | Es lo que se demuestra al vender |
| Baja lógica en todo | Lo borrado deja huecos en banners y pedidos que lo referencian |
| Sin desactivación en cascada | El sistema nombra el obstáculo; la persona ordena |
| Módulos de demostración imposibles en producción | Dos barreras de compilación, no configuración |
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
- **Un campo nulo puede significar dos cosas.** `admin_user_id` nulo era «cuenta eliminada» y también «intento de acceso con un correo inexistente». Confundirlas habría hecho que la auditoría mintiera sobre gente que solo se equivocó al teclear.

---

## 5. Pendientes

| Pendiente | Estado |
|---|---|
| **Verificación visual del panel completo** | Claude Code no ve la interfaz. Es lo único que separa a CORE de estar verificado de punta a punta. Lista en la §6 |
| Implementar la ADR-019 | Abortar si un módulo activo no aparece en el descubrimiento. Cabe en el paso 3 de M01, que es cuando M01 se activa por primera vez |
| Verificación visual del panel | Sigue pendiente: es lo único que separa a CORE de estar verificado de punta a punta |

| Tu `.env` local está desfasado | Le faltan `API_PORT` y `MEDIA_PATH`, que sí están en `.env.example`. Sin ellos, `docker compose --profile full up -d` no levanta el API |
| Borrar `docs/BITACORA-SESION-2026-08-14.md` | Cumplió su función —traspasar contexto entre sesiones— y lo durable ya está en la ADR-012 y en las entregas. Dos bitácoras confunden cuál es la bitácora |
| Tipografía y logo de SILLAR | La paleta está validada; lo demás no |
| Dominio del producto | Sin registrar |
| Nombres comerciales de las ediciones | Pendientes. No bloquean: son etiquetas de venta, no identificadores de código |
| Datos administrativos de Bsale | Certificado, costo, volumen, series y correlativos. Preguntas 7 a 10 de la guía de observación |

Aplazados por decisión, no pendientes: retención de auditoría, vectoriales en medios, permisos granulares, vencimiento de licencias, marca blanca.

---

## 6. Verificación manual pendiente

```
cd backend  && Modules__IncludeDemoModules=true dotnet run --project Sillar.Api
cd frontend && pnpm dev
```

Siete tarjetas: CORE sin interruptor, dos activas, dos inactivas, dos bloqueadas.

1. **Las cuatro variantes de tarjeta.** Que la bloqueada nombre lo que le falta y el enlace lleve a su tarjeta.
2. **Ciclo completo.** Activa un módulo: aviso del reinicio, superposición de reconexión, vuelta sola **con la sesión abierta**, y el módulo activo.
3. **El 409.** Intenta desactivar uno del que otro dependa: frase que nombra al que bloquea, sin reinicio.
4. **Dos pestañas** escribiendo. Ningún 403.
5. **Teclado.** Recorre módulos y usuarios con Tab. Foco siempre visible, diálogos que atrapan el foco y se cierran con Escape.
6. **Tema oscuro.** Busca texto que pierda contraste.
7. **Medios.** Arrastra un archivo. Prueba uno de más de 5 MB, un `.png` que no lo sea y un SVG: tres mensajes distintos. Sube dos veces el mismo: aviso, no error.
8. **Configuración.** Que los `PENDIENTE_DEFINIR` se destaquen y se cuenten, y que el interruptor de público aparezca deshabilitado con su razón si entras como `admin`.

Es lo último que queda de CORE.

---

## 7. Registro

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

**15 ago · paso 2 de M01 cerrado, y la ADR-018 aplicada.** `core.media_assets` replica con clave `uuid` v7 acuñada una sola vez en `MediaStorage.SaveAsync` —el mismo valor en la fila y en el disco—, las cuatro FK del catálogo pasan a `Guid`, y las migraciones de CORE se refundieron en una sola, que es lo que la ADR-018 autorizaba por no haber instalación desplegada. Las 181 pruebas siguen en verde (132 + 49).

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
