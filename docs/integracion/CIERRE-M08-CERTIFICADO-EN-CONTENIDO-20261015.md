# SILLAR — Cierre M08: certificado en contenido con puerta E2E no concluyente

- **Fecha del cierre documental:** 15 de octubre de 2026 · America/Lima.
- **Estado de M08:** **CERTIFICADO EN CONTENIDO**.
- **Identificador primario certificado:** tree `6de2517370b0e5c257e4926a57260ecd090edaad`.
- **Padre del candidato:** `44e3b1f0fc6b1fcd392f19e493ef4af6af958c31`.
- **HEAD de referencia del colíder:** `7785ad4a55b8bd5ee5217783e8dec7123a17421e` — informativo; la rama de certificación no estaba publicada al redactar este cierre.
- **HEAD de la reproducción local de Chat 2:** `2b6f99297ee5b556b10dcbde104e6e6efa59eaf0` — mismo tree y mismo padre; difiere en metadatos de commit.
- **Carácter:** certificación del contenido; **no autoriza por sí sola integración a `main`**. La integración requiere autorización expresa de JP.
- **Puerta canónica:** no existe marcador 6/6 verde para este tree. Este hecho forma parte del cierre y no se oculta.
- **Repeticiones:** no hay autorización para una tercera canónica **sobre este tree**.

## 1. Qué se certifica

La certificación pertenece al **tree**, no a un SHA de commit que hoy no resuelve en el remoto. El contenido certificado es exactamente:

```text
TREE   6de2517370b0e5c257e4926a57260ecd090edaad
PADRE  44e3b1f0fc6b1fcd392f19e493ef4af6af958c31
```

El tree se reprodujo de forma independiente con distintos metadatos de commit. El HEAD de referencia del colíder (`7785ad4…`) y el HEAD local (`2b6f9929…`) se conservan únicamente para trazabilidad.

La certificación **no equivale** a autorización de merge. Estado de integración al redactar este documento:

```text
INTEGRACION_A_MAIN=PENDIENTE_AUTORIZACION_EXPRESA_JP
PUSH_AUTORIZADO=NO
MERGE_AUTORIZADO=NO
```

## 2. Reproducción durable del tree certificado

Los dos parches que materializan A15 quedan dentro del expediente:

```text
docs/evidencias/m08/A15-completo-sobre-candidata-44e3b1f-f88.patch.b64
SHA-256 del archivo versionado: d904bdc4d5e083064c5380b6535791be3a942b391d51993ccac45068b308fd3e
SHA-256 del parche decodificado: 24c6266a7dd4a813956a28d47131a036150bdbf1285e85ca877817d5fef632bf

docs/evidencias/m08/A15-vocabulario-sobre-f88f296b.patch.b64
SHA-256 del archivo versionado: 45ebc54ecece651747a61cfd6818fd5ce3ec96009e853b2c1d2162e638268e98
SHA-256 del parche decodificado: 84e5a44713a0dfd3fbe821dce8c20fff4c1fc55b172948252815df4a3e9c6df0
```

**Por qué Base64 y por qué se declara binario.** Este repositorio normaliza texto con `* text=auto eol=lf` porque el desarrollo alterna Windows y Arch Linux. Un registro (`.txt`, TRX, logs) puede y debe seguir siendo texto; un parche que se pretende reaplicar no es solo lectura: sus hunks contienen whitespace y finales de línea que deben conservarse byte a byte. Por eso estos dos artefactos reaplicables se archivan como Base64 y `.gitattributes` declara `*.b64 binary`. La robustez no depende de que Base64 casualmente sobreviva a la normalización: queda expresada como política del repositorio.

Receta normativa de **contenido**, en este orden: primero el parche completo sobre `44e3b1f`, que debe producir `f88f296b`; después el parche de vocabulario, que debe producir `6de25173`:

```bash
git switch --detach 44e3b1f0fc6b1fcd392f19e493ef4af6af958c31

base64 -d docs/evidencias/m08/A15-completo-sobre-candidata-44e3b1f-f88.patch.b64 > /tmp/A15-completo-sobre-candidata-44e3b1f-f88.patch
echo "24c6266a7dd4a813956a28d47131a036150bdbf1285e85ca877817d5fef632bf  /tmp/A15-completo-sobre-candidata-44e3b1f-f88.patch" | sha256sum -c -
git apply --check /tmp/A15-completo-sobre-candidata-44e3b1f-f88.patch
git apply /tmp/A15-completo-sobre-candidata-44e3b1f-f88.patch
git add e2e/tests/audit-email-summary.spec.ts frontend/src/modules/core/pages/AuditPage.tsx

test "$(git write-tree)" = "f88f296b6c2a5abb18b36dd3c6730ff8debba0bd"

base64 -d docs/evidencias/m08/A15-vocabulario-sobre-f88f296b.patch.b64 > /tmp/A15-vocabulario-sobre-f88f296b.patch
echo "84e5a44713a0dfd3fbe821dce8c20fff4c1fc55b172948252815df4a3e9c6df0  /tmp/A15-vocabulario-sobre-f88f296b.patch" | sha256sum -c -
git apply --check /tmp/A15-vocabulario-sobre-f88f296b.patch
git apply /tmp/A15-vocabulario-sobre-f88f296b.patch
git add frontend/src/modules/core/pages/AuditPage.tsx

test "$(git write-tree)" = "6de2517370b0e5c257e4926a57260ecd090edaad"
```

Ese último `git write-tree` es la barrera normativa: no depende de autor, committer, fecha ni mensaje de commit.

### Metadatos de la referencia del colíder

El commit de referencia fue declarado con:

```text
HEAD       7785ad4a55b8bd5ee5217783e8dec7123a17421e
AUTHOR     JP-777 <jparedessai@unsa.edu.pe>
COMMITTER  JP-777 <jparedessai@unsa.edu.pe>
AUTHOR_DATE     2026-10-14T11:00:00-05:00
COMMITTER_DATE  2026-10-14T11:00:00-05:00
```

Esos metadatos fijados se conservan dentro del expediente para trazabilidad, no como identificador de certificación. No se afirma aquí una receta byte a byte del objeto commit `7785ad4…` mientras la rama siga sin publicar; la barrera reproducible certificada es el tree. Si la rama se publica, el objeto remoto pasa a ser la fuente canónica del commit. Mientras no se publique, el tree y los dos parches codificados anteriores son la receta reproducible y auditable de contenido. La codificación Base64 evita que un parche que contiene líneas de contexto en blanco introduzca falsos `trailing whitespace` al ser versionado como dato.

## 3. Evidencia de A15

Sobre el tree certificado quedaron acreditadas las barreras específicas de A15:

- etapa 1 completa verde, incluidos los ocho pasos de la puerta y su focal de vocabulario;
- `typecheck` E2E verde;
- focal A15 + transversal: 2 aprobadas, 0 omitidas;
- sonda adversarial: 24 casos, 0 fugas y 0 discrepancias;
- cuatro sabotajes diseñados para romper las barreras: los cuatro rojos;
- A15-01, A15-03 y A15-04 cerrados con mecanismo probado;
- `cellIndex` y barrera anti-barrido-vacío presentes;
- recentralización del literal de vocabulario retirada.

Los cinco rojos de la segunda canónica no tocan `AuditPage.tsx`, `audit-email-summary.spec.ts`, el vocabulario de auditoría ni la privacidad de `email_send`.

## 4. Primera canónica sobre el tree certificado

La primera canónica sobre `6de25173…` recorrió las seis etapas y terminó en la etapa E2E:

```text
197 passed
1 failed
0 skipped
```

Único rojo:

```text
[M04-CICLO] zz-z-m04-ciclo.spec.ts
nada-montado
32 errores de consola net::ERR_INSUFFICIENT_RESOURCES
```

Expediente preservado:

```text
/var/tmp/sillar-m08-a15-tree6de-final-20261014-105138.tar.gz
SHA-256 f93952906b1a2fa388c7b340369f598e271b4cbfb05d3976940ef5fb7adfa48e
```

## 5. Focal M04 autorizada

La misma `zz-z-m04-ciclo.spec.ts`, sobre el mismo tree, pasó aislada:

```text
1 passed
ERR_INSUFFICIENT_RESOURCES=0
nada-montado=0
load1 máximo=19.68 sobre 8 núcleos
MemAvailable mínima=4299172 kB
```

Expediente:

```text
/var/tmp/sillar-m08-m04-focal-autorizada-autorizada-20261014-145717.tar.gz
SHA-256 f785b57ef56c0844b8a8ef3b7c19d00e2844a50c8c83c768b4eec74398710233
```

La focal descarta que M04 falle de forma determinista y descarta la carga CPU alta por sí sola como explicación suficiente.

## 6. Segunda y última canónica autorizada

La segunda canónica terminó antes del apagado posterior de la máquina. Las seis etapas fueron ejecutadas; las etapas 1–5 quedaron verdes y la etapa 6 produjo:

```text
193 passed
5 failed
0 skipped
```

Los cinco rojos fueron:

1. `arnes-diagnostico-arranque.spec.ts` — timeout; `renderizador-sin-respuesta`; contexto/browser cerrado.
2. `catalogo.spec.ts` — la SPA no volvió a montar tras activar B2B; timeout de 180 s.
3. `categorias.spec.ts` — `AxeBuilder.analyze()` agotó el timeout.
4. `categorias.spec.ts` — `arranque-sin-terminar`; indicador «Cargando» persistente.
5. `zz-z-m04-ciclo.spec.ts` — `nada-montado` y un `ERR_INSUFFICIENT_RESOURCES`.

La lectura prefijada antes de correrla fue aplicada sin reinterpretar el resultado:

```text
PREAGREED_READING=NEW_INFORMATION_REQUIRES_COLIDER_DICTAMEN
```

Expediente:

```text
/var/tmp/sillar-m08-a15-tree6de-second-final-canonical-20261014-152625.tar.gz
SHA-256 cc670b69ab9d1a77e1ba1e77a2cc39cf4dc0842774551e83b095f4e45434a401
```

Integridad final de la corrida:

```text
VERIFY_DB_CLEAN=YES
E2E_CONTAINERS_CLEAN=YES
GIT_CLEAN=YES
INSTRUMENT_STABLE=YES
NO_PUSH=YES
NO_MERGE_MAIN=YES
```

## 7. §27 se disparó: M02 no se cierra como «entorno»

La segunda canónica volvió a fallar en la prueba cuyo nombre estable es:

```text
Con M01 desactivado no queda entrada de menú, ni ruta viva, ni hueco en el inicio
```

`docs/PENDIENTES.md` §27 la había identificado históricamente como `catalogo.spec.ts:209`. La medición posterior del colíder comprobó:

```text
origin/main                          línea 210
origin/qa/m08-repair-6de6-20261012  línea 210
§27 sobre 2e6c7215                  línea 209
```

Por tanto, **los trece commits de reparación de M08 no desplazaron la prueba**. La línea cambió antes, entre la redacción de §27 y el `main` actual. El disparador se cumplió porque es la misma prueba —y la única de ese archivo con `test.setTimeout(180_000)`—, no porque coincida un número de línea.

Desde este cierre, §27 usa el **nombre de prueba** como identificador durable. El fallo no se atribuye automáticamente al entorno: queda pendiente un diagnóstico reproducible propio de esa prueba.

## 8. Dos episodios medidos durante la segunda canónica

### Episodio A — capacidad agotada durante etapa 6

La monitorización registró:

```text
MemAvailable mínimo global  111304 kB
swap                         0
```

Correlación disponible para tres rojos:

```text
F2 catálogo      ~377756 kB disponibles
F3 categorías    ~126256 kB disponibles
F4 categorías    ~936792 kB disponibles
```

La capacidad agotada explica que la etapa 6 produzca timeouts y procesos inestables, pero §27 prohíbe usar esa observación para cerrar la prueba de catálogo como «ambiental». Una corrida posterior verde tampoco probaría ausencia de defecto intermitente.

### Episodio B — acumulación de descriptores en un renderer

A las `16:55:09-05:00`, dentro de la ventana temporal de M04-CICLO, se midió:

```text
renderer Chromium FD     4128
FD Chromium totales      4273
MemAvailable             7486476 kB
load1                    6.29
```

M04 terminó después con `nada-montado` y un `ERR_INSUFFICIENT_RESOURCES`. La focal aislada había sido verde. Esto es evidencia de una acumulación anómala de recursos por proceso, pero **no demuestra todavía que el límite sea 4096**. Infra QA debe medir `Max open files` en `/proc/<pid>/limits` durante una reproducción controlada antes de convertir esa hipótesis en hecho.

## 9. Dictamen de contenido

El colíder certifica:

> **M08 — CERTIFICADO EN CONTENIDO** sobre el tree
> `6de2517370b0e5c257e4926a57260ecd090edaad`, obtenible aplicando los parches de este expediente sobre
> `44e3b1f0fc6b1fcd392f19e493ef4af6af958c31`.

No certifica un 6/6 que no ocurrió. Tampoco rechaza el contenido: nada de los rojos medidos apunta al delta A15/M08.

Este dictamen **no autoriza integración**. JP conserva la autorización exclusiva de cualquier merge/push a `main`.

## 10. Infra QA — pendientes transversales

Dueño operativo: **Infra QA**. Integración conserva la evidencia y escala decisiones de política por la jerarquía normal.

1. Habilitar swap en el equipo de QA y fijar qué puede correr en paralelo durante la etapa 6.
2. Implementar el vigía de memoria ya propuesto por §27: umbral acordado, parada ordenada y preservación de artefactos.
3. Medir el límite real de descriptores (`Max open files`) de los procesos renderer Chromium.
4. Evitar que las specs de ciclo hereden el navegador de las ~190 pruebas anteriores; la implementación concreta corresponde al dueño del arnés.
5. Ejecutar el diagnóstico reproducible propio de la prueba `Con M01 desactivado no queda entrada de menú, ni ruta viva, ni hueco en el inicio` exigido por §27; no es otra canónica.
6. Inventariar `ERR_INSUFFICIENT_RESOURCES` como señal **probable de recursos**, sin convertirla en prueba de entorno: también puede existir un bucle de peticiones.
7. Alinear los patrones de saturación del verificador con los estados actuales del diagnóstico de arranque, incluido `nada-montado`.
8. Guardar la URL/recurso asociado en el collector de errores de consola; hoy el fixture conserva el mensaje pero pierde el discriminador que habría permitido cerrar el análisis.
9. Conservar en el resumen de la puerta las cifras de carga, FD y el `N passed` de etapa 6, en lugar de calcularlas o producirlas y perderlas.
10. Incorporar `e2e/unit/` a la cobertura de la puerta o fijar expresamente otra barrera canónica: `e2e/package.json` declara `test:unit`, hoy con 8 pruebas verdes en la comprobación independiente, pero ninguna de las seis etapas de `scripts/verificar.mjs` lo invoca.

La propuesta del colíder de tratar la fiabilidad transversal de la puerta **antes de abrir M10/M11/M12** se registra como decisión pendiente de JP; este documento no la convierte silenciosamente en autorización ni en bloqueo ya decidido.

## 11. Reglas posteriores a este cierre

- No ejecutar una tercera canónica **sobre `6de25173…`** sin una autorización nueva y expresa.
- No cambiar `workers`, `retries`, partir la suite ni silenciar aserciones para fabricar un verde retrospectivo.
- No llamar «CERTIFICADO» a secas a este cierre: la fórmula durable es **CERTIFICADO EN CONTENIDO**.
- No registrar `7785ad4…` como identidad primaria mientras no resuelva en el remoto.
- Si JP autoriza publicar la rama de certificación, esa publicación mejora la trazabilidad del HEAD pero **no cambia el tree certificado**.
- Si JP autoriza integrar, el commit/PR de integración debe incluir este dictamen y la actualización de §27; la autorización de integración debe quedar separada de la recomendación del colíder.
