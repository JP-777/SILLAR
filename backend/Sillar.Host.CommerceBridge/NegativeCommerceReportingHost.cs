using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sillar.Modules.Reports;

namespace Sillar.Host.CommerceBridge;

/// <summary>
/// Host de integración en proceso para la consulta de Reporting (M10) sobre los lectores
/// reales de M03, a través de la composición negativa cerrada.
/// </summary>
/// <remarks>
/// <para>
/// <b>No abre ningún listener.</b> Devuelve un <see cref="WebApplication"/> construido y sin
/// arrancar: sus rutas se ejecutan en proceso, igual que en las pruebas de M10. No se
/// conecta a <c>Sillar.Api</c>, que no referencia este proyecto.
/// </para>
/// <para>
/// <b>Rutas.</b> Solo las que monta <c>ReportsModule.MapEndpoints</c>, con su política
/// <c>admin</c> declarada. Ni M03 ni M11 montan rutas aquí. La autenticación real exige el
/// almacén de sesiones de CORE (PostgreSQL): este Host no la compone ni la simula.
/// </para>
/// </remarks>
public static class NegativeCommerceReportingHost
{
    /// <summary>
    /// Construye el Host. <paramref name="isolateInfrastructure"/> solo puede sustituir
    /// infraestructura (bases de datos y almacenes): la composición se verifica de nuevo
    /// después, se valida al construir y se comprueba lo que resuelven los puertos guardados.
    /// </summary>
    public static WebApplication Build(
        IConfiguration configuration,
        string environmentName,
        Action<IServiceCollection>? isolateInfrastructure = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName,
            ApplicationName = typeof(NegativeCommerceReportingHost).Assembly.GetName().Name,
        });
        builder.Configuration.AddConfiguration(configuration);
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });
        builder.Services.AddRouting();
        builder.Services.AddNegativeCommerceIntegration(builder.Configuration, builder.Environment);
        isolateInfrastructure?.Invoke(builder.Services);
        NegativeCommerceIntegration.EnsureIntact(builder.Services);

        var app = builder.Build();
        NegativeCommerceIntegration.EnsureResolves(app.Services);
        new ReportsModule().MapEndpoints(app);
        return app;
    }
}
