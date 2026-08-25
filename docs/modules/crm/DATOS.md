# DATOS — M04 Clientes

Diccionario y modelo ER **tal como quedaron construidos** (Paso 2 · DATOS del ciclo). El diseño de cada campo y las reglas de negocio están en el SPEC — este documento no los repite enteros, los verifica contra la migración y añade lo que solo se ve al implementar: nombres de restricciones tal como quedaron, el comportamiento de cada `ON DELETE`, los triggers y funciones PostgreSQL, y los tropiezos que el SPEC no podía anticipar.

**Migración fuente:** `Sillar.Modules.Crm/Migrations/20260824190200_CrmInitial.cs`.

---

## 1. Historial de esta ficha

**24 de agosto de 2026.** Construcción inicial del esquema `crm`. Seis tablas, una migración escrita a mano siguiendo el precedente de `CatalogInitial`: extensiones `pg_trgm` y `unaccent` al inicio, colaciones `core.es_ci` y `core.es_search` aplicadas con SQL explícito (`ALTER COLUMN ... TYPE`), índices de búsqueda (trigramas y texto completo) con SQL explícito, configuración de búsqueda textual propia `crm.spanish_unaccent`, triggers y funciones propias del schema. Sin datos semilla.

---

## 2. Replicación

Dos tablas se replican (ADR-016, regla 4); cuatro no:

| Tabla | Replica | PK | Tipo de PK |
|---|---|---|---|
| `crm.customers` | sí | `customer_id` | `uuid` v7, generado en aplicación |
| `crm.customer_addresses` | sí | `customer_address_id` | `uuid` v7, generado en aplicación |
| `crm.customer_accounts` | no | `customer_account_id` | `integer GENERATED ALWAYS AS IDENTITY` |
| `crm.customer_sessions` | no | `customer_session_id` | `integer GENERATED ALWAYS AS IDENTITY` |
| `crm.customer_tokens` | no | `customer_token_id` | `integer GENERATED ALWAYS AS IDENTITY` |
| `crm.contact_messages` | no | `contact_message_id` | `integer GENERATED ALWAYS AS IDENTITY` |

`crm.contact_messages` no replica (ADR-017): la ficha del cliente es un dato compartido WEB/ERP, pero la captación pertenece exclusivamente al lado WEB.

Las dos tablas replicadas llevan las cuatro columnas de replicación, que no se repiten en cada tabla:

| Campo | Tipo | Nulo | Default | Notas |
|---|---|---|---|---|
| `origin_node` | `text` | no | — | Nodo donde nació la fila. Lo fija `CrmDbContext.StampReplicationColumns`, nunca la base |
| `row_version` | `bigint` | no | `1` | Sube en cada `UPDATE`, la aplicación la incrementa |
| `created_at` | `timestamptz` | no | `now()` | |
| `updated_at` | `timestamptz` | no | `now()` | Trigger `crm.set_updated_at()` |

Las cuatro tablas no replicadas no llevan `origin_node` ni `row_version`. Verificado por prueba (Tests 18 y 26).

### DEUDA: StampReplicationColumns está duplicado por tercera vez

`CrmDbContext.StampReplicationColumns()` es la tercera copia temporal del mismo patrón que ya tienen `CoreDbContext` y `CatalogDbContext`. No se ha extraído a `Sillar.Shared` deliberadamente.

**DISPARADOR PARA GENERALIZAR:**
- aparece una cuarta copia; o
- dos implementaciones existentes empiezan a discrepar.

No modificar `Sillar.Shared`, `Sillar.Core` ni `Sillar.Modules.Catalog` para generalizarlo.

---

## 3. Diccionario, por tabla

### 3.1 `crm.customers`

| Campo | Tipo | Nulo | Clave / restricción |
|---|---|---|---|
| `customer_id` | `uuid` | no | `pk_customers` — v7, generado en `Customer.CustomerId` con `Guid.CreateVersion7()` |
| `full_name` | `text COLLATE core.es_search` | no | `ck_customers_full_name_no_vacio` (`btrim(full_name) <> ''`) |
| `email` | `text COLLATE core.es_ci` | no | `uq_customers_email`, `ck_customers_email_no_vacio`. Longitud máxima lógica: 150 |
| `phone` | `text` | sí | |
| `document_type` | `text` | sí | `ck_customers_document_type` (`IS NULL OR IN ('dni','ruc')`) |
| `document_number` | `text` | sí | `ck_customers_document_number_no_vacio` |
| `internal_notes` | `text` | sí | |
| `is_active` | `boolean` | no | default `true` |
| `deactivated_at` | `timestamptz` | sí | |
| `blocked_at` | `timestamptz` | sí | |
| `reactivation_requested_at` | `timestamptz` | sí | |
| `reactivation_resolved_at` | `timestamptz` | sí | |

**Constraints adicionales:**

- `ck_customers_document_pair`: `(document_type IS NULL AND document_number IS NULL) OR (document_type IS NOT NULL AND document_number IS NOT NULL)` — el documento va siempre en par, o no va.
- `ck_customers_lifecycle_state`: exactamente tres estados físicos permitidos (ACTIVA, DE BAJA, BLOQUEADA). No existe cuarto estado.
- `ck_customers_reactivation_timestamps`: `reactivation_resolved_at IS NULL OR (reactivation_requested_at IS NOT NULL AND reactivation_resolved_at >= reactivation_requested_at)`.

**Estado físico (ck_customers_lifecycle_state):**

| Estado | `is_active` | `deactivated_at` | `blocked_at` |
|---|---|---|---|
| ACTIVA | `true` | `NULL` | `NULL` |
| DE BAJA | `false` | no `NULL` | `NULL` |
| BLOQUEADA | `false` | `NULL` | no `NULL` |

**Estado derivado de reactivación (ck_customers_reactivation_timestamps):**

| Estado | `reactivation_requested_at` | `reactivation_resolved_at` |
|---|---|---|
| nunca solicitó | `NULL` | `NULL` |
| pendiente | no `NULL` | `NULL` |
| resuelta | no `NULL` | no `NULL` (≥ `requested_at`) |

Las fechas de solicitud **no** se relacionan con `blocked_at`. Sobreviven después de que administración desbloquee la ficha.

**Índices:**

| Nombre | Columna(s) | Tipo | Filtro |
|---|---|---|---|
| `uq_customers_email` | `email` | UNIQUE | sobre TODAS las fichas |
| `uq_customers_document` | `document_type, document_number` | UNIQUE | `WHERE document_number IS NOT NULL` |
| `idx_customers_email_trgm` | `(email COLLATE "C")` | GIN gin_trgm_ops | Búsqueda parcial de email |
| `idx_customers_full_name_search` | `to_tsvector('crm.spanish_unaccent', full_name)` | GIN | Búsqueda textual de nombres |

#### Colaciones de `crm.customers`

| Columna | Colación | Propósito | Comportamiento |
|---|---|---|---|
| `email` | `core.es_ci` | identidad / unicidad | ignora mayúsculas; respeta tildes distintas; colapsa NFC/NFD equivalentes por ICU |
| `full_name` | `core.es_search` | búsqueda | ignora mayúsculas y tildes |

`core.es_ci` y `core.es_search` ya existen y pertenecen a CORE. CRM las consume sin definirlas. La distinción sigue siendo normativa: `email` → `core.es_ci` (identidad), `full_name` → `core.es_search` (búsqueda). Catalog ya documenta la misma separación.

### 3.2 `crm.customer_addresses`

| Campo | Tipo | Nulo | Clave / restricción |
|---|---|---|---|
| `customer_address_id` | `uuid` | no | `pk_customer_addresses` — v7, generado en aplicación |
| `customer_id` | `uuid` | no | `fk_customer_addresses_customer_id` → `crm.customers.customer_id`, `ON DELETE RESTRICT` |
| `label` | `text` | sí | |
| `address_line` | `text` | no | `ck_customer_addresses_address_line_no_vacio` |
| `district` | `text` | sí | |
| `province` | `text` | sí | |
| `department` | `text` | sí | |
| `reference` | `text` | sí | |
| `is_preferred` | `boolean` | no | default `false` |
| `is_active` | `boolean` | no | default `true` |

**Constraints adicionales:**

- `ck_customer_addresses_preferred_active`: `NOT is_preferred OR is_active` — una dirección inactiva no puede ser preferida.

**Índices:**

| Nombre | Columna | Tipo | Filtro |
|---|---|---|---|
| `uq_customer_addresses_preferred` | `customer_id` | UNIQUE | `WHERE is_preferred AND is_active` |

Solo puede haber una dirección preferida y activa por cliente.

### 3.3 `crm.customer_accounts`

| Campo | Tipo | Nulo | Clave / restricción |
|---|---|---|---|
| `customer_account_id` | `integer` | no | `pk_customer_accounts` — `GENERATED ALWAYS AS IDENTITY` |
| `customer_id` | `uuid` | no | `fk_customer_accounts_customer_id` → `crm.customers.customer_id`, `ON DELETE RESTRICT`. `uq_customer_accounts_customer_id` (UNIQUE) |
| `password_hash` | `text` | no | `ck_customer_accounts_password_hash_no_vacio` |
| `email_verified_at` | `timestamptz` | sí | Se anula mediante trigger si cambia `customers.email` |
| `created_at` | `timestamptz` | no | default `now()` |
| `updated_at` | `timestamptz` | no | default `now()`, trigger `crm.set_updated_at()` |

**FK es tabla local → tabla replicada**: la dirección permitida. Garantiza físicamente que una ficha tiene 0..1 cuenta y una cuenta siempre tiene ficha.

No implementa BCrypt todavía. Solo persistencia.

### 3.4 `crm.customer_sessions`

| Campo | Tipo | Nulo | Clave / restricción |
|---|---|---|---|
| `customer_session_id` | `integer` | no | `pk_customer_sessions` — `GENERATED ALWAYS AS IDENTITY` |
| `customer_account_id` | `integer` | no | `fk_customer_sessions_customer_account_id` → `crm.customer_accounts.customer_account_id`, `ON DELETE CASCADE` |
| `token_hash` | `text` | no | `uq_customer_sessions_token_hash` (UNIQUE), `ck_customer_sessions_token_hash_no_vacio` |
| `csrf_token_hash` | `text` | no | `ck_customer_sessions_csrf_token_hash_no_vacio` |
| `issued_at` | `timestamptz` | no | |
| `last_seen_at` | `timestamptz` | no | `ck_customer_sessions_last_seen_after_issued` (`>= issued_at`) |
| `expires_at` | `timestamptz` | no | `ck_customer_sessions_expires_after_issued` (`> issued_at`) |
| `revoked_at` | `timestamptz` | sí | `ck_customer_sessions_revoked_after_issued` (`IS NULL OR >= issued_at`) |
| `ip_address` | `text` | sí | |
| `user_agent` | `text` | sí | |

No lleva `origin_node` ni `row_version`. No lleva `updated_at`: sus timestamps son funcionales. `last_seen_at` sostiene la renovación deslizante.

No copia la PK Guid histórica de `AdminSession`: tabla no replicada → `integer GENERATED ALWAYS AS IDENTITY`.

### 3.5 `crm.customer_tokens`

| Campo | Tipo | Nulo | Clave / restricción |
|---|---|---|---|
| `customer_token_id` | `integer` | no | `pk_customer_tokens` — `GENERATED ALWAYS AS IDENTITY` |
| `customer_id` | `uuid` | no | `fk_customer_tokens_customer_id` → `crm.customers.customer_id`, `ON DELETE RESTRICT` |
| `purpose` | `text` | no | `ck_customer_tokens_purpose` (`IN ('invitation','email_verification','password_reset')`) |
| `token_hash` | `text` | no | `uq_customer_tokens_token_hash` (UNIQUE), `ck_customer_tokens_token_hash_no_vacio` |
| `created_at` | `timestamptz` | no | default `now()` |
| `expires_at` | `timestamptz` | no | `ck_customer_tokens_expires_after_created` (`> created_at`) |
| `used_at` | `timestamptz` | sí | `ck_customer_tokens_used_after_created` (`IS NULL OR >= created_at`) |

No lleva `origin_node` ni `row_version`. No lleva `updated_at`: sus timestamps son funcionales.

**Consumo atómico:**

```sql
UPDATE crm.customer_tokens
   SET used_at = now()
 WHERE customer_token_id = @id
   AND used_at IS NULL
   AND expires_at > now();
```

1 fila afectada = éxito. 0 filas = usado / inválido / caducado. Dos consumos concurrentes producen exactamente un ganador (verificado por prueba, Test 11).

### 3.6 `crm.contact_messages`

| Campo | Tipo | Nulo | Clave / restricción |
|---|---|---|---|
| `contact_message_id` | `integer` | no | `pk_contact_messages` — `GENERATED ALWAYS AS IDENTITY` |
| `customer_id` | `uuid` | sí | `fk_contact_messages_customer_id` → `crm.customers.customer_id`, `ON DELETE SET NULL` |
| `full_name` | `text COLLATE core.es_search` | no | `ck_contact_messages_full_name_no_vacio` |
| `email` | `text COLLATE core.es_ci` | sí | `ck_contact_messages_email_no_vacio`. Longitud máxima lógica: 150 |
| `phone` | `text` | sí | `ck_contact_messages_phone_no_vacio` |
| `subject` | `text` | sí | `ck_contact_messages_subject_no_vacio` |
| `message` | `text` | no | `ck_contact_messages_message_no_vacio` |
| `is_active` | `boolean` | no | default `true` |
| `created_at` | `timestamptz` | no | default `now()` |
| `updated_at` | `timestamptz` | no | default `now()`, trigger `crm.set_updated_at()` |

**No replica (ADR-017):** la ficha del cliente es un dato compartido WEB/ERP, pero la captación pertenece exclusivamente al lado WEB. No lleva `origin_node` ni `row_version`.

**`customer_id` es nullable** porque un visitante puede escribir sin tener ficha. Si se conoce o se vincula después, la FK interna apunta a `crm.customers`. `ON DELETE SET NULL` porque el mensaje es un registro independiente de captación: perder la asociación no debe borrar el mensaje.

**Constraints adicionales:**

- `ck_contact_messages_contact_channel`: `(email IS NOT NULL OR phone IS NOT NULL)` — debe existir al menos un medio de contacto, email o teléfono. No se obliga a que sea correo.

**Colaciones:**

| Columna | Colación | Propósito |
|---|---|---|
| `full_name` | `core.es_search` | búsqueda — ignora mayúsculas y tildes |
| `email` | `core.es_ci` | identidad/contacto — ignora mayúsculas, respeta tildes distintas |

**Índices:**

| Nombre | Columna(s) | Tipo | Filtro |
|---|---|---|---|
| `idx_contact_messages_customer` | `customer_id` | btree | sobre TODAS las filas |
| `idx_contact_messages_created_at` | `created_at` | btree | sobre TODAS las filas |
| `idx_contact_messages_active` | `is_active` | btree | `WHERE is_active` |

No hay índices trigram/full-text para mensajes de contacto: no existe todavía una consulta de búsqueda especificada para esta bandeja.

**Normalización del email:** si `contact_messages.email` no es null, `CrmDbContext` aplica el mismo almacenamiento canónico que a `customers.email`: `Trim()` + `Normalize(NormalizationForm.FormC)`. No lowercase.

---

## 4. Modelo ER

```mermaid
erDiagram
    customers ||--o| customer_accounts : "customer_id (RESTRICT)"
    customers ||--o{ customer_addresses : "customer_id (RESTRICT)"
    customers ||--o{ customer_tokens : "customer_id (RESTRICT)"
    customers ||--o{ contact_messages : "customer_id (SET NULL)"
    customer_accounts ||--o{ customer_sessions : "customer_account_id (CASCADE)"

    customers {
        uuid customer_id PK
        text full_name
        text email UK
        text phone
        text document_type
        text document_number
        boolean is_active
        timestamptz deactivated_at
        timestamptz blocked_at
        timestamptz reactivation_requested_at
        timestamptz reactivation_resolved_at
        text origin_node
        bigint row_version
        timestamptz created_at
        timestamptz updated_at
    }
    customer_addresses {
        uuid customer_address_id PK
        uuid customer_id FK
        text address_line
        boolean is_preferred
        boolean is_active
        text origin_node
        bigint row_version
    }
    customer_accounts {
        integer customer_account_id PK
        uuid customer_id FK_UK
        text password_hash
        timestamptz email_verified_at
        timestamptz created_at
        timestamptz updated_at
    }
    customer_sessions {
        integer customer_session_id PK
        integer customer_account_id FK
        text token_hash UK
        text csrf_token_hash
        timestamptz issued_at
        timestamptz last_seen_at
        timestamptz expires_at
        timestamptz revoked_at
    }
    customer_tokens {
        integer customer_token_id PK
        uuid customer_id FK
        text purpose
        text token_hash UK
        timestamptz created_at
        timestamptz expires_at
        timestamptz used_at
    }
    contact_messages {
        integer contact_message_id PK
        uuid customer_id FK
        text full_name
        text email
        text phone
        text subject
        text message
        boolean is_active
        timestamptz created_at
        timestamptz updated_at
    }
```

Todas las FK son internas del schema `crm`. No hay salidas hacia otros schemas: CRM no depende de `core.media_assets` ni de ningún otro módulo.

---

## 5. Triggers y funciones PostgreSQL

### 5.1 `crm.set_updated_at()`

```sql
CREATE OR REPLACE FUNCTION crm.set_updated_at()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END;
$$;
```

Triggers `BEFORE UPDATE` en:

| Trigger | Tabla |
|---|---|
| `trg_customers_set_updated_at` | `crm.customers` |
| `trg_customer_addresses_set_updated_at` | `crm.customer_addresses` |
| `trg_customer_accounts_set_updated_at` | `crm.customer_accounts` |
| `trg_contact_messages_set_updated_at` | `crm.contact_messages` |

No se aplica a `customer_sessions` ni `customer_tokens`: sus timestamps son funcionales.

Verificado por prueba (Test 20 y Test 27): un `UPDATE` SQL directo que no toca `updated_at` provoca que el trigger lo actualice.

### 5.2 `crm.invalidate_customer_email_verification()`

```sql
CREATE OR REPLACE FUNCTION crm.invalidate_customer_email_verification()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE crm.customer_accounts
       SET email_verified_at = NULL
     WHERE customer_id = NEW.customer_id
       AND email_verified_at IS NOT NULL;

    RETURN NEW;
END;
$$;
```

```sql
CREATE TRIGGER trg_customers_invalidate_email_verification
    AFTER UPDATE OF email ON crm.customers
    FOR EACH ROW
    WHEN (OLD.email IS DISTINCT FROM NEW.email)
    EXECUTE FUNCTION crm.invalidate_customer_email_verification();
```

**Razón:** si cambia la dirección realmente almacenada, deja de ser válido afirmar que esa dirección fue verificada.

**Funciona incluso mediante SQL directo**, no solo mediante `CrmDbContext`.

**Comportamiento observado bajo `core.es_ci`:**

- Cambio solo de mayúsculas (ej. `verify@ejemplo.pe` → `VERIFY@EJEMPLO.PE`): `IS DISTINCT FROM` devuelve `false` bajo `es_ci` → el trigger **no** dispara → `email_verified_at` queda intacto. Verificado por prueba (Test 16).

- Cambio real de correo (ej. `verify@ejemplo.pe` → `nuevo@ejemplo.pe`): `IS DISTINCT FROM` devuelve `true` → el trigger dispara → `email_verified_at` queda en `NULL`. Verificado por prueba (Test 15).

---

## 6. Normalización del correo

En `CrmDbContext`, antes de persistir `Customer` (Added o Modified):

```csharp
email = email.Trim().Normalize(NormalizationForm.FormC);
```

Se ejecuta tanto en `SaveChanges` como en `SaveChangesAsync`.

No se convierte el correo a minúsculas: la equivalencia de mayúsculas la resuelve `core.es_ci`.

**Trim sigue siendo necesario:** el espacio es lo que `es_ci` sí distingue. `a@x.com` y `a@x.com ` son dos correos para el índice.

**Normalize(FormC) se conserva para almacenamiento canónico, no para garantizar unicidad.** La garantía de que NFC y NFD no crean dos fichas viene de `uq_customers_email` bajo `core.es_ci`: ICU normaliza al construir la clave de colación, así que las dos formas chocan en el índice único (11 bytes contra 12, y el índice rechaza la segunda). La normalización a NFC ya no es lo que impide el duplicado — es `es_ci` — pero se mantiene porque la base almacena la última forma escrita, y sin ella el valor guardado depende de qué teclado lo escribió.

Verificado por prueba (Test 14): insertar directamente en PostgreSQL un email NFC e intentar insertar el mismo correo en NFD produce `unique_violation` (SQLSTATE 23505) desde `uq_customers_email`.

---

## 7. Notas de implementación que el SPEC no podía anticipar

**`pg_trgm` es dependencia de infraestructura compartida.** `CrmInitial` ejecuta `CREATE EXTENSION IF NOT EXISTS pg_trgm` al inicio, mismo precedente que `CatalogInitial`. `99_drop.sql` **no** elimina `pg_trgm`: es una extensión compartida y otro módulo puede usarla.

**`COLLATE` aplicado con `ALTER COLUMN ... TYPE` después de crear la tabla.** El proveedor Npgsql, si se le pide la colación en la definición de columna, genera `COLLATE "core.es_ci"` — comillas alrededor del nombre calificado completo, como si fuera un único identificador — y PostgreSQL busca (y no encuentra) una colación llamada literalmente `core.es_ci`. El `ALTER COLUMN` con SQL explícito, ejecutado después de crear los índices únicos, reconstruye esos índices con la colación ya aplicada. Es el mismo tropiezo documentado en `CoreInitial` y `CatalogInitial`.

**`PROHIBIDO` crear `core.es_ci` o `core.es_search` u otra colación equivalente.** Ambas ya existen y pertenecen a CORE. CRM las consume sin definirlas.

**NFC y NFD chocan realmente en `uq_customers_email`.** Comprobado por efecto contra la colación real del proyecto: 11 bytes (NFC) contra 12 (NFD), y el índice único rechaza la segunda inserción con `unique_violation` (SQLSTATE 23505). ICU normaliza al construir la clave de colación, así que las dos formas son el mismo correo para `es_ci`.

**`core.es_ci` colapsa NFC/NFD en `IS DISTINCT FROM`.** Comportamiento observado (Test 17): sin colación, NFC y NFD son bytes distintos, y `IS DISTINCT FROM` devuelve `true`. Con `core.es_ci`, ICU normaliza al comparar, `IS DISTINCT FROM` devuelve `false`, y el trigger `trg_customers_invalidate_email_verification` **no** dispara. El correo NFD no invalida `email_verified_at` porque es el mismo correo bajo `es_ci`.

**No afirmar que `core.es_ci` distingue NFC/NFD.** El SPEC corregido (§5) lo deja claro: ICU normaliza al construir la clave de colación, así que las dos formas chocan en el índice único. La afirmación anterior del SPEC de que `es_ci` todavía distinguía esas dos representaciones era incorrecta; la base real demostró que no.

**LIKE/ILIKE directo sobre colaciones no deterministas produce 0A000.** PostgreSQL no admite `LIKE` ni `ILIKE` sobre una colación no determinista, y `email` es `es_ci`:

```sql
SELECT ... WHERE email ILIKE '%texto%'
ERROR 0A000: nondeterministic collations are not supported for LIKE
```

Por eso el índice trigram va sobre `(email COLLATE "C")`, no sobre `email`, y la consulta debe repetir exactamente esa expresión:

```sql
WHERE email COLLATE "C" ILIKE '%texto%'
```

Mismo precedente que `catalog.product_items.code`. La trampa no salta con la tabla vacía: el error aparece al ejecutar, no al planificar.

**`full_name` con `core.es_search` tampoco admite `LIKE`/`ILIKE` directo** — también es una colación ICU no determinista. Para nombres se usa búsqueda textual con la configuración propia `crm.spanish_unaccent`:

```sql
WHERE to_tsvector('crm.spanish_unaccent', full_name) @@ plainto_tsquery('crm.spanish_unaccent', @texto)
```

El índice `idx_customers_full_name_search` replica esa expresión exacta.

**Hallazgo que motivó `crm.spanish_unaccent`:** la configuración `pg_catalog.spanish` por sí sola **no quita tildes**. El lexema de `Peña` es `'peñ'` y el de `pena` es `'pen'` — son lexemas distintos, y `@@` devuelve `false`. `core.es_search` expresa correctamente que un nombre se busca sin exigir mayúsculas ni tildes, pero PostgreSQL no permite `LIKE`/`ILIKE` sobre esa colación no determinista, y la configuración `spanish` de texto completo no elimina diacríticos.

**Mecanismo:** CRM crea `crm.spanish_unaccent`, copia de `pg_catalog.spanish` con el diccionario `unaccent` delante de `spanish_stem`. Así la búsqueda conserva stemming español y además ignora diacríticos: «Peña» produce «pen» igual que «pena». La extensión `unaccent` es compartida (no se elimina al desinstalar CRM); la configuración `crm.spanish_unaccent` sí pertenece al módulo y desaparece con su schema.

**`unaccent`:**

- extensión compartida
- `CrmInitial` ejecuta `CREATE EXTENSION IF NOT EXISTS unaccent`
- `99_drop` NO la elimina
- otro módulo futuro puede usarla

**`crm.spanish_unaccent`:**

- objeto propio del schema CRM
- desaparece con `DROP SCHEMA crm CASCADE`
- se recrea al reinstalar `CrmInitial`

**`full_name` (resumen):**

| Aspecto | Valor |
|---|---|
| Columna | `core.es_search` |
| Búsqueda textual | `crm.spanish_unaccent` |
| Índice | `idx_customers_full_name_search` |
| Mecanismo | `unaccent` → `spanish_stem` |

---

## 8. Verificación aplicada (Paso 2 · DATOS)

Confirmado el 24 de agosto de 2026 sobre PostgreSQL 16 con los schemas `core` y `catalog` ya presentes. Todas las pruebas cruzan la frontera con PostgreSQL real — no EF InMemory, no metadata de EF, no inspección de código.

| # | Verificación | Resultado | Evidencia |
|---|---|---|---|
| 1 | CrmInitial aplica desde cero | PASS | `crm.__migrations` contiene `20260824190200_CrmInitial` |
| 2 | customer y customer_address generan UUID v7 | PASS | `Guid.Version == 7` en aplicación y en base |
| 3 | Segunda account para mismo customer_id falla | PASS | `DbUpdateException` por `uq_customer_accounts_customer_id` |
| 4 | Email duplicado falla con ficha de baja/bloqueada | PASS | `DbUpdateException` por `uq_customers_email` |
| 5 | Documento duplicado falla | PASS | `DbUpdateException` por `uq_customers_document` |
| 6 | Combinaciones inválidas lifecycle fallan | PASS | 4 variantes Theory, todas `DbUpdateException` por `ck_customers_lifecycle_state` |
| 7 | reactivation_resolved sin requested falla | PASS | `DbUpdateException` por `ck_customers_reactivation_timestamps` |
| 8 | reactivation_resolved anterior a requested falla | PASS | `DbUpdateException` por `ck_customers_reactivation_timestamps` |
| 9 | Dos direcciones preferidas activas fallan | PASS | `DbUpdateException` por `uq_customer_addresses_preferred` |
| 10 | purpose inválido falla | PASS | 3 variantes Theory, todas `DbUpdateException` por `ck_customer_tokens_purpose` |
| 11 | Consumo concurrente produce un ganador | PASS | 2 `UPDATE` en paralelo, `rows affected` suma = 1 |
| 12 | Eliminar schema CRM no toca CORE ni Catalog; extensiones permanecen | PASS | `core` y `catalog` mantienen sus tablas; `core.es_ci` sigue existiendo; `crm.spanish_unaccent` desaparece; `unaccent` y `pg_trgm` siguen existiendo |
| 13 | Reinstalar CrmInitial sobre schema limpio: tablas y spanish_unaccent | PASS | `MigrateAsync()` recrea ≥6 tablas; `crm.spanish_unaccent` vuelve a existir; `unaccent` sigue existiendo |
| 14 | NFC y NFD chocan en uq_customers_email | PASS | SQL directo: NFC insertado, NFD rechazado con SQLSTATE 23505 (`unique_violation`); 1 fila |
| 15 | Cambiar email limpia email_verified_at | PASS | `email_verified_at` queda `NULL` tras cambiar correo |
| 16 | Cambiar solo mayúsculas NO invalida | PASS | `email_verified_at` queda intacto bajo `es_ci` |
| 17 | SQL directo NFC↔NFD IS DISTINCT FROM | PASS | `raw_distinct = true`, `ci_distinct = false`; trigger no dispara; `email_verified_at` intacto |
| 18 | Tablas no replicadas sin origin_node/row_version | PASS | `count(*) = 0` para las tres tablas |
| 19 | Tablas replicadas con origin_node/row_version, se incrementa | PASS | `row_version` sube de 1 a 2 tras `UPDATE` |
| 20 | set_updated_at modifica updated_at | PASS | `after > before` tras `UPDATE` SQL directo |
| 21 | Búsqueda parcial de email con trigramas | PASS | `email COLLATE "C" ILIKE '%prueba@ejemplo%'` devuelve 1; índice y `pg_trgm` existen |
| 22 | Búsqueda de full_name: Peña → pena, Álvarez → alvarez, José → jose | PASS | `to_tsvector('crm.spanish_unaccent', ...)` `@@` `plainto_tsquery('crm.spanish_unaccent', ...)` = `true` para los tres casos; extensión `unaccent` y configuración `crm.spanish_unaccent` existen |
| 23 | contact_message sin customer_id | PASS | Insert con `customer_id = NULL` aceptado; 1 fila con `customer_id IS NULL` |
| 24 | contact_message vinculado y FK inexistente | PASS | Vinculado a customer existente: 1 fila; `customer_id` inexistente: SQLSTATE 23503 (`foreign_key_violation`) |
| 25 | Sin email y sin phone falla | PASS | SQLSTATE 23514 (`check_violation`) por `ck_contact_messages_contact_channel` |
| 26 | contact_messages sin origin_node/row_version | PASS | `count(*) = 0` para `contact_messages` |
| 27 | contact_messages set_updated_at modifica updated_at | PASS | `after > before` tras `UPDATE` SQL directo |
