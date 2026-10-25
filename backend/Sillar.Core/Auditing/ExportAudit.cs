using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain;

namespace Sillar.Core.Auditing;

/// <summary>
/// Productor candidato de <see cref="IExportAudit"/>: valida los metadatos,
/// toma el autor de <see cref="ICurrentAdmin"/> y escribe con
/// <see cref="IAuditWriter"/>.
/// </summary>
/// <remarks>
/// El resumen lo compone CORE a partir de los metadatos validados; nada de lo que
/// llega de quien llama entra en él sin pasar por un formato cerrado.
/// </remarks>
internal sealed partial class ExportAudit(
    IAuditWriter writer,
    ICurrentAdmin admin,
    CoreDbContext database,
    ILogger<ExportAudit> logger) : IExportAudit
{
    /// <summary>Longitudes de las columnas de <c>core.audit_log</c> que reciben cada metadato.</summary>
    internal const int ModuleCodeMaxLength = 40;
    internal const int ExportKindMaxLength = 60;
    internal const int FormatMaxLength = 10;

    /// <inheritdoc />
    public async Task<ExportAuditResult> RecordAsync(ExportAuditRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (admin.AdminUserId is not { } adminUserId || string.IsNullOrEmpty(admin.Email))
        {
            return ExportAuditResult.Rejected(ExportAuditFailure.ActorMissing);
        }

        if (!IsValid(request))
        {
            return ExportAuditResult.Rejected(ExportAuditFailure.MetadataInvalid);
        }

        var exportId = Guid.CreateVersion7();
        var entry = new AuditEntry(AuditAction.Export)
        {
            AdminUserId = adminUserId,
            AdminUserEmail = admin.Email,
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
            // Sin registro no hay exportación. Incluye la base caída y el CHECK
            // ck_audit_log_action mientras CoreAuditExport no esté aplicada.
            Forget(exportId);
            logger.LogWarning(exception, "No se pudo auditar la exportación {Kind} de {Module}; no debe entregarse.", request.ExportKind, request.ModuleCode);
            return ExportAuditResult.Rejected(ExportAuditFailure.AuditUnavailable);
        }

        return ExportAuditResult.Recorded(exportId);
    }

    /// <summary>
    /// Quita del contexto la fila que no se pudo guardar. Si se quedara, el
    /// siguiente <c>SaveChanges</c> de la misma petición la reintentaría por su
    /// cuenta, ya sin nadie esperando el resultado.
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

/// <summary>Registro explícito y opcional del productor candidato de auditoría de exportaciones.</summary>
/// <remarks>No lo llama <c>CoreServices</c> ni el host.</remarks>
public static class CandidateExportAuditRegistration
{
    /// <summary>Registra <see cref="IExportAudit"/> con el productor candidato, con ámbito de petición.</summary>
    public static IServiceCollection AddCandidateExportAudit(this IServiceCollection services)
    {
        services.AddScoped<IExportAudit, ExportAudit>();
        return services;
    }
}
