namespace Sillar.Core.Contracts;

/// <summary>
/// Auditoría obligatoria de exportaciones, antes del archivo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Decisión de JP:</b> toda exportación se audita, y el contenido exportado no
/// se guarda. <b>Primero el registro, después los bytes:</b> si
/// <see cref="ExportAuditResult.IsRecorded"/> no es verdadero, no sale ni un byte.
/// <see cref="ExportAuditDelivery.DeliverAuditedAsync{T}"/> lo hace cumplir.
/// </para>
/// <para>
/// La acción export está admitida por el vocabulario y su migración física.
/// Sin esquema compatible o sin confirmación durable no se devuelve un recibo.
/// </para>
/// <para>
/// <b>Actor real:</b> sale de la sesión validada en la petición
/// (<see cref="ICurrentAdmin"/>), nunca de quien llama, y exige nombre, id, correo y nodo.
/// <b>Durabilidad:</b> el registro se confirma en su propio guardado; si el
/// contexto de auditoría está dentro de una transacción ajena, se rechaza, porque
/// esa transacción podría deshacerlo después de entregado el archivo.
/// </para>
/// <para>Si el token del llamador se cancela, se propaga <see cref="OperationCanceledException"/>.</para>
/// </remarks>
public interface IExportAudit
{
    /// <summary>Registra una exportación antes de entregarla.</summary>
    Task<ExportAuditResult> RecordAsync(ExportAuditRequest request, CancellationToken cancellationToken);
}

/// <summary>Lo que se registra de una exportación. Ningún campo es texto libre.</summary>
/// <param name="ModuleCode">Módulo que exporta (M10: <c>reporting</c>). <c>^[a-z][a-z0-9_]*$</c>, hasta 40.</param>
/// <param name="ExportKind">Tipo estable (M10: <c>order_indicators</c>). Mismo formato, hasta 60.</param>
/// <param name="Format">Formato del archivo (M10: <c>csv</c>). <c>^[a-z0-9]+$</c>, hasta 10.</param>
/// <param name="RowCount">Filas, si se saben. Nunca negativo.</param>
public sealed record ExportAuditRequest(string ModuleCode, string ExportKind, string Format, int? RowCount);

/// <summary>Resultado del registro.</summary>
public enum ExportAuditOutcome
{
    /// <summary>Registrado y confirmado. Se puede entregar.</summary>
    Recorded,

    /// <summary>No registrado. No se entrega.</summary>
    Rejected,
}

/// <summary>Por qué no se registró.</summary>
public enum ExportAuditFailure
{
    /// <summary>Sin administrador identificado con id, correo y nodo.</summary>
    ActorMissing,

    /// <summary>La sesión o el permiso vigente no autoriza exportar.</summary>
    PermissionDenied,

    /// <summary>Algún metadato no cumple su formato.</summary>
    MetadataInvalid,

    /// <summary>El vocabulario de auditoría todavía no admite <c>export</c>.</summary>
    ExportActionNotAdmitted,

    /// <summary>El contexto de auditoría está dentro de una transacción ajena: el registro no sería durable.</summary>
    AuditNotDurable,

    /// <summary>No se pudo escribir en <c>core.audit_log</c>.</summary>
    AuditUnavailable,
}

/// <summary>Quién exporta, tal como lo validó la sesión.</summary>
public sealed record ExportActor(int AdminUserId, string Email, string HomeNode, string? DisplayName = null);

/// <summary>El resultado, con el identificador y el actor si se registró.</summary>
/// <param name="Outcome">Registrado o rechazado.</param>
/// <param name="ExportId">uuid v7 generado por CORE y guardado en <c>entity_id</c>. Nulo si se rechazó.</param>
/// <param name="Failure">La causa, si se rechazó.</param>
/// <param name="Actor">El actor registrado. Nulo si se rechazó.</param>
public sealed record ExportAuditResult(ExportAuditOutcome Outcome, Guid? ExportId, ExportAuditFailure? Failure, ExportActor? Actor)
{
    /// <summary>Verdadero solo si quedó registrada y confirmada.</summary>
    public bool IsRecorded => Outcome == ExportAuditOutcome.Recorded;

    /// <summary>Registrada.</summary>
    public static ExportAuditResult Recorded(Guid exportId, ExportActor actor) => new(ExportAuditOutcome.Recorded, exportId, null, actor);

    /// <summary>Rechazada.</summary>
    public static ExportAuditResult Rejected(ExportAuditFailure failure) => new(ExportAuditOutcome.Rejected, null, failure, null);
}

/// <summary>Resultado de una entrega auditada.</summary>
/// <param name="Audit">El registro.</param>
/// <param name="Delivered">Si se llegó a llamar a la entrega.</param>
/// <param name="Value">Lo que devolvió la entrega, si se llamó.</param>
public sealed record ExportDeliveryResult<T>(ExportAuditResult Audit, bool Delivered, T? Value);

/// <summary>Auditar y después entregar, en ese orden y sin atajos.</summary>
public static class ExportAuditDelivery
{
    /// <summary>
    /// Registra la exportación y, solo si quedó registrada, llama a <paramref name="deliver"/>
    /// con su identificador. Si no, no se llama: ni cabeceras ni bytes.
    /// </summary>
    /// <remarks>
    /// Si la entrega falla después de registrar, su excepción sube tal cual: el
    /// registro queda como constancia de una exportación autorizada que no llegó.
    /// </remarks>
    public static async Task<ExportDeliveryResult<T>> DeliverAuditedAsync<T>(
        this IExportAudit audit,
        ExportAuditRequest request,
        Func<Guid, CancellationToken, Task<T>> deliver,
        CancellationToken cancellationToken)
    {
        var result = await audit.RecordAsync(request, cancellationToken);
        if (!result.IsRecorded || result.ExportId is not { } exportId)
        {
            return new(result, false, default);
        }

        return new(result, true, await deliver(exportId, cancellationToken));
    }
}
