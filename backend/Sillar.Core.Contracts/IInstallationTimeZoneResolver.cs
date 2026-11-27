namespace Sillar.Core.Contracts;

/// <summary>
/// La zona horaria con la que trabaja la instalación y su estado de verificación.
/// </summary>
/// <remarks>
/// <para>
/// <b>Decisión de JP:</b> America/Lima es la zona inicial y sigue sin verificar.
/// Sin valor configurado, la zona es <see cref="InitialZoneId"/> con origen
/// <see cref="InstallationTimeZoneOrigin.ApprovedInitial"/>. Con un valor
/// inválido no hay zona: no se cae a la inicial.
/// </para>
/// <para>
/// CORE verifica solo con fuente canónica versionada, identidad de instalación
/// coincidente y una atestación vigente emitida por su operación autorizada.
/// El instalador puede establecer explícitamente America/Lima con esa evidencia.
/// Un valor de respaldo sin atestación nunca se promueve a verificado.
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
    /// <summary>Zona inicial aprobada por JP. El respaldo por sí solo no acredita.</summary>
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

    /// <summary>Evidencia completa, vigente y atribuida por CORE.</summary>
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

    /// <summary>Fuente, instalación y atribución vigente acreditadas por CORE.</summary>
    AttestationAccepted,
}

/// <summary>La zona resuelta, su estado y de qué sale.</summary>
/// <param name="Zone">La zona, si la hay.</param>
/// <param name="ZoneId">El identificador que devolvió el runtime, si hay zona.</param>
/// <param name="Origin">De dónde sale, si hay zona.</param>
/// <param name="Status">Estado observado de verificación.</param>
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
