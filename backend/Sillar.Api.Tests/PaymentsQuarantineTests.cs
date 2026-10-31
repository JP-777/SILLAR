using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sillar.Shared.Data.Modularity;
using Sillar.Shared.Modularity;

using static Sillar.Api.Tests.HostDePrueba;

namespace Sillar.Api.Tests;

/// <summary>
/// Barreras no físicas G4-A. Las huellas persistentes se simulan; ninguna
/// prueba de esta clase abre PostgreSQL, aplica migraciones o activa módulos.
/// </summary>
public sealed class PaymentsQuarantineTests
{
    private const string AssistantCode = "assistant";
    private const string PaymentsCode = "payments";

    [Fact]
    public void P1_Assistant_y_Payments_estan_presentes_como_binarios()
    {
        var deployment = Path.GetDirectoryName(RutaDelBinario())!;

        Assert.True(File.Exists(Path.Combine(deployment, "Sillar.Modules.Assistant.dll")));
        Assert.True(File.Exists(Path.Combine(deployment, "Sillar.Modules.Payments.dll")));
    }

    [Fact]
    public void P2_Ningun_modulo_en_cuarentena_entra_al_catalogo_operativo()
    {
        var discovered = Discover();
        var admitted = Admit(discovered);

        Assert.Contains(discovered, module => module.Code == AssistantCode);
        Assert.Contains(discovered, module => module.Code == PaymentsCode);
        Assert.DoesNotContain(admitted, module => module.Code == AssistantCode);
        Assert.DoesNotContain(admitted, module => module.Code == PaymentsCode);
    }

    [Fact]
    public void P3_El_plan_excluye_ambos_y_conserva_las_migraciones_restantes()
    {
        var discovered = Discover();
        var admitted = Admit(discovered);
        var complete = ModuleGraph.Validate(discovered);
        var operational = ModuleGraph.Validate(admitted);

        Assert.True(complete.IsValid, complete.DescribeErrors());
        Assert.True(operational.IsValid, operational.DescribeErrors());

        var expectedOrder = complete.InstallationOrder
            .Where(module => module.Code is not AssistantCode and not PaymentsCode)
            .Select(module => module.Code);
        Assert.Equal(expectedOrder, operational.InstallationOrder.Select(module => module.Code));

        var expectedMigrations = complete.InstallationOrder
            .Where(module => module.Code is not AssistantCode and not PaymentsCode)
            .OfType<IModuleMigrations>()
            .Select(module => ((IModule)module).Code);
        var actualMigrations = operational.InstallationOrder
            .OfType<IModuleMigrations>()
            .Select(module => ((IModule)module).Code);
        Assert.Equal(expectedMigrations, actualMigrations);

        var payments = Assert.Single(discovered, module => module.Code == PaymentsCode);
        Assert.IsNotAssignableFrom<IModuleMigrations>(payments);
    }

    [Fact]
    public void P4_Registros_activos_inactivos_o_historicos_de_Payments_abortan()
    {
        var active = AssertBlocked(Observation(PaymentsCode, hasModule: true, hasActivation: true, isActive: true));
        Assert.Contains("activación activa", active.Message);

        var inactive = AssertBlocked(Observation(PaymentsCode, hasModule: true, hasActivation: true));
        Assert.Contains("inactiva o histórica", inactive.Message);
        Assert.Contains(PaymentsCode, inactive.Message);
    }

    [Fact]
    public void P5_Medios_o_schema_de_Payments_abortan_sin_escrituras()
    {
        var media = AssertBlocked(Observation(PaymentsCode, hasMedia: true));
        Assert.Contains("owner_module_code es 'payments'", media.Message);

        var schema = AssertBlocked(Observation(PaymentsCode, hasSchema: true));
        Assert.Contains("schema 'payments'", schema.Message);
        Assert.Contains("No se eliminaron filas", schema.Message);
    }

    [Fact]
    public async Task P6_Lectura_fallida_o_estado_indeterminado_siempre_abortan()
    {
        var indeterminate = AssertBlocked(Observation(
            PaymentsCode,
            indeterminateReason: "catálogo simulado incompleto"));
        Assert.Contains("estado indeterminado", indeterminate.Message);

        var observationType = ObservationType();
        var listType = typeof(IReadOnlyList<>).MakeGenericType(observationType);
        var taskType = typeof(Task<>).MakeGenericType(listType);
        var delegateType = typeof(Func<,>).MakeGenericType(typeof(CancellationToken), taskType);
        var token = Expression.Parameter(typeof(CancellationToken), "token");
        var failure = Expression.Throw(
            Expression.New(
                typeof(InvalidOperationException).GetConstructor([typeof(string)])!,
                Expression.Constant("lectura de cuarentena simulada fallida")),
            taskType);
        var reader = Expression.Lambda(delegateType, failure, token).Compile();

        var method = QuarantineType().GetMethod(
            "EnsureAllSafeAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        var pending = Assert.IsAssignableFrom<Task>(
            method.Invoke(null, [reader, CancellationToken.None]));
        var error = Assert.IsAssignableFrom<Exception>(
            await Record.ExceptionAsync(async () => await pending));

        Assert.Equal("Sillar.Api.Modularity.StartupAbortedException", error.GetType().FullName);
        Assert.Contains("No se pudo acreditar", error.Message);
        Assert.Contains("lectura de cuarentena simulada fallida", error.Message);
    }

    [Fact]
    public void P7_Payments_es_NotFound_para_activacion_aunque_exista_su_schema()
    {
        var admitted = Admit(Discover());
        Assert.DoesNotContain(admitted, module => module.Code == PaymentsCode);

        // Evidencia estructural, no ejecución PostgreSQL: ModuleActivationService
        // busca únicamente en DeclaredModules y devuelve NotFound antes de abrir
        // transacción o consultar el schema.
        var source = File.ReadAllText(Path.Combine(
            BackendDirectory().FullName, "Sillar.Core", "Services", "ModuleActivationService.cs"));
        var lookup = source.IndexOf("var module = Catalog.FirstOrDefault", StringComparison.Ordinal);
        var notFound = source.IndexOf(
            "return new ActivationOperation(ActivationOutcome.NotFound);", StringComparison.Ordinal);
        var transaction = source.IndexOf(
            "BeginTransactionAsync(cancellationToken)", StringComparison.Ordinal);
        var schema = source.IndexOf("SchemaExisteAsync(schema", StringComparison.Ordinal);

        Assert.True(lookup >= 0 && lookup < notFound && notFound < transaction && transaction < schema);
        AssertBlocked(Observation(PaymentsCode, hasSchema: true));
    }

    [Fact]
    public void P8_Ni_configuracion_ni_entorno_retiraran_las_dos_cuarentenas()
    {
        var variables = new[]
        {
            "Modules__AssistantQuarantine",
            "Modules__PaymentsQuarantine"
        };
        var previous = variables.ToDictionary(name => name, Environment.GetEnvironmentVariable);

        try
        {
            foreach (var variable in variables)
            {
                Environment.SetEnvironmentVariable(variable, "false");
            }

            var admitted = Admit(Discover());
            Assert.DoesNotContain(admitted, module => module.Code == AssistantCode);
            Assert.DoesNotContain(admitted, module => module.Code == PaymentsCode);
        }
        finally
        {
            foreach (var (variable, value) in previous)
            {
                Environment.SetEnvironmentVariable(variable, value);
            }
        }
    }

    [Fact]
    public void P9_Las_huellas_Assistant_siguen_bloqueando_independientemente()
    {
        var assistant = AssertBlocked(Observation(AssistantCode, hasModule: true));
        Assert.Contains("módulo 'assistant'", assistant.Message);

        // La foto limpia de Payments es aceptada por separado; no oculta la
        // huella anterior ni modifica su interpretación.
        AssertNotBlocked(Observation(PaymentsCode));
    }

    [Fact]
    public void P10_El_resto_conserva_catalogo_orden_dependencias_y_rutas()
    {
        var discovered = Discover();
        var admitted = Admit(discovered);
        var expected = discovered
            .Where(module => module.Code is not AssistantCode and not PaymentsCode)
            .ToArray();

        Assert.Equal(expected.Select(module => module.Code), admitted.Select(module => module.Code));
        foreach (var pair in expected.Zip(admitted))
        {
            Assert.Equal(pair.First.DisplayName, pair.Second.DisplayName);
            Assert.Equal(pair.First.Version, pair.Second.Version);
            Assert.Equal(pair.First.DisplayOrder, pair.Second.DisplayOrder);
            Assert.Equal(pair.First.HardDependencies, pair.Second.HardDependencies);
            Assert.Equal(pair.First.SoftDependencies, pair.Second.SoftDependencies);
        }

        var program = File.ReadAllText(Path.Combine(BackendDirectory().FullName, "Sillar.Api", "Program.cs"));
        Assert.Contains("foreach (var module in boot.Active)", program);
        Assert.Contains("module.MapEndpoints(app);", program);
    }

    [Fact]
    public async Task P11_Payments_no_registra_puertos_positivos_ni_endpoints()
    {
        var payments = Assert.Single(Discover(), module => module.Code == PaymentsCode);
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] =
                    "Host=127.0.0.1;Database=no_abrir;Username=no_abrir;Password=no_abrir"
            })
            .Build();

        payments.RegisterServices(services, configuration);
        var forbidden = new[]
        {
            "IProviderEvidenceVerifier",
            "IProviderCommercialEffectPort",
            "IProviderAuthenticatedOrderDeclarationSource",
            "IM03OrderCorrelationCandidatePort",
            "IPayableOrderCandidate",
            "IExternalSettlementCandidate",
            "IOrderCorrelationCandidate"
        };
        Assert.DoesNotContain(services, descriptor => forbidden.Contains(descriptor.ServiceType.Name));

        var builder = WebApplication.CreateBuilder();
        await using var app = builder.Build();
        var routes = (IEndpointRouteBuilder)app;
        var before = routes.DataSources.Count;
        payments.MapEndpoints(app);
        Assert.Equal(before, routes.DataSources.Count);
    }

    [Fact]
    public void P12_La_guarda_de_ambos_precede_migraciones_y_sincronizacion()
    {
        // Evidencia estructural: no acredita serialización entre procesos.
        var backend = BackendDirectory();
        var bootstrap = File.ReadAllText(Path.Combine(
            backend.FullName, "Sillar.Api", "Modularity", "ModuleBootstrapper.cs"));
        var quarantine = File.ReadAllText(Path.Combine(
            backend.FullName, "Sillar.Api", "Modularity", "QuarantinedModules.cs"));

        var guard = bootstrap.IndexOf(
            "await QuarantinedModules.EnsureSafeAsync(database, cancellationToken);",
            StringComparison.Ordinal);
        var migrations = bootstrap.IndexOf(
            "await ApplyMigrationsIfAllowedAsync(builder, database, logger, cancellationToken);",
            StringComparison.Ordinal);
        var synchronization = bootstrap.IndexOf(
            "var sync = await synchronizer.SynchronizeAsync(admittedModules, cancellationToken);",
            StringComparison.Ordinal);

        Assert.True(guard >= 0 && guard < migrations && guard < synchronization);
        Assert.Contains("AssistantCode", quarantine);
        Assert.Contains("PaymentsCode", quarantine);
        Assert.Contains("EnsureAllSafeAsync", quarantine);
    }

    private static Exception AssertBlocked(object observation)
    {
        var method = QuarantineType().GetMethod(
            "EnsureQuarantinedObservationSafe", BindingFlags.Static | BindingFlags.NonPublic)!;
        var wrapper = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [observation]));
        var error = Assert.IsAssignableFrom<Exception>(wrapper.InnerException);
        Assert.Equal("Sillar.Api.Modularity.StartupAbortedException", error.GetType().FullName);
        return error;
    }

    private static void AssertNotBlocked(object observation)
    {
        var method = QuarantineType().GetMethod(
            "EnsureQuarantinedObservationSafe", BindingFlags.Static | BindingFlags.NonPublic)!;
        method.Invoke(null, [observation]);
    }

    private static object Observation(
        string moduleCode,
        bool hasModule = false,
        bool hasActivation = false,
        bool isActive = false,
        bool hasMedia = false,
        bool hasSchema = false,
        string? indeterminateReason = null)
        => Activator.CreateInstance(ObservationType(),
        [
            moduleCode,
            false,
            true,
            hasModule,
            hasActivation,
            isActive,
            hasMedia,
            hasSchema,
            indeterminateReason
        ])!;

    private static IReadOnlyList<IModule> Discover()
        => QuarantineModuleDiscoveryFixture.Discover();

    private static IReadOnlyList<IModule> Admit(IReadOnlyList<IModule> discovered)
    {
        var method = QuarantineType().GetMethod("Admit", BindingFlags.Static | BindingFlags.NonPublic)!;
        return Assert.IsAssignableFrom<IReadOnlyList<IModule>>(method.Invoke(null, [discovered]));
    }

    private static Type ObservationType() => ApiAssembly().GetType(
        "Sillar.Api.Modularity.QuarantinedModuleObservation", throwOnError: true)!;

    private static Type QuarantineType() => ApiAssembly().GetType(
        "Sillar.Api.Modularity.QuarantinedModules", throwOnError: true)!;

    private static Assembly ApiAssembly() => Assembly.LoadFrom(RutaDelBinario());

    private static DirectoryInfo BackendDirectory()
    {
        var deployment = new FileInfo(RutaDelBinario()).Directory!;
        return deployment.Parent!.Parent!.Parent!.Parent!;
    }
}

/// <summary>
/// Prepara una fotografía inmutable del despliegue real para las dos clases de
/// cuarentena. La publicación es atómica y nunca elimina DLL preexistentes.
/// </summary>
internal static class QuarantineModuleDiscoveryFixture
{
    private static readonly Lazy<bool> DeploymentPrepared = new(
        PrepareDeployment,
        LazyThreadSafetyMode.ExecutionAndPublication);

    internal static IReadOnlyList<IModule> Discover()
    {
        _ = DeploymentPrepared.Value;

        var api = Assembly.LoadFrom(HostDePrueba.RutaDelBinario());
        var type = api.GetType("Sillar.Api.Modularity.ModuleDiscovery", throwOnError: true)!;
        var method = type.GetMethod("Discover", BindingFlags.Static | BindingFlags.Public)!;
        return Assert.IsAssignableFrom<IReadOnlyList<IModule>>(
            method.Invoke(null, [NullLogger.Instance, false]));
    }

    private static bool PrepareDeployment()
    {
        var hostDeployment = Path.GetDirectoryName(HostDePrueba.RutaDelBinario())!;
        foreach (var source in Directory.EnumerateFiles(hostDeployment, "Sillar.Modules.*.dll"))
        {
            PublishIfMissing(source, Path.Combine(AppContext.BaseDirectory, Path.GetFileName(source)));
        }

        return true;
    }

    private static void PublishIfMissing(string source, string destination)
    {
        if (File.Exists(destination))
        {
            AssertSameAssembly(source, destination);
            return;
        }

        var temporary = $"{destination}.g5e-{Environment.ProcessId}-{Guid.NewGuid():N}.tmp";
        try
        {
            File.Copy(source, temporary, overwrite: false);
            try
            {
                File.Move(temporary, destination, overwrite: false);
            }
            catch (IOException) when (File.Exists(destination))
            {
                // Otro proceso publicó primero una DLL completa mediante rename.
            }

            AssertSameAssembly(source, destination);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static void AssertSameAssembly(string source, string destination)
    {
        Assert.Equal(
            SHA256.HashData(File.ReadAllBytes(source)),
            SHA256.HashData(File.ReadAllBytes(destination)));
    }
}
