namespace Sillar.Shared.Configuration;

/// <summary>
/// Carga el archivo <c>.env</c> de la raíz del repositorio como variables de
/// entorno del proceso.
/// </summary>
/// <remarks>
/// Docker Compose lee <c>.env</c> por su cuenta, pero <c>dotnet run</c> no. Sin
/// esto habría que repetir la cadena de conexión en cada máquina y el proyecto
/// alterna entre Windows y Arch Linux (ADR-006).
///
/// Solo para desarrollo y para las herramientas de diseño de EF Core. En
/// producción la configuración llega por variables de entorno reales, y por eso
/// lo que ya está definido en el entorno nunca se sobrescribe.
/// </remarks>
public static class DotEnv
{
    private const string FileName = ".env";

    /// <summary>
    /// Busca <c>.env</c> hacia arriba desde el directorio del ejecutable y desde
    /// el directorio de trabajo, y publica sus claves en el entorno del proceso.
    /// </summary>
    /// <returns>Ruta del archivo cargado, o <c>null</c> si no se encontró.</returns>
    /// <remarks>
    /// Debe llamarse <b>antes</b> de construir el host: el proveedor de
    /// configuración por variables de entorno lee una sola vez, al arrancar.
    /// </remarks>
    public static string? Load()
    {
        var path = Find(AppContext.BaseDirectory) ?? Find(Directory.GetCurrentDirectory());
        if (path is null)
        {
            return null;
        }

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();

            // Línea vacía o comentario. El '#' solo cuenta al principio: una
            // contraseña puede contener almohadillas y no es asunto nuestro.
            if (trimmed.Length == 0 || trimmed[0] == '#')
            {
                continue;
            }

            if (trimmed.StartsWith("export ", StringComparison.Ordinal))
            {
                trimmed = trimmed["export ".Length..].TrimStart();
            }

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = trimmed[..separator].TrimEnd();
            var value = Unquote(trimmed[(separator + 1)..].Trim());

            // Lo que ya viene del entorno manda: permite sobreescribir un valor
            // puntual sin editar el archivo.
            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        return path;
    }

    private static string? Find(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, FileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string Unquote(string value)
        => value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0]
            ? value[1..^1]
            : value;
}
