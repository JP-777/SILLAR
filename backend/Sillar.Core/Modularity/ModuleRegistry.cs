using Sillar.Core.Contracts;

namespace Sillar.Core.Modularity;

/// <summary>
/// Implementación de <see cref="IModuleRegistry"/> sobre la foto de activaciones
/// del arranque.
/// </summary>
internal sealed class ModuleRegistry : IModuleRegistry
{
    private readonly IReadOnlyList<ActiveModule> _active;
    private readonly HashSet<string> _activeCodes;

    public ModuleRegistry(ModuleActivationSnapshot snapshot)
    {
        _active = snapshot.ActiveModules;
        _activeCodes = snapshot.ActiveModules
            .Select(module => module.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public bool IsActive(string moduleCode) => _activeCodes.Contains(moduleCode);

    /// <inheritdoc />
    public IReadOnlyList<ActiveModule> GetActive() => _active;
}
