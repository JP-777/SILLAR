# SILLAR — Acta cronológica M08-05 | Backend inicial del Portal

**Fecha:** sábado 10 de octubre de 2026, America/Lima (hora exacta de pruebas no incluida en la salida).
**Identificador:** SILLAR-COLIDER-M08-20261010-05.
**Preparó:** Chat 2 (coordinación técnica). **Ejecutó:** JP en entorno local SILLAR.
**Tipo de evidencia:** desarrollo de rama, compilación y pruebas focales internas; **no constituye auditoría independiente**.

## 1. Posición cronológica

- Candidata contractual base: `963e0f1d12b131c4296cd836c88c3334fc22288e`, rama `integration/m08-contratos-m05b-m06-20261009`.
- Acta 04 de arquitectura/API: `8acae1d456540bf3d5bf3d1e6d9863f3d92fd737`, rama documental.
- Incremento backend M08: **`91d617606b078cc2692dab6b82aca9a7f1cccb37`**, rama `feature/m08-portal-backend-manual-20261010`.
- Revisión del colíder: diferida por falta temporal de disponibilidad. No se sustituye con QA propia.

## 2. Código entregado bajo el SHA auditado

Seis archivos nuevos, fuera de `main`, en `backend/Sillar.Modules.Portal/**` y `backend/Sillar.Modules.Portal.Tests/**`:

1. `PortalModule.cs`: `Schema => null`, `HardDependencies => ["core", "crm"]`, `SoftDependencies => ["sales", "tracking"]`; sin `IModuleMigrations`.
2. `Application/PortalReader.cs`: composición de `ICurrentCustomer`, `ICustomerIdentityReader`, `ICustomerOrderHistory` e `ICustomerTrackingProgress`, límites acotados y estados de secciones.
3. `Endpoints/PortalEndpoints.cs`: diseño implementado de `GET /api/portal/overview` y `GET /api/portal/work/{visibleCode}`, protegido por política `crm:customer`.
4. `Sillar.Modules.Portal.csproj`: referencias a interfaces de proveedores y Sillar.Shared, no a dominios ni DbContexts concretos.
5. `PortalReaderTests.cs`: seis pruebas focales con proveedores simulados.
6. `Sillar.Modules.Portal.Tests.csproj`: proyecto de pruebas separado.

**Nota:** estas rutas existen en el módulo compilado, pero **no están integradas ni expuestas por el host Sillar.Api**; faltan costuras en API, solución y frontend. No son endpoints ejecutados con HTTP real.

## 3. Evidencia reproducible aportada por JP

- Compilación focal de `Sillar.Modules.Portal.csproj`: **0 errores y 0 advertencias**, duración informada 18,14 s.
- Pruebas unitarias de M08: **6/6 aprobadas, 0 fallidas, 0 omitidas**, duración informada 367 ms.
- Evidencia local de ejecución: `/var/tmp/sillar-m08-backend-20261010-14mB2y`.
- SHA-256 `build.log`: `99fed0be18e8cacedc0e91a2242f20fcd0f81871e50afeb2a888541122233a35`.
- SHA-256 `test.log`: `e9e0cfac2de94aefa405c9903206ef78e90e8ec919d37a46b8fae94f2b7570f7`.
- SHA-256 `M08-Portal-Backend.trx`: `e4677bf4eca3c35d692d57a0527badf25c636127e3a9002d6d970101eb54d2ab`.
- El cotejo del commit confirma los seis archivos originales y la base contractual; `main` no fue modificada.

**Importante:** logs y TRX permanecen en `/var/tmp` del equipo de JP, no en GitHub. El colíder deberá poder reproducirlos sobre el commit publicado; no debe suponerse acceso a ese `/var/tmp` desde otra máquina.

## 4. Revisión preliminar de Chat 2 (estática, no independiente)

- El Portal usa `Schema => null`; por diseño D1 no requiere tablas ni migraciones.
- Las dependencias duras/blandas coinciden con las ratificadas. M06 ya filtra propiedad vía M05b; M08 no consulta SQL ajeno.
- La política de endpoints exige la sesión de cliente de M04. `CustomerId` se toma del contrato de sesión y no de query, URL, header ni body.
- Los DTO de la lectura usan contratos públicos de M03/M06 y perfil de M04. No se serializa un `CustomerId` en la respuesta diseñada.
- Se manejan por separado proveedor `unavailable`, sección vacía `empty`, resultados `available` y excepciones `error`.
- **Limitación relevante:** los tests usan dobles; no prueban filtros y estado real de HTTP, enrutamiento real, DI completo ni fallos con PostgreSQL. El detalle de trabajo depende de la protección de M04 y el contrato M06; deberá probarse con datos A/B/NULL.

## 5. Barreras que conserva el colíder

| Barrera | Estado al concluir este avance |
|---|---|
| Inspección independiente del SHA del Portal y su base contractual | PENDIENTE |
| Montaje/activación/desactivación en Sillar.Api, DI y grafo real | PENDIENTE |
| HTTP real 401 sin sesión, solo admin 401, 404 orden ajena | PENDIENTE |
| Protección frente a `CustomerId` arbitrario y cookie caducada | PENDIENTE |
| JSON real sin notas internas, prioridad, vencimiento y personal | PENDIENTE |
| Separación de error/empty/unavailable con M03/M06 activados y apagados | PENDIENTE |
| Sabotajes nuevos de auth/montaje con retorno a verde | PENDIENTE |
| QA canónica `node scripts/verificar.mjs` 6/6 y `[M08-CICLO]` | PENDIENTE |
| Revisión visual del §9 y frontend Portal | PENDIENTE |

## 6. Continuación autorizable sin el colíder

Preparar costuras de API y solución en una candidata separada; construir pruebas HTTP con host efímero y PostgreSQL; implementar UI de Portal según §9 bajo revisión de producto de JP. Publicar SHA y nuevas actas después de cada avance, sin fusionar a `main` ni falsificar QA independiente.

**Estado de salida:** M08 BACKEND FOCAL VERDE, PUBLICADO EN RAMA; M08 API INTEGRADA AL HOST NO; FRONTEND NO; QA INDEPENDIENTE PENDIENTE; HOLD MERGE A `main`.
