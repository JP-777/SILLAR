using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Sillar.Core.Modularity;
using Sillar.Shared.Data.Modularity;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

/// <summary>
/// Frontera PostgreSQL que construye la foto de módulos activos del arranque.
/// </summary>
public sealed class ModuleSynchronizerReadActivePostgresTests
{
    [Fact]
    public async Task ReadActiveAsync_devuelve_exactamente_el_modulo_activo_de_PostgreSQL()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            var core = Instalador().Orden().Single(module => module.Code == "core");
            await ((IModuleMigrations)core).ApplyMigrationsAsync(cadena, ct);

            var declarados = Desplegados().Modules
                .Where(module => module.Code is "core" or "demo_catalog" or "demo_crm")
                .ToArray();

            // CORE se incluye únicamente porque ambos módulos objetivo declaran
            // dependencia dura hacia él. Los dos estados que esta prueba
            // contrasta son demo_catalog (activo) y demo_crm (inactivo).
            Assert.Equal(3, declarados.Length);

            // Primera sincronización: crea las tres filas declaradas
            // y sus activaciones. Los módulos no CORE nacen inactivos.
            await using (var inicial = Contexto(cadena))
            {
                var synchronizer = new ModuleSynchronizer(
                    inicial,
                    NullLogger<ModuleSynchronizer>.Instance);

                await synchronizer.SynchronizeAsync(declarados, ct);
            }

            // Prepara en PostgreSQL un activo y un inactivo.
            await using (var preparar = Contexto(cadena))
            {
                var activaciones = await preparar.ModuleActivations
                    .Include(activation => activation.Module)
                    .ToDictionaryAsync(
                        activation => activation.Module!.Code,
                        StringComparer.Ordinal,
                        ct);

                Assert.Equal(3, activaciones.Count);
                Assert.True(activaciones["core"].IsActive);

                activaciones["demo_catalog"].IsActive = true;
                activaciones["demo_catalog"].ActivatedAt = DateTimeOffset.UtcNow;
                activaciones["demo_catalog"].DeactivatedAt = null;

                activaciones["demo_crm"].IsActive = false;
                activaciones["demo_crm"].ActivatedAt = null;

                await preparar.SaveChangesAsync(ct);
            }

            // Contexto nuevo: ninguna entidad de la preparación puede aportar
            // el resultado. SynchronizeAsync debe volver a PostgreSQL y llegar
            // a ReadActiveAsync.
            await using var lectura = Contexto(cadena);
            var resultado = await new ModuleSynchronizer(
                    lectura,
                    NullLogger<ModuleSynchronizer>.Instance)
                .SynchronizeAsync(declarados, ct);

            var activos = resultado.Active
                .Select(module => module.Code)
                .ToArray();

            // CORE permanece activo por invariante. Entre los dos módulos
            // preparados en PostgreSQL, entra exactamente el activo.
            Assert.Equal(["core", "demo_catalog"], activos);
            Assert.DoesNotContain("demo_crm", activos);
        }, ct);
    }
}
