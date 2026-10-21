namespace Sillar.Modules.Payments.Infrastructure.Persistence.Entities;

internal static class ProviderVerificationStates
{
    internal const string Received = "received";
    internal const string Authenticated = "authenticated";
    internal const string Rejected = "rejected";
    internal const string Indeterminate = "indeterminate";

    internal static readonly string[] All =
        [Received, Authenticated, Rejected, Indeterminate];
}

internal static class ProviderReceptionStates
{
    internal const string Received = "received";
    internal const string Rejected = "rejected";
    internal const string Indeterminate = "indeterminate";
    internal const string Held = "held";
    internal const string Duplicate = "duplicate";
    internal const string PendingDurableReview = "pending_durable_review";

    internal static readonly string[] All =
    [
        Received,
        Rejected,
        Indeterminate,
        Held,
        Duplicate,
        PendingDurableReview
    ];
}

internal static class ProviderInboxStates
{
    internal const string PendingReview = "pending_review";
    internal const string Held = "held";
    internal const string Ready = "ready";
    internal const string Processing = "processing";
    internal const string Indeterminate = "indeterminate";
    internal const string Completed = "completed";

    internal static readonly string[] All =
        [PendingReview, Held, Ready, Processing, Indeterminate, Completed];
}

internal static class ProviderCommercialEffectStates
{
    internal const string NotRequested = "not_requested";
    internal const string Indeterminate = "indeterminate";
    internal const string Conflict = "conflict";
    internal const string DecisionRequired = "decision_required";
    internal const string Applied = "applied";
    internal const string AlreadyApplied = "already_applied";

    internal static readonly string[] All =
    [
        NotRequested,
        Indeterminate,
        Conflict,
        DecisionRequired,
        Applied,
        AlreadyApplied
    ];
}

internal static class ProviderAttemptOutcomes
{
    internal const string Running = "running";
    internal const string Succeeded = "succeeded";
    internal const string Held = "held";
    internal const string Indeterminate = "indeterminate";

    internal static readonly string[] All =
        [Running, Succeeded, Held, Indeterminate];
}

internal static class ProviderSettlementFactClassifications
{
    internal const string New = "new";
    internal const string Duplicate = "duplicate";
    internal const string Conflict = "conflict";
    internal const string DecisionRequired = "decision_required";

    internal static readonly string[] All =
        [New, Duplicate, Conflict, DecisionRequired];
}

internal static class ProviderPersistenceGuard
{
    internal static string OpaqueRequired(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "La identidad externa no puede estar vacía.",
                parameterName);
        }

        return value;
    }

    internal static string Sha256(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length != 64
            || !value.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException(
                "La evidencia exige un SHA-256 hexadecimal.",
                parameterName);
        }

        return value.ToLowerInvariant();
    }

    internal static DateTimeOffset RequiredInstant(
        DateTimeOffset value,
        string parameterName)
    {
        if (value == default)
        {
            throw new ArgumentException(
                "El instante es obligatorio.",
                parameterName);
        }

        return value;
    }
}
