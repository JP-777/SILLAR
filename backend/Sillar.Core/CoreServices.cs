using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Auditing;
using Sillar.Core.Authentication;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain.Values;
using Sillar.Core.Services;

namespace Sillar.Core;

/// <summary>Registro de los servicios de CORE en el contenedor.</summary>
/// <remarks>
/// Se separa en dos entradas porque el arranque tiene dos formas. En modo
/// instalación solo existe <c>/api/setup*</c> y no hay sesiones que autenticar,
/// así que registrar el esquema de autenticación allí sería montar maquinaria
/// para nadie.
/// </remarks>
public static class CoreServices
{
    private const string WorkFactorSetting = "Sillar:Security:PasswordWorkFactor";

    /// <summary>
    /// Lo que CORE necesita en cualquier modo de arranque: datos, reloj, hashes,
    /// auditoría e instalación.
    /// </summary>
    public static IServiceCollection AddCoreEssentials(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        services.AddCoreData(connectionString);
        services.AddHttpContextAccessor();
        services.TryAddTimeProvider();

        var workFactor = configuration.GetValue<int?>(WorkFactorSetting) ?? BCryptPasswordHasher.MinimumWorkFactor;
        services.AddSingleton<IPasswordHasher>(_ => new BCryptPasswordHasher(workFactor));

        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<SetupService>();

        return services;
    }

    /// <summary>
    /// Autenticación por cookie, autorización por rol y el resto de servicios de
    /// CORE. Solo en modo normal.
    /// </summary>
    public static IServiceCollection AddCoreAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
                SessionAuthenticationHandler.SchemeName,
                configureOptions: null);

        services.AddAuthorization(options =>
        {
            // Una política por rol, con la jerarquía dentro: exigir 'admin'
            // acepta también a 'super_admin', sin repetir la lista en cada
            // endpoint.
            foreach (var role in AdminRole.All)
            {
                var required = role;

                options.AddPolicy(required, policy => policy
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context => RoleHierarchy.Satisfies(
                        context.User.FindFirst(ClaimTypes.Role)?.Value,
                        required)));
            }
        });

        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(provider => provider.GetRequiredService<CurrentUser>());

        services.AddScoped<AdminAuthenticationService>();
        services.AddScoped<AdminUserService>();

        return services;
    }

    /// <summary>
    /// Registra el reloj del sistema si nadie lo hizo antes.
    /// </summary>
    /// <remarks>
    /// Inyectarlo en vez de llamar a <c>DateTimeOffset.UtcNow</c> es lo que hace
    /// que las reglas de caducidad se puedan probar sin esperar ocho horas.
    /// </remarks>
    private static IServiceCollection TryAddTimeProvider(this IServiceCollection services)
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }

        return services;
    }
}
