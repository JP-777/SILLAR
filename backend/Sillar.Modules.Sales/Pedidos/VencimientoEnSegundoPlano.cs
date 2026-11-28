using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sillar.Modules.Sales.Pedidos;

/// <summary>El estado persistido alimenta la bandeja del personal; no depende de entrega por el bus.</summary>
internal sealed class VencimientoEnSegundoPlano(IServiceScopeFactory scopes,
    ILogger<VencimientoEnSegundoPlano> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<VencimientoDePlazos>()
                    .VencerLoCumplidoAsync(stoppingToken);
                if (result.Vencidos > 0) logger.LogInformation("Hay {Cantidad} nuevos pedidos vencidos en la bandeja de atención.", result.Vencidos);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "No se pudieron revisar los plazos de pedidos. Se reintentará en un minuto."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
