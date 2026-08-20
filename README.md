# SILLAR

> **S**istema **I**ntegrado y **L**icenciable de **L**ogística, **A**dministración y **R**etail

Plataforma web para negocios de retail y servicios, construida como **conjunto de módulos desmontables y licenciables por separado**: catálogo, contenido web, clientes, ventas online, servicios, seguimiento de servicios, solicitudes B2B, portal del cliente, inventario y reportes.

El sillar es la piedra volcánica con la que está construida Arequipa: un bloque modular con el que se levantan edificios. La metáfora es deliberada.

Primera instalación en curso: una librería y bazar de Arequipa, Perú.

---

## Puesta en marcha

Requisitos: Docker Desktop con WSL2 en Windows, o Docker en Linux.

```bash
# 1. Configurar el entorno
cp .env.example .env          # en PowerShell: Copy-Item .env.example .env
# editar .env y cambiar las contraseñas

# 2. Levantar la base de datos
docker compose up -d

# 3. Verificar que arrancó bien
docker compose ps
docker compose logs -f db
```

Herramientas opcionales:

```bash
docker compose --profile tools up -d     # añade pgAdmin en http://localhost:5050
```

Conectarse por línea de comandos:

```bash
docker compose exec db psql -U postgres -d sillar_dev
```

Detener:

```bash
docker compose down        # conserva los datos
docker compose down -v     # BORRA los datos
```

---

## Instalación de módulos

La base de datos no se crea de una sola vez: se instala módulo por módulo, según lo que el cliente tenga licenciado.

Las tablas las crean las **migraciones de EF Core** de cada módulo (ADR-009):

```bash
# 1. Aplicar las migraciones del módulo
dotnet ef database update --context CatalogDbContext --project backend/Sillar.Modules.Catalog

# 2. Sembrar sus datos mínimos
docker compose exec db psql -U postgres -d sillar_dev -f /scripts/modules/catalog/02_seed.sql
```

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
