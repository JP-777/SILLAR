using System.Text.RegularExpressions;

namespace Sillar.Shared.Modularity;

/// <summary>
/// Valida en memoria el grafo de módulos declarado en el código y calcula el
/// orden en que deben registrarse.
/// </summary>
/// <remarks>
/// Es el paso 2 del arranque (SPEC CORE §7) y ocurre antes de tocar la base de
/// datos: lo que se comprueba aquí no depende de la instalación ni de la
/// licencia, sino de cómo está escrito el producto.
/// </remarks>
public static partial class ModuleGraph
{
    /// <summary>Código del módulo núcleo. Del que dependen todos los demás.</summary>
    public const string CoreCode = "core";

    // Límites de las columnas de core.modules (SPEC §4.2). Se validan aquí para
    // que un módulo mal declarado falle con un mensaje legible en el arranque y
    // no con un error de PostgreSQL al sincronizar.
    private const int MaxCodeLength = 40;
    private const int MaxDisplayNameLength = 80;
    private const int MaxDescriptionLength = 300;
    private const int MaxVersionLength = 20;

    [GeneratedRegex(@"^[a-z][a-z0-9_]{1,39}$")]
    private static partial Regex CodePattern { get; }

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex VersionPattern { get; }

    /// <summary>
    /// Comprueba códigos, dependencias y ausencia de ciclos, y devuelve el
    /// orden de instalación.
    /// </summary>
    public static ModuleGraphResult Validate(IReadOnlyList<IModule> modules)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (modules.Count == 0)
        {
            errors.Add(
                "No se descubrió ningún módulo. El host no puede arrancar sin CORE: " +
                "revisa que Sillar.Core esté referenciado por Sillar.Api.");
            return ModuleGraphResult.Invalid(errors, warnings);
        }

        ValidateDeclarations(modules, errors);
        var byCode = IndexByCode(modules, errors);

        // Sin un índice fiable de códigos no tiene sentido revisar dependencias:
        // los mensajes serían ruido sobre un problema ya reportado.
        if (errors.Count > 0)
        {
            return ModuleGraphResult.Invalid(errors, warnings);
        }

        if (!byCode.ContainsKey(CoreCode))
        {
            errors.Add($"Falta el módulo '{CoreCode}'. Es la base de la plataforma y siempre tiene que estar presente.");
            return ModuleGraphResult.Invalid(errors, warnings);
        }

        var dependencies = ResolveDependencies(modules, byCode, errors, warnings);

        if (errors.Count > 0)
        {
            return ModuleGraphResult.Invalid(errors, warnings);
        }

        if (!TryOrder(modules, dependencies, out var ordered, out var cycle))
        {
            errors.Add(
                $"Ciclo de dependencias entre módulos: {cycle}. " +
                "Las dependencias son dirigidas y nunca circulares.");
            return ModuleGraphResult.Invalid(errors, warnings);
        }

        return ModuleGraphResult.Valid(warnings, ordered);
    }

    /// <summary>
    /// Comprueba que ningún módulo activo dependa de forma dura de uno inactivo.
    /// </summary>
    /// <remarks>
    /// Es el paso 6 del arranque y ocurre ya con las activaciones leídas de la
    /// base de datos. Si Seguimiento está activo y Órdenes de Servicio no, el
    /// sistema no arranca: es preferible caerse en el despliegue, donde alguien
    /// lo ve, a funcionar a medias en producción.
    /// </remarks>
    /// <param name="modules">Módulos declarados en el código.</param>
    /// <param name="activeCodes">Códigos de los módulos activos en esta instalación.</param>
    /// <returns>Lista de problemas. Vacía si las activaciones son coherentes.</returns>
    public static IReadOnlyList<string> ValidateActivations(
        IReadOnlyList<IModule> modules,
        IReadOnlySet<string> activeCodes)
    {
        var problems = new List<string>();

        foreach (var module in modules.Where(m => activeCodes.Contains(m.Code)))
        {
            foreach (var required in module.HardDependencies.Where(d => !activeCodes.Contains(d)))
            {
                problems.Add(
                    $"El módulo '{module.Code}' está activo, pero su dependencia dura '{required}' no lo está. " +
                    $"Actívala o desactiva '{module.Code}'.");
            }
        }

        return problems;
    }

    /// <summary>
    /// Calcula qué módulos quedarían activos tras activar o desactivar uno.
    /// </summary>
    /// <remarks>
    /// No decide si la operación es legítima: solo dice cómo quedaría el mundo.
    /// Quien llama pasa el resultado por <see cref="ValidateActivations"/>, que es
    /// la comprobación que hace el arranque, y así ambos juzgan con el mismo
    /// criterio en vez de con dos parecidos.
    /// </remarks>
    /// <param name="activeCodes">Módulos activos ahora.</param>
    /// <param name="moduleCode">Módulo que se activa o se desactiva.</param>
    /// <param name="activate">Verdadero para activar, falso para desactivar.</param>
    public static IReadOnlySet<string> ResultingActiveCodes(
        IReadOnlySet<string> activeCodes,
        string moduleCode,
        bool activate)
    {
        var resulting = new HashSet<string>(activeCodes, StringComparer.OrdinalIgnoreCase);

        if (activate)
        {
            resulting.Add(moduleCode);
        }
        else
        {
            resulting.Remove(moduleCode);
        }

        return resulting;
    }

    /// <summary>
    /// Devuelve las dependencias duras del módulo indicado que están inactivas.
    /// </summary>
    /// <remarks>
    /// Es lo que hay que nombrar en el 409 al activar: decir «faltan
    /// dependencias» sin decir cuáles obliga a la persona a adivinar.
    /// </remarks>
    public static IReadOnlyList<string> MissingHardDependencies(
        IReadOnlyList<IModule> modules,
        IReadOnlySet<string> activeCodes,
        string moduleCode)
    {
        var module = modules.FirstOrDefault(
            candidate => string.Equals(candidate.Code, moduleCode, StringComparison.OrdinalIgnoreCase));

        return module is null
            ? []
            : [.. module.HardDependencies.Where(required => !activeCodes.Contains(required)).Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Códigos activos en la base que el binario no declara (ADR-019).
    /// </summary>
    /// <remarks>
    /// Distinto de <see cref="ValidateActivations"/>: aquello comprueba
    /// coherencia entre módulos que sí existen los dos lados. Esto comprueba
    /// que lo que la base dice activo siga estando en el binario — si no, es
    /// un despliegue incompleto, no una instalación degradada, y el host tiene
    /// que abortar en vez de servir el producto sin ese módulo en silencio.
    /// </remarks>
    /// <param name="modules">Módulos que el binario trae.</param>
    /// <param name="activeCodesInDatabase">
    /// Códigos marcados activos en <c>core.module_activations</c>, tal como
    /// estaban antes de sincronizar — sin filtrar todavía contra el binario.
    /// </param>
    public static IReadOnlyList<string> ActiveButUndeclared(
        IReadOnlyList<IModule> modules,
        IEnumerable<string> activeCodesInDatabase)
    {
        var declared = modules.Select(module => module.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return [.. activeCodesInDatabase
            .Where(code => !declared.Contains(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Devuelve los módulos activos que dependen de forma dura del indicado.
    /// </summary>
    /// <remarks>
    /// Son los que impiden desactivarlo. Las dependencias blandas no aparecen
    /// aquí a propósito: tolerar la ausencia es justamente lo que significan, y
    /// bloquear por una sería negar el concepto.
    /// </remarks>
    public static IReadOnlyList<string> ActiveHardDependents(
        IReadOnlyList<IModule> modules,
        IReadOnlySet<string> activeCodes,
        string moduleCode)
        => [.. modules
            .Where(module => activeCodes.Contains(module.Code)
                && !string.Equals(module.Code, moduleCode, StringComparison.OrdinalIgnoreCase)
                && module.HardDependencies.Contains(moduleCode, StringComparer.OrdinalIgnoreCase))
            .Select(module => module.Code)
            .Order(StringComparer.Ordinal)];

    /// <summary>Comprueba que cada módulo se declara a sí mismo correctamente.</summary>
    private static void ValidateDeclarations(IReadOnlyList<IModule> modules, List<string> errors)
    {
        foreach (var module in modules)
        {
            var origin = module.GetType().FullName ?? module.GetType().Name;

            if (string.IsNullOrWhiteSpace(module.Code) || !CodePattern.IsMatch(module.Code))
            {
                errors.Add(
                    $"{origin} declara el código '{module.Code}', que no es válido. " +
                    $"Debe empezar por letra minúscula, seguir con minúsculas, dígitos o guion bajo, " +
                    $"y no pasar de {MaxCodeLength} caracteres: es también el nombre de su schema.");
            }

            if (string.IsNullOrWhiteSpace(module.DisplayName) || module.DisplayName.Length > MaxDisplayNameLength)
            {
                errors.Add($"El módulo '{module.Code}' necesita un nombre visible de 1 a {MaxDisplayNameLength} caracteres.");
            }

            if (string.IsNullOrWhiteSpace(module.Description) || module.Description.Length > MaxDescriptionLength)
            {
                errors.Add(
                    $"El módulo '{module.Code}' necesita una descripción de 1 a {MaxDescriptionLength} caracteres: " +
                    "es lo que lee el negocio en el panel para decidir si lo activa.");
            }

            if (module.DisplayOrder < 0)
            {
                errors.Add($"El módulo '{module.Code}' declara la posición {module.DisplayOrder}, que no puede ser negativa.");
            }

            if (string.IsNullOrWhiteSpace(module.Version)
                || module.Version.Length > MaxVersionLength
                || !VersionPattern.IsMatch(module.Version))
            {
                errors.Add($"El módulo '{module.Code}' declara la versión '{module.Version}'. Se espera mayor.menor.parche.");
            }
        }
    }

    /// <summary>Indexa por código y detecta duplicados.</summary>
    private static Dictionary<string, IModule> IndexByCode(IReadOnlyList<IModule> modules, List<string> errors)
    {
        var byCode = new Dictionary<string, IModule>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in modules)
        {
            if (string.IsNullOrWhiteSpace(module.Code))
            {
                continue;
            }

            if (byCode.TryGetValue(module.Code, out var existing))
            {
                errors.Add(
                    $"Dos módulos declaran el código '{module.Code}': " +
                    $"{existing.GetType().FullName} y {module.GetType().FullName}. " +
                    "El código identifica al módulo y a su schema, así que es único.");
                continue;
            }

            byCode.Add(module.Code, module);
        }

        return byCode;
    }

    /// <summary>
    /// Resuelve las dependencias de cada módulo contra los códigos existentes.
    /// </summary>
    /// <remarks>
    /// Una dependencia dura hacia un módulo inexistente es un error: el módulo
    /// no puede funcionar. Una blanda hacia un módulo inexistente es solo un
    /// aviso —tolerar la ausencia es justamente lo que significa ser blanda—,
    /// pero casi siempre es una errata en el código.
    /// </remarks>
    private static Dictionary<string, List<string>> ResolveDependencies(
        IReadOnlyList<IModule> modules,
        Dictionary<string, IModule> byCode,
        List<string> errors,
        List<string> warnings)
    {
        var dependencies = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in modules)
        {
            var resolved = new List<string>();

            foreach (var required in module.HardDependencies)
            {
                if (string.Equals(required, module.Code, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"El módulo '{module.Code}' se declara dependiente de sí mismo.");
                    continue;
                }

                if (!byCode.ContainsKey(required))
                {
                    errors.Add(
                        $"El módulo '{module.Code}' depende de forma dura de '{required}', " +
                        "que no existe en la solución.");
                    continue;
                }

                resolved.Add(byCode[required].Code);
            }

            foreach (var optional in module.SoftDependencies)
            {
                if (string.Equals(optional, module.Code, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"El módulo '{module.Code}' se declara dependiente blando de sí mismo.");
                    continue;
                }

                if (!byCode.ContainsKey(optional))
                {
                    warnings.Add(
                        $"El módulo '{module.Code}' declara la dependencia blanda '{optional}', " +
                        "que no existe en la solución. Se ignora, pero revisa si es una errata.");
                    continue;
                }

                if (module.HardDependencies.Contains(optional, StringComparer.OrdinalIgnoreCase))
                {
                    warnings.Add(
                        $"El módulo '{module.Code}' declara '{optional}' como dependencia dura y blanda a la vez. " +
                        "Se trata como dura.");
                    continue;
                }

                resolved.Add(byCode[optional].Code);
            }

            // Regla del SPEC de CORE §3: todos dependen de CORE, y de forma dura.
            if (!string.Equals(module.Code, CoreCode, StringComparison.OrdinalIgnoreCase)
                && !module.HardDependencies.Contains(CoreCode, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"El módulo '{module.Code}' no declara la dependencia dura de '{CoreCode}'. " +
                    "Todo módulo se enchufa a CORE.");
            }

            dependencies[module.Code] = resolved;
        }

        return dependencies;
    }

    /// <summary>
    /// Ordena los módulos dejando cada uno detrás de aquellos de los que
    /// depende, y detecta ciclos por el camino.
    /// </summary>
    /// <remarks>
    /// Recorrido en profundidad con marcado: gris = en curso, negro = resuelto.
    /// Encontrar un gris es encontrar un ciclo. El recorrido es determinista
    /// —se ordena por posición en el panel y luego por código— para que dos
    /// arranques produzcan siempre el mismo orden.
    /// </remarks>
    private static bool TryOrder(
        IReadOnlyList<IModule> modules,
        Dictionary<string, List<string>> dependencies,
        out IReadOnlyList<IModule> ordered,
        out string? cycle)
    {
        var byCode = modules.ToDictionary(m => m.Code, StringComparer.OrdinalIgnoreCase);
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<IModule>(modules.Count);
        var path = new List<string>();

        foreach (var module in modules.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Code, StringComparer.Ordinal))
        {
            if (!Visit(module.Code, out cycle))
            {
                ordered = [];
                return false;
            }
        }

        ordered = result;
        cycle = null;
        return true;

        bool Visit(string code, out string? foundCycle)
        {
            foundCycle = null;

            if (state.TryGetValue(code, out var mark))
            {
                if (mark == 2)
                {
                    return true;
                }

                // Gris: hemos vuelto a un módulo que sigue en curso.
                var from = path.IndexOf(code);
                foundCycle = string.Join(" → ", path.Skip(from).Append(code));
                return false;
            }

            state[code] = 1;
            path.Add(code);

            foreach (var required in dependencies[code].OrderBy(c => c, StringComparer.Ordinal))
            {
                if (!Visit(required, out foundCycle))
                {
                    return false;
                }
            }

            path.RemoveAt(path.Count - 1);
            state[code] = 2;
            result.Add(byCode[code]);
            return true;
        }
    }
}
