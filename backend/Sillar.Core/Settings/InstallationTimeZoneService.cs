using Microsoft.EntityFrameworkCore;
using Sillar.Core.Authentication;
using Sillar.Core.Auditing;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain;

namespace Sillar.Core.Settings;

internal sealed record TimeZoneChangeResult(bool Changed, int Status, string? Error, Guid? AttestationId = null);

/// <summary>Única operación para acreditar una zona; el rol se comprueba aquí, además de HTTP.</summary>
internal sealed class InstallationTimeZoneService(
    CoreDbContext database,
    CurrentAdmin current,
    ITzdbSource source,
    AuditWriter audit,
    SettingsCache cache,
    TimeProvider clock)
{
    internal const string AuthorityVersion = "super_admin-installation:v1";
    internal const long ChangeLock = 5_111_401_003;

    public async Task<TimeZoneChangeResult> EstablishAsync(string? zoneId, CancellationToken ct)
    {
        if (current.SessionId is not { } sessionId || current.AdminUserId is not { } userId)
            return new(false, 403, "Se necesita una sesión de superadministrador para acreditar la zona.");
        var session = await database.AdminSessions.AsNoTracking().Include(s => s.AdminUser)
            .SingleOrDefaultAsync(s => s.AdminSessionId == sessionId && s.AdminUserId == userId, ct);
        if (session?.AdminUser is not { IsActive: true, Role: AdminRole.SuperAdmin } actor
            || session.ExpiresAt <= clock.GetUtcNow() || session.IssuedAt > clock.GetUtcNow()
            || SessionPolicy.Evaluate(session.IssuedAt, session.LastSeenAt, session.RevokedAt, clock.GetUtcNow()) != SessionState.Valid)
            return new(false, 403, "La sesión ya no autoriza acreditar la zona de la instalación.");
        if (string.IsNullOrWhiteSpace(actor.FullName) || string.IsNullOrWhiteSpace(actor.HomeNode))
            return new(false, 403, "La cuenta no tiene nombre y nodo completos para conservar su atribución.");

        var candidate = IanaZoneCandidate.Evaluate(zoneId);
        if (candidate.Outcome != IanaZoneOutcome.RuntimeResolvedCandidate || candidate.RuntimeId != zoneId)
            return new(false, 400, "Indica una zona IANA exacta que esté disponible en el servidor.");
        var observed = source.Observe(zoneId!);
        if (!observed.IsAvailable)
            return new(false, 503, "No se puede verificar la fuente de reglas horarias del servidor.");
        if (!observed.IsCanonical)
            return new(false, 400, "Usa el identificador canónico de la zona IANA, no un alias.");

        await using var tx = await database.Database.BeginTransactionAsync(ct);
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", [ChangeLock], ct);
        var installation = await database.Installations.AsNoTracking().SingleOrDefaultAsync(ct);
        if (installation is not { IsSetupComplete: true } || installation.InstallationKey == Guid.Empty)
            return new(false, 409, "La identidad de la instalación no está preparada.");
        var setting = await database.SiteSettings.SingleOrDefaultAsync(s => s.SettingKey == IanaZoneCandidate.SettingKey, ct);
        if (setting is null)
        {
            setting = NewSetting(zoneId!);
            database.SiteSettings.Add(setting);
        }
        setting.SettingValue = zoneId!;
        setting.IsActive = true;
        setting.IsPublic = false;
        var attestation = NewAttestation(installation.InstallationKey, zoneId!, observed, actor, clock.GetUtcNow());
        database.TimeZoneAttestations.Add(attestation);
        await audit.WriteAsync(new AuditEntry(AuditAction.Update)
        {
            AdminUserId = actor.AdminUserId, AdminUserEmail = actor.Email,
            AdminUserName = actor.FullName, AdminUserHomeNode = actor.HomeNode,
            ModuleCode = "core", EntityType = "installation_time_zone", EntityId = attestation.AttestationId.ToString(),
            Summary = $"Zona de la instalación acreditada: {zoneId}.",
        }, ct);
        await tx.CommitAsync(ct);
        cache.Invalidate();
        return new(true, 200, null, attestation.AttestationId);
    }

    /// <summary>El instalador ya autorizó crear su primera cuenta; todo queda en su transacción.</summary>
    internal static void StageInitial(CoreDbContext database, Guid installationKey, AdminUser actor, DateTimeOffset now)
    {
        var observed = new SystemTzdbSource().Observe(IInstallationTimeZoneResolver.InitialZoneId);
        // Sin fuente no se inventa un positivo. La pantalla permite acreditarla
        // cuando el runtime real aporte sus datos, sin migrar desde el panel.
        if (!observed.IsAvailable || !observed.IsCanonical) return;
        database.SiteSettings.Add(NewSetting(IInstallationTimeZoneResolver.InitialZoneId));
        database.TimeZoneAttestations.Add(NewAttestation(installationKey,
            IInstallationTimeZoneResolver.InitialZoneId, observed, actor, now));
    }

    private static SiteSetting NewSetting(string zoneId) => new()
    {
        SettingKey = IanaZoneCandidate.SettingKey, SettingValue = zoneId, ValueType = Sillar.Core.Domain.Values.SettingValueType.Text,
        IsPublic = false, IsActive = true, Description = "Zona IANA de la instalación, con evidencia de reglas y autoría.",
    };

    private static InstallationTimeZoneAttestation NewAttestation(Guid installationKey, string zoneId,
        TzdbObservation source, AdminUser actor, DateTimeOffset now) => new()
    {
        InstallationKey = installationKey, ZoneId = zoneId, SourceName = source.SourceName!,
        SourceVersion = source.SourceVersion!, AttestedByName = actor.FullName,
        AttestedByAdminUserLocalId = actor.AdminUserId, AttestedByAdminUserHomeNode = actor.HomeNode,
        AttestedAt = now, AuthorityVersion = AuthorityVersion,
    };
}
