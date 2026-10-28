using Microsoft.Extensions.DependencyInjection;

namespace Sillar.Host.AssistantBridge;

/// <summary>
/// Compone una única fachada editorial revocable sin publicar el servicio de
/// persistencia como contrato HTTP. No registra rutas ni concede permisos.
/// </summary>
public static class AssistantEditorialFacadeComposition
{
    /// <summary>
    /// Registra el servicio de persistencia solo por su tipo concreto y expone
    /// el contrato editorial exclusivamente mediante la fachada indicada.
    /// </summary>
    /// <remarks>
    /// El Host futuro usará como <typeparamref name="TInner"/> el servicio EF y
    /// como <typeparamref name="TFacade"/> la fachada revocable de M12. La
    /// factoría permanece opt-in y no se invoca desde <c>Program.cs</c>.
    /// </remarks>
    public static IServiceCollection AddSingleRevocableAssistantEditorialFacade<
        TContract,
        TInner,
        TFacade>(
        this IServiceCollection services,
        Func<IServiceProvider, TInner> innerFactory,
        Func<IServiceProvider, TInner, TFacade> facadeFactory)
        where TContract : class
        where TInner : class, TContract
        where TFacade : class, TContract
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(innerFactory);
        ArgumentNullException.ThrowIfNull(facadeFactory);

        if (typeof(TInner) == typeof(TFacade))
        {
            throw new InvalidOperationException(
                "La fachada editorial revocable no puede ser el servicio de persistencia.");
        }

        if (services.Any(descriptor => descriptor.ServiceType == typeof(TContract)))
        {
            throw new InvalidOperationException(
                $"Ya existe una implementación de {typeof(TContract).Name}; " +
                "M12 debe exponer exactamente una fachada editorial revocable.");
        }

        services.AddScoped(innerFactory);
        services.AddScoped<TFacade>(provider =>
            facadeFactory(provider, provider.GetRequiredService<TInner>()));
        services.AddScoped<TContract>(provider => provider.GetRequiredService<TFacade>());
        return services;
    }
}
