# CORE · Entrega 3 — Activación de módulos, configuración y auditoría

- **Módulo:** `core` · **Estado:** Cerrado (14/08/2026, commit `4fe76b4`, 152 pruebas) · con una corrección pendiente, ver §2
- **Refina:** `SPEC.md` §6 (endpoints), §7 (arranque) y §8 (reglas 1, 2, 3, 10, 14, 15)
- **Decide sobre:** ADR-004

Esta entrega convierte la modularidad en algo operable desde el panel. Hasta ahora las activaciones solo existían como filas que nadie podía tocar sin SQL.

**Alcance:** consulta y cambio de activaciones, configuración del sitio y consulta de auditoría.

**Fuera de esta entrega, en la 3b:** gestión de medios. Se separa porque no comparte maquinaria con lo anterior, es la pieza más grande de CORE y nadie la necesita hasta que M01 o M02 tengan interfaz.

---

## 1. El problema del efecto en caliente

Es la decisión que condiciona todo lo demás y conviene resolverla antes de mirar los endpoints.

El SPEC §7 registra servicios y endpoints **únicamente de los módulos activos**, y eso ocurre una vez, al arrancar. Un `POST .../activate` no puede hacer aparecer rutas en el proceso vivo: escribir la fila de activación no cambia el enrutamiento ya construido.

**Decisión: el host se detiene tras el cambio y lo relanza el orquestador.** Es el mismo patrón que ya usa `POST /api/setup`.

Se descartó el registro dinámico de endpoints: obliga a pelear con el enrutamiento de ASP.NET Core y con el ciclo de vida del contenedor de servicios para una operación que un negocio ejecuta dos o tres veces en la vida de su instalación. Y se descartó responder 200 pidiendo un reinicio manual, porque encender y apagar módulos en vivo es literalmente el argumento de venta del producto; que requiera entrar por consola lo arruina.

### Cómo se detiene

1. Se valida y se escribe la activación en una transacción.
2. Se responde **200**, con el cuerpo descrito en §2.
3. Solo cuando la respuesta ha salido —`HttpContext.Response.OnCompleted`— se programa `IHostApplicationLifetime.StopApplication()` tras un margen breve.

El orden importa: detener el proceso antes de vaciar la respuesta deja al panel sin saber si la operación se hizo, y el reinicio le impide preguntarlo.

### En desarrollo no se detiene

En desarrollo el API corre con `dotnet run` y nadie lo relanza: detenerse dejaría el proceso muerto en cada prueba. La configuración `Modules:RestartAfterActivation` gobierna las dos conductas —`true` en el despliegue con contenedor, `false` en desarrollo, donde la respuesta indica que el reinicio queda pendiente y el proceso sigue en pie.

El contenedor del API necesita política de reinicio (`restart: unless-stopped`) en `docker-compose.yml`. Sin ella, activar un módulo apaga el sistema y no lo vuelve a encender. **Es el único punto donde esta decisión puede salir mal**, así que va comprobado en la entrega.

### Lo que sobrevive al reinicio

Las sesiones abiertas siguen siendo válidas: viven en `core.admin_sessions`, no en memoria. Y los tokens CSRF también, porque desde la entrega 2.1 se derivan de `installation_key` (ADR-012). Quien active un módulo vuelve a encontrar su sesión intacta al reconectar el panel. Con el diseño anterior, cada activación habría expulsado a todo el mundo.

**Consecuencia para F-08:** el panel necesita una pantalla de «reiniciando, reconectando», que reintenta `GET /api/capabilities` hasta que responde. Queda anotado aquí para que no aparezca como sorpresa al construir el frontend.

---

## 2. Activación de módulos

### `GET /api/admin/modules` · rol `admin`

Devuelve el catálogo completo, activos e inactivos, con lo necesario para pintar la pantalla que sostiene el argumento comercial:

```json
[
  {
    "code": "catalog",
    "displayName": "Catálogo de Productos",
    "description": "Categorías, productos, imágenes y búsqueda.",
    "version": "1.0.0",
    "isCore": false,
    "isActive": true,
    "activatedAt": "2026-08-14T10:00:00Z",
    "expiresAt": null,
    "displayOrder": 20,
    "hardDependencies": ["core"],
    "softDependencies": [],
    "canDeactivate": true,
    "blockedBy": []
  }
]
```

`canDeactivate` y `blockedBy` se calculan en el servidor. El frontend no debe rehacer el análisis del grafo: si lo hiciera, tendríamos dos implementaciones de la misma regla y una de las dos se quedaría atrás.

`expiresAt` se devuelve pero **no se evalúa**. El control de vencimientos es de la fase 5 (ADR-004); hasta entonces un módulo vencido sigue activo. Se expone para que la pantalla pueda mostrarlo, no para decidir.

### `POST /api/admin/modules/{code}/activate` · rol `super_admin`
### `POST /api/admin/modules/{code}/deactivate` · rol `super_admin`

Respuesta 200:

```json
{ "code": "catalog", "isActive": true, "restart": "scheduled" }
```

`restart` vale `scheduled` cuando el host va a detenerse, `required` cuando la configuración lo impide y hace falta relanzarlo a mano, y `none` cuando la operación no cambió nada.

**Reglas:**

1. Un módulo que ya está en el estado pedido responde 200 con `restart: none`. No se reinicia por una operación que no cambia nada, ni se escribe auditoría.
2. `code` desconocido → **404**.
3. `is_core = true` → **409**. CORE no se desactiva (SPEC §8.1).
4. Activar con alguna dependencia dura inactiva → **409**, nombrando cuáles faltan.
5. Desactivar un módulo del que otro módulo activo depende de forma dura → **409**, nombrando quién lo bloquea.
6. Las dependencias blandas no bloquean nada, ni al activar ni al desactivar. Ese es justamente su significado.
7. Se escribe `activated_at` o `deactivated_at` según corresponda, y auditoría con acción `activate` o `deactivate`.

### La validación tiene que ser la misma que la del arranque

Es el punto delicado de esta entrega. El host aborta el arranque si un módulo activo tiene una dependencia dura inactiva (SPEC §7, paso 6). Si el endpoint valida con un criterio ligeramente distinto, puede persistir un estado que el arranque rechaza — y como el proceso se detiene inmediatamente después, el sistema **queda muerto y solo se recupera por SQL**.

Por eso la comprobación del endpoint y la del arranque son **la misma función**, sobre el mismo grafo, no dos implementaciones parecidas. La prueba que lo vigila toma la activación resultante y la pasa por el validador de arranque antes de confirmar la transacción: si no arrancaría, la operación se rechaza y no se escribe nada.

### Dos activaciones a la vez — corrección pendiente

Validar dentro de la transacción protege contra un cambio malo, no contra **dos cambios buenos que juntos son malos**. Con dos `super_admin` operando a la vez, cada transacción ve su propia instantánea: si uno desactiva M05b mientras el otro activa M06, ambas operaciones son válidas por separado, ambas confirman, y el estado resultante impide arrancar. Es el mismo escenario contra el que se diseñó la validación, entrando por la puerta de al lado.

**Regla:** la transacción de activación toma antes un `pg_advisory_xact_lock` sobre una clave constante del módulo CORE. Serializa todas las activaciones de la instalación; se libera al terminar la transacción. No hace falta subir el nivel de aislamiento.

Es un caso improbable —dos administradores tocando módulos en el mismo segundo— con una consecuencia desproporcionada: instalación muerta, recuperable solo por SQL. El bloqueo cuesta una línea.

Criterios:

- [x] Dos activaciones concurrentes que juntas producirían un estado no arrancable no pueden confirmar las dos
- [x] Una activación normal no queda esperando el bloqueo más de lo que dura su transacción

**Aplicado en la entrega 3b.** El bloqueo se toma antes de leer las activaciones, no después: tomado después, cada transacción ya tendría su instantánea y llegaría tarde para lo único que debe impedir.

Verificado contra PostgreSQL con dos conexiones simultáneas sobre la misma clave: la segunda quedó esperando y obtuvo el bloqueo a los 1506 ms, exactamente cuando la primera confirmó, sin colarse antes. Sin contención, tomarlo y soltarlo cuesta 1 ms; tres activaciones seguidas por HTTP respondieron en 214, 57 y 31 ms. Tras un `rollback` no quedaba ningún bloqueo vivo.

Se descarta desactivar en cascada. Que apagar Servicios apague también Seguimiento sin avisar es el tipo de comodidad que un día apaga media instalación con un clic. El 409 explica qué bloquea y la persona decide el orden.

---

## 3. Configuración del sitio

### `GET /api/admin/settings` · rol `admin`

Todas las claves con su valor, tipo, descripción, `is_public` y un indicador de si sigue con el marcador `PENDIENTE_DEFINIR` del seed. Ese indicador es lo que permite al panel mostrar qué le falta configurar al negocio recién instalado.

### `PUT /api/admin/settings/{key}` · rol `admin`

```json
{ "value": "+51 999 999 999", "isPublic": true }
```

**Reglas:**

1. **No se crean claves desde el API.** Una clave desconocida responde 404. Las claves nacen del seed o de la migración del módulo que las necesita; si cualquiera puede inventarlas, `site_settings` se convierte en un cajón de sastre sin tipo ni descripción.
2. Tampoco se borran. Para retirar una clave está `is_active`.
3. El valor se valida contra `value_type`: `number` numérico, `boolean` un booleano reconocible, `url` y `email` con formato, `json` analizable. Un valor que no encaja → **400** indicando el tipo esperado.
4. `text` obligatorio no vacío, según la convención de textos del proyecto.
5. **Cambiar `is_public` exige `super_admin`**, aunque cambiar el valor solo exija `admin`. Publicar un dato es una decisión de otra naturaleza que corregir un número de teléfono, y el SPEC §8.10 ya insiste en que sea deliberada.
6. Se publica el evento `SettingChanged` del contrato de CORE.
7. Se audita con acción `update`, entidad `setting` y la clave en `entity_id`.

**El valor no se escribe en auditoría.** Ni el nuevo ni el anterior. Hoy todas las claves del seed son inocuas, pero la tabla está pensada para alojar también credenciales de correo saliente, y una auditoría que registre valores se convierte en un almacén de secretos en claro que además nadie puede borrar (SPEC §8.15). El resumen dice qué clave cambió y quién; para saber el valor está la propia tabla.

### Caché e invalidación

`ISettingsReader` se consulta en cada petición pública, así que los valores se cachean en memoria. La escritura invalida la entrada. Es caché por proceso: con una sola instancia por instalación —que es el modelo de despliegue del ADR-001— basta y no hace falta nada distribuido.

### `GET /api/settings/public`

Estaba declarado en el SPEC §6 desde el principio, pero **no se implementó en las entregas 1 ni 2**; entra aquí. Responde desde la misma caché y refleja los cambios de inmediato, sin reinicio. La configuración no toca el enrutamiento; los módulos sí.

---

## 4. Auditoría consultable

### `GET /api/admin/audit` · rol `super_admin`

Solo lectura. No hay `POST`, `PUT` ni `DELETE`: se escribe desde dentro, mediante `IAuditWriter` (SPEC §8.15).

**Filtros:** `from`, `to`, `adminUserId`, `moduleCode`, `action`, `entityType`, `entityId`.
**Orden:** `occurred_at` descendente.
**Paginación:** la de `Sillar.Shared`, con tamaño de página **por defecto 50 y máximo 200**. Sin tope, la primera consulta de una instalación con dos años de uso vuelca la tabla entera.

Los tres índices del SPEC §4.9 cubren los filtros previstos. Si aparece un filtro sin índice, se añade en su migración; no se resuelve escaneando.

Se devuelve `admin_user_email` tal como está almacenado —el snapshot, no una unión con `admin_users`—, que es justamente lo que permite que el registro sobreviva al borrado del usuario.

La depuración por antigüedad queda fuera de alcance. Se decidirá cuando haya una instalación con volumen real; inventar hoy una política de retención es una abstracción sin caso.

---

## 5. Criterios de aceptación

**Activación**

- [ ] `GET /api/admin/modules` devuelve activos e inactivos, con `canDeactivate` y `blockedBy` calculados en el servidor
- [ ] Activar un módulo con una dependencia dura inactiva devuelve 409 nombrándola
- [ ] Desactivar un módulo del que otro activo depende duro devuelve 409 nombrando quién bloquea
- [ ] Desactivar CORE devuelve 409
- [ ] Un `code` desconocido devuelve 404
- [ ] Activar un módulo ya activo devuelve `restart: none` y no escribe auditoría
- [ ] Una dependencia blanda inactiva no impide activar ni desactivar
- [ ] La respuesta llega completa al cliente antes de que el proceso se detenga
- [ ] Con `Modules:RestartAfterActivation = false` el proceso no se detiene y la respuesta dice `restart: required`
- [ ] Tras el reinicio, `GET /api/capabilities` refleja el cambio
- [ ] Tras el reinicio, la sesión y el token CSRF anteriores siguen siendo válidos
- [ ] El contenedor del API se relanza solo tras la detención
- [ ] Ninguna secuencia de activaciones y desactivaciones permitida por el endpoint produce un estado que impida arrancar
- [ ] `activate` y `deactivate` quedan auditados

**Configuración**

- [ ] `GET /api/admin/settings` marca las claves que siguen en `PENDIENTE_DEFINIR`
- [ ] Un valor que no encaja con `value_type` devuelve 400 indicando el tipo esperado
- [ ] Una clave desconocida devuelve 404 y no se crea
- [ ] Un `admin` puede cambiar el valor pero no `is_public`; un `super_admin` puede ambas cosas
- [ ] `GET /api/settings/public` refleja el cambio sin reiniciar
- [ ] `GET /api/settings/public` sigue sin devolver claves con `is_public = false`
- [ ] La auditoría del cambio no contiene el valor, ni el nuevo ni el anterior

**Auditoría**

- [ ] Los filtros combinan entre sí
- [ ] El tamaño de página por defecto es 50 y no se puede pedir más de 200
- [ ] Los resultados llegan ordenados por fecha descendente
- [ ] No existe ninguna ruta que edite o borre registros de auditoría
- [ ] Un usuario desactivado sigue apareciendo en los registros anteriores, con su correo

**General**

- [ ] Todos los endpoints documentados en Swagger
- [ ] Un `editor` no accede a ninguno de estos endpoints

---

## 6. Fuera de alcance

| Qué | Cuándo |
|---|---|
| Gestión de medios | Entrega 3b |
| Firma y vencimiento de la licencia | Fase 5, comercialización |
| Límites de uso por módulo | Fase 5 |
| Depuración de auditoría por antigüedad | Cuando exista volumen real |
| Exportación de auditoría | Cuando alguien la pida |
| Creación de claves de configuración desde el panel | Sin caso de uso; las claves nacen del módulo que las necesita |
