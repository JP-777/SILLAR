# Levantar SILLAR para una demostración

Esta guía corresponde al producto desplegado actual: **CORE + M01 Catálogo +
M02 Contenido Web + M04 Clientes y Contacto**.

CORE queda siempre activo. M01, M02 y M04 se activan desde **Módulos**.
Las migraciones de los cuatro módulos forman parte de la instalación, pero
los datos automáticos de demostración siguen siendo solo de **Catálogo**:
CMS y CRM se muestran inicialmente vacíos, sin inventar contenido ni clientes.

---

## 0 · Lo que hace falta tener

- Docker Desktop con WSL2 en Windows o Docker en Linux, arrancado.
- SDK de .NET 10.
- Node.js/corepack y `pnpm` si se enseñará el frontend.
- Un `.env` propio de este árbol, generado así:

```bash
node scripts/estrenar.mjs
```

No copies `.env.example` ni el `.env` de otra worktree.

Después cambia los secretos. Mientras la conexión conserve `Password=...`,
`POSTGRES_PASSWORD` y el password de `ConnectionStrings__Default` deben
coincidir.

En una máquina nueva restaura primero las herramientas y dependencias:

```bash
cd backend
dotnet tool restore
dotnet restore Sillar.sln
cd ..
```

`dotnet-ef` está fijado por `backend/.config/dotnet-tools.json`.

Para ver la identidad y los puertos de este árbol:

```bash
node scripts/identidad.mjs
```

---

## 1 · La base de datos, desde cero

**Este paso borra únicamente la base desechable de esta demostración.**

```bash
docker compose down -v
docker compose up -d db
docker compose ps
```

`POSTGRES_PASSWORD` se usa cuando PostgreSQL crea `db_data`. Cambiarla luego
en `.env` no modifica la contraseña almacenada dentro de un volumen existente.

`docker compose down -v` elimina `db_data` y todos sus datos: no es un método
para rotar credenciales de una base que se quiera conservar.

---

## 2 · Las migraciones de los cuatro módulos reales

Las tablas las crean las migraciones de EF Core:

```bash
cd backend
dotnet ef database update --project Sillar.Core --startup-project Sillar.Api
dotnet ef database update --project Sillar.Modules.Catalog --startup-project Sillar.Api
dotnet ef database update --project Sillar.Modules.Cms --startup-project Sillar.Api
dotnet ef database update --project Sillar.Modules.Crm --startup-project Sillar.Api
cd ..
```

El instalador también sabe aplicar migraciones pendientes antes de completar
una instalación y se niega a modificar un destino que contenga objetos ajenos
a SILLAR.

En esta demostración se aplican explícitamente antes del seed mínimo de CORE
para que ese seed exista cuando el setup escriba el nombre público del negocio.

---

## 3 · El seed mínimo del producto

CORE sí contiene configuración mínima. Los seeds de M01, M02 y M04 están
vacíos por diseño: no crean productos, banners, contenido ni clientes ficticios.

```bash
docker compose exec -T db sh -lc   'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /scripts/modules/core/02_seed.sql'
```

En Git Bash, si MSYS intenta reescribir `/scripts/...`, antepón:

```bash
MSYS_NO_PATHCONV=1
```

En PowerShell y Linux no hace falta.

---

## 4 · La API

Levanta el API:

```bash
docker compose --profile full up -d --build api
```

Consulta el puerto propio de esta worktree:

```bash
node scripts/identidad.mjs
```

En Bash puedes obtener la URL sin escribir el puerto a mano:

```bash
API_PORT="$(node --input-type=module -e "import { identidadDeLaWorktree } from './scripts/identidad.mjs'; console.log(identidadDeLaWorktree(process.cwd()).dev.puertoApi)")"
API="http://localhost:${API_PORT}"
echo "$API"
```

Antes de instalar:

```bash
curl "$API/api/setup/status"
```

Debe indicar:

```text
"setupRequired": true
```

---

## 5 · La instalación

Crea el negocio y su primer administrador.

La contraseña debe cumplir la política del instalador y **no puede contener
el nombre ni el correo del administrador**. Si no la acepta, el setup
responde 400 sin completar ni dejar una instalación a medias.

```bash
curl -i -X POST "$API/api/setup"   -H "Content-Type: application/json"   -d '{"businessName":"Demostracion SILLAR","licenseType":"trial",
       "admin":{"fullName":"Persona Administradora",
                "email":"demo@sillar.local",
                "password":"LA-QUE-ELIJAS-AQUI"}}'
```

La respuesta correcta es **201**. Después el host se detiene y Docker vuelve
a levantarlo en modo normal.

La señal positiva de que terminó la instalación ya no consiste en provocar
un login inválido. Espera al reinicio y consulta:

```bash
curl "$API/api/setup/status"
```

Debe contener:

```text
"setupRequired": false
```

En modo normal `POST /api/setup` deja de estar disponible.

---

## 6 · Activar los módulos reales y sembrar Catálogo

Entra con `demo@sillar.local` y la contraseña elegida en el paso 5.

Desde **Módulos**, activa en este orden:

1. **M01 · Catálogo de Productos**
2. **M02 · Contenido Web**
3. **M04 · Clientes y Contacto**

Cada cambio de activación reinicia el API unos segundos.

La activación **no aplica migraciones**: comprueba el schema preparado por el
instalador antes de cambiar el estado.

Con M01 activo:

```bash
SILLAR_EMAIL=demo@sillar.local SILLAR_PASSWORD='LA-QUE-ELIJAS-AQUI' node scripts/demo/seed-demo.mjs
```

`seed-demo.mjs` deriva automáticamente el puerto de esta worktree. Solo define
`SILLAR_API` si deliberadamente quieres apuntar a otro host.

El script crea datos de demostración de Catálogo. No crea contenido CMS ni
clientes CRM ficticios.

---

## 7 · El frontend

Para enseñar en desarrollo:

```bash
cd frontend
pnpm install     # solo la primera vez
pnpm dev
```

- Tienda pública: **http://localhost:5173/catalogo**
- Panel: **http://localhost:5173/admin**

---

## Los datos de acceso

| | |
|---|---|
| Correo | `demo@sillar.local` |
| Contraseña | **la que elegiste en el paso 5** |

**La contraseña no está escrita en el repositorio, y no debe estarlo.** La regla del proyecto no
distingue entre credenciales importantes y poco importantes: una contraseña en el historial de
git no se borra, y la que se teclea en el paso 5 es la de una instalación que existe de verdad
en la máquina de quien la teclea.

Elígela al instalar y guárdala donde guardas las demás. Si se pierde, se vuelve desde el paso 1:
la base de demostración se borra y se rehace en unos minutos, que es justo lo que la hace
desechable.

El correo sí está aquí porque **no es una credencial**: es el nombre de la cuenta, y hace falta
para que los comandos del paso 6 se puedan copiar.

---

## El recorrido que se enseña

Está probado entero y seguido en `e2e/tests/recorrido.spec.ts`, así que si algo de esto se rompe
se sabrá antes de la demostración y no durante:

1. Entrar al panel.
2. Crear una marca con su imagen — se sube en **Archivos** y se elige en la ficha.
3. Crear dos categorías, una dentro de otra.
4. Crear un producto con nombre, descripción, precio, imagen y categorías.
5. Crear otro con dos presentaciones de precio distinto.
6. Ir a la tienda: verlos en el catálogo, entrar en una categoría, abrir la ficha, **cambiar de
   presentación** y buscar uno por su nombre.
7. Desactivar M01 y ver que el panel sigue en pie, sin entrada de menú ni ruta muerta —
   `e2e/tests/catalogo.spec.ts:232`.
8. Volver a activarlo.

**Los pasos 7 y 8 reinician el proceso** y tardan entre diez y noventa segundos cada uno. La
pantalla lo dice mientras pasa; conviene saberlo antes de enseñarlo.

## Qué enseñar de los datos, y por qué está puesto

- **«Desde S/ 4.50»** en el plumón y en el archivador: tienen presentaciones que cuestan
  distinto, y la tarjeta no tiene selector, así que el precio es una cota y se dice.
- **«A consultar»** en el anillado y la impresión: nulo no es gratis, y la tarjeta lo aclara.
- **Tres productos sin foto**: el cuadrado lo ocupa el nombre. Es una decisión, no un hueco.
- **El árbol de categorías**: Papelería → Cuadernos, Escritura → Lápices y colores.
- **La ficha del plumón**: el bloque se titula «Color» y en ninguna pantalla aparece la palabra
  «variante».

---

## Si algo va mal

| Síntoma | Qué pasa |
|---|---|
| `/api/setup/status` da 404 | La API que corre es de una imagen anterior. `docker compose --profile full up -d --build api` |
| Login da 404 justo tras instalar | El proceso viejo aún no cedió el puesto. Esperar y repetir |
| `psql: No such file or directory` | Git Bash reescribiendo la ruta. Ver el aviso del paso 3 |
| El seed dice `No se pudo entrar (401)` | Correo o contraseña distintos de los del paso 5 |
| El seed no crea nada y dice `=` en todo | Ya estaba sembrado. Es lo correcto |
| El catálogo público responde 404 | M01 está inactivo. Paso 6 |
