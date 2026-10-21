using Npgsql;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Endpoints;

namespace Sillar.Modules.Assistant.Services;

/// <summary>
/// Puerta exclusiva para escrituras administrativas de cuotas. Su productor
/// debe ser una autoridad confiable del host (identidad + permiso de escritura
/// + entitlement según CORE). NO registrar implementaciones de prueba en DI.
/// </summary>
public interface ITrustedAssistantQuotaPolicyWriter
{
    bool CanWritePolicy(Guid installationId);
}

public sealed record DurableQuotaPolicyChange(
    AssistantMode Mode, bool EngineConfigured, long LimitUnits, int ConcurrencyLimit);

public sealed record DurableQuotaPolicySnapshot(
    Guid InstallationId, AssistantMode Mode, bool EngineConfigured,
    long LimitUnits, long ConsumedUnits, long ReservedUnits,
    int ConcurrencyLimit, int RunningRequests, long PolicyVersion);

public enum DurableQuotaPolicyOutcome
{
    Updated, AlreadyCurrent, Denied, Invalid, NotFound, StaleVersion, CapacityConflict
}

public sealed record DurableQuotaPolicyResult(
    DurableQuotaPolicyOutcome Outcome, DurableQuotaPolicySnapshot? Snapshot = null);

/// <summary>
/// CANDIDATO P6: lecturas y CAS de políticas existentes en assistant.quota_accounts.
/// No crea instalaciones, cuentas, derechos comerciales ni proveedores.
/// NO registra endpoints ni activa IA. Las escrituras no son accesibles sin
/// autoridad de política explícita distinta del scope de instalación.
/// </summary>
public sealed class DurableAssistantQuotaPolicyStore(
    NpgsqlDataSource dataSource,
    ITrustedAssistantInstallationScope installationScope,
    ITrustedAssistantQuotaPolicyWriter policyWriter)
{
    private bool TrustedScope(out Guid installationId)
    {
        installationId = Guid.Empty;
        try
        {
            if (!installationScope.TryGetEnabledInstallation(out installationId) || installationId == Guid.Empty)
            {
                installationId = Guid.Empty;
                return false;
            }
            return true;
        }
        catch
        {
            installationId = Guid.Empty;
            return false;
        }
    }

    private bool AuthorizedWriter(out Guid installationId)
    {
        if (!TrustedScope(out installationId)) return false;
        try { return policyWriter.CanWritePolicy(installationId); }
        catch { return false; } // autoridad averiada = DENY; nunca fallback permisivo
    }

    public static bool ValidPolicy(DurableQuotaPolicyChange? policy)
    {
        if (policy is null) return false;
        var mode = (short)policy.Mode;
        return mode is >= 0 and <= 2
            && policy.LimitUnits >= 0 && policy.ConcurrencyLimit >= 0
            && (mode != 0 || !policy.EngineConfigured);
    }

    /// <summary>Evita sobreventa al reducir topes con consumo y reservas vivos.</summary>
    public static bool FitsOutstanding(DurableQuotaPolicyChange policy,
        long consumed, long reserved, int running)
        => ValidPolicy(policy) && consumed >= 0 && reserved >= 0 && running >= 0
            && policy.LimitUnits >= consumed
            && reserved <= policy.LimitUnits - consumed
            && policy.ConcurrencyLimit >= running;

    public async Task<DurableQuotaPolicyResult> ReadAsync(CancellationToken ct = default)
    {
        if (!TrustedScope(out var id)) return new(DurableQuotaPolicyOutcome.Denied);
        await using var db = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT mode, engine_configured, limit_units, consumed_units, reserved_units,
                   concurrency_limit, running_requests, policy_version
            FROM assistant.quota_accounts WHERE installation_id = @i
            """, db);
        cmd.Parameters.AddWithValue("i", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return new(DurableQuotaPolicyOutcome.NotFound);
        return new(DurableQuotaPolicyOutcome.AlreadyCurrent, Map(id, reader));
    }

    public async Task<DurableQuotaPolicyResult> TryUpdateAsync(
        long expectedVersion, DurableQuotaPolicyChange? change, CancellationToken ct = default)
    {
        if (!AuthorizedWriter(out var id)) return new(DurableQuotaPolicyOutcome.Denied);
        if (expectedVersion < 0 || !ValidPolicy(change)) return new(DurableQuotaPolicyOutcome.Invalid);
        var desired = change!;
        await using var db = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        DurableQuotaPolicySnapshot current;
        await using (var cmd = new NpgsqlCommand("""
            SELECT mode, engine_configured, limit_units, consumed_units, reserved_units,
                   concurrency_limit, running_requests, policy_version
            FROM assistant.quota_accounts WHERE installation_id = @i FOR UPDATE
            """, db, tx))
        {
            cmd.Parameters.AddWithValue("i", id);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return new(DurableQuotaPolicyOutcome.NotFound);
            current = Map(id, reader);
        }
        if (current.PolicyVersion != expectedVersion)
            return new(DurableQuotaPolicyOutcome.StaleVersion, current);
        if (!FitsOutstanding(desired, current.ConsumedUnits, current.ReservedUnits, current.RunningRequests))
            return new(DurableQuotaPolicyOutcome.CapacityConflict, current);
        if (current.Mode == desired.Mode && current.EngineConfigured == desired.EngineConfigured
            && current.LimitUnits == desired.LimitUnits
            && current.ConcurrencyLimit == desired.ConcurrencyLimit)
            return new(DurableQuotaPolicyOutcome.AlreadyCurrent, current);
        if (current.PolicyVersion == long.MaxValue)
            return new(DurableQuotaPolicyOutcome.CapacityConflict, current);

        await using (var update = new NpgsqlCommand("""
            UPDATE assistant.quota_accounts
            SET mode = @m, engine_configured = @e, limit_units = @l,
                concurrency_limit = @c, policy_version = policy_version + 1,
                updated_at = now()
            WHERE installation_id = @i AND policy_version = @v
            """, db, tx))
        {
            update.Parameters.AddWithValue("i", id);
            update.Parameters.AddWithValue("v", expectedVersion);
            update.Parameters.AddWithValue("m", (short)desired.Mode);
            update.Parameters.AddWithValue("e", desired.EngineConfigured);
            update.Parameters.AddWithValue("l", desired.LimitUnits);
            update.Parameters.AddWithValue("c", desired.ConcurrencyLimit);
            if (await update.ExecuteNonQueryAsync(ct) != 1)
                return new(DurableQuotaPolicyOutcome.StaleVersion, current);
        }
        await tx.CommitAsync(ct);
        return new(DurableQuotaPolicyOutcome.Updated, current with
        {
            Mode = desired.Mode,
            EngineConfigured = desired.EngineConfigured,
            LimitUnits = desired.LimitUnits,
            ConcurrencyLimit = desired.ConcurrencyLimit,
            PolicyVersion = expectedVersion + 1
        });
    }

    private static DurableQuotaPolicySnapshot Map(Guid installationId, NpgsqlDataReader reader)
        => new(installationId, (AssistantMode)reader.GetInt16(0), reader.GetBoolean(1),
            reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4),
            reader.GetInt32(5), reader.GetInt32(6), reader.GetInt64(7));
}
