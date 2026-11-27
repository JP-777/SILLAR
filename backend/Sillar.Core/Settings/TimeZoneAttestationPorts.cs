namespace Sillar.Core.Settings;

/// <summary>
/// Puerto de fuente TZDB: dice si una zona es canónica en una fuente con versión.
/// </summary>
/// <remarks>
/// WEB usa tzdata.zi del runtime Linux, con versión y huella. La ausencia de
/// fuente se mantiene distinguible y nunca se convierte en un positivo.
/// </remarks>
internal interface ITzdbSource
{
    TzdbObservation Observe(string zoneId);
}

/// <summary>Lo que la fuente dice de una zona.</summary>
/// <param name="IsAvailable">Si hay fuente. Sin fuente, nada de lo demás significa algo.</param>
/// <param name="SourceName">Nombre estable de la fuente.</param>
/// <param name="SourceVersion">Versión de los datos (por ejemplo <c>2026e</c>).</param>
/// <param name="IsCanonical">Si la zona es canónica en esa fuente. Un alias no lo es.</param>
internal sealed record TzdbObservation(bool IsAvailable, string? SourceName, string? SourceVersion, bool IsCanonical)
{
    public static TzdbObservation Unavailable { get; } = new(false, null, null, false);
}

/// <summary>Sin fuente TZDB elegida.</summary>
internal sealed class UnavailableTzdbSource : ITzdbSource
{
    public TzdbObservation Observe(string zoneId) => TzdbObservation.Unavailable;
}

/// <summary>
/// Puerto de atestación: la constancia, firmada por un actor, de que una zona es la
/// de esta instalación.
/// </summary>
/// <remarks>
/// CORE conserva la evidencia local sin FK al administrador. La configuración
/// vigente y la evidencia se leen en una sentencia para evitar estados mezclados.
/// </remarks>
internal interface ITimeZoneAttestationStore
{
    Task<TimeZoneAttestation?> ReadCurrentAsync(CancellationToken cancellationToken);
}

/// <summary>Una atestación de zona. Solo hechos; ninguno basta por sí solo.</summary>
internal sealed record TimeZoneAttestation(
    Guid AttestationId,
    Guid InstallationKey,
    string ZoneId,
    string SourceName,
    string SourceVersion,
    int AttestedByAdminUserId,
    string AttestedByHomeNode,
    DateTimeOffset AttestedAt,
    DateTimeOffset? RevokedAt,
    string? AttestedByName = null,
    string? AuthorityVersion = null);

/// <summary>Sin almacenamiento de atestaciones.</summary>
internal sealed class NoTimeZoneAttestationStore : ITimeZoneAttestationStore
{
    public Task<TimeZoneAttestation?> ReadCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<TimeZoneAttestation?>(null);
    }
}
