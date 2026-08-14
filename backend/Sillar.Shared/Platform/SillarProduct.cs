using System.Reflection;

namespace Sillar.Shared.Platform;

/// <summary>
/// Identidad del producto. Fuente única para <c>/api/capabilities</c> y para la
/// versión que se registra en <c>core.installation</c>.
/// </summary>
public static class SillarProduct
{
    /// <summary>Nombre del producto. Nunca el del negocio instalado.</summary>
    public const string Name = "SILLAR";

    /// <summary>
    /// Versión del producto, tomada del ensamblado y fijada en
    /// <c>Directory.Build.props</c>.
    /// </summary>
    /// <remarks>
    /// Se recorta lo que va tras '+', que es metadato de compilación y no
    /// interesa a quien consume el API.
    /// </remarks>
    public static string Version { get; } = Resolve();

    private static string Resolve()
    {
        var informational = typeof(SillarProduct).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return typeof(SillarProduct).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        var plus = informational.IndexOf('+');
        return plus < 0 ? informational : informational[..plus];
    }
}
