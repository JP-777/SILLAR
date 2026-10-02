# Evidencias de QA — barrera de fronteras del frontend (PR #2)

Creado: 27/09/2026, America/Lima · Publicado por: frente B (Claude Code B), en su papel de QA
independiente, con autorización de JP.

| | SHA |
|---|---|
| **Código probado** | `e2760f81d746c5a52230c616a04c297200676da9` |
| **Commit documental** (este índice y los archivos de esta carpeta) | El commit que añade esta carpeta, hijo directo de `e2760f8`. No cambia código: la QA no se repitió sobre él |

**Equipo:** el equipo local de JP (Linux `archlinux`, Docker 29.5.2), worktree aislada
`/home/JP777/sillar-qa-fronteras`, identidad derivada con offset 22.

## Cómo se sabe que los registros son de `e2760f8`

El reflog de la worktree de QA registra un solo movimiento a ese commit, y ninguno después:

```
e2760f8 HEAD@{2026-09-26 23:27:25 -0500}: checkout: moving from 345dd88c9bf2c08d50cd896727ba2ae1aa6c00d0 to e2760f81d746c5a52230c616a04c297200676da9
```

La worktree seguía en `e2760f8` y limpia (0 cambios) después de la puerta y al preparar esta
publicación. Los tres registros se escribieron **después** de ese checkout (horas abajo). Las
provocaciones se hicieron sobre una copia de `src/`, `scripts/` y `tests/`, y esa copia sigue
siendo idéntica al árbol de `e2760f8` (`diff -r`, sin diferencias, comprobado al publicar).

## Resultados

| Comprobación | Resultado | Archivo |
|---|---|---|
| `pnpm --dir frontend test:fronteras` | Árbol real: 151 ficheros, 685 imports, sin violaciones · **34/34**, 0 fallos, 0 omitidas | `QA-FRONTERAS-TEST-20260926.txt` |
| Provocaciones y formas legítimas | **38 casos**, todos con el resultado esperado · **4 mutaciones**, las 4 en rojo | `QA-PROVOCACIONES-20260926.txt` |
| `node scripts/verificar.mjs` | **6/6 PASS**, código de salida 0 | `QA-PUERTA-20260926.txt` |
| Suite e2e (etapa 6) | **147/147** esperadas, 0 inesperadas, 0 inestables, **0 omitidas** | `PLAYWRIGHT-REPORT-20260926.json` |

Etapas de la puerta, todas **PASS**: 1 tipos del frontend (incluye `test:fronteras`) · 2 tipos del
arnés e2e · 3 compilación del backend · 4 migraciones backend (BD efímera) · 5 pruebas del
backend · 6 suite e2e.

## Horarios verificables (America/Lima, UTC−5)

| Qué | Hora | De dónde sale |
|---|---|---|
| Checkout de `e2760f8` | 2026-09-26 23:27:25 | Reflog, arriba |
| `test:fronteras` terminado | 2026-09-26 23:27:41 | Fecha de modificación del original |
| Provocaciones terminadas | 2026-09-26 23:29:16 | Fecha de modificación del original |
| Puerta: inicio | 2026-09-26 23:29:30 | Primera línea del registro (`date` antes de lanzar) |
| Suite e2e: inicio | 2026-09-26 23:32:40 | `startTime` del informe de Playwright (`1790483560142`) |
| Suite e2e: duración | 21,3 min | `duration` del informe (`1277653.216` ms) |
| Puerta: fin | 2026-09-26 23:53:58 | Última línea del registro, y fecha del original |

## Archivos publicados, originales y hashes

**Los archivos de esta carpeta son copias byte a byte**, salvo el `.tsv`, que es derivado. Por eso
el SHA-256 de cada copia es el mismo que el del original.

| Publicado | Original en el equipo de JP | SHA-256 |
|---|---|---|
| `QA-FRONTERAS-TEST-20260926.txt` | `/tmp/claude-1000/-home-JP777-SILLAR/ae53168d-09b0-4948-b5f3-bd8f3bc41db8/scratchpad/qa2/fronteras.log` | `e5c428ff85f0f415ad5388c0b8c7de8fc8d104fa611bc94e010944ae1fc45533` |
| `QA-PROVOCACIONES-20260926.txt` | `…/scratchpad/qa2/provocaciones.log` | `f60184fcd51669c301cb42ba4c23b845b6131953203f86f79ad33c7e95164d2e` |
| `QA-PUERTA-20260926.txt` | `…/scratchpad/qa2/puerta.log` | `5f347d05a3f0adea76900c8fa98debf5757c6a42d2a34936cef47fef551b488d` |
| `PLAYWRIGHT-LAST-RUN-20260926.json` | `/home/JP777/sillar-qa-fronteras/e2e/test-results/.last-run.json` | `91d1c43004802cd49950d78eb11c8fa7d05da8ffffe219a8b13b2f561bc00903` |
| `PLAYWRIGHT-REPORT-20260926.json` | Entrada `report.json` del zip embebido en `/home/JP777/sillar-qa-fronteras/e2e/playwright-report/index.html` | `fde84a6427c02c7915f0cce43d46d0d4fce2f91dfb3a8f5e0fc59e5b0f2d4350` |
| `PLAYWRIGHT-PRUEBAS-20260926.tsv` | **Derivado** del anterior: una fila por prueba | `bc038f6a1b833c310c50f1336f66393929745754e503c91b03e5997055f73d32` |

**El informe HTML no se publica** (865 127 bytes, con los adjuntos dentro). Queda en el equipo
de JP con este hash, para que el JSON publicado se pueda volver a extraer y comparar:

```
9417d4ffee51545e46c01ee6d0f95bd5cf93b5a2e9235aa2edc242e2cc8f4ab1  e2e/playwright-report/index.html
```

**Cómo se extrae el JSON del HTML**, para repetirlo: el HTML lleva un zip en
`data:application/zip;base64,…`; se decodifica, y `report.json` es una entrada de ese zip. Sus
bytes son exactamente los de `PLAYWRIGHT-REPORT-20260926.json`. El `.tsv` sale de su campo
`files[].tests[]`: `location.file`, `location.line`, `projectName`, `outcome`, `duration`, `title`.

## Qué no contienen los registros, dicho para no reconstruirlo

- **`QA-PROVOCACIONES`**: las cabeceras de sección que se veían en pantalla («ILEGALES»,
  «LEGÍTIMAS», «FUERA DE ALCANCE DECLARADO»), la corrida de control sano posterior a las
  provocaciones y la ejecución de las autopruebas tras restaurar el script **se imprimieron solo en
  pantalla y no están en el archivo**. No se añaden aquí. Lo que sí está: las 38 líneas de casos
  (1–36 en el orden de ejecución, más las dos sondas de glob con llaves) y las 4 mutaciones.
- **`QA-PUERTA`**: con todo verde la puerta no imprime el detalle de cada paso; el registro trae las
  cabeceras de etapa y el veredicto. El detalle de la etapa 6 está en el informe de Playwright.
- **Las pruebas del backend (etapa 5) no tienen recuento propio en esta carpeta**: la puerta no lo
  imprime con verde, y no se ha reconstruido.

## Secretos y redacciones

**Ninguna redacción.** Ninguno de los registros contiene contraseñas, tokens ni cadenas de conexión
(búsqueda de `password`, `token`, `secret`, `Password=` sin coincidencias en los tres `.log`). En
`report.json` las únicas coincidencias son títulos de pruebas («…sin exponer el secreto», «la
cookie de cliente…», `sesion-csrf.spec.ts`); los cuerpos de sus 4 adjuntos (`stdout`/`stderr`) no
van en `report.json` sino en otras entradas del zip, que **no se publican**. El nombre de la base
efímera de `QA-PUERTA` (`sillar_verify_…`) no es un secreto.

La contraseña de la base de desarrollo de la worktree de QA se generó al azar en su `.env`, nunca
se imprimió y no aparece en ningún archivo de esta carpeta.
