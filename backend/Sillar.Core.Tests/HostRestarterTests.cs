using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Sillar.Core.Modularity;

namespace Sillar.Core.Tests;

/// <summary>
/// Parada del host tras un cambio de activación.
/// </summary>
/// <remarks>
/// Lo que se comprueba aquí es <b>cuándo</b> se pide la parada, no que el
/// proceso muera: que la petición se cuelgue del final de la respuesta y no del
/// manejador. Si se pidiera desde el manejador, el panel se quedaría sin saber
/// si la operación se hizo, y el reinicio le impediría preguntarlo.
/// </remarks>
public class HostRestarterTests
{
    [Fact]
    public void Con_la_bandera_en_false_el_host_no_se_detiene()
    {
        // Es la conducta de desarrollo: con 'dotnet run' nadie relanza el
        // proceso, así que detenerse dejaría el sistema muerto en cada prueba.
        var lifetime = new FakeLifetime();
        var restarter = Build(lifetime, restartAfterActivation: false);
        var context = new DefaultHttpContext();

        Assert.False(restarter.RestartsAutomatically);

        restarter.ScheduleAfterResponse(context, "prueba");

        Assert.Equal(0, lifetime.StopRequests);
    }

    [Fact]
    public void Con_la_bandera_en_true_la_parada_se_programa_para_despues_de_la_respuesta()
    {
        var lifetime = new FakeLifetime();
        var restarter = Build(lifetime, restartAfterActivation: true);
        var context = new DefaultHttpContext();

        Assert.True(restarter.RestartsAutomatically);

        restarter.ScheduleAfterResponse(context, "prueba");

        // Todavía no: la respuesta no ha salido. Este es el punto de la prueba.
        Assert.Equal(0, lifetime.StopRequests);
    }

    [Fact]
    public void La_parada_incondicional_tampoco_ocurre_antes_de_la_respuesta()
    {
        // La usa la instalación, que se detiene siempre; el momento sigue siendo
        // el mismo.
        var lifetime = new FakeLifetime();
        var restarter = Build(lifetime, restartAfterActivation: false);

        restarter.StopAfterResponse(new DefaultHttpContext(), "instalación");

        Assert.Equal(0, lifetime.StopRequests);
    }

    [Fact]
    public void Sin_configuracion_el_host_no_se_detiene()
    {
        // El valor por defecto es el prudente: quien despliegue con orquestador
        // lo activa a propósito. Al revés, un despliegue sin política de
        // reinicio se apagaría al primer cambio de módulo.
        var restarter = Build(new FakeLifetime(), restartAfterActivation: null);

        Assert.False(restarter.RestartsAutomatically);
    }

    private static HostRestarter Build(IHostApplicationLifetime lifetime, bool? restartAfterActivation)
    {
        var values = restartAfterActivation is null
            ? []
            : new Dictionary<string, string?>
            {
                [HostRestarter.RestartSetting] = restartAfterActivation.Value ? "true" : "false"
            };

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        return new HostRestarter(lifetime, configuration, NullLogger<HostRestarter>.Instance);
    }

    private sealed class FakeLifetime : IHostApplicationLifetime
    {
        public int StopRequests { get; private set; }

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => StopRequests++;
    }
}
