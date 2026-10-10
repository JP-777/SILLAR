# SILLAR — M08 Portal del Cliente · SPEC de Paso 1

**Estado:** D1–D6 RATIFICADAS POR JP el 09/10/2026. Paso 1 sujeto a revisión contractual de M05b/M06 y contraste de las pruebas; no hay implementación integrada de M08.
**Fecha:** 2026-10-09 · America/Lima. **Base verificada:** `main` `4a3fd1ef12fc2226bad185f003acea9168b9a4c9`.
**Preparación:** Chat 2 (Integración). **Ejecución prevista:** Agente A, cuando vuelva a disponer de capacidad.
**Ámbito:** SILLAR WEB, no SILLAR ERP. La preparación de contratos M05b/M06 es una candidata separada; no implica código incorporado a main, ni migraciones ni API de Portal.

## 0. Autoridad, evidencia y decisiones

**Ya ratificado por JP/colíder:** M08 Portal del Cliente; M04 como dependencia dura; M03 y M06 como proveedores blandos; M08 muestra pedidos y trabajos cuando estén disponibles, y conserva un portal funcional con solo M04; atribuciones administrativas desde `ICurrentAdmin` mediante nombre visible + identificador local + nodo de la cuenta; clasificación ADR-016/018 antes de primera migración; §9 con estados; ciclo de montaje/desmontaje en etapa 6; paridad del arnés desde la SPEC; M08 no lee notas internas de M06.

**Constatado en el código de esta base:**

- M04 registra `ICurrentCustomer`, la política `crm:customer`, el filtro CSRF de cliente, rutas `/api/customer/auth/*`, `/api/customer/profile` y `/mi-cuenta`. Es el único dueño de credenciales y sesión de cliente. `ICurrentAdmin` es una población diferente.
- M03 expone `ICustomerOrderHistory.ObtenerPedidosDeAsync(Guid customerId, int limit, CancellationToken)` y `CustomerOrderSummary`, así como `/api/sales/my-orders` y `/api/sales/my-orders/{orderCode}` con pertenencia verificada en la consulta. En este árbol no existe `frontend/src/modules/sales`: **no presuponer un enlace UI de M03 que no esté implementado**.
- M05b persiste `service_orders.service_orders.customer_id` opcional y `customer_name_snapshot`. Su contrato `IServiceOrderTrackingSource` es administrativo/de seguimiento, **no comprueba propiedad por cliente**. M05b es dueño de estado, historial, numeración y contactos internos.
- M06 registra solo rutas administrativas `/api/admin/tracking/*` y no publica aún un proyecto `Sillar.Modules.Tracking.Contracts`; la SPEC de M06 §6.5 reservó ese contrato para cuando abriera M08. `tracking.tracking_notes` está reservado exclusivamente al personal.
- La arquitectura histórica `docs/ARQUITECTURA_MODULAR.md` aún enumera `portal.users` y `portal.customer_profiles`; la implementación real de M04 ya tiene usuarios, perfil y autenticación de cliente. **Existe discrepancia documental, no autoridad para duplicar tablas.**

**Ratificación JP 09/10/2026:** D1–D6 aprobadas tal como las recomendó Chat 2, incluidas D2-A (solo vínculos existentes por CustomerId y ninguna recuperación manual en v1) y las dos ampliaciones de contratos M05b/M06. Las firmas de §6 quedan autorizadas como objetivo de implementación, pero la disponibilidad real exige compilación, pruebas PostgreSQL y entrega del dueño del contrato.

## 1. Propósito y valor de negocio

Un único lugar privado para que una persona autenticada consulte **su cuenta**, sus **pedidos de M03** cuando M03 esté activo, y el **estado consultable de sus trabajos** cuando M06 esté activo. M08 es composición de lecturas autorizadas, no otra identidad ni otra máquina de estados. Ante un módulo ausente explica su ausencia sin decir «no tienes pedidos» cuando simplemente no existe un proveedor.

La secuencia de valor es: entrar con M04 → ver un resumen seguro → ir a pedidos propios o trabajos vinculados → abrir un detalle propio → regresar. No exigir comprar ni poseer órdenes para poder usar el portal.

## 2. Alcance y fronteras

**Incluido en v1, ratificado en D1–D6:**

1. Ruta de entrada «Mi portal» bajo sesión de cliente ya existente de M04.
2. Datos mínimos de perfil vigente tomados de M04, con enlace a `/mi-cuenta` (M04 conserva edición, direcciones, verificación, restablecimiento y logout).
3. Tarjeta/lista paginable o acotada de pedidos propios cuando está registrado `ICustomerOrderHistory` (M03); códigos visibles, estado, total, número de líneas y plazo de pago, sin promesa de reserva de stock.
4. Detalle de pedido mediante enlace/ruta *solo si* existe una superficie real y con comprobación de pertenencia; la API real de M03 ya tiene `/api/sales/my-orders/{orderCode}`. No copiar lógica de pagos.
5. Tarjeta/lista de trabajos vinculados explícitamente al `CustomerId` de M04 y sus estados visibles, con consulta pública **nueva y mínima** propiedad de M05b/M06 (ver §6); nunca filtrar por nombre, teléfono, email o código visible.
6. Ausencia/inactividad de proveedores blandos explicada por separado; error de proveedor no se representa como lista vacía.
7. Accesibilidad, móvil/escritorio, temas claro/oscuro y desactivación sin rutas muertas.

**Fuera:** registro/login/recuperación y ficha editable propios de M08; inventario; cobro o confirmación de pago; cancelación/transición de pedidos/trabajos desde el portal; mensajes nuevos; notificaciones push; creación de órdenes de servicio; exposición de notas de taller, `staff_notes`, asignaciones, prioridades y vencimientos *internos*; acceso a schema de M03/M04/M05b/M06 desde M08; «reclamar» una orden introduciendo código; módulos ERP. No crear campo `customer_visible` para notas internas.

## 3. Dependencias, disponibilidad y propiedad

| Módulo | Relación | Dato/contrato que presta | Si no está disponible |
|---|---|---|---|
| CORE | Plataforma | Activación, host, auditoría administrativa, `ICurrentAdmin` si hay operaciones de personal | M08 no inicia |
| M04 CRM | **DURA** | `ICurrentCustomer`, `CustomerAuthorization.PolicyName`, sesión, perfil y cuenta | M08 no se activa |
| M03 Sales | **BLANDA** | `ICustomerOrderHistory` y API propia de detalles autorizados | Tarjeta «Pedidos no disponible», sin fallo de host |
| M06 Tracking | **BLANDA** | Proveedor de vista de avance *apta para cliente*, todavía no materializado | Tarjeta «Seguimiento de trabajos no disponible»; no consultar `/api/admin/tracking` |
| M05b ServiceOrders | **INDIRECTA por M06** | Identidad autoritativa de orden y pertenencia al cliente; contrato nuevo mínimo por ratificar | No convertirla en dependencia dura de M08 sin decisión de JP |

**Regla de composición:** preguntar por el contrato realmente disponible en DI, no solo por un indicador de activación que pudiera no corresponder al host. M06 tiene dependencia dura de M05b; M08 no debe forzar la instalación de ninguno de los dos. La desactivación de M08 no apaga M04, M03 ni M06.

**Matriz observable de capacidades:**

| M04 | M03 | M06 | Pantalla M08 |
|---|---|---|---|
| Inactivo | cualquiera | cualquiera | Sin ruta ni navegación del portal; rechazo de activación |
| Activo | Inactivo | Inactivo | Perfil y explicación de ausencia de ambos proveedores |
| Activo | Activo | Inactivo | Perfil y pedidos; trabajos no disponibles |
| Activo | Inactivo | Activo | Perfil y trabajos vinculados; pedidos no disponibles |
| Activo | Activo | Activo | Perfil, pedidos y trabajos propios |

Un proveedor activo sin registros responde «Todavía no tienes…»; no es lo mismo que ausente.

## 4. Replicación y modelo de datos ANTES de primera migración

**D1 ratificada:** M08 v1 es un **agregador de lecturas**, y no tiene tablas de negocio propias. M04 ya guarda cuentas y perfiles, M03 pedidos y M05b/M06 seguimiento. Por tanto: **cero tablas nuevas, cero identificadores persistidos, cero FK y cero nuevas migraciones de dominio en v1**. No se crea `portal.users` ni `portal.customer_profiles` por copia del esquema histórico.

| Objeto | Dueño real | Clasificación | Regla en M08 |
|---|---|---|---|
| Cuenta y perfil de cliente | M04 CRM | Según migraciones/ADR de M04, no reclasificar aquí | M08 no duplica credenciales ni perfil |
| Pedidos e ítems | M03 Sales | Replicados, UUID v7 de aplicación según SPEC | Solo lectura por contrato/autorización |
| Órdenes de servicio | M05b ServiceOrders | Replicadas, UUID v7 de aplicación | Solo vinculadas al cliente, sin FK de portal |
| Seguimiento y notas | M06 Tracking | Replicadas, UUID v7 de aplicación | Solo proyección apta para cliente; **notas jamás expuestas** |
| Tablas propias `portal.*` | **Ninguna propuesta para v1** | No aplica | Cualquier propuesta nueva vuelve a D1/ADR-016/018 antes de DDL |

Si JP requiere persistir preferencias nuevas en `portal`, D1 deja de ser válido: precisar tabla por tabla si una fila nace en nodo y se necesita en otro; usar UUID v7 + `origin_node` + `row_version` si replica, o `integer identity` si es local; aplicar ADR-018 al conjunto de FK. Ninguna fila replicada referencia una fila local. La autoría administrativa de una futura tabla replicada congela los **tres** componentes desde `ICurrentAdmin` y nunca tiene FK a `core.admin_users`. Ningún dato de identidad administrativa se extrae de una cookie/token/DbContext de CORE.

**Barrera de arnés:** incluso si M08 no posee schema ni migración, su presencia en host/setup/activación/e2e debe estar cubierta y documentada con la misma exhaustividad; `migrate()`/`seed()` deben reflejar honestamente «sin esquema propio», no declararse aplicados inventando un seed. Si se aprueba DDL, son obligatorios proyecto de migraciones, scripts `02_seed.sql`/`99_drop.sql`, registro en la etapa 4 y reconstrucción por `migrate()+seed()`. La implementación exacta del módulo sin schema se ratifica antes del Paso 2, no por inferencia de cómo funcionan los otros nueve.

## 5. Identidad, visibilidad y seguridad

1. La API del portal exige `CustomerAuthorization.PolicyName`, autentica por la sesión M04 y deriva el `customerId` **solo de `ICurrentCustomer.CustomerId`**, nunca del cuerpo de petición, query, encabezado arbitrario ni URL.
2. Para pedidos se usa `ICustomerOrderHistory`, que recibe dicho `customerId`; no se consulta `sales.orders` desde M08. Un detalle propio debe delegar en API/contrato del dueño que aplique filtro de `customerId` **antes** de materializar datos.
3. Para trabajos, solo se listan órdenes con `service_orders.customer_id == customerId` **autorizadas por contrato de M05b**. El valor `customer_id = NULL` significa **no vinculada** y no aparece en cuenta alguna, aunque coincidan nombre, email o teléfono. No existe autovinculación por código de orden.
4. La consulta de M06 debe devolver solo datos aprobados para clientela, y revalidar la pertenencia por contrato de M05b antes de entregar el detalle; no confiar únicamente en que una URL anterior fue listada. No usar `IServiceOrderTrackingSource.GetAsync(Guid)` como endpoint público sin filtro de dueño.
5. Los tres vocabularios de notas internas (M04, M05b, M06) quedan fuera de toda respuesta del portal. Tampoco se publican autores/IDs locales/nodos del personal, asignaciones, prioridades ni fechas de taller no prometidas al cliente.
6. Un identificador o código de otra persona responde 404 indistinguible de inexistente. Una cuenta administrativa no autoriza lectura de perfil de cliente; una cookie de cliente no abre administración.
7. El portal v1 es **solo lectura**. Si posteriormente contiene escrituras, exigir filtro CSRF de M04 *por la sesión de cliente*, y auditoría/triple administrativa donde exista actor administrativo; jamás simular `ICurrentAdmin` con `ICurrentCustomer`.
8. No hacer endpoint que devuelva un inventario global y después filtre en el frontend. No poner nombres personales, tokens o cookies en logs de error.

## 6. Fronteras de contratos — firmas RATIFICADAS para implementación, todavía no integradas

### 6.1 M03 — interfaz REAL, suficiente para tarjetas

```csharp
// Existe en Sillar.Modules.Sales.Contracts
Task<IReadOnlyList<CustomerOrderSummary>> ObtenerPedidosDeAsync(
    Guid customerId, int limit, CancellationToken cancellationToken);
// CustomerOrderSummary: OrderCode, Status, TotalAmount, LineCount,
// PlacedAt, PaymentDueAt.
```

Para el detalle, M03 ya ofrece `GET /api/sales/my-orders/{orderCode}` con pertenencia filtrada en SQL. Si no hay superficie frontend de M03 en esta base, M08 puede renderizar el DTO *autorizado* mediante la API, sin copiar consultas internas; el enlace visual exacto es D5.

### 6.2 M05b — contrato mínimo NUEVO, D2-A RATIFICADA

```csharp
// Implementa M05b; solo lo consumen módulos confiables, nunca HTTP anónimo.
public interface ICustomerServiceOrderReader
{
    Task<IReadOnlyList<CustomerServiceOrderSummary>> ListForCustomerAsync(
        Guid customerId, int limit, CancellationToken cancellationToken);

    Task<CustomerServiceOrderDetail?> GetForCustomerAsync(
        Guid customerId, string visibleCode, CancellationToken cancellationToken);
}
```

`CustomerServiceOrderSummary`: `VisibleCode`, `CurrentStatus`, `ReceivedAt`, `PromisedAt`, `LastStatusChangedAt`. `CustomerServiceOrderDetail` añade exclusivamente `Items` con nombre/descripción pública congelados, cantidad y unidad. La lectura filtra `customer_id` dentro de la consulta PostgreSQL del módulo dueño, devuelve null para códigos ajenos o inexistentes y nunca asocia automáticamente órdenes manuales con `CustomerId=NULL`. El límite de la lista se acota a 20; no hay campos de notas, personal, prioridad ni identificadores técnicos en las respuestas. No se implementará vinculación posterior en v1.

### 6.3 M06 — contrato NUEVO de vista pública, D3/D4/D5/D6 RATIFICADAS

```csharp
// Nuevo proyecto Sillar.Modules.Tracking.Contracts, aportado por M06.
public interface ICustomerTrackingProgress
{
    Task<IReadOnlyList<CustomerTrackingSummary>> ListForCustomerAsync(
        Guid customerId, int limit, CancellationToken cancellationToken);

    Task<CustomerTrackingDetail?> GetForCustomerAsync(
        Guid customerId, string visibleCode, CancellationToken cancellationToken);
}
```

La implementación M06 **revalida en cada detalle la pertenencia mediante M05b**. V1 devuelve código visible, estado *autoritativo* de M05b, fecha de recepción, fecha prometida cuando existe, fecha de último cambio de estado y, en el detalle, nombre/descripción pública/cantidad/unidad de las líneas congeladas. No publica `TrackingNote`, `OrderTracking`, EF, SQL, `board_priority`, `internal_due_at`, `author_*`, asignaciones ni `customer_visible`. Que los datos de avance públicos vengan de la máquina de estados de M05b es intencional: el tablero de M06 no tiene hitos adicionales públicos ratificados, por lo que no los inventamos.

**Orden de publicación:** dueño M05b ratifica y entrega frontera de pertenencia **para consumo interno por M06** → dueño M06 publica frontera sanitaria de lista/detalle → M08 consume únicamente M06. No se añade a M05b como dependencia directa de M08 sin ratificación de JP. No permitir que M08 modifique internamente dos módulos sin revisión de sus propietarios.

### 6.4 Contrato/API propios de M08 — propuesta de composición

- `GET /api/portal/overview`: respuesta por secciones con `available`/`empty`/`error`, resumen de cuenta y tarjetas de órdenes. Exige política de cliente. Nunca acepta `customerId` externo. Una falla de M03 o M06 no cambia el estado de la otra sección.
- `GET /api/portal/work/{visibleCode}`: **opcional según D3/D5**; exige pertenencia comprobada por M05b y M06, devuelve 404 si es de otra cuenta; podría sustituirse por API del dueño si el contrato la incorpora.
- Ninguna ruta administrativa ni escritura en v1. Los DTO públicos se definen después de ratificar D2–D5, no se congelan por anticipación.

## 7. Reglas de producto y estados visibles

- M03 expone siete estados de pedido: `pending_payment`, `payment_to_verify`, `preparing`, `ready_for_pickup`, `delivered`, `expired`, `cancelled`; el texto visible es español y lo presenta quien pinta. **Vencido no significa cancelado**, y `PaymentDueAt` no es reserva de mercancía.
- M05b es la única autoridad de transiciones: `received`, `in_progress`, `ready`, `completed`, `cancelled`. M06 no crea estados ni promesas nuevos. M08 no llama `TransitionAsync`.
- Una sección ausente informa «Esta función no está disponible en esta instalación». Una sección presente vacía informa «Todavía no tienes ...». Un error de red dice «No pudimos cargar ...; reintenta» y permite volver a consultar; no puede parecer ausencia de datos.
- Mostrar estado e historial público **no implica** mostrar notas o personal. Ningún mensaje dice que existe «stock reservado».
- Fechas y horas legibles con zona definida por la experiencia de SILLAR; ninguna conversión silente de calendario que cambie el año visible del pedido.
- El acceso al perfil no exige correo verificado; una compra nueva sí, y esa validación permanece exclusivamente en M03.

## 8. Barreras verificables, negativas y sabotajes propuestos

| ID | Barrera | Positivo | Negativo rojo requerido | Sabotaje del detector |
|---|---|---|---|---|
| B1 | M04 dura | Activa Portal con CRM | Rechaza activar Portal sin CRM | Quitar CRM de HardDependencies debe volver roja la prueba |
| B2 | M03/M06 blandas | Cada combinación de §3 responde | Activar Portal sin proveedores lanza excepción o ruta muerta | Inyectar excepción por ausencia sin que detecte: rojo |
| B3 | Identidad propia | Cliente A solo ve A | Cliente A consulta código de B o cambia `customerId` en la solicitud: 404/sin fuga | Eliminar filtro SQL por cliente y observar rojo |
| B4 | No reclamar por coincidencia | Orden M05b vinculada a A aparece | Orden `customer_id=NULL` con mismo correo/nombre NO aparece | Cambiar consulta a correo/nombre: rojo |
| B5 | No notas internas | Vista pública enumera solo campos autorizados | Cualquier campo `tracking_notes`, `ReceivedNotes`, notas internas CRM, actor/admin/prioridad interna en JSON: rojo | Añadir campo sensible a record/JSON y exigir prueba roja |
| B6 | Filtro en el dueño | Contrato M05b filtra antes de proyectar | Consulta global de órdenes filtrada en frontend o M08: rojo | Simular dos clientes y eliminar predicado propietario |
| B7 | Contratos, no internals | Dependencias `*.Contracts` | M08 referencia `*.Data`, `*.Domain` o lee schema ajeno: rojo | Introducir `ProjectReference`/SQL indebido, detector rojo |
| B8 | Replicación/autoría | Sin tablas o esquema clasificado sin FK ilegal | FK replicada→local / `core.admin_users` desde tabla replicada: rojo | Invalidar barrera FK EF+PostgreSQL y observar falso verde como fallo |
| B9 | Montaje/desmontaje | Apagar M08 deja M04/M03/M06 operativos | Ruta/navegación fantasma o drop ajeno: rojo | Romper la limpieza propia y probar detector |
| B10 | Paridad de arnés | Binario/setup/migrate/seed y puerta coherentes | Omitir Portal del inventario que aplique sin provocar rojo: fallo | Mutar una entrada de inventario y comprobar que prueba falla |
| B11 | Seguridad de sesión | Cookie de cliente permite Portal, no admin | Cookie administrativa por sí sola consulta portal: 401/403 | Intercambiar policy y esperar rojo |
| B12 | Calidad UI/Swagger | §9 en móvil/escritorio, 2 temas; Swagger ejemplo cuando haya DTO | Ejemplo comprobado con módulo apagado falsamente verde / axe falla | Desactivar aportación intencional y exigir rojo |

**Ejecutar sabotajes solo en bases/entornos efímeros.** No basta que un nombre de prueba incluya «seguridad»; los negativos deben dispararse y producir fallo atribuible. En igualdad de datos, verificar resultados con PostgreSQL real (EF→SQL), nunca solo fake.

## 9. Pantallas para el Paso 3.5 — estados completos

**Reglas comunes:** tokens y `frontend/src/shared/ui`, sin biblioteca UI nueva ni CSS con colores propios; contraste claro/oscuro, teclado completo, foco visible, móvil/escritorio, movimiento reducido. Microcopy español. Nada de IDs técnicos visibles. No construir UI antes de aprobación del diseño por JP.

### 9.1 P1 — Mi portal / Resumen

Tarjetas de cuenta (M04), pedidos (M03) y trabajos (M06), con acceso directo a cada sección habilitada. Con solo M04 hay pantalla útil, no cajón vacío. **Estados:** carga inicial con esqueleto; con datos propios; sin pedidos/trabajos; cada proveedor ausente (texto propio); fallo aislado de una tarjeta; sesión caducada (volver a `/entrar` con retorno seguro); sin autorización (no pintar datos previos). No prometer que una tarjeta ausente esté vacía.

### 9.2 P2 — Pedidos propios

Lista cronológica de códigos visibles, estado español, fecha, total y plazo para pagar. Si se ofrecen detalles, se usan endpoints seguros de M03. **Estados:** carga; lista; vacío real; sin M03; fallo; código ajeno/inexistente (404 uniforme); límites de 20 de M03 en API propia / parámetros acotados en Contracts. No diseño de cobro nuevo ni cancelación.

### 9.3 P3 — Detalle de pedido propio

Información congelada del pedido y líneas solo desde `GET /api/sales/my-orders/{orderCode}`; se respetan las restricciones de presentación del estado Vencido. **Estados:** carga; éxito; 404 uniforme; M03 ausente; red caída; navegación de vuelta. No incluir dirección de entrega: M03 v1 es recojo en tienda.

### 9.4 P4 — Mis trabajos

Solo órdenes con vinculación formal `CustomerId` a sesión vigente, código visible, estado autoritativo y fecha prometida si existe. Si la orden no está vinculada, **no se muestra ni se deduce que exista**. **Estados:** carga; trabajos propios; vacío real; M06 no instalado (explicación); error; sesión cambiada; paginación acotada. No listar por nombre/correo.

### 9.5 P5 — Estado de un trabajo propio

Vista legible del progreso mediante la proyección aprobada de M05b/M06: estado actual y hechos públicos permitidos; sin prioridades, plazos internos, asignaciones, notas, actor ni información de otra cuenta. **Estados:** carga; disponible; 404 tanto para inexistente como ajeno; M06 ausente; conflicto si proveedor se desactiva durante consulta; error recuperable; retorno. Se prohíbe llamar API administrativa M06.

### 9.6 P6 — Cuenta / acceso existente

M08 enlaza la cuenta real M04 (`/mi-cuenta`), registro/entrada ya implementados. **No duplica pantalla editable**: solo tarjeta resumen en P1 y enlace. **Estados:** sesión presente; sin correo verificado (puede ver portal, no comprar); sesión vencida; cuenta no disponible. No formularios de contraseña ni direcciones dentro de M08.

### 9.7 Tabla transversal de composición

| Caso | Respuesta visual |
|---|---|
| M04 desactivado | M08 no monta ruta ni elemento de menú |
| Solo M04 | Portal y perfil disponibles, dos explicaciones de proveedor ausente |
| M03/M06 activos sin registros | Dos vacíos reales e independientes |
| M03 ausente, M06 activo | Trabajos visibles sin romper resumen |
| M06 ausente, M03 activo | Pedidos visibles sin mencionar un trabajo inexistente |
| Proveedor cae después de cargar | Estado de error local, borrar datos potencialmente obsoletos de esa sección |
| Cambio de cliente A a B en misma pestaña | Invalidar todas las consultas cacheadas; nunca enseñar datos de A |
| M08 apagado y proveedores encendidos | No ruta/navegación Portal; M04/M03/M06 intactos |

## 10. Criterios de aceptación y cierre de módulo

D1–D6 se ratificaron el 09/10/2026; para cierre funcional futuro exigir:

- [ ] Contratos nuevos de pertenencia a trabajos aprobados y publicados por dueños M05b/M06 antes del consumidor.
- [ ] Solo M04 autentica cuentas; GET del portal exige la política `crm:customer`, no acepta identidad suministrada.
- [ ] Matriz completa §3 verde en PostgreSQL real y E2E, sin proveedores activos/inactivos omitidos.
- [ ] Negativos cliente A/B, `CustomerId=NULL`, enumeración por código y no filtrado en navegador.
- [ ] Ausencia de notas internas en cualquier JSON y DOM; sabotaje deliberado da rojo.
- [ ] M08 no referencia código de dominio/DbContext/schema M03/M04/M05b/M06; solo fronteras publicadas.
- [ ] Modelo sin tablas o con tablas *clasificadas y ratificadas*; barrera combinada EF+`pg_constraint` si hay FK.
- [ ] §9 materializado desde diseño aprobado, con accesibilidad, temas, estados y móvil.
- [ ] Binario, host, setup y ruta de `migrate()`/`seed()` consistente con si el módulo tiene o no DDL, sin inventar tablas.
- [ ] `[M08-CICLO]` ejecutado **dentro de etapa 6 canónica**: instalar, activar, navegar, desactivar, desmontar/reinstalar si aplica y reactivar, protegiendo datos de CRM/Sales/ServiceOrders/Tracking. Ninguna omisión.
- [ ] QA independiente, `node scripts/verificar.mjs` 6/6, cero fallos y cero omitidas; adjuntar TRX, informe Playwright y limpieza de base efímera con SHA de candidata inmutable.
- [ ] JP autoriza explícitamente la integración del SHA concreto. No inferir autorización de una rama verde.

## 11. Registro de decisiones — RATIFICADAS POR JP, 09/10/2026

| ID | Decisión ratificada | Valor aprobado por JP | Consecuencia de una modificación futura |
|---|---|---|---|
| **D1** | ¿M08 guarda `portal.users`/`customer_profiles` propios? | **No**: M04 ya es dueño; Portal 1.0 sin persistencia | Rehacer §4, clasificar cada nueva tabla y crear migraciones |
| **D2** | ¿Qué trabajos puede ver una cuenta? | Solo M05b con `customer_id == CustomerId` verificado por contrato; nulos invisibles. Sin reclamación por código, correo o teléfono | Si quieren recuperación/vinculación, definir flujo explícito de propiedad y auditoría |
| **D3** | ¿Qué publica M06 exactamente a un cliente? | Estado/hitos públicos mínimos sin notas ni fechas internas; nuevo `Tracking.Contracts` | Cambiar DTO y pruebas negativas; dueño M06 conserva frontera |
| **D4** | ¿Sin M06 pero con M05b se muestran trabajos? | Portal informa «Seguimiento no disponible», **sin acceso directo a M05b** en v1 | Si sí, hace falta contrato de M05b visible, producto y matriz adicional |
| **D5** | ¿El portal ofrece detalles propios o solo resumen/enlace? | Incluir detalle de M03 mediante API propia existente; detalles de trabajo por contrato nuevo M05b/M06 | Cambia P3/P5 y la asignación de rutas, no la seguridad |
| **D6** | ¿Qué identificación/apariencia pública se adopta para trabajos? | Código visible y estados M05b; sin mostrar `Guid` ni identidades del personal; redacción en español | Afecta DTO + diseño, no autoridad del estado |

**Estado de Paso 1:** D1–D6 aprobadas. Los contratos §6 están ratificados como especificación pero su implementación sigue condicionada a revisión de los propietarios, compilación, PostgreSQL real y barreras de exposición; ningún contrato se declara «existente en main» antes de publicarse. M08 Paso 3 (API) requiere entrega y verificación contractual y autorización de rama propia; el Paso 4 (UI) espera al diseño 3.5 aprobado por JP. No se abre Paso 2 mientras no se cierre formalmente esta SPEC.

## 12. Handoff y orden de trabajo

1. JP ratificó D1–D6 y los contratos el 09/10/2026; Chat 2 coordina revisión e implementación focal por los propietarios M05b y M06.
2. Confirmar en ramas separadas las firmas reales, ejecución PostgreSQL y límites del DTO; cerrar Paso 1 cuando la evidencia corresponda exactamente al SHA publicado.
3. Agente A implementa en su rama propia, sin editar costuras de integración; las solicita a Chat 2 con diff/efecto y prueba.
4. Pasos 2 Datos (si corresponde), 3 API, 3.5 diseño por JP y 4 UI; ejecutar e2e focal y sabotajes; QA independiente en candidata congelada; integración autorizada por JP.
5. La preparación paralela de cuatro agentes para **módulos posteriores** se abre después de concluir este paquete M08, sin anticipar su implementación ni mezclar ramas.
