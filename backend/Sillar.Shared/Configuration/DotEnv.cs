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
    /// De qué archivo se cargó la configuración, o <c>null</c> si no se
    /// encontró ninguno.
    /// </summary>
    /// <remarks>
    /// Existe para que el arranque pueda <b>decirlo</b>. Un
    /// <c>.env</c> equivocado se ve exactamente igual que uno correcto: el
    /// proceso levanta, se conecta y funciona — contra la base de otro. La
    /// búsqueda sube por el árbol de directorios, así que basta lanzar el
    /// proceso desde el sitio equivocado para cargar el de al lado.
    /// <para>
    /// Pasó de verdad el 21 de agosto de 2026: un módulo en construcción se
    /// registró en la base de la demostración y nadie lo supo hasta días
    /// después. La carga era muda.
    /// </para>
    /// </remarks>
    public static string? LoadedFrom { get; private set; }

    /// <summary>
    /// Busca <c>.env</c> hacia arriba desde el directorio del ejecutable y desde
    /// el directorio de trabajo, y publica sus claves en el entorno del proceso.
    /// </summary>
    /// <returns>Ruta del archivo cargado, o <c>null</c> si no se encontró.</returns>
    /// <remarks>
    /// <para>
    /// Debe llamarse <b>antes</b> de construir el host: el proveedor de
    /// configuración por variables de entorno lee una sola vez, al arrancar.
    /// </para>
    /// <para>
    /// <b>Gana el árbol del binario, no el directorio desde el que se lanza.</b>
    /// Se prueba <c>AppContext.BaseDirectory</c> primero, y solo si ahí no hay
    /// ninguno se mira el directorio de trabajo. Comprobado provocándolo:
    /// ejecutando desde una carpeta con su propio <c>.env</c>, se cargó el del
    /// árbol del proyecto igualmente. Por eso el arranque <b>dice de dónde
    /// cargó y a qué base apunta</b> — es lo único que distingue una
    /// configuración correcta de una que se ve igual y va a otro sitio.
    /// </para>
    /// </remarks>
    public static string? Load()
    {
        var path = Find(AppContext.BaseDirectory) ?? Find(Directory.GetCurrentDirectory());
        LoadedFrom = path;

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
