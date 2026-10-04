# Costura de M05a en la puerta — evidencias del 4 de octubre de 2026

**Rama:** `m05a-vitrina-dev`. **Árbol bajo prueba:** `b230ac4` más los cambios de la costura,
que entran en el commit que publica este índice. **Máquina:** contenedor en la nube.
**Escrito por:** Claude Code D, con el turno de navegación concedido por Chat 2 vía JP.
**No es la certificación canónica:** esa la hace la QA local.

Las dos corridas son `node scripts/verificar.mjs`, sin argumentos.

## Las dos direcciones

| Evidencia | `Sillar.Modules.Services` en la lista de la etapa 4 | Resultado |
|---|---|---|
| `COSTURA-PUERTA-A-SIN-SERVICES-20261004.txt` | **No** (quitado a mano solo para esta corrida y restaurado por SHA-256) | **ROJA** en la etapa 5, `EXIT=1` |
| `COSTURA-PUERTA-B-CON-SERVICES-20261004.txt` | **Sí** | Etapas **1 a 5 en verde**; la 6 muere por el entorno |

### A · sin Services

`FALLÓ en la etapa: pruebas del backend`. Los **10** fallos son las pruebas de persistencia de M05a
(`ServicesPostgresTests`), con la causa en su propio mensaje: «El schema 'services' no está migrado
en 'sillar_verify_…'». Las 7 de reglas puras siguen en verde.

**La etapa 4 pasa**: nadie pide las migraciones de Services. El rojo sale en la etapa 5, donde
algo reclama el schema. Es el mismo patrón de §j de Sales
(`docs/modules/sales/evidencias/J-INDICE-20261003.md`).

Para que esta dirección fuera observable, las pruebas ya **no migran por su cuenta**
(`ServicesDbFixture.PrepararAsync`). Si lo hicieran, quitar Services de la lista no rompería
nada.

### B · con Services

Etapas 1 a 5: **PASS**. Incluyen:

- en la etapa 1, `test:services` y el vocabulario de auditoría con `service_entry`;
- en la etapa 3, la compilación de la solución con los tres proyectos de M05a;
- en la etapa 4, la migración de `services`;
- en la etapa 5, toda la suite del backend.

La etapa 6 muere al construir la imagen de la API: `NU1301 … UntrustedRoot`, el certificado del
proxy de la nube dentro de `docker build`. Es el fallo de entorno ya conocido en este contenedor
desde el inicio del proyecto, y **no depende de M05a**. No se presenta como 6/6.

**Recuentos.** La puerta en verde no los imprime, así que se tomaron aparte, sobre una base efímera
con los mismos seis módulos migrados:

| Medida | Resultado |
|---|---|
| `dotnet test backend/Sillar.sln` | **611/611**, 0 fallidas, 0 omitidas, en 9 proyectos |
| `Sillar.Modules.Services.Tests` | **17/17** |

## Archivos

Las dos copias son del log de la corrida, con una sola normalización: se quitaron los espacios
finales de línea para que `git diff --check` pase.

| Publicado | SHA-256 del original | SHA-256 publicado |
|---|---|---|
| A | `1ad8a2316802e70f0b7cb690dbf15124065fd6c53a232ae88c4fd78b1b898f15` | idéntico (no tenía espacios finales) |
| B | `29dd9434ab133d96ebee368958ab323c6cac22a42b6a4c92356b9e2c07caf112` | `e7edd956289f2400b10f8e29810c8350d08dc90a79452a73fa47cdb8cf0ac8a1` |

**Secretos:** se buscó en los dos la contraseña de la base local, `Password=`, `Username=` y
`ConnectionStrings__`. Ninguna coincidencia.
