using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Sillar.Shared.Events;

/// <summary>
/// Bus de eventos dentro del proceso, resuelto desde el contenedor.
/// </summary>
/// <remarks>
/// Dentro del proceso porque los módulos comparten despliegue (ADR-002). No hay
/// cola, ni reintentos, ni orden garantizado entre manejadores: cuando algo
/// necesite esas garantías, será el momento de decidir con un caso delante.
///
/// La entrega se hace en el momento y de forma síncrona respecto a quien
/// publica, así que un manejador lento retrasa la respuesta. Es aceptable
/// mientras los manejadores hagan trabajo local, como invalidar una caché.
/// </remarks>
public sealed class InProcessEventBus(IServiceProvider services, ILogger<InProcessEventBus> logger)
    : IEventPublisher
{
    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : notnull
    {
        var handlers = services.GetServices<IEventHandler<TEvent>>().ToList();

        if (handlers.Count == 0)
        {
            logger.LogDebug("Evento {Event} publicado sin manejadores.", typeof(TEvent).Name);
            return;
        }

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(domainEvent, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Un manejador roto no puede deshacer lo que ya ocurrió: el
                // cambio de configuración se guardó y publicarlo es una
                // consecuencia, no parte de la operación.
                logger.LogError(
                    exception,
                    "El manejador {Handler} falló al atender {Event}. Se continúa con los demás.",
                    handler.GetType().Name,
                    typeof(TEvent).Name);
            }
        }
    }
}
