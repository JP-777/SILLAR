# SPEC — M05a Servicios — Vitrina

- **Código:** `services`
- **Nombre:** M05a Servicios — Vitrina
- **Schema reservado:** `services`
- **Versión propuesta:** 0.1.0
- **Estado:** Borrador propuesto · pendiente de ratificación
- **Fase:** MVP · paso 1 SPEC
- **Fecha de creación:** 26 de septiembre de 2026
- **Última verificación:** 26 de septiembre de 2026
- **Zona horaria:** America/Lima
- **Base de referencia y SHA efectivamente leído:** `711bfba7cf3be80baa146b44e79ddf7a633d695d`
- **Estado de esta SPEC:** propuesta documental; ningún criterio de implementación se declara cumplido

## 0. Trazabilidad de decisiones

### 0.1 Decisión previa abierta

`docs/modules/services/DECISIONES-PREVIAS-M05a.md` dejó abierta la existencia de M05a porque M01 ya admite servicios como artículos de catálogo. Los datos de demostración incluyen anillado e impresión con precio a consultar, y el modelo de M01 permite unidades de venta de texto libre, opciones o presentaciones y precios diferentes por presentación.

La pregunta histórica era: «¿Qué hace M05a que M01 no haga ya?».

### 0.2 Decisión posterior de JP

JP decidió el 26 de septiembre de 2026 que los servicios permanentes tendrán un módulo propio: **M05a Servicios — Vitrina**.

La existencia del módulo deja de ser una cuestión abierta. La decisión no demuestra por sí misma que M05a necesite tablas propias ni autoriza a incorporar órdenes, cobros, inventario o seguimiento.

### 0.3 Motivo y consecuencia arquitectónica

M05a aporta un canal público y administrativo específicamente dedicado a servicios permanentes, activable y vendible independientemente del catálogo general.

La consecuencia es que la vitrina debe:

- aparecer y desaparecer como una capacidad completa;
- tener rutas públicas y administrativas propias;
- no quedar escondida dentro del listado general de productos;
- funcionar con CORE como única dependencia dura confirmada;
- no absorber las responsabilidades de M05b, M06, M09 o M13;
- mantener su propio ciclo de vida, separado del schema `service_orders`.

### 0.4 Decisiones previas conservadas

- M05a usa el schema `services`.
- M05b usa el schema `service_orders`.
- Sus ciclos de instalación y desinstalación son independientes.
- M05a no replica por defecto.
- Si M05a incorpora persistencia propia, sus identificadores serán `integer GENERATED ALWAYS AS IDENTITY`.
- No se incorporan automáticamente `origin_node` ni `row_version`.
- La decisión de replicación se revisará al escribir la SPEC de M13 o antes si la observación confirma que los servicios se cobran en caja.
- Cambiar identificadores cuando ya existen datos sería una migración costosa; el disparador no debe omitirse.

### 0.5 Cuestión principal pendiente

Debe ratificarse cuál será la fuente de datos de M05a:

1. contenido editorial propio y autónomo;
2. proyección de artículos de M01;
3. modelo híbrido con referencias o snapshots de M01.

Esta SPEC recomienda la primera alternativa porque es la única que mantiene el comportamiento autónomo declarado en la arquitectura. La recomendación sigue siendo una propuesta, no una decisión ratificada.

---

## 1. Propósito

M05a presenta públicamente los servicios permanentes ofrecidos por el negocio y permite al personal mantener esa vitrina.

Resuelve un problema de descubrimiento: un servicio no debe quedar oculto entre productos físicos ni depender de que la persona visitante conozca de antemano la categoría o el término de búsqueda adecuado.

M05a describe lo que se ofrece. No cobra, reserva, recibe trabajos, mueve inventario, registra órdenes ni sigue su ejecución.

### 1.1 Diferencia frente a M01

M01 ya puede representar:

- nombre;
- descripción breve y completa;
- imágenes;
- precio fijo o «a consultar»;
- unidad de venta de texto libre;
- opciones o presentaciones;
- diferencias de precio entre presentaciones;
- publicación y baja lógica;
- búsqueda y exposición pública.

M05a no debe reproducir esas capacidades para simular una diferencia inexistente.

La responsabilidad diferencial propuesta es:

- ofrecer un destino público inequívoco de «Servicios»;
- mantener una selección exclusivamente formada por servicios permanentes;
- ordenar y publicar esa selección sin mezclarla con productos físicos;
- poder venderse y activarse como capacidad independiente de M01;
- conservar su propia experiencia pública y administrativa durante todo su ciclo de vida.

No se confirman en esta SPEC campos como duración, agenda, requisitos del trabajo, tiempos de entrega, disponibilidad por local o instrucciones de recepción. Si la observación demuestra que alguno es necesario, deberá ratificarse antes de entrar al modelo de datos.

---

## 2. Valor comercial

Un negocio puede contratar M05a para publicar de forma visible servicios permanentes —por ejemplo, anillado o impresión— aunque no compre un catálogo general de productos.

El valor separado no está en inventar otro concepto de precio o presentación, sino en dar a los servicios:

- una entrada pública propia;
- una navegación sin ruido de productos físicos;
- una administración acotada;
- activación, desactivación y licenciamiento independientes.

El coste de mantenerlo separado es real:

- nueva superficie pública y administrativa;
- ciclo propio de instalación y desmontaje;
- posible duplicación de contenido con M01;
- necesidad de definir qué sistema es dueño del contenido cuando ambos estén instalados;
- pruebas propias de composición y desmontaje.

La separación solo se justifica si JP ratifica que esa independencia comercial y editorial compensa dicho coste.

---

## 3. Alcance y límites

### 3.1 Dentro de alcance

- listado público de servicios publicados;
- ficha pública de un servicio cuando la información disponible la justifique;
- presentación de precio fijo, gratuito o a consultar, sin cobrarlo;
- presentación de una unidad descriptiva;
- presentación de opciones editoriales cuando estén confirmadas;
- administración mínima de la vitrina;
- estados editoriales;
- orden de presentación;
- asociación opcional con medios administrados por CORE;
- auditoría de cambios administrativos si existe persistencia propia;
- ciclo completo de instalación, activación, desactivación, desinstalación y reinstalación.

### 3.2 Fuera de alcance

- cobros y pagos;
- carrito o checkout;
- reservas o agenda de citas;
- caja o punto de venta;
- movimientos, bloqueos o consulta de inventario;
- recepción o procesamiento de órdenes de servicio;
- captura de características específicas de un encargo;
- asignación de responsables;
- seguimiento de trabajos;
- historial operativo;
- estados de una orden;
- notificaciones operativas;
- reportes comerciales;
- sincronización entre nodos.

Las órdenes pertenecen a M05b. Su seguimiento pertenece a M06. Los cobros y caja pertenecen a M13. Las existencias pertenecen a M09.

Si una futura necesidad de M05a exige alguno de esos comportamientos, debe escalarse al dueño del módulo correspondiente; no puede introducirse como un detalle de la vitrina.

---

## 4. Dependencias

| Módulo | Tipo | Necesidad | Comportamiento ante ausencia |
|---|---|---|---|
| CORE | Dura | Capacidades, activación, autenticación administrativa, CSRF, auditoría y medios si se usan imágenes | No aplica: CORE forma parte de la plataforma |
| M01 Catálogo | Ninguna confirmada | Puede aportar una futura fuente de contenido o vinculación | M05a debe seguir mostrando y administrando su vitrina sin errores |
| M05b Órdenes | No es dependencia de M05a | M05b podrá depender de M05a según la arquitectura | M05a no cambia si M05b está ausente |
| M06 Seguimiento | Ninguna | M06 pertenece al flujo operativo de órdenes | Sin efecto |
| M13 Punto de Venta | Futura revisión, no dependencia actual | Dispara la revisión de replicación si los servicios se cobran en caja | Sin efecto actual |

### 4.1 Módulos dependientes

La arquitectura declara que M05b depende de M05a. La forma concreta de esa dependencia no se define aquí: M05b todavía deberá decidir si referencia una identidad de servicio, conserva un snapshot o utiliza otro contrato.

M05a no puede importar dominio ni datos de M05b.

### 4.2 Posible relación con M01

No se propone una dependencia dura ni blanda de M01 en esta versión.

Si JP ratifica una integración futura:

- deberá utilizar únicamente un contrato público de M01;
- no podrá leer directamente el schema `catalog`;
- deberá definirse el comportamiento sin M01;
- deberá resolverse la titularidad de nombre, descripción, precio e imágenes;
- deberá evitar ciclos;
- deberá especificar cómo se detectan y resuelven divergencias;
- deberá indicar si la relación es viva o un snapshot.

---

## 5. Modelo conceptual de datos

### 5.1 Decisión propuesta

Se propone persistencia propia mínima para mantener la independencia frente a M01. No se especifican todavía tablas, DDL, migraciones ni diccionario físico.

La propuesta se limita a conceptos indispensables para una vitrina administrable.

### 5.2 Conceptos

#### Entrada de vitrina de servicio

Representa un servicio permanente presentado públicamente.

Información conceptual mínima:

- identidad interna;
- nombre visible;
- slug público estable;
- descripción breve opcional;
- descripción completa opcional;
- precio fijo opcional;
- unidad descriptiva opcional;
- estado editorial;
- orden de presentación;
- referencia opcional a un medio de CORE;
- marcas temporales de creación y modificación.

Reglas:

- precio nulo significa «consultar precio»;
- precio cero significa gratuito y no puede confundirse con nulo;
- el slug no cambia automáticamente al editar el nombre;
- la identidad técnica no se muestra en la interfaz;
- una entrada solo es pública cuando está publicada;
- archivar no equivale a eliminar físicamente;
- el estado editorial es distinto de la activación del módulo.

#### Opción de presentación

Concepto condicional, pendiente de ratificación.

Solo se justificaría si M05a debe mostrar alternativas estables de un servicio, como tipos de tapa o modalidades de impresión. M01 ya resuelve un problema semejante mediante `ProductItem`; M05a no debe copiarlo sin confirmar un caso administrativo real.

Si se ratifica, una opción podrá tener:

- nombre visible;
- precio propio opcional;
- orden;
- estado activo.

No representa inventario ni una característica capturada al recibir una orden.

### 5.3 Relaciones

```text
Entrada de vitrina 1 ─── 0..N Opciones de presentación
Entrada de vitrina 0..1 ─── 1 Medio de CORE
```

La relación con medios pertenece a CORE y sería dura si el modelo físico utiliza una FK. La política concreta de borrado debe definirse en el paso 2 siguiendo el patrón de medios existente.

### 5.4 Titularidad

M05a sería dueño de:

- selección de servicios exhibidos;
- nombre y textos editoriales de esa vitrina;
- slug;
- orden;
- estado editorial;
- precio meramente informativo, si JP ratifica que se mantenga aquí;
- opciones meramente informativas, si se ratifican.

M05a no sería dueño de:

- productos y variantes de M01;
- órdenes de M05b;
- estados e historial de M06;
- existencias de M09;
- líneas de venta, caja o cobros de M13;
- archivos físicos de medios, que pertenecen a CORE.

### 5.5 Colaciones y búsqueda

Antes de crear cualquier tabla deberá decidirse qué textos requieren `core.es_ci` o `core.es_search`.

Si una columna usa una colación no determinista:

- no se podrá asumir que `LIKE`, `ILIKE` o expresiones regulares funcionarán;
- cualquier búsqueda por fragmentos deberá aplicar `COLLATE "C"` en la expresión;
- el índice deberá utilizar la misma expresión;
- la migración y la consulta deberán verificarse por vías independientes.

No se fija en esta SPEC un mecanismo de búsqueda porque una vitrina pequeña puede no necesitarlo.

### 5.6 Identificadores y replicación

Si se ratifica persistencia propia:

- los identificadores serán `integer GENERATED ALWAYS AS IDENTITY`;
- no se incorporarán `origin_node` ni `row_version`;
- no se presupone replicación;
- la revisión será obligatoria al escribir la SPEC de M13 o antes si se confirma cobro de servicios en caja.

La revisión deberá ocurrir antes de almacenar datos cuyo cambio de identidad resulte costoso.

### 5.7 Datos iniciales

No se propone contenido comercial como seed del producto.

Anillado e impresión son evidencia de que M01 puede representar servicios, no datos universales que deban instalarse en todos los negocios.

---

## 6. Contratos, API y eventos

Todo lo descrito en esta sección es borrador. No representa rutas existentes.

### 6.1 Contrato público propuesto

Solo se justificaría un contrato interno cuando exista un consumidor confirmado, previsiblemente M05b.

Propuesta mínima futura:

```csharp
namespace Sillar.Modules.Services.Contracts;

public sealed record ServiceShowcaseSnapshot(
    int ServiceId,
    string Name,
    string Slug,
    string? DisplayPrice,
    string? SaleUnit);

public interface IServicesShowcase
{
    Task<ServiceShowcaseSnapshot?> GetPublishedAsync(
        int serviceId,
        CancellationToken cancellationToken);
}
```

Antes de ratificarlo debe resolverse si M05b necesita una referencia viva o un snapshot. Un contrato no debe congelarse únicamente porque M05b aparece como dependiente en el mapa.

### 6.2 Endpoints públicos propuestos

| Método | Ruta | Resultado | Autorización | Errores observables |
|---|---|---|---|---|
| GET | `/api/services` | Entradas publicadas, ordenadas | Pública | 200 con lista vacía; 404 si la ruta no existe por módulo inactivo |
| GET | `/api/services/{slug}` | Ficha publicada | Pública | 404 si no existe, está archivada o no está publicada |

Parámetros de listado inicialmente permitidos:

- ninguno obligatorio;
- no se añaden búsqueda, categorías ni paginación hasta que el volumen real las justifique.

La API pública no revela si una entrada existe como borrador o archivada.

### 6.3 Endpoints administrativos propuestos

| Método | Ruta | Propósito | Autorización |
|---|---|---|---|
| GET | `/api/admin/services` | Listar todas las entradas y estados | Sesión administrativa |
| POST | `/api/admin/services` | Crear borrador | Sesión administrativa + CSRF |
| GET | `/api/admin/services/{id}` | Obtener edición | Sesión administrativa |
| PUT | `/api/admin/services/{id}` | Modificar contenido | Sesión administrativa + CSRF |
| POST | `/api/admin/services/{id}/publish` | Publicar | Sesión administrativa + CSRF |
| POST | `/api/admin/services/{id}/unpublish` | Volver a borrador | Sesión administrativa + CSRF |
| POST | `/api/admin/services/{id}/archive` | Archivar | Sesión administrativa + CSRF |
| PUT | `/api/admin/services/order` | Cambiar orden | Sesión administrativa + CSRF |

Resultados y errores:

- 200 o 201 para operaciones correctas;
- 400 para datos inválidos;
- 401 para ausencia de sesión;
- 403 para CSRF o autorización insuficiente;
- 404 sin revelar información técnica;
- 409 para slug duplicado, transición inválida o conflicto de concurrencia;
- Problem Details o formato común de plataforma, sin nombres de tablas, SQL, stack traces ni identificadores internos innecesarios.

### 6.4 Eventos

No se proponen eventos en esta etapa.

No existe todavía un consumidor confirmado para hechos editoriales como publicación o archivo. M10 no justifica publicar eventos hipotéticos.

Si M05b termina requiriendo reaccionar a cambios de la vitrina, deberá documentarse el hecho de negocio, el consumidor y la política ante servicios archivados antes de añadir eventos.

---

## 7. Publicación, administración, auditoría y errores

### 7.1 Estados editoriales

Estados propuestos:

- **Borrador:** visible y editable en administración; no aparece públicamente.
- **Publicado:** aparece en listado y ficha pública.
- **Archivado:** no aparece públicamente; se conserva para trazabilidad y eventual consulta administrativa.

Transiciones propuestas:

```text
Borrador ── publicar ──> Publicado
Publicado ── retirar ──> Borrador
Borrador/Publicado ── archivar ──> Archivado
```

La restauración desde Archivado queda pendiente de ratificación. No se añade por simetría sin un caso real.

### 7.2 Requisitos mínimos de publicación

Una entrada solo puede publicarse si tiene:

- nombre no vacío;
- slug válido y único;
- descripción breve o completa suficiente para no producir una ficha vacía;
- estado no archivado.

Imagen, precio y unidad no son obligatorios.

### 7.3 Activación frente a publicación

- Activar M05a habilita la capacidad completa.
- Desactivar M05a elimina sus rutas, navegación y contribuciones visuales.
- Publicar una entrada solo cambia la visibilidad de esa entrada.
- Un módulo activo puede tener cero entradas publicadas.
- Tener entradas publicadas no autoriza a activar automáticamente el módulo.

Nunca se deduce el estado del módulo contando servicios publicados.

### 7.4 Administración mínima

El personal puede:

- crear un borrador;
- editar textos y presentación;
- definir precio informativo o «a consultar»;
- publicar;
- retirar;
- archivar;
- reordenar.

No puede:

- cobrar;
- crear una orden;
- reservar una cita;
- cambiar inventario;
- registrar avances;
- asignar responsables;
- iniciar seguimiento.

### 7.5 Auditoría

Si se ratifica persistencia propia, toda escritura administrativa debe registrarse en `core.audit_log`.

Cada resumen debe identificar la entrada con un nombre comprensible:

- `Alta del servicio «Anillado espiral».`
- `Publicación del servicio «Impresión láser».`
- `Modificación del servicio «Anillado espiral».`
- `Retiro del servicio «Impresión láser».`
- `Archivo del servicio «Anillado espiral».`

No basta con «Modificación de servicio» ni con mostrar solamente un identificador técnico.

El detalle técnico podrá conservar el identificador, pero no sustituirá el nombre legible.

### 7.6 Errores y conflictos

| Situación | Comportamiento observable |
|---|---|
| Slug duplicado | 409 y mensaje para elegir otra dirección |
| Publicación incompleta | 409 o 400 con campos que deben completarse |
| Precio negativo | 400; nunca se guarda |
| Servicio público inexistente o no publicado | 404 indistinguible |
| Transición editorial inválida | 409 con la acción válida sugerida |
| Edición concurrente | 409; no sobrescribe silenciosamente |
| Medio eliminado | La entrada debe degradar sin imagen ni hueco roto; política física en paso 2 |
| Módulo desactivado | Rutas y enlaces ausentes, no pantallas de error |
| Schema ausente al activar | Activación rechazada con diagnóstico accionable |
| Error interno | Mensaje genérico correlacionable; sin SQL, stack trace ni detalles del schema |

---

## 8. Ciclo de vida y cuatro preguntas arquitectónicas

### 8.1 Instalación

La instalación prepara exclusivamente los artefactos de M05a y, si se ratifica persistencia, el schema `services` y su historial de migraciones.

No activa automáticamente el módulo.

No crea ni modifica tablas de `catalog`, `service_orders` o `tracking`.

### 8.2 Activación

La activación:

- comprueba que los artefactos esperados existan;
- se niega si falta el schema requerido;
- no ejecuta migraciones dentro de la petición;
- incorpora rutas y contribuciones de navegación después de un arranque coherente.

### 8.3 Desactivación

La desactivación:

- conserva los datos propios;
- retira rutas públicas y administrativas;
- retira enlaces, menú y contribuciones a portada;
- no deja huecos visuales;
- no cambia datos de M01, M05b ni M06;
- no convierte entradas publicadas en borrador.

### 8.4 Desinstalación

La desinstalación física, si existe schema propio:

- elimina únicamente `services`;
- no elimina `service_orders`;
- no elimina órdenes, historial ni datos de catálogo;
- debe ser impedida o coordinada si una dependencia dura activa la requiere;
- no puede depender de `DROP SCHEMA ... CASCADE` para resolver relaciones ajenas silenciosamente.

La política de conservar, exportar o perder datos propios antes de eliminar `services` queda pendiente de ratificación.

### 8.5 Reinstalación

Después de una desinstalación completa:

- reinstalar recrea un M05a vacío;
- no reconstruye contenido comercial de demostración;
- no altera otros módulos;
- puede activarse solo después de preparar correctamente sus artefactos.

Si se decide conservar datos fuera del schema, esa restauración deberá especificarse antes del paso 2.

### 8.6 Primera pregunta: reglas que podrían depender accidentalmente de un solo caso

- No se debe deducir que M05a está vacío porque no haya servicios publicados: puede haber borradores o archivados.
- No se debe identificar ausencia de M05a buscando la palabra «Servicios»: otros módulos pueden usarla legítimamente.
- No se debe asumir que todo servicio tiene precio, imagen, unidad u opciones.
- No se debe asumir que solo existe una opción por servicio.
- No se debe asumir que una entrada con precio nulo es gratuita.
- No se debe asumir que M01 está instalado.
- No se debe inferir activación del módulo desde la existencia de su schema ni desde sus filas.
- No se debe inferir que todos los servicios se cobran o que ninguno se cobra; esa decisión pertenece al disparador de M13.

### 8.7 Segunda y tercera preguntas: barreras nuevas y lugar de aplicación

Las guardas deben estar en la operación de dominio o servicio que modifica el estado, no solo en el formulario o endpoint llamador.

| Barrera | Condición protegida | Lugar futuro |
|---|---|---|
| B1 Publicación válida | No publicar sin nombre, slug único y contenido descriptivo | Operación de publicación |
| B2 Precio semántico | Rechazar precio negativo y conservar diferencia entre cero y nulo | Operaciones de creación y edición |
| B3 Transición editorial | Rechazar transiciones no permitidas | Operación de transición |
| B4 Aislamiento funcional | Ninguna operación de M05a crea cobros, órdenes, reservas o movimientos | Contratos, endpoints y composición |
| B5 Aislamiento de desmontaje | Eliminar M05a no toca schemas ni datos ajenos | Orquestador y script futuro de desinstalación |
| B6 Capacidad coherente | No activar con artefactos ausentes | Operación de activación de CORE |
| B7 Slug estable y único | No cambiarlo automáticamente ni permitir duplicados | Operaciones de creación y edición |
| B8 Ocultación pública | No revelar borradores o archivados | Consulta pública y operación de detalle |

### 8.8 Casos positivos, negativos y falsificación deliberada

| Barrera | Caso positivo | Caso negativo y rechazo deliberado | Defecto deliberado para probar la prueba | Evidencia futura |
|---|---|---|---|---|
| B1 | Publicar entrada completa | Intentar publicar sin descripción | Retirar la validación en la operación | Prueba unitaria roja y API 409 |
| B2 | Guardar cero y mostrar «Gratis» | Guardar `-1` | Cambiar la prueba para aceptar negativo o colapsar nulo en cero | Prueba de dominio y respuesta 400 |
| B3 | Publicado → Borrador | Ejecutar una transición no admitida desde Archivado | Omitir la tabla de transiciones | Prueba pura de estados y 409 |
| B4 | Editar textos sin crear operación comercial | Intentar invocar una ruta de cobro/orden bajo `/api/services` | Añadir una ruta prohibida en un fixture controlado | Inventario independiente de rutas y revisión de referencias |
| B5 | Desinstalar M05a preservando M05b | Ejecutar desmontaje con datos centinela en `service_orders` | Apuntar el drop al schema equivocado en entorno desechable | Conteos y hashes antes/después más inspección de schemas |
| B6 | Activar con schema preparado | Activar sin schema | Sustituir la comprobación por éxito constante | Prueba de integración que exige rechazo y mensaje |
| B7 | Crear dos slugs distintos y conservar slug al renombrar | Crear un duplicado | Quitar índice o validación en entorno desechable | Prueba de servicio y restricción física |
| B8 | Consultar un publicado | Consultar borrador por slug | Eliminar el filtro editorial de la consulta | API pública debe pasar de 404 a fallo de prueba |

La falsificación deliberada se realizará únicamente durante implementación, en un entorno desechable y con restauración posterior. No se ejecuta en este paso.

### 8.9 Cuarta pregunta: segunda vía independiente

| Afirmación crítica | Primera vía futura | Segunda vía independiente |
|---|---|---|
| Solo se publican entradas válidas | Prueba de dominio | Petición HTTP real y lectura directa controlada |
| Cero y nulo son distintos | Prueba de serialización/API | Inspección de persistencia y representación visual |
| Borradores no son públicos | Prueba del servicio | Petición anónima por slug |
| Desactivar elimina navegación | Prueba UI | Consulta de capacidades y acceso directo a ruta |
| Desinstalar preserva M05b | Ejecución del ciclo | Consulta independiente de schemas y datos centinela |
| No hay dependencia de M01 | Compilación/referencias | Arranque y navegación con M01 ausente |
| No existen rutas comerciales | Inventario de endpoints | Inspección de OpenAPI |
| Auditoría nombra la entrada | Prueba de productor | Consulta independiente de `core.audit_log` |
| Schema correcto | Modelo/migración | Consulta a catálogos de PostgreSQL |
| No replica | Modelo de datos | Inspección de columnas y contratos de sincronización |

---

## 9. Pantallas y estados para el futuro paso 3.5

No se diseñan componentes en esta etapa.

### 9.1 Listado público de servicios

- **Ruta propuesta:** `/servicios`
- **Objetivo y usuario:** permitir que cualquier visitante descubra los servicios permanentes.
- **Información:** nombre, descripción breve, imagen opcional, precio fijo/gratuito/a consultar y unidad opcional.
- **Acciones:** abrir ficha; no comprar, reservar ni crear orden.
- **Vacío:** “Todavía no hay servicios publicados”, sin cuadrícula vacía.
- **Con datos:** tarjetas ordenadas editorialmente.
- **Cargando:** esqueleto o indicador no bloqueante siguiendo la plataforma.
- **Error:** mensaje comprensible y posibilidad de reintentar; sin detalles técnicos.
- **Claro/oscuro:** mismos estados y jerarquía; colores mediante tokens.
- **Móvil:** una columna, zonas táctiles amplias, contenido esencial primero.
- **Escritorio:** cuadrícula adaptable, sin barra de filtros mientras no exista necesidad confirmada.
- **M05a desactivado:** ruta no compuesta y enlace ausente.
- **Componentes:** tarjeta, precio y estados vacíos existentes pueden inspirar componentes compartidos, pero M05a no importará componentes de M01. Cualquier generalización irá a `shared` solo con contrato transversal confirmado.

### 9.2 Ficha pública de servicio

- **Ruta propuesta:** `/servicios/:slug`
- **Objetivo y usuario:** explicar un servicio concreto.
- **Información:** nombre, medio opcional, descripciones, precio informativo, unidad y opciones ratificadas.
- **Acciones:** volver al listado; no comprar, reservar ni enviar una orden.
- **Vacío:** no aplica para una entidad válida; contenido incompleto impide publicar.
- **Con datos:** ficha legible con opciones si existen.
- **Cargando:** indicador con etiqueta específica.
- **Error/conflicto:** 404 amable si no existe o dejó de publicarse; error recuperable ante fallo de red.
- **Claro/oscuro:** paridad funcional y contraste.
- **Móvil:** flujo vertical, medios responsivos.
- **Escritorio:** contenido y medio en una composición legible, sin simular checkout.
- **M05a desactivado:** ruta ausente.
- **Componentes:** enlace, precio, medio, EmptyState y Spinner de plataforma.

### 9.3 Administración — listado de vitrina

- **Ruta propuesta:** `/admin/servicios`
- **Objetivo y usuario:** permitir al personal revisar y mantener las entradas.
- **Información:** nombre, estado editorial, precio visible, orden y última modificación.
- **Acciones:** crear borrador, editar, publicar, retirar, archivar y reordenar.
- **Vacío:** explicación y acción “Crear servicio”.
- **Con datos:** lista o tabla adaptable con estado explícito.
- **Cargando:** esqueleto que conserva la estructura.
- **Error/conflicto:** alerta accionable; un conflicto no descarta cambios silenciosamente.
- **Claro/oscuro:** controles y estados no dependen solo del color.
- **Móvil:** lista apilada; acciones secundarias agrupadas.
- **Escritorio:** tabla o lista densa con acciones por fila.
- **M05a desactivado:** entrada de administración y ruta ausentes.
- **Componentes:** patrones administrativos compartidos; puede requerirse un patrón común de estado editorial y reordenación.

### 9.4 Administración — editor de servicio

- **Ruta propuesta:** `/admin/servicios/nuevo` y `/admin/servicios/:id`
- **Objetivo y usuario:** crear o editar una entrada.
- **Información:** nombre, slug, textos, precio, unidad, medio, estado y opciones si se ratifican.
- **Acciones:** guardar borrador, publicar cuando sea válido, retirar y archivar.
- **Vacío:** formulario inicial sin datos comerciales precargados.
- **Con datos:** valores persistidos y estado visible.
- **Cargando:** formulario bloqueado temporalmente con indicador accesible.
- **Error/conflicto:** errores junto al campo; conflicto concurrente con opción de recargar, sin sobrescritura.
- **Claro/oscuro:** paridad y foco visible.
- **Móvil:** una columna y acción primaria accesible.
- **Escritorio:** agrupación por contenido, publicación y medios.
- **M05a desactivado:** ruta ausente.
- **Componentes:** campos, alertas, selector de medios y confirmaciones de plataforma. No se crea un editor visual nuevo sin necesidad.

### 9.5 Contribuciones a navegación o portada

No constituyen una pantalla propia, pero deben diseñarse como estados de composición:

- activa con servicios publicados;
- activa sin publicaciones;
- desactivada;
- cargando capacidades;
- fallo al obtener capacidades.

No debe reservarse un hueco vacío cuando M05a esté desactivado o no tenga contenido publicable.

---

## 10. Criterios de aceptación futuros

Ningún criterio se declara cumplido en este paso.

### 10.1 Funcionales

- [ ] El público puede listar los servicios publicados.
- [ ] El público puede abrir una ficha mediante slug.
- [ ] Un borrador o archivado devuelve 404 públicamente.
- [ ] El personal puede crear, editar, publicar, retirar, archivar y ordenar entradas.
- [ ] Precio nulo se muestra como “Consultar precio”.
- [ ] Precio cero se muestra como gratuito y no como “Consultar”.
- [ ] Renombrar una entrada no cambia automáticamente su slug.
- [ ] Una entrada incompleta no puede publicarse.
- [ ] La auditoría identifica el nombre concreto de la entrada.

### 10.2 Negativos y de frontera

- [ ] M05a no expone cobro, checkout, caja ni pagos.
- [ ] M05a no crea reservas.
- [ ] M05a no crea ni procesa órdenes.
- [ ] M05a no registra estados o historial de trabajos.
- [ ] M05a no mueve ni bloquea inventario.
- [ ] M05a no requiere M01 para arrancar o mostrar contenido propio.
- [ ] M05a no lee directamente tablas de otros módulos.
- [ ] La API pública no revela borradores ni archivados.
- [ ] Los errores no exponen SQL, nombres internos de tablas ni stack traces.
- [ ] Activar el módulo no ejecuta migraciones.

### 10.3 Barreras

Para B1–B8 deberá existir:

- [ ] caso positivo;
- [ ] caso negativo provocado deliberadamente;
- [ ] comprobación del mensaje o efecto observable;
- [ ] falsificación deliberada de la prueba;
- [ ] evidencia de que la prueba se vuelve roja ante el defecto;
- [ ] restauración de la implementación correcta;
- [ ] segunda vía independiente.

### 10.4 Ciclo de vida innegociable

Cuando se implemente, deberá demostrarse que M05a se instala, desactiva, desinstala y reinstala sin:

- enlaces rotos;
- rutas muertas;
- huecos visuales;
- fallos de arranque;
- destrucción de información de otros módulos;
- dependencias circulares;
- dependencias implícitas no declaradas.

Evidencia futura prevista:

| Condición | Evidencia |
|---|---|
| Sin enlaces rotos | Rastreo de navegación pública y administrativa con capacidad activa/inactiva |
| Sin rutas muertas | Inventario de rutas más accesos directos antes y después de desactivar |
| Sin huecos visuales | Capturas o revisión visual de portada, navegación y administración en ambos temas y tamaños |
| Sin fallos de arranque | Arranque con M05a activo, inactivo, instalado no activo y ausente |
| Sin destrucción ajena | Datos centinela y comparación de schemas antes/después del desmontaje |
| Sin ciclos | Validación del grafo de dependencias |
| Sin dependencias implícitas | Inspección de referencias y arranque sin M01/M05b/M06 |
| Reinstalación limpia | Ciclo instalación → activación → desactivación → desinstalación → reinstalación |
| Separación de schemas | Inspección independiente de `services` y `service_orders` |
| OpenAPI coherente | Comparación de operaciones expuestas según capacidad |

### 10.5 Responsive, accesibilidad y temas

- [ ] Todas las pantallas funcionan en móvil y escritorio.
- [ ] Todas tienen variantes clara y oscura.
- [ ] Estados y acciones no dependen solo del color.
- [ ] El teclado permite alcanzar las acciones.
- [ ] Los indicadores de carga y errores tienen etiquetas accesibles.
- [ ] Imágenes informativas tienen texto alternativo; decorativas usan alternativa vacía.
- [ ] La preferencia de movimiento reducido se respeta mediante la plataforma.
- [ ] No se incorpora una dependencia de ejecución nueva para resolver presentación o animación.

---

## 11. Alternativas consideradas

### A. Vista de M01 sin persistencia propia

M05a filtra o compone servicios ya almacenados como productos.

**Ventajas:**

- evita duplicar nombre, descripción, precio e imágenes;
- reutiliza capacidades ya implementadas;
- reduce administración.

**Costes y consecuencias:**

- convierte M01 en dependencia real;
- contradice la dependencia exclusiva sobre CORE mientras no se actualice la arquitectura;
- M05a deja de funcionar si M01 está ausente;
- obliga a identificar qué productos son servicios, dato que M01 no tiene confirmado;
- puede reducir M05a a una vista comercial con poco aislamiento.

### B. Persistencia editorial propia — recomendada

M05a mantiene entradas independientes.

**Ventajas:**

- funciona con CORE solamente;
- es vendible y activable sin M01;
- tiene titularidad clara;
- cumple el objetivo de una vitrina especializada.

**Costes y consecuencias:**

- duplica potencialmente contenido;
- obliga a mantener consistencia manual si el mismo servicio también existe en M01;
- necesita schema, migraciones, administración y auditoría propios;
- hace urgente decidir cómo M05b y M13 identificarán un servicio.

### C. Modelo híbrido

Una entrada de M05a puede vincularse a M01 y mantener un snapshot o sobreescrituras editoriales.

**Ventajas:**

- puede reutilizar datos sin perder una vitrina propia;
- admite degradación si M01 desaparece, si existe snapshot suficiente.

**Costes y consecuencias:**

- es la alternativa más compleja;
- crea reglas de precedencia y sincronización;
- requiere resolver referencias huérfanas;
- introduce una dependencia blanda y scripts o contratos de integración;
- no hay todavía un segundo caso real que justifique esa complejidad.

### D. Solo navegación o categoría de M01

La necesidad se resuelve con una categoría, filtro o bloque de portada.

**Ventajas:**

- coste técnico mínimo;
- una sola fuente de verdad.

**Costes y consecuencias:**

- no constituye un módulo independiente;
- no satisface literalmente la decisión posterior de JP;
- no funciona como producto separable sin M01.

La alternativa se conserva como trazabilidad, pero no revoca la existencia de M05a.

---

## 12. Fuera de alcance y pendientes de ratificación

Quedan pendientes:

1. fuente de datos definitiva;
2. persistencia propia sí/no;
3. precio informativo propio sí/no;
4. opciones propias sí/no;
5. relación futura con M01;
6. identidad o snapshot que necesitará M05b;
7. política de archivo y posible restauración;
8. política de pérdida o conservación al desinstalar;
9. necesidad real de ficha individual frente a listado suficiente;
10. necesidad de imagen;
11. contribución concreta a portada;
12. roles administrativos autorizados;
13. disparador de replicación ligado a M13 y observación de caja.

No se avanza a DATOS mientras los puntos 1, 2, 5, 6 y 8 no hayan sido ratificados.
