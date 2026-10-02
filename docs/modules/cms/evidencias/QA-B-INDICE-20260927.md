# M02 — QA local independiente de la candidata de cierre (frente B)

Creado: 27/09/2026, America/Lima · Publicado por: frente B (Claude Code B), en su papel de QA
independiente, con autorización de JP.

| | SHA |
|---|---|
| **Código probado** | `2e6c72151a7046aad8269acbe2e5e4bff8f452cf` (cabeza de `integration/m02-cierre-candidata`, con `main` `9fe08b8` incorporado) |
| **Commit documental** | El que añade los archivos `QA-B-*`, hijo directo de `2e6c721`. No cambia código: la QA no se repitió sobre él |

**Equipo:** el equipo local de JP (Linux `archlinux`, 13 GiB, sin swap). Worktree aislada
`/home/JP777/sillar-qa-m02`, identidad derivada con offset 1, sin choque con otras worktrees. La
worktree está en `2e6c721` desde las 2026-09-27 01:12:22 (reflog), sin moverse después, y tenía 0
cambios sin commitear al empezar y al terminar la corrida (lo registra la cabecera de
`QA-B-PUERTA`).

## Lo que certifica esta QA — la corrida verde

| Comprobación | Resultado | Archivo |
|---|---|---|
| `cmsHome.tsx:3` | `import { NoPhoto } from '../../shared/ui/NoPhoto';` | leído en el árbol de `2e6c721` |
| `pnpm --dir frontend test:fronteras` (08:57) | 151 ficheros, 686 imports, sin violaciones · **34/34**, 0 omitidas · rc=0 | `QA-B-FRONTERAS-20260927.txt` |
| `node scripts/verificar.mjs` (08:58:02 → 09:21:02) | **6/6 PASS**, rc=0 | `QA-B-PUERTA-20260927.txt` |
| Suite e2e (etapa 6, 09:00:16, 20,7 min) | **159 en total: 159 aprobadas, 0 fallidas, 0 inestables, 0 omitidas** | `QA-B-PLAYWRIGHT-REPORT-20260927.json` |
| `[M02-C21]` (`m02-cierre-evidencia.spec.ts:440`) | **Aprobada**, 3,2 s | ídem |
| `[M02-CICLO]` (`zz-z-m02-ciclo-diagnostico.spec.ts:285`) | **Aprobada**, 75,8 s, con sus capturas desactivado / desinstalado / reinstalado | ídem |
| `[M02-C32]` (`m02-cierre-evidencia.spec.ts:570`) | Aprobada, 16,5 s | ídem |
| `catalogo.spec.ts:209` | Aprobada, 30,7 s | ídem |
| Memoria disponible durante la puerta | Mínimo **2673 MiB** (09:18:59), 273 muestras cada 5 s | `QA-B-MEMORIA-20260927.tsv` |

Etapas: 1 tipos del frontend (incluye `test:fronteras`) · 2 tipos del arnés e2e · 3 compilación del
backend · 4 migraciones backend (BD efímera) · 5 pruebas del backend · 6 suite e2e — **todas PASS**.

**Cómo corrió.** Lanzada desacoplada de la sesión (`setsid`), con un vigía que registraba memoria
cada 5 s y que habría detenido la puerta con SIGINT si la memoria disponible bajaba de 1 GiB dos
veces seguidas. **No llegó a actuar.** Los registros se escribieron en
`/var/tmp/sillar-qa-m02-20260927-085735/`, fuera del scratchpad.

## Lo que hubo antes, y no se oculta: una puerta roja sobre el mismo SHA

**Esta QA no es la primera corrida sobre `2e6c721`.** Hubo antes otra, en esta misma worktree:

| Corrida | Hora (27/09) | Resultado |
|---|---|---|
| Puerta A | 01:13 → 01:40 | **rc=1. Etapas 1–5 PASS; etapa 6 FAIL: 157 aprobadas, 2 fallidas, 0 omitidas, 0 inestables (159).** Fallaron `catalogo.spec.ts:209` (tras reactivar M01 la aplicación quedó en «Cargando» y `#modulo-catalog` no apareció en 10 s) y `[M02-C32]` (`/admin/contenido/redes-sociales` no pintó en 15 s; la instantánea al fallar sí muestra el armazón pintado). **`[M02-C21]` y `[M02-CICLO]` pasaron** |
| Repetición aislada | → 01:49:34 | `catalogo.spec.ts` y `m02-cierre-evidencia.spec.ts` completos: **17/17**, rc=0 |
| Puerta B | 01:49 → ~02:04 | **Sin resultado.** Claude Code detuvo el proceso en la etapa 6 por memoria del sistema críticamente baja |

**Sus registros originales se perdieron** al reiniciarse la sesión: vivían en el scratchpad, que se
vació. **No se reconstruyen.** Lo único que queda son los SHA-256 que se anotaron antes de perderlos:

| Original perdido | SHA-256 |
|---|---|
| Registro de la puerta A | `8ed567fa1fd8325dc608dc5aeaee4cac619a430d105712673b88c0216e6adfd9` |
| Informe HTML de Playwright de la puerta A | `ec0be5cd797387ac14f278970379060edbfb1a3c7e47e9c74b7ecaa30f7ce9ce` |
| Registro de la barrera de esa tanda | `8a0835c20e8e1a189b62636104005b421987cccd7f99db63a3c45b1e4c5cd11a` |
| Registro de la repetición aislada | `7c4dff01da80ab1fc0a25258d8d4e18f762f3b7ef39f505a5cd6c798eeb6febc` |
| Registro parcial de la puerta B | `d96476a478b7d876c93232772c1224dc34b4e5ca6e9ef5d107e001ee7be9774d` |

Lo que se afirma arriba de la puerta A procede de lo leído en esos originales mientras existían,
recogido en el informe de sesión de B. **No hay archivo que lo respalde hoy**, y así queda dicho.

### Lectura de la puerta A — DEDUCIDA, no demostrada

- **Observado entonces:** en la traza de C32, `/api/setup/status` —que responde en unos 10 ms—
  tardó 2,2–3,0 s; la puerta B murió por falta de memoria, y justo después la máquina tenía unos
  3,5 GiB disponibles, con el navegador de escritorio ocupando varios GiB. **La memoria durante la
  puerta A no se midió.**
- **Observado ahora:** con memoria liberada, las dos pruebas pasan dentro de la suite completa, y
  mucho más rápido: C32 16,5 s (frente a 1,2 min al fallar) y `catalogo:209` 30,7 s (frente a 1,0
  min). La repetición aislada también pasó.
- **Deducido:** los dos fallos son sensibles a tiempos bajo presión de memoria del equipo, no un
  defecto reproducible de `2e6c721`. **No está demostrado**: no se midió la memoria durante la
  puerta A, y un fallo intermitente puede volver. Si reaparece con memoria holgada, es un hallazgo
  nuevo y hay que tratarlo como tal.

## Evidencias de D: comprobadas contra estos resultados

- `FRONTERAS-ANTES-20260927.txt` (`6e01a20`): F1 en `cmsHome.tsx:3`, rc=1. **Coherente** con que
  `2e6c721` corrige ese import.
- `FRONTERAS-DESPUES-20260927.txt`: 151 ficheros, **686** imports, sin violaciones. **Coincide** con
  la barrera de esta QA.
- `PUERTA-ANTES-20260927.txt` (`6e01a20`): muere en la etapa 1. Coherente.
- `PUERTA-DESPUES-20260927.txt` (`94ed702`, entorno cloud): etapas 1–5 PASS, etapa 6 FAIL por
  `NU1301 UntrustedRoot`, y **declara «NO es 6/6»**. No contradice esta QA: es otro entorno y otro
  SHA, y no afirma verde.

## Archivos publicados — copias byte a byte

| Publicado | Original en el equipo de JP | SHA-256 |
|---|---|---|
| `QA-B-FRONTERAS-20260927.txt` | `/var/tmp/sillar-qa-m02-20260927-085735/FRONTERAS.log` | `ebdd9e73da41ebf004943d17061b666590bbaf04d0a8cd725e62e1543745adf1` |
| `QA-B-PUERTA-20260927.txt` | `…/PUERTA-1.log` | `6010b46e0b46ba9545956e425ac7f852b91b86f4d2b2dd5b95feff72ddd0af49` |
| `QA-B-MEMORIA-20260927.tsv` | `…/MEMORIA-1.tsv` | `04a75aecb10e87882a3b1877486e0db96b59bf7995c0d93b8f93fcb775020622` |
| `QA-B-PLAYWRIGHT-REPORT-20260927.json` | Entrada `report.json` del zip embebido en `…/artefactos-1/playwright-report/index.html` | `d7ef18398006ef83033bf13cfc744cee2f7bd73c8f358850e905abaaae9767a2` |
| `QA-B-PLAYWRIGHT-LAST-RUN-20260927.json` | `…/artefactos-1/test-results/.last-run.json` | `91d1c43004802cd49950d78eb11c8fa7d05da8ffffe219a8b13b2f561bc00903` |

El informe HTML completo no se publica (lleva adjuntos: las capturas del ciclo y su
`stdout`/`stderr`). Queda en el equipo de JP con este hash, para volver a extraer el JSON y
compararlo:

```
60631ed24605c036d18e2018b800fcb01eb178c832d25704671971998f249b8d  /var/tmp/sillar-qa-m02-20260927-085735/artefactos-1/playwright-report/index.html
```

Extracción: el HTML lleva un zip en `data:application/zip;base64,…`; `report.json` es una entrada
de ese zip, y sus bytes son los del JSON publicado. En el mismo directorio de `/var/tmp` está
`correr-puerta.sh`, el guion con el que se lanzó la puerta y el vigía.

**Los recuentos de las pruebas del backend (etapa 5) no se publican**: la puerta no los imprime con
verde, y no se reconstruyen.

## Secretos y redacciones

**Ninguna redacción.** Sin coincidencias de `password`, `secret`, `token=` ni `Password=` en los
registros. En el JSON de Playwright solo aparecen en títulos de pruebas («…sin exponer el secreto»,
«…colores solo desde tokens»); los cuerpos de los adjuntos no van en `report.json`. La contraseña de
la base de desarrollo de la worktree se generó al azar en su `.env` y no se imprimió nunca.
