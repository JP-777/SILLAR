# Levantar SILLAR para una demostración

Los comandos exactos, **probados una vez de principio a fin el 20 de agosto de 2026** sobre una
base recién borrada. No están escritos de memoria: son los que se ejecutaron, en este orden.

Lo que queda en pie al terminar: **CORE + M01 Catálogo**, con 4 marcas, 9 categorías en dos
niveles y 20 productos.

---

## 0 · Lo que hace falta tener

- Docker Desktop (Windows con WSL2) o Docker (Linux), **arrancado**.
- El SDK de .NET 10 y `dotnet-ef` — solo para las migraciones.
- `pnpm`, si se va a enseñar con el frontend de desarrollo.
- El archivo `.env` en la raíz. Si no está: `cp .env.example .env` y cambiar las contraseñas.

**La primera vez, la API tarda varios minutos** en construirse: se descarga el SDK de .NET.
Hacerlo el día anterior, no delante de nadie.

---

## 1 · La base de datos, desde cero

```bash
docker compose down -v          # BORRA los datos. Es lo que se quiere aquí, y solo aquí
docker compose up -d db
docker compose ps               # esperar a que db diga «healthy»
```

## 2 · Las migraciones

Las tablas las crean las migraciones de EF Core, nunca un script (ADR-009):

```bash
cd backend
dotnet ef database update --project Sillar.Core            --startup-project Sillar.Api
dotnet ef database update --project Sillar.Modules.Catalog --startup-project Sillar.Api
cd ..
```

## 3 · Los seeds

```bash
docker compose exec -T db psql -U postgres -d sillar_dev -f /scripts/modules/core/02_seed.sql
docker compose exec -T db psql -U postgres -d sillar_dev -f /scripts/modules/catalog/02_seed.sql
```

> **El truco de Git Bash en Windows.** Git Bash reescribe `/scripts/...` a `C:/Program
> Files/scripts/...` antes de que el comando salga, y `psql` responde *No such file or
> directory* por un archivo que sí existe dentro del contenedor. Se evita anteponiendo
> `MSYS_NO_PATHCONV=1` a cada comando:
>
> ```bash
> MSYS_NO_PATHCONV=1 docker compose exec -T db psql -U postgres -d sillar_dev -f /scripts/modules/core/02_seed.sql
> ```
>
> En PowerShell y en Linux no hace falta nada.

Los dos seeds están **vacíos de datos de negocio a propósito** (SPEC de M01 §6.9). Los datos de
la demostración llegan en el paso 6, que es de otra naturaleza y por eso vive fuera.

## 4 · La API

```bash
docker compose --profile full up -d --build api
```

Y esperar a que responda:

```bash
curl http://localhost:5080/api/setup/status
# {"setupRequired":true}   ← base limpia, instalación pendiente
```

## 5 · La instalación

Crea el negocio y su primer administrador. **Después de esto el proceso se reinicia solo**: es a
propósito, el enrutamiento se construye al arrancar.

```bash
curl -X POST http://localhost:5080/api/setup \
  -H "Content-Type: application/json" \
  -d '{"businessName":"Demostracion SILLAR","licenseType":"trial",
       "admin":{"fullName":"Persona Administradora",
                "email":"demo@sillar.local",
                "password":"LA-QUE-ELIJAS-AQUI"}}'
```

En PowerShell, con `curl.exe` y comillas dobles escapadas, o desde Swagger en
`http://localhost:5080/swagger`.

Esperar unos segundos y comprobar que el proceso nuevo está arriba:

```bash
curl -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5080/api/admin/auth/login \
  -H "Content-Type: application/json" -d '{"email":"x@x.x","password":"x"}'
# 401 ← el proceso nuevo responde. Un 404 significa que el viejo todavía no cedió el puesto
```

## 6 · Activar el catálogo y sembrar la demostración

**M01 nace inactivo.** Se activa desde el panel (Módulos → Catálogo) o por API. Activarlo
**vuelve a reiniciar** el proceso; es la misma razón de antes.

Con el módulo activo:

```bash
SILLAR_EMAIL=demo@sillar.local SILLAR_PASSWORD='la que elegiste en el paso 5' \
  node scripts/demo/seed-demo.mjs
```

En PowerShell:

```powershell
$env:SILLAR_EMAIL = "demo@sillar.local"
$env:SILLAR_PASSWORD = "la que elegiste en el paso 5"
node scripts/demo/seed-demo.mjs
```

Tarda menos de un minuto y es **idempotente**: correrlo dos veces no duplica nada, dice `=` en
vez de `+` y sigue. Las imágenes **se generan**, no están en el repositorio.

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
