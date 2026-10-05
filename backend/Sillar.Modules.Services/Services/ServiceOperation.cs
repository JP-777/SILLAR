namespace Sillar.Modules.Services.Services;
internal enum ServiceOutcome { Ok, NotFound, Invalid, Conflict }
internal sealed record ServiceOperation<T>(ServiceOutcome Outcome, string? Error = null, T? Value = default);
