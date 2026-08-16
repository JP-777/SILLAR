using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Catalog.Data.Configurations;

/// <summary>
/// Las columnas que llevan todas las tablas replicadas (ADR-016, regla 4).
/// </summary>
internal static class ReplicationColumns
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

        // La escribe el trigger catalog.set_updated_at(). Marcada como generada
        // para que EF la relea tras guardar y la entidad en memoria refleje lo
        // que puso la base.
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();
    }

    /// <summary>
    /// Mapea la clave primaria <c>uuid</c> de una entidad del catálogo.
    /// </summary>
    /// <remarks>
    /// <c>ValueGeneratedNever</c> es deliberado: la genera la aplicación con
    /// <c>Guid.CreateVersion7()</c>, no la base de datos. Un nodo sin conexión
    /// tiene que poder crear la fila entera antes de hablar con nadie.
    /// </remarks>
    public static void MapCatalogKey<T>(this EntityTypeBuilder<T> builder, string tableName)
        where T : Domain.CatalogEntity
    {
        builder.HasKey(x => x.Id).HasName($"pk_{tableName}");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
    }
}

/// <summary>Textos de las restricciones CHECK.</summary>
internal static class Check
{
    /// <summary>Texto obligatorio que no puede quedar en blanco.</summary>
    public static string NotEmpty(string column) => $"btrim({column}) <> ''";

    /// <summary>
    /// Formato de slug: solo minúsculas, dígitos y guiones.
    /// </summary>
    /// <remarks>
    /// Sin guiones al principio ni al final, y sin dos seguidos: un slug así
    /// produce URL feas y ambiguas al normalizarlas.
    ///
    /// <b><c>COLLATE "C"</c> no es opcional.</b> Las columnas de slug llevan
    /// <c>core.es_ci</c>, que es una colación no determinista, y PostgreSQL no
    /// admite expresiones regulares sobre ellas — igual que no admite
    /// <c>LIKE</c>:
    ///
    /// <code>
    /// ERROR 0A000: nondeterministic collations are not supported for regular expressions
    /// </code>
    ///
    /// Sin esto el <c>CHECK</c> se crea sin protestar y luego <b>ningún</b>
    /// <c>INSERT</c> funciona, porque el error salta al evaluarlo. Comprobado.
    /// </remarks>
    public static string SlugFormat(string column) => $"{column} COLLATE \"C\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'";
}
