# SILLAR — M08 Portal del Cliente | Paso 2: arquitectura sin persistencia

**Fecha de análisis:** sábado 10/10/2026, America/Lima.
**Estado:** análisis estático de código REALIZADO, implementación y QA de módulo PENDIENTES.
**Documento:** SILLAR-M08-P2-20261010.
**Autoría de análisis:** Chat 2; decisiones de producto D1–D6 ratificadas previamente por JP.
**Contratos revisados:** `963e0f1d12b131c4296cd836c88c3334fc22288e`.
**Documentación vigente en rama:** `54bd232e0c4c4fe4959bd41be2dcc57a6f6bf6fe`.
**Base histórica de main verificada en este corte:** `4a3fd1ef12fc2226bad185f003acea9168b9a4c9`.

## 1. Pregunta del Paso 2 y respuesta

**Pregunta:** ¿M08 necesita tablas, schema PostgreSQL, migraciones, seed o mecanismo de identidad nuevos?

**Respuesta de diseño: NO.** D1 ratifica que M08 v1 agrega lecturas existentes de M04, M03 y M06 (M06 a su vez consume el contrato seguro M05b). No almacena entidades propias ni cambia estados. El `CustomerId` pertenece a la sesión de M04 y no se persiste en M08. El diseño requiere **cero** tablas, FK, DDL, migraciones, seeds y scripts `99_drop.sql` propios.

**Verificación de viabilidad en código:**

1. `backend/Sillar.Shared/Modularity/IModule.cs`: `Schema` es `string?` y por defecto devuelve `Code`; permite sobrescritura `Schema => null` para un módulo sin persistencia.
2. `backend/Sillar.Modules.Demo/DemoModule.cs`: implementación concreta del patrón sin schema (`public string? Schema => null`), solo de referencia de la plataforma; **no reutilizar código de demostración en producción**.
3. `backend/Sillar.Core/Services/ModuleActivationService.cs` (alrededor de las líneas 176–195): comprueba que exista el schema **solo cuando** `module.Schema is { } schema`; con `null` no exige `portal`.
4. `backend/Sillar.Api/Modularity/ModuleBootstrapper.cs`: registra servicios únicamente de módulos activos; `backend/Sillar.Api/Program.cs` mapea endpoints únicamente de módulos activos.
5. `e2e/setup/migrate.ts`: invoca migraciones únicamente de los módulos persistentes conocidos; M08 **no** debe añadirse falsamente a esa secuencia ni inventar archivos `database/modules/portal/02_seed.sql` o `99_drop.sql`.

**Resultado:** viabilidad arquitectónica para un módulo sin schema = **PASS ESTÁTICO**. No confundir con prueba real de activación: todavía no existe `PortalModule`.

## 2. Declaración prevista de módulo (DISEÑO, no código implementado)

```csharp
// Firma orientativa que deberá ajustar e implementar el frente dueño de M08.
public sealed class PortalModule : IModule
{
    public string Code => "portal";
    public string? Schema => null; // CRÍTICO: de lo contrario la activación buscaría schema portal
    public string DisplayName => "Portal del Cliente";
    public string Description => "Consulta privada de cuenta, pedidos y seguimiento de trabajos propios.";
    public string Version => "1.0.0";
    public int DisplayOrder => 80; // Propuesta: verificar numeración con módulos reales antes de escribir
    public string[] HardDependencies => ["core", "crm"];
    public string[] SoftDependencies => ["sales", "tracking"];
    public void RegisterServices(IServiceCollection services, IConfiguration configuration) { /* lectura/composición */ }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) { /* API autenticada */ }
}
```

- **No implementar `IModuleMigrations`** en Portal; no registrar `PortalDbContext`.
- La clave de módulo es `crm`, no `M04`; `sales` = M03, `tracking` = M06. No añadir `service_orders` como dependencia directa en v1.
- M06 ya declara dependencia dura de `service_orders` y su lectura para clientes delega en `ICustomerServiceOrderReader`; Portal no lee `ServiceOrdersDbContext`, `TrackingDbContext` ni SQL de otros schemas.
- En `Sillar.Api.csproj` deberá existir una referencia de proyecto al futuro módulo Portal para que su DLL sea descubierta. Esa costura es propiedad de Integración/Chat 2.
- La ruta pública React deberá activarse solo con `has('portal')` y sesión del cliente, dentro del `PublicLayout`; la edición de `frontend/src/app/routes.tsx` es costura compartida.

## 3. Contratos elegidos (verificados)

| Necesidad de M08 | Frontera existente | Uso correcto |
|---|---|---|
| Cliente autenticado | `Sillar.Modules.Crm.Contracts.ICurrentCustomer` | Solo `CustomerId` extraído de sesión validada; la sesión usa `crm:customer`. Nunca en URL/query/header/body del navegador |
| Perfil mínimo | `Sillar.Modules.Crm.Contracts.ICustomerIdentityReader.GetAsync(customerId)` | Proyectar `FullName`, `Email` y, si corresponde, `Phone`; **no serializar su `CustomerId`** |
| Pedidos propios | `Sillar.Modules.Sales.Contracts.ICustomerOrderHistory.ObtenerPedidosDeAsync(customerId, limit, ct)` | Solo tarjetas; si no hay provider, estado `unavailable`, no lista vacía |
| Detalle de pedido | M03 existente: `GET /api/sales/my-orders/{orderCode}` | Reutilizar la API de M03, protegida con sesión y filtro SQL; no leer SalesDbContext en M08 |
| Avance de trabajos | `Sillar.Modules.Tracking.Contracts.ICustomerTrackingProgress` | Lista/detalle de M06, que delega propiedad en M05b; sin notas ni datos internos |

**Importante:** las interfaces nuevas de M06/M05b están publicadas en **rama contractual** `963e0f1`, no incorporadas a `main` en este corte. Crear futura rama M08 desde una base que las incluya; no declarar a `main` como si ya contuviera estos tipos.

## 4. Combinaciones de activación que deben demostrarse

| M04 | M03 | M06 | Resultado observable esperado | Estado de prueba |
|---|---|---|---|---|
| OFF | cualquiera | cualquiera | Portal no puede estar activo; no hay ruta M08 | PENDIENTE de implementación |
| ON | OFF | OFF | Portal útil con perfil; tarjetas de pedidos/trabajos `unavailable` | PENDIENTE |
| ON | ON | OFF | Perfil + pedidos; seguimiento `unavailable` | PENDIENTE |
| ON | OFF | ON | Perfil + trabajos autorizados; pedidos `unavailable`; M05b activo por dependencia de M06 | PENDIENTE |
| ON | ON | ON | Perfil + pedidos y trabajos propios | PENDIENTE |
| ON | ON sin filas | ON sin filas | Proveedores `available` y secciones `empty`, NO `unavailable` | PENDIENTE |
| ON | ON con fallo | ON sano | Aislamiento: error en pedidos, trabajos funcionales | PENDIENTE |
| ON | ON sano | ON con fallo | Aislamiento: pedidos funcionales, error en trabajos | PENDIENTE |
| ON | cualquiera | cualquiera, Portal OFF | No hay endpoints ni navegación M08; otros módulos intactos | PENDIENTE |

**Control de consistencia:** `ModuleGraph.ValidateActivations` prohíbe dejar un módulo activo sin dependencia dura; por ello una instalación con M06 activo y M05b inactivo no constituye estado legítimo de prueba. Debe comprobarse que el panel bloquea esa desactivación o exige primero desactivar M06, sin forzar estados ilegales en PostgreSQL.

## 5. Propiedad de archivos, riesgos y límite de etapa

**Territorio futuro del implementador M08:** `backend/Sillar.Modules.Portal/**`, pruebas propias `backend/Sillar.Modules.Portal.Tests/**`, `frontend/src/modules/portal/**` y e2e propio ratificado.

**Costuras reservadas a Chat 2/Integración:** `backend/Sillar.Api/Sillar.Api.csproj`, `backend/Sillar.sln`, `frontend/src/app/routes.tsx`, navegación/portada compartida, arnés e2e y puerta canónica. Solicitudes de cambios por efecto observable, nunca ediciones concurrentes.

**Posibles fallos que invalidarían D1:** `PortalModule` sin `Schema => null`, creación de migraciones o tablas, dependencias duras indebidas M03/M06, lectura de SQL ajeno, dependencia a entidades/DbContext de proveedor, rutas M08 activas con Portal desactivado, o aceptación de un `CustomerId` enviado por HTTP.

**Dictamen interno del Paso 2 (solo diseño):** **VIABILIDAD ESTÁTICA VALIDADA; no se requiere nueva decisión de JP sobre persistencia.** La implementación, su ejecución, el paso canónico y el dictamen independiente siguen pendientes. El colíder debe reproducir la comprobación en el SHA de implementación futuro.
