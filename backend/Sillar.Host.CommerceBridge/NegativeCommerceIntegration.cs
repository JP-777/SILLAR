using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Sillar.Modules.Payments;
using Sillar.Modules.Payments.Application;
using Sillar.Modules.Reports;
using Sillar.Modules.Sales;
using Sillar.Modules.Sales.Commercial;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Pedidos;

namespace Sillar.Host.CommerceBridge;

/// <summary>
/// Composición vertical negativa de M03, M10 y M11 con sus servicios reales.
/// </summary>
/// <remarks>
/// <para>
/// <b>Barrera.</b> Tres capas, todas necesarias. <c>Sillar.Api</c> no referencia este
/// proyecto, así que el despliegue normal no lo carga; el método falla en
/// <c>Production</c>; y exige <see cref="OptInKey"/> = <see cref="OptInValue"/>.
/// Las cuarentenas de <c>QuarantinedModules</c> no se tocan.
/// </para>
/// <para>
/// <b>Qué conecta.</b> Los registros reales de los tres módulos, los opt-in candidatos de
/// M03 (correlación, evaluación sin efectos y lectores monetarios) y el puente negativo
/// de M11. <b>Qué no conecta:</b> el aplicador comercial de M03
/// (<c>AddCandidateSalesCommerce</c>), ninguna autoridad de autenticación, correlación o
/// moneda, ninguna ruta.
/// </para>
/// </remarks>
public static class NegativeCommerceIntegration
{
    /// <summary>Clave de configuración que habilita la composición.</summary>
    public const string OptInKey = "Sillar:NegativeCommerceIntegration";

    /// <summary>Único valor aceptado.</summary>
    public const string OptInValue = "isolated-non-production";

    /// <summary>
    /// Servicios que se retiran a propósito, con su razón: no tienen productor real y la
    /// orden no permite fabricarlo. Se publican para que la exclusión sea verificable.
    /// </summary>
    public static IReadOnlyList<(string Service, string Reason)> Excluded { get; } =
    [
        (nameof(IOrderValueAggregateReader),
            "I02: sin autoridad monetaria ratificada (SalesModule registra SinAutoridadDeMoneda) el valor no se publica"),
        ("IProviderReceptionService",
            "recepción M11: exige IProviderEvidenceVerifier, que no tiene implementación; autenticar al proveedor no se simula"),
        ("ProviderReceptionOrchestrator",
            "recepción M11: exige IProviderEvidenceVerifier, que no tiene implementación; autenticar al proveedor no se simula"),
        ("OrderCodeAllocator", "escritor M03 (numeración por SQL directo); además exige ISettingsReader de CORE"),
        ("CongeladorDeCliente", "escritor M03 (congela el cliente al crear); además exige ICustomerSnapshotReader de CRM"),
        ("CreadorDePedidos", "escritor M03 (crea pedidos, líneas y estados); además exige ICustomerSnapshotReader de CRM"),
        ("VencimientoDePlazos", "escritor M03 (vence pedidos con SaveChanges)"),
    ];

    /// <summary>Los escritores de M03 que registra <c>SalesModule</c>; son internos, de ahí el nombre completo.</summary>
    private static readonly string[] EscritoresM03 =
    [
        "Sillar.Modules.Sales.Data.OrderCodeAllocator",
        "Sillar.Modules.Sales.Pedidos.CongeladorDeCliente",
        "Sillar.Modules.Sales.Pedidos.CreadorDePedidos",
        "Sillar.Modules.Sales.Pedidos.VencimientoDePlazos",
    ];

    /// <summary>
    /// Registra la composición. Falla en Production o sin el opt-in exacto, antes de
    /// registrar nada.
    /// </summary>
    public static IServiceCollection AddNegativeCommerceIntegration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                "La composición negativa M03–M10–M11 no se monta en Production.");
        }

        if (!string.Equals(configuration[OptInKey], OptInValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"La composición negativa M03–M10–M11 exige {OptInKey} = {OptInValue}.");
        }

        // M03 tal como se registra en producción, más sus opt-in candidatos sin efectos.
        new SalesModule().RegisterServices(services, configuration);
        services.AddCandidateSalesOrderCorrelation();
        services.AddCandidateSalesReadOnlyEvaluation();
        services.AddCandidateSalesMonetaryReaders();

        // M10 tal cual: lee de M03 lo que M03 publique.
        new ReportsModule().RegisterServices(services, configuration);

        // M11 tal cual, más el puente negativo hacia la evaluación de M03.
        new PaymentsModule().RegisterServices(services, configuration);
        services.AddPaymentsNegativeM03Bridge();

        // El reloj de la plataforma, que en el Host normal registra CORE
        // (CoreServices.TryAddTimeProvider). Los lectores de M03 lo exigen.
        services.TryAddSingleton(TimeProvider.System);

        // Al final, para que ningún registro posterior las reponga.
        services.RemoveAll<IOrderValueAggregateReader>();
        services.RemoveReceptionWithoutVerifier();
        RemoveExactlyOnce(services, EscritoresM03);

        return services;
    }

    /// <summary>
    /// Retira cada tipo por su nombre completo y exige que estuviera registrado una sola
    /// vez: si M03 cambia, la composición falla en vez de dejar pasar un escritor.
    /// </summary>
    private static void RemoveExactlyOnce(IServiceCollection services, IReadOnlyCollection<string> fullNames)
    {
        foreach (var name in fullNames)
        {
            var found = services.Where(d => d.ServiceType.FullName == name).ToArray();
            if (found.Length != 1)
            {
                throw new InvalidOperationException(
                    $"La composición negativa esperaba un único registro de {name} y encontró {found.Length}.");
            }

            services.Remove(found[0]);
        }
    }
}
