namespace Sillar.Core.Contracts;

/// <summary>
/// Candidate installation-owned setting; no current seed or public claim.
/// Actual authority must be read on the server, never from client input.
/// </summary>
public static class IanaZoneCandidate
{
    public const string SettingKey = "installation.timezone_iana";

    public static IanaZoneEvaluation Evaluate(string? value)
    {
        if (value is null || string.IsNullOrWhiteSpace(value))
            return new(IanaZoneOutcome.Missing, null);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Contains('\\') || value.Contains(' ')
            || !value.Contains('/')
            || value.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
            return new(IanaZoneOutcome.Invalid, value);
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(value);
            // Runtime resolution alone does not ratify alias or IANA canonicality.
            return new(IanaZoneOutcome.RuntimeResolvedCandidate, tz.Id);
        }
        catch (TimeZoneNotFoundException)
        {
            return new(IanaZoneOutcome.Invalid, value);
        }
        catch (InvalidTimeZoneException)
        {
            return new(IanaZoneOutcome.Invalid, value);
        }
    }
}

public enum IanaZoneOutcome { Missing, Invalid, RuntimeResolvedCandidate }
public sealed record IanaZoneEvaluation(IanaZoneOutcome Outcome, string? RuntimeId);
