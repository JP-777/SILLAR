namespace Sillar.Core.Contracts;

/// <summary>
/// Autoridad CORE sobre una operación editorial del Asistente (M12) sobre un
/// artículo concreto. Fase A, candidato: <b>deniega por defecto</b>.
/// </summary>
/// <remarks>
/// <para>
/// Reúne lo que CORE sabe de verdad, leído de la base en el momento de evaluar y
/// en una sola sentencia: la sesión (y si está revocada o caducada), la cuenta
/// (activa, rol, nodo), la instalación y su licencia, y la activación de
/// <c>assistant</c>. No se fía de los claims de la petición más que para saber
/// qué sesión mirar.
/// </para>
/// <para>
/// <b>Nunca devuelve <see cref="EditorialAuthorityOutcome.Allowed"/>.</b> Un rol
/// global es solo condición necesaria: los permisos por módulo y por recurso
/// están diferidos (ADR-010) y el control de licencias también (ADR-004). Con
/// todos los hechos a favor, la decisión es <see cref="EditorialAuthorityOutcome.Denied"/>
/// con <see cref="EditorialDenialReason.ContextualPolicyUnratified"/>.
/// </para>
/// <para>
/// <b>La decisión es una foto.</b> Entre ella y el guardado de M12 puede llegar
/// una revocación, y CORE no puede bloquear filas de <c>core</c> desde la
/// transacción de M12. Quien consuma la decisión guarda
/// <see cref="EditorialActor.SessionId"/> y <see cref="EditorialAuthorityDecision.ObservedAt"/>
/// con su cambio, para poder detectarlo después.
/// </para>
/// <para>
/// Si el token del llamador se cancela, se propaga <see cref="OperationCanceledException"/>;
/// cualquier otro fallo de lectura es <see cref="EditorialDenialReason.AuthorityUnavailable"/>.
/// </para>
/// </remarks>
public interface IAssistantEditorialAuthority
{
    /// <summary>Evalúa una operación sobre un artículo, para el actor de la petición.</summary>
    Task<EditorialAuthorityDecision> EvaluateAsync(EditorialAuthorityRequest request, CancellationToken cancellationToken);
}

/// <summary>Qué se quiere hacer y sobre qué.</summary>
/// <param name="ArticleId">Identificador del artículo en M12. Obligatorio también al crear: lo genera M12 antes de pedir.</param>
/// <param name="Operation">La operación.</param>
public sealed record EditorialAuthorityRequest(string ArticleId, EditorialOperation Operation);

/// <summary>Operaciones editoriales.</summary>
public enum EditorialOperation
{
    /// <summary>Crear un artículo.</summary>
    Create,

    /// <summary>Editar un artículo.</summary>
    Edit,

    /// <summary>Publicarlo.</summary>
    Publish,

    /// <summary>Retirarlo.</summary>
    Retire,

    /// <summary>Configurar el módulo.</summary>
    Configure,
}

/// <summary>Resultado.</summary>
public enum EditorialAuthorityOutcome
{
    /// <summary>Denegado.</summary>
    Denied,

    /// <summary>Reservado a una política ratificada. Nada lo devuelve.</summary>
    Allowed,
}

/// <summary>Por qué se denegó.</summary>
public enum EditorialDenialReason
{
    /// <summary>La petición no trae sesión administrativa.</summary>
    SessionMissing,

    /// <summary>La sesión no existe, o no es de la cuenta que dice la petición.</summary>
    SessionUnknown,

    /// <summary>La sesión fue revocada.</summary>
    SessionRevoked,

    /// <summary>La sesión caducó por inactividad o por antigüedad.</summary>
    SessionExpired,

    /// <summary>La cuenta está desactivada.</summary>
    AccountInactive,

    /// <summary>El rol de la cuenta no es uno de los tres admitidos.</summary>
    RoleUnrecognized,

    /// <summary>No hay instalación.</summary>
    InstallationMissing,

    /// <summary>Clave de instalación vacía, o más de una instalación.</summary>
    InstallationIdentityInvalid,

    /// <summary>La instalación no terminó su configuración inicial.</summary>
    InstallationIncomplete,

    /// <summary>Tipo de licencia fuera de los admitidos.</summary>
    LicenseUnrecognized,

    /// <summary>Licencia temporal sin fecha de fin.</summary>
    LicenseExpiryMissing,

    /// <summary>Licencia perpetua con fecha de fin.</summary>
    LicenseInconsistent,

    /// <summary>Licencia vencida; en el instante exacto del fin ya lo está.</summary>
    LicenseExpired,

    /// <summary><c>assistant</c> no está en <c>core.modules</c>.</summary>
    ModuleMissing,

    /// <summary>Sin activación, o desactivado.</summary>
    ModuleInactive,

    /// <summary>Activación vencida.</summary>
    ModuleExpired,

    /// <summary>El identificador del artículo está vacío o fuera de formato.</summary>
    ResourceInvalid,

    /// <summary>Operación desconocida.</summary>
    OperationUnknown,

    /// <summary>Todos los hechos a favor; no hay política por artículo y operación ratificada.</summary>
    ContextualPolicyUnratified,

    /// <summary>No se pudo leer la autoridad.</summary>
    AuthorityUnavailable,
}

/// <summary>El actor, tal como lo dice la base al evaluar.</summary>
/// <param name="AdminUserId">Id local de la cuenta. Solo identifica junto a <paramref name="HomeNode"/>.</param>
/// <param name="HomeNode">Nodo de pertenencia de la cuenta.</param>
/// <param name="Role">Rol global. No es un permiso sobre el artículo.</param>
/// <param name="SessionId">Sesión evaluada.</param>
public sealed record EditorialActor(int AdminUserId, string HomeNode, string Role, Guid SessionId);

/// <summary>La instalación, por su clave.</summary>
/// <param name="InstallationKey">
/// Identidad persistida de la instalación (<c>installation_key</c>). <b>No es</b>
/// <c>installation_id</c>, que es la clave primaria local de la fila y no se expone.
/// </param>
/// <param name="LicenseType">Tipo de licencia.</param>
/// <param name="LicensedUntil">Fin de la licencia, si tiene.</param>
public sealed record EditorialInstallation(Guid InstallationKey, string LicenseType, DateTimeOffset? LicensedUntil);

/// <summary>La decisión y los hechos de los que sale.</summary>
public sealed record EditorialAuthorityDecision(
    EditorialAuthorityOutcome Outcome,
    EditorialDenialReason Reason,
    EditorialActor? Actor,
    EditorialInstallation? Installation,
    DateTimeOffset ObservedAt)
{
    /// <summary>Verdadero solo con <see cref="EditorialAuthorityOutcome.Allowed"/>, que nada devuelve.</summary>
    public bool IsAllowed => Outcome == EditorialAuthorityOutcome.Allowed;
}
