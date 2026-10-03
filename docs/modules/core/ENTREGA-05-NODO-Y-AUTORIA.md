# CORE · Entrega 05 — Nodo de las cuentas y autoría de los medios

**Creación:** 03/10/2026, America/Lima
**Base:** `main` = `4561d9b48664bfef7da9d174b0ed686fd5c0c71f`
**Rama:** `core/admin-home-node-y-autor-de-medios`
**Escrito por:** Claude Code D, por encargo ratificado de Chat 2 vía JP
**Estado:** candidata publicada, **sin certificar**. No incorpora `main` (instrucción del colíder:
se rebasará sobre `main` cuando entre §j, con autorización expresa)

Refina el `SPEC.md` de CORE dentro de su alcance; el SPEC ya recoge las filas nuevas (§4.5, §4.8, §4.10, §5).

---

## 1 · El encargo, tal como se ratificó

1. `admin_users.home_node`: NULL → relleno → comprobar 0 NULL → NOT NULL.
2. Relleno desde `Sillar:Node:Code` / `NodeIdentity`. **Nunca literal.**
3. `ICurrentAdmin`: nombre visible y nodo de pertenencia, conservando las propiedades actuales.
4. Actualizar `CurrentAdmin` y todos sus consumidores y dobles.
5. `media_assets`, **en la misma migración**:
   - fotografía inmutable del autor: nombre, id local y `created_by_admin_user_home_node`;
   - ese nodo se copia de `admin_users.home_node`, **no** de `media_assets.origin_node`, y la
     fotografía **no relee** `NodeIdentity`;
   - fuera `fk_media_assets_created_by`;
   - autor NULL = válido; autor inexistente = corrupción → la migración aborta, informa la cantidad
     y cómo localizar las filas.
6. Orden obligatorio: `admin_users` completo **primero**, `media_assets` **después**.
7. Pruebas: instalación limpia; migración sobre datos preexistentes; autor válido, NULL y corrupto;
   `ICurrentAdmin` actual y nuevo contra PostgreSQL real.
8. Barrido completo de referencias antes de editar.
9. Toda decisión queda escrita en el repositorio (este documento).

Fuera de alcance, por instrucción: M03, M07, M05a, `IsActive`, `ISettingsReader.Get<T>`, `main`.

**Por qué existe.** `core.media_assets` se replica desde la ADR-018 y `core.admin_users` no, pero
`fk_media_assets_created_by` las unía. Es el renglón prohibido de la tabla de
`docs/adr/ADR-018-medios-replicables.md`, y lo detectó M03
(`docs/modules/sales/ESCALADAS-M03.md` §i, en su rama). M03 necesita además que `ICurrentAdmin` dé
nombre y nodo para su atribución del personal (R-14).

---

## 2 · Barrido previo (sobre `4561d9b`)

| Qué | Dónde estaba |
|---|---|
| Escritores de `admin_users` | `Services/SetupService.cs:217`, `Services/AdminUserService.cs:67`. Ningún seed ni script SQL inserta cuentas |
| Implementaciones de `ICurrentAdmin` | `Authentication/CurrentAdmin.cs:8` y el doble `Sillar.Modules.Cms.Tests/ReactivacionRedSocialTests.cs:30`. El resto son consumidores que no cambian |
| Claims de la sesión | `AdminSessionAuthenticationHandler.cs:109-114`: ya llevaba `ClaimTypes.Name` = `full_name` |
| Escritura de `created_by` | `Services/MediaService.cs:64`, en un `UPDATE` posterior al `INSERT` de `MediaStorage` |
| Caminos que aplican migraciones de CORE | instalador (`CoreModule.cs:36-40` → `InstaladorDeModulos`), arranque en desarrollo (`ModuleBootstrapper.ApplyMigrationsIfAllowedAsync`) y `dotnet ef` (`CoreDbContextFactory`). Los dos últimos construían el contexto con `NodeIdentity.DefaultCode` escrito en el código |
| Fuera de `main` | M07 (`cd2bb40`) tiene un doble `Administrador : ICurrentAdmin` en `Sillar.Modules.B2B.Tests/CotizacionesTests.cs:307`; M03 (`273cc75`) declara en `SalesModule.cs:111` que espera estos datos. **No se tocan** |

---

## 3 · Cómo llega el nodo configurado a la migración

Una migración de EF no recibe servicios ni configuración, y el contrato compartido
`IModuleMigrations.ApplyMigrationsAsync(string connectionString, …)` solo pasa una cadena. **No
hizo falta cambiar ese contrato:** el nodo viaja en la conexión, como parámetro de sesión de
PostgreSQL.

- `Sillar.Core/Data/NodoParaMigrar.cs` añade `-c sillar.node_code=<nodo>` a `Options` de la cadena,
  escapando barra invertida y espacio, y conservando lo que ya hubiera.
- La migración lo lee con `current_setting('sillar.node_code', true)`.
- Lo ponen los tres caminos:
  - el instalador recibe `IConfiguration` (`InstaladorDeModulos`);
  - el arranque en desarrollo lo hace con `builder.Configuration`;
  - la fábrica de `dotnet ef` lo toma de la variable `Sillar__Node__Code` del entorno, que
    `.env.example:87` ya define.
- **Solo el valor configurado.** `NodoParaMigrar` no tiene valor por defecto. Sin
  `Sillar:Node:Code`, la conexión no lleva nodo y la migración **aborta** en cuanto haya una cuenta
  que rellenar. Con `admin_users` vacía —toda instalación limpia, y la base efímera de la puerta— no
  necesita nodo.

---

## 4 · La migración `20261003203701_CoreHomeNodeYAutorDeMedios`

Corre en una sola transacción, así que si aborta no queda nada a medias. Esto está probado: tras un
abortado, `home_node` no existe y el historial sigue en la migración anterior.

| Paso | Qué hace |
|---|---|
| 1a | `admin_users.home_node text NULL` |
| 1b | Relleno con el nodo de la conexión. Con cuentas y sin nodo: `22023 invalid_parameter_value`, con el número de cuentas y un HINT que dice qué configurar |
| 1c | Cuenta los NULL; si queda alguno: `23502`, con la consulta que los localiza |
| 1d | `NOT NULL` y `ck_admin_users_home_node_not_empty` |
| 2a | Corrupción: `created_by` que no existe en `admin_users` → `23503`, con el número de filas y la consulta que las localiza en el HINT |
| 2b | `DROP CONSTRAINT IF EXISTS fk_media_assets_created_by` y su índice |
| 2c | `created_by` → `created_by_admin_user_local_id`; añade `created_by_admin_user_name` y `created_by_admin_user_home_node` |
| 2d | Fotografía de las filas existentes con `JOIN` a `admin_users`: nombre = `full_name`, nodo = `home_node` |
| 2e | Índice `(home_node, local_id)`, cuatro `CHECK` (todo o nada, id > 0, textos no vacíos) y el trigger `trg_media_assets_autor_inmutable` |

`Down` deshace todo. La FK solo vuelve si todos los autores existen: restaurarla a ciegas sería
reintroducir la referencia prohibida.

---

## 5 · Decisiones tomadas hoy

| Decisión | Descartado | Por qué | Reversible |
|---|---|---|---|
| El nodo viaja como parámetro de sesión en la conexión | Cambiar `IModuleMigrations`, que es un contrato compartido por todos los módulos; o un interceptor de conexión en `CoreDbContext` | No cambia ninguna arquitectura y no añade un viaje de red a cada conexión en ejecución | Sí, hasta que la migración se aplique en una instalación real |
| La migración no tiene ningún valor por defecto para el nodo | Usar `NodeIdentity.DefaultCode` cuando falta configuración | Un relleno sobre datos existentes no adivina. Es literal y sería inventar el origen | Sí |
| Las cuentas **nuevas** toman `home_node` del `NodeIdentity` del contexto | Leerlo de la configuración en cada servicio | Es el nodo donde nace la cuenta, y la misma fuente que ya sella `origin_node` | Sí |
| `created_by` se **renombra** a `created_by_admin_user_local_id` | Conservar `created_by` y añadir dos columnas | Alinea con el trío de M03 (`..._admin_user_local_id`, `..._admin_user_home_node`), y el nombre dice que el id es local | **No, una vez aplicada en una instalación real**: el nombre de columna pasa a los datos |
| Columnas de nodo `text`, nombre `varchar(150)` | Longitudes arbitrarias | `text` como `origin_node` (`ReplicationMapping.cs:31-36`); 150 como `admin_users.full_name` | Igual que la anterior |
| El autor entra en el `INSERT`, por una sobrecarga **interna** de `MediaStorage` | Cambiar `IMediaStorage`; o mantener el `UPDATE` posterior | El contrato de los módulos no cambia, y el trigger puede prohibir cualquier cambio de la fotografía, incluido NULL → valor | Sí |
| Trigger de inmutabilidad solo sobre las tres columnas del autor | Bloquear toda la fila | `alt_text`, `is_active` e `is_orphan` siguen editándose | Sí |
| El relleno suspende el trigger de `updated_at` | Dejar que todas las filas cambien de `updated_at` | Añadir una columna no es modificar la cuenta ni el archivo | Sí |
| `ICurrentAdmin.DisplayName` lee `ClaimTypes.Name`, que ya existía; `HomeNode` lee un claim nuevo, `sillar:admin:home_node` | Consultar la base desde `CurrentAdmin` | El handler ya carga la cuenta en cada petición | Sí |
| CORE pasa a `1.1.0` | Mantener `1.0.0` | El contrato crece de forma aditiva; mismo criterio que M04 1.1.0/1.2.0 | Sí |

**Punto para JP, sin decidir:** las cuentas nuevas heredan la regla vigente de `NodeIdentity`
(`NodeIdentity.cs:53-60`), que usa `DefaultCode` cuando `Sillar:Node:Code` no está configurado. Esa
regla es anterior a esta entrega y sella también `origin_node`; esta entrega no la cambia. La
migración, en cambio, no la usa.

---

## 6 · Pruebas

`backend/Sillar.Core.Tests/NodoYAutoriaTests.cs`: 10 pruebas, cada una contra una base propia
`sillar_vacia_*` que se crea y se destruye.

| Prueba | Qué afirma |
|---|---|
| Instalación limpia | Migra sin nodo; `home_node` NOT NULL; ninguna FK en `media_assets`; trigger presente |
| Datos preexistentes | Con la migración anterior y SQL: `home_node` = nodo configurado; autor válido con sus tres datos y el nodo **de la cuenta**, distinto del `origin_node` del archivo; autor NULL con los tres nulos; `updated_at` intacto |
| Sin nodo y con cuentas | `22023`, «hay 1 cuenta(s)»; nada cambió |
| Autor corrupto | `23503`, «tiene 2 fila(s)»; la consulta del HINT las localiza (9001, 9002); `admin_users` tampoco cambió |
| Inmutabilidad | Cambiar el nombre fotografiado da `23514`; `alt_text` sí se edita |
| Media fotografía | Un `INSERT` con solo el nombre viola `ck_media_assets_autor_completo` |
| `ICurrentAdmin` | Handler real contra la base: id, correo, rol e `IsInRole` como siempre; `DisplayName` = `full_name`; `HomeNode` = nodo de la cuenta |
| Sin sesión | Todo nulo, sin lanzar |
| Subida | Instalación en `nodo-de-la-instalacion`, cuenta de `nodo-de-la-cuenta`: `origin_node` es el primero y la fotografía lleva el segundo. Por el contrato sin sesión, sin autor |
| Conexión | El nodo solo viaja si está configurado; escapado y conservando `Options` |

**Sabotajes**, cada uno restaurado y comprobado por SHA-256:

| Sabotaje | Pruebas en rojo |
|---|---|
| Fotografía desde `origin_node` | datos preexistentes |
| Sin abortar cuando falta el nodo | sin nodo y con cuentas |
| Sin comprobar corrupción | autor corrupto |
| Trigger que avisa en vez de impedir | inmutabilidad |
| `HomeNode` que lee otro claim | `ICurrentAdmin` y subida |
| Subida que relee el nodo de la instalación | subida |

**Diagnóstico no canónico** en la nube, con una base efímera propia, las migraciones de los cuatro
módulos por `dotnet ef` y `dotnet test backend/Sillar.sln`: **494/494, 0 fallidas, 0 omitidas, en
7 proyectos**. No es la puerta: la etapa 6 en la nube falla por la TLS del proxy, y la certificación
corresponde a la QA local.

---

## 7 · Lo que esto cambia fuera de CORE, sin tocarlo

- **M07** (`cd2bb40`): su doble `Administrador : ICurrentAdmin`
  (`Sillar.Modules.B2B.Tests/CotizacionesTests.cs:307`) **no compilará** al integrarse con esta
  versión. Necesita las dos propiedades nuevas. Su petición C13 (nombre de quien registra el pago)
  queda atendida por `DisplayName`.
- **M03** (`273cc75`): `ICurrentAdmin` ya da los tres datos de atribución que pedía
  (`SalesModule.cs:111`, `ESCALADAS-M03.md` §k-§l).
- **ADR-018**: con esta migración vuelve a ser cierta su frase de la decisión 4, «ninguna tabla
  replicada las referencia» (`ADR-018-medios-replicables.md:57`). La ADR no se edita.
- **`PENDIENTES.md` §29**, la comprobación automática de la ADR-018, vive en
  `integration/cola-costuras` y no en `main`. Esta entrega quita la única infracción conocida, pero
  no implementa esa comprobación.
