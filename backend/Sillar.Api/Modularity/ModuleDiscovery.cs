using System.Reflection;
using Microsoft.Extensions.Logging;
using Sillar.Shared.Modularity;

namespace Sillar.Api.Modularity;

/// <summary>
/// Encuentra las implementaciones de <see cref="IModule"/> presentes en el
/// despliegue.
/// </summary>
/// <remarks>
/// Paso 1 del arranque (SPEC CORE §7). Los módulos se publican junto al host
/// (ADR-002: monolito modular, no complementos cargados en caliente), así que
/// basta con mirar los ensamblados del propio despliegue.
///
/// Se recorren los archivos y no los ensamblados ya cargados porque .NET carga
/// un ensamblado la primera vez que se usa: un módulo cuyo código todavía no se
/// ha ejecutado no estaría en memoria y pasaría desapercibido.
/// </remarks>
internal static class ModuleDiscovery
{
    private const string AssemblyPattern = "Sillar.*.dll";

    /// <summary>Descubre e instancia los módulos del despliegue.</summary>
    public static IReadOnlyList<IModule> Discover(ILogger logger)
    {
        var modules = new List<IModule>();

        foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory, AssemblyPattern).Order())
        {
            Assembly assembly;

            try
            {
                assembly = Assembly.LoadFrom(file);
            }
            catch (BadImageFormatException)
            {
                // No es un ensamblado gestionado. No es asunto nuestro.
                continue;
            }

            foreach (var type in GetModuleTypes(assembly, logger))
            {
                if (Activator.CreateInstance(type) is not IModule module)
                {
                    throw new StartupAbortedException(
                        $"No se pudo instanciar el módulo '{type.FullName}'. " +
                        "Toda implementación de IModule necesita un constructor sin parámetros.");
                }

                modules.Add(module);
                logger.LogDebug("Módulo descubierto: {Code} ({Type}).", module.Code, type.FullName);
            }
        }

        return modules;
    }

    private static IEnumerable<Type> GetModuleTypes(Assembly assembly, ILogger logger)
    {
        Type[] types;

        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            // Un ensamblado con tipos rotos no debe tumbar el descubrimiento en
            // silencio: se avisa y se sigue con lo que sí cargó.
            logger.LogWarning(
                "No se pudieron cargar todos los tipos de '{Assembly}'. Se continúa con los disponibles.",
                assembly.GetName().Name);
            types = [.. exception.Types.Where(type => type is not null)!];
        }

        return types.Where(type =>
            typeof(IModule).IsAssignableFrom(type)
            && type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false });
    }
}
