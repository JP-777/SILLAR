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
    internal const string Rejected = "rejected";
    internal const string Ready = "ready";
    internal const string Processing = "processing";
    internal const string Indeterminate = "indeterminate";
    internal const string Completed = "completed";

    internal static readonly string[] All =
        [PendingReview, Held, Rejected, Ready, Processing, Indeterminate, Completed];
}

internal static class ProviderCommercialEffectStates
{
    internal const string NotRequested = "not_requested";
    internal const string Indeterminate = "indeterminate";
    internal const string Accredited = "accredited";

    internal static readonly string[] All =
        [NotRequested, Indeterminate, Accredited];
}

internal static class ProviderAttemptOutcomes
{
    internal const string Running = "running";
    internal const string Succeeded = "succeeded";
    internal const string Failed = "failed";
    internal const string Indeterminate = "indeterminate";

    internal static readonly string[] All =
        [Running, Succeeded, Failed, Indeterminate];
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
