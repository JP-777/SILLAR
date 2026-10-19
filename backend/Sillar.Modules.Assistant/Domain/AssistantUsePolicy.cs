namespace Sillar.Modules.Assistant.Domain;

public enum AssistantMode
{
    Faq,
    Local,
    ExternalApi
}

public enum AdmissionPlan
{
    FaqOnly,
    LocalAllowedBySnapshot,
    ApiAllowedBySnapshot,
    FaqFallback,
    InvalidConfiguration
}

/// <summary>Mediciones y reservas ya efectuadas por una futura autoridad de cuota local.</summary>
public sealed record ResourceBudgetSnapshot(
    Guid InstallationId,
    AssistantMode ExplicitMode,
    bool EngineConfigured,
    long LimitUnits,
    long ConsumedUnits,
    long ReservedUnits,
    long WorstCaseNewUnits,
    int ConcurrencyLimit,
    int RunningRequests);

/// <summary>
/// Planificador puro que nunca reserva cuota, invoca IA ni selecciona otro proveedor.
/// Un AllowedBySnapshot no permite llamar a un modelo sin reserva atómica posterior.
/// </summary>
public static class AssistantUsePolicy
{
    public static AdmissionPlan Evaluate(Guid installationId, ResourceBudgetSnapshot? snapshot)
    {
        if (snapshot is null || installationId == Guid.Empty ||
            snapshot.InstallationId != installationId ||
            snapshot.LimitUnits < 0 || snapshot.ConsumedUnits < 0 ||
            snapshot.ReservedUnits < 0 || snapshot.WorstCaseNewUnits < 0 ||
            snapshot.ConcurrencyLimit < 0 || snapshot.RunningRequests < 0 ||
            !Enum.IsDefined(snapshot.ExplicitMode))
            return AdmissionPlan.InvalidConfiguration;

        if (snapshot.ExplicitMode == AssistantMode.Faq)
            return AdmissionPlan.FaqOnly;

        if (!snapshot.EngineConfigured || snapshot.ConcurrencyLimit == 0 ||
            snapshot.RunningRequests >= snapshot.ConcurrencyLimit ||
            snapshot.LimitUnits == 0 || snapshot.WorstCaseNewUnits == 0)
            return AdmissionPlan.FaqFallback;

        // Resta escalonada evita overflow. La cuota real exige reserva atómica en su fuente.
        if (snapshot.ConsumedUnits > snapshot.LimitUnits ||
            snapshot.ReservedUnits > snapshot.LimitUnits - snapshot.ConsumedUnits ||
            snapshot.WorstCaseNewUnits > snapshot.LimitUnits - snapshot.ConsumedUnits - snapshot.ReservedUnits)
            return AdmissionPlan.FaqFallback;

        return snapshot.ExplicitMode switch
        {
            AssistantMode.Local => AdmissionPlan.LocalAllowedBySnapshot,
            AssistantMode.ExternalApi => AdmissionPlan.ApiAllowedBySnapshot,
            _ => AdmissionPlan.InvalidConfiguration
        };
    }
}
