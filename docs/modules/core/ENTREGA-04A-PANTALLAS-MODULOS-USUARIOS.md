# CORE · Entrega 4a — Pantallas de módulos y usuarios

- **Módulo:** `core` · **Estado:** Aprobado
- **Depende de:** F-08 · entregas 2, 2.1 y 3 · `MARCA.md` §6 · `sillar-design-system.html`
- **Continúa en:** entrega 4b — configuración, auditoría y medios

Primera entrega de pantallas de administración. Llena el menú que F-08 dejó vacío.

**Se parte en 4a y 4b a propósito.** Estas dos pantallas son las que tienen reglas de negocio de verdad; las tres de 4b son formularios, una tabla y una subida. Los patrones que se fijen aquí se repetirán tres veces después: si salen mal, salen mal multiplicados.

---

## 0. Requisito previo — módulos de mentira

Aprobado hace dos entregas y aún sin construir. Ahora bloquea: **la pantalla de módulos no se puede probar con un solo módulo**, y CORE es el único que existe.

Hace falta un grafo de módulos ficticios que ejercite los cuatro estados: activo, inactivo, bloqueado por dependencia dura y núcleo. Con dependencias duras y blandas entre ellos.

Dos usos:

1. **Pruebas automatizadas.** Cierra los tres criterios de la entrega 3 que quedaron sin verificar —el ciclo activar → reiniciar → ruta nueva— y da protección contra regresiones al validador de grafo, que es el mecanismo que sostiene toda la arquitectura.
2. **Revisión visual.** La pantalla debe poder verse con un grafo realista antes de que exista M01.

**Restricción innegociable: los módulos de mentira no pueden existir en un despliegue de producción.** El mecanismo lo propone quien implemente; la condición es esa.

---

## 1. Pantalla de módulos

Es la pantalla más importante del producto. No es una tabla de configuración: es donde el negocio ve sus bloques, los que tiene, los que podría tener y por qué uno está bloqueado. Sostiene el argumento de venta.

El componente ya está diseñado en `docs/sillar-design-system.html` — la tarjeta con interruptor, con el caso de Seguimiento bloqueado por faltarle Órdenes de Servicio.

### Qué muestra cada tarjeta

Código, nombre, descripción, versión, estado y dependencias. La descripción es obligatoria en `core.modules` precisamente para que esta tarjeta nunca salga en blanco.

### Los cuatro estados

| Estado | Interruptor | Distintivo | Qué dice |
|---|---|---|---|
| **Activo** | encendido | Activo | — |
| **Inactivo** | apagado | Inactivo | Qué requiere, si algo |
| **Bloqueado** | deshabilitado | Bloqueado | **Qué le falta, nombrado.** No «requisitos no cumplidos» |
| **Núcleo** | ausente | Núcleo | No se puede desactivar |

CORE no lleva interruptor. No es un interruptor deshabilitado: es que no lo tiene, porque desactivarlo no es una operación que exista.

Si `expires_at` tiene valor, se muestra. **No se evalúa**: el control de vencimientos es de la fase 5 y fingir que ya funciona sería peor que no mostrarlo.

### Activar o desactivar

```
1. El usuario pulsa el interruptor.
2. Diálogo de confirmación que dice explícitamente que el sistema se
   reiniciará unos segundos. El botón nombra la acción: «Activar Catálogo».
3. Al confirmar, se envía la petición.
4. Respuesta 200 → el frontend entra en estado de reconexión global (F-08 §8).
5. Respuesta 409 → no se reinicia nada; se muestra qué lo impide.
```

**El aviso del reinicio no es un detalle de cortesía.** Es la única operación del panel que interrumpe el servicio, incluida la web pública del negocio. Descubrirlo mientras ocurre es la diferencia entre una pausa y un susto.

### El 409

Nunca se muestra como «error». Se muestra como una frase que dice qué lo impide y qué se puede hacer:

> **No se puede desactivar Servicios.** Seguimiento de Servicios depende de él y está activo. Desactiva primero Seguimiento de Servicios.

Y el nombre del módulo que bloquea lleva a su tarjeta. **Nada de desactivación en cascada** (entrega 3): el sistema nombra el obstáculo y la persona ordena.

### Con un solo módulo

Con solo CORE, la pantalla debe verse intencionada, no rota. Una tarjeta de núcleo y un texto que explique que los módulos disponibles aparecerán aquí conforme se contraten.

---

## 2. Pantalla de usuarios

Solo `super_admin`. **La entrada de menú no existe para los demás roles**, no aparece deshabilitada.

Esto añade una regla a la composición del menú de F-08: **se construye desde las capacidades y además se filtra por rol.** Un `editor` no ve «Usuarios».

### Listado

Tabla con nombre, correo, rol, estado y último acceso. Los inactivos se muestran atenuados, no se ocultan: son eliminación lógica y desaparecerlos haría creer que se borraron.

### Crear y editar

Formulario en panel lateral. Nombre, correo, rol, teléfono opcional y, al crear, contraseña.

- Los requisitos de la contraseña se muestran **antes** de escribir. Doce caracteres mínimo.
- La contraseña la fija el `super_admin`. No hay contraseñas generadas ni envío por correo mientras no exista envío de correo.
- Cambiar el rol de alguien **revoca sus sesiones**, y el diálogo lo dice antes de confirmar.

### Desactivar

Baja lógica. El diálogo advierte que se cerrarán sus sesiones abiertas.

Tres rechazos que la interfaz debe manejar como frases, no como errores:

| Caso | Qué se muestra |
|---|---|
| Último `super_admin` activo | Debe quedar al menos un administrador principal activo |
| Uno mismo | No puedes desactivar tu propia cuenta ni cambiar tu propio rol |
| Sin permiso | Pantalla de acceso denegado, no un menú roto |

### Sesiones

Listado de sesiones activas con dispositivo, dirección y último uso, y la posibilidad de revocar una. Es lo que hace útil que las sesiones vivan en base de datos, y lo que un negocio con personal rotando va a agradecer.

### Cambiar la propia contraseña

Accesible desde el menú de usuario, no desde la administración de usuarios: es una acción sobre uno mismo.

Exige la contraseña actual y **avisa de que se cerrarán las demás sesiones** antes de confirmar. Si alguien cambia su contraseña es porque sospecha; dejar vivas las otras sesiones anularía el gesto, y no decirlo lo convierte en una sorpresa.

---

## 3. Patrones que esta entrega fija

Son la razón de partir el trabajo. Todo lo de 4b los reutiliza.

**Tabla.** Paginación, orden, estado de carga, estado vacío con acción, y fila atenuada para lo dado de baja.

**Formulario.** Panel lateral para formularios cortos; página completa solo si no cabe. Validación al enviar, no en cada tecla. Errores del servidor situados en su campo cuando el servidor dice cuál.

**Confirmación destructiva.** Diálogo que enuncia la consecuencia real —«se cerrarán sus sesiones», «el sistema se reiniciará»— y un botón que **nombra la acción**. Nunca «Aceptar».

**Errores tipados a frases.** Cada tipo del cliente HTTP tiene su tratamiento: `Conflict` es una frase que explica el obstáculo, `Forbidden` es una pantalla, `Network` lo absorbe la reconexión. **Ningún «Ha ocurrido un error».**

**Confirmación de escritura.** Aviso breve y no intrusivo. Sin diálogos que haya que cerrar para seguir trabajando.

**Estados.** Cargando, vacío, error y sin permiso. Los cuatro se diseñan; ninguno se deja al azar.

---

## 4. Criterios de aceptación

Leyenda: **[x]** verificado · **[~]** verificado por lectura del código · **[ ]** requiere navegador.

**Requisito previo**

- [x] Existe un grafo de módulos de mentira que ejercita los cuatro estados
- [x] Los tres criterios pendientes de la entrega 3 quedan verificados
- [x] Los módulos de mentira no pueden aparecer en un despliegue de producción

### El mecanismo elegido

`Sillar.Modules.Demo`, un proyecto aparte con **tres barreras**:

| Barrera | Qué garantiza |
|---|---|
| `ProjectReference` condicionada a `Configuration == Debug` | La DLL no existe en una publicación Release. El `Dockerfile` publica con `-c Release` |
| Todo el contenido bajo `#if DEBUG` | Aunque alguien añadiera la referencia en Release, el ensamblado compilaría vacío |
| `Modules:IncludeDemoModules`, por defecto `false`, y solo respetada en Development | Ni en desarrollo aparecen sin pedirlo |

Las dos primeras son de tiempo de compilación: no es que se filtren, es que no pueden estar. La tercera además hace ruido — el descubrimiento registra un aviso por cada módulo de demostración que carga.

**Comprobado, no prometido.** `dotnet publish -c Release` produce cuatro ensamblados —`Sillar.Api`, `Sillar.Core`, `Sillar.Core.Contracts`, `Sillar.Shared`— y ninguno es de demostración.

El grafo son seis módulos calcados del catálogo real: `demo_catalog`, `demo_crm`, `demo_sales` (dura de catálogo, blanda de clientes), `demo_services`, `demo_service_orders` y `demo_tracking`. Reproduce el caso del sistema de diseño: Seguimiento bloqueado porque le falta Órdenes de Servicio.

### Criterios de la entrega 3 que cierra

| Criterio | Resultado |
|---|---|
| Activar con dependencia dura inactiva → 409 nombrándola | `'demo_sales' no puede activarse porque necesita módulos que están inactivos: demo_catalog` |
| Desactivar con dependiente activo → 409 nombrando quién bloquea | `'demo_catalog' no puede desactivarse porque estos módulos activos dependen de él: demo_sales` |
| Dependencia blanda que no impide activar ni desactivar | `demo_sales` se activa con `demo_crm` inactivo; `demo_crm` se desactiva con `demo_sales` activo |
| `restart: required` con la bandera en `false` | Correcto, y el host sigue vivo |
| Tras el reinicio, `/api/capabilities` refleja el cambio | `core, demo_catalog, demo_sales` — coincide con lo activado |
| Sin desactivación en cascada | Tras el 409, `demo_sales` sigue activo |
| `activate` y `deactivate` auditados | Cuatro registros con su acción y su módulo |

Sigue sin verificarse el del contenedor que se relanza solo: necesita el CLI de Docker, que no está disponible en esta máquina.

**Módulos**

- [ ] Las cuatro variantes de tarjeta se ven correctas
- [~] CORE aparece sin interruptor, no con uno deshabilitado
- [~] Una tarjeta bloqueada **nombra** el módulo que le falta y enlaza a él
- [~] El diálogo de confirmación menciona el reinicio antes de confirmar
- [ ] Activar lleva al estado de reconexión y vuelve solo, con la sesión abierta
- [x] El 409 al desactivar nombra quién bloquea y no reinicia nada
- [x] No existe desactivación en cascada
- [~] Con solo CORE, la pantalla se ve intencionada

El API entrega los cuatro estados correctamente, comprobado a través del proxy de Vite: `core` como núcleo, `demo_catalog` y `demo_sales` activos, `demo_crm` y `demo_services` inactivos, y `demo_service_orders` y `demo_tracking` bloqueados con su `blockedBy` poblado. Ninguna descripción llega vacía.

**Usuarios**

- [~] La entrada de menú no existe para `admin` ni para `editor`
- [~] Acceder a la ruta directamente con otro rol da acceso denegado
- [~] Los requisitos de contraseña se ven antes de escribir
- [x] Desactivar advierte del cierre de sesiones y las cierra
- [x] Cambiar el rol de alguien revoca sus sesiones
- [x] El último `super_admin` no se puede desactivar, y el mensaje lo explica
- [x] Nadie puede desactivarse ni cambiarse el rol a sí mismo
- [x] Las sesiones se listan y se pueden revocar una a una
- [x] Cambiar la propia contraseña avisa del cierre de las demás sesiones y conserva la actual
- [~] Los usuarios inactivos se muestran atenuados, no ocultos

Las marcadas **[x]** son conducta del backend, verificada en la entrega 2; lo que 4a añade es la interfaz que la enuncia antes de confirmarla. La entrada de menú se filtra por rol en `visibleNavigation`, y la ruta lleva además `RequireRole` para que un acceso directo no llegue a pedir nada al servidor.

**Transversales**

- [x] Ningún mensaje dice «Ha ocurrido un error»
- [x] Ningún botón de confirmación dice «Aceptar»
- [x] Ningún componente contiene un color literal
- [ ] Todo es navegable con teclado, con foco visible
- [~] Los diálogos atrapan el foco y se cierran con Escape
- [ ] Correcto en tema claro y oscuro

`confirmLabel` no tiene valor por defecto: no se puede acabar con un «Aceptar» por descuido, porque el componente no compila sin él. Los tres usos actuales son «Cerrar sesión», «Desactivar usuario» y «Activar *nombre del módulo*».

---

## 5. Fuera de alcance

| Qué | Cuándo |
|---|---|
| Configuración, auditoría y medios | Entrega 4b |
| Recuperación de contraseña por correo | Cuando exista envío de correo |
| Permisos granulares por módulo | Cuando exista un caso real |
| Evaluación de `expires_at` | Fase 5 |
| Pruebas de interfaz automatizadas | Cuando los patrones se estabilicen, después de 4b |

---

## 4b. Cierre

- **Compilación:** `pnpm build` limpio, 83 módulos. Backend con 0 advertencias tras corregir cinco que el build incremental ocultaba desde la entrega 3b.
- **Sin migración** y **sin dependencias nuevas.**

### Los patrones que quedan fijados

Viven en `shared/` desde el primer uso, no dentro de la pantalla que los estrenó. 4b los reutiliza tal cual.

| Patrón | Dónde | Lo que resuelve |
|---|---|---|
| `Table` | `shared/ui/patterns.tsx` | Paginación, cargando, vacío con acción y fila atenuada para lo dado de baja |
| `Drawer` | ídem | Panel lateral con foco atrapado, cierre con Escape y foco devuelto al abridor |
| `ConfirmDialog` | ídem | Enuncia la consecuencia; `confirmLabel` **sin valor por defecto** |
| `Toasts` / `useToasts` | ídem | Aviso de escritura que no hay que cerrar |
| `describe()` | `shared/errors/messages.ts` | De error tipado a frase, con cuatro tratamientos: en línea, por campo, pantalla o silencio |
| `useResource` | `shared/hooks/` | Los cuatro estados: cargando, listo, error y sin permiso |

Dos decisiones dentro de esos patrones que conviene no deshacer:

1. **`describe()` usa la frase del servidor en los 409.** El backend ya las redacta para leerse —«no puede desactivarse porque estos módulos activos dependen de él: demo_sales»—. Reescribirlas en el frontend sería tener la misma frase en dos sitios y corregir solo una.
2. **Un fallo de red no cambia el estado de la pantalla.** `useResource` lo ignora, porque la reconexión global ya está encima explicándolo. Si lo tratara como error, la pantalla se vaciaría bajo la superposición y volvería en blanco al recuperarse.

### Lo que queda por mirar en un navegador

Las cuatro variantes de tarjeta, el ciclo activar → reconexión → vuelta, el recorrido con teclado y el tema oscuro. El código está escrito para cumplirlos, pero eso se comprueba usándolo.

Para verlo con el grafo completo:

```bash
cd backend  && Modules__IncludeDemoModules=true dotnet run --project Sillar.Api
cd frontend && pnpm dev
```
