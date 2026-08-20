using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Modules.Cms.Data;
using Sillar.Modules.Cms.Domain;

namespace Sillar.Modules.Cms.Services;

/// <summary>Una posición final calculada antes de tocar entidades.</summary>
internal sealed record OrderAssignment(int Id, int DisplayOrder);

/// <summary>Plan completo o error; nunca contiene una asignación parcial.</summary>
internal sealed record OrderPlan(IReadOnlyList<OrderAssignment> Assignments, string? Error)
{
    internal bool IsValid => Error is null;

    internal static OrderPlan Create(
        IReadOnlyCollection<int> currentIds,
        IReadOnlyList<int>? requestedIds)
    {
        var orderedIds = requestedIds ?? [];
        var current = currentIds.ToHashSet();

        if (orderedIds.Count != current.Count
            || orderedIds.Distinct().Count() != orderedIds.Count
            || orderedIds.Any(id => !current.Contains(id)))
        {
            return new OrderPlan(
                [],
                "La lista debe incluir cada elemento de la sección exactamente una vez.");
        }

        return new OrderPlan(
            [.. orderedIds.Select((id, index) => new OrderAssignment(id, index))],
            null);
    }
}

/// <summary>Aplica un orden completo dentro de una única transacción serializable.</summary>
internal sealed class CmsOrderService(CmsDbContext database)
{
    internal async Task<CmsOperation<IReadOnlyList<int>>> ReorderAsync<TEntity>(
        DbSet<TEntity> set,
        IReadOnlyList<int>? requestedIds,
        Action<TEntity, int> assignOrder,
        CancellationToken cancellationToken)
        where TEntity : CmsEntity
    {
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var entities = await set.OrderBy(entity => entity.Id).ToListAsync(cancellationToken);
        var plan = OrderPlan.Create(entities.Select(entity => entity.Id).ToArray(), requestedIds);

        if (!plan.IsValid)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new CmsOperation<IReadOnlyList<int>>(CmsOutcome.Invalid, plan.Error);
        }

        var byId = entities.ToDictionary(entity => entity.Id);
        foreach (var assignment in plan.Assignments)
        {
            assignOrder(byId[assignment.Id], assignment.DisplayOrder);
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (IsSerializationFailure(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return new CmsOperation<IReadOnlyList<int>>(
                CmsOutcome.Conflict,
                "El contenido cambió mientras se reordenaba. Recarga la lista e inténtalo de nuevo.");
        }

        return new CmsOperation<IReadOnlyList<int>>(
            CmsOutcome.Ok,
            Value: [.. plan.Assignments.Select(assignment => assignment.Id)]);
    }

    private static bool IsSerializationFailure(Exception exception)
        => exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }
           || exception.InnerException is PostgresException
           {
               SqlState: PostgresErrorCodes.SerializationFailure
           };
}
