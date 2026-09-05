using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Sillar.Shared.Replication;

namespace Sillar.Shared.Data.Replication;

/// <summary>
/// Rellena las columnas de replicación al guardar, para cualquier
/// <c>DbContext</c> de la plataforma.
/// </summary>
/// <remarks>
/// <para>
/// Va junto a <see cref="ReplicationMapping"/> y no por separado: mapear las
/// columnas sin sellarlas deja filas con <c>origin_node</c> vacío, y sellarlas
/// sin mapearlas no compila. Son las dos mitades de la misma regla.
/// </para>
/// <para>
/// <b>Por qué no lo hace quien escribe la entidad.</b> Porque dejarlo a cada
/// llamada garantiza que alguna se olvide, y una fila sin <c>origin_node</c> es
/// una fila que no se sabe de dónde vino — que es justo lo que la columna existe
/// para responder (ADR-016, regla 4).
/// </para>
/// <para>
/// <b>El nodo se fija solo al crear.</b> Una fila editada en otro nodo no cambia
/// de origen: nació donde nació, y esa es la pregunta que la columna responde.
/// Por eso <c>Modified</c> desmarca <c>OriginNode</c> y <c>CreatedAt</c> en vez
/// de reescribirlos.
/// </para>
/// </remarks>
public static class ReplicationStamping
{
    /// <summary>
    /// Sella las entidades replicadas que estén pendientes de guardar.
    /// </summary>
    /// <remarks>
    /// Se llama desde <c>SaveChanges</c> y <c>SaveChangesAsync</c>, antes de
    /// delegar en la base: después, el rastreador ya no dice qué cambió.
    /// </remarks>
    /// <param name="tracker">Rastreador del contexto que va a guardar.</param>
    /// <param name="node">Nodo donde nacen las filas de esta instalación.</param>
    /// <param name="clock">Reloj, inyectado para que las pruebas lo fijen.</param>
    public static void StampReplicationColumns(
        this ChangeTracker tracker,
        NodeIdentity node,
        TimeProvider clock)
    {
        var now = clock.GetUtcNow();

        foreach (var entry in tracker.Entries<IReplicatedEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.OriginNode = node.Code;
                    entry.Entity.RowVersion = 1;
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;

                case EntityState.Modified:
                    // El origen no se toca: quien edita no es quien creó.
                    entry.Property(nameof(IReplicatedEntity.OriginNode)).IsModified = false;
                    entry.Property(nameof(IReplicatedEntity.CreatedAt)).IsModified = false;
                    entry.Entity.RowVersion += 1;
                    break;
            }
        }
    }
}
