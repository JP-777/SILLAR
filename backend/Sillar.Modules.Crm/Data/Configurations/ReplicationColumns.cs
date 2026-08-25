using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Crm.Data.Configurations;

/// <summary>
/// Las columnas que llevan todas las tablas replicadas (ADR-016, regla 4).
/// </summary>
/// <remarks>
/// Tercera copia temporal del mismo helper que ya existe en CORE y Catalog.
/// DEUDA: StampReplicationColumns y este helper están duplicados por tercera
/// vez. No se ha extraído a Shared deliberadamente.
/// </remarks>
internal static class ReplicationColumns
{
    /// <summary>Mapea nodo de origen, versión y fechas.</summary>
    public static void MapReplication<T>(this EntityTypeBuilder<T> builder)
        where T : class, IReplicatedEntity
    {
        builder.Property(x => x.OriginNode)
            .HasColumnName("origin_node")
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .HasColumnName("row_version")
            .HasDefaultValue(1L)
            .ValueGeneratedNever();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

        // La escribe el trigger crm.set_updated_at(). Marcada como generada
        // para que EF la relea tras guardar.
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();
    }
}
