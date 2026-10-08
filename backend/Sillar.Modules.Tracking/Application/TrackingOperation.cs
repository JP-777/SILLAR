namespace Sillar.Modules.Tracking.Application;

internal enum TrackingOutcome
{
    Ok,
    NotFound,
    Invalid
}

internal sealed record TrackingOperation<T>(
    TrackingOutcome Outcome,
    string? Error = null,
    string? Field = null,
    T? Value = default);
