using System.Text;

namespace Sillar.Shared.Modularity;

/// <summary>
/// Resultado de validar el grafo de módulos declarado en el código.
/// </summary>
/// <remarks>
/// Un error aquí no es un problema de datos ni de licencia: es un fallo de
/// compilación lógica del producto. El host aborta el arranque.
/// </remarks>
public sealed class ModuleGraphResult
{
    private ModuleGraphResult(
        IReadOnlyList<string> errors,
        IReadOnlyList<string> warnings,
        IReadOnlyList<IModule> installationOrder)
    {
        Errors = errors;
        Warnings = warnings;
        InstallationOrder = installationOrder;
    }

    /// <summary>Problemas que impiden arrancar.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>Avisos que no impiden arrancar, pero que conviene mirar.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Módulos ordenados de forma que uno nunca aparece antes que aquellos de
    /// los que depende. Vacío si la validación falló.
    /// </summary>
    public IReadOnlyList<IModule> InstallationOrder { get; }

    /// <summary>El grafo es correcto y se puede continuar el arranque.</summary>
    public bool IsValid => Errors.Count == 0;

    internal static ModuleGraphResult Invalid(IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
        => new(errors, warnings, []);

    internal static ModuleGraphResult Valid(IReadOnlyList<string> warnings, IReadOnlyList<IModule> installationOrder)
        => new([], warnings, installationOrder);

    /// <summary>
    /// Devuelve los errores en un texto de varias líneas, listo para el log de
    /// aborto. Quien lo lea tiene que poder arreglar el problema sin depurar.
    /// </summary>
    public string DescribeErrors()
    {
        var sb = new StringBuilder();
        sb.AppendLine("El grafo de módulos declarado en el código no es válido:");
        foreach (var error in Errors)
        {
            sb.Append("  · ").AppendLine(error);
        }

        return sb.ToString().TrimEnd();
    }
}
