namespace Sillar.Core.Settings;

/// <summary>
/// Puerto de fuente TZDB: dice si una zona es canónica en una fuente con versión.
/// </summary>
/// <remarks>
/// La fuente no está elegida: <c>tzdata.zi</c> del sistema (no existe en Windows,
/// y su versión cambia con la imagen) o una TZDB empaquetada, que sería una
/// dependencia nueva. Hasta que JP la elija, el único adaptador es
/// <see cref="UnavailableTzdbSource"/>.
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
/// No hay almacenamiento: guardarla necesita filas nuevas (migración), y quién
/// puede atestiguar no está ratificado. El único adaptador es
/// <see cref="NoTimeZoneAttestationStore"/>, que nunca encuentra ninguna.
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
    DateTimeOffset? RevokedAt);

/// <summary>Sin almacenamiento de atestaciones.</summary>
internal sealed class NoTimeZoneAttestationStore : ITimeZoneAttestationStore
{
    public Task<TimeZoneAttestation?> ReadCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<TimeZoneAttestation?>(null);
    }
}
