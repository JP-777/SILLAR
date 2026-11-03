using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Sillar.Core;
using Sillar.Core.Auditing;
using Sillar.Core.Authentication;
using Sillar.Core.Data;
using Sillar.Core.Endpoints;
using Sillar.Core.Modularity;
using Sillar.Core.Settings;
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
/// <para>
/// <b>Costura con CORE (R9).</b> Con Reporting activo se registran los productores candidatos de
/// CORE que consumen <c>/installation-zone</c> y <c>/orders/export-audit</c>:
/// <c>AddCandidateInstallationTimeZone</c> y <c>AddCandidateExportAudit</c>. Son los de CORE, sin
/// cambios: la zona nunca sale verificada y la acción <c>export</c> no está en el vocabulario de
/// auditoría, así que la exportación sigue cerrada.
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
            // R9: la costura M10 ↔ CORE (CoreSeamEndpoints) con los productores candidatos de CORE,
            // por sus registros públicos y sin cambiarlos. Se registran antes de fotografiar los
            // servicios críticos: la sustitución de infraestructura no puede tocarlos.
            builder.Services.AddCandidateInstallationTimeZone();
            builder.Services.AddCandidateExportAudit();
            builder.Services.AddNegativeCommerceIntegration(builder.Configuration, builder.Environment);
            var criticos = builder.Services.Where(IsCoreCritical).ToArray();
            isolateInfrastructure?.Invoke(builder.Services);
            EnsureCoreCriticalUntouched(criticos, builder.Services);
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

    /// <summary>
    /// Servicios que la sustitución de infraestructura nunca puede tocar: todo lo de CORE y
    /// todo lo de autenticación y autorización de ASP.NET, también como argumento genérico
    /// (opciones, configuradores, manejadores de esquema).
    /// </summary>
    internal static bool IsCoreCritical(ServiceDescriptor d)
    {
        var implementation = d.ImplementationType ?? d.ImplementationInstance?.GetType();
        return IsCritical(d.ServiceType) || implementation is not null && IsCritical(implementation);
    }

    private static readonly System.Reflection.Assembly[] CoreAssemblies =
        [typeof(CoreModule).Assembly, typeof(Sillar.Core.Contracts.AdminRole).Assembly];

    private static bool IsCritical(Type type)
        => CoreAssemblies.Contains(type.Assembly)
            || type.Namespace is { } ns && (ns.StartsWith("Microsoft.AspNetCore.Authentication", StringComparison.Ordinal)
                || ns.StartsWith("Microsoft.AspNetCore.Authorization", StringComparison.Ordinal))
            || type.IsGenericType && type.GetGenericArguments().Any(IsCritical);

    /// <summary>
    /// Los descriptores críticos tienen que seguir siendo exactamente los mismos objetos, en el
    /// mismo orden: ni uno quitado, ni uno añadido, ni uno sustituido.
    /// </summary>
    private static void EnsureCoreCriticalUntouched(IReadOnlyList<ServiceDescriptor> before, IServiceCollection services)
    {
        var after = services.Where(IsCoreCritical).ToArray();
        if (after.Length == before.Count && after.Zip(before).All(par => ReferenceEquals(par.First, par.Second)))
        {
            return;
        }

        var quitados = before.Where(d => !after.Contains(d)).Select(Describe);
        var añadidos = after.Where(d => !before.Contains(d)).Select(Describe);
        throw new InvalidOperationException(
            "La sustitución de infraestructura no puede tocar servicios críticos de CORE ni de autenticación. "
            + $"Quitados o sustituidos: [{string.Join("; ", quitados)}]. Añadidos: [{string.Join("; ", añadidos)}].");
    }

    private static string Describe(ServiceDescriptor d)
        => $"{Sillar.Modules.Payments.Application.NegativeCompositionInventory.NameOf(d.ServiceType)} → "
            + Sillar.Modules.Payments.Application.NegativeCompositionInventory.ImplementationOf(d);

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
