# M07 — Plan de pruebas de lo ya decidido

Creado: 26/09/2026, America/Lima · Última verificación: 26/09/2026 ·
Commit base comprobado: `711bfba7cf3be80baa146b44e79ddf7a633d695d` (rama `m07-b2b` sobre él).

**Qué es.** Las pruebas que M07 tiene que tener para cinco requisitos que **ya están decididos**:
autenticación, límite por cuenta, permisos, ausencia de rutas muertas, e instalación y
desinstalación. Se escribe antes del código para que el código se construya contra ellas.

**Qué no es.** No fija ninguna de las decisiones abiertas (`ESCALADAS-M07.md`, E1–E5 y E9). Donde
una prueba toca un tramo en suspenso, lo dice y **no presupone la respuesta**: prueba solo la parte
que no depende de ella.

**Las tres reglas que valen para todo el plan:**

1. **Toda afirmación de «no se creó nada» se comprueba contra la base**, contando filas antes y
   después. Un código de estado no prueba que no se haya escrito nada.
2. **Toda barrera nueva se ve disparar y dejar pasar**, y su prueba se rompe a propósito una vez
   (`ANTES-DE-EMPEZAR-UN-MODULO.md` §2). La columna «Rotura deliberada» de cada tabla dice qué
   defecto se introduce para ver la prueba en rojo.
3. **Cero pruebas omitidas.** La puerta ya las rechaza si no están declaradas
   (`scripts/verificar.mjs:2114-2150`); ninguna prueba de M07 se declara omitida.

**Dónde vive cada nivel:**

| Nivel | Dónde | Toca la base |
|---|---|---|
| Lógica pura | `backend/Sillar.Modules.B2B.Tests/` (nombre según `ARQUITECTURA_MODULAR.md:308`; se confirma al crear el proyecto) | **No** (`CLAUDE.md`, «Pruebas») |
| API directa y base | `e2e/tests/b2b-*.spec.ts`, con los ayudantes existentes de `e2e/setup/` sin modificarlos | Sí |
| Pantalla | `e2e/tests/b2b-*.spec.ts` | Sí |

Si hiciera falta un ayudante nuevo en `e2e/setup/**`, se pide a Chat 2: es costura.

---

## 1 · Autenticación

**El requisito:** los cuatro endpoints públicos exigen sesión de cliente (`SPEC.md` §6, regla 1 y
el criterio de §9). **Por API directa**, no por pantalla: esconder un botón no cierra un endpoint.

| # | Prueba (nombre en español, tal como saldrá en la lista) | Cómo | Rotura deliberada |
|---|---|---|---|
| 1.1 | Sin sesión, crear una personalización da 401 y no deja fila | `POST` sin cookie; `count(*)` antes y después | Quitar la política de cliente del endpoint |
| 1.2 | Sin sesión, crear una de volumen da 401 y no deja fila | Igual | Igual |
| 1.3 | Sin sesión, las solicitudes propias dan 401 | `GET /api/b2b/my-requests` | Igual |
| 1.4 | Sin sesión, una cotización da 401 | `GET /api/b2b/quotes/{n}` | Igual |
| 1.5 | La cookie del panel no sirve como cliente en M07 | Cookie de personal contra los cuatro | Autenticar con el esquema del panel |
| 1.6 | La cookie de cliente no abre ninguna ruta de `/api/admin/b2b/*` | Recorre la tabla de administración | Autorizar el panel con la política de cliente |
| 1.7 | Con sesión y sin token CSRF, crear da 403 y no deja fila | `POST` sin `X-CSRF-Token` (`CustomerCsrfEndpointFilter.cs:16`) | Quitar el filtro del endpoint |
| 1.8 | La cotización de otro cliente da 404, igual que un número inexistente | Dos cuentas; misma respuesta, **mismo cuerpo** | Devolver 403 cuando existe |
| 1.9 | `staff_notes` no aparece en ninguna respuesta pública | Escribir una nota con un marcador único y buscarlo en **el cuerpo crudo** de las cuatro respuestas | Proyectar la entidad entera |
| 1.10 | El `customer_id` de la solicitud es el de la sesión, no el del cuerpo | Mandar otro `customerId` en el JSON; la fila queda con el de la sesión | Leer el identificador del cuerpo |

**Precedentes que se imitan, no se copian:** `e2e/tests/crm-auth-isolation.spec.ts:58` y `:70`
para 1.5 y 1.6; `e2e/tests/sesion-csrf.spec.ts` para 1.7.

**1.9 y 1.8 no dependen de E5:** usan el número que devuelva la API, sea cual sea su formato.

---

## 2 · Límite por cuenta

**El requisito:** «como toda escritura está autenticada, el ritmo se limita contra la cuenta»
(`SPEC.md` §6). La enmienda del 26/09 establece que ese límite **no existe todavía y lo pone M07**.

**Diferencia con el precedente, y es la que las pruebas tienen que fijar:** el límite del contacto
de M04 se cuenta **por IP** (`backend/Sillar.Modules.Crm/Contact/ContactMessageService.cs:35-36`).
El de M07 se cuenta **por cuenta**. Una prueba que no distinga las dos cosas deja pasar la copia
literal del precedente.

**Los números —cuántas por ventana y de qué duración— no se fijan aquí.** Son un parámetro
comercial. Las pruebas se escriben contra el parámetro (N, ventana), no contra un valor.

**La decisión va aparte y pura** (§2 de `ANTES-DE-EMPEZAR-UN-MODULO.md`: una barrera que solo se
provoca levantando medio sistema no la provoca nadie): una función que recibe cuenta, instante y
contador, y responde si pasa y cuándo se puede volver a intentar.

| # | Prueba | Nivel | Rotura deliberada |
|---|---|---|---|
| 2.1 | La solicitud N dentro de la ventana pasa | Pura | Poner el límite en N−1 |
| 2.2 | La solicitud N+1 dentro de la ventana se rechaza y dice cuándo volver | Pura | Poner el límite en N+1 |
| 2.3 | Pasada la ventana, la cuenta vuelve a poder | Pura, con reloj inyectado | Olvidar el reinicio de la ventana |
| 2.4 | **Una cuenta agotada no bloquea a otra desde la misma IP** | Pura | **Contar por IP** — es la que caza la copia del precedente |
| 2.5 | Las dos formas de solicitud comparten el límite de la cuenta | Pura | Llevar un contador por tipo |
| 2.6 | Por API, N+1 da 429 con `Retry-After`, una frase que dice cuándo volver, y no deja fila | e2e | Responder 400 o crear la fila |
| 2.7 | La frase del rechazo no dice «error» ni ofrece «Aceptar» | e2e, pantalla | — (regla de `CLAUDE.md`, Frontend) |

`429` y `Retry-After` ya los usa M04 (`ContactMessageEndpoints.cs:33` y `:113`).

**Queda abierto, y no se decide aquí:** si una petición inválida (descripción vacía) consume
cupo. El precedente de M04 lo consume, porque limita antes de validar
(`ContactMessageService.cs:35-51`). Se anota como pregunta para la implementación; la prueba 2.1
se escribe con peticiones válidas para no depender de la respuesta.

---

## 3 · Permisos del panel

**El requisito:** `SPEC.md` §6, tabla de administración. Mínimo `editor`; **bajas lógicas y registro
de pago, solo `admin`**. Toda escritura lleva CSRF y deja auditoría con `module_code = 'b2b'`.

**Una sola prueba parametrizada por la tabla**, no una por celda escrita a mano. La tabla del SPEC
es la fuente; si se añade un endpoint y no se añade a la tabla de la prueba, **la prueba tiene que
fallar**: por eso se contrasta también contra Swagger (3.5).

| Quién | Lectura | Escritura de `editor` | `DELETE` ×3 y `PUT .../payment` |
|---|---|---|---|
| Anónimo | 401 | 401 | 401 |
| Cliente | 401 | 401 | 401 |
| `editor` | 200 | 200/204 | **403 y la fila no cambia** |
| `admin` | 200 | 200/204 | 200/204 |

| # | Prueba | Rotura deliberada |
|---|---|---|
| 3.1 | Cada ruta del panel responde según la tabla, para los cuatro perfiles | Bajar el pago a `editor` |
| 3.2 | Un `editor` que intenta dar de baja no cambia `is_active` | Comprobar el rol después de escribir |
| 3.3 | Toda escritura del panel sin CSRF da 403 y no cambia nada | Quitar el filtro de una ruta |
| 3.4 | Toda escritura deja una fila de auditoría `b2b` **que nombra la fila** | Resumen genérico («Cambio de estado») — §5 de `ANTES-DE-EMPEZAR-UN-MODULO.md` |
| 3.5 | Las rutas de la prueba y las de Swagger bajo `/api/admin/b2b` son el mismo conjunto | Añadir una ruta sin añadirla a la tabla |
| 3.6 | Una cotización `enviada` no admite edición de líneas | Permitir edición en `enviada` |

**3.4 y E5:** la prueba comprueba que el resumen contiene el número visible que devolvió la API,
no un formato concreto.

---

## 4 · Sin rutas muertas

**El requisito:** criterio de terminado de `CLAUDE.md` y §7 del SPEC. Con M07 inactivo, ni rutas,
ni menú, ni hueco en la ficha.

**Hecho de código que las pruebas aprovechan:** un módulo inactivo no mapea sus endpoints
(`backend/Sillar.Api/Program.cs:143-146`), y una ruta de frontend de un módulo inactivo no existe
y cae en la redirección (`frontend/src/app/routes.tsx`, comentario de cabecera).

| # | Prueba | Rotura deliberada |
|---|---|---|
| 4.1 | Con M07 inactivo, `/api/b2b/*` y `/api/admin/b2b/*` dan 404 | Mapear las rutas sin mirar la activación |
| 4.2 | Con M07 inactivo, las tres rutas `/admin/solicitudes/*` redirigen | Montar las rutas sin condición |
| 4.3 | Con M07 inactivo, el grupo «Solicitudes» no está en el menú | Declarar el grupo fuera del módulo |
| 4.4 | Con M07 inactivo, la ficha de producto no tiene la acción **ni su hueco** | Pintar un contenedor vacío |
| 4.5 | Con M07 activo, los mismos cuatro sí aparecen | Olvidar montar algo — sin 4.5, 4.1–4.4 pasan con un módulo que nunca se monta |
| 4.6 | Desactivar y reactivar M07 no pierde solicitudes ni cotizaciones | Borrar al desactivar |
| 4.7 | Con M07 activo no se puede desactivar M01 ni M04, y el 409 nombra a M07 | — ya lo impone la plataforma; se prueba que **M07 figura** en el mensaje |

**Criterios de ausencia bien planteados** (`ANTES-DE-EMPEZAR-UN-MODULO.md` §1): 4.3 y 4.4 no buscan
la palabra «Solicitudes» —otro módulo puede usarla con toda la razón—, sino **lo que identifica al
sujeto**: el destino de los enlaces (`/admin/solicitudes/`) y el marcador propio de la acción de M07.

**4.4 depende de C1** (la superficie en la ficha, pedida a Chat 2). Se escribe cuando exista el
mecanismo; hasta entonces **no se declara como omitida**: sencillamente no existe todavía.

**Precedentes:** `e2e/tests/zz-desmontaje.spec.ts:104` para 4.6; `e2e/tests/modulos.spec.ts:159`
para 4.7.

---

## 5 · Instalación y desinstalación

**El requisito:** «el schema se crea y se elimina sin afectar a otros módulos», «los scripts son
idempotentes» (`SPEC.md` §9), y el criterio de terminado de `CLAUDE.md`.

**Precedente directo, y se sigue su forma:** `e2e/tests/zz-instalacion.spec.ts:97-160`. Compara
**estados**, no ausencia de excepciones, y **reinstalar es parte de la prueba, no limpieza**
(`:146-148`).

| # | Prueba | Rotura deliberada |
|---|---|---|
| 5.1 | Migrar crea el schema `b2b` con sus tablas y su `__migrations` dentro | Migración sin `HasDefaultSchema` |
| 5.2 | Migrar dos veces da el mismo esquema | — |
| 5.3 | Sembrar dos veces da los mismos recuentos (hoy el seed está vacío: se prueba igual) | Un `INSERT` sin guarda |
| 5.4 | Desinstalar M07 elimina `b2b` y deja `core`, `catalog`, `crm` y `cms` con los mismos recuentos, fila por fila | `CASCADE` hacia arriba |
| 5.5 | Desinstalar M07 dos veces no falla | Quitar `IF EXISTS` |
| 5.6 | Reinstalar M07 sobre una base que ya lo tuvo lo deja como nuevo | — |
| 5.7 | **Tras reinstalar, las claves foráneas de `b2b` hacia `crm` y `catalog` existen** | Ver el hallazgo de abajo |
| 5.8 | Las restricciones de datos se prueban con `INSERT` real: `ck_quotes_origen` rechaza dos orígenes y ninguno | Relajar el `CHECK` |

### Hallazgo C6: riesgo futuro, no un fallo reproducido

> **Enmienda 26/09.** La primera redacción de este apartado hablaba de «la suite de hoy». No era
> exacto: **M07 no está instalado en `main`**, así que el escenario todavía no puede darse. Lo que
> sigue es lectura de código (**LEÍDO**) más una consecuencia **DEDUCIDA** que nadie ha provocado.

**LEÍDO.** `database/modules/catalog/99_drop.sql:61` y `database/modules/crm/99_drop.sql:24` hacen
`DROP SCHEMA ... CASCADE`. El script de catálogo lo avisa: CASCADE «se lleva también la clave
foránea que ese módulo declaró hacia catalog… la tabla del otro módulo no desaparece, se queda con
la columna huérfana» (`catalog/99_drop.sql:27-34`). Su lista de módulos avisados no incluye `b2b`
(`:43`: `sales`, `inventory`, `pos`, `purchasing`). `e2e/tests/zz-instalacion.spec.ts:114`
desinstala y reinstala el catálogo.

**DEDUCIDO, sin reproducir.** Cuando M07 exista con sus FK, desinstalar a mano M01 o M04 se las
llevaría. Al reinstalar la dependencia, la migración de M07 **podría no volver a correr** —ya
figuraría en `b2b.__migrations`—, y entonces sus FK no volverían. Si la suite sigue desinstalando
el catálogo en la misma base, lo que corra después lo haría sin esas FK.

**Distinción que importa:** esto es de la **desinstalación manual** con scripts. La **desactivación
administrativa** ya se niega con dependientes duros activos (`e2e/tests/modulos.spec.ts:159`) y no
borra nada (`zz-desmontaje.spec.ts:104`).

**Primera provocación prevista:** la prueba 5.7, cuando exista la migración de M07. Hasta entonces
no hay nada que reproducir.

- **No es un defecto de M07 ni se arregla desde M07**: `catalog/99_drop.sql` es de M01,
  `crm/99_drop.sql` de M04, y `e2e/tests/zz-instalacion.spec.ts` es de plataforma.
- **Petición C6 a Chat 2, por efecto:** que desinstalar a mano un módulo del que M07 depende duro
  **lo diga** nombrando a `b2b`, y que la suite no deje a M07 sin sus FK para las pruebas que corren
  después. **La prueba 5.7 es la que lo vigilará**, y tendrá que correr **después** de cualquier
  desinstalación de M01 o M04 en la suite.
- **Pregunta para la auditoría (no se decide aquí):** ¿la reinstalación de una dependencia dura
  debe restaurar las FK de sus dependientes, o desinstalar una dependencia dura con dependientes
  instalados debe negarse también a mano? Es de plataforma.

---

## 6 · Lo que este plan no cubre, y por qué

| Qué | Por qué no | Cuándo entra |
|---|---|---|
| La consulta «a consultar» | E1 y E2 abiertas | Al resolverse, con su criterio compartido con A |
| Caducidad de líneas de catálogo por precio | E3 abierta (producto o presentación) | Al resolverse E3 |
| Refresca / congela | Depende de la forma de `quote_lines` (E3) para la mitad que congela | La mitad que refresca (`special_order_leads`) se puede escribir ya; la otra, con E3 |
| Foto de referencia | E4 | Si sobrevive a E4 |
| Formato del número | E5 | Las pruebas usan el número devuelto, sin suponer formato |
| Formulario de volumen | E9 | Al resolverse E9 |
| Estados de pantalla (vacío, cargando, conflicto) | Paso 3.5: se prueba lo que Diseño dibuje, no un mockup | Paso 4 |
