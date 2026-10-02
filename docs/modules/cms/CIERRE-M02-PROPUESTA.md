# M02 — Propuesta de cierre

**Fecha:** 2026-09-25

**Rama:** `test/m02-cierre-evidencia`

**Base del encargo:** `d18b24419b4fb573e561139f04ae34b271149344`

**Veredicto técnico:** 33 criterios con evidencia positiva.
Puerta canónica y criterio innegociable aprobados.

**Estado administrativo:** cierre propuesto, pendiente de
revisión del colíder. No fusionado.

## Corrección del criterio 21

CMS reutiliza `NoPhoto` de Catálogo y presenta el nombre
del producto y su categoría en el espacio de la imagen
cuando el destacado no tiene fotografía.

Se mantuvo Catálogo intacto. La prueba compara directamente
los textos y el tratamiento visual de ambas tarjetas.

La mutación temporal que eliminaba la categoría fue
detectada por la prueba. Restaurado el código, C21 volvió
a pasar.

## Los 33 criterios de aceptación

La numeración y los enunciados proceden directamente de
`docs/modules/cms/SPEC.md`, sección 11.

La evidencia histórica de los otros 22 criterios se conserva
en la bitácora y las pruebas anteriores. Los once criterios
que estaban pendientes tienen sus pruebas focales y la
ejecución integrada posterior.

| N.º | Criterio | Estado | Evidencia |
|---|---|---|---|
| 1 | El schema `cms` se crea y se elimina sin afectar a `core` ni a `catalog`, y se vuelve a crear | PASS | Histórica, ciclo y puerta |
| 2 | Los scripts son idempotentes: se corren dos veces y se comparan estados, no la ausencia de excepciones | PASS | Histórica, ciclo y puerta |
| 3 | Con M02 desactivado, la aplicación arranca, el menú no muestra «Contenido», y la portada y el pie no quedan con huecos | PASS | Histórica, ciclo y puerta |
| 4 | **El caso de la campaña escolar:** un banner con vigencia de febrero no aparece en enero, aparece en febrero y desaparece en marzo, sin que nadie lo toque | PASS | Histórica y puerta |
| 5 | Un banner sin imagen de móvil usa la de escritorio; con las dos, cada una en su proporción | PASS | Prueba focal y puerta |
| 6 | `ends_at` anterior a `starts_at` se rechaza con una frase que nombra la fecha | PASS | Histórica y puerta |
| 7 | `link_url` sin `link_label` se rechaza | PASS | Histórica y puerta |
| 8 | Reordenar cinco banners y recargar devuelve el mismo orden; una petición interrumpida no deja dos en la misma posición | PASS | Histórica y puerta |
| 9 | Dos enlaces de la misma red se rechazan, sin distinguir mayúsculas | PASS | Histórica y puerta |
| 10 | Desactivar y reactivar una red conserva la misma fila y la devuelve al feed público; `editor` recibe 403 y `admin` puede reactivarla | PASS | Histórica y puerta |
| 11 | **Con M01 desinstalado, los destacados conservan nombre e imagen y la portada no muestra enlaces rotos** | PASS | Histórica y puerta |
| 12 | Renombrar un producto destacado actualiza su tarjeta en la portada, sin que nadie toque M02 | PASS | Histórica y puerta |
| 13 | Corregir a mano el slug de un producto destacado deja el enlace de la portada apuntando al sitio correcto | PASS | Prueba focal y puerta |
| 14 | Desactivar un producto en M01 lo retira de la portada y lo deja visible en el panel | PASS | Histórica y puerta |
| 15 | Destacar un producto activo pero no publicado se permite, se avisa en el buscador, y **no** aparece en el endpoint público hasta que se publique | PASS | Histórica y puerta |
| 16 | Un producto con presentaciones de distinto precio muestra «Desde»; uno sin precio, «a consultar» | PASS | Prueba focal y puerta |
| 17 | **Un producto gratis muestra «Gratis», y uno con una presentación gratis y otra de pago no dice que sea gratis** | PASS | Prueba focal y puerta |
| 18 | Retirar la presentación más cara de un producto destacado actualiza el precio de la portada | PASS | Prueba focal y puerta |
| 19 | Editar el precio de una presentación de un producto destacado actualiza el de la portada | PASS | Prueba focal y puerta |
| 20 | Desactivar la categoría principal emite `CategoriaDesactivada`; M02 relee todos sus destacados y actualiza la categoría efectiva de la tarjeta | PASS | Histórica y puerta |
| 21 | Un destacado sin foto muestra el nombre con su categoría encima, igual que la tarjeta del catálogo | PASS | Prueba focal, control negativo y puerta |
| 22 | Con un evento perdido a propósito, «actualizar datos» deja el snapshot al día | PASS | Histórica y puerta |
| 23 | Dos refrescos simultáneos del mismo producto no dejan el valor viejo | PASS | Histórica y puerta |
| 24 | Con Catálogo inactivo, el panel no solicita el selector: oculta destacar, buscar y reenlazar, y explica que hay que activar Catálogo | PASS | Prueba focal y puerta |
| 25 | Mientras la búsqueda de M01 exija palabras completas, escribir `plum`, `lapi` o `cuad` no deja un vacío mudo: el selector explica que hay que completar la palabra | PASS | Prueba focal y puerta |
| 26 | Tras retirar la integración, los destacados aparecen en el panel como «pendiente de volver a enlazar», con su nombre, y se pueden reasignar | PASS | Histórica y puerta |
| 27 | Borrar en CORE un medio usado por un banner **no falla**: el banner queda sin imagen y deja de publicarse | PASS | Histórica y puerta |
| 28 | Desactivar en CORE un medio usado por un banner deja el banner sin imagen y ninguna operación falla | PASS | Histórica y puerta |
| 29 | Ninguna pantalla muestra un identificador | PASS | Histórica y puerta |
| 30 | Ningún «Ha ocurrido un error» ni ningún botón «Aceptar» | PASS | Histórica y puerta |
| 31 | Todos los endpoints en Swagger | PASS | Histórica y puerta |
| 32 | La interfaz responde en móvil y escritorio, navegable por teclado, sin colores escritos a mano | PASS | Prueba focal y puerta |
| 33 | `cms_catalog.sql` añade la FK solo con ambos módulos instalados, y su `_drop` la retira anulando huérfanos | PASS | Histórica, ciclo y puerta |

## Puerta canónica

**Resultado:** PASS, seis de seis etapas.

La puerta verificó frontend, arnés E2E, compilación del
backend, migraciones, pruebas del backend y suite E2E.

El registro de la puerta en verde no expone el recuento
individual de Playwright. No se atribuye aquí una cantidad
de pruebas que dicho registro no acredita.

## Criterio innegociable

**Resultado:** PASS en ejecución independiente.

La prueba ejercita la publicación inicial de CMS, su
desactivación, la retirada idempotente de sus integraciones
y esquema, la conservación de CORE, Catálogo y CRM y la
posterior reinstalación de M02.

También comprueba las capacidades, las rutas y las
superficies públicas afectadas por su desactivación y
reinstalación, dentro del alcance de la prueba.

El control negativo del ciclo realizado previamente detectó
una mutación temporal que alteraba el inventario de CORE.
El SQL original permanece intacto.

## Trazabilidad

Los registros anteriores de evidencias y el documento
`CIERRE-M02-EVIDENCIA-PARCIAL.md` se conservan como
historial de los estados previos a esta corrección.

Resumen final:
`docs/modules/cms/evidencias/C21-CIERRE-20260925.txt`

## Decisión solicitada

Se propone el cierre de M02 con los 33 criterios
acreditados y el criterio innegociable aprobado.

La aprobación de esta propuesta corresponde al colíder.

No se autoriza ninguna fusión desde esta entrega.

---

## Revalidación sobre la rama candidata — 27/09/2026

**Añadido el 27 de septiembre de 2026, America/Lima.** Lo anterior se
conserva sin editar: describe el estado de `test/m02-cierre-evidencia`
(`b86738d`) antes de incorporar `main`.

**Rama candidata:** `integration/m02-cierre-candidata`. La rama original
`test/m02-cierre-evidencia` queda intacta en `b86738d`.

**`main` incorporado:** `9fe08b86a543ab7f8bf3dcf31d084bf16cdcf650`, por
merge normal (`6e01a20`). Trae la barrera de fronteras entre módulos del
frontend, que corre en la etapa `[1/6]` de la puerta.

### Por qué hacía falta

`cmsHome.tsx:3` importaba `NoPhoto` de `../catalog/components/ProductCard`:
un módulo importando de otro. La barrera lo rechaza (F1) y la puerta se
detiene en la etapa 1. `NoPhoto` vive en `frontend/src/shared/ui/NoPhoto.tsx`
desde `345dd88`, **trasladado sin cambios** y con las mismas clases
`ti-nophoto*`.

**Consecuencia sobre el criterio 21.** Donde la sección «Corrección del
criterio 21» dice que CMS reutiliza `NoPhoto` de Catálogo, desde `94ed702`
lo reutiliza de `shared/ui`. Es el mismo componente, que el catálogo también
toma de ahí, así que la comparación de C21 sigue midiendo lo mismo. **Su
acreditación sobre la candidata depende de la etapa 6**, pendiente abajo.

### Evidencia

| Momento | Registro | Resultado |
|---|---|---|
| Antes, barrera | `evidencias/FRONTERAS-ANTES-20260927.txt` | F1 en `modules/cms/cmsHome.tsx:3`, rc=1, sobre `6e01a20` |
| Antes, puerta | `evidencias/PUERTA-ANTES-20260927.txt` | Se detiene en `[1/6]`, paso «fronteras entre módulos», por ese F1. **Fallo intencional** |
| Corrección | commit `94ed702` | Solo `cmsHome.tsx:3` → `import { NoPhoto } from '../../shared/ui/NoPhoto';` |
| Después, barrera | `evidencias/FRONTERAS-DESPUES-20260927.txt` | Sin violaciones (151 ficheros, 686 referencias), rc=0; pruebas de la barrera 34/34 |
| Después, puerta (cloud) | `evidencias/PUERTA-DESPUES-20260927.txt` | Etapas 1–5 PASS; etapa 6 FAIL **de entorno** |

### Puerta sobre la candidata

| Etapa | Resultado en el entorno cloud |
|---|---|
| 1 Tipos del frontend (incluye la barrera) | PASS |
| 2 Tipos del arnés e2e | PASS |
| 3 Compilación del backend | PASS |
| 4 Migraciones sobre BD efímera | PASS |
| 5 Pruebas del backend | PASS |
| 6 Suite e2e | **FAIL de entorno**: `NU1301 … UntrustedRoot` en el `docker build` del API. El proxy TLS del entorno cloud no es de confianza dentro de la construcción; no ocurre en el equipo local |

**La candidata no está acreditada 6/6.** El «6/6 PASS» de la sección
«Puerta canónica» corresponde al estado anterior a incorporar `main`.

### Pendiente — puerta local, por B

Sobre la cabeza de `integration/m02-cierre-candidata`:

```bash
git fetch origin integration/m02-cierre-candidata
git worktree add ../sillar-m02-candidata origin/integration/m02-cierre-candidata
cd ../sillar-m02-candidata
node scripts/estrenar.mjs          # y rellena POSTGRES_PASSWORD / Password= en .env
pnpm install --dir frontend && pnpm install --dir e2e
docker compose up -d db
node frontend/scripts/fronteras-frontend.mjs   # sin violaciones
node scripts/verificar.mjs                     # 6/6 esperado, incluido [M02-C21]
```

Conviene repetir además el ciclo innegociable (`tests/zz-z-m02-ciclo-diagnostico.spec.ts`)
sobre la candidata, porque `main` trae cambios de M04 y de la puerta.

### Observación sobre la evidencia anterior

`evidencias/C21-CIERRE-20260925.txt` resume la puerta 6/6 y el ciclo con
los SHA-256 de sus registros locales, pero no nombra el commit de código
sobre el que corrieron. Para la revalidación conviene anotarlo.

### Decisión solicitada, actualizada

Se propone el cierre de M02 **sobre la cabeza de
`integration/m02-cierre-candidata`**, condicionado a que la puerta local
dé 6/6 sobre ese SHA. La aprobación corresponde al colíder.

**No se autoriza ninguna fusión desde esta entrega.**
