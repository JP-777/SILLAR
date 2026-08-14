namespace Sillar.Core.Data.Configurations;

/// <summary>
/// Genera el texto de las restricciones CHECK a partir de las listas de valores
/// del dominio.
/// </summary>
/// <remarks>
/// Escribir los valores a mano en la migración los desincronizaría del código a
/// la primera. Generándolos desde la misma constante, añadir un rol o una acción
/// de auditoría es tocar un solo sitio y crear la migración correspondiente.
/// </remarks>
internal static class Check
{
    /// <summary>Texto obligatorio que no puede quedar en blanco.</summary>
    public static string NotEmpty(string column) => $"btrim({column}) <> ''";

    /// <summary>El valor de la columna pertenece a la lista indicada.</summary>
    public static string OneOf(string column, IEnumerable<string> values)
        => $"{column} IN ({string.Join(", ", values.Select(value => $"'{value}'"))})";
}
