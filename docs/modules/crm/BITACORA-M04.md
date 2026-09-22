# Bitácora de M04 Clientes

Lo que se decide mientras se construye este módulo, con su porqué. **Se vuelca en
`docs/BITACORA.md` al cerrar**; hasta entonces vive aquí para que la común tenga un solo escritor.

---

## La ventana del esquema, y cuándo se cierra

**Abierta desde el 21 de agosto de 2026 hasta la primera instalación de M04 en cualquier entorno.**

> **Mientras M04 no esté instalado en ningún sitio, la migración inicial se reescribe. Desde la
> primera instalación, solo se añade.**

Es la misma regla que se escribió para M02, y está aquí con fecha porque **quien lea esto dentro de
tres meses necesita saber en cuál de los dos días está**. Antes de esa línea, equivocarse en una
columna cuesta una tarde; después, una migración con datos dentro.

Eso no es permiso para ir deprisa. **Es permiso para no ir con miedo**, que no es lo mismo: hay que
ir con cuidado igual, pero sin pagar por adelantado el precio de un error que hoy todavía se
deshace.

---

## Decisiones del esquema

### El índice único del correo cubre **todas** las filas, incluidas las de baja

**La alternativa era un índice parcial** —único solo entre fichas activas—, y se descarta porque
**rompe la regla del SPEC §4**: si puede haber dos fichas con el mismo correo, «se enlaza a la
existente» deja de ser determinista y hay que decidir a cuál.

Pero eso abre el caso que el SPEC no cerraba, y que **no es raro**: sin borrado físico, **todo el
que se dé de baja conserva su correo para siempre**. Con el índice completo, esa persona chocaría al
registrarse — y recibiría el mensaje genérico, porque la respuesta no puede revelar que la cuenta
existe. **Un callejón sin salida disfrazado de mensaje neutro.**

#### La primera versión de esta decisión estaba mal, y por dónde se cayó

Escribí que al verificar se crea la cuenta pero **la ficha sigue de baja**, defendiéndolo así:

> *«Verificar el correo prueba que controla el buzón, no que el negocio quiera volver a tenerlo de
> cliente.»*

**La frase es correcta y la decisión no**, porque solo se sostiene si «de baja» significa que el
negocio no lo quiere. Y estaba haciendo que ese estado cargara **dos significados distintos** que
el propio SPEC ya separa en su §8:

| Estado | Qué pasó |
|---|---|
| **De baja** | Ya no es cliente activo: limpieza, inactividad, o él mismo lo pidió |
| **Bloqueada** | **El negocio decidió que no lo quiere** |

> **Un estado que carga dos significados obliga a elegir el peor comportamiento para los dos.**

Es la misma forma que ya se arregló tres veces: un `null` que significaba «de baja» y «no existe»,
`IsActive` frente a `IsPublic`, y `product_is_active` naciendo porque «desactivado» y «no existe» se
estorbaban.

Y el precio de no separarlos era concreto: alguien de baja **verificaba su correo y luego no podía
entrar**, con el mensaje genérico y sin explicación posible. **Había hecho todo bien y llegaba a una
pared muda.** En el bloqueado eso es el precio deliberado del caso raro; en el de baja era un
callejón para el caso normal.

#### Lo que se hace, ya separado

**Registrarse siempre da la misma respuesta y siempre envía el correo de verificación.** Lo que
cambia es qué ocurre al verificar:

| Estado de la ficha | Al verificar |
|---|---|
| **No existe** | Se crea ficha y cuenta |
| **Existe sin cuenta** | Se enlaza la cuenta a la ficha existente |
| **De baja** | **Se reactiva** y se enlaza la cuenta |
| **Bloqueada** | **No entra.** Queda **solicitud registrada con su fecha**, y quien administra decide |

**Por qué la de baja se reactiva:** «de baja» no era una decisión sobre esa persona, era que dejó de
estar activa. Que vuelva y demuestre que controla su buzón es exactamente lo que revierte ese
estado — y es el caso común, no el raro: quien vuelve a una tienda de barrio después de un año no
es un problema de seguridad.

**Por qué la bloqueada no:** ahí el negocio sí decidió, y un formulario público no deshace una
decisión del negocio.

> **Y la reactivación queda en la auditoría**: qué ficha, cuándo, y **que fue por verificación de
> correo, no por mano de nadie**. Es un cambio de estado que ninguna persona autorizó, así que
> tiene que poder leerse después.

**No implementes todavía la auditoría descrita ahí. Pertenece al Paso 3.**

> **Las dos ramas revisaron este párrafo por separado, con 50 minutos de diferencia.** El 24 de
> agosto a las 23:40 en `fase-a-b2b-m18` (`fe8898c`) y el 25 a las 00:30 en `fx-eval` (`c4da78a`),
> ninguna viendo a la otra. **Llegaron a la misma decisión**: «de baja» se reactiva al verificar,
> «bloqueada» no. Lo que queda arriba es la redacción de la primera, que conserva la autopsia de
> por dónde se cayó la versión anterior; de la segunda se conserva la nota de alcance. Se anota
> aquí porque dos revisiones independientes que coinciden dicen algo que una sola no dice.


### El documento es único **entre las fichas que lo tienen**

Índice parcial, donde no es nulo. Dos clientes con el mismo DNI es un error de datos, no un caso.

**Y choca con el alta manual**, que es lo que hay que resolver en la pantalla y no en la base:

> **La ficha avisa antes de guardar: «ya existe una ficha con ese documento», con el enlace a
> ella.** Un choque de índice único no es un mensaje para una persona — es lo mismo que ya se
> resolvió en marcas y categorías, donde el 409 nombra lo que estorba en vez de decir «conflicto».

El índice sigue estando, porque la pantalla es una comodidad y la base es la garantía.

### El testigo de un solo uso se consume en **una sola operación**

Un testigo usado **no se borra** —se perdería el rastro— así que lleva su marca de usado. Y ahí hay
una carrera real: dos clics seguidos en el mismo enlace, o el enlace abierto en dos pestañas.

**Leer y luego escribir no vale**: los dos leen «sin usar» y los dos siguen adelante.

```sql
UPDATE crm.customer_tokens
   SET used_at = now()
 WHERE customer_token_id = @id
   AND used_at IS NULL
   AND expires_at > now()
```

**Se actúa solo si esa operación afectó a una fila.** Cero filas significa «ya estaba usado o
caducó», y las dos son la misma respuesta para quien lo abrió.

**Y se verifica por el efecto, no leyendo el código:** dos peticiones a la vez con el mismo testigo,
**una funciona y la otra no.**

### `contact_messages` entra en la migración inicial

`ARQUITECTURA_MODULAR.md` ya asignaba `contact_messages` a M04 y declaraba su
FK interna hacia `customers`. Su ausencia del SPEC era una omisión, no una
decisión de retirarla.

Entra ahora, mientras la ventana de esquema sigue abierta, en vez de convertir
una tabla conocida desde arquitectura en una migración posterior.

No replica: ADR-017 deja la captación del lado WEB. `customer_id` es opcional
porque un mensaje puede llegar antes de que exista una ficha.

### La búsqueda por nombre quita diacríticos antes del stemming

`core.es_search` expresa correctamente que un nombre se busca sin exigir
mayúsculas ni tildes, pero PostgreSQL 16 no permite LIKE/ILIKE sobre esa
colación no determinista y la configuración `spanish` de texto completo no
elimina diacríticos: «Peña» produce `peñ` y «pena» produce `pen`.

CRM crea `crm.spanish_unaccent`, copia de `pg_catalog.spanish` con el
diccionario `unaccent` delante de `spanish_stem`. Así la búsqueda conserva
stemming español y además ignora diacríticos.

La extensión `unaccent` es compartida y no se elimina al desinstalar CRM;
la configuración `crm.spanish_unaccent` sí pertenece al módulo y desaparece
con su schema.
---

### Criterio 5 — el espacio final no choca con `uq_customers_email` — **FAIL** (22 sep 2026)

**Base comprobada:** `a7416aece5117433ff5d1ab96d69cc24120065ac`.

El criterio 5 de `SPEC.md` §9 exige demostrar, a nivel de base de datos, que una
segunda ficha con el mismo correo escrito con un espacio final choque con el
índice único `uq_customers_email`.

Se añadió la prueba durable
`CrmPersistenceTests.Test14b_correo_con_espacio_final_choca_en_uq_customers_email`.
La prueba evita deliberadamente las normalizaciones de la aplicación:

1. inserta por SQL directo `espacio-final@ejemplo.pe`;
2. intenta insertar por SQL directo `espacio-final@ejemplo.pe `, con un espacio final;
3. exige `23505 unique_violation` de `uq_customers_email`.

**Resultado observado:** PostgreSQL aceptó el segundo `INSERT`. xUnit informó
`Assert.Throws() Failure: No exception was thrown`. La suite
`Sillar.Modules.Crm.Tests` terminó con **93 pruebas: 92 PASS y 1 FAIL**, siendo
este caso el único fallo. La puerta se detuvo correctamente en
`pruebas del backend`.

**Conclusión:** el criterio 5 queda **FAIL** tal como está escrito. El índice
`uq_customers_email` no constituye por sí solo una barrera contra una variante
que difiere únicamente por espacio final.

Esto no implica que las vías normales de la aplicación estén insertando hoy
esa variante. Las vías de aplicación que escriben `crm.customers.email`
verificadas en esta base son:

- **Registro público** — `CustomerRegistrationService.cs:38-40,63`:
  aplica `Trim()` y normalización NFC antes de escribir.
- **Alta desde el panel** — `CustomerAdminService.cs:169-189`:
  escribe `request.Email!.Trim()`.
- **Edición desde el panel** — `CustomerAdminService.cs:228-258`:
  escribe `request.Email!.Trim()`.
- **Edición del perfil propio** — `CustomerProfileService.cs:82-102`:
  escribe `request.Email!.Trim()`.
- **Invitación** — `CustomerAdminService.cs:458` y
  `CustomerAccountTokenService.cs:107+`: **no escribe el correo**; usa el
  valor ya guardado en la ficha para emitir la invitación.

**No se corrige aquí.** Por instrucción del cierre de M04, decidir si debe
cambiar el criterio, la colación/índice o alguna otra barrera corresponde al
líder técnico después de ver esta evidencia.

**Detención de la unidad:** al haberse revelado un fallo real, no se avanzó
con las nuevas pruebas de los criterios 1, 13 y 17, no se ejecutaron las dos
puertas consecutivas del criterio 19 y no se añadió todavía la nota histórica
de M02 en `PENDIENTES.md`.
---

### Cierre parcial M04 — criterios 5, 13 y 17 tras decisión del líder técnico — 22 sep 2026

**Base de trabajo:** `01f7e42bb6ec25b1687094c2b4138a86d653bec8`.

#### Criterio 5 — **PASS tras corrección autorizada**

El líder técnico decidió el 22 de septiembre de 2026 que **la base de datos
es la autoridad** para impedir blancos al principio o al final de
`crm.customers.email`.

La enumeración se hizo en ejecución con **.NET 10.0.10** usando
`char.IsWhiteSpace` sobre todo `char`. El conjunto obtenido fue de
**25 caracteres**:

`U+0009`, `U+000A`, `U+000B`, `U+000C`, `U+000D`, `U+0020`,
`U+0085`, `U+00A0`, `U+1680`, `U+2000`, `U+2001`, `U+2002`,
`U+2003`, `U+2004`, `U+2005`, `U+2006`, `U+2007`, `U+2008`,
`U+2009`, `U+200A`, `U+2028`, `U+2029`, `U+202F`, `U+205F`,
`U+3000`.

`U+200B` devolvió `char.IsWhiteSpace == false` y se conserva como caso
válido: PostgreSQL no debe rechazar más de lo que `String.Trim()` recorta.

La migración
`20260922115958_CrmCustomersEmailTrimAuthority` añade
`ck_customers_email_sin_blancos_en_bordes`. Tanto el precheck como el
`CHECK` comparan bajo **`COLLATE "C"`**. La migración cuenta primero las
filas incompatibles y, si encuentra alguna, falla con `23514`, informa la
cantidad y entrega una consulta para encontrarlas; **no modifica ningún
correo**.

`Test14b_base_rechaza_todos_los_blancos_que_dotnet_trim_recorta` inserta
por SQL directo y demuestra las tres direcciones:

- un correo limpio pasa;
- U+200B al inicio y al final pasa;
- los 25 `char.IsWhiteSpace` son rechazados tanto al inicio como al final
  con `ck_customers_email_sin_blancos_en_bordes`.

`Test14c_migracion_falla_y_no_corrige_correos_preexistentes` reproduce una
instalación anterior con un correo terminado en U+00A0: la migración falla,
anuncia **1 fila**, muestra cómo localizarla y el valor queda intacto.

**Barrera deliberada.** Se quitó temporalmente únicamente
`AddCheckConstraint` de la migración, manteniendo el resto. Entre
**2026-09-22 08:55:12 -0500 America/Lima** y **2026-09-22 08:57:53 -0500 America/Lima**, la puerta quedó roja
específicamente en
`Test14b_base_rechaza_todos_los_blancos_que_dotnet_trim_recorta` porque no
se lanzó la excepción esperada. El fichero de migración se restauró
byte a byte antes de continuar. Esto demuestra que la prueba protege la
barrera y no pasa por accidente.

#### Criterio 13 — **PASS**

Hay evidencia durable en dos niveles:

- `CustomerPasswordExposureTests` afirma que los contratos públicos de
  respuesta de CRM no exponen propiedades `Password` ni `Hash`.
- `CierreM04SeguridadYCorreoTests` registra una contraseña centinela mediante
  el host real y afirma que no aparece en la respuesta HTTP, en
  `core.audit_log` ni en stdout/stderr del proceso.

Las pruebas focales pasaron con la restricción del criterio 5 restaurada.

#### Criterio 17 — **PASS**

`CierreM04SeguridadYCorreoTests` registra un cliente con SMTP sin configurar.
El registro HTTP sigue funcionando; después espera el `email_send` de
**ese correo concreto** en `core.audit_log`, inicia sesión como
`super_admin`, consulta `GET /api/admin/audit` y exige que quien administra
vea exactamente el mismo fallo de envío.

La prueba focal pasó con el host y PostgreSQL reales.

**Criterios todavía fuera de esta unidad:** el 1 espera la decisión de JP
sobre un capturador de correo; el 19 se comprueba después, sobre un SHA
limpio y fijo, con dos puertas consecutivas.
