using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Tracking.Data;
using Sillar.Modules.Tracking.Domain;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Tracking.Tests;

public sealed class ModelContractTests
{
    [Fact]
    public void Both_business_tables_are_replicated_with_application_UUID_v7()
    {
        var tracking = new OrderTracking();
        var note = new TrackingNote { Body = "Avance interno" };

        Assert.IsAssignableFrom<IReplicatedEntity>(tracking);
        Assert.IsAssignableFrom<IReplicatedEntity>(note);

        Assert.Equal('7', tracking.OrderTrackingId.ToString("D")[14]);
        Assert.Equal('7', note.TrackingNoteId.ToString("D")[14]);
    }

    [Fact]
    public void Lazy_priority_is_nullable_and_negative_values_have_a_model_check()
    {
        using var database = Context();

        var entity = database.Model.FindEntityType(typeof(OrderTracking))!;
        var priority = entity.FindProperty(nameof(OrderTracking.BoardPriority))!;

        Assert.True(priority.IsNullable);

        var check = entity.GetCheckConstraints()
            .Single(constraint => constraint.Name == "ck_order_tracking_priority");

        Assert.Equal(
            "board_priority IS NULL OR board_priority >= 0",
            check.Sql);
    }

    [Fact]
    public void There_is_one_tracking_row_per_service_order_without_importing_M05b_entities()
    {
        using var database = Context();

        var entity = database.Model.FindEntityType(typeof(OrderTracking))!;

        var index = entity.GetIndexes().Single(candidate =>
            candidate.Properties.Count == 1 &&
            candidate.Properties[0].Name == nameof(OrderTracking.ServiceOrderId));

        Assert.True(index.IsUnique);

        // La FK cross-schema física se añadirá en la migración. No se modela
        // con una entidad M05b porque eso obligaría a referenciar su Domain/Data.
        Assert.Empty(entity.GetForeignKeys());
    }

    [Fact]
    public void Notes_are_internal_and_only_reference_the_local_tracking_row()
    {
        using var database = Context();

        var entity = database.Model.FindEntityType(typeof(TrackingNote))!;

        Assert.Null(entity.FindProperty("CustomerVisible"));

        var foreignKey = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(OrderTracking), foreignKey.PrincipalEntityType.ClrType);
    }

    private static TrackingDbContext Context()
        => new(
            new DbContextOptionsBuilder<TrackingDbContext>()
                .UseNpgsql("Host=localhost;Database=not_used;Username=none;Password=none")
                .Options,
            new NodeIdentity("principal"),
            TimeProvider.System);
}
