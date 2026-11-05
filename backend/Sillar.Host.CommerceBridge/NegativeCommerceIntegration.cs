using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Sillar.Modules.Payments.Application;
using Sillar.Modules.Reports;
using Sillar.Modules.Sales;
using Sillar.Modules.Sales.Commercial;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Pedidos;

namespace Sillar.Host.CommerceBridge;

/// <summary>
/// Composición vertical negativa de M03, M10 y M11 con sus servicios reales, cerrada por
/// inventario: cada fuente se registra aparte y solo pasa lo que figura en la lista positiva.
/// </summary>
/// <remarks>
/// <para>
/// <b>Barrera.</b> <c>Sillar.Api</c> no referencia este proyecto; el método falla en
/// <c>Production</c> y sin <see cref="OptInKey"/> = <see cref="OptInValue"/>. Las
/// cuarentenas de <c>QuarantinedModules</c> no se tocan.
/// </para>
/// <para>
/// <b>Fail-closed.</b> Si <c>SalesModule</c>, un opt-in de M03, <c>ReportsModule</c> o
/// <c>PaymentsModule</c> registran un descriptor que no figura en su inventario, o dejan de
/// registrar uno que figura, la composición lanza antes de admitir nada de esa fuente.
/// </para>
/// <para>
/// <b>Alcance.</b> El contenedor no es de solo lectura: admite el repositorio del inbox M11 y
/// los contextos EF, que pueden escribir. <see cref="Inventory"/> dice, descriptor a
/// descriptor, qué se admite, para qué y si escribe. Ningún escritor comercial de M03 se admite.
/// </para>
/// </remarks>
public static class NegativeCommerceIntegration
{
    /// <summary>Clave de configuración que habilita la composición.</summary>
    public const string OptInKey = "Sillar:NegativeCommerceIntegration";

    /// <summary>Único valor aceptado.</summary>
    public const string OptInValue = "isolated-non-production";

    private const string Sales = "Sillar.Modules.Sales";
    private const string Ef = "Microsoft.EntityFrameworkCore";
    private const string Escritor = "escritor comercial de M03: no se admite";

    /// <summary>
    /// Inventario exacto de M03: <c>SalesModule.RegisterServices</c>, más
    /// <c>AddCandidateSalesReadOnlyEvaluation</c> y <c>AddCandidateSalesMonetaryReaders</c>.
    /// </summary>
    public static IReadOnlyList<NegativeCompositionEntry> M03Inventory { get; } =
    [
        new("Sillar.Shared.Replication.NodeIdentity", "instancia:Sillar.Shared.Replication.NodeIdentity", ServiceLifetime.Singleton,
            true, "identidad de nodo que exige SalesDbContext", "no"),
        new($"{Ef}.Infrastructure.ServiceProviderAccessor", $"{Ef}.Infrastructure.ServiceProviderAccessor", ServiceLifetime.Singleton,
            true, "infraestructura EF (una sola vez para M03 y M11)", "no"),
        new($"{Ef}.Infrastructure.IDbContextOptionsConfiguration<{Sales}.Data.SalesDbContext>", "factoría", ServiceLifetime.Scoped,
            true, "infraestructura EF de SalesDbContext", "no"),
        new($"{Ef}.DbContextOptions<{Sales}.Data.SalesDbContext>", "factoría", ServiceLifetime.Scoped,
            true, "infraestructura EF de SalesDbContext", "no"),
        new($"{Ef}.DbContextOptions", "factoría", ServiceLifetime.Scoped,
            false, "opciones EF no genéricas: M03 y M11 registran una cada uno y gana la última; ningún contexto las necesita", "no"),
        new($"{Sales}.Data.SalesDbContext", $"{Sales}.Data.SalesDbContext", ServiceLifetime.Scoped,
            true, "contexto M03, usado solo por los lectores y el evaluador admitidos", "técnicamente sales.*; ningún servicio admitido escribe"),
        new($"{Sales}.Data.OrderCodeAllocator", $"{Sales}.Data.OrderCodeAllocator", ServiceLifetime.Scoped, false, Escritor, "sales.*"),
        new($"{Sales}.Pedidos.CongeladorDeCliente", $"{Sales}.Pedidos.CongeladorDeCliente", ServiceLifetime.Scoped, false, Escritor, "sales.*"),
        new($"{Sales}.Pedidos.VencimientoDePlazos", $"{Sales}.Pedidos.VencimientoDePlazos", ServiceLifetime.Scoped, false, Escritor, "sales.*"),
        new($"{Sales}.Pedidos.CreadorDePedidos", $"{Sales}.Pedidos.CreadorDePedidos", ServiceLifetime.Scoped, false, Escritor, "sales.*"),
        new($"{Sales}.Pedidos.IAutoridadDeMoneda", $"{Sales}.Pedidos.AutoridadDeMonedaDeCore", ServiceLifetime.Scoped,
            false, "autoridad monetaria legítima de CORE: solo la usa el creador de pedidos y no cruza CommerceBridge", "no"),
        new("Sillar.Core.Contracts.IMonetaryRecordsProbe", $"{Sales}.Pedidos.SondaDeRegistrosMonetarios", ServiceLifetime.Scoped,
            false, "sonda de pedidos que CORE consulta al administrar la moneda; no pertenece a CommerceBridge", "no"),
        new($"{Sales}.Contracts.ICustomerOrderHistory", $"{Sales}.Pedidos.HistorialDePedidos", ServiceLifetime.Scoped,
            false, "historial para M04: fuera de los flujos negativos", "no"),
        new($"{Sales}.Contracts.IOrderAggregateReader", $"{Sales}.Pedidos.LectorAgregadoDePedidos", ServiceLifetime.Scoped,
            true, "lector I01/I03/I04 para M10", "no"),
        new($"{Sales}.Contracts.ICommercialReadOnlyEvaluationCandidate", "factoría", ServiceLifetime.Scoped,
            true, "evaluación sin efectos (construye su propio evaluador sobre SalesDbContext)", "no"),
        new($"{Sales}.Contracts.IOrderValueAggregateReader", $"{Sales}.Pedidos.LectorDeValorDePedidos", ServiceLifetime.Scoped,
            false, "I02: sin autoridad monetaria ratificada el valor no se publica", "no"),
        new($"{Sales}.Contracts.IOrderPaymentAggregateReader", $"{Sales}.Pedidos.LectorAgregadoDePagos", ServiceLifetime.Scoped,
            true, "lector I05–I07 (no medible mientras no haya escritor acreditado)", "no"),
    ];

    /// <summary>Inventario exacto de <c>ReportsModule.RegisterServices</c>.</summary>
    public static IReadOnlyList<NegativeCompositionEntry> M10Inventory { get; } =
    [
        new("Sillar.Modules.Reports.Orders.IOrderIndicatorsReport", "Sillar.Modules.Reports.Orders.OrderIndicatorsReport", ServiceLifetime.Scoped,
            true, "indicadores I01/I03/I04", "no"),
    ];

    /// <summary>Lo que la composición añade por su cuenta.</summary>
    public static IReadOnlyList<NegativeCompositionEntry> HostInventory { get; } =
    [
        new("System.TimeProvider", "instancia:" + NegativeCompositionInventory.NameOf(TimeProvider.System.GetType()), ServiceLifetime.Singleton,
            true, "reloj de la plataforma, como CoreServices.TryAddTimeProvider; lo exigen los lectores", "no"),
    ];

    /// <summary>Todo el inventario, descriptor a descriptor.</summary>
    public static IReadOnlyList<(string Source, NegativeCompositionEntry Entry)> Inventory { get; } =
    [
        .. M03Inventory.Select(e => ("M03", e)),
        .. M10Inventory.Select(e => ("M10", e)),
        .. PaymentsNegativeM03Composition.PaymentsModuleInventory.Select(e => ("M11", e)),
        .. PaymentsNegativeM03Composition.NegativeBridgeInventory.Select(e => ("M11 puente", e)),
        .. HostInventory.Select(e => ("Host", e)),
    ];

    /// <summary>Servicios de M03 y M10 que, admitidos, no pueden cambiar de implementación.</summary>
    private static readonly (string Service, string Implementation)[] Guarded =
    [
        ($"{Sales}.Contracts.IOrderAggregateReader", $"{Sales}.Pedidos.LectorAgregadoDePedidos"),
        ($"{Sales}.Contracts.IOrderPaymentAggregateReader", $"{Sales}.Pedidos.LectorAgregadoDePagos"),
        ($"{Sales}.Contracts.ICommercialReadOnlyEvaluationCandidate", "factoría"),
        ("Sillar.Modules.Reports.Orders.IOrderIndicatorsReport", "Sillar.Modules.Reports.Orders.OrderIndicatorsReport"),
    ];

    /// <summary>Servicios que no pueden aparecer: aplicación comercial, autoridad o I02.</summary>
    private static readonly string[] Forbidden =
    [
        $"{Sales}.Contracts.IExternalSettlementCandidate",
        $"{Sales}.Commercial.ISettlementCandidateJournal",
        $"{Sales}.Contracts.IPayableOrderCandidate",
        $"{Sales}.Contracts.IOrderValueAggregateReader",
        $"{Sales}.Pedidos.IAutoridadDeMoneda",
    ];

    /// <summary>Servicios de plataforma que no se duplican si el destino ya los registra.</summary>
    private static readonly string[] SharedPlatform =
    [
        "System.TimeProvider",
        "Sillar.Shared.Replication.NodeIdentity",
        $"{Ef}.Infrastructure.ServiceProviderAccessor",
    ];

    /// <summary>Servicios de M03 admitidos que pueden sustituirse en pruebas: solo infraestructura.</summary>
    private static readonly string[] Substitutable = [$"{Sales}.Data.SalesDbContext"];

    /// <summary>
    /// Registra la composición. Falla en Production, sin el opt-in exacto, con registros previos
    /// de M03, M10 o M11 en el destino, o si alguna fuente no coincide con su inventario.
    /// </summary>
    public static IServiceCollection AddNegativeCommerceIntegration(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
        => AddNegativeCommerceIntegration(services, configuration, environment, null, null);

    /// <summary>Igual, con las fuentes de M03 y M10 inyectables para las pruebas de sabotaje.</summary>
    internal static IServiceCollection AddNegativeCommerceIntegration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        Action<IServiceCollection, IConfiguration>? registerM03,
        Action<IServiceCollection, IConfiguration>? registerM10)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsProduction())
        {
            throw new InvalidOperationException("La composición negativa M03–M10–M11 no se monta en Production.");
        }

        if (!string.Equals(configuration[OptInKey], OptInValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"La composición negativa M03–M10–M11 exige {OptInKey} = {OptInValue}.");
        }

        if (services.FirstOrDefault(BelongsToModules) is { } previous)
        {
            throw new InvalidOperationException(
                $"La composición negativa exige un destino sin registros previos de M03, M10 ni M11: {Describe(previous)}.");
        }

        // Se compone en una colección aparte y solo se vuelca al destino si todo pasa.
        var composed = new ServiceCollection();
        NegativeCompositionInventory.Admit(composed, "M03", s => (registerM03 ?? RegisterM03)(s, configuration), M03Inventory);
        NegativeCompositionInventory.Admit(composed, "M10", s => (registerM10 ?? RegisterM10)(s, configuration), M10Inventory);
        composed.TryAddSingleton(TimeProvider.System);
        composed.AddPaymentsNegativeComposition(configuration);
        EnsureIntact(composed);

        foreach (var descriptor in composed)
        {
            // Servicios de plataforma que el Host ya puede traer (CORE registra reloj, nodo e
            // infraestructura EF): no se duplican. Ninguno es de M03, M10 ni M11.
            if (SharedPlatform.Contains(NegativeCompositionInventory.NameOf(descriptor.ServiceType), StringComparer.Ordinal)
                && services.Any(d => d.ServiceType == descriptor.ServiceType))
            {
                continue;
            }

            services.Add(descriptor);
        }

        return services;
    }

    /// <summary>
    /// Verifica una colección ya compuesta, también después de sustituir infraestructura en
    /// pruebas: puertos guardados únicos y exactos, nada prohibido, ningún servicio de M03 o
    /// M10 fuera del inventario admitido, y la verificación propia de M11.
    /// </summary>
    public static void EnsureIntact(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var violations = new List<string>();

        foreach (var (service, implementation) in Guarded)
        {
            var found = services.Where(d => NegativeCompositionInventory.NameOf(d.ServiceType) == service).ToArray();
            if (found is not [var only] || only.IsKeyedService || NegativeCompositionInventory.ImplementationOf(only) != implementation)
            {
                violations.Add($"{service}: se esperaba un único {implementation}; hay [{string.Join("; ", found.Select(Describe))}]");
            }
        }

        foreach (var d in services)
        {
            var name = NegativeCompositionInventory.NameOf(d.ServiceType);
            if (Forbidden.Contains(name, StringComparer.Ordinal))
            {
                violations.Add($"servicio prohibido: {Describe(d)}");
            }
        }

        var admitted = M03Inventory.Concat(M10Inventory).Where(e => e.Admitted).Select(e => e.Service).ToHashSet(StringComparer.Ordinal);
        foreach (var d in services.Where(d => IsSalesOrReports(d.ServiceType)
                     || (d.ImplementationType ?? d.ImplementationInstance?.GetType()) is { } t && IsSalesOrReports(t)))
        {
            var name = NegativeCompositionInventory.NameOf(d.ServiceType);
            var implementation = NegativeCompositionInventory.ImplementationOf(d);
            var expected = M03Inventory.Concat(M10Inventory).Where(e => e.Admitted && e.Service == name).ToArray();
            if (!IsSalesOrReports(d.ServiceType) && !name.StartsWith(Ef, StringComparison.Ordinal))
            {
                violations.Add($"implementación de M03/M10 bajo un servicio ajeno: {Describe(d)}");
            }
            else if (IsSalesOrReports(d.ServiceType) && !admitted.Contains(name))
            {
                violations.Add($"servicio de M03/M10 no admitido: {Describe(d)}");
            }
            else if (expected.Length > 0 && !Substitutable.Contains(name, StringComparer.Ordinal)
                && expected.All(e => e.Implementation != implementation))
            {
                violations.Add($"implementación no admitida para {name}: {Describe(d)}");
            }
        }

        foreach (var group in services.Where(d => IsSalesOrReports(d.ServiceType)).GroupBy(d => d.ServiceType).Where(g => g.Count() > 1))
        {
            violations.Add($"registro múltiple de {group.Key.Name}: [{string.Join("; ", group.Select(Describe))}]");
        }

        if (violations.Count > 0)
        {
            throw new InvalidOperationException("La composición negativa M03–M10 no está intacta: " + string.Join(" | ", violations));
        }

        PaymentsNegativeM03Composition.EnsureNegativePaymentsIntact(services);
    }

    /// <summary>
    /// Construye el contenedor solo si la colección sigue intacta, valida al construir y
    /// comprueba lo que resuelven los puertos guardados.
    /// </summary>
    public static ServiceProvider BuildNegativeServiceProvider(this IServiceCollection services)
    {
        EnsureIntact(services);
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        try
        {
            EnsureResolves(provider);
            return provider;
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }

    /// <summary>Comprueba, ya construido el contenedor, lo que resuelven los puertos guardados.</summary>
    public static void EnsureResolves(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        using (var scope = provider.CreateScope())
        {
            var evaluation = scope.ServiceProvider.GetServices<ICommercialReadOnlyEvaluationCandidate>().ToArray();
            if (evaluation is not [var only] || only.GetType().FullName != $"{Sales}.Commercial.CandidateCommercialReadOnlyEvaluationAdapter")
            {
                throw new InvalidOperationException(
                    "ICommercialReadOnlyEvaluationCandidate debe resolver solo al adaptador sin efectos de M03.");
            }
        }

        PaymentsNegativeM03Composition.EnsureNegativePaymentsResolves(provider);
    }

    private static void RegisterM03(IServiceCollection services, IConfiguration configuration)
    {
        new SalesModule().RegisterServices(services, configuration);
        services.AddCandidateSalesReadOnlyEvaluation();
        services.AddCandidateSalesMonetaryReaders();
    }

    private static void RegisterM10(IServiceCollection services, IConfiguration configuration)
        => new ReportsModule().RegisterServices(services, configuration);

    private static bool IsSalesOrReports(Type type)
        => type.Assembly == typeof(SalesModule).Assembly
            || type.Assembly == typeof(IOrderAggregateReader).Assembly
            || type.Assembly == typeof(ReportsModule).Assembly;

    private static bool BelongsToModules(ServiceDescriptor d)
    {
        var implementation = d.ImplementationType ?? d.ImplementationInstance?.GetType();
        return IsSalesOrReports(d.ServiceType)
            || d.ServiceType.Assembly == typeof(PaymentsNegativeM03Composition).Assembly
            || implementation is not null && (IsSalesOrReports(implementation)
                || implementation.Assembly == typeof(PaymentsNegativeM03Composition).Assembly);
    }

    private static string Describe(ServiceDescriptor d)
        => $"{d.ServiceType.Name} → {NegativeCompositionInventory.ImplementationOf(d)} ({d.Lifetime})";
}
