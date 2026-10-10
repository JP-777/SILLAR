# SILLAR — M08 | Diseño de API y matriz de pruebas de seguridad

**Fecha:** 10/10/2026, America/Lima.
**Naturaleza:** contrato de diseño para implementación futura, NO código ya ejecutable ni certificado.
**ID:** SILLAR-M08-P3-P5-20261010.
**Fuentes:** `963e0f1` (M04/M03/M05b/M06 y plataforma) y documentación M08 `54bd232`.

## 1. Flujo de datos permitido

1. Navegador usa la cookie de **cliente M04**, no la cookie de administrador.
2. Endpoint M08 exige `CustomerAuthorization.PolicyName` (`crm:customer`).
3. `ICurrentCustomer.CustomerId` es extraído por el servidor **en cada petición** y debe tener un `Guid` no vacío.
4. `ICustomerIdentityReader` obtiene nombre/correo/telefono públicos vigentes; la respuesta de Portal no expone `CustomerId` ni IDs técnicos.
5. Si están activos M03 o M06, el endpoint intenta resolver sus contratos opcionales mediante DI; no intenta leer schemas ni llamar endpoints administrativos.
6. M05b realiza filtro de propiedad dentro de PostgreSQL en lista y detalle; M06 solo proyecta `VisibleCode`, estado, fechas permitidas e ítems seguros.
7. Nunca enviar a cliente `ReceivedNotes`, `TrackingNotes`, `BoardPriority`, `InternalDueAt`, personal, identidad del nodo ni metadatos administrativos.

**Salvedad:** UI protegida por `RequireCustomerAuth` mejora navegación pero NO sustituye la autorización de cada endpoint de backend.

## 2. Rutas propuestas; decisiones ya ratificadas / diseño por concretar

| Ruta | Requiere | Respuesta | Prohibiciones |
|---|---|---|---|
| `GET /api/portal/overview` | `crm:customer` + identidad M04 | Secciones `profile`, `orders`, `work`; cada proveedor informa `available` / `empty` / `unavailable` / `error` de forma inequívoca | No recibe ni expone CustomerId; ningún SQL a proveedores; no propaga datos parciales prohibidos |
| `GET /api/portal/work/{visibleCode}` | `crm:customer` + identidad M04 | Detalle autorizado de M06 con código público y estado, fecha pública, ítems seguros | No usa `IServiceOrderTrackingSource.GetAsync` como bypass; ajeno e inexistente indistinguibles 404 |
| `GET /api/sales/my-orders/{orderCode}` | Endpoint **existente en M03**, autenticado | Detalle propio delegado a M03 | **No duplicar** un detalle Sales en M08 sin nuevo motivo y revisión |
| `/api/admin/tracking/*` | Administración M06 | **NO** utilizado por M08 | Nunca convertirlo en fuente del Portal |

**Diseño de secciones:** `available` (proveedor sano y datos), `empty` (proveedor sano sin filas), `unavailable` (proveedor no activo), `error` (proveedor activo falló). El esquema exacto del DTO debe fijarse antes de implementación y comprobarse con serialización real. Un proveedor que se desactiva en medio de la petición no debe dejar filtrar una respuesta anterior a otra sesión.

**Detalles de protocolo que aún son diseño:** GET sin escritura/CSRF; límites de consulta acotados (máx. actual de M05b = 20; M03 cuenta con acotación propia); fechas públicas reales solamente; errores no deben revelar existencia de órdenes ajenas. El estado `PromisedAt` solo se muestra si es una promesa pública real, nunca como equivalente del `internal_due_at` de M06.

## 3. Matriz de prueba a construir en PostgreSQL y HTTP

| ID | Escenario | Resultado requerido | Responsable de implementarlo | Estado |
|---|---|---|---|---|
| H01 | Sin cookie de cliente solicita overview | 401; no JSON personal | M08 backend | PENDIENTE |
| H02 | Solo cookie de administrador | 401 o rechazo por esquema; nunca datos de cliente | M08 backend | PENDIENTE |
| H03 | Cookie de cliente vencida/revocada | 401 sin reutilizar datos cacheados | M04/M08 integración | PENDIENTE |
| H04 | URL/query/header/body manipulado con CustomerId B | Se ignora/rechaza parámetro, la identidad sigue siendo A; nunca datos B | M08 backend | PENDIENTE |
| H05 | Cliente A lista órdenes M06 A/B y manual NULL | Solo A | M05b/M06 ya focal; endpoint M08 pendiente | PARCIAL (focal previa) |
| H06 | Cliente A pide trabajo de B por código conocido | 404 igual que código inexistente | M08 + contrato M06 | PENDIENTE |
| H07 | Orden NULL con email/teléfono/nombre de A | Nunca aparece | M05b ya focal; endpoint pendiente | PARCIAL |
| H08 | Nota/prioridad/vencimiento internos deliberadamente poblados | Campo y contenido prohibidos ausentes de HTTP crudo | M08 + M06 | PENDIENTE |
| H09 | M04 desactivado | Portal no se activa; no ruta cliente | Plataforma / M08 | PENDIENTE |
| H10 | M03 desactivado | `orders.unavailable`, profile/work independientes | M08 | PENDIENTE |
| H11 | M06 desactivado | `work.unavailable`, profile/orders independientes | M08 | PENDIENTE |
| H12 | M03 activo sin ventas | `orders.empty`, nunca `unavailable` | M08 | PENDIENTE |
| H13 | M06 activo sin servicios | `work.empty`, nunca `unavailable` | M08 | PENDIENTE |
| H14 | M03 activo falla mientras M06 responde | Solo sección orders = `error`; work conservada | M08 | PENDIENTE |
| H15 | M06 activo falla mientras M03 responde | Solo work = `error`; pedidos conservados | M08 | PENDIENTE |
| H16 | M08 desactivado | Endpoints `/api/portal/*` y menú desaparecen, otros siguen | Plataforma + frontend | PENDIENTE |
| H17 | Intento de POST/PUT/DELETE en Portal v1 | Sin operaciones Portal; no persistencia | M08 | PENDIENTE |
| H18 | Desmontaje y reinstalación de Portal | Sin DROP ajeno, sin schema portal; datos M04/M03/M05b/M06 intactos | Integración / QA | PENDIENTE |
| H19 | Desinstalación de M06 con M08 activo | Degradación prevista, no crash ni datos falsos | Integración / QA | PENDIENTE |
| H20 | Lista extensa | Límites se acotan y datos no autorizados no aparecen | M08 / proveedores | PENDIENTE |
| H21 | M04 customer no verificado pero sesión válida | Puede ver Portal; no inventar bloqueo de compras en Portal | M08 | PENDIENTE |
| H22 | La sesión cambia de A a B mientras se navega | Borra datos antiguos de UI, nunca renderiza caché de A con sesión B | M08 frontend/e2e | PENDIENTE |
| H23 | `PortalModule.Schema == null`, no `IModuleMigrations` | Activación sin schema portal, sin seed ni migraciones | M08/P2 | PENDIENTE |
| H24 | Eliminar guard de autorización deliberadamente | Test de seguridad se pone ROJO y vuelve VERDE al restaurar | QA interna y colíder | PENDIENTE |
| H25 | Quitar condición de montaje frontend deliberadamente | Test de ruta muerta se pone ROJO y se restaura VERDE | QA interna y colíder | PENDIENTE |

**Cobertura ya ejecutada antes de implementar M08:** M05b 47/47, M06 35/35 y dos secuencias VERDE→ROJO→VERDE en SHA `963e0f1`. No contar las 25 pruebas anteriores como realizadas: son **criterios de aceptación pendientes**.

## 4. Comprobaciones estructurales obligatorias

- Backend Portal solo referencia `*.Contracts` de proveedores; prohibir dependencia de `Sillar.Modules.Crm`, `Sillar.Modules.Sales`, `Sillar.Modules.ServiceOrders` y `Sillar.Modules.Tracking` (concretos), y prohibir `DbContext`/SQL de esos módulos en Portal.
- `PortalModule` debe declarar `Schema => null`, `HardDependencies => ["core", "crm"]`, `SoftDependencies => ["sales", "tracking"]`, y no implementar `IModuleMigrations`.
- La API Portal debe registrarse en el host únicamente cuando `portal` esté activo; dependencia M04 significa que CRM también lo está.
- `frontend/src/app/routes.tsx` necesita la costura de `has('portal') && portalPublicRoutes`; ruta dentro del `PublicLayout`, protegida con `RequireCustomerAuth`; ninguna ruta /admin es acceso sustituto a M08.
- Añadir el módulo a `backend/Sillar.Api/Sillar.Api.csproj` y `backend/Sillar.sln` como costura de integración; comprobar binario descubierto, activación y rutas.
- Alinear e2e `migrate()+seed()` y `scripts/verificar.mjs`: Portal v1 no tiene schema; no agregar migraciones fantasma, pero sí comprobar activación, ciclo etapa 6 y barreras de omisión.
- La UI debe cubrir todas las vistas P1–P6 y estados de la SPEC actual, con diseño de JP pendiente de validación donde corresponda; no inventar aprobaciones visuales.

## 5. Política de fallos y examen del colíder

**Bloquean exposición del Portal:** falta de autenticación; identidad extraída de dato externo; falta de filtro propietario en lista/detalle; fuga de campos internos; endpoint administrativo reutilizado; fuga entre sesiones; módulos opcionales convertidos accidentalmente en dependencias duras.

**No constituyen automáticamente rechazo de diseño:** M03/M06 ausentes con explicación correcta, listas vacías verdaderas, y ausencia de schema de Portal prevista por D1.

La certificación independiente deberá reproducir escenarios de riesgo sobre el SHA exacto de implementación y verificar la puerta canónica de seis etapas, incluido `[M08-CICLO]` en etapa 6. Hasta entonces **HOLD MERGE A MAIN**.
