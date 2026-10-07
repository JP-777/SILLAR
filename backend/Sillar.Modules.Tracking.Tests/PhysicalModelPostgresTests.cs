using Npgsql;

namespace Sillar.Modules.Tracking.Tests;

public sealed class PhysicalModelPostgresTests
{
    [Fact]
    public async Task Migration_rejects_installation_without_hard_dependencies()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            var error = await Assert.ThrowsAsync<PostgresException>(
                () => new TrackingModule().ApplyMigrationsAsync(
                    connection,
                    CancellationToken.None));

            Assert.Equal(PostgresErrorCodes.UndefinedTable, error.SqlState);

            Assert.Contains(
                "M06 Seguimiento no se puede instalar",
                error.MessageText);

            var businessTables =
                await EphemeralDatabase.ScalarAsync<int>(
                    connection,
                    """
                    SELECT count(*)::int
                    FROM information_schema.tables
                    WHERE table_schema = 'tracking'
                      AND table_name IN (
                          'order_tracking',
                          'tracking_notes'
                      );
                    """);

            Assert.Equal(0, businessTables);

            var applied =
                await new TrackingModule().AppliedMigrationsAsync(
                    connection,
                    CancellationToken.None);

            Assert.Empty(applied);
        });
    }

    [Fact]
    public async Task Migration_materializes_expected_schema_FKs_and_triggers()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);

            var tables =
                await EphemeralDatabase.ScalarAsync<int>(
                    connection,
                    """
                    SELECT count(*)::int
                    FROM information_schema.tables
                    WHERE table_schema = 'tracking'
                      AND table_name IN (
                          'order_tracking',
                          'tracking_notes'
                      );
                    """);

            Assert.Equal(2, tables);

            var externalFk =
                await EphemeralDatabase.ScalarAsync<int>(
                    connection,
                    """
                    SELECT count(*)::int
                    FROM pg_constraint c
                    JOIN pg_class child
                      ON child.oid = c.conrelid
                    JOIN pg_namespace child_ns
                      ON child_ns.oid = child.relnamespace
                    JOIN pg_class parent
                      ON parent.oid = c.confrelid
                    JOIN pg_namespace parent_ns
                      ON parent_ns.oid = parent.relnamespace
                    WHERE c.contype = 'f'
                      AND c.conname = 'fk_order_tracking_service_order_id'
                      AND child_ns.nspname = 'tracking'
                      AND child.relname = 'order_tracking'
                      AND parent_ns.nspname = 'service_orders'
                      AND parent.relname = 'service_orders';
                    """);

            Assert.Equal(1, externalFk);

            var triggers =
                await EphemeralDatabase.ScalarAsync<int>(
                    connection,
                    """
                    SELECT count(*)::int
                    FROM pg_trigger t
                    JOIN pg_class c
                      ON c.oid = t.tgrelid
                    JOIN pg_namespace n
                      ON n.oid = c.relnamespace
                    WHERE NOT t.tgisinternal
                      AND n.nspname = 'tracking'
                      AND t.tgname IN (
                          'trg_order_tracking_set_updated_at',
                          'trg_tracking_notes_set_updated_at'
                      );
                    """);

            Assert.Equal(2, triggers);
        });
    }

    [Fact]
    public async Task Lazy_row_and_note_allow_null_priority()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);

            var orderId = Guid.CreateVersion7();
            var trackingId = Guid.CreateVersion7();
            var noteId = Guid.CreateVersion7();

            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertOrder(orderId));

            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertTracking(
                    trackingId,
                    orderId));

            await EphemeralDatabase.ExecuteAsync(
                connection,
                $"""
                INSERT INTO tracking.tracking_notes
                    (
                        tracking_note_id,
                        order_tracking_id,
                        body,
                        is_active,
                        origin_node,
                        row_version
                    )
                VALUES
                    (
                        '{noteId}',
                        '{trackingId}',
                        'Nota sin prioridad manual',
                        true,
                        'principal',
                        1
                    );
                """);

            var hasNullPriority =
                await EphemeralDatabase.ScalarAsync<bool>(
                    connection,
                    $"""
                    SELECT board_priority IS NULL
                    FROM tracking.order_tracking
                    WHERE order_tracking_id = '{trackingId}';
                    """);

            Assert.True(hasNullPriority);
        });
    }

    [Fact]
    public async Task Negative_priority_is_rejected_by_PostgreSQL_check()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);

            var orderId = Guid.CreateVersion7();

            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertOrder(orderId));

            var error = await Assert.ThrowsAsync<PostgresException>(
                () => EphemeralDatabase.ExecuteAsync(
                    connection,
                    EphemeralDatabase.InsertTracking(
                        Guid.CreateVersion7(),
                        orderId,
                        "-1")));

            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                error.SqlState);

            Assert.Equal(
                "ck_order_tracking_priority",
                error.ConstraintName);
        });
    }

    [Fact]
    public async Task Tracking_for_unknown_service_order_is_rejected_by_FK()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);

            var error = await Assert.ThrowsAsync<PostgresException>(
                () => EphemeralDatabase.ExecuteAsync(
                    connection,
                    EphemeralDatabase.InsertTracking(
                        Guid.CreateVersion7(),
                        Guid.CreateVersion7())));

            Assert.Equal(
                PostgresErrorCodes.ForeignKeyViolation,
                error.SqlState);

            Assert.Equal(
                "fk_order_tracking_service_order_id",
                error.ConstraintName);
        });
    }

    [Fact]
    public async Task Updated_at_trigger_runs_for_tracking_and_notes()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);

            var orderId = Guid.CreateVersion7();
            var trackingId = Guid.CreateVersion7();
            var noteId = Guid.CreateVersion7();

            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertOrder(orderId));

            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertTracking(
                    trackingId,
                    orderId));

            await EphemeralDatabase.ExecuteAsync(
                connection,
                $"""
                INSERT INTO tracking.tracking_notes
                    (
                        tracking_note_id,
                        order_tracking_id,
                        body,
                        is_active,
                        origin_node,
                        row_version
                    )
                VALUES
                    (
                        '{noteId}',
                        '{trackingId}',
                        'Primera nota',
                        true,
                        'principal',
                        1
                    );
                """);

            await EphemeralDatabase.ExecuteAsync(
                connection,
                $"""
                UPDATE tracking.order_tracking
                SET updated_at = '2000-01-01T00:00:00Z',
                    pinned = true
                WHERE order_tracking_id = '{trackingId}';

                UPDATE tracking.tracking_notes
                SET updated_at = '2000-01-01T00:00:00Z',
                    body = 'Nota actualizada'
                WHERE tracking_note_id = '{noteId}';
                """);

            var trackingUpdated =
                await EphemeralDatabase.ScalarAsync<bool>(
                    connection,
                    $"""
                    SELECT updated_at > '2026-01-01T00:00:00Z'
                    FROM tracking.order_tracking
                    WHERE order_tracking_id = '{trackingId}';
                    """);

            var noteUpdated =
                await EphemeralDatabase.ScalarAsync<bool>(
                    connection,
                    $"""
                    SELECT updated_at > '2026-01-01T00:00:00Z'
                    FROM tracking.tracking_notes
                    WHERE tracking_note_id = '{noteId}';
                    """);

            Assert.True(trackingUpdated);
            Assert.True(noteUpdated);
        });
    }

    [Fact]
    public async Task Database_enforces_unique_tracking_row_and_internal_note_FK()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);

            var orderId = Guid.CreateVersion7();
            var trackingId = Guid.CreateVersion7();

            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertOrder(orderId));

            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertTracking(
                    trackingId,
                    orderId));

            var duplicate = await Assert.ThrowsAsync<PostgresException>(
                () => EphemeralDatabase.ExecuteAsync(
                    connection,
                    EphemeralDatabase.InsertTracking(
                        Guid.CreateVersion7(),
                        orderId)));

            Assert.Equal(
                PostgresErrorCodes.UniqueViolation,
                duplicate.SqlState);

            Assert.Equal(
                "uq_order_tracking_service_order_id",
                duplicate.ConstraintName);

            var unknownParent = await Assert.ThrowsAsync<PostgresException>(
                () => EphemeralDatabase.ExecuteAsync(
                    connection,
                    $"""
                    INSERT INTO tracking.tracking_notes
                        (
                            tracking_note_id,
                            order_tracking_id,
                            body,
                            is_active,
                            origin_node,
                            row_version
                        )
                    VALUES
                        (
                            '{Guid.CreateVersion7()}',
                            '{Guid.CreateVersion7()}',
                            'Nota huérfana',
                            true,
                            'principal',
                            1
                        );
                    """));

            Assert.Equal(
                PostgresErrorCodes.ForeignKeyViolation,
                unknownParent.SqlState);

            Assert.Equal(
                "fk_tracking_notes_order_tracking_id",
                unknownParent.ConstraintName);

            var noteId = Guid.CreateVersion7();

            await EphemeralDatabase.ExecuteAsync(
                connection,
                $"""
                INSERT INTO tracking.tracking_notes
                    (
                        tracking_note_id,
                        order_tracking_id,
                        body,
                        is_active,
                        origin_node,
                        row_version
                    )
                VALUES
                    (
                        '{noteId}',
                        '{trackingId}',
                        'Nota válida',
                        true,
                        'principal',
                        1
                    );
                """);

            var restrict = await Assert.ThrowsAsync<PostgresException>(
                () => EphemeralDatabase.ExecuteAsync(
                    connection,
                    $"""
                    DELETE FROM tracking.order_tracking
                    WHERE order_tracking_id = '{trackingId}';
                    """));

            Assert.Equal(
                PostgresErrorCodes.ForeignKeyViolation,
                restrict.SqlState);

            Assert.Equal(
                "fk_tracking_notes_order_tracking_id",
                restrict.ConstraintName);
        });
    }

    [Fact]
    public async Task Database_enforces_actor_triples_and_rejects_fictitious_actor()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);

            var orderId = Guid.CreateVersion7();
            var trackingId = Guid.CreateVersion7();

            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertOrder(orderId));

            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertTracking(
                    trackingId,
                    orderId));

            var partialTracking =
                await Assert.ThrowsAsync<PostgresException>(
                    () => EphemeralDatabase.ExecuteAsync(
                        connection,
                        $"""
                        UPDATE tracking.order_tracking
                        SET last_touched_by_name = 'Ana'
                        WHERE order_tracking_id = '{trackingId}';
                        """));

            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                partialTracking.SqlState);

            Assert.Equal(
                "ck_order_tracking_last_touched_triple",
                partialTracking.ConstraintName);

            var fictitiousTracking =
                await Assert.ThrowsAsync<PostgresException>(
                    () => EphemeralDatabase.ExecuteAsync(
                        connection,
                        $"""
                        UPDATE tracking.order_tracking
                        SET last_touched_by_name = 'Sistema',
                            last_touched_by_admin_user_id = 7,
                            last_touched_by_admin_user_home_node = 'principal'
                        WHERE order_tracking_id = '{trackingId}';
                        """));

            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                fictitiousTracking.SqlState);

            Assert.Equal(
                "ck_order_tracking_last_touched_not_fictitious",
                fictitiousTracking.ConstraintName);

            var partialNote =
                await Assert.ThrowsAsync<PostgresException>(
                    () => EphemeralDatabase.ExecuteAsync(
                        connection,
                        $"""
                        INSERT INTO tracking.tracking_notes
                            (
                                tracking_note_id,
                                order_tracking_id,
                                body,
                                author_name,
                                is_active,
                                origin_node,
                                row_version
                            )
                        VALUES
                            (
                                '{Guid.CreateVersion7()}',
                                '{trackingId}',
                                'Actor incompleto',
                                'Ana',
                                true,
                                'principal',
                                1
                            );
                        """));

            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                partialNote.SqlState);

            Assert.Equal(
                "ck_tracking_notes_author_triple",
                partialNote.ConstraintName);

            var fictitiousNote =
                await Assert.ThrowsAsync<PostgresException>(
                    () => EphemeralDatabase.ExecuteAsync(
                        connection,
                        $"""
                        INSERT INTO tracking.tracking_notes
                            (
                                tracking_note_id,
                                order_tracking_id,
                                body,
                                author_name,
                                author_admin_user_id,
                                author_admin_user_home_node,
                                is_active,
                                origin_node,
                                row_version
                            )
                        VALUES
                            (
                                '{Guid.CreateVersion7()}',
                                '{trackingId}',
                                'Actor ficticio',
                                'Sistema',
                                7,
                                'principal',
                                true,
                                'principal',
                                1
                            );
                        """));

            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                fictitiousNote.SqlState);

            Assert.Equal(
                "ck_tracking_notes_author_not_fictitious",
                fictitiousNote.ConstraintName);
        });
    }

    [Fact]
    public async Task Database_enforces_replication_checks_on_both_tables()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);

            var orderId = Guid.CreateVersion7();
            var trackingId = Guid.CreateVersion7();
            var noteId = Guid.CreateVersion7();

            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertOrder(orderId));

            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertTracking(
                    trackingId,
                    orderId));

            var badTracking =
                await Assert.ThrowsAsync<PostgresException>(
                    () => EphemeralDatabase.ExecuteAsync(
                        connection,
                        $"""
                        UPDATE tracking.order_tracking
                        SET row_version = 0
                        WHERE order_tracking_id = '{trackingId}';
                        """));

            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                badTracking.SqlState);

            Assert.Equal(
                "ck_order_tracking_replication",
                badTracking.ConstraintName);

            await EphemeralDatabase.ExecuteAsync(
                connection,
                $"""
                INSERT INTO tracking.tracking_notes
                    (
                        tracking_note_id,
                        order_tracking_id,
                        body,
                        is_active,
                        origin_node,
                        row_version
                    )
                VALUES
                    (
                        '{noteId}',
                        '{trackingId}',
                        'Nota válida',
                        true,
                        'principal',
                        1
                    );
                """);

            var badNote =
                await Assert.ThrowsAsync<PostgresException>(
                    () => EphemeralDatabase.ExecuteAsync(
                        connection,
                        $"""
                        UPDATE tracking.tracking_notes
                        SET origin_node = ''
                        WHERE tracking_note_id = '{noteId}';
                        """));

            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                badNote.SqlState);

            Assert.Equal(
                "ck_tracking_notes_replication",
                badNote.ConstraintName);
        });
    }

}
