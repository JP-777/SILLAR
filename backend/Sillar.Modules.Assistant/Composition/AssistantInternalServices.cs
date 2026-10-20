using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        services.AddDbContext<AssistantDbContext>(configurePersistence);
        services.AddSingleton(editorialPolicy);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<EfEditorialArticleStore>();
        services.AddScoped<IAssistantEditorialAdministration, EfAssistantEditorialAdministration>();
        services.AddScoped<IAsyncPublishedFaqReader, EfPublishedFaqReader>();
        return services;
    }
}
