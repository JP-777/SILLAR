using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Sillar.Shared.Data.Modularity;
using Sillar.Shared.Modularity;

using static Sillar.Api.Tests.HostDePrueba;

namespace Sillar.Api.Tests;

/// <summary>
/// Barreras no físicas de la cuarentena M12. Las observaciones persistentes se
/// simulan; la consulta real a PostgreSQL queda pendiente de QA física.
/// </summary>
public sealed class AssistantQuarantineTests
{
    private const string AssistantCode = "assistant";
    private const string PaymentsCode = "payments";

    [Fact]
    public void T1_Assistant_esta_en_el_binario_pero_no_en_los_modulos_admitidos()
    {
        var deployment = Path.GetDirectoryName(RutaDelBinario())!;
        Assert.True(File.Exists(Path.Combine(deployment, "Sillar.Modules.Assistant.dll")));

        var discovered = Discover();
        var admitted = Admit(discovered);

        Assert.Contains(discovered, module => module.Code == AssistantCode);
        Assert.DoesNotContain(discovered, module => module.Code == PaymentsCode);
        Assert.DoesNotContain(admitted, module => module.Code == AssistantCode);

        // Caso controlado "solo Assistant": el oráculo se construye con los
        // módulos reales descubiertos, pero sin Payments. Así se conserva la
        // cobertura original y el verde no depende de que falte su DLL.
        var onlyAssistantQuarantined = discovered
            .Where(module => module.Code != PaymentsCode)
            .ToArray();
        var admittedWithoutPayments = Admit(onlyAssistantQuarantined);
        var expectedWithoutPayments = onlyAssistantQuarantined
            .Where(module => module.Code != AssistantCode)
            .Select(module => module.Code)
            .ToArray();

        Assert.Contains(onlyAssistantQuarantined, module => module.Code == AssistantCode);
        Assert.DoesNotContain(onlyAssistantQuarantined, module => module.Code == PaymentsCode);
        Assert.DoesNotContain(admittedWithoutPayments, module => module.Code == AssistantCode);
        Assert.Equal(expectedWithoutPayments, admittedWithoutPayments.Select(module => module.Code));
    }

    [Fact]
    public void T2_El_plan_excluye_las_dos_migraciones_Assistant_y_conserva_las_demas()
    {
        var discovered = Discover();
        var admitted = Admit(discovered);
        var completeGraph = ModuleGraph.Validate(discovered);
        var admittedGraph = ModuleGraph.Validate(admitted);

        Assert.True(completeGraph.IsValid, completeGraph.DescribeErrors());
        Assert.True(admittedGraph.IsValid, admittedGraph.DescribeErrors());

        Assert.Contains(discovered, module => module.Code == AssistantCode);
        Assert.DoesNotContain(discovered, module => module.Code == PaymentsCode);
        Assert.DoesNotContain(admitted, module => module.Code == AssistantCode);
        Assert.DoesNotContain(admitted, module => module.Code == PaymentsCode);

        var expectedCodes = completeGraph.InstallationOrder
            .Where(module => module.Code is not AssistantCode and not PaymentsCode)
            .Select(module => module.Code)
            .ToArray();
        var actualCodes = admittedGraph.InstallationOrder.Select(module => module.Code).ToArray();
        Assert.Equal(expectedCodes, actualCodes);

        var installationPlan = admittedGraph.InstallationOrder
            .Where(module => module is IModuleMigrations)
            .Select(module => module.Code)
            .ToArray();
        Assert.DoesNotContain(AssistantCode, installationPlan);

        var assistant = Assert.IsAssignableFrom<IModuleMigrations>(
            Assert.Single(discovered, module => module.Code == AssistantCode));
        var known = assistant.KnownMigrations(
            "Host=127.0.0.1;Database=no_abrir;Username=no_abrir;Password=no_abrir");
        Assert.Contains(known, migration => migration.Contains("AssistantInitial", StringComparison.Ordinal));
        Assert.Contains(known, migration => migration.Contains("AssistantQuotaLedger", StringComparison.Ordinal));
    }

    [Fact]
    public void T3_Una_activacion_activa_aborta_con_el_estado_real_visible()
    {
        var error = AssertBlocked(Observation(
            hasModule: true,
            hasActivation: true,
            isActive: true));

        Assert.Contains("activación activa", error.Message);
        Assert.Contains("No se eliminaron filas", error.Message);
        Assert.Contains("autorizada por JP", error.Message);
    }

    [Fact]
    public void T4_Huellas_inactivas_historicas_o_medios_tambien_abortan()
    {
        var inactive = AssertBlocked(Observation(
            hasModule: true,
            hasActivation: true,
            isActive: false));
        Assert.Contains("inactiva o histórica", inactive.Message);

        var media = AssertBlocked(Observation(hasMedia: true));
        Assert.Contains("owner_module_code", media.Message);
    }

    [Fact]
    public void T5_Un_schema_Assistant_preexistente_requiere_conciliacion()
    {
        var error = AssertBlocked(Observation(hasSchema: true));

        Assert.Contains("schema 'assistant'", error.Message);
        Assert.Contains("conciliación", error.Message);
    }

    [Fact]
    public void T6_Configuracion_y_entorno_no_pueden_retirar_la_cuarentena()
    {
        const string setting = "Modules__AssistantQuarantine";
        var previous = Environment.GetEnvironmentVariable(setting);

        try
        {
            Environment.SetEnvironmentVariable(setting, "false");
            var admitted = Admit(Discover());
            Assert.DoesNotContain(admitted, module => module.Code == AssistantCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(setting, previous);
        }
    }

    [Fact]
    public void T7_Los_demas_modulos_conservan_catalogo_orden_dependencias_y_montaje()
    {
        var discovered = Discover();
        var admitted = Admit(discovered);
        var expected = discovered
            .Where(module => module.Code is not AssistantCode and not PaymentsCode)
            .ToArray();

        Assert.Contains(discovered, module => module.Code == AssistantCode);
        Assert.DoesNotContain(discovered, module => module.Code == PaymentsCode);
        Assert.DoesNotContain(admitted, module => module.Code == AssistantCode);
        Assert.DoesNotContain(admitted, module => module.Code == PaymentsCode);

        Assert.Equal(expected.Select(module => module.Code), admitted.Select(module => module.Code));

        foreach (var pair in expected.Zip(admitted))
        {
            Assert.Equal(pair.First.DisplayName, pair.Second.DisplayName);
            Assert.Equal(pair.First.Version, pair.Second.Version);
            Assert.Equal(pair.First.DisplayOrder, pair.Second.DisplayOrder);
            Assert.Equal(pair.First.HardDependencies, pair.Second.HardDependencies);
            Assert.Equal(pair.First.SoftDependencies, pair.Second.SoftDependencies);
        }

        var backend = BackendDirectory();
        var program = File.ReadAllText(Path.Combine(backend.FullName, "Sillar.Api", "Program.cs"));
        Assert.Contains("foreach (var module in boot.Active)", program);
        Assert.Contains("module.MapEndpoints(app);", program);
    }

    [Fact]
    public void T8_La_guarda_precede_migraciones_y_sincronizacion_y_el_sabotaje_es_rojo()
    {
        var path = Path.Combine(
            BackendDirectory().FullName, "Sillar.Api", "Modularity", "ModuleBootstrapper.cs");
        var source = File.ReadAllText(path);

        Assert.True(GuardPrecedesWrites(source));

        const string guard = "await QuarantinedModules.EnsureSafeAsync(database, cancellationToken);";
        const string sync = "var sync = await synchronizer.SynchronizeAsync(admittedModules, cancellationToken);";
        var sabotaged = source.Replace(guard, string.Empty, StringComparison.Ordinal)
            .Replace(sync, $"{sync}{Environment.NewLine}        {guard}", StringComparison.Ordinal);

        Assert.False(GuardPrecedesWrites(sabotaged));
    }

    [Fact]
    public async Task T9_Una_lectura_fallida_aborta_en_vez_de_suponer_ausencia()
    {
        var observationType = ApiAssembly().GetType(
            "Sillar.Api.Modularity.AssistantQuarantineObservation", throwOnError: true)!;
        var taskType = typeof(Task<>).MakeGenericType(observationType);
        var delegateType = typeof(Func<,>).MakeGenericType(typeof(CancellationToken), taskType);
        var token = Expression.Parameter(typeof(CancellationToken), "token");
        var failure = Expression.Throw(
            Expression.New(
                typeof(InvalidOperationException).GetConstructor([typeof(string)])!,
                Expression.Constant("lectura simulada fallida")),
            taskType);
        var reader = Expression.Lambda(delegateType, failure, token).Compile();

        var method = QuarantineType().GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == "EnsureSafeAsync"
                && candidate.GetParameters()[0].ParameterType.IsGenericType);
        var pending = Assert.IsAssignableFrom<Task>(
            method.Invoke(null, [reader, CancellationToken.None]));

        var error = Assert.IsAssignableFrom<Exception>(
            await Record.ExceptionAsync(async () => await pending));
        Assert.Equal("Sillar.Api.Modularity.StartupAbortedException", error.GetType().FullName);
        Assert.Contains("No se pudo acreditar", error.Message);
        Assert.Contains("lectura simulada fallida", error.Message);
    }

    private static bool GuardPrecedesWrites(string source)
    {
        var guard = source.IndexOf(
            "await QuarantinedModules.EnsureSafeAsync(database, cancellationToken);",
            StringComparison.Ordinal);
        var migrations = source.IndexOf(
            "await ApplyMigrationsIfAllowedAsync(builder, database, logger, cancellationToken);",
            StringComparison.Ordinal);
        var synchronization = source.IndexOf(
            "var sync = await synchronizer.SynchronizeAsync(admittedModules, cancellationToken);",
            StringComparison.Ordinal);

        return guard >= 0 && guard < migrations && guard < synchronization;
    }

    private static Exception AssertBlocked(object observation)
    {
        var method = QuarantineType().GetMethod(
            "EnsureObservationSafe", BindingFlags.Static | BindingFlags.NonPublic)!;
        var wrapper = Assert.Throws<TargetInvocationException>(() =>
            method.Invoke(null, [observation]));
        var error = Assert.IsAssignableFrom<Exception>(wrapper.InnerException);
        Assert.Equal("Sillar.Api.Modularity.StartupAbortedException", error.GetType().FullName);
        return error;
    }

    private static object Observation(
        bool hasModule = false,
        bool hasActivation = false,
        bool isActive = false,
        bool hasMedia = false,
        bool hasSchema = false)
    {
        var type = ApiAssembly().GetType(
            "Sillar.Api.Modularity.AssistantQuarantineObservation", throwOnError: true)!;
        return Activator.CreateInstance(type,
        [
            false,
            true,
            hasModule,
            hasActivation,
            isActive,
            hasMedia,
            hasSchema,
            null
        ])!;
    }

    private static IReadOnlyList<IModule> Discover()
        => QuarantineModuleDiscoveryFixture.Discover();

    private static IReadOnlyList<IModule> Admit(IReadOnlyList<IModule> discovered)
    {
        var method = QuarantineType().GetMethod(
            "Admit", BindingFlags.Static | BindingFlags.NonPublic)!;
        return Assert.IsAssignableFrom<IReadOnlyList<IModule>>(
            method.Invoke(null, [discovered]));
    }

    private static Type QuarantineType() => ApiAssembly().GetType(
        "Sillar.Api.Modularity.QuarantinedModules", throwOnError: true)!;

    private static Assembly ApiAssembly() => Assembly.LoadFrom(RutaDelBinario());

    private static DirectoryInfo BackendDirectory()
    {
        var deployment = new FileInfo(RutaDelBinario()).Directory!;
        return deployment.Parent!.Parent!.Parent!.Parent!;
    }
}
