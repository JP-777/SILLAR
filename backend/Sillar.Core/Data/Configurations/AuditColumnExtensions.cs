using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sillar.Core.Data.Configurations;

/// <summary>
/// Mapea las columnas de auditoría que se repiten en casi todas las tablas.
/// </summary>
internal static class AuditColumnExtensions
{
    /// <summary>
    /// <c>created_at timestamptz NOT NULL DEFAULT now()</c>, escrita por la base
    /// de datos en el alta.
    /// </summary>
    public static PropertyBuilder<DateTimeOffset> AsCreatedAt(this PropertyBuilder<DateTimeOffset> property)
        => property
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

    /// <summary>
    /// <c>updated_at timestamptz NOT NULL DEFAULT now()</c>, mantenida por el
    /// trigger <c>core.set_updated_at()</c>.
    /// </summary>
    /// <remarks>
    /// Marcada como generada al insertar y al actualizar: EF nunca la escribe y
    /// la vuelve a leer después de guardar, de modo que la entidad en memoria
    /// refleja lo que puso el trigger.
    /// </remarks>
    public static PropertyBuilder<DateTimeOffset> AsUpdatedAt(this PropertyBuilder<DateTimeOffset> property)
        => property
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();
}
