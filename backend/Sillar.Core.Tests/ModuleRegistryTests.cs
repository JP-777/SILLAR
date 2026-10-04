using Sillar.Core.Contracts;
using Sillar.Core.Modularity;

namespace Sillar.Core.Tests;

/// <summary>
/// Contrato de lectura de la foto de activaciones del arranque.
/// </summary>
public sealed class ModuleRegistryTests
{
    [Fact]
    public void IsActive_devuelve_true_si_el_modulo_esta_en_la_foto_activa()
    {
        var snapshot = new ModuleActivationSnapshot(
        [
            new ActiveModule("catalog", "Catálogo", "1.0.0")
        ]);

        IModuleRegistry registry = new ModuleRegistry(snapshot);

        Assert.True(registry.IsActive("catalog"));
    }

    [Fact]
    public void IsActive_devuelve_false_si_el_modulo_no_esta_en_la_foto_activa()
    {
        var snapshot = new ModuleActivationSnapshot(
        [
            new ActiveModule("core", "CORE", "1.0.0")
        ]);

        IModuleRegistry registry = new ModuleRegistry(snapshot);

        Assert.False(registry.IsActive("catalog"));
    }
}
