using Npgsql;
using Sillar.Core;
using Sillar.Modules.Services;

namespace Sillar.Modules.ServiceOrders.Tests;

public sealed class PhysicalModelPostgresTests
{
    private static readonly string[] ExpectedTables =
    [
        "service_order_assignment_events",
        "service_order_items",
        "service_order_series",
        "service_order_status_history",
        "service_orders",
    ];

    [Fact]
    public Task Clean_migration_is_idempotent_and_materializes_five_tables()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);
            await new ServiceOrdersModule().ApplyMigrationsAsync(connection, CancellationToken.None);

            var tables = await EphemeralDatabase.ScalarAsync<string>(connection, """
                SELECT string_agg(table_name, ',' ORDER BY table_name)
                  FROM information_schema.tables
                 WHERE table_schema = 'service_orders' AND table_name <> '__migrations'
                """);
            Assert.Equal(ExpectedTables, tables.Split(','));
            Assert.Equal(2L, await EphemeralDatabase.ScalarAsync<long>(connection,
                "SELECT count(*) FROM service_orders.__migrations"));

            await EphemeralDatabase.ExecuteAsync(connection, EphemeralDatabase.Script("01_schema.sql"));
            await EphemeralDatabase.ExecuteAsync(connection, EphemeralDatabase.Script("01_schema.sql"));
            await EphemeralDatabase.ExecuteAsync(connection, EphemeralDatabase.Script("02_seed.sql"));
            await EphemeralDatabase.ExecuteAsync(connection, EphemeralDatabase.Script("02_seed.sql"));
            Assert.Equal(0L, await EphemeralDatabase.ScalarAsync<long>(connection,
                "SELECT count(*) FROM service_orders.service_orders"));
        });

    [Fact]
    public Task Installing_without_M05a_is_rejected_with_attributable_message()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallAsync(connection, new CoreModule());
            var error = await Assert.ThrowsAnyAsync<PostgresException>(
                () => new ServiceOrdersModule().ApplyMigrationsAsync(connection, CancellationToken.None));

            Assert.Equal(PostgresErrorCodes.UndefinedTable, error.SqlState);
            Assert.Contains("M05a", error.MessageText);
            Assert.Contains("services", error.MessageText);
            Assert.Equal(0L, await EphemeralDatabase.ScalarAsync<long>(connection,
                "SELECT count(*) FROM pg_tables WHERE schemaname = 'service_orders' AND tablename <> '__migrations'"));
        });

    [Fact]
    public Task Physical_catalog_matches_ADR_classification_and_FKs_are_internal()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var replicated = await EphemeralDatabase.ScalarAsync<string>(connection, """
                SELECT string_agg(table_name, ',' ORDER BY table_name)
                  FROM information_schema.columns
                 WHERE table_schema = 'service_orders' AND column_name IN ('origin_node','row_version')
                 GROUP BY table_schema
                HAVING count(*) = 8
                """);
            Assert.Equal(
                "service_order_assignment_events,service_order_assignment_events,service_order_items,service_order_items,service_order_status_history,service_order_status_history,service_orders,service_orders",
                replicated);

            Assert.Equal("integer", await EphemeralDatabase.ScalarAsync<string>(connection, """
                SELECT data_type FROM information_schema.columns
                 WHERE table_schema='service_orders' AND table_name='service_order_series'
                   AND column_name='service_order_series_id'
                """));
            Assert.Equal(0L, await EphemeralDatabase.ScalarAsync<long>(connection, """
                SELECT count(*) FROM information_schema.columns
                 WHERE table_schema='service_orders' AND table_name='service_order_series'
                   AND column_name IN ('origin_node','row_version')
                """));

            var foreignKeys = await EphemeralDatabase.ScalarAsync<string>(connection, """
                SELECT string_agg(c.conname || '->' || rn.nspname || '.' || r.relname, ',' ORDER BY c.conname)
                  FROM pg_constraint c
                  JOIN pg_namespace n ON n.oid=c.connamespace
                  JOIN pg_class r ON r.oid=c.confrelid
                  JOIN pg_namespace rn ON rn.oid=r.relnamespace
                 WHERE c.contype='f' AND n.nspname='service_orders'
                """);
            Assert.Equal(
                "fk_service_order_assignment_events_service_order_id->service_orders.service_orders," +
                "fk_service_order_items_service_order_id->service_orders.service_orders," +
                "fk_service_order_status_history_service_order_id->service_orders.service_orders",
                foreignKeys);
            Assert.DoesNotContain("services.", foreignKeys, StringComparison.Ordinal);
            Assert.DoesNotContain("core.", foreignKeys, StringComparison.Ordinal);
            Assert.DoesNotContain("service_order_series", foreignKeys, StringComparison.Ordinal);

            var checks = await EphemeralDatabase.ScalarAsync<long>(connection, """
                SELECT count(*) FROM pg_constraint c JOIN pg_namespace n ON n.oid=c.connamespace
                 WHERE n.nspname='service_orders' AND c.contype='c'
                """);
            Assert.True(checks >= 30, $"Solo se encontraron {checks} CHECK físicos.");
        });

    [Fact]
    public Task Every_ratified_column_PK_and_unique_constraint_exists_physically()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            async Task AssertColumns(string table, params string[] expected)
            {
                var actual = await EphemeralDatabase.ScalarAsync<string>(connection, $"""
                    SELECT string_agg(column_name, ',' ORDER BY column_name)
                      FROM information_schema.columns
                     WHERE table_schema='service_orders' AND table_name='{table}'
                    """);
                Assert.Equal(expected.Order(), actual.Split(',').Order());
            }

            await AssertColumns("service_orders",
                "service_order_id", "visible_code", "status", "create_idempotency_key", "customer_id",
                "customer_name_snapshot", "customer_phone_snapshot", "customer_email_snapshot",
                "received_notes", "received_at", "promised_at",
                "created_by_admin_name", "created_by_admin_user_id", "created_by_admin_user_home_node",
                "current_assignee_name", "current_assignee_admin_user_id", "current_assignee_admin_user_home_node",
                "last_status_changed_at", "last_status_changed_by_name", "last_status_changed_by_admin_user_id",
                "last_status_changed_by_admin_user_home_node", "origin_node", "row_version", "created_at", "updated_at");

            await AssertColumns("service_order_items",
                "service_order_item_id", "service_order_id", "service_source_id", "service_source_node",
                "service_name_snapshot", "service_slug_snapshot", "service_short_description_snapshot",
                "service_description_snapshot", "showcase_price_snapshot", "sale_unit_snapshot",
                "media_asset_id_snapshot", "image_url_snapshot", "image_alt_text_snapshot",
                "requested_details", "quantity", "agreed_unit_price", "sort_order",
                "origin_node", "row_version", "created_at", "updated_at");

            await AssertColumns("service_order_assignment_events",
                "assignment_event_id", "service_order_id", "action",
                "assignee_name", "assignee_admin_user_id", "assignee_admin_user_home_node",
                "performed_by_name", "performed_by_admin_user_id", "performed_by_admin_user_home_node",
                "occurred_at", "origin_node", "row_version", "created_at", "updated_at");

            await AssertColumns("service_order_status_history",
                "status_history_id", "service_order_id", "from_status", "to_status", "occurred_at",
                "performed_by_name", "performed_by_admin_user_id", "performed_by_admin_user_home_node",
                "origin_node", "row_version", "created_at", "updated_at");

            await AssertColumns("service_order_series",
                "service_order_series_id", "node_code", "year", "last_number", "created_at", "updated_at");

            Assert.Equal(
                "pk_service_order_assignment_events,pk_service_order_items,pk_service_order_series,pk_service_order_status_history,pk_service_orders",
                await EphemeralDatabase.ScalarAsync<string>(connection, """
                    SELECT string_agg(c.conname, ',' ORDER BY c.conname)
                      FROM pg_constraint c
                      JOIN pg_namespace n ON n.oid=c.connamespace
                      JOIN pg_class r ON r.oid=c.conrelid
                     WHERE n.nspname='service_orders' AND c.contype='p' AND r.relname <> '__migrations'
                    """));

            Assert.Equal(
                "uq_service_order_items_order_sort,uq_service_order_series_node_code_year," +
                "uq_service_orders_create_idempotency_key,uq_service_orders_visible_code",
                await EphemeralDatabase.ScalarAsync<string>(connection, """
                    SELECT string_agg(indexname, ',' ORDER BY indexname)
                      FROM pg_indexes
                     WHERE schemaname='service_orders' AND indexname LIKE 'uq_%'
                    """));

            Assert.Equal("ALWAYS", await EphemeralDatabase.ScalarAsync<string>(connection, """
                SELECT identity_generation FROM information_schema.columns
                 WHERE table_schema='service_orders' AND table_name='service_order_series'
                   AND column_name='service_order_series_id'
                """));
            Assert.Equal(4L, await EphemeralDatabase.ScalarAsync<long>(connection, """
                SELECT count(*) FROM information_schema.columns
                 WHERE table_schema='service_orders' AND data_type='uuid'
                   AND (table_name, column_name) IN (
                       ('service_orders','service_order_id'),
                       ('service_order_items','service_order_item_id'),
                       ('service_order_assignment_events','assignment_event_id'),
                       ('service_order_status_history','status_history_id'))
                """));
        });

    [Fact]
    public Task Status_actor_accepts_complete_human_or_all_null_and_rejects_partial_or_fictitious()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);
            var order = Guid.CreateVersion7();
            await EphemeralDatabase.ExecuteAsync(connection, EphemeralDatabase.InsertOrder(order));

            static string History(Guid id, Guid orderId, string name, string userId, string homeNode) => $"""
                INSERT INTO service_orders.service_order_status_history
                    (status_history_id, service_order_id, from_status, to_status, occurred_at,
                     performed_by_name, performed_by_admin_user_id, performed_by_admin_user_home_node,
                     origin_node, row_version)
                VALUES ('{id}', '{orderId}', NULL, 'received', '2026-10-07T10:00:00Z',
                        {name}, {userId}, {homeNode}, 'principal', 1)
                """;

            await EphemeralDatabase.ExecuteAsync(connection,
                History(Guid.CreateVersion7(), order, "'Ana Pérez'", "7", "'principal'"));
            await EphemeralDatabase.ExecuteAsync(connection,
                History(Guid.CreateVersion7(), order, "NULL", "NULL", "NULL"));

            var partial = await Assert.ThrowsAnyAsync<PostgresException>(() =>
                EphemeralDatabase.ExecuteAsync(connection,
                    History(Guid.CreateVersion7(), order, "'Ana Pérez'", "7", "NULL")));
            Assert.Equal("ck_service_order_status_history_actor_triple", partial.ConstraintName);

            var fictitious = await Assert.ThrowsAnyAsync<PostgresException>(() =>
                EphemeralDatabase.ExecuteAsync(connection,
                    History(Guid.CreateVersion7(), order, "'Sistema'", "7", "'principal'")));
            Assert.Equal("ck_service_order_status_history_actor_not_fictitious", fictitious.ConstraintName);
            Assert.Equal(2L, await EphemeralDatabase.ScalarAsync<long>(connection,
                "SELECT count(*) FROM service_orders.service_order_status_history"));
        });

    [Fact]
    public Task Decimal_quantity_and_promised_at_boundaries_are_physical_constraints()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);
            var order = Guid.CreateVersion7();
            await EphemeralDatabase.ExecuteAsync(connection,
                EphemeralDatabase.InsertOrder(order, promisedAt: "'2026-10-07T11:00:00Z'"));

            var item = Guid.CreateVersion7();
            await EphemeralDatabase.ExecuteAsync(connection, $"""
                INSERT INTO service_orders.service_order_items
                    (service_order_item_id, service_order_id, service_source_id, service_source_node,
                     service_name_snapshot, service_slug_snapshot, service_description_snapshot,
                     requested_details, quantity, sort_order, origin_node, row_version)
                VALUES ('{item}', '{order}', 12, 'principal', 'Impresión', 'impresion',
                        'Impresión por hoja', 'A4 color', 1.250, 0, 'principal', 1)
                """);
            Assert.Equal(1.250m, await EphemeralDatabase.ScalarAsync<decimal>(connection,
                $"SELECT quantity FROM service_orders.service_order_items WHERE service_order_item_id='{item}'"));

            var badQuantity = await Assert.ThrowsAnyAsync<PostgresException>(() =>
                EphemeralDatabase.ExecuteAsync(connection, $"""
                    INSERT INTO service_orders.service_order_items
                        (service_order_item_id, service_order_id, service_source_id, service_source_node,
                         service_name_snapshot, service_slug_snapshot, service_description_snapshot,
                         requested_details, quantity, sort_order, origin_node, row_version)
                    VALUES ('{Guid.CreateVersion7()}', '{order}', 12, 'principal', 'Impresión', 'impresion',
                            'Impresión por hoja', 'A4', 0, 1, 'principal', 1)
                    """));
            Assert.Equal("ck_service_order_items_quantity", badQuantity.ConstraintName);

            var badPromise = await Assert.ThrowsAnyAsync<PostgresException>(() =>
                EphemeralDatabase.ExecuteAsync(connection,
                    EphemeralDatabase.InsertOrder(Guid.CreateVersion7(), "S-2026-0002", "'2026-10-07T09:59:59Z'")));
            Assert.Equal("ck_service_orders_promised_at", badPromise.ConstraintName);
        });

    [Fact]
    public Task Drop_is_idempotent_isolated_and_blocks_future_hard_dependent()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);
            var coreBefore = await EphemeralDatabase.ScalarAsync<long>(connection,
                "SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('core','services')");

            await EphemeralDatabase.ExecuteAsync(connection, """
                INSERT INTO core.modules (code, display_name, description, version)
                VALUES ('service_orders','Órdenes','x','1'), ('tracking','Seguimiento','x','1');
                INSERT INTO core.module_dependencies (module_id, depends_on_module_id, kind)
                SELECT m.module_id, t.module_id, 'hard'
                  FROM core.modules m, core.modules t
                 WHERE m.code='tracking' AND t.code='service_orders';
                CREATE SCHEMA tracking;
                """);

            var blocked = await Assert.ThrowsAnyAsync<PostgresException>(() =>
                EphemeralDatabase.ExecuteAsync(connection, EphemeralDatabase.Script("99_drop.sql")));
            Assert.Equal(PostgresErrorCodes.DependentObjectsStillExist, blocked.SqlState);
            Assert.True(await EphemeralDatabase.ScalarAsync<bool>(connection,
                "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname='service_orders')"));

            await EphemeralDatabase.ExecuteAsync(connection, """
                DROP SCHEMA tracking;
                DELETE FROM core.module_dependencies;
                DELETE FROM core.modules WHERE code IN ('tracking','service_orders');
                """);
            await EphemeralDatabase.ExecuteAsync(connection, EphemeralDatabase.Script("99_drop.sql"));
            await EphemeralDatabase.ExecuteAsync(connection, EphemeralDatabase.Script("99_drop.sql"));

            Assert.False(await EphemeralDatabase.ScalarAsync<bool>(connection,
                "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname='service_orders')"));
            Assert.Equal(coreBefore, await EphemeralDatabase.ScalarAsync<long>(connection,
                "SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('core','services')"));
        });
}
