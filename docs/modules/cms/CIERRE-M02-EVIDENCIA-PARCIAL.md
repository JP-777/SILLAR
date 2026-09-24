# M02 — Evidencia parcial de cierre

**Base:** `21983d1f5491c87f7ff61d8d21e748d5483f9ff1`

**Rama:** `test/m02-cierre-evidencia`

**Veredicto:** 10 PASS, 1 FAIL. No constituye cierre M02.

## Ejecución de los once criterios pendientes

| Criterio | Estado | Evidencia |
|---|---|---|
| 3 | PASS | Desactivación, arranque, menú, portada y pie |
| 5 | PASS | Fallback móvil e imágenes con proporciones distintas |
| 13 | PASS | Cambio efectivo del slug y del enlace público |
| 16 | PASS | Precios variables y sin precio |
| 17 | PASS | Gratis puro frente a gratis más presentación de pago |
| 18 | PASS | Retirada de la presentación más cara |
| 19 | PASS | Cambio de precio propagado al destacado |
| 21 | FAIL | Diferencia visual entre CMS y Catálogo sin fotografía |
| 24 | PASS | Cero solicitudes al selector con Catálogo inactivo |
| 25 | PASS | Mensaje explícito para búsquedas por prefijo |
| 32 | PASS | Móvil, escritorio, teclado y comprobación de colores |

Los diez criterios PASS completaron la secuencia: ejecución
positiva inicial, regresión simulada detectada y retorno a verde.
Estas simulaciones se realizaron en la frontera UI/API;
no fueron mutaciones del código de producción.

## Hallazgo C21 — No corregido

La tarjeta sin foto de Catálogo utiliza el componente `NoPhoto`,
con categoría y nombre dentro del espacio de la fotografía.

La tarjeta de CMS omite ese espacio cuando no hay imagen,
aunque sí muestra el nombre, la categoría y el enlace público.

La prueba comprobó la existencia de esos datos y falló
únicamente al exigir la presentación equivalente a Catálogo.

**Clasificación:** diferencia funcional de presentación
respecto de la equivalencia exigida por el criterio 21.

**Acción:** registrada; no se modifica producción ni se rebaja
la prueba para conseguir un resultado verde.

## Pendiente

- Completada la comprobación negativa simulada de las diez
  barreras aprobadas y verificado su retorno a verde.
- Pendiente, si se exige, la mutación del código de producto:
  las simulaciones UI/API no acreditan esa modalidad.
- Mantener C21 en FAIL hasta una corrección autorizada
  y verificada fuera de este encargo de evidencia.
- No declarar los 33 criterios en PASS.
- No ejecutar el criterio innegociable como certificado
  final mientras no estén aprobados los 33.
- No marcar M02 como cerrado en el ROADMAP.

## Registro

Ejecución completa:
`docs/modules/cms/evidencias/ONCE-CRITERIOS-20260923.txt`

Pruebas:
`e2e/tests/m02-cierre-evidencia.spec.ts`

SHA256 del log original: `d12a3a42173d31047f893d52f9cea803e289412dd91da151274c0e387e3a4bda`


## Controles negativos y retorno a verde — 2026-09-24

**Base de la comprobación:** `6d2353cefcfc9889a6db564de2d49602065e07ec`

| Criterio | Positivo inicial | Regresión simulada | Retorno |
|---|---|---|---|
| 3 | PASS | Detectada | PASS |
| 5 | PASS | Detectada | PASS |
| 13 | PASS | Detectada | PASS |
| 16 | PASS | Detectada | PASS |
| 17 | PASS | Detectada | PASS |
| 18 | PASS | Detectada | PASS |
| 19 | PASS | Detectada | PASS |
| 24 | PASS | Detectada | PASS |
| 25 | PASS | Detectada | PASS |
| 32 | PASS | Detectada | PASS |

Las diez simulaciones provocaron el fallo de la afirmación
correspondiente. En C24 se produjo además un error de consola,
efecto adicional de la solicitud indebida simulada.

Se eliminó la spec temporal. La spec original permaneció
inalterada y los diez casos volvieron a pasar.

**C21:** excluido de las simulaciones. Conserva el defecto real
de presentación registrado, sin modificación del producto.

**Alcance:** simulación controlada en la frontera UI/API.
No acredita mutaciones de código de producción.

**Estado:** evidencia parcial; M02 sigue abierto.

### Registros conservados

- `docs/modules/cms/evidencias/CONTROLES-NEGATIVOS-20260924.txt`
- `docs/modules/cms/evidencias/RETORNO-VERDE-20260924.txt`

SHA-256 del log negativo original: `a50a7b2ae9e83bb9d4e05af7e436abc97c9357452e7f9b8551b44b817e1894d2`

SHA-256 del log de retorno original: `8bf0696c9a84845d77eeb55464741de0ecdf4540feb406699d23b1bf1f1c2172`

## Ciclo de instalación y desinstalación — 2026-09-24

**Resultado:** PASS diagnóstico, mutación detectada y retorno
a verde. Esta evidencia no constituye el cierre de M02.

### Ejecución positiva

Se publicó un producto destacado y una red social.
La prueba comprobó que M02 puede desactivarse, retirar su
integración con Catálogo y desinstalar su schema.

Comprobó que CORE y Catálogo conservan sus datos y tablas,
y que la aplicación mantiene las rutas y superficies
correspondientes a los módulos restantes.

Los scripts de retirada se ejecutaron dos veces. Después,
las migraciones reconstruyeron M02 y se pudo publicar
contenido nuevamente.

**Resultado:** PASS.

### Mutación real y control negativo

Se añadió temporalmente al script `cms/99_drop.sql`
la creación indebida de una tabla en CORE.

La prueba detectó la alteración del inventario de
CORE: diez tablas antes del desmontaje y once después.

**Resultado:** fallo provocado detectado por la comprobación
de conservación de los módulos ajenos.

La alteración del archivo SQL se revirtió byte por byte.

### Retorno a verde

Con el SQL original restaurado se repitió la prueba
diagnóstica completa.

**Resultado:** PASS.

### Alcance y limitaciones

La mutación fue real sobre una copia de trabajo del script
SQL y se ejecutó exclusivamente contra el entorno E2E
efímero de esta worktree.

Esta prueba demuestra que la barrera detecta la alteración
introducida en CORE. No demuestra que pueda detectar
todas las regresiones posibles del ciclo de vida.

C21 sigue abierto por la diferencia visual entre la tarjeta
sin fotografía de CMS y la presentación de Catálogo.

M02 continúa sin autorización de cierre ni fusión.

### Archivos

- `e2e/tests/zz-z-m02-ciclo-diagnostico.spec.ts`
- `docs/modules/cms/evidencias/CICLO-INICIAL-20260924.txt`
- `docs/modules/cms/evidencias/CICLO-MUTACION-20260924.txt`
- `docs/modules/cms/evidencias/CICLO-RETORNO-20260924.txt`

SHA-256 de los registros originales:

- Inicial: `324df6ac4b23d3e024784d74a91ae7f2aa601fba8aa054f457b12c76c4a8b34a`
- Mutación: `23e43b560f122e9022c2ab904758f9af1515aadc1ad2b290cdb95379df5c8317`
- Retorno: `ae2b343b69241e7db702f714efd42727575022f968a2dedadef9a6500c06a8b9`
