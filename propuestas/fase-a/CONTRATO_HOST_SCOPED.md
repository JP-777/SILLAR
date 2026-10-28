# HOST · preimplementación aislada / Coordinación CORE → Host → M12

## Firma CORE candidata recibible

`IAssistantAccessAuthority.EvaluateCurrentInstallationAsync(CancellationToken)`
→ `Task<AssistantAccessDecision>` con `Outcome`, `Reason`, `InstallationKey?`, `Source`,
`ObservedAt`, `ValidUntil` y hechos de licencia/activación. Los tipos exactos de
los tres últimos elementos continúan en conciliación.

En FASE A el productor CORE **nunca retorna Allowed**; puede retornar `Denied` o `PendingDecision`. Host **no** debe derivar `IsAssistantLicensed = true` a partir de `LicensedUntil == null` ni `IsInstallationConfigured = true` a partir de un Guid no vacío: son hechos diferentes. `InstallationKey` NO es credencial ni UUID público ratificado.

El Host actual conoce la instalación durante el bootstrap y conserva una
`ModuleActivationSnapshot`, pero esa foto solo describe activaciones. No es una
autoridad de licencia/entitlement y no se combina con headers, cookies, claims o
datos del cliente para fabricar una autorización.

## Puente hacia las firmas M12/P11

Interfaz actual M12: `ITrustedAssistantHostEvidence` getters booleanos síncronos + `Guid? InstallationId`; `ITrustedAssistantQuotaPolicyWriter.CanWritePolicy(Guid)` síncrono. **No** hacer E/S dentro de los getters.

Patrón propuesto para revisión de Chat 3: un resolvedor scoped asíncrono ejecuta CORE **antes** de crear una fotografía inmutable; el adaptador scoped expone getters únicamente sobre esa fotografía. Un scope sin resolución, con error o con decisión pendiente devuelve false/Guid null. Aun con `Allowed` futuro, Host no produce evidencia positiva hasta que JP ateste una identidad para M12, y CAS sigue negado hasta autorizar política granular y protocolo de revocación.

No se coloca código compilable consumidor de CORE en esta worktree: el contrato nuevo aún no forma parte de su base sin integración. HOST debe trabajar solo en su espacio propio y registrar divergencias; NO copiar archivos CORE a HOST ni editar M12/Program.cs, no registrar el adaptador DI ni crear endpoints.

La composición candidata vive en `backend/Sillar.Host.AssistantBridge` y es
deliberadamente opt-in. `AssistantAuthorityProbe` es una costura privada del
Host, no una interfaz que pretenda pertenecer a CORE. No se registra desde
`Program.cs`, no se añade a `Sillar.sln` y no tiene productor predeterminado.
La sobrecarga de composición acepta una fábrica por ámbito porque el productor
CORE candidato también es scoped; nunca se captura esa autoridad en el root.

## Firmas que requiere la conciliación

**Productor CORE ya candidato:**

```csharp
Task<AssistantAccessDecision> EvaluateCurrentInstallationAsync(
    CancellationToken cancellationToken);
```

El adaptador futuro necesita que `AssistantAccessDecision` conserve un resultado
semántico (`Allowed`, `Denied`, `PendingDecision`) y una identidad interna nullable.
Ningún resultado produce hoy una foto positiva: `InstallationKey` observada se
reduce a `InternalKeyObservedButUnattested`, sin transportar el GUID. Cualquier
resultado, incoherencia, excepción o cancelación termina en foto denegada.
`Reason` y `Source` sirven para diagnóstico interno; no son autoridad adicional.

`InstallationId` de M12 permanece siempre `null`: Host no convierte
`InstallationKey` de CORE ni siquiera como identidad interna de M12. Hace falta
que JP ratifique una identidad atestada y su semántica; si fuese pública, además
debe ser distinta por ADR-012. La proyección actual solo conserva el hecho
categórico de que CORE observó una clave, nunca el GUID.

La costura tipada ya compilable es:

```csharp
new TypedAssistantAuthorityProbe<AssistantAccessDecision>(
    authority.EvaluateCurrentInstallationAsync,
    ProjectDecision,
    timeProvider);

AssistantAuthorityObservation ProjectDecision(
    AssistantAccessDecision decision,
    DateTimeOffset evaluatedAt);
```

Solo falta escribir `ProjectDecision` cuando el DTO CORE sea definitivo. No usa
reflexión, `dynamic` ni una copia local de `AssistantAccessDecision`.

Firmas/semánticas todavía requeridas de Claude D y Chat 2:

- tipo y nulabilidad exactos de `ValidUntil`, más si el límite es inclusivo;
- tipo exacto de los hechos de licencia y activación y si son individualmente
  autoritativos o únicamente explicativos del `Outcome`;
- invariantes entre `Outcome`, `Reason`, hechos, `ObservedAt` y `ValidUntil`;
- identidad atestada por JP para M12, diferente de un simple renombrado de
  `InstallationKey`;
- tratamiento de `Source` vacío/desconocido y desfase de reloj admisible.

**Consumidores M12 observados, pendientes de ratificación:**

```csharp
public interface ITrustedAssistantHostEvidence
{
    bool IsInstallationConfigured { get; }
    bool IsAssistantActive { get; }
    bool IsAssistantLicensed { get; }
    Guid? InstallationId { get; }
}

public interface ITrustedAssistantQuotaPolicyWriter
{
    bool CanWritePolicy(Guid installationId);
}
```

La primera forma es compatible con el snapshot inmutable candidato. La segunda
solo permite una consulta previa y no puede cerrar una carrera con la revocación.

También falta conciliar quién invoca `ResolveAsync` antes del primer consumidor
M12. La construcción DI no puede esperar asíncronamente y los getters no pueden
bloquear. Las opciones mínimas son un filtro/middleware acotado a las operaciones
M12 o que la propia operación de aplicación solicite la resolución asíncrona. Si
ninguno lo hace, el comportamiento intencional es DENY por foto no resuelta.

## Revocación CAS

`CanWritePolicy(Guid)` se evalúa antes de transacción M12 y NO protege frente a revocación concurrente posterior. In-memory scopes no son prueba de serializabilidad. CORE lock de activación no es reusable sin ratificación JP. Mantener denegación universal en FASE A.

Alternativas para ratificar, sin implementar todavía:

1. **Bloqueo de la fila autoritativa dentro de la misma transacción.** La operación
   M12 abre la transacción, pide a CORE que bloquee y revalide la autoridad usando
   esa conexión/transacción, ejecuta el CAS y confirma. La revocación bloquea la
   misma fila. Requiere un contrato CORE transaccional que no exponga su DbContext,
   una abstracción conciliada de conexión/transacción y que todos los escritores
   de revocación respeten la misma serialización.
2. **Advisory lock transaccional dedicado a entitlement.** Revocación y escritura
   CAS toman la misma clave `pg_advisory_xact_lock`, CORE revalida después del lock
   y M12 escribe antes del commit. Requiere namespace/derivación de clave ratificados,
   orden global de locks y participación obligatoria de todos los caminos de
   revocación. Es un protocolo nuevo; no es el lock de activación existente.
3. **Autoridad que ejecute la operación protegida.** Un contrato CORE recibe una
   operación transaccional acotada y solo la ejecuta tras revalidar. Reduce el uso
   incorrecto del resultado, pero exige decidir cómo comparte la transacción con
   el almacenamiento M12 sin convertir CORE en dueño de sus tablas.

Un `epoch`, token, caché o segunda consulta sin compartir la transacción solo
detecta algunos cambios; no garantiza revocación durante toda la escritura y no
es alternativa equivalente.

## Test plan focal (sin PG/Host real)

- Scope sin productor: DENY; sin resolver: DENY.
- `Outcome != Allowed`: todos los booleanos `false`, identidad no atestada.
- Identity mismatch: DENY; nunca usar identity del cliente.
- Excepción/timeout: DENY, sin bloqueo síncrono.
- CAS writer: false para cualquier id, incluido el correcto; no confundir super_admin con permiso granular.

Las pruebas focales de esta fase no abren PostgreSQL. La carrera de revocación y
la serialización escogida requieren una prueba PostgreSQL posterior con dos
transacciones reales y sabotaje del camino de revocación.

**Estado HOST:** PREPARADO para consumir CORE; implementación cruzada detenida hasta conciliación de firmas y decisión JP si cambia semántica. Ninguna prueba física autorizada.
