namespace Sillar.Core.Contracts;

/// <summary>
/// La zona horaria con la que trabaja la instalación. Fase B, candidato.
/// </summary>
/// <remarks>
/// <para>
/// <b>Decisión de JP (25/10/2026):</b> America/Lima es la zona horaria inicial, y
/// no por eso queda verificada.
/// </para>
/// <para>
/// Sin valor configurado en <see cref="IanaZoneCandidate.SettingKey"/>, la zona
/// es <see cref="InitialZoneId"/> con origen <see cref="InstallationTimeZoneOrigin.ApprovedInitial"/>.
/// Con un valor que el runtime resuelve, es esa, con origen
/// <see cref="InstallationTimeZoneOrigin.ConfiguredSetting"/>. Con un valor
/// inválido <b>no hay zona</b>: un valor mal escrito es una avería que hay que
/// ver, no algo que la zona inicial pueda tapar.
/// </para>
/// <para>
/// <b>Nunca verificada.</b> No existe ninguna autoridad de verificación, así que
/// <see cref="InstallationTimeZoneResolution.Verification"/> es siempre
/// <see cref="InstallationTimeZoneVerification.Unverified"/>. Frescura: la de
/// <see cref="ISettingsReader"/>, que en el host es una caché singleton.
/// </para>
/// </remarks>
public interface IInstallationTimeZoneResolver
{
    /// <summary>Zona inicial aprobada por JP el 25/10/2026.</summary>
    const string InitialZoneId = "America/Lima";

    /// <summary>Resuelve la zona vigente.</summary>
    InstallationTimeZoneResolution Resolve();
}

/// <summary>De dónde sale la zona.</summary>
public enum InstallationTimeZoneOrigin
{
    /// <summary>Del valor guardado en <see cref="IanaZoneCandidate.SettingKey"/>.</summary>
    ConfiguredSetting,

    /// <summary>De la zona inicial aprobada, porque no hay valor guardado.</summary>
    ApprovedInitial,
}

/// <summary>Si la zona está verificada.</summary>
public enum InstallationTimeZoneVerification
{
    /// <summary>Sin verificar. Es el único valor que se produce hoy.</summary>
    Unverified,

    /// <summary>Reservado a una autoridad de verificación ratificada. Nada lo devuelve.</summary>
    Verified,
}

/// <summary>Por qué no hay zona.</summary>
public enum InstallationTimeZoneFailure
{
    /// <summary>El valor guardado no es una zona que el runtime resuelva. No se cae a la inicial.</summary>
    ConfiguredValueInvalid,

    /// <summary>El runtime no tiene la zona inicial en su base de zonas.</summary>
    InitialZoneUnresolvable,
}

/// <summary>La zona resuelta, o por qué no la hay.</summary>
/// <param name="Zone">La zona, si se resolvió.</param>
/// <param name="ZoneId">El identificador que devolvió el runtime, si se resolvió.</param>
/// <param name="Origin">De dónde sale, si se resolvió.</param>
/// <param name="Verification">Siempre <see cref="InstallationTimeZoneVerification.Unverified"/>.</param>
/// <param name="Failure">La causa, si no se resolvió.</param>
/// <param name="Setting">Lo que se leyó de la configuración, sin tocar.</param>
public sealed record InstallationTimeZoneResolution(
    TimeZoneInfo? Zone,
    string? ZoneId,
    InstallationTimeZoneOrigin? Origin,
    InstallationTimeZoneVerification Verification,
    InstallationTimeZoneFailure? Failure,
    IanaZoneEvaluation Setting)
{
    /// <summary>Verdadero si hay zona utilizable.</summary>
    public bool IsResolved => Zone is not null;
}
