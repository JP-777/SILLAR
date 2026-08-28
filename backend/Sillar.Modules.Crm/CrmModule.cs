using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Crm.Authentication;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Crm.Data;
using Sillar.Modules.Crm.Endpoints;
using Sillar.Modules.Crm.Profiles;
using Sillar.Shared.Modularity;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Crm;

/// <summary>M04 — Clientes y Contacto.</summary>
public sealed class CrmModule : IModule
{
    public const string ModuleCode = "crm";
    public const string ConnectionStringName = "Default";

    public string Code => ModuleCode;
    public string DisplayName => "Clientes y Contacto";

    public string Description =>
        "Clientes, cuentas de tienda, direcciones y contacto. " +
        "Es dueño de la identidad de la clientela.";

    public string Version => "1.0.0";

    // M01=10, M02=20; M04 conserva el orden del catálogo modular.
    public int DisplayOrder => 40;

    public string[] HardDependencies => ["core"];
    public string[] SoftDependencies => [];

    public void RegisterServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Falta la cadena de conexión '{ConnectionStringName}'.");

        services.TryAddNodeIdentity(configuration);

        services.AddDbContext<CrmDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(
                CrmDbContext.MigrationsHistoryTable,
                CrmDbContext.Schema)));

        // Segundo esquema. No cambia el default administrativo de CORE.
        services.AddAuthentication()
            .AddScheme<
                AuthenticationSchemeOptions,
                CustomerSessionAuthenticationHandler>(
                    CustomerSessionAuthenticationHandler.SchemeName,
                    configureOptions: null);

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                CustomerAuthorization.PolicyName,
                policy =>
                {
                    policy.AddAuthenticationSchemes(
                        CustomerSessionAuthenticationHandler.SchemeName);
                    policy.RequireAuthenticatedUser();
                });
        });

        services.AddSingleton<CustomerPasswordHasher>();
        services.AddSingleton<CustomerLoginThrottle>();
        services.AddScoped<CustomerSessionService>();
        services.AddScoped<CustomerAuthenticationService>();
        services.AddScoped<CustomerRegistrationService>();
        services.AddScoped<CustomerAccountTokenService>();
        services.AddScoped<CustomerProfileService>();
        services.AddScoped<ICustomerSnapshotReader, CustomerSnapshotReader>();
        services.AddScoped<CurrentCustomer>();
        services.AddScoped<ICurrentCustomer>(
            provider => provider.GetRequiredService<CurrentCustomer>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCustomerAuthEndpoints();
        endpoints.MapCustomerProfileEndpoints();
    }
}
