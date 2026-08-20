namespace Sillar.Modules.Cms.Services;

/// <summary>Cómo terminó una operación de contenido.</summary>
internal enum CmsOutcome
{
    Ok,
    NotFound,
    Invalid,
    Conflict
}

/// <summary>Resultado tipado que los endpoints convertirán después a HTTP.</summary>
internal sealed record CmsOperation<T>(
    CmsOutcome Outcome,
    string? Error = null,
    T? Value = default);
