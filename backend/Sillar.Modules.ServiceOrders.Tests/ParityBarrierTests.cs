namespace Sillar.Modules.ServiceOrders.Tests;

/// <summary>
/// Barrera temprana de la costura binario/setup/migrate/seed. El ciclo real de
/// aplicación sigue reservado para [M05B-CICLO] en etapa 6.
/// </summary>
public sealed class ParityBarrierTests
{
    [Fact]
    public void M05b_is_named_in_every_applicable_installation_surface()
    {
        var root = RepositoryRoot();
        var api = File.ReadAllText(Path.Combine(root, "backend", "Sillar.Api", "Sillar.Api.csproj"));
        var verify = File.ReadAllText(Path.Combine(root, "scripts", "verificar.mjs"));
        var e2e = File.ReadAllText(Path.Combine(root, "e2e", "setup", "migrate.ts"));
        var seed = File.ReadAllText(Path.Combine(root, "database", "modules", "service_orders", "02_seed.sql"));

        Assert.Contains("Sillar.Modules.ServiceOrders\\Sillar.Modules.ServiceOrders.csproj", api, StringComparison.Ordinal);
        Assert.Contains("'Sillar.Modules.ServiceOrders'", verify, StringComparison.Ordinal);
        Assert.Contains("applyMigrations('Sillar.Modules.ServiceOrders')", e2e, StringComparison.Ordinal);
        Assert.Contains("'service_orders'", e2e, StringComparison.Ordinal);
        Assert.Contains("SIN CONTENIDO DE NEGOCIO", seed.ToUpperInvariant(), StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO service_orders", seed, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CLAUDE.md")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "[M05b paridad] No se encontró la raíz; la barrera no puede comprobar binario/setup/migrate/seed.");
    }
}
