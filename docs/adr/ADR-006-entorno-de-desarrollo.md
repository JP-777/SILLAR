# ADR-006 — Entorno de desarrollo con Docker Compose

- **Estado:** Aceptada
- **Fecha:** 2026-08-14
- **Decide:** JP

## Contexto

El desarrollo alterna entre dos máquinas: una PC con Arch Linux, donde se hizo todo el trabajo previo, y una laptop con Windows, que es la máquina actual. El trabajo estuvo detenido justamente por problemas al instalar PostgreSQL. El código se sincronizará mediante un repositorio Git clonado en ambas.

## Decisión

PostgreSQL —y en el futuro los demás servicios de infraestructura— se levantan con **Docker Compose**, no con instalaciones nativas.

## Razones

- El entorno es idéntico en Windows y en Arch: misma versión, misma configuración, mismos datos de arranque. Desaparece la clase de problema que detuvo el proyecto.
- Levantar el entorno es un comando, y desmontarlo también. Reiniciar desde cero deja de ser un evento traumático, lo que importa cuando se van a instalar y desinstalar módulos para probar la modularidad.
- Es lo mismo que se desplegará después, de modo que el entorno de desarrollo se parece a producción.
- Con una instancia por cliente (ADR-001), poder levantar varias bases de datos aisladas en paralelo es directamente útil para probar instalaciones con distintos conjuntos de módulos.

## Consecuencias

**Positivas.** Entorno reproducible y desechable. Onboarding de una máquina nueva en minutos. Camino directo hacia el despliegue en contenedores.

**Negativas.** Requiere Docker Desktop con WSL2 en Windows, que consume memoria y tarda en arrancar. En equipos con poca RAM puede resultar pesado; si eso ocurre, la salida es el instalador nativo de PostgreSQL, con el costo de mantener dos configuraciones distintas.

## Detalles de configuración

- Imagen `postgres:16-alpine`, alineada con la versión usada para validar el script inicial.
- Volumen con nombre para persistir datos entre reinicios.
- `healthcheck` para que el backend no intente conectarse antes de tiempo.
- La carpeta `database/` se monta como solo lectura dentro del contenedor, de modo que los scripts se ejecutan bajo control y no automáticamente al arrancar.
- Credenciales y puertos en `.env`, nunca en el repositorio. `.env.example` sí se versiona.

## Nota sobre trabajar en dos sistemas operativos

Se versiona un `.gitattributes` con `* text=auto eol=lf` para evitar que los finales de línea de Windows ensucien los diffs y rompan los scripts al ejecutarse en Linux. Es un detalle pequeño que, sin él, genera confusión difícil de diagnosticar.
