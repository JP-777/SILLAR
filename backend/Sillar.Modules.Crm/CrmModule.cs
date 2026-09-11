using Sillar.Shared.Data.Modularity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Crm.Administration;
using Sillar.Modules.Crm.Authentication;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Crm.Contact;
using Sillar.Modules.Crm.Data;
using Sillar.Modules.Crm.Endpoints;
using Sillar.Modules.Crm.Profiles;
using Sillar.Shared.Modularity;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Crm;

/// <summary>M04 — Clientes y Contacto.</summary>
public sealed class CrmModule : IModule, IModuleMigrations
{
    public const string ModuleCode = "crm";
    public const string ConnectionStringName = "Default";

    public string Code => ModuleCode;

    /// <summary>
    /// Sus migraciones, para que el instalador las aplique sin conocer este
    /// módulo. El contexto se construye aquí, con la misma configuración que
    /// en tiempo de ejecución; el nodo y el reloj no intervienen al migrar.
    /// </summary>
    private static readonly MigracionesDeContexto<CrmDbContext> Migraciones = new(
        connectionString => new CrmDbContext(
            PersistenciaDeModulo.Opciones<CrmDbContext>(
                connectionString, CrmDbContext.Schema, CrmDbContext.MigrationsHistoryTable),
            new NodeIdentity(NodeIdentity.DefaultCode),
            TimeProvider.System),
        CrmDbContext.MigrationsHistoryTable);

    /// <inheritdoc />
    public string MigrationsHistoryTable => Migraciones.MigrationsHistoryTable;

    /// <inheritdoc />
    public IReadOnlyList<string> KnownMigrations(string connectionString)
        => Migraciones.KnownMigrations(connectionString);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> AppliedMigrationsAsync(string connectionString, CancellationToken cancellationToken)
        => Migraciones.AppliedMigrationsAsync(connectionString, cancellationToken);

    /// <inheritdoc />
    public Task ApplyMigrationsAsync(string connectionString, CancellationToken cancellationToken)
        => Migraciones.ApplyMigrationsAsync(connectionString, cancellationToken);
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

        // Por el mismo sitio que el instalador: ver PersistenciaDeModulo.
        services.AddDbContext<CrmDbContext>(options => PersistenciaDeModulo.Configurar(
            options, connectionString, CrmDbContext.Schema, CrmDbContext.MigrationsHistoryTable));

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
        services.AddSingleton<DeferredEmailDispatcher>();
        services.AddSingleton<CustomerPublicUrlResolver>();
        services.AddSingleton<ContactSubmissionThrottle>();
        services.AddScoped<ContactMessageService>();
        services.AddScoped<CustomerSessionService>();
        services.AddScoped<CustomerAuthenticationService>();
        services.AddScoped<CustomerRegistrationService>();
        services.AddScoped<CustomerAccountTokenService>();
        services.AddScoped<CustomerAdminService>();
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
        endpoints.MapCustomerAdminEndpoints();
        endpoints.MapContactMessageEndpoints();
    }
}
