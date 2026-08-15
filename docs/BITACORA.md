# Bitácora de SILLAR

Registro vivo para continuar el trabajo. Los documentos de `docs/` dicen **qué** construir; esto dice **con qué criterio** y **qué toca ahora**.

Si retomas el proyecto sin haber estado en la conversación: lee las secciones 1 a 4 antes de decidir nada.

**Última actualización:** 14 de agosto de 2026 · commit `3930784`

---

## 1. Estado

| | |
|---|---|
| Fundación F-01 a F-08 | Completa |
| CORE — backend | Completo. 20 rutas, 181 pruebas |
| CORE — pantallas | **En curso: entrega 4a** |
| M01 Catálogo en adelante | Sin empezar |

Entorno: PostgreSQL 16 en Docker con colación ICU `es-PE`. Backend en `:5080`, frontend Vite en `:5173` con proxy a `/api` y `/media`.

**Ahora mismo:** entrega 4a — pantallas de módulos y usuarios, con los módulos de mentira como requisito previo. Después, 4b: configuración, auditoría y medios. Luego M01.

---

## 2. El criterio

Reglas de decisión del proyecto. Una duda nueva se resuelve con estas, no improvisando otras.

**No se construye una abstracción hasta que existe un segundo caso real.** Se ha usado para *no* construir: `Result`/`Error` sin endpoint que los use, recuento de referencias entre módulos y archivos, marca blanca del panel.

**Fallar ruidosamente antes que degradarse en silencio.** El host aborta si un módulo activo tiene una dependencia dura inactiva. Un 403 de CSRF no se reintenta. A medias es peor que parado, porque nadie lo mira.

**Lo específico de un cliente nunca entra al producto.** Ni en código, ni en semilla, ni en nombres. Primero configuración, luego opción del módulo, luego módulo aparte.

**La documentación describe lo que debe existir, no lo que existe.** Un documento de entrega no da por hecho que lo anterior está construido: lo comprueba.

**Cuando código y documento discrepan, gana el documento y se avisa.** Y si el código tenía razón, se corrige el documento explicando por qué. Ha pasado varias veces y siempre mejoró el documento.

**Lo barato ahora es carísimo después.** Renombrar el producto antes de escribir código costó una tarde. La colación se fijó al crear el clúster.

**En la interfaz: ningún «Ha ocurrido un error» y ningún botón «Aceptar».** Un conflicto es una frase que dice qué lo impide y qué hacer; un botón nombra la acción que ejecuta.

---

## 3. Decisiones que se rompen sin querer

| Decisión | Por qué |
|---|---|
| Una instancia por cliente | Ninguna tabla lleva `tenant_id`; el aislamiento es físico |
| Un schema por módulo | Desinstalar es soltar un schema |
| FK cruzada solo en dependencia dura | Las blandas van en `database/integrations/`, **nunca** en una migración |
| Migraciones EF como fuente de verdad | Seeds e integraciones siguen siendo SQL a mano |
| Dos colaciones | `es_ci` respeta tildes para identidad; `es_search` las ignora para búsqueda |
| Token CSRF derivado | Estable en la sesión → varias pestañas, y sobrevive al reinicio del host |
| `installation_key` no sale del servidor | Uso criptográfico. Todo uso externo va por un valor derivado |
| Activar un módulo reinicia el host | El enrutamiento se construye al arrancar |
| `is_orphan` = desinstalado, no desactivado | Desactivar es reversible y pasa en cada demostración |
| SVG rechazado | Se ejecuta en el mismo origen del panel y puede pedir el token CSRF |
| Panel con marca SILLAR | Es lo que se demuestra al vender |
| Baja lógica en todo | Lo borrado deja huecos en banners y pedidos que lo referencian |
| Sin desactivación en cascada | El sistema nombra el obstáculo; la persona ordena |

---

## 4. Hábitos que salieron de errores reales

- **Los avisos del arranque se leen.** Dos líneas ignorables delataron que la base ordenaba `ñandú` después de `zapato`.
- **Verificar el efecto observable, no que el mecanismo actúe.** El límite de tamaño funcionaba y la respuesta devolvía 500 en vez de 413.
- **Patrones de `.gitignore` anclados a la raíz.** `media/` sin anclar se tragó código fuente.
- **`tasklist /FI "PID eq <pid>"` antes de matar nada.** Matar por puerto tumbó Docker Desktop entero. Para liberar un puerto, cambiar el puerto.
- **Serialización de binarios explícita.** `Guid.ToByteArray()` habría dado tokens distintos en Windows y en Arch.
- **Cada entrada de `onlyBuiltDependencies` exige justificación en el commit.** La lista corta es la defensa.

---

## 5. Pendientes

| Pendiente | Estado |
|---|---|
| **Módulos de mentira** | Requisito previo de la entrega 4a. Sin ellos, la pantalla de módulos no se puede probar y tres criterios de la entrega 3 siguen sin verificar |
| **Verificación visual de F-08** | Lista de diez minutos en la §6. Hacerla **antes** de las pantallas |
| `docker compose --profile full up -d --build` | Criterio de reinicio del contenedor, sin ejecutar |
| Confirmar `CLAUDE.md` de la raíz | Que conserve la línea de `ENTREGA-NN-*.md` y la de no reintroducir rotación de CSRF |
| Tipografía y logo de SILLAR | La paleta está validada; lo demás no |
| Dominio del producto | Sin registrar |

Aplazados por decisión, no pendientes: retención de auditoría, vectoriales en medios, permisos granulares, vencimiento de licencias, marca blanca.

---

## 6. Verificación manual de F-08

Con API y frontend levantados, diez minutos:

1. **Reconexión.** Entra al panel, detén el API con Ctrl+C, comprueba que aparece la superposición y que no salen más peticiones. Relánzalo: debe recuperarse solo, **con la sesión abierta**, y una escritura debe funcionar sin volver a entrar.
2. **Dos pestañas** escribiendo. Ningún 403.
3. **Teclado.** Recorre el login con Tab. El foco siempre visible.
4. **Tema oscuro.** Busca texto que pierda contraste.
5. **Sesión.** Recarga con sesión abierta: se mantiene. Cierra sesión: el panel te rechaza.

Lo que falle se arregla antes de las pantallas. Corregir el armazón con cinco pantallas encima cuesta el triple.

---

## 7. Última sesión — 14 de agosto de 2026

**Decidido:** identidad del panel (`MARCA.md` §6, marca SILLAR con el negocio como contexto); `installation_key` no sale del servidor —se detectó que la fase 5 tendería a meterla en el archivo de licencia, poniendo la clave del CSRF en manos del cliente—; procedimiento de recuperación de una instalación que no arranca.

**F-08 entregado.** Tres decisiones de Claude Code aprobadas: el estado de reconexión fuera de React —así el cliente HTTP lo consulta antes de cada envío y ninguna pantalla tiene que acordarse—, la recarga completa al reconectar, y la autorización de un único script de instalación en pnpm.

**Consecuencia anotada:** la recarga completa pierde un formulario a medias en otra pestaña. Hoy no aplica porque no hay formularios largos; cuando 4a los tenga, avisar antes de recargar.

**Entrega 4a especificada.** Se parte de 4b a propósito: módulos y usuarios tienen reglas reales, y los patrones que fijen —tabla, formulario, confirmación destructiva, errores tipados— se repiten tres veces en 4b. Si salen mal, salen mal multiplicados.
