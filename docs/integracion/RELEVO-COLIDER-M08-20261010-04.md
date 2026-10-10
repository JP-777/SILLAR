# SILLAR — Relevo cronológico al colíder | Acta M08-04

**Fecha:** sábado 10 de octubre de 2026, America/Lima (hora exacta no asentada).
**ID:** SILLAR-COLIDER-M08-20261010-04.
**Autores del avance:** JP (instrucción de proseguir sin colíder) y Chat 2 (auditoría estática y documentación).
**Naturaleza:** análisis de arquitectura y definición de pruebas; **NO CERTIFICA** código nuevo.

## 1. Posición cronológica y SHA

- **09/10/2026 23:36 (Lima):** contrato M05b/M06 publicado en `963e0f1d12b131c4296cd836c88c3334fc22288e`; 82/82 regresiones reportadas y posterior ensayo VRV de cinco TRX.
- **10/10/2026:** acta anterior y documentos Portal publicados, rama `docs/m08-paso1-propuesta-20261009` en `54bd232e0c4c4fe4959bd41be2dcc57a6f6bf6fe` antes de este avance.
- **10/10/2026 (esta acta):** Chat 2 contrasta la base del host, interfaces CRM/Sales y el mecanismo de persistencia/activación, y documenta Paso 2 y matriz Paso 3/5. No crea `PortalModule`, endpoints ni modifica `main`.
- `main` comprobado al comienzo de este análisis: `4a3fd1ef12fc2226bad185f003acea9168b9a4c9`. **Verificar de nuevo al publicar**, pues puede cambiar.

## 2. Evidencia nueva (inspección de código de GitHub, commit contractual `963e0f1`)

1. `IModule.Schema` permite `null`, `DemoModule` lo implementa de modo explícito, y `ModuleActivationService` solo requiere schema de PostgreSQL cuando la propiedad no es nula. **D1 es arquitectónicamente viable**, sin tablas ni migraciones Portal.
2. `ModuleBootstrapper` registra solo servicios de módulos activos y `Program.cs` solo mapea endpoints activos. Un Portal apagado **debería** no registrar endpoints; falta demostrarlo con el módulo implementado.
3. `CrmModule` registra `ICurrentCustomer` e `ICustomerIdentityReader` y declara política de cliente; `CurrentCustomer` lee la identidad de claims de sesión, y `CustomerSessionAuthenticationHandler` consulta sesiones reales en CRM, comprueba vencimiento/revocación/cliente activo.
4. M03 expone `ICustomerOrderHistory` y su endpoint propio de detalle autorizado; reutilizarlos evita dependencia a SalesDbContext.
5. M06 `ICustomerTrackingProgress` delega propiedad a M05b y no lee notas propias; sigue sin existir endpoint HTTP Portal hasta su implementación.
6. `frontend/src/app/routes.tsx` solo monta rutas de módulos con capability activa; M04 ofrece `RequireCustomerAuth`, reutilizable por Portal, y la ruta `/mi-cuenta` ya existe.

**Alcance de este PASS:** inspección estática de patrones; **NO** se ejecutaron nuevas pruebas durante esta acta, ni se validó un binario M08.

## 3. Entregables del Paso 2/3 preparados

- `docs/modules/portal/PASO2-ARQUITECTURA-SIN-PERSISTENCIA-20261010.md`: declaración objetivo `Schema => null`, grafo dura/blandas, fronteras, ausencia de DDL y matriz de activación.
- `docs/modules/portal/CONTRATO-API-Y-MATRIZ-PRUEBAS-20261010.md`: rutas y DTO de diseño, estados por sección, y 25 escenarios de aceptación de seguridad, disponibilidad, e2e y sabotajes.

Estos documentos son diseño de implementación, no autorización de integrar, no reemplazan la SPEC ratificada ni modifican D1–D6.

## 4. Pendientes del colíder, en orden de auditoría

1. Auditar D1 en `PortalModule`: `Schema => null`, ninguna migración/FK/seed propia, schema/activación en una base efímera.
2. Auditar dependencias duras (`core`, `crm`), blandas (`sales`, `tracking`) y ausencia de referencias concretas/DbContext de módulos ajenos.
3. Auditar autorización de endpoints Portal: policy `crm:customer`, `ICurrentCustomer.CustomerId` desde cookie validada, 401 sin sesión, ajeno 404 y parámetros manipulados sin fuga.
4. Auditar DTO JSON crudo con datos centinela de M05b/M06: notas, prioridades, vencimientos y personal no deben estar presentes.
5. Auditar degradación y errores independientes: proveedores inactivos/activos-vacíos/fallidos, reinicio/desactivación de Portal y cambio de sesión.
6. Ejecutar y comprobar sabotajes VERDE→ROJO→VERDE nuevos de auth y montaje; no contar como aprobados los casos aún solo diseñados.
7. Reproducir puerta canónica `node scripts/verificar.mjs`, cero skipped, `[M08-CICLO]` etapa 6, sin destrucción de datos ajenos y con evidencias firmadas por SHA.

**Estado final:** Paso 2 viable en arquitectura mediante inspección; diseño Paso 3/5 definido para revisión; implementación, QA independiente y `main` retenidos. M08 **NO ESTÁ TERMINADO**. No empezar todavía la planificación de módulos de la semana siguiente hasta agotar el trabajo de M08 o recibir instrucción nueva de JP.
