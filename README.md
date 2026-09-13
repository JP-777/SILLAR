# SILLAR

> **S**istema **I**ntegrado y **L**icenciable de **L**ogística, **A**dministración y **R**etail

Plataforma web para negocios de retail y servicios, construida como **conjunto de módulos desmontables y licenciables por separado**: catálogo, contenido web, clientes, ventas online, servicios, seguimiento de servicios, solicitudes B2B, portal del cliente, inventario y reportes.

El sillar es la piedra volcánica con la que está construida Arequipa: un bloque modular con el que se levantan edificios. La metáfora es deliberada.

Primera instalación en curso: una librería y bazar de Arequipa, Perú.

---

## Puesta en marcha

Requisitos: Docker Desktop con WSL2 en Windows, o Docker en Linux; Node.js y .NET SDK 10 para desarrollo.

```bash
# 1. Generar la configuración propia de este árbol
node scripts/estrenar.mjs

# 2. Editar únicamente los secretos de .env
# POSTGRES_PASSWORD y Password=... dentro de ConnectionStrings__Default
# deben contener la misma contraseña.
# PGADMIN_PASSWORD solo se usa con el perfil tools.

# 3. Levantar PostgreSQL
docker compose up -d db

# 4. Verificar que arrancó bien
docker compose ps
docker compose logs -f db
```

No copies `.env.example` ni el `.env` de otra worktree. `scripts/estrenar.mjs`
deriva automáticamente nombre de proyecto, base, nodo y puertos desde el
directorio del árbol.

**Importante:** `POSTGRES_PASSWORD` se utiliza al crear un clúster nuevo.
Cambiarla después en `.env` no cambia la contraseña almacenada dentro de un
`db_data` que ya existe. `docker compose down -v` elimina ese volumen y sus
datos, por lo que solo se usa con bases deliberadamente desechables.

Herramientas opcionales:

```bash
docker compose --profile tools up -d    # añade pgAdmin; consulta su puerto con `node scripts/identidad.mjs`
```

Conectarse por línea de comandos:

```bash
docker compose exec db sh -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```

Detener:

```bash
docker compose down        # conserva los datos
docker compose down -v     # BORRA los datos
```

---

## Instalación de módulos

La instalación inicial prepara los schemas de **todos los módulos desplegados**
antes de crear la instalación. El instalador aplica sus migraciones siguiendo
el orden del grafo; la activación no migra, sino que comprueba que el schema
esperado exista antes de marcar un módulo como activo.

Para ejecutar migraciones explícitamente en desarrollo, diagnóstico o
recuperación, la herramienta `dotnet-ef` está fijada en
`backend/.config/dotnet-tools.json`:

```bash
cd backend
dotnet tool restore
dotnet ef database update --context CatalogDbContext --project Sillar.Modules.Catalog
cd ..
```

Los seeds de módulo contienen solo datos mínimos del producto. Los datos de
demostración se cargan aparte mediante `scripts/demo/seed-demo.mjs`.

Los scripts de integración se ejecutan **solo si ambos módulos están instalados**:

```bash
docker compose exec db psql -U postgres -d sillar_dev -f /scripts/integrations/sales_crm.sql
```

Todos los scripts son idempotentes: ejecutarlos dos veces no duplica datos ni produce errores.

---

## Levantar una demostración

Los comandos exactos para arrancar de cero —base limpia, migraciones, instalación, catálogo de
demostración— están en **`docs/DEMOSTRACION.md`**, probados de principio a fin y no escritos de
memoria. Incluye los datos de acceso, el recorrido que se enseña y qué hacer si algo falla.

Los datos de demostración **no viven en los seeds de los módulos**, que están vacíos de datos de
negocio a propósito (ADR-008): los siembra `scripts/demo/seed-demo.mjs` por API, y las imágenes
se generan en memoria en vez de commitearse.

---

## Estructura

```
├── CLAUDE.md                     instrucciones para Claude Code
├── docker-compose.yml            entorno de desarrollo
├── docs/
│   ├── ARQUITECTURA_MODULAR.md   documento maestro
│   ├── ROADMAP_MODULAR.md        plan de trabajo
│   ├── MARCA.md                  identidad del producto y del cliente
│   ├── adr/                      decisiones de arquitectura
│   └── modules/<módulo>/SPEC.md  especificación de cada módulo
├── database/
│   ├── modules/<módulo>/         schema, seed y drop por módulo
│   └── integrations/             claves foráneas entre módulos opcionales
├── backend/                      solución .NET Sillar (pendiente)
└── frontend/                     aplicación React (pendiente)
```

---

## Documentación

| Documento | Para qué sirve |
|---|---|
| `docs/ARQUITECTURA_MODULAR.md` | Catálogo de módulos, dependencias, schemas y reglas |
| `docs/ROADMAP_MODULAR.md` | Fases, orden de construcción y ciclo de módulo |
| `docs/adr/` | Por qué se decidió cada cosa |
| `docs/modules/core/SPEC.md` | Especificación del módulo CORE, el primero a construir |
| `docs/MARCA.md` | Identidad de SILLAR y separación respecto a la marca del cliente |
| `docs/modules/_PLANTILLA_SPEC.md` | Plantilla para especificar un módulo nuevo |
| `docs/adr/ADR-008-repositorio-por-cliente.md` | Dónde vive lo específico de cada instalación |
| `CLAUDE.md` | Reglas que sigue Claude Code al escribir código |
