using Sillar.Modules.Assistant.Domain;

namespace Sillar.Modules.Assistant.Services;

/// <summary>
/// Principal verificado por un futuro adaptador CORE. Nunca construir con datos
/// proporcionados libremente por un visitante o recibidos en el cuerpo HTTP.
/// </summary>
public sealed record TrustedQuotaAdministrator(
    Guid InstallationId, string Role, string AuditActorKey, bool HasScopeAuthority);

public enum QuotaPolicyUpdateOutcome
{
    Updated,
    Unchanged,
    Forbidden,
    NotFound,
    Invalid,
    Conflict,
    InFlight
}

/// <summary>Lectura restringida de la politica vigente, no accesible al visitante.</summary>
public sealed record LocalQuotaPolicySnapshot(LocalQuotaConfiguration Configuration, long Version);

/// <summary>
/// Prueba auditable local de un cambio. No es evidencia durable ni reemplaza
/// auditoria de CORE. No almacena preguntas, transcripciones ni claves del motor.
/// </summary>
public sealed record LocalQuotaPolicyChange(
    Guid InstallationId,
    string ActorAuditKey,
    long PreviousVersion,
    long NewVersion,
    LocalQuotaConfiguration Previous,
    LocalQuotaConfiguration Current,
    DateTimeOffset ChangedAt);
