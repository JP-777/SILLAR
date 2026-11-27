using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain;

namespace Sillar.Core.Auditing;

/// <summary>
/// Productor durable de <see cref="IExportAudit"/>.
/// </summary>
/// <remarks>
/// <para>
/// La acción export forma parte del vocabulario y de la restricción física.
/// Si una instalación no admite esa acción, el productor no entrega un recibo.
/// </para>
/// <para>
/// El resumen lo compone CORE con metadatos validados; nada de quien llama entra
/// en él sin pasar por un formato cerrado.
/// </para>
/// </remarks>
internal sealed partial class ExportAudit : IExportAudit
{
    /// <summary>Código de la acción que se guarda antes de entregar el archivo.</summary>
    internal const string ExportActionCode = "export";

    /// <summary>Longitudes de las columnas de <c>core.audit_log</c> que reciben cada metadato.</summary>
    internal const int ModuleCodeMaxLength = 40;
    internal const int ExportKindMaxLength = 60;
    internal const int FormatMaxLength = 10;

    private readonly IAuditWriter writer;
    private readonly ICurrentAdmin admin;
    private readonly CoreDbContext database;
    private readonly ILogger<ExportAudit> logger;
    private readonly IReadOnlyCollection<string> admittedActions;

    public ExportAudit(IAuditWriter writer, ICurrentAdmin admin, CoreDbContext database, ILogger<ExportAudit> logger)
        : this(writer, admin, database, logger, AuditAction.All)
    {
    }

    /// <summary>Con otro vocabulario. Permite provocar el rechazo por vocabulario incompleto.</summary>
    internal ExportAudit(
        IAuditWriter writer,
        ICurrentAdmin admin,
        CoreDbContext database,
        ILogger<ExportAudit> logger,
        IReadOnlyCollection<string> admittedActions)
    {
        this.writer = writer;
        this.admin = admin;
        this.database = database;
        this.logger = logger;
        this.admittedActions = admittedActions;
    }

    /// <inheritdoc />
    public async Task<ExportAuditResult> RecordAsync(ExportAuditRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (admin.AdminUserId is not { } adminUserId
            || string.IsNullOrEmpty(admin.Email)
            || string.IsNullOrEmpty(admin.HomeNode)
            || string.IsNullOrWhiteSpace(admin.DisplayName))
        {
            return ExportAuditResult.Rejected(ExportAuditFailure.ActorMissing);
        }

        if (!IsValid(request))
        {
            return ExportAuditResult.Rejected(ExportAuditFailure.MetadataInvalid);
        }

        if (!admittedActions.Contains(ExportActionCode))
        {
            return ExportAuditResult.Rejected(ExportAuditFailure.ExportActionNotAdmitted);
        }

        if (database.Database.CurrentTransaction is not null)
        {
            return ExportAuditResult.Rejected(ExportAuditFailure.AuditNotDurable);
        }

        var actor = new ExportActor(adminUserId, admin.Email, admin.HomeNode, admin.DisplayName);
        var exportId = Guid.CreateVersion7();
        var entry = new AuditEntry(ExportActionCode)
        {
            AdminUserId = actor.AdminUserId,
            AdminUserEmail = actor.Email,
            AdminUserName = actor.DisplayName,
            AdminUserHomeNode = actor.HomeNode,
            ModuleCode = request.ModuleCode,
            EntityType = request.ExportKind,
            EntityId = exportId.ToString(),
            Summary = Summary(request),
        };

        try
        {
            await writer.WriteAsync(entry, cancellationToken);
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            Forget(exportId);
            throw exception as OperationCanceledException
                ?? new OperationCanceledException("Registro cancelado por el llamador.", exception, cancellationToken);
        }
        catch (Exception exception)
        {
            Forget(exportId);
            logger.LogWarning(exception, "No se pudo auditar la exportación {Kind} de {Module}; no se entrega.", request.ExportKind, request.ModuleCode);
            return ExportAuditResult.Rejected(ExportAuditFailure.AuditUnavailable);
        }

        return ExportAuditResult.Recorded(exportId, actor);
    }

    /// <summary>
    /// Quita del contexto la fila que no se pudo guardar, para que ningún guardado
    /// posterior de la misma petición la reintente a escondidas.
    /// </summary>
    private void Forget(Guid exportId)
    {
        var id = exportId.ToString();
        foreach (var pending in database.ChangeTracker.Entries<AuditLog>()
                     .Where(e => e.State == EntityState.Added && e.Entity.EntityId == id)
                     .ToList())
        {
            pending.State = EntityState.Detached;
        }
    }

    internal static bool IsValid(ExportAuditRequest request)
        => request.ModuleCode is { Length: > 0 and <= ModuleCodeMaxLength } && Code().IsMatch(request.ModuleCode)
            && request.ExportKind is { Length: > 0 and <= ExportKindMaxLength } && Code().IsMatch(request.ExportKind)
            && request.Format is { Length: > 0 and <= FormatMaxLength } && FormatCode().IsMatch(request.Format)
            && request.RowCount is null or >= 0;

    internal static string Summary(ExportAuditRequest request)
        => request.RowCount is { } rows
            ? $"Exportación {request.ExportKind} en {request.Format}, {rows} filas."
            : $"Exportación {request.ExportKind} en {request.Format}.";

    [GeneratedRegex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Code();

    [GeneratedRegex("^[a-z0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatCode();
}

/// <summary>Registro explícito y opcional del productor candidato.</summary>
/// <remarks>
/// No lo llama <c>CoreServices</c> ni el host. Solo vale en modo normal:
/// <see cref="ICurrentAdmin"/> no existe en modo instalación.
/// </remarks>
public static class CandidateExportAuditRegistration
{
    /// <summary>Registra <see cref="IExportAudit"/> con el productor candidato, con ámbito de petición.</summary>
    public static IServiceCollection AddCandidateExportAudit(this IServiceCollection services)
    {
        services.AddScoped<IExportAudit, ExportAudit>();
        return services;
    }
}
