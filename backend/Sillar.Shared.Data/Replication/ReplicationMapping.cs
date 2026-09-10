using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Shared.Replication;

namespace Sillar.Shared.Data.Replication;

/// <summary>
/// Mapea las cuatro columnas que lleva toda tabla replicada (ADR-016, regla 4).
/// </summary>
/// <remarks>
/// <para>
/// Estuvo copiado en <c>Catalog</c> y en <c>Crm</c>, y una tercera vez repartido
/// entre <c>MediaAssetConfiguration</c> y <c>AuditColumnExtensions</c> de CORE.
/// Las tres decían lo mismo, que es exactamente el problema: nada obligaba a que
/// lo siguieran diciendo.
/// </para>
/// <para>
/// <b>Aquí solo va lo que es igual en todos.</b> Lo que cambia por schema —el
/// nombre del trigger que mantiene <c>updated_at</c>: <c>core.set_updated_at()</c>,
/// <c>catalog.set_updated_at()</c>, <c>crm.set_updated_at()</c>— no se nombra en
/// el mapeo porque el mapeo no lo declara: solo declara que la columna la escribe
/// la base y no EF.
/// </para>
/// </remarks>
public static class ReplicationMapping
{
    /// <summary>Mapea nodo de origen, versión y fechas.</summary>
    public static void MapReplication<T>(this EntityTypeBuilder<T> builder)
        where T : class, IReplicatedEntity
    {
        // text, no varchar: el SPEC §6 lo declara así y en PostgreSQL no hay
        // diferencia de rendimiento. Un límite arbitrario aquí solo serviría
        // para rechazar el nombre de un nodo el día que alguien elija uno largo.
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

        // La escribe el trigger set_updated_at() del schema del módulo. Marcada
        // como generada para que EF la relea tras guardar y la entidad en
        // memoria refleje lo que puso la base.
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();
    }
}
