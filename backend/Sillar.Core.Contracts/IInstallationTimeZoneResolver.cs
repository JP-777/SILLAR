namespace Sillar.Core.Contracts;

/// <summary>
/// La zona horaria con la que trabaja la instalación y su estado de verificación.
/// Fase A, candidato.
/// </summary>
/// <remarks>
/// <para>
/// <b>Decisión de JP:</b> America/Lima es la zona inicial y sigue sin verificar.
/// Sin valor configurado, la zona es <see cref="InitialZoneId"/> con origen
/// <see cref="InstallationTimeZoneOrigin.ApprovedInitial"/>. Con un valor
/// inválido no hay zona: no se cae a la inicial.
/// </para>
/// <para>
/// <b>Ningún camino devuelve <see cref="TimeZoneVerificationStatus.Verified"/>.</b>
/// Haría falta una fuente TZDB ratificada, la identidad de la instalación y una
/// atestación legítima de un actor con autoridad. Mientras la fuente y la
/// autoridad de atestación no estén ratificadas, incluso con todos los hechos a
/// favor el estado es <see cref="TimeZoneVerificationStatus.Unverified"/> con
/// <see cref="TimeZoneVerificationReason.AttestationAuthorityUnratified"/>.
/// </para>
/// <para>
/// Quien consuma la zona estampa <see cref="InstallationTimeZoneResolution.AttestationId"/> y
/// <see cref="InstallationTimeZoneResolution.ObservedAt"/>: un estado vale para el
/// instante en que se observó, no para después.
/// </para>
/// <para>
/// Si el token del llamador se cancela, se propaga <see cref="OperationCanceledException"/>.
/// </para>
/// </remarks>
public interface IInstallationTimeZoneResolver
{
    /// <summary>Zona inicial aprobada por JP. Nunca verificada.</summary>
    const string InitialZoneId = "America/Lima";

    /// <summary>Resuelve la zona vigente y su estado.</summary>
    Task<InstallationTimeZoneResolution> ResolveAsync(CancellationToken cancellationToken);
}

/// <summary>De dónde sale la zona.</summary>
public enum InstallationTimeZoneOrigin
{
    /// <summary>Del valor guardado en <see cref="IanaZoneCandidate.SettingKey"/>.</summary>
    ConfiguredSetting,

    /// <summary>De la zona inicial aprobada, porque no hay valor guardado.</summary>
    ApprovedInitial,
}

/// <summary>Estado de verificación de la zona.</summary>
public enum TimeZoneVerificationStatus
{
    /// <summary>Sin verificar.</summary>
    Unverified,

    /// <summary>Hay atestación, pero los hechos cambiaron desde que se hizo.</summary>
    Stale,

    /// <summary>La atestación vigente fue revocada.</summary>
    Revoked,

    /// <summary>No hay fuente TZDB con la que comprobar la zona.</summary>
    SourceNotVerifiable,

    /// <summary>Reservado. Ningún camino de Fase A lo devuelve.</summary>
    Verified,
}

/// <summary>Por qué la zona tiene ese estado.</summary>
public enum TimeZoneVerificationReason
{
    /// <summary>Es la zona inicial: nunca se verifica.</summary>
    InitialZoneNotAttestable,

    /// <summary>El valor guardado no es una zona que el runtime resuelva. No hay zona.</summary>
    ConfiguredValueInvalid,

    /// <summary>El runtime no tiene la zona inicial. No hay zona.</summary>
    InitialZoneUnresolvable,

    /// <summary>No hay fuente TZDB disponible.</summary>
    TzdbSourceUnavailable,

    /// <summary>La fuente no la tiene como zona canónica: es un alias o no está.</summary>
    NotCanonicalInSource,

    /// <summary>No hay atestación.</summary>
    NotAttested,

    /// <summary>No se pudo leer la atestación.</summary>
    AttestationUnavailable,

    /// <summary>No se pudo leer una identidad única de la instalación.</summary>
    InstallationUnavailable,

    /// <summary>La atestación fue revocada.</summary>
    AttestationRevoked,

    /// <summary>La atestación es de otro valor de zona.</summary>
    StaleValueChanged,

    /// <summary>La atestación es de otra fuente o de otra versión de la fuente.</summary>
    StaleSourceChanged,

    /// <summary>La atestación es de otra instalación.</summary>
    StaleInstallationChanged,

    /// <summary>Todos los hechos coinciden, pero la autoridad de atestación no está ratificada.</summary>
    AttestationAuthorityUnratified,
}

/// <summary>La zona resuelta, su estado y de qué sale.</summary>
/// <param name="Zone">La zona, si la hay.</param>
/// <param name="ZoneId">El identificador que devolvió el runtime, si hay zona.</param>
/// <param name="Origin">De dónde sale, si hay zona.</param>
/// <param name="Status">Estado de verificación. Nunca <see cref="TimeZoneVerificationStatus.Verified"/> en Fase A.</param>
/// <param name="Reason">Por qué.</param>
/// <param name="Setting">Lo que se leyó de la configuración, sin tocar.</param>
/// <param name="AttestationId">La atestación evaluada, si había una.</param>
/// <param name="ObservedAt">El instante de la observación.</param>
public sealed record InstallationTimeZoneResolution(
    TimeZoneInfo? Zone,
    string? ZoneId,
    InstallationTimeZoneOrigin? Origin,
    TimeZoneVerificationStatus Status,
    TimeZoneVerificationReason Reason,
    IanaZoneEvaluation Setting,
    Guid? AttestationId,
    DateTimeOffset ObservedAt)
{
    /// <summary>Verdadero si hay zona utilizable, verificada o no.</summary>
    public bool IsResolved => Zone is not null;
}
