using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sillar.Core.Modularity;

/// <summary>
/// Detiene el host tras un cambio de activación, para que el orquestador lo
/// relance.
/// </summary>
/// <remarks>
/// El enrutamiento se construye al arrancar (SPEC §7): escribir la fila de
/// activación no hace aparecer ni desaparecer rutas en el proceso vivo. La única
/// forma honesta de aplicar el cambio es volver a arrancar.
/// </remarks>
public sealed class HostRestarter(
    IHostApplicationLifetime lifetime,
    IConfiguration configuration,
    ILogger<HostRestarter> logger)
{
    /// <summary>Bandera que gobierna si el proceso se detiene.</summary>
    public const string RestartSetting = "Modules:RestartAfterActivation";

    /// <summary>
    /// Margen entre el final de la respuesta y la parada.
    /// </summary>
    /// <remarks>
    /// La respuesta ya salió, pero el servidor todavía está cerrando la conexión.
    /// Parar en ese instante puede cortarla antes de que el cliente termine de
    /// leerla.
    /// </remarks>
    private static readonly TimeSpan Grace = TimeSpan.FromMilliseconds(500);

    /// <summary>Indica si esta instalación se reinicia sola.</summary>
    public bool RestartsAutomatically => configuration.GetValue(RestartSetting, defaultValue: false);

    /// <summary>
    /// Programa la parada del host <b>para cuando la respuesta haya salido</b>.
    /// </summary>
    /// <remarks>
    /// El orden no es negociable. Detener el proceso antes de vaciar la respuesta
    /// deja al panel sin saber si la operación se hizo, y el reinicio le impide
    /// preguntarlo: la petición muere sin respuesta justo después de que el
    /// cambio se haya escrito.
    ///
    /// Por eso se cuelga del final de la respuesta —<c>HttpResponse.OnCompleted</c>—
    /// y no se llama a <c>StopApplication</c> desde el manejador.
    /// </remarks>
    /// <param name="context">Petición en curso.</param>
    /// <param name="reason">Qué provocó el reinicio, para el registro.</param>
    public void ScheduleAfterResponse(HttpContext context, string reason)
    {
        if (!RestartsAutomatically)
        {
            logger.LogWarning(
                "{Reason}. El host NO se detiene porque {Setting} está en false: el cambio surtirá " +
                "efecto cuando alguien lo relance.",
                reason,
                RestartSetting);
            return;
        }

        StopAfterResponse(context, reason);
    }

    /// <summary>
    /// Detiene el host cuando la respuesta haya salido, sin consultar la
    /// configuración.
    /// </summary>
    /// <remarks>
    /// Para los casos en que el reinicio no es una preferencia sino la única
    /// salida: terminar la instalación cambia el modo de arranque, así que el
    /// proceso tiene que volver a levantarse aunque nadie lo haya pedido.
    /// </remarks>
    public void StopAfterResponse(HttpContext context, string reason)
        => context.Response.OnCompleted(() =>
        {
            logger.LogWarning(
                "{Reason}. La respuesta ya salió; el host se detiene para que el orquestador lo relance. " +
                "En Docker el contenedor reinicia solo; con 'dotnet run' hay que volver a lanzarlo.",
                reason);

            // Sin await: la respuesta está cerrada y este callback no debe
            // quedarse esperando medio segundo.
            _ = Task.Delay(Grace).ContinueWith(_ => lifetime.StopApplication(), TaskScheduler.Default);

            return Task.CompletedTask;
        });
}
