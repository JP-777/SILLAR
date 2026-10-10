using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sillar.Modules.ServiceOrders.Application;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.ServiceOrders.Data;
using Sillar.Shared.Replication;

namespace Sillar.Modules.ServiceOrders.Tests;

public sealed class CustomerServiceOrderReaderPostgresTests
{
    [Fact]
    public Task Solo_entrega_ordenes_vinculadas_al_cliente_y_oculta_las_manuales()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var clienteA = Guid.CreateVersion7();
            var clienteB = Guid.CreateVersion7();
            var ordenA = Guid.CreateVersion7();
            var ordenB = Guid.CreateVersion7();
            var ordenManual = Guid.CreateVersion7();

            await EphemeralDatabase.ExecuteAsync(connection,
                EphemeralDatabase.InsertOrder(ordenA, "S-2026-0101"));
            await EphemeralDatabase.ExecuteAsync(connection,
                EphemeralDatabase.InsertOrder(ordenB, "S-2026-0102"));
            await EphemeralDatabase.ExecuteAsync(connection,
                EphemeralDatabase.InsertOrder(ordenManual, "S-2026-0103"));

            await EphemeralDatabase.ExecuteAsync(connection, $"""
                UPDATE service_orders.service_orders
                   SET customer_id = '{clienteA}',
                       customer_email_snapshot = 'compartido@ejemplo.test',
                       received_notes = 'NOTA_INTERNA_PROHIBIDA'
                 WHERE service_order_id = '{ordenA}';
                UPDATE service_orders.service_orders
                   SET customer_id = '{clienteB}',
                       customer_email_snapshot = 'compartido@ejemplo.test'
                 WHERE service_order_id = '{ordenB}';
                UPDATE service_orders.service_orders
                   SET customer_email_snapshot = 'compartido@ejemplo.test'
                 WHERE service_order_id = '{ordenManual}';
                """);

            await using var db = Context(connection);
            var reader = new CustomerServiceOrderReader(db);

            var propiosA = await reader.ListForCustomerAsync(clienteA, 20, CancellationToken.None);
            var propiosB = await reader.ListForCustomerAsync(clienteB, 20, CancellationToken.None);
            Assert.Equal(["S-2026-0101"], propiosA.Select(o => o.VisibleCode));
            Assert.Equal(["S-2026-0102"], propiosB.Select(o => o.VisibleCode));

            var permitido = await reader.GetForCustomerAsync(
                clienteA, "S-2026-0101", CancellationToken.None);
            Assert.NotNull(permitido);
            Assert.Equal("received", permitido.CurrentStatus);

            Assert.Null(await reader.GetForCustomerAsync(
                clienteA, "S-2026-0102", CancellationToken.None));
            Assert.Null(await reader.GetForCustomerAsync(
                clienteA, "S-2026-0103", CancellationToken.None));
            Assert.Null(await reader.GetForCustomerAsync(
                clienteA, "INEXISTENTE", CancellationToken.None));
            Assert.Empty(await reader.ListForCustomerAsync(
                Guid.Empty, 20, CancellationToken.None));

            var json = JsonSerializer.Serialize(permitido);
            Assert.DoesNotContain("NOTA_INTERNA_PROHIBIDA", json, StringComparison.Ordinal);
            Assert.DoesNotContain("CustomerId", json, StringComparison.Ordinal);
            Assert.DoesNotContain("PerformedBy", json, StringComparison.Ordinal);
            Assert.DoesNotContain("AdminUser", json, StringComparison.Ordinal);
            Assert.DoesNotContain("ReceivedNotes", json, StringComparison.Ordinal);
        });

    [Fact]
    public Task La_lista_acota_el_limite_sin_revelar_ordenes_de_otros()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);
            var cliente = Guid.CreateVersion7();
            for (var i = 1; i <= 3; i++)
            {
                var id = Guid.CreateVersion7();
                await EphemeralDatabase.ExecuteAsync(connection,
                    EphemeralDatabase.InsertOrder(id, $"S-2026-02{i:00}"));
                await EphemeralDatabase.ExecuteAsync(connection, $"""
                    UPDATE service_orders.service_orders
                       SET customer_id = '{cliente}'
                     WHERE service_order_id = '{id}'
                    """);
            }

            await using var db = Context(connection);
            var reader = new CustomerServiceOrderReader(db);
            Assert.Single(await reader.ListForCustomerAsync(cliente, 0, CancellationToken.None));
            Assert.Equal(3, (await reader.ListForCustomerAsync(
                cliente, int.MaxValue, CancellationToken.None)).Count);
        });

    private static ServiceOrdersDbContext Context(string connection)
        => new(
            new DbContextOptionsBuilder<ServiceOrdersDbContext>()
                .UseNpgsql(connection, options => options.MigrationsHistoryTable(
                    ServiceOrdersDbContext.MigrationsHistoryTable,
                    ServiceOrdersDbContext.Schema))
                .Options,
            new NodeIdentity("principal"),
            TimeProvider.System);
}
