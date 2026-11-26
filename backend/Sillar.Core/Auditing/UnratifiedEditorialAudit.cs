using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;

namespace Sillar.Core.Auditing;

/// <summary>
/// Productor candidato de <see cref="IEditorialAudit"/> mientras no haya
/// estrategia ratificada: valida el evento y no registra nada.
/// </summary>
/// <remarks>
/// No escribe en <c>core.audit_log</c> a propósito. Escribir aquí y dejar que M12
/// guarde después serían dos guardados sueltos, y un registro de un cambio que
/// quizá nunca se confirmó.
/// </remarks>
internal sealed class UnratifiedEditorialAudit : IEditorialAudit
{
    internal const int ArticleIdMaxLength = 60;

    /// <inheritdoc />
    public EditorialAuditCapability Capability { get; } = new(EditorialAuditStrategy.None, false, false);

    /// <inheritdoc />
    public Task<EditorialAuditResult> RecordAsync(EditorialAuditRecord record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(IsValid(record)
            ? EditorialAuditResult.Rejected(EditorialAuditFailure.StrategyUnratified)
            : EditorialAuditResult.Rejected(EditorialAuditFailure.RecordInvalid));
    }

    internal static bool IsValid(EditorialAuditRecord record)
        => record.EventId != Guid.Empty
            && record.EventId.Version == 7
            && record.InstallationKey != Guid.Empty
            && record.Actor is { AdminUserId: > 0, HomeNode.Length: > 0, Role.Length: > 0 }
            && record.Actor.SessionId != Guid.Empty
            && !string.IsNullOrWhiteSpace(record.ArticleId)
            && record.ArticleId.Length <= ArticleIdMaxLength
            && !record.ArticleId.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))
            && Enum.IsDefined(record.Operation);
}

/// <summary>Registro explícito y opcional del productor editorial no ratificado.</summary>
/// <remarks>No lo llama <c>CoreServices</c> ni el host.</remarks>
public static class CandidateEditorialAuditRegistration
{
    /// <summary>Registra <see cref="IEditorialAudit"/> con el productor que no registra.</summary>
    public static IServiceCollection AddCandidateEditorialAudit(this IServiceCollection services)
    {
        services.AddSingleton<IEditorialAudit, UnratifiedEditorialAudit>();
        return services;
    }
}
