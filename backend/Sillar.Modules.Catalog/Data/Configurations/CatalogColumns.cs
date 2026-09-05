using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sillar.Modules.Catalog.Data.Configurations;

/// <summary>
/// Lo que el catálogo mapea igual en todas sus tablas.
/// </summary>
/// <remarks>
/// Las cuatro columnas de replicación ya no están aquí: son iguales en CORE, en
/// Catalog y en CRM, así que viven una sola vez en
/// <c>Sillar.Shared.Data.Replication.ReplicationMapping</c>. Lo que queda es lo
/// que de verdad es del catálogo.
/// </remarks>
internal static class CatalogColumns
{
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
