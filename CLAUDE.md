# Instrucciones del proyecto — SILLAR

**SILLAR** — Sistema Integrado y Licenciable de Logística, Administración y Retail.

Plataforma modular para negocios de retail y servicios. Cada sistema —catálogo, ventas, servicios, seguimiento, contenido, portal, mostrador— es un **módulo desmontable y licenciable por separado**.

**SILLAR es la plataforma; lo que se vende son dos productos construidos con ella** (ADR-015, enmendada por la ADR-017):

| | **SILLAR WEB** | **SILLAR ERP** |
|---|---|---|
| Dónde vive | Servidor en la nube, una instancia por cliente | Máquina del propio negocio |
| Para qué | Presencia, catálogo público, venta en línea | Mostrador, existencias, compras, comprobantes |
| Módulos | M01–M08, M10–M12 | CORE, M01, M04, M09, M13–M17 |

**Comparten el código, no la instalación.** Un módulo escrito una vez sirve a los dos: la diferencia entre WEB y ERP no se decide al compilar, se decide al instalar. En código no hay ninguna marca de producto — los espacios de nombres siguen siendo `Sillar.*`, que es la plataforma.

**SILLAR ERP está aparcado.** Todo el trabajo en curso es de SILLAR WEB. No adelantar módulos del ERP.

**Este repositorio contiene el producto, nunca a un cliente.** No escribas el nombre de ningún negocio real en proyectos, espacios de nombres, identificadores ni datos semilla. Cada instalación vive en su propio repositorio privado (ADR-008).

Antes de escribir código, lee:

- `docs/ARQUITECTURA_MODULAR.md` — módulos, dependencias, schemas, reglas
- `docs/ROADMAP_MODULAR.md` — orden de trabajo y ciclo de módulo
- `docs/adr/` — decisiones tomadas y sus razones. Una ADR marcada «sustituida» o «enmendada»
  se conserva sin editar: lo vigente está en la que la sustituye
- `docs/BITACORA.md` — con qué criterio se decide y qué toca ahora
- `docs/modules/<módulo>/SPEC.md` — la especificación del módulo en el que se está trabajando
- `docs/modules/<módulo>/ENTREGA-NN-*.md` — cuando un módulo es demasiado grande para un solo ciclo, se parte en entregas. Cada una refina el SPEC para su alcance y cierra con su propia verificación

El SPEC del módulo en curso es la fuente de verdad. Si algo del SPEC contradice a este archivo, gana el SPEC y hay que avisarlo. Un documento de entrega refina el SPEC dentro de su alcance: donde ambos hablen del mismo punto, manda el de la entrega.

---

## Stack

- **Base de datos:** PostgreSQL 16, en Docker
- **Backend:** ASP.NET Core Web API + Entity Framework Core (.NET 10 LTS)
- **Frontend:** React + TypeScript + Vite, gestor de paquetes **pnpm** (nunca npm)
- **Entorno:** Docker Compose

---

## Reglas de arquitectura — innegociables

### Módulos

1. Cada módulo tiene **su propio schema PostgreSQL** y **su propio `DbContext`** con `HasDefaultSchema("<código>")`.
2. Un módulo **solo escribe en su propio schema**.
3. Un módulo solo puede referenciar `Sillar.Shared`, `Sillar.Core.Contracts` y los `Contracts` de sus dependencias declaradas. **Nunca** el `Domain` ni el `Data` de otro módulo.
4. Cada módulo implementa `IModule` declarando código, versión, dependencias duras y dependencias blandas.
5. Las dependencias son dirigidas y **nunca circulares**.

### Esquema y migraciones

- **Las migraciones de EF Core son la fuente de verdad del esquema** (ADR-009). Cada módulo tiene sus migraciones y su historial `__migrations` dentro de su propio schema.
- Se escriben a mano, no se generan: los seeds (`database/modules/<código>/02_seed.sql`), los scripts de integración y la desinstalación (`99_drop.sql`).
- Extensiones de PostgreSQL e índices especializados van dentro de una migración con `MigrationBuilder.Sql(...)`.
- **En producción las migraciones nunca se aplican al arrancar.** Es un paso explícito del despliegue.

### Claves foráneas entre schemas

- **Dependencia dura:** se permite FK cruzada, declarada en la migración del módulo dependiente.
- **Dependencia blanda:** prohibida la FK, tanto en migraciones como en el esquema base. Columna nullable más datos snapshot. La FK va en `database/integrations/<a>_<b>.sql`, que solo se ejecuta si ambos módulos están instalados.

### Dependencias blandas en código

Se resuelven pidiendo el contrato al contenedor y comprobando si existe. Si no está, el módulo **degrada su comportamiento sin fallar**. Nunca lanzar excepción porque falte una dependencia blanda.

### Frontend

- Un módulo **nunca importa** de otro módulo. Lo compartido vive en `src/shared/`.
- Cada módulo exporta su `routes.ts`; la app monta solo las rutas de módulos activos.
- Menú, home y footer se construyen desde `GET /api/capabilities`. Nada escrito a mano.
- Nada de `fetch` suelto en componentes: cada módulo tiene su capa de servicios, y todo pasa por el cliente HTTP de `shared/http`.
- **Nunca `localStorage` ni `sessionStorage`** para la sesión ni el token CSRF: viven en memoria.
- Ningún componente lleva un color escrito. Solo variables CSS de `shared/styles/tokens.css`.
- Ningún «Ha ocurrido un error» y ningún botón «Aceptar». Un conflicto es una frase que dice qué lo impide y qué hacer; un botón nombra la acción que ejecuta.

---

## Nomenclatura

```
Base de datos → snake_case, inglés técnico, tablas en plural   → product_id
Backend C#    → PascalCase                                     → ProductId
Frontend TS   → camelCase en propiedades, PascalCase en tipos   → productId
Contenido visible al usuario → SIEMPRE en español
```

Nombres de productos en datos, según el diccionario:
`Producto + característica principal + marca/modelo + presentación`
Ejemplo correcto: `Cuaderno universitario cuadriculado Stanford A4 100 hojas`

---

## Convenciones de base de datos

- Claves primarias: **`uuid` v7 generado por la aplicación** en tablas que **se replican entre
  nodos**; `integer GENERATED ALWAYS AS IDENTITY` en las que no. Nunca `SERIAL` (ADR-016).
  Ante la duda: *¿puede esta fila nacer en un nodo y tener que existir en otro?*
  Se replican catálogo, clientes, existencias, ventas y comprobantes. No se replica nada de
  CORE: sesiones, auditoría, configuración y activación de módulos son del nodo.
  Las tablas replicadas llevan además `origin_node` y `row_version`.
- **Los identificadores nunca se muestran al usuario.** Los códigos visibles —`slug` para la
  web, código de negocio para la caja, número de venta o de comprobante— son campos aparte,
  legibles, y llevan la serie de su nodo delante
- `timestamptz` para fechas, nunca `timestamp`
- Eliminación **lógica** con `is_active`, nunca `DELETE` físico en tablas de negocio
- `CHECK` para reglas de valor: precios y cantidades no negativos, textos obligatorios no vacíos
- `created_at` con `DEFAULT now()`, `updated_at` mediante trigger `set_updated_at()`
- Datos snapshot en tablas transaccionales: el pedido conserva nombre y precio del momento
- **Todos los scripts deben ser idempotentes**
- El clúster usa colación **ICU `es-PE`** y búsqueda de texto en español
- Dos colaciones compartidas: **`core.es_ci`** (ignora mayúsculas, respeta tildes) para
  identidad y unicidad, y **`core.es_search`** (ignora mayúsculas y tildes) para los campos
  por los que el usuario busca. No confundirlas

Nombres de restricciones: `pk_`, `fk_`, `uq_`, `ck_`, `idx_` seguidos de tabla y campo.

---

## Seguridad — no negociable

- Contraseñas con **BCrypt**, factor de trabajo ≥ 12. Nunca en claro, nunca en logs, nunca en respuestas del API.
- Sesión administrativa por **cookie `httpOnly`, `Secure`, `SameSite=Strict`**, respaldada en `core.admin_sessions` (ADR-010). Se guarda el hash del token, jamás el token.
- **Protección CSRF obligatoria** en toda petición que modifique datos. El token es determinista, derivado de la sesión por HMAC, y `GET /api/admin/auth/csrf` es idempotente (ADR-012). No volver a introducir rotación.
- El mensaje de error de acceso es siempre el mismo, exista o no la cuenta.
- Archivos subidos: nombre **generado**, nunca el original. Se valida el tipo real del contenido, no la extensión (ADR-011).
- Ninguna respuesta del API expone `password_hash` ni datos de licencia.

## Reglas de trabajo

1. **Antes de modificar, explicar el plan.** Después de modificar, indicar qué archivos cambiaron y cómo probarlo.
2. **No instalar dependencias sin preguntar.**
3. Este repositorio contiene **solo el producto**. Nada específico de un cliente entra al código de un módulo.
4. Trabajar solo en el módulo en curso. No adelantar trabajo de otros módulos.
5. No crear abstracciones "por si acaso". Solo se generaliza cuando existe un segundo caso real.
6. Cada endpoint documentado con comentarios XML y visible en Swagger.

## Pruebas

- Un proyecto de pruebas por proyecto que las necesite, **junto a él** en `backend/`, con el
  nombre `Sillar.<Proyecto>.Tests`. No una carpeta `tests/` aparte.
- Los nombres de las pruebas van **en español**: la salida de `dotnet test` debe leerse como
  la lista de reglas que el sistema garantiza.
- Las pruebas de lógica no tocan la base de datos.

## Criterio de terminado

Un módulo está terminado cuando **se puede instalar y desinstalar sin romper nada del resto del sistema**. Si al desactivarlo aparece un enlace roto, una ruta muerta, un hueco visual o un fallo al arrancar, no está terminado.

---

## Entorno

```bash
docker compose up -d          # levanta PostgreSQL
docker compose logs -f db     # revisa el arranque
docker compose down           # detiene
docker compose down -v        # detiene y BORRA los datos
```

Cadena de conexión y credenciales en `.env`, nunca en el repositorio.

El desarrollo alterna entre **Windows** y **Arch Linux**. Escribir scripts y rutas de forma que funcionen en ambos; nada de rutas absolutas con letra de unidad, nada de comandos exclusivos de PowerShell en scripts compartidos.
