using System.Xml;
using System.Xml.Linq;

namespace Sillar.Api.Tests;

/// <summary>Inspecciona ProjectReference reales, incluidas referencias condicionadas.</summary>
public sealed class FronterasDeProyectosTests
{
    [Fact]
    public void Los_proyectos_de_producto_respetan_la_direccion_y_los_contratos_de_modulo()
    {
        var host = new FileInfo(HostDePrueba.RutaDelBinario());
        var backend = host.Directory!.Parent!.Parent!.Parent!.Parent!;
        Assert.True(File.Exists(Path.Combine(backend.FullName, "Sillar.sln")), "No se encontró el backend del host acreditado.");
        var modules = QuarantineModuleDiscoveryFixture.Discover();
        var byAssembly = modules.ToDictionary(m => m.GetType().Assembly.GetName().Name!, StringComparer.Ordinal);
        var projects = backend.EnumerateDirectories("Sillar.*")
            .Select(d => Path.Combine(d.FullName, d.Name + ".csproj"))
            .Where(File.Exists)
            .Where(p => !Path.GetFileNameWithoutExtension(p).EndsWith(".Tests", StringComparison.Ordinal))
            .ToArray();
        Assert.True(projects.Length >= 15, "El inventario de proyectos de producto está incompleto.");
        var failures = new List<string>();
        foreach (var project in projects)
        {
            var name = Path.GetFileNameWithoutExtension(project);
            var document = XDocument.Load(project, LoadOptions.SetLineInfo);
            var permittedContracts = new HashSet<string>(StringComparer.Ordinal);
            if (byAssembly.TryGetValue(name, out var module))
            {
                var dependencies = module.HardDependencies.Concat(module.SoftDependencies).Append(module.Code).ToHashSet(StringComparer.Ordinal);
                foreach (var dependency in modules.Where(m => dependencies.Contains(m.Code)))
                    permittedContracts.Add(dependency.GetType().Assembly.GetName().Name! + ".Contracts");
            }
            foreach (var reference in document.Descendants().Where(e => e.Name.LocalName == "ProjectReference"))
            {
                var include = reference.Attribute("Include")?.Value;
                var line = ((IXmlLineInfo)reference).LineNumber;
                if (string.IsNullOrWhiteSpace(include) || include.Contains("$(", StringComparison.Ordinal))
                {
                    failures.Add($"{project}:{line}: referencia vacía o dinámica que la barrera no puede acreditar.");
                    continue;
                }
                var target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, include.Replace('\\', Path.DirectorySeparatorChar)));
                if (!target.StartsWith(backend.FullName + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(target))
                {
                    failures.Add($"{project}:{line}: el proyecto referenciado no existe: {include}.");
                    continue;
                }
                var targetName = Path.GetFileNameWithoutExtension(target);
                if (!AllowedReference(name, targetName, permittedContracts))
                    failures.Add($"{project}:{line}: {name} no puede referenciar {targetName}; usa únicamente contratos de dependencias declaradas.");
            }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData("Sillar.Modules.Sales", "Sillar.Modules.Crm.Contracts", true)]
    [InlineData("Sillar.Modules.Sales", "Sillar.Modules.Crm", false)]
    [InlineData("Sillar.Modules.Sales", "Sillar.Core", false)]
    [InlineData("Sillar.Modules.Sales", "Sillar.Modules.Tracking.Contracts", false)]
    [InlineData("Sillar.Modules.Sales.Contracts", "Sillar.Shared.Data", false)]
    [InlineData("Sillar.Core", "Sillar.Modules.Sales.Contracts", false)]
    [InlineData("Sillar.Shared", "Sillar.Core.Contracts", false)]
    [InlineData("Sillar.Shared.Data", "Sillar.Shared", true)]
    [InlineData("Sillar.Api", "Sillar.Modules.Sales", true)]
    public void La_barrera_distingue_contrato_de_implementacion_y_dependencia_no_declarada(
        string origin, string target, bool expected)
        => Assert.Equal(expected, AllowedReference(origin, target,
            new HashSet<string>(StringComparer.Ordinal) { "Sillar.Modules.Crm.Contracts", "Sillar.Core.Contracts" }));

    private static bool AllowedReference(string origin, string target, IReadOnlySet<string> declaredContracts)
    {
        if (origin == target || target.EndsWith(".Tests", StringComparison.Ordinal)) return false;
        if (origin == "Sillar.Api" || origin.StartsWith("Sillar.Host.", StringComparison.Ordinal)) return true;
        if (origin == "Sillar.Shared") return false;
        if (origin == "Sillar.Core.Contracts") return target == "Sillar.Shared";
        if (origin == "Sillar.Shared.Data") return target == "Sillar.Shared";
        if (origin == "Sillar.Core") return target is "Sillar.Shared" or "Sillar.Shared.Data" or "Sillar.Core.Contracts";
        if (origin.EndsWith(".Contracts", StringComparison.Ordinal))
            return target == "Sillar.Shared" || target.EndsWith(".Contracts", StringComparison.Ordinal);
        if (origin == "Sillar.Modules.Demo")
            return target is "Sillar.Shared" or "Sillar.Core.Contracts";
        if (origin.StartsWith("Sillar.Modules.", StringComparison.Ordinal))
            return target is "Sillar.Shared" or "Sillar.Shared.Data" || declaredContracts.Contains(target);
        return false;
    }
}
