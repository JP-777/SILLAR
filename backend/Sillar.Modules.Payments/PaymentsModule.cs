using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Payments.Application;
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Infrastructure.Persistence;
using Sillar.Shared.Data.Modularity;
using Sillar.Shared.Modularity;

namespace Sillar.Modules.Payments;

/// <summary>M11 — Pagos en línea.</summary>
/// <remarks>
/// La estructura modular queda disponible para composición y pruebas, pero el
/// host no referencia todavía este módulo y no se publican rutas de cobro.
/// </remarks>
public sealed class PaymentsModule : IModule
{
    public const string ModuleCode = "payments";

    public const string ConnectionStringName = "Default";

    public string Code => ModuleCode;

    public string DisplayName => "Pagos en línea";

    public string Description =>
        "Recepción, evidencia, replay y revisión segura de señales de pago externas.";

    public string Version => "0.1.0";

    public int DisplayOrder => 110;

    /// <summary>CORE por plataforma y M03 como autoridad comercial.</summary>
    public string[] HardDependencies => ["core", "sales"];

    public string[] SoftDependencies => [];

    public void RegisterServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString =
            configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Falta la cadena de conexión '{ConnectionStringName}'.");

        services.AddDbContext<PaymentsDbContext>(options =>
            PersistenciaDeModulo.Configurar(
                options,
                connectionString,
                PaymentsDbContext.Schema,
                PaymentsDbContext.MigrationsHistoryTable));

        services.AddScoped<
            IProviderSettlementAnalysisService,
            ProviderSettlementAnalysisService>();
        services.AddScoped<ProviderReceptionOrchestrator>();
        services.AddScoped<ProviderCommercialRecoveryService>();
        services.AddScoped<ProviderInboxProcessor>();
        services.AddScoped<EfProviderInboxRepository>();
        services.AddScoped<IProviderReceptionLedger>(provider =>
            provider.GetRequiredService<EfProviderInboxRepository>());
        services.AddScoped<IProviderInboxRepository>(provider =>
            provider.GetRequiredService<EfProviderInboxRepository>());
        services.AddScoped<IProviderReceptionService>(provider =>
            provider.GetRequiredService<ProviderReceptionOrchestrator>());
        services.AddScoped<IProviderInboxProcessingService>(provider =>
            provider.GetRequiredService<ProviderInboxProcessor>());
    }

    /// <summary>
    /// M11 no expone endpoints hasta que la integración operativa sea autorizada.
    /// </summary>
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
        => ArgumentNullException.ThrowIfNull(endpoints);
}
