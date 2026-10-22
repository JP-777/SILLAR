using Npgsql;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Endpoints;

namespace Sillar.Modules.Assistant.Services;

/// <summary>
/// CANDIDATO P4 / PG-06. Ledger PostgreSQL aislado de M12.
/// No se registra en DI ni autoriza invocar proveedores: se requiere una fuente
/// confiable del host y una verificación física independiente antes de utilizarlo.
/// </summary>
public enum DurableQuotaOutcome
{
    Reserved, Replay, FaqOnly, FaqFallback, Invalid, Denied, NotFound,
    Dispatched, AlreadyDispatched, Uncertain, Completed, AlreadyCompleted,
    Cancelled, AlreadyCancelled, Conflict
}

public sealed record DurableQuotaResult(
    DurableQuotaOutcome Outcome, Guid? ReservationId = null, AssistantMode? Mode = null);

public sealed record OutstandingQuotaReservation(
    Guid ReservationId, string State, long WorstCaseUnits, DateTimeOffset CreatedAt);

public sealed class DurableAssistantQuotaLedger(
    NpgsqlDataSource dataSource, ITrustedAssistantInstallationScope installationScope)
{
    /// <summary>
    /// Clave generada y gestionada por servidor, nunca un permiso de gasto enviado
    /// por un cliente. Esta validación no acredita su origen; lo garantiza el host.
    /// </summary>
    public static bool ValidIdempotencyKey(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
        value.All(ch => ch is >= '!' and <= '~');

    private bool Trusted(out Guid id)
    {
        id = Guid.Empty;
        try
        {
            if (!installationScope.TryGetEnabledInstallation(out id) || id == Guid.Empty)
            {
                id = Guid.Empty;
                return false;
            }
            return true;
        }
        catch
        {
            // CORE/Host indisponible o fallando: DENY sin tocar PostgreSQL.
            id = Guid.Empty;
            return false;
        }
    }

    public async Task<DurableQuotaResult> TryReserveAsync(
        long worstCaseUnits, string idempotencyKey, CancellationToken ct = default)
    {
        if (!Trusted(out var installationId)) return new(DurableQuotaOutcome.Denied);
        if (worstCaseUnits <= 0 || !ValidIdempotencyKey(idempotencyKey))
            return new(DurableQuotaOutcome.Invalid);

        await using var db = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        var account = await LockAccountAsync(db, tx, installationId, ct);
        if (account is null) return new(DurableQuotaOutcome.NotFound);

        await using (var existingCommand = Cmd(db, tx, """
            SELECT reservation_id, state, worst_case_units FROM assistant.quota_reservations
            WHERE installation_id = @i AND idempotency_key = @k
            """))
        {
            existingCommand.Parameters.AddWithValue("i", installationId);
            existingCommand.Parameters.AddWithValue("k", idempotencyKey);
            await using var reader = await existingCommand.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0);
                var state = reader.GetString(1);
                var units = reader.GetInt64(2);
                var status = units != worstCaseUnits ? DurableQuotaOutcome.Conflict : state switch
                {
                    "reserved" => DurableQuotaOutcome.Replay,
                    "dispatched" => DurableQuotaOutcome.AlreadyDispatched,
                    "uncertain" => DurableQuotaOutcome.Uncertain,
                    "completed" => DurableQuotaOutcome.AlreadyCompleted,
                    _ => DurableQuotaOutcome.Conflict
                };
                return new(status, id, account.Mode);
            }
        }

        var plan = AssistantUsePolicy.Evaluate(installationId, new ResourceBudgetSnapshot(
            installationId, account.Mode, account.EngineConfigured, account.Limit,
            account.Consumed, account.Reserved, worstCaseUnits, account.ConcurrencyLimit,
            account.Running));
        switch (plan)
        {
            case AdmissionPlan.FaqOnly: return new(DurableQuotaOutcome.FaqOnly);
            case AdmissionPlan.FaqFallback: return new(DurableQuotaOutcome.FaqFallback);
            case AdmissionPlan.InvalidConfiguration: return new(DurableQuotaOutcome.Invalid);
        }
        if (account.Running == int.MaxValue) return new(DurableQuotaOutcome.FaqFallback);

        var token = Guid.NewGuid();
        await ExecuteAsync(db, tx, """
            INSERT INTO assistant.quota_reservations
            (installation_id, reservation_id, idempotency_key, mode, worst_case_units, state, created_at)
            VALUES (@i, @r, @k, @m, @w, 'reserved', now())
            """, ct, ("i", installationId), ("r", token), ("k", idempotencyKey),
            ("m", (short)account.Mode), ("w", worstCaseUnits));
        await ExecuteAsync(db, tx, """
            UPDATE assistant.quota_accounts
            SET reserved_units = reserved_units + @w,
                running_requests = running_requests + 1, updated_at = now()
            WHERE installation_id = @i
            """, ct, ("w", worstCaseUnits), ("i", installationId));
        await tx.CommitAsync(ct);
        return new(DurableQuotaOutcome.Reserved, token, account.Mode);
    }

    public Task<DurableQuotaResult> MarkDispatchedAsync(Guid reservationId, CancellationToken ct = default)
        => TransitionAsync(reservationId, "dispatch", null, ct);

    /// <summary>No libera presupuesto hasta disponer de una prueba verificable del proveedor.</summary>
    public Task<DurableQuotaResult> MarkUncertainAsync(Guid reservationId, CancellationToken ct = default)
        => TransitionAsync(reservationId, "uncertain", null, ct);

    public Task<DurableQuotaResult> CompleteAsync(Guid reservationId, long actualUnits, CancellationToken ct = default)
        => TransitionAsync(reservationId, "complete", actualUnits, ct);

    public Task<DurableQuotaResult> CancelBeforeDispatchAsync(Guid reservationId, CancellationToken ct = default)
        => TransitionAsync(reservationId, "cancel", null, ct);

    private async Task<DurableQuotaResult> TransitionAsync(
        Guid reservationId, string action, long? actual, CancellationToken ct)
    {
        if (!Trusted(out var installationId)) return new(DurableQuotaOutcome.Denied);
        if (reservationId == Guid.Empty || (actual is < 0)) return new(DurableQuotaOutcome.Invalid);

        await using var db = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        var account = await LockAccountAsync(db, tx, installationId, ct);
        if (account is null) return new(DurableQuotaOutcome.NotFound);
        var lease = await LockReservationAsync(db, tx, installationId, reservationId, ct);
        if (lease is null) return new(DurableQuotaOutcome.NotFound);

        if (action == "dispatch")
        {
            if (lease.State == "dispatched") return new(DurableQuotaOutcome.AlreadyDispatched, reservationId);
            if (lease.State == "uncertain") return new(DurableQuotaOutcome.Uncertain, reservationId);
            if (lease.State != "reserved") return new(DurableQuotaOutcome.Conflict, reservationId);
            await ExecuteAsync(db, tx, """
                UPDATE assistant.quota_reservations SET state = 'dispatched', dispatched_at = now()
                WHERE installation_id = @i AND reservation_id = @r
                """, ct, ("i", installationId), ("r", reservationId));
            await tx.CommitAsync(ct);
            return new(DurableQuotaOutcome.Dispatched, reservationId);
        }
        if (action == "uncertain")
        {
            if (lease.State == "uncertain") return new(DurableQuotaOutcome.Uncertain, reservationId);
            if (lease.State != "dispatched") return new(DurableQuotaOutcome.Conflict, reservationId);
            await ExecuteAsync(db, tx, """
                UPDATE assistant.quota_reservations SET state = 'uncertain'
                WHERE installation_id = @i AND reservation_id = @r
                """, ct, ("i", installationId), ("r", reservationId));
            await tx.CommitAsync(ct);
            return new(DurableQuotaOutcome.Uncertain, reservationId);
        }
        if (action == "complete")
        {
            if (lease.State == "completed")
                return new(lease.Actual == actual ? DurableQuotaOutcome.AlreadyCompleted : DurableQuotaOutcome.Conflict,
                    reservationId);
            if (lease.State != "dispatched" || actual is null || actual > lease.Worst)
                return new(DurableQuotaOutcome.Conflict, reservationId);
            await ExecuteAsync(db, tx, """
                UPDATE assistant.quota_reservations SET state = 'completed',
                    actual_units = @a, settled_at = now()
                WHERE installation_id = @i AND reservation_id = @r
                """, ct, ("a", actual.Value), ("i", installationId), ("r", reservationId));
            await ExecuteAsync(db, tx, """
                UPDATE assistant.quota_accounts
                SET reserved_units = reserved_units - @w,
                    consumed_units = consumed_units + @a,
                    running_requests = running_requests - 1, updated_at = now()
                WHERE installation_id = @i
                """, ct, ("w", lease.Worst), ("a", actual.Value), ("i", installationId));
            await tx.CommitAsync(ct);
            return new(DurableQuotaOutcome.Completed, reservationId);
        }
        if (action == "cancel")
        {
            if (lease.State == "cancelled") return new(DurableQuotaOutcome.AlreadyCancelled, reservationId);
            if (lease.State != "reserved") return new(DurableQuotaOutcome.Conflict, reservationId);
            await ExecuteAsync(db, tx, """
                UPDATE assistant.quota_reservations SET state = 'cancelled', settled_at = now()
                WHERE installation_id = @i AND reservation_id = @r
                """, ct, ("i", installationId), ("r", reservationId));
            await ExecuteAsync(db, tx, """
                UPDATE assistant.quota_accounts
                SET reserved_units = reserved_units - @w,
                    running_requests = running_requests - 1, updated_at = now()
                WHERE installation_id = @i
                """, ct, ("w", lease.Worst), ("i", installationId));
            await tx.CommitAsync(ct);
            return new(DurableQuotaOutcome.Cancelled, reservationId);
        }
        return new(DurableQuotaOutcome.Invalid);
    }

    /// <summary>
    /// Recuperación conservadora: enumera reservas cuyo consumo aún puede estar
    /// pendiente o incierto. No cancela, no reenvía y no libera presupuesto.
    /// Su uso requiere el ámbito host verificado; jamás exponer como HTTP público.
    /// </summary>
    public async Task<IReadOnlyList<OutstandingQuotaReservation>> ListOutstandingAsync(
        int maxResults = 100, CancellationToken ct = default)
    {
        if (!Trusted(out var installationId) || maxResults is < 1 or > 100)
            return [];
        await using var db = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT reservation_id, state, worst_case_units, created_at
            FROM assistant.quota_reservations
            WHERE installation_id = @i AND state IN ('dispatched', 'uncertain')
            ORDER BY created_at, reservation_id LIMIT @lim
            """;
        cmd.Parameters.AddWithValue("i", installationId);
        cmd.Parameters.AddWithValue("lim", maxResults);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var output = new List<OutstandingQuotaReservation>();
        while (await reader.ReadAsync(ct))
            output.Add(new(reader.GetGuid(0), reader.GetString(1),
                reader.GetInt64(2), reader.GetFieldValue<DateTimeOffset>(3)));
        return output;
    }

    private sealed record Account(
        AssistantMode Mode, bool EngineConfigured, long Limit, long Consumed,
        long Reserved, int ConcurrencyLimit, int Running);
    private sealed record Lease(string State, long Worst, long? Actual);

    private static async Task<Account?> LockAccountAsync(
        NpgsqlConnection db, NpgsqlTransaction tx, Guid id, CancellationToken ct)
    {
        await using var cmd = Cmd(db, tx, """
            SELECT mode, engine_configured, limit_units, consumed_units, reserved_units,
                   concurrency_limit, running_requests
            FROM assistant.quota_accounts WHERE installation_id = @i FOR UPDATE
            """);
        cmd.Parameters.AddWithValue("i", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new((AssistantMode)reader.GetInt16(0), reader.GetBoolean(1),
            reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4),
            reader.GetInt32(5), reader.GetInt32(6));
    }

    private static async Task<Lease?> LockReservationAsync(
        NpgsqlConnection db, NpgsqlTransaction tx, Guid installationId, Guid reservationId,
        CancellationToken ct)
    {
        await using var cmd = Cmd(db, tx, """
            SELECT state, worst_case_units, actual_units FROM assistant.quota_reservations
            WHERE installation_id = @i AND reservation_id = @r FOR UPDATE
            """);
        cmd.Parameters.AddWithValue("i", installationId);
        cmd.Parameters.AddWithValue("r", reservationId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new(reader.GetString(0), reader.GetInt64(1),
            reader.IsDBNull(2) ? null : reader.GetInt64(2));
    }

    private static NpgsqlCommand Cmd(NpgsqlConnection db, NpgsqlTransaction tx, string sql)
        => new(sql, db, tx);

    private static async Task ExecuteAsync(NpgsqlConnection db, NpgsqlTransaction tx,
        string sql, CancellationToken ct, params (string Name, object Value)[] values)
    {
        await using var cmd = Cmd(db, tx, sql);
        foreach (var (name, value) in values) cmd.Parameters.AddWithValue(name, value);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1)
            throw new InvalidOperationException("M12: fila de cuota inesperadamente ausente (fail-closed).");
    }
}
