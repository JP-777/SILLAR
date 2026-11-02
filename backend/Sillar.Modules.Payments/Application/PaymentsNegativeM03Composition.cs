using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Payments.Application.Contracts;

namespace Sillar.Modules.Payments.Application;

/// <summary>Un descriptor esperado de una fuente de registros, con su papel en la composición.</summary>
/// <param name="Service">Nombre completo del tipo de servicio.</param>
/// <param name="Implementation">Nombre completo de la implementación, <c>instancia:</c> + tipo, o <c>factoría</c>.</param>
/// <param name="Lifetime">Ciclo de vida.</param>
/// <param name="Admitted">Si pasa a la composición.</param>
/// <param name="Role">Qué es y, si no se admite, por qué.</param>
/// <param name="Writes">Si puede escribir: <c>no</c>, o dónde escribiría.</param>
public sealed record NegativeCompositionEntry(
    string Service, string Implementation, ServiceLifetime Lifetime, bool Admitted, string Role, string Writes);

/// <summary>
/// Admisión cerrada de registros. Una fuente se registra en una colección aparte, su
/// inventario tiene que coincidir exactamente con el esperado —ni un descriptor de más,
/// ni uno de menos, ni uno distinto— y solo los admitidos pasan al destino.
/// </summary>
public static class NegativeCompositionInventory
{
    /// <summary>Nombre estable de un tipo, también genérico.</summary>
    public static string NameOf(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var definition = type.GetGenericTypeDefinition().FullName!;
        return $"{definition[..definition.IndexOf('`', StringComparison.Ordinal)]}<{string.Join(",", type.GetGenericArguments().Select(NameOf))}>";
    }

    /// <summary>Cómo se registra la implementación de un descriptor.</summary>
    public static string ImplementationOf(ServiceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.IsKeyedService)
        {
            return "clave:" + descriptor.ServiceKey;
        }

        return descriptor.ImplementationType is { } type ? NameOf(type)
            : descriptor.ImplementationInstance is { } instance ? "instancia:" + NameOf(instance.GetType())
            : "factoría";
    }

    /// <summary>
    /// Registra la fuente aparte, exige su inventario exacto y copia los admitidos al destino.
    /// Lanza, sin tocar el destino, ante cualquier diferencia.
    /// </summary>
    public static void Admit(
        IServiceCollection target,
        string source,
        Action<IServiceCollection> register,
        IReadOnlyList<NegativeCompositionEntry> expected)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(register);
        ArgumentNullException.ThrowIfNull(expected);

        var staging = new ServiceCollection();
        register(staging);

        var pending = expected.ToList();
        var unexpected = new List<string>();
        var admitted = new List<ServiceDescriptor>();
        foreach (var descriptor in staging)
        {
            var key = (NameOf(descriptor.ServiceType), ImplementationOf(descriptor), descriptor.Lifetime);
            var index = pending.FindIndex(e => (e.Service, e.Implementation, e.Lifetime) == key);
            if (index < 0)
            {
                unexpected.Add($"{key.Item1} | {key.Item2} | {key.Lifetime}");
                continue;
            }

            if (pending[index].Admitted)
            {
                admitted.Add(descriptor);
            }

            pending.RemoveAt(index);
        }

        if (unexpected.Count > 0 || pending.Count > 0)
        {
            throw new InvalidOperationException(
                $"La composición negativa rechaza el inventario de {source}. "
                + $"Inesperados: [{string.Join("; ", unexpected)}]. "
                + $"Ausentes: [{string.Join("; ", pending.Select(e => $"{e.Service} | {e.Implementation} | {e.Lifetime}"))}].");
        }

        foreach (var descriptor in admitted)
        {
            target.Add(descriptor);
        }
    }
}

/// <summary>
/// Composición negativa de M11: <see cref="PaymentsModule"/> admitido por inventario y los dos
/// puertos negativos hacia M03. <see cref="PaymentsModule"/> no la llama: solo una composición
/// de Host aislada, con su propia barrera, decide invocarla.
/// </summary>
/// <remarks>
/// No registra fuentes de declaración autenticada, verificadores de proveedor ni fuentes de
/// correlación autorizada: no hay productor real de ninguno, y fabricarlos sería crear una
/// autoridad. La correlación del inbox sigue siendo la barrera de FASE A.
/// </remarks>
public static class PaymentsNegativeM03Composition
{
    private const string Ns = "Sillar.Modules.Payments.Application";
    private const string Persistence = "Sillar.Modules.Payments.Infrastructure.Persistence";
    private const string Ef = "Microsoft.EntityFrameworkCore";
    private const string Inbox = "payments.* (estado del inbox M11; no comercial)";

    /// <summary>El inventario exacto de <see cref="PaymentsModule.RegisterServices"/> y qué se admite.</summary>
    public static IReadOnlyList<NegativeCompositionEntry> PaymentsModuleInventory { get; } =
    [
        new($"{Ef}.Infrastructure.ServiceProviderAccessor", $"{Ef}.Infrastructure.ServiceProviderAccessor", ServiceLifetime.Singleton,
            false, "infraestructura EF duplicada: ya la aporta M03", "no"),
        new($"{Ef}.Infrastructure.IDbContextOptionsConfiguration<{Persistence}.PaymentsDbContext>", "factoría", ServiceLifetime.Scoped,
            true, "infraestructura EF de PaymentsDbContext", "no"),
        new($"{Ef}.DbContextOptions<{Persistence}.PaymentsDbContext>", "factoría", ServiceLifetime.Scoped,
            true, "infraestructura EF de PaymentsDbContext", "no"),
        new($"{Ef}.DbContextOptions", "factoría", ServiceLifetime.Scoped,
            false, "opciones EF no genéricas: M03 y M11 registran una cada uno y gana la última; ningún contexto las necesita", "no"),
        new($"{Persistence}.PaymentsDbContext", $"{Persistence}.PaymentsDbContext", ServiceLifetime.Scoped,
            true, "contexto M11", Inbox),
        new($"{Ns}.Contracts.IProviderSettlementAnalysisService", $"{Ns}.ProviderSettlementAnalysisService", ServiceLifetime.Scoped,
            false, "análisis de liquidaciones: fuera de los flujos negativos", "no"),
        new($"{Ns}.ProviderReceptionOrchestrator", $"{Ns}.ProviderReceptionOrchestrator", ServiceLifetime.Scoped,
            false, "recepción: exige IProviderEvidenceVerifier, sin implementación; autenticar al proveedor no se simula", Inbox),
        new($"{Ns}.ProviderCommercialRecoveryService", $"{Ns}.ProviderCommercialRecoveryService", ServiceLifetime.Scoped,
            false, "recuperación con reintento comercial: fuera de los flujos negativos", Inbox),
        new($"{Ns}.Contracts.IProviderCommercialRetryPolicy", $"{Ns}.Contracts.HoldIndeterminateCommercialRetryPolicy", ServiceLifetime.Singleton,
            true, "política Hold: sin reintento automático", "no"),
        new($"{Ns}.ProviderCommercialCandidateSource", $"{Ns}.ProviderCommercialCandidateSource", ServiceLifetime.Scoped,
            true, "preparación del candidato (lee hechos, referencias y correlación)", "no"),
        new($"{Ns}.Contracts.IProviderCommercialCandidateSource", "factoría", ServiceLifetime.Scoped,
            true, "reenvía a ProviderCommercialCandidateSource", "no"),
        new($"{Ns}.Contracts.IProviderAuthorizedOrderCorrelationSource", $"{Ns}.UnavailableProviderAuthorizedOrderCorrelationSource", ServiceLifetime.Scoped,
            true, "barrera de FASE A: nunca correlaciona", "no"),
        new($"{Ns}.ProviderInboxProcessor", $"{Ns}.ProviderInboxProcessor", ServiceLifetime.Scoped,
            true, "procesador del inbox", Inbox),
        new($"{Persistence}.EfProviderInboxRepository", $"{Persistence}.EfProviderInboxRepository", ServiceLifetime.Scoped,
            true, "repositorio del inbox M11", Inbox),
        new($"{Ns}.Contracts.IProviderReceptionLedger", "factoría", ServiceLifetime.Scoped,
            false, "registro de recepciones: solo lo usa la recepción, que no se admite", Inbox),
        new($"{Ns}.Contracts.IProviderInboxRepository", "factoría", ServiceLifetime.Scoped,
            true, "reenvía a EfProviderInboxRepository", Inbox),
        new($"{Ns}.Contracts.IProviderSettlementFactRepository", "factoría", ServiceLifetime.Scoped,
            false, "escritor de hechos de liquidación: ningún flujo negativo lo usa", Inbox),
        new($"{Ns}.Contracts.IProviderSettlementFactsSource", "factoría", ServiceLifetime.Scoped,
            true, "lectura de hechos ya persistidos", "no"),
        new($"{Ns}.Contracts.IProviderVerifiedEvidenceReferenceSource", "factoría", ServiceLifetime.Scoped,
            true, "lectura de referencias ya persistidas", "no"),
        new($"{Ns}.Contracts.IProviderReceptionService", "factoría", ServiceLifetime.Scoped,
            false, "recepción: exige IProviderEvidenceVerifier, sin implementación", Inbox),
        new($"{Ns}.Contracts.IProviderInboxProcessingService", "factoría", ServiceLifetime.Scoped,
            true, "reenvía a ProviderInboxProcessor", Inbox),
    ];

    /// <summary>Los dos puertos negativos que añade esta composición.</summary>
    public static IReadOnlyList<NegativeCompositionEntry> NegativeBridgeInventory { get; } =
    [
        new($"{Ns}.Contracts.IM03CommercialReadOnlyEvaluationPort", $"{Ns}.M03CommercialReadOnlyEvaluationPortAdapter", ServiceLifetime.Scoped,
            true, "consumidor negativo de la evaluación M03", "no"),
        new($"{Ns}.Contracts.IProviderCommercialEffectPort", $"{Ns}.M03ReadOnlyNegativeEffectPortAdapter", ServiceLifetime.Scoped,
            true, "puerto de efectos negativo R4: solo Rejected, DecisionRequired, Conflict o Indeterminate", "no"),
    ];

    /// <summary>Puertos cuya implementación no puede cambiar: uno, exacto y con su ciclo de vida.</summary>
    private static readonly (Type Service, Type Implementation, ServiceLifetime Lifetime)[] Guarded =
    [
        (typeof(IProviderCommercialEffectPort), typeof(M03ReadOnlyNegativeEffectPortAdapter), ServiceLifetime.Scoped),
        (typeof(IM03CommercialReadOnlyEvaluationPort), typeof(M03CommercialReadOnlyEvaluationPortAdapter), ServiceLifetime.Scoped),
        (typeof(IProviderCommercialRetryPolicy), typeof(HoldIndeterminateCommercialRetryPolicy), ServiceLifetime.Singleton),
        (typeof(IProviderAuthorizedOrderCorrelationSource), typeof(UnavailableProviderAuthorizedOrderCorrelationSource), ServiceLifetime.Scoped),
        (typeof(ProviderInboxProcessor), typeof(ProviderInboxProcessor), ServiceLifetime.Scoped),
        (typeof(ProviderCommercialCandidateSource), typeof(ProviderCommercialCandidateSource), ServiceLifetime.Scoped),
    ];

    /// <summary>Servicios de M11 admitidos que pueden sustituirse en pruebas: solo infraestructura.</summary>
    private static readonly Type[] Substitutable =
    [
        typeof(Infrastructure.Persistence.PaymentsDbContext),
        typeof(Infrastructure.Persistence.EfProviderInboxRepository),
        typeof(IProviderInboxRepository),
        typeof(IProviderSettlementFactsSource),
        typeof(IProviderVerifiedEvidenceReferenceSource),
    ];

    /// <summary>Registra M11 admitido por inventario y el puente negativo, y verifica el resultado.</summary>
    public static IServiceCollection AddPaymentsNegativeComposition(this IServiceCollection services, IConfiguration configuration)
        => AddPaymentsNegativeComposition(services, configuration, (s, c) => new PaymentsModule().RegisterServices(s, c));

    /// <summary>Igual, con la fuente de registros de M11 inyectable para las pruebas de sabotaje.</summary>
    internal static IServiceCollection AddPaymentsNegativeComposition(
        this IServiceCollection services, IConfiguration configuration, Action<IServiceCollection, IConfiguration> registerPayments)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(registerPayments);

        if (services.FirstOrDefault(BelongsToPayments) is { } previous)
        {
            throw new InvalidOperationException(
                $"La composición negativa de M11 exige un destino sin registros previos de M11: {Describe(previous)}.");
        }

        NegativeCompositionInventory.Admit(services, "PaymentsModule", s => registerPayments(s, configuration), PaymentsModuleInventory);
        services.Add(ServiceDescriptor.Scoped<IM03CommercialReadOnlyEvaluationPort, M03CommercialReadOnlyEvaluationPortAdapter>());
        services.Add(ServiceDescriptor.Scoped<IProviderCommercialEffectPort, M03ReadOnlyNegativeEffectPortAdapter>());
        EnsureNegativePaymentsIntact(services);
        return services;
    }

    /// <summary>
    /// Verifica los registros de M11: cada puerto guardado una sola vez y con su implementación
    /// exacta; ningún servicio de M11 que no esté admitido; ninguna implementación de M11 bajo
    /// un servicio no previsto; ningún servicio de M11 registrado dos veces. Lanza con todas
    /// las violaciones.
    /// </summary>
    public static void EnsureNegativePaymentsIntact(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var payments = typeof(PaymentsModule).Assembly;
        var violations = new List<string>();

        foreach (var (service, implementation, lifetime) in Guarded)
        {
            var found = services.Where(d => d.ServiceType == service).ToArray();
            if (found is not [var only]
                || only.IsKeyedService
                || only.ImplementationType != implementation
                || only.Lifetime != lifetime)
            {
                violations.Add($"{service.Name}: se esperaba un único {implementation.Name} {lifetime}; hay "
                    + $"[{string.Join("; ", found.Select(Describe))}]");
            }
        }

        var admitted = PaymentsModuleInventory.Concat(NegativeBridgeInventory).Where(e => e.Admitted)
            .Select(e => e.Service).ToHashSet(StringComparer.Ordinal);
        var guarded = Guarded.Select(g => g.Service).ToHashSet();
        foreach (var d in services.Where(BelongsToPayments))
        {
            var name = NegativeCompositionInventory.NameOf(d.ServiceType);
            var implementation = d.ImplementationType ?? d.ImplementationInstance?.GetType();
            if (d.ServiceType.Assembly == payments && !admitted.Contains(name))
            {
                violations.Add($"servicio de M11 no admitido: {Describe(d)}");
            }
            else if (implementation?.Assembly == payments
                && d.ServiceType.Assembly != payments
                && !d.ServiceType.Namespace!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
            {
                violations.Add($"implementación de M11 bajo un servicio ajeno: {Describe(d)}");
            }
            else if (implementation?.Assembly == payments
                && !guarded.Contains(d.ServiceType)
                && !Substitutable.Contains(d.ServiceType)
                && d.ServiceType != implementation)
            {
                violations.Add($"implementación de M11 no prevista para {d.ServiceType.Name}: {Describe(d)}");
            }
        }

        foreach (var group in services.Where(d => d.ServiceType.Assembly == payments)
                     .GroupBy(d => d.ServiceType).Where(g => g.Count() > 1))
        {
            violations.Add($"registro múltiple de {group.Key.Name}: [{string.Join("; ", group.Select(Describe))}]");
        }

        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "La composición negativa de M11 no está intacta: " + string.Join(" | ", violations));
        }
    }

    /// <summary>Verifica, ya construido el contenedor, lo que resuelven los puertos guardados.</summary>
    public static void EnsureNegativePaymentsResolves(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var ports = sp.GetServices<IProviderCommercialEffectPort>().ToArray();
        if (ports is not [M03ReadOnlyNegativeEffectPortAdapter])
        {
            throw new InvalidOperationException(
                "IProviderCommercialEffectPort debe resolver solo al adaptador negativo; resuelve "
                + $"[{string.Join(", ", ports.Select(p => p.GetType().Name))}].");
        }

        if (sp.GetRequiredService<IM03CommercialReadOnlyEvaluationPort>() is not M03CommercialReadOnlyEvaluationPortAdapter
            || sp.GetRequiredService<IProviderCommercialRetryPolicy>() is not HoldIndeterminateCommercialRetryPolicy
            || sp.GetRequiredService<IProviderAuthorizedOrderCorrelationSource>() is not UnavailableProviderAuthorizedOrderCorrelationSource
            || sp.GetRequiredService<IProviderInboxProcessingService>() is not ProviderInboxProcessor)
        {
            throw new InvalidOperationException("Un puerto guardado de M11 resuelve a una implementación no autorizada.");
        }
    }

    private static bool BelongsToPayments(ServiceDescriptor d)
        => d.ServiceType.Assembly == typeof(PaymentsModule).Assembly
            || d.ImplementationType?.Assembly == typeof(PaymentsModule).Assembly
            || d.ImplementationInstance?.GetType().Assembly == typeof(PaymentsModule).Assembly;

    private static string Describe(ServiceDescriptor d)
        => $"{d.ServiceType.Name} → {NegativeCompositionInventory.ImplementationOf(d)} ({d.Lifetime})";
}
