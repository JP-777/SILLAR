using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Sillar.Core;
using Sillar.Core.Authentication;
using Sillar.Core.Data;
using Sillar.Core.Endpoints;
using Sillar.Core.Modularity;
using Sillar.Modules.Reports;
using Sillar.Shared.Modularity;
using Sillar.Shared.Replication;

namespace Sillar.Host.CommerceBridge;

/// <summary>Un Host autenticado ya construido, con los módulos que CORE dio por activos.</summary>
public sealed record AuthenticatedHost(WebApplication App, IReadOnlyList<string> ActiveModules, bool ReportingMounted);

/// <summary>
/// Host de integración con la autenticación real de CORE: su almacén de sesiones en PostgreSQL,
/// su middleware (<c>UseAuthentication</c>, <c>UseAuthorization</c>), sus rutas y sus políticas.
/// Reproduce los dos modos de <c>Sillar.Api</c> (Program.cs:126-148; ModuleBootstrapper.cs).
/// </summary>
/// <remarks>
/// <para>
/// <b>Barrera.</b> <c>Sillar.Api</c> no referencia este proyecto. El Host falla en
/// <c>Production</c>, sin el opt-in de la composición y con cualquier servidor de base de datos
/// que no sea de bucle local. Escucha solo en <c>127.0.0.1</c>, en un puerto efímero.
/// </para>
/// <para>
/// <b>Módulos.</b> Solo CORE y Reporting (M10). M03 y M11 no son módulos de este Host: entran
/// por la composición negativa cerrada, y solo cuando CORE ha dado Reporting por activo. El
/// instalador de CORE solo migra módulos con migraciones, así que aplica las de CORE y ninguna
/// otra: ni Sales ni Payments.
/// </para>
/// </remarks>
public static class NegativeCommerceAuthenticatedHost
{
    /// <summary>Los módulos que declara este Host.</summary>
    public static IReadOnlyList<IModule> Declared() => [new CoreModule(), new ReportsModule()];

    /// <summary>
    /// Modo instalación: solo <c>/api/setup*</c>, como <c>Sillar.Api</c> sin instalación. Completar
    /// la instalación aplica las migraciones de CORE y crea la cuenta de super administración.
    /// </summary>
    public static WebApplication BuildSetupHost(IConfiguration configuration, string environmentName)
    {
        var connectionString = Guard(configuration, environmentName);
        var builder = NewBuilder(configuration, environmentName);
        builder.Services.AddSingleton(new DeclaredModules(Declared()));
        builder.Services.AddCoreEssentials(builder.Configuration, connectionString);

        var app = builder.Build();
        app.MapSetupEndpoints();
        return app;
    }

    /// <summary>
    /// Modo normal: lee la instalación, sincroniza los módulos declarados con CORE y monta
    /// CORE y, si CORE la da por activa, Reporting con la composición negativa.
    /// <paramref name="isolateInfrastructure"/> solo puede sustituir infraestructura comercial.
    /// </summary>
    public static async Task<AuthenticatedHost> BuildAsync(
        IConfiguration configuration,
        string environmentName,
        Action<IServiceCollection>? isolateInfrastructure,
        CancellationToken cancellationToken)
    {
        var connectionString = Guard(configuration, environmentName);
        var declared = Declared();
        var nodeCode = configuration[NodeIdentity.SettingKey] ?? NodeIdentity.DefaultCode;

        Guid installationKey;
        ModuleSyncResult sync;
        await using (var database = new CoreDbContext(CoreDataServiceExtensions.BuildOptions(connectionString), new NodeIdentity(nodeCode), TimeProvider.System))
        {
            installationKey = await database.Installations.Select(i => i.InstallationKey).SingleOrDefaultAsync(cancellationToken);
            if (installationKey == Guid.Empty)
            {
                throw new InvalidOperationException("No hay instalación: este Host arranca en modo instalación (BuildSetupHost).");
            }

            sync = await new ModuleSynchronizer(database, NullLogger<ModuleSynchronizer>.Instance).SynchronizeAsync(declared, cancellationToken);
        }

        var activeCodes = sync.Active.Select(m => m.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var reporting = activeCodes.Contains(ReportsModule.ModuleCode);

        var builder = NewBuilder(configuration, environmentName);
        builder.Services.AddSingleton(new DeclaredModules(declared));
        builder.Services.AddSingleton(new CsrfTokenFactory(installationKey));
        builder.Services.AddSingleton(new ModuleActivationSnapshot(sync.Active));
        new CoreModule().RegisterServices(builder.Services, builder.Configuration);
        if (reporting)
        {
            builder.Services.AddNegativeCommerceIntegration(builder.Configuration, builder.Environment);
            isolateInfrastructure?.Invoke(builder.Services);
            NegativeCommerceIntegration.EnsureIntact(builder.Services);
        }

        var app = builder.Build();
        if (reporting)
        {
            NegativeCommerceIntegration.EnsureResolves(app.Services);
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAlwaysAvailableStatus();
        new CoreModule().MapEndpoints(app);
        if (reporting)
        {
            new ReportsModule().MapEndpoints(app);
        }

        return new(app, [.. activeCodes.Order(StringComparer.Ordinal)], reporting);
    }

    private static WebApplicationBuilder NewBuilder(IConfiguration configuration, string environmentName)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName,
            ApplicationName = typeof(NegativeCommerceAuthenticatedHost).Assembly.GetName().Name,
        });
        builder.Configuration.AddConfiguration(configuration);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });
        return builder;
    }

    /// <summary>Barrera común: entorno, opt-in y base de datos de bucle local.</summary>
    private static string Guard(IConfiguration configuration, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        if (string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("El Host autenticado de integración no se monta en Production.");
        }

        if (!string.Equals(configuration[NegativeCommerceIntegration.OptInKey], NegativeCommerceIntegration.OptInValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"El Host autenticado de integración exige {NegativeCommerceIntegration.OptInKey} = {NegativeCommerceIntegration.OptInValue}.");
        }

        var connectionString = configuration.GetConnectionString(CoreModule.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("El Host autenticado de integración exige ConnectionStrings:Default.");
        }

        var host = new NpgsqlConnectionStringBuilder(connectionString).Host;
        if (host is not ("127.0.0.1" or "localhost" or "::1"))
        {
            throw new InvalidOperationException(
                $"El Host autenticado de integración solo acepta una base de bucle local; la cadena apunta a «{host}».");
        }

        return connectionString;
    }
}
