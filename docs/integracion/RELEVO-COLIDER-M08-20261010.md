# SILLAR — Expediente cronológico M08 para revisión del colíder

**Acta inicial:** SILLAR-COLIDER-M08-20261010-01

**Corte inicial:** sábado 10 de octubre de 2026, 00:17 (America/Lima, UTC−05:00)

**Actualización 02:** sábado 10 de octubre de 2026, después de ensayo verde–rojo–verde; hora exacta no asentada en el log compartido.

**Responsables de preparación:** JP (autoridad de producto y de integración) y Chat 2 (coordinación técnica)

**Estado:** **REVISIÓN INTERNA / QA FOCAL VERDE. QA INDEPENDIENTE PENDIENTE. HOLD MERGE A `main`.**
**Motivo del relevo diferido:** colíder temporalmente no disponible. No se sustituye ni se simula su dictamen.

## I. Referencias exactas y bases

- Repositorio: `JP-777/SILLAR`.
- `main` contrastado en GitHub al preparar esta acta: `4a3fd1ef12fc2226bad185f003acea9168b9a4c9`.
- **Candidata contractual**: rama `integration/m08-contratos-m05b-m06-20261009`, commit `963e0f1d12b131c4296cd836c88c3334fc22288e`, 11 archivos, 400 inserciones; publicada el **09/10/2026 23:36:09 (Lima)**. Enlace: https://github.com/JP-777/SILLAR/commit/963e0f1d12b131c4296cd836c88c3334fc22288e
- **Documentación de M08**: rama `docs/m08-paso1-propuesta-20261009`, commit `5e3154ff23049a7d64fc798f347e47eaed77c543`, 3 archivos, 428 inserciones; publicada el **09/10/2026 23:36:21 (Lima)**. Enlace: https://github.com/JP-777/SILLAR/commit/5e3154ff23049a7d64fc798f347e47eaed77c543
- Ambos commits tienen como único padre `4a3fd1ef12fc2226bad185f003acea9168b9a4c9`; ambas ramas estaban 1 commit adelante y 0 atrás de esa base. Ninguna se fusionó a `main`.
- Artefacto local anterior (no sustituye al commit): `/var/tmp/sillar-m08-revision-colider.tar.gz`, SHA-256 `5c1593d491b430fbe0bf14fdcb132562c5c43bcbd7c05557badb02f21e1cd51b`, presente solamente en la máquina de JP.

## II. Cronología verificada y antecedentes

| Fecha (Lima) | Evento | Evidencia disponible | Interpretación y límite |
|---|---|---|---|
| 09/10/2026, hora exacta no asentada | JP ratificó **D1–D6** de M08; Chat 2 preparó `SPEC`, matriz y plan de implementación. | Commit documental `5e3154f` (publicado después). | Se autorizó diseñar las costuras, **no** fusionar a `main` ni desplegar rutas de Portal. |
| 09/10/2026, hora exacta no asentada | Se prepararon nuevas interfaces, implementaciones, registros DI y pruebas para M05b y M06. | Commit contractual `963e0f1`, 11 archivos. | Código revisable, sin tablas ni migraciones nuevas de M08. |
| 09/10/2026, antes de publicación | Worktree QA aislada `sillar-m08-contracts-qa`, offset 65, PostgreSQL 16 en puerto 55665, proyecto Docker `sillar_m08_contracts_qa`. | Salida de JP; PG saludable. | Instancia aislada, **no** certificación de todos los entornos. |
| 09/10/2026, antes de publicación | Pruebas focales PostgreSQL M05b: **2/2 PASS**, sin skips; M06 unitarias focales: **2/2 PASS**. | `/var/tmp/sillar-m08-contratos-focal/M08-M05B-POSTGRES-QA65-RECUPERACION.trx`; registros compartidos por JP. | Confirmación por logs aportados por JP; colíder puede reproducir sobre SHA publicado. |
| 09/10/2026, antes de publicación | Compilación de `backend/Sillar.sln`: **RC=0**, 0 warnings, 0 errors. | `/var/tmp/sillar-m08-build.log`, salida de JP. | Incluye proyecto `Tracking.Contracts` registrado en `.sln`. |
| 09/10/2026, antes de publicación | Regresiones completas: **47/47** M05b y **35/35** M06, sin skips. | `/var/tmp/sillar-m08-regresiones-v3/M05B.trx` y `M06.trx`. | **82/82 focales**; no equivale a la puerta canónica completa. |
| 09/10/2026 aprox. 23:15 (según nombre UTC de evidencia) | QA negativa: control original 1/1 PASS; sabotaje de propiedad 1/1 FAIL esperado; sabotaje de campo interno 1/1 FAIL esperado. | `/var/tmp/sillar-m08-qa-negativa-20261010T041551Z-74017/`, tres TRX. | Se acreditó detección de ambos defectos por la prueba. |
| 09/10/2026, posterior a pruebas | El primer script de sabotaje terminó HOLD por una **barrera de restauración incorrecta**: comparaba toda la worktree temporal contra el commit base aunque contenía deliberadamente un patch válido. Auditor posterior confirmó copias restauradas y ninguna worktree temporal registrada. | Auditoría QA negativa compartida por JP; `QA NEGATIVA ACREDITADA`. | Error del arnés, no evidencia de fallo del contrato. **VERDE→ROJO→VERDE completo fue reproducido en corrida posterior del 10/10/2026** (ver registro y evidencia nuevos). |
| 09/10/2026, antes de las 23:36 | Colíder declaró **OBSERVADO por imposibilidad de acceso**: no existía SHA contractual remoto y no podía acceder a `/var/tmp` local de JP. | Dictamen del colíder comunicado por JP. | Observación procedente entonces; se subsanó publicando las dos ramas, **sin dictamen sustantivo posterior**. |
| **09/10/2026 23:36:09** | Commit contractual `963e0f1` creado/publicado en GitHub. | URL del commit, metadatos de GitHub y referencia remota. | Objeto ahora auditable de manera independiente. |
| **09/10/2026 23:36:21** | Commit documental `5e3154f` creado/publicado en GitHub. | URL del commit, metadatos de GitHub y referencia remota. | D1–D6 disponibles al colíder por SHA. |
| **10/10/2026 00:17** | JP ordena proseguir M08 sin colíder, dejando acta fechada de todo punto que requiera su intervención. | Esta acta inicial. | **No** levantar el requisito de QA independiente ni el HOLD de merge. |
| **10/10/2026, hora no registrada** | Intento de publicar el acta inicial en rama documental; `git diff --cached --check` detectó espacios finales en cuatro líneas y abortó antes del commit. | Salida terminal de JP: `trailing whitespace` en líneas 3–6. | No se creó commit de esta acta. El primer intento **sí dejó el archivo añadido al índice** de la worktree documental; requiere publicación de versión corregida sin borrar cambios ajenos. |
| **10/10/2026, hora no registrada** | Reproducción negativa **verde → rojo → verde** completa en worktree efímera desde commit contractual `963e0f1`. | `/var/tmp/sillar-m08-vrv-20261010-lmsjMoLj/`, cinco archivos TRX. | Control 1/1 PASS; sabotaje de propiedad 0/1 PASS, 1 FAIL esperado; restauración 1/1 PASS; sabotaje de campo interno 1 FAIL esperado; segunda restauración 1/1 PASS; cero skips en todas. Integridad final informada PASS, sin commit/push/merge. |
| **10/10/2026, hora no registrada** | JP fija el **modo automático para sábados y domingos** con acceso intermitente, Claude Code sin tokens y Codex disponible, mientras JP coordina tareas manuales con Chat 3. | Instrucción expresa de JP en conversación. | Ejecutar encargos autónomos de duración amplia únicamente con límites y evidencias; no presuponer herramientas/agents disponibles fuera de su sesión; no ejecutar merges automáticos. |

> Las fechas y horas exactas de los commits proceden de sus metadatos remotos. Las demás horas no se inventan: se anotan solo cuando el registro permite fundamentarlas. Los log/TRX del servidor son rutas locales reportadas, no ficheros publicados automáticamente en GitHub.

## III. Decisiones ratificadas D1–D6 (no reabrir por inferencia)

| ID | Regla de producto |
|---|---|
| D1 | M08 reutiliza cuenta, sesión y perfil de **M04**. Sin tablas, esquema ni migraciones de Portal v1. |
| D2 | Solo órdenes M05b con `customer_id == ICurrentCustomer.CustomerId` autenticado. `NULL` invisible; no vincular por correo, teléfono, nombre ni código en v1. |
| D3 | Avance consultable por contrato mínimo de **M06**, con estado/fecha públicos y sin notas, prioridad, fechas internas ni personal. |
| D4 | Si M06 no está disponible, Portal explica la ausencia del seguimiento; no inventa datos ni se cae por un proveedor blando. |
| D5 | Lista y detalle de ventas **M03** y órdenes de servicio propias (M05b por fachada M06). |
| D6 | Presentar código público, estado comprensible en español, fechas y compromiso público reales y descripción pública; nunca GUID, datos privados o del personal. |

## IV. Revisión técnica preliminar de Chat 2 (no independiente)

1. `CustomerServiceOrderReader` de M05b usa `Where(order => order.CustomerId == customerId)` **dentro de la consulta EF traducida a PostgreSQL** para lista; detalle añade `order.VisibleCode == visibleCode` en la misma consulta, antes de `SingleOrDefaultAsync`.
2. `Guid.Empty` o código vacío producen resultado no autorizado sin buscar otros clientes; una orden con `customer_id NULL` no cumple igualdad con un GUID válido. Las pruebas reales crean clientes A/B con correo compartido y una orden manual.
3. `ICustomerServiceOrderReader` y `ICustomerTrackingProgress` exponen DTOs acotados, sin `ReceivedNotes`, `TrackingNotes`, `BoardPriority`, `InternalDueAt`, personal ni GUID. `CustomerTrackingProgressService` no usa `TrackingDbContext`: revalida vía M05b en cada detalle.
4. `ServiceOrdersModule` y `TrackingModule` registran interfaces en DI; `Tracking.Contracts` está añadido a `Sillar.sln`, la solución compila. `Tracking` tiene dependencia dura de `service_orders`.
5. `ModuleBootstrapper` registra servicios y rutas solo de módulos activos. **Aún no hay M08** para probar composición con M03/M06 deshabilitados.
6. Las únicas rutas actuales de M06 son administrativas (`/api/admin/tracking`, autorización de personal); **los contratos nuevos no crean rutas públicas**. La futura API de M08 debe exigir política `crm:customer` y extraer la identidad de `ICurrentCustomer` de la petición autenticada, no del usuario HTTP.
7. Documentos M08 describen la base anterior a los contratos como una fotografía histórica. Es necesario actualizar su **estado vigente** para que el agente A no los trate como inexistentes una vez disponibles en rama/`main`.

## V. Lista de auditoría diferida del colíder

| ID | Qué debe comprobar de manera independiente | Evidencia requerida / condición de cierre | Estado |
|---|---|---|---|
| C-01 | SHA remoto, padre, diff completo, rama y consistencia con D1–D6. | Reproducir checkout SHA `963e0f1` y contrastar documentos SHA `5e3154f`. | PENDIENTE |
| C-02 | Filtro de propiedad en SQL en lista **y** detalle, sin filtro posterior en memoria. | Inspección de consulta/SQL real y pruebas A/B/código ajeno. | PENDIENTE |
| C-03 | Órdenes manuales `CustomerId=NULL`, coincidencias engañosas de correo/nombre y `Guid.Empty`. | Datos reales en PG, retorno no autorizado. | PENDIENTE |
| C-04 | Ausencia estructural de notas, prioridad, vencimientos internos, personal e IDs en DTO público de ambos contratos. | Inspección de firmas, serialización y test deliberado de campo prohibido. | PENDIENTE |
| C-05 | Sabotajes **VERDE→ROJO→VERDE** de propiedad y privacidad, con código y SHA restaurados, `skipped=0`. | Reproducción interna completada el 10/10; el colíder debe repetirla o auditarla por sí mismo. | **QA INTERNA PASS; REVISIÓN INDEPENDIENTE PENDIENTE** |
| C-06 | DI, solución, fronteras, ausencia de SQL directo a schemas ajenos y comportamiento del host con módulos activos/inactivos. | Compilación y pruebas de integración/ciclo donde aplique. | PENDIENTE |
| C-07 | Endpoint HTTP aún inexistente: M08 deberá autenticar con M04, no aceptar `customerId` externo, devolver 404 ante código ajeno y distinguir proveedor ausente de lista vacía. | **Condición de implementación futura**; no atribuirla a estos contratos. | DIFERIDO A M08 |
| C-08 | Documentación: distinguir base histórica de commits publicados y futuros artefactos integrados en `main`. | Documentación actualizada, SHA propio y rastreo de cambios. | PENDIENTE |
| C-09 | Puerta canónica `node scripts/verificar.mjs` sobre SHA exacto y ciclo pertinente, PG real, sin skips, con evidencias publicadas. | 6/6 etapas que correspondan, incl. etapa 6; veredicto de QA independiente. | PENDIENTE |

## VI. Política de avance sin colíder

- JP puede autorizar trabajo en **ramas aisladas**, publicación de ramas y documentación. **Merge a `main` exige aprobación expresa de JP**; mientras no haya QA independiente, mantener `HOLD MAIN`.
- Chat 2 realiza revisión técnica preliminar y reporta objeciones con severidad y reproducibilidad, **sin emitir dictamen como colíder**.
- Cada corte donde sería necesaria intervención externa produce una entrada fechada en este expediente y se continúa solo con tareas que no dependan del dictamen. Si una decisión de arquitectura/producto excede D1–D6, elevar a JP y esperar resolución antes de implementarla.
- No ejecutar pruebas destructivas fuera de instancia efímera identificada; `skipped=0`; un ensayo rojo no certifica; no mover archivos compartidos entre agentes simultáneamente.
- Identificar en cada entrada: **fecha y hora Lima; acción; objetivo; rama/SHA antes y después; pruebas con resultado/archivos TRX; hallazgos; decisiones JP; revisión diferida; próximo paso**.

## VII. Plan inmediato M08

1. **HECHO 10/10:** QA negativa verde → rojo → verde en copia descartable de `963e0f1`; 5 TRX, sin omisiones, sin tocar candidata ni `main`.
2. **EN CURSO:** contrastar cobertura, estructura DTO, lectura SQL, registro DI y escenarios no cubiertos (por ejemplo, detalle ajeno y retorno indistinguible). Corregir únicamente defectos verificables y crear nuevo commit/QA si cambia código.
3. **EN CURSO:** corregir este acta y publicarla en rama documental `docs/m08-paso1-propuesta-20261009`, indicando SHA resultante. Después preparar actualización explícita de SPEC/matriz/plan: en `main` base siguen ausentes los nuevos contratos, pero **sí están publicados en rama contractual SHA `963e0f1`**. No borrar fotografía histórica.
4. **PENDIENTE:** preparar y, con la autorización de JP, implementar M08 en rama propia con pruebas de autenticación M04, objeto ajeno 404, proveedor ausente, estados públicos y cero DDL. Las costuras compartidas quedan bajo Chat 2.
5. **DIFERIDO:** puerta canónica, reproducción independiente y dictamen del colíder; prohibido marcar cierre de esa barrera por QA interna.

## VIII. Protocolo de «trabajo automático» recuperado de «Auditoría General SILLAR»

- **JP interactúa lo mínimo:** Chat 2 prepara encargos largos, claros y con destinatario; JP principalmente los reenvía. Escalar a JP solo decisiones urgentes, de producto/arquitectura o riesgos mayores.
- **Paralelismo por territorios, no por archivos compartidos:** agentes con encargos independientes pueden trabajar simultáneamente; costuras de integración (especialmente navegación) se serializan.
- **No interrumpir a un agente ya ocupado** salvo riesgo imprescindible; un encargo por agente y por territorio.
- **Evidencias:** base/SHA exactos, PG real donde aplique, `skipped=0`, pruebas legal/ilegal/sabotaje con restitución; una puerta roja o interrumpida no certifica.
- **Al terminar jornada:** auditoría nocturna de SHAs, ancestros, `main`, migraciones/FK, contratos, QA y evidencias, divergencias documentales, ramas, riesgos y orden siguiente; agentes en HOLD durante la auditoría.
- **Disponibilidad declarada para la jornada:** Claude Codes sin tokens; Codex disponible. JP participa manualmente con Chat 3, en paralelo y en territorio separado. Chat 2 coordina la integración y este expediente. Ningún agente certifica independientemente su propio código.
- **Calendario confirmado por JP:** modo automático todos los **sábados y domingos**. Es un método de trabajo para el acceso intermitente de JP, no una autorización para ejecutarse en segundo plano ni para omitir QA.
- **Reglas de delegación este fin de semana:** Codex recibe encargos técnicos completos con rama/territorio/contrato/barreras/entregables y cero merges; JP y Chat 3 pueden trabajar manualmente en otro territorio sin tocar archivos de Codex. Todo cambio de costura común y cualquier riesgo de producto se escala a JP/Chat 2. Una tarea no necesita intervención continua si el alcance ya fue ratificado; una decisión nueva sí.
- **Secuencia estratégica ratificada:** cerrar cuanto sea posible de M08 primero; **después** planificar módulos nuevos; **al final** distribuir la jornada automática y sus encargos por agente disponible. No iniciar trabajos ajenos en paralelo que compitan con M08.

## IX. Formato obligatorio para próximas entradas del expediente

```text
### REGISTRO M08-[NN] — [AAAA-MM-DD HH:MM America/Lima]
- Responsable, territorio y alcance:
- Base, rama y SHA inicial:
- Cambios realizados; nuevo SHA / hash de patch:
- Pruebas reales: comandos, PASS/FAIL/SKIP y ubicación de TRX/log:
- Riesgos y hallazgos nuevos (severidad):
- Decisiones ratificadas de JP / propuestas aún no autorizadas:
- Qué debe revisar el colíder y por qué se difiere:
- ¿HOLD de main? Sí, hasta nueva orden expresa.
- Próxima tarea que sí puede continuar sin el colíder:
```

## X. REGISTRO M08-02 — sábado 10/10/2026 (hora exacta no consignada)

**Objeto:** cierre de la prueba verde–rojo–verde y recuperación de la publicación del acta inicial.

- **Responsables:** JP ejecuta pruebas locales en QA65; Chat 2 redacta el expediente. Colíder ausente.
- **Base y SHA bajo prueba:** `963e0f1d12b131c4296cd836c88c3334fc22288e` (contratos en remoto). Documentos en `5e3154ff23049a7d64fc798f347e47eaed77c543` antes del acta.
- **Prueba ejecutada:** script `SILLAR_M08_VERDE_ROJO_VERDE_20261010.sh`, en worktree efímera; PostgreSQL QA65 validado; candidato original limpio.
- **Secuencia observada:** `control-verde.trx` 1 PASS; `sabotaje-propiedad.trx` 1 FAIL deliberado y 0 omitidas; `restaurado-propiedad-verde.trx` 1 PASS; `sabotaje-campo.trx` 1 FAIL deliberado; `restaurado-campo-verde.trx` 1 PASS. El script imprimió `VERDE → ROJO → VERDE (PROPIEDAD Y PRIVACIDAD): PASS`.
- **Ubicación de evidencia en la máquina JP:** `/var/tmp/sillar-m08-vrv-20261010-lmsjMoLj`. No afirmar que esos TRX existan en GitHub; el colíder deberá recibirlos o reproducirlos desde el SHA.
- **Integridad:** script informó `SIN COMMIT, PUSH NI MERGE`; no se registra nuevo commit contractual.
- **Incidencia documental:** ejecución de `SILLAR_REGISTRAR_RELEVO_20261010.sh` abortó por cuatro líneas con `trailing whitespace` al correr `git diff --cached --check`. El fichero del acta original quedó staged en rama documental; no hubo commit ni push del acta.
- **Acción de corrección:** reemplazar **solo el acta staged** por esta versión sin espacios finales, validar `git diff --cached --check`, commitear y publicar en la misma rama documental. Registrar SHA después de que JP confirme publicación remota.
- **Riesgos abiertos:** autenticación HTTP de M08 aún no desarrollada; degradación M06 inactivo pendiente de probar; ausencia de una revisión independiente; documentación funcional con referencias a la base histórica antes de integrar contratos.
- **Lo que queda para colíder:** ratificar/reproducir los cinco estados de prueba, inspeccionar SQL y DTO, validar fronteras/DI/host y emitir dictamen sobre SHA contractual y SHA documental actualizados.
- **HOLD:** `main` sin merge; QA canónica e independiente pendientes.
- **Próxima tarea sin colíder:** publicar este expediente y revisar las referencias documentales desactualizadas de M08, sin alterar la candidata contractual.

**Advertencia de interpretación:** este expediente registra hallazgos preliminares y solicitudes de revisión; **no contiene un dictamen APROBADO del colíder ni una certificación canónica M08**.
