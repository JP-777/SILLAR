using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sillar.Core.Contracts;
using Sillar.Modules.Assistant.Authorization;
using Sillar.Modules.Assistant.Data;
using Sillar.Modules.Assistant.Data.Repositories;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Composition;

/// <summary>
/// Composición interna de M12 para un futuro registro modular. No registra
/// endpoints, sesiones, identidad de instalación ni una implementación de auditoría.
/// El host deberá suministrar esas autoridades antes de activar el módulo.
/// </summary>
public static class AssistantInternalServices
{
    /// <summary>
    /// Prepara los componentes EF y de aplicación sin abrir conexiones ni crear tablas.
    /// La política y la configuración de base deben venir de fuentes confiables del host.
    /// </summary>
    public static IServiceCollection AddAssistantInternalServices(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configurePersistence,
        EditorialPersistencePolicy editorialPolicy)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configurePersistence);
        ArgumentNullException.ThrowIfNull(editorialPolicy);
        var policies = services.Where(x => x.ServiceType == typeof(EditorialPersistencePolicy)).ToArray();
        if (policies.Length != 1 || !ReferenceEquals(policies[0].ImplementationInstance, editorialPolicy))
            throw new InvalidOperationException(
                "M12 requiere exactamente la política editorial proporcionada por el host.");
        if (services.Any(x => x.ServiceType == typeof(IAssistantEditorialAdministration)))
            throw new InvalidOperationException("M12 ya tiene una fachada editorial registrada.");

        services.AddDbContext<AssistantDbContext>(configurePersistence);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        // La única interfaz que pueden resolver handlers o servicios externos es la
        // fachada revocable. Ni el adaptador EF ni el repositorio son resolubles
        // directamente desde DI; la fábrica les da el DbContext scoped.
        services.AddScoped<IAssistantEditorialAdministration>(provider =>
            new RevocableAssistantEditorialAdministration(
                new EfAssistantEditorialAdministration(new EfEditorialArticleStore(
                    provider.GetRequiredService<AssistantDbContext>(), editorialPolicy,
                    provider.GetRequiredService<TimeProvider>())),
                provider.GetService<IAssistantAccessAuthority>(),
                provider.GetService<IRevocableAssistantEditorialAuthority>()));
        services.AddScoped<IAsyncPublishedFaqReader>(provider => new EfPublishedFaqReader(
            provider.GetRequiredService<AssistantDbContext>(),
            new EfEditorialArticleStore(provider.GetRequiredService<AssistantDbContext>(),
                editorialPolicy, provider.GetRequiredService<TimeProvider>()),
            editorialPolicy));
        return services;
    }
}
