# Decisiones previas de M08 Portal del Cliente

**Creación:** 26 de septiembre de 2026 — America/Lima
**Última verificación:** 26 de septiembre de 2026 — America/Lima
**Commit base verificado:** `711bfba7cf3be80baa146b44e79ddf7a633d695d` (`origin/main`)
**Estado:** propuesta — **no es SPEC**. Se detiene antes del paso 2 porque la pregunta de
producto del §4 está sin responder.

Este documento no sustituye al SPEC. Registra qué sirve ya M04, qué le quedaría a M08 y qué
contradicen los documentos compartidos, para que el SPEC —si llega a escribirse— no tenga que
reconstruirlo.

Taxonomía de cada afirmación: **LEÍDO** (cita de archivo y línea), **OBSERVADO** (salida de un
comando ejecutado en esta verificación), **DEDUCIDO** (conclusión que no está escrita en ninguna
parte y que JP debe confirmar).

---

## 1 · Lo que M04 ya sirve a la clientela

M04 está declarado dueño de la identidad del cliente: «su cuenta para entrar a la tienda»
(`docs/modules/crm/SPEC.md:20-21`), y «tiene su propia identidad, sus propias sesiones y su propio
esquema» (`docs/modules/crm/SPEC.md:30-31`). **LEÍDO.**

| Capacidad | Backend | Frontend | Fuente |
| --- | --- | --- | --- |
| Registro | `POST /api/customer/auth/register` | `/crear-cuenta` | `CustomerAuthEndpoints.cs:34`, `routes.tsx` (crm) |
| Entrar / salir | `POST …/login`, `POST …/logout`, `GET …/me` | `/entrar`, botón «Cerrar sesión» | `CustomerAuthEndpoints.cs:23,92,110`; `CustomerProfilePage.tsx:91-93` |
| Recuperar contraseña | `POST …/password-reset/request`, `…/confirm` | `/recuperar-contrasena`, `/restablecer-contrasena` | `CustomerAuthEndpoints.cs:45-64` |
| Verificación de correo | `POST …/email-verification/confirm`, reenvío | `/verificar-correo`, aviso en el perfil | `CustomerAuthEndpoints.cs:69-75,103-107`; `CustomerProfilePage.tsx:98,130-166` |
| Invitación del personal | `POST /api/admin/crm/customers/{id}/invite`, `POST …/invitation/accept` | `/activar-cuenta` | `CustomerAdminEndpoints.cs:71`; `CustomerAuthEndpoints.cs:79-85` |
| Datos personales | `GET`/`PUT /api/customer/profile` | `/mi-cuenta` | `CustomerProfileEndpoints.cs:25-32` |
| Direcciones (alta, edición, preferida, baja) | `POST`/`PUT`/`DELETE …/profile/addresses…` | `/mi-cuenta` | `CustomerProfileEndpoints.cs:39-62` |
| Cierre de sesiones al restablecer | regla de dominio | — | `docs/modules/crm/SPEC.md:201-204` |
| Hueco explicado de pedidos | en la **ficha del panel** | `AdminCustomerDetailPage` | `docs/modules/crm/SPEC.md:337-345` |

**Contratos públicos de M04** (`backend/Sillar.Modules.Crm.Contracts/`, **LEÍDO**):

- `ICurrentCustomer` — `CustomerId`, `Email`, `EmailVerified` (`ICurrentCustomer.cs:10-20`).
- `ICustomerSnapshotReader.GetForOrderAsync(customerId, customerAddressId)` — instantánea para un
  pedido (`ICustomerSnapshotReader.cs:10-16`).
- `CustomerAuthorization.PolicyName = "crm:customer"` (`CustomerAuthorization.cs:4-10`).
- `CustomerCsrfEndpointFilter` (`CustomerCsrfEndpointFilter.cs:13`).

**Lo que M04 no sirve hoy** (**LEÍDO** por ausencia en los endpoints citados): cambiar la contraseña
estando dentro, y listar o cerrar las sesiones abiertas. **Las dos son identidad**, y por
`crm/SPEC.md:30-31` pertenecen a M04, no a M08. **DEDUCIDO.**

## 2 · Matriz de solapamiento

El mandato de M08 en los documentos compartidos es: «Cuentas de cliente, historial y consulta de
estados» (`docs/ARQUITECTURA_MODULAR.md:116`, `docs/ROADMAP_MODULAR.md:108`). **LEÍDO.**

| Parte del mandato | ¿Ya lo sirve M04? | ¿Qué le quedaría a M08? | Clase |
| --- | --- | --- | --- |
| Cuenta: registro, entrada, recuperación, verificación | **Sí**, entero (§1) | Nada | (a) componible por contrato — pero no hay nada que componer: ya es una pantalla |
| Datos personales y direcciones | **Sí**, `/mi-cuenta` | Nada | (a) |
| Cambio de contraseña con sesión, gestión de sesiones | No | Nada: es identidad → petición a M04 | fuera de M08 |
| Historial de pedidos | No | Sección que lee el contrato de M03 | (c) **PENDIENTE** — disparador: M03 publica su contrato |
| Estado de trabajos / órdenes de servicio | No | Sección que lee el contrato de M06 | (c) **PENDIENTE** — disparador: M06 publica su contrato pertinente |
| «Funciona con solo el perfil si no hay ninguno» (`ARQUITECTURA_MODULAR.md:116`) | **Sí**: eso es `/mi-cuenta` hoy | Nada | (a) |

**(b) Capacidad genuinamente nueva de M08 en v1, con pedidos y trabajos aplazados: ninguna
encontrada.** **DEDUCIDO**, a partir de la matriz.

Y el propio SPEC de M04 ya reserva el hueco de los pedidos en el perfil de la tienda: «el perfil
—datos, direcciones, y más adelante sus pedidos» (`docs/modules/crm/SPEC.md:334-335`). **LEÍDO.**

## 3 · Estado de las dependencias blandas

| Módulo | Estado en `main` (`711bfba`) | Contrato publicado |
| --- | --- | --- |
| M03 Ventas | Sin proyecto `Sillar.Modules.Sales*` en `backend/` (**OBSERVADO**, `ls backend`). La rama `m03-ventas-online` (`617bb28`) solo trae documentos en `docs/modules/sales/` y `database/modules/sales/.gitkeep` (**OBSERVADO**, `git ls-tree`) | **No** |
| M06 Seguimiento | Sin proyecto ni carpeta de documentación. Depende duro de M05b (`ARQUITECTURA_MODULAR.md:62`), que tampoco existe | **No** |

M03 y M06 son dependencias blandas de M08 (`docs/ARQUITECTURA_MODULAR.md:64`). **LEÍDO.** Los
trabajos no los define M03: vienen de M06 sobre M05b (`ARQUITECTURA_MODULAR.md:61-62`). **LEÍDO.**

## 4 · Pregunta de producto — ABIERTA, para JP

> **Con pedidos y trabajos aplazados, M08 no tiene un valor funcional autónomo en v1.** ¿Qué es
> M08?

Opciones, sin elegir:

1. **M08 se aplaza entero** hasta que M03 publique su contrato. Hasta entonces no hay SPEC ni
   esquema.
2. **M08 se disuelve**: cada módulo aporta su sección a `/mi-cuenta` de M04 (M03 «Mis pedidos», M06
   «Mis trabajos»), por un punto de extensión de plataforma parecido a `HomeSection`. M08 deja de
   ser vendible por separado.
3. **M08 existe como contenedor de historial**: sin tablas propias, compone secciones de M03 y M06
   y enseña su ausencia explicada. Solo tiene sentido si JP quiere licenciar el historial aparte de
   la cuenta.

Lo que **no** es una opción: dar a M08 identidad, sesiones o una tabla de clientes. Lo prohíbe
`docs/modules/crm/SPEC.md:30-31`.

## 5 · Contradicciones en documentos compartidos — para Chat 2

No se editan desde aquí: son territorio de Integración (`docs/DIVISION-DE-TRABAJO.md:38-47`).

| Cita | Dice | Choca con |
| --- | --- | --- |
| `docs/ARQUITECTURA_MODULAR.md:200` | El esquema `portal` crea `users` y `customer_profiles` | La identidad es de M04 (`ARQUITECTURA_MODULAR.md:102`; `crm/SPEC.md:30-31`). Una tabla `portal.users` sería la segunda identidad |
| `docs/ARQUITECTURA_MODULAR.md:226` | `portal.customer_profiles.customer_id → crm.customers` como FK **prohibida** en el script base, y a la vez «dura» | Con dependencia dura la FK cruzada está permitida (`CLAUDE.md`, «Claves foráneas entre schemas»). La línea se contradice a sí misma, y además describe una tabla que no debería existir |
| `docs/ROADMAP_MODULAR.md:110` | M08 «introduce autenticación de clientes finales» | Ya la introdujo M04 (`ROADMAP_MODULAR.md:67`) |
| `docs/adr/ADR-010-autenticacion-administrativa.md:43` | «La autenticación de clientes finales pertenece al módulo M08» | Igual. La ADR no se edita: hace falta una nota o una ADR que la enmiende |
| `docs/DIVISION-DE-TRABAJO.md:24-28` | Territorios: Frente A = M03, Frente B = M07, Integración | M08 no tiene frente asignado |

## 6 · Condiciones del paso 2, comprobadas

| Condición | Estado | Evidencia |
| --- | --- | --- |
| (1) Sin duplicación de M04 y con función propia ratificada | **No cumplida**: sin función propia (§2) y la pregunta del §4 está abierta | §2, §4 |
| (2) Comprobación general de fronteras en `main` y llamada en la etapa 1 | **No encontrada.** La etapa 1 ejecuta vocabulario de auditoría, errores del API, seguidor de conexión, fallos de arranque, higiene del frontend y tipos (`scripts/verificar.mjs:2027-2036`). Ninguno comprueba imports entre módulos (las pruebas de `frontend/tests/frontendHygiene.test.mjs` son H-12…H-20 y §18). `git log origin/main --grep=fronter` no devuelve nada (**OBSERVADO**) | `scripts/verificar.mjs:2027-2036` |
| (3) Entorno capaz de migrar en una base efímera | Ver el informe de la puerta de esta sesión | Informe de la puerta |

**Ningún paso de código ni de esquema arranca** mientras (1) y (2) no se cumplan.
