namespace Sillar.Core.Contracts;

/// <summary>
/// Auditoría obligatoria de exportaciones. Fase B, candidato.
/// </summary>
/// <remarks>
/// <para>
/// <b>Decisión de JP (25/10/2026):</b> toda exportación se audita, y el
/// contenido exportado no se almacena.
/// </para>
/// <para>
/// <b>Primero se registra, después se entrega.</b> Quien exporta llama a
/// <see cref="RecordAsync"/> antes de entregar el archivo, y si el resultado no
/// es <see cref="ExportAuditResult.IsRecorded"/> no lo entrega. Un registro cuya
/// entrega luego falla queda como constancia de una exportación autorizada que
/// no llegó: es preferible a una entrega sin registro.
/// </para>
/// <para>
/// <b>Metadatos mínimos</b>, y ninguno de texto libre: quién (sale de
/// <see cref="ICurrentAdmin"/>, nunca de quien llama), qué módulo, qué tipo de
/// exportación, en qué formato y cuántas filas. CORE añade cuándo, desde qué IP y
/// un identificador de la exportación. No hay campo para filtros, nombres de
/// archivo ni contenido, para que no puedan colarse.
/// </para>
/// <para>
/// Si el token del llamador se cancela, se propaga <see cref="OperationCanceledException"/>.
/// </para>
/// </remarks>
public interface IExportAudit
{
    /// <summary>Registra una exportación antes de entregarla.</summary>
    Task<ExportAuditResult> RecordAsync(ExportAuditRequest request, CancellationToken cancellationToken);
}

/// <summary>Lo que se registra de una exportación.</summary>
/// <param name="ModuleCode">Código del módulo que exporta. Minúsculas, cifras y guion bajo; hasta 40.</param>
/// <param name="ExportKind">Tipo estable de exportación (<c>sales_by_day</c>). Mismo formato; hasta 60. Es un código, no una descripción.</param>
/// <param name="Format">Formato del archivo (<c>csv</c>). Minúsculas y cifras; hasta 10.</param>
/// <param name="RowCount">Cuántas filas lleva, si se sabe. Nunca negativo.</param>
public sealed record ExportAuditRequest(string ModuleCode, string ExportKind, string Format, int? RowCount);

/// <summary>Resultado del registro.</summary>
public enum ExportAuditOutcome
{
    /// <summary>Registrado. Se puede entregar.</summary>
    Recorded,

    /// <summary>No registrado. No se entrega.</summary>
    Rejected,
}

/// <summary>Por qué no se registró.</summary>
public enum ExportAuditFailure
{
    /// <summary>No hay administrador identificado: no hay a quién atribuir la exportación.</summary>
    ActorMissing,

    /// <summary>Algún metadato no cumple el formato.</summary>
    MetadataInvalid,

    /// <summary>No se pudo escribir en <c>core.audit_log</c>: fallo de la base o acción no admitida todavía por el CHECK.</summary>
    AuditUnavailable,
}

/// <summary>El resultado, con el identificador de la exportación si se registró.</summary>
/// <param name="Outcome">Registrado o rechazado.</param>
/// <param name="ExportId">Identificador generado por CORE (uuid v7), guardado en <c>entity_id</c>. Sirve para marcar el archivo entregado. Nulo si se rechazó.</param>
/// <param name="Failure">La causa, si se rechazó.</param>
public sealed record ExportAuditResult(ExportAuditOutcome Outcome, Guid? ExportId, ExportAuditFailure? Failure)
{
    /// <summary>Verdadero solo si quedó registrada.</summary>
    public bool IsRecorded => Outcome == ExportAuditOutcome.Recorded;

    /// <summary>Registrada.</summary>
    public static ExportAuditResult Recorded(Guid exportId) => new(ExportAuditOutcome.Recorded, exportId, null);

    /// <summary>Rechazada.</summary>
    public static ExportAuditResult Rejected(ExportAuditFailure failure) => new(ExportAuditOutcome.Rejected, null, failure);
}
