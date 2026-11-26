# M12 — Consolidación candidata de FASE A

24/10/2026. Base examinada: `f733a57c25b4592336fc5eaf37c1b7d207e4c569`.
Referencia externa: SPEC candidata v2 del ZIP entregado por Chat 3. Este inventario
no la convierte en SPEC canónica, no aprueba contratos físicos y no cierra M12.

**E:** implementada y demostrada en el alcance focal local indicado.
**P:** parcialmente implementada; no equivale al criterio productivo completo.
**A:** ausente, desarrollable íntegramente en FASE A.
**B:** bloqueada por autorización, integración o infraestructura.
Las referencias a pruebas existentes son evidencia previamente aceptada, no nuevas corridas.

| AC-M12 | Estado | Capacidad / pendiente concreto | Fuente local |
|---|---|---|---|
| 01 | B | Denegación de consulta preparada; falta ciclo completo de activación, licencia, UI y desmontaje. | [AssistantFaqEndpointTests.cs:88](../../../backend/Sillar.Modules.Assistant.Tests/AssistantFaqEndpointTests.cs#L88) |
| 02 | P | FAQ vacía se abstiene; instalación productiva vacía no acreditada en este frente. | [EditorialYFaqTests.cs:91](../../../backend/Sillar.Modules.Assistant.Tests/EditorialYFaqTests.cs#L91) |
| 03 | E | Borrador no público; conservar publicación anterior al editar, demostrado en memoria. | [RepositorioEditorialLocalTests.cs:40](../../../backend/Sillar.Modules.Assistant.Tests/RepositorioEditorialLocalTests.cs#L40) |
| 04 | E | Editor no publica; rechazo y publicación previa protegidos en dominio/manejadores aislados. | [EditorialYFaqTests.cs:22](../../../backend/Sillar.Modules.Assistant.Tests/EditorialYFaqTests.cs#L22), [AdministracionEditorialHttpTests.cs:122](../../../backend/Sillar.Modules.Assistant.Tests/AdministracionEditorialHttpTests.cs#L122) |
| 05 | P | Publicación y cita verificadas localmente; atomicidad física de sustitución pendiente. | [RepositorioEditorialLocalTests.cs:27](../../../backend/Sillar.Modules.Assistant.Tests/RepositorioEditorialLocalTests.cs#L27), [EfPublishedFaqReader.cs:32](../../../backend/Sillar.Modules.Assistant/Services/EfPublishedFaqReader.cs#L32) |
| 06 | P | Retirada local probada; corregida la revalidación posterior a política URL. Revocación física/replicada pendiente. | [RevalidacionFinalFaqTests.cs:33](../../../backend/Sillar.Modules.Assistant.Tests/RevalidacionFinalFaqTests.cs#L33), [FaqService.cs:69](../../../backend/Sillar.Modules.Assistant/Services/FaqService.cs#L69) |
| 07 | E | Evidencia indeterminada obliga a abstención en el motor FAQ aislado; no implica disponibilidad offline. | [EditorialYFaqTests.cs:137](../../../backend/Sillar.Modules.Assistant.Tests/EditorialYFaqTests.cs#L137) |
| 08 | E | Otro escritor no edita en dominio local; transferencia de escritor no implementada ni autorizada. | [EditorialYFaqTests.cs:77](../../../backend/Sillar.Modules.Assistant.Tests/EditorialYFaqTests.cs#L77) |
| 09 | E | Aislamiento local de artículos, lectura, dominios y reservas; sin certificación de caches/proveedor externos. | [RepositorioEditorialLocalTests.cs:138](../../../backend/Sillar.Modules.Assistant.Tests/RepositorioEditorialLocalTests.cs#L138), [DominiosEditorialesInternacionalizadosTests.cs:23](../../../backend/Sillar.Modules.Assistant.Tests/DominiosEditorialesInternacionalizadosTests.cs#L23), [IdempotenciaCuotasLocalesTests.cs:38](../../../backend/Sillar.Modules.Assistant.Tests/IdempotenciaCuotasLocalesTests.cs#L38) |
| 10 | E | Sin fuente o ante ambigüedad exacta, FAQ se abstiene sin cita inventada. | [EditorialYFaqTests.cs:91](../../../backend/Sillar.Modules.Assistant.Tests/EditorialYFaqTests.cs#L91), [EditorialYFaqTests.cs:155](../../../backend/Sillar.Modules.Assistant.Tests/EditorialYFaqTests.cs#L155) |
| 11 | P | FAQ determinista y errores sin detalles sensibles; falta evaluación adversaria del futuro motor/contexto, todavía no habilitado. | [FaqService.cs:21](../../../backend/Sillar.Modules.Assistant/Services/FaqService.cs#L21), [PrivacidadFronterasAssistantTests.cs:28](../../../backend/Sillar.Modules.Assistant.Tests/PrivacidadFronterasAssistantTests.cs#L28) |
| 12 | B | Política de URL comprobada; garantía XSS/Markdown depende del renderizado y diseño no aprobados. | [DominiosEditorialesInternacionalizadosTests.cs:40](../../../backend/Sillar.Modules.Assistant.Tests/DominiosEditorialesInternacionalizadosTests.cs#L40) |
| 13 | E | Ciclo de FAQ determinista sin GPU/API demostrado con fuente local publicada. | [DominiosEditorialesInternacionalizadosTests.cs:49](../../../backend/Sillar.Modules.Assistant.Tests/DominiosEditorialesInternacionalizadosTests.cs#L49) |
| 14 | P | Planificador propone FAQ al faltar motor/capacidad; no existe integración de inferencia que acreditar. | [AssistantUsePolicy.cs:53](../../../backend/Sillar.Modules.Assistant/Domain/AssistantUsePolicy.cs#L53) |
| 15 | P | Admisión interna y retención de gasto incierto; egreso/facturación externos bloqueados. | [DespachosInciertosLocalesTests.cs:14](../../../backend/Sillar.Modules.Assistant.Tests/DespachosInciertosLocalesTests.cs#L14) |
| 16 | P | Concurrencia e idempotencia volátiles demostradas; durabilidad y techo contractual del proveedor pendientes. | [IdempotenciaCuotasLocalesTests.cs:14](../../../backend/Sillar.Modules.Assistant.Tests/IdempotenciaCuotasLocalesTests.cs#L14) |
| 17 | P | Política interna de rol/ámbito; autorización efectiva, auditoría CORE y CAS continúan bloqueados. | [LocalAssistantQuotaStore.cs:88](../../../backend/Sillar.Modules.Assistant/Services/LocalAssistantQuotaStore.cs#L88), [AssistantQuotaComposition.cs:8](../../../backend/Sillar.Modules.Assistant/Composition/AssistantQuotaComposition.cs#L8) |
| 18 | B | Minimización y contrato de datos externos requieren modalidad/proveedor y política aprobados; no habilitar egreso. | [AssistantUsePolicy.cs:31](../../../backend/Sillar.Modules.Assistant/Domain/AssistantUsePolicy.cs#L31) |
| 19 | P | Modelo de datos editorial y errores minimizados; faltan métricas operativas, retención e inventario de logs externos. | [AssistantDbContext.cs:15](../../../backend/Sillar.Modules.Assistant/Data/AssistantDbContext.cs#L15), [PrivacidadFronterasAssistantTests.cs:28](../../../backend/Sillar.Modules.Assistant.Tests/PrivacidadFronterasAssistantTests.cs#L28) |
| 20 | B | Modelo físico candidato pendiente de aprobación ADR-016/018, UUID v7, origen/versionado y verificación física. No corregir migraciones por cuenta propia. | [EditorialArticleConfiguration.cs:20](../../../backend/Sillar.Modules.Assistant/Data/Configurations/EditorialArticleConfiguration.cs#L20), [AssistantEditorialEndpoints.cs:69](../../../backend/Sillar.Modules.Assistant/Endpoints/AssistantEditorialEndpoints.cs#L69) |
| 21 | P | Módulo declara CORE y ninguna dependencia comercial; falta demostrar ciclo real e integración fail-closed. | [AssistantModule.cs:36](../../../backend/Sillar.Modules.Assistant/AssistantModule.cs#L36) |
| 22 | P | Revisión local retirada no se reutiliza; reinicio durable/reindexación y réplica requieren garantías aún no aprobadas. | [IdentidadRevisionLocalTests.cs:39](../../../backend/Sillar.Modules.Assistant.Tests/IdentidadRevisionLocalTests.cs#L39) |
| 23 | E | Fallo de fuente/evidencia produce abstención o error mínimo, demostrado sin infraestructura. | [FaqEvidenciaInvalidaTests.cs:27](../../../backend/Sillar.Modules.Assistant.Tests/FaqEvidenciaInvalidaTests.cs#L27), [PrivacidadFronterasAssistantTests.cs:74](../../../backend/Sillar.Modules.Assistant.Tests/PrivacidadFronterasAssistantTests.cs#L74) |
| 24 | B | D-M12-04/diseño 3.5 abierto según SPEC candidata externa; no construir UI por inferencia. | Referencia externa, §7 y AC-24. |
| 25 | P | Roles y autoridad editorial locales probados; falta identidad/política y trazabilidad CORE efectivas. | [EditorialYFaqTests.cs:60](../../../backend/Sillar.Modules.Assistant.Tests/EditorialYFaqTests.cs#L60), [AssistantModule.cs:72](../../../backend/Sillar.Modules.Assistant/AssistantModule.cs#L72) |
| 26 | P | Filtro de autoridad local implementado; no identidad productiva atestada ni protocolo de lector remoto. | [FaqService.cs:81](../../../backend/Sillar.Modules.Assistant/Services/FaqService.cs#L81), [HostAttestedAssistantInstallationScope.cs:18](../../../backend/Sillar.Modules.Assistant/Endpoints/HostAttestedAssistantInstallationScope.cs#L18) |

No se identifica un criterio completo ausente que pueda desarrollarse autónomamente
sin fijar contratos pendientes. La subcapacidad A seleccionada y corregida está dentro
de AC-06: cerrar la ventana entre comprobación de URL y última lectura autoritativa
de la FAQ local. Resultado nuevo: `RevalidacionFinalFaqTests`, 4 PASS, 0 FAIL, 0 SKIP.
Comprueba retirada, sustitución, pérdida de autoridad y borrador que no reemplaza
la publicación. No se modificó el contrato de fuente ni el lector EF/su aislamiento.
Regresión justificada por el cambio de orden de lectura/política: las 8 pruebas
`EditorialYFaqTests.Faq*`, 8 PASS, 0 FAIL, 0 SKIP. No se repitieron las demás familias.
`git diff --check`: limpio. Sin PostgreSQL, endpoints reales ni suites completas.

## Pendientes definitivos para conciliación

- CORE/Host: identidad M12 atestada, entitlement, permisos por artículo, auditoría y revocación de CAS. Sin autorización positiva ni CAS activo.
- M12/Chat 2: contrato de consistencia de consulta frente a publicación/retirada concurrentes; validación PostgreSQL de escritura y snapshot. La última lectura no garantiza revocación posterior ni una fotografía global entre múltiples artículos.
- JP/Chat 2: modelo físico compatible ADR-016/018; no transformar los candidatos en esquema aprobado.
- Motores/cuotas: ejecución local/API, minimización concreta, custodia, unidades, costes tardíos, conciliación de estados inciertos y presupuesto durable. Sin inventar proveedor, reinicios de cupo o liberación automática.
- Producto/diseño: D-M12-04, renderizado seguro y ciclo de módulo completo.
- Privacidad/operación: métricas y retención definidas, logs de Host/proxies/proveedor y revisión legal antes de publicación.

Tras la corrección seleccionada no se identifica otra brecha **sustancial autónoma**
respaldada por los contratos vigentes: detener implementación y conciliar estas puertas.
