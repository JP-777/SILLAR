# Catálogo de hallazgos H01–H29 — Fase 1

**Creación:** 21 de septiembre de 2026, 11:15:03 -05 — America/Lima
**Última verificación:** 21 de septiembre de 2026, 11:41:27 -05:00 — America/Lima
**Commit del producto verificado:** `d79f4635851eb42678777bfa1589102672add562`
**Fuente primaria:** informe de verificación manual del panel del 11 de septiembre de 2026, dirigido por el líder técnico y ejecutado/observado por JP.
**Objeto:** dejar durable en el repositorio la disposición de los 29 hallazgos utilizados para el cierre de Fase 1.

## 1. Resultado

| Disposición | Cantidad |
|---|---:|
| Corregido | 24 |
| Deuda aceptada | 4 |
| Diferido | 1 |
| **Total** | **29** |

Un hallazgo se marca **Corregido** únicamente cuando el commit que materializa su corrección pertenece a la historia de `main`. Antes de cerrar este documento se comprobará cada SHA citado mediante:

`git merge-base --is-ancestor <sha> d79f4635851eb42678777bfa1589102672add562`

Los hallazgos sin commit correctivo son **H09, H16, H23, H24 y H28**. H09, H16, H24 y H28 son deudas aceptadas; H23 queda diferido con disparador explícito.

## 2. Enmiendas posteriores que gobiernan este catálogo

1. Son **ocho familias, A–H**, no seis.
2. **H19:** la sección se queda como «Usuarios». El alta usa «Nuevo usuario» y «Crear usuario». La propuesta original de renombrar la sección a «Administradores» queda retirada.
3. **H14:** decisión de JP. Tanto las fechas mostradas como las escritas siguen `es-PE`, día/mes/año.
4. **H09:** no causó H21. Permanece como riesgo latente.
5. **H29:** JP adoptó el 21 de septiembre de 2026 el comportamiento integrado en `5cf1dea87a4dc0250e49e7fe424d08ffc9f9e6f9`, incluido el umbral de 6 destinos y el tema dentro de la cuenta en pantalla estrecha.

---

## FAMILIA A · Lenguaje de máquina mostrado a personas

### H-12 · Claves de base de datos en la tarjeta de inicio

**Enunciado.** La tarjeta de configuración de Inicio mostraba la clave técnica del ajuste aun cuando la misma respuesta del API ya llevaba su descripción humana.

**Disposición: Corregido.** Commit `e057141c0dfe8b02e3b1b9b7f6c2c78ebcd51d55`. Inicio reutiliza la descripción humana en lugar de presentar la clave de configuración como texto principal.

### H-15 · Códigos técnicos en los filtros de módulo

**Enunciado.** Archivos y Auditoría presentaban códigos internos como `core` y `catalog` aunque `AdminModule.displayName` ya proporcionaba el nombre visible.

**Disposición: Corregido.** Commit `e057141c0dfe8b02e3b1b9b7f6c2c78ebcd51d55`. Los consumidores comparten la traducción `code → displayName`; el código continúa siendo el valor interno enviado al API.

### H-20 · Dos diccionarios de roles que ya divergen

**Enunciado.** La tabla de usuarios y el formulario mantenían traducciones independientes para los mismos roles y ya discrepaban en `super_admin`.

**Disposición: Corregido.** Commit `e057141c0dfe8b02e3b1b9b7f6c2c78ebcd51d55`. El vocabulario visible de roles pasa a tener un único dueño.

### H-19 · «Usuarios» frente al vocabulario del alta

**Enunciado original.** El informe observó que el menú decía «Usuarios» mientras el botón y el formulario hablaban de «administrador», y propuso renombrar la sección a «Administradores».

**Enmienda del líder.** La sección **se queda como «Usuarios»**. Solo cambia el lenguaje del alta a **«Nuevo usuario»** y **«Crear usuario»**. La propuesta original de «Administradores» queda retirada.

**Disposición: Corregido.** Commit `e057141c0dfe8b02e3b1b9b7f6c2c78ebcd51d55`.

---

## FAMILIA B · El arranque es una foto fija

### H-02 · `migrationsPending` se calculaba y el frontend lo ignoraba

**Enunciado.** El backend distinguía una instalación pendiente de unas migraciones pendientes, pero el arranque del frontend solo interpretaba `setupRequired`.

**Disposición: Corregido.** Commit `ffda6b255aa2e40c8b35de683d8c292feef05e52`.

### H-11 · «Ir al acceso» no hacía nada

**Enunciado.** Tras completar la instalación, `navigate('/login')` cambiaba la URL pero el árbol seguía fijado en `SetupPage` porque la fase de arranque no se reevaluaba.

**Disposición: Corregido.** Commit `ffda6b255aa2e40c8b35de683d8c292feef05e52`. H02 y H11 se corrigieron como dos caras del mismo problema.

### H-21 · El middleware de autenticación consultaba una tabla que podía no existir

**Enunciado.** Con una cookie previa y una base vacía, el middleware intentaba consultar `core.admin_sessions` antes de que `/api/setup/status` pudiera responder y producía `42P01`.

**Disposición: Corregido.** Commit `2f59ebc7a7b3ccd266b76b08344d70f9ee61779b`. La autenticación degrada a `NoResult` únicamente cuando falta exactamente la relación que le pertenece; otros errores siguen subiendo.

---

## FAMILIA C · Errores que no explican, o culpan al inocente

### H-01 · El `detail` útil no llegaba a las pantallas

**Enunciado.** `ApiError` almacenaba `detail`, pero la presentación elegía el título y descartaba información accionable producida por el backend.

**Disposición: Corregido.** Commit `0c6107b8db2545ee5a6ae7c4139e341165902535`. La composición de la frase del servidor tiene un único dueño, conserva detalle presentable y evita propagar texto sensible o del framework.

### H-07 · Texto en inglés en una pantalla en español

**Enunciado.** Un 500 podía presentar el título de ASP.NET Core «An error occurred while processing your request.».

**Disposición: Corregido.** Commit `0c6107b8db2545ee5a6ae7c4139e341165902535`. Los 500 dejan fuera el texto del framework y reciben una frase propia en español. Es un efecto cubierto por la corrección de H01.

### H-03 · El título del 503 culpaba al instalador

**Enunciado.** Distintas causas de 503 se presentaban como si todas fueran una negativa del instalador.

**Disposición: Corregido.** Commit `22ed79c9ec27e34f3b58f3dd8939986dd7da2213`.

### H-08 · Un reinicio planificado se presentaba como fallo

**Enunciado.** Durante el reinicio deliberado de activación, un estado transitorio podía acabar presentado como avería.

**Disposición: Corregido.** Commit `22ed79c9ec27e34f3b58f3dd8939986dd7da2213`.

### H-22 · `PlatformErrorPage` aconsejaba sobre una causa que no ocurrió

**Enunciado.** Una pantalla genérica atribuía el fallo a módulos o reinicio aunque el error hubiese ocurrido antes de consultar capacidades; incluso ofrecía «Reintentar» en estados que no podían corregirse reintentando.

**Disposición: Corregido.** Commit `22ed79c9ec27e34f3b58f3dd8939986dd7da2213`.

### H-28 · Una sección rota se presentaba como operativa

**Enunciado.** Con el schema de CRM ausente, Clientes podía conservar título, acciones y formulario mientras la lectura mostraba un error. El informe dejó abierta la cuestión de si intentar crear un cliente en ese estado podía provocar pérdida de trabajo.

**Disposición: Deuda aceptada.** Aceptada por **JP el 21 de septiembre de 2026**. **Motivo:** desde H27 (`e60416c`), la activación exige el esquema y la instalación migra todos los módulos desplegados, así que el estado de H28 no se alcanza por el flujo normal. **Disparador:** reabrir si un módulo activo aparece sin su esquema por cualquier vía —restauración de copia, manipulación manual de la base o instalación anterior a `e60416c`—, o al volver a tocar la presentación de errores de módulo.

---

## FAMILIA D · Barreras que callan

### H-27 · Activar un módulo confirmaba éxito sin comprobar que podía funcionar

**Enunciado.** Un módulo podía quedar activo aun cuando su schema no existía; el fallo aparecía después al entrar a su funcionalidad.

**Decisión ratificada por JP.** La activación **comprueba y se niega** si falta el schema; el **instalador aplica** las migraciones.

**Disposición: Corregido.** Commit `e60416c3a706c10597cea5e2ba3d39e1e0b96876`.

### H-09 · `GetStateAsync` atrapa una sola excepción

**Enunciado.** La ruta de inspección contempla específicamente `42P01`; el informe la señaló como riesgo latente ante otras formas de fallo.

**Enmienda del líder.** H09 **no causó H21**. El log de H21 mostró `42P01`, que esa captura sí reconoce; su causa estaba en el middleware de autenticación.

**Disposición: Deuda aceptada.** Aceptada por el **líder técnico** como riesgo latente, según la enmienda posterior transmitida por JP el 21 de septiembre de 2026. No existe commit correctivo porque no se decidió ampliar la captura sin un caso real que lo justificara.

---

## FAMILIA E · La documentación describe un entorno que ya no existe

### H-26 · El procedimiento documentado preparaba solo parte de los schemas

**Enunciado.** `DEMOSTRACION.md` describía CORE + Catálogo aunque el producto ya desplegaba migraciones propias de CMS y CRM.

**Disposición: Corregido.** Commit `fefa7237e212ab37ed6986acbf29177de9e2d800`. La guía vigente prepara los módulos reales desplegados y separa migraciones, seed mínimo y datos de demostración.

### H-25 · El producto recomendaba una herramienta que la máquina no tenía

**Enunciado.** Los mensajes y documentación indicaban `dotnet ef`, pero el repositorio no fijaba `dotnet-ef` ni daba una restauración reproducible.

**Disposición: Corregido.** Commit `fefa7237e212ab37ed6986acbf29177de9e2d800`. El repositorio fija la herramienta y documenta `dotnet tool restore`.

### H-04 · `DEMOSTRACION.md` no incluía `dotnet restore`

**Enunciado.** Una máquina nueva podía seguir el procedimiento y llegar a EF sin haber restaurado las dependencias de la solución.

**Disposición: Corregido.** Commit `fefa7237e212ab37ed6986acbf29177de9e2d800`.

### H-05 · Puerto y contraseña duplicados en `.env`

**Enunciado.** La identidad de PostgreSQL estaba expresada como variables independientes y también dentro de `ConnectionStrings__Default`, permitiendo que ambas representaciones divergieran.

**Disposición: Corregido.** Cadena de corrección:

- `fefa7237e212ab37ed6986acbf29177de9e2d800`: `scripts/estrenar.mjs` deriva identidad, base y puertos de la worktree y genera la cadena correspondiente.
- `f2188376338b1f33c7b03e105ffdb59464a99c4a`: la plantilla falla en cerrado si `POSTGRES_PASSWORD` y el `Password=` de `ConnectionStrings__Default` divergen.

La arquitectura adoptada para Fase 1 es la opción A. Una refactorización distinta solo se reconsiderará ante una instalación real fuera de desarrollo.

### H-06 · Cambiar la contraseña de `.env` no cambia un clúster ya creado

**Enunciado.** `POSTGRES_PASSWORD` se usa al inicializar el volumen; modificarlo después no rota la contraseña almacenada por PostgreSQL.

**Disposición: Corregido.** Commit `fefa7237e212ab37ed6986acbf29177de9e2d800`. La documentación y el generador explican la semántica del volumen y advierten que `down -v` borra datos y no es un mecanismo de rotación.

### H-23 · Lo recordado y lo registrado no coincidieron

**Enunciado.** Durante la revisión, el estado efectivo del sistema y lo que se creía haber ejecutado no coincidieron con lo que mostraban los registros. No se estableció la causa.

**Disposición: Diferido.** No se declara resuelto ni «no reproducible». Su disparador oficial está en [`PENDIENTES.md`](./PENDIENTES.md) §21:

> reabrir inmediatamente si vuelve a aparecer una discrepancia entre el estado efectivo del sistema y lo que indican los registros.

La segunda aparición debe aportar la evidencia que la primera no dejó.

### H-10 · Era imposible saber si la instalación había terminado con éxito

**Enunciado.** El procedimiento carecía de una señal positiva y explícita de que el host había reiniciado en modo normal.

**Disposición: Corregido.** Commit `fefa7237e212ab37ed6986acbf29177de9e2d800`. Después del setup se verifica `GET /api/setup/status` y `setupRequired: false`.

---

## FAMILIA F · Localización y formato

### H-14 · Fechas `mm/dd/yyyy` en una interfaz española

**Enunciado.** Las tablas mostraban fechas `es-PE`, pero los filtros dependían de `<input type="date">` y por tanto del locale del navegador, pudiendo pedir mes/día en la misma pantalla que mostraba día/mes.

**Enmienda de JP.** Las fechas **que se escriben y las que se muestran** siguen `es-PE`, **día/mes/año**.

**Disposición: Corregido.** Commit `e057141c0dfe8b02e3b1b9b7f6c2c78ebcd51d55`. Se usa análisis y formateo explícitos `dd/mm/yyyy`, incluida validación de fechas inválidas y pruebas frente a zonas horarias distintas.

---

## FAMILIA G · Presentación en pantalla estrecha

### H-29 · Barra superior y navegación lateral a 390 px

**Enunciado.** A 390 px la barra superior amontonaba identidad y acciones, mientras la navegación lateral desplegaba todos los destinos antes del contenido.

**Disposición: Corregido.** Commit `5cf1dea87a4dc0250e49e7fe424d08ffc9f9e6f9`.

**Decisión de JP del 21 de septiembre de 2026:** se adopta y acepta tal como quedó integrado:

- navegación estrecha mediante superficie móvil;
- con **6 o más destinos visibles** se usan grupos plegables; con 5 o menos permanecen planos;
- la cuenta se separa de la navegación;
- el **tema vive dentro de la cuenta** en pantalla estrecha;
- la barra lateral de escritorio se conserva;
- movimiento reducido elimina desplazamientos.

### H-16 · Tablas con desplazamiento interno a 390 px

**Enunciado.** Usuarios y Auditoría necesitan desplazamiento horizontal interno en pantalla estrecha.

**Disposición: Deuda aceptada.** Aceptada por el **líder técnico en el informe original del 11 de septiembre de 2026**: el desplazamiento interno se consideró comportamiento correcto, no avería. Convertir eventualmente esas tablas en tarjetas es deuda de presentación y no condición de Fase 1.

---

## FAMILIA H · Menores y latentes

### H-13 · La tarjeta de Inicio describía un producto anterior

**Enunciado.** Inicio afirmaba que las pantallas de administración todavía no existían aunque ya estaban construidas.

**Disposición: Corregido.** Corrección principal en `e057141c0dfe8b02e3b1b9b7f6c2c78ebcd51d55`, con ajuste final en `2bc95e9cdadde7b65b3ee5e48604bf5a9f0c3261`.

### H-17 · `useFocusTrap` filtraba ocultos de forma distinta al enfocar inicialmente

**Enunciado.** El recorrido con Tab descartaba controles ocultos mediante `offsetParent`, pero la selección inicial no aplicaba el mismo criterio.

**Disposición: Corregido.** Commit `5a8ff13f05615304bd4cc8eb634d0e312ec7129a`.

### H-18 · La tienda pública ofrecía el panel administrativo a desconocidos

**Enunciado.** La portada pública mostraba un enlace al panel aun sin sesión administrativa. No saltaba la autenticación, pero exponía una acción administrativa a clientes.

**Disposición: Corregido.** Commit `e057141c0dfe8b02e3b1b9b7f6c2c78ebcd51d55`. El CTA administrativo depende de la sesión.

### H-24 · `libgssapi_krb5.so.2` aparece como «Error» en un arranque sano

**Enunciado.** Npgsql intenta cargar soporte Kerberos que la imagen no trae y produce ruido de error aunque el host funcione.

**Disposición: Deuda aceptada.** Aceptada por el **líder técnico en el informe original del 11 de septiembre de 2026**, donde se clasificó expresamente como inofensiva. No se introdujo una dependencia de sistema únicamente para silenciar ese ruido.

---

## 3. Hallazgos sin commit correctivo

| Hallazgo | Disposición |
|---|---|
| H09 | Deuda aceptada: riesgo latente; no causó H21 |
| H16 | Deuda de presentación aceptada; el scroll interno es correcto |
| H23 | Diferido con disparador en `PENDIENTES.md` §21 |
| H24 | Ruido ambiental inofensivo aceptado |
| H28 | Deuda aceptada por JP el 21 de septiembre de 2026; reabrir si un módulo activo aparece sin su esquema por cualquier vía, o al volver a tocar la presentación de errores de módulo |

## 4. Contradicciones y enmiendas entre el informe original y el repositorio

### 4.1 · Número de familias

El texto introductorio del informe decía **seis**, pero el propio informe contiene las familias A, B, C, D, E, F, G y H. La decisión posterior del líder fija el número correcto en **ocho**.

### 4.2 · H19

El informe original proponía renombrar «Usuarios» a «Administradores». El repositorio final conserva «Usuarios» y usa «Nuevo usuario» / «Crear usuario». La enmienda posterior del líder retira la propuesta original, por lo que el árbol coincide con la decisión vigente.

### 4.3 · H14

El informe original documentó la dependencia del locale del navegador para `<input type="date">`. La decisión posterior de JP exige `es-PE`, día/mes/año, también para escritura. El árbol final materializa esa decisión.

### 4.4 · H09

El propio informe terminó constatando que H09 **no causó H21**. La enmienda del líder eleva esa constatación a disposición oficial: riesgo latente aceptado.

### 4.5 · H28

El informe dejó abierta una posible pérdida de trabajo al escribir con el schema ausente. La disposición vigente es la decidida posteriormente por JP: desde H27 (`e60416c`), la activación exige el esquema y la instalación migra todos los módulos desplegados, por lo que H28 no se alcanza por el flujo normal. Se reabre si un módulo activo aparece sin su esquema por restauración de copia, manipulación manual de la base o una instalación anterior a `e60416c`, o al volver a tocar la presentación de errores de módulo.

### 4.6 · H29

El informe detectó el problema pero dejó el patrón concreto a Diseño. Después se integró una solución en `5cf1dea87a4dc0250e49e7fe424d08ffc9f9e6f9`, y JP la adoptó expresamente el 21 de septiembre de 2026, incluido el umbral de seis destinos y el tema dentro de la cuenta.

## 5. Verificación de commits correctivos

Antes del commit final de este documento se deben comprobar contra
`d79f4635851eb42678777bfa1589102672add562` los siguientes SHAs mediante
`git merge-base --is-ancestor`:

- `0c6107b8db2545ee5a6ae7c4139e341165902535`
- `ffda6b255aa2e40c8b35de683d8c292feef05e52`
- `22ed79c9ec27e34f3b58f3dd8939986dd7da2213`
- `fefa7237e212ab37ed6986acbf29177de9e2d800`
- `f2188376338b1f33c7b03e105ffdb59464a99c4a`
- `e057141c0dfe8b02e3b1b9b7f6c2c78ebcd51d55`
- `2bc95e9cdadde7b65b3ee5e48604bf5a9f0c3261`
- `5a8ff13f05615304bd4cc8eb634d0e312ec7129a`
- `2f59ebc7a7b3ccd266b76b08344d70f9ee61779b`
- `e60416c3a706c10597cea5e2ba3d39e1e0b96876`
- `5cf1dea87a4dc0250e49e7fe424d08ffc9f9e6f9`

Ningún hallazgo marcado **Corregido** puede cerrarse si cualquiera de estas comprobaciones falla.
