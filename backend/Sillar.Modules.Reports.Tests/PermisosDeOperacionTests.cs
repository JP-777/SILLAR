using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
using Sillar.Modules.Reports.Orders;

namespace Sillar.Modules.Reports.Tests;

public sealed class PermisosDeOperacionTests
{
    [Theory]
    [InlineData("recuentos")]
    [InlineData("valor")]
    [InlineData("pagos")]
    public async Task La_operacion_sin_endpoint_rechaza_si_CORE_no_puede_autorizar(string kind)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var denied = await Assert.ThrowsAsync<ModuleAccessDeniedException>(() => Read(services, kind));
        Assert.Equal(503, denied.StatusCode);
    }

    [Theory]
    [InlineData("recuentos", 401)]
    [InlineData("valor", 403)]
    [InlineData("pagos", 404)]
    public async Task Un_rechazo_no_se_convierte_en_recuentos_cero_ni_fuente_ausente(string kind, int status)
    {
        var authority = new AutoridadDePrueba { Rechazo = status };
        using var services = new ServiceCollection().AddSingleton<IModuleAuthorization>(authority).BuildServiceProvider();
        Assert.Equal(status, (await Assert.ThrowsAsync<ModuleAccessDeniedException>(() => Read(services, kind))).StatusCode);
        Assert.Equal(["reporting:read"], authority.Consultas);
    }

    private static async Task Read(IServiceProvider services, string kind)
    {
        var start = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddDays(1);
        var ct = TestContext.Current.CancellationToken;
        switch (kind)
        {
            case "recuentos": await new OrderIndicatorsReport(services).ReadAsync(start, end, ct); break;
            case "valor": await new OrderValueReport(services).ReadAsync(start, end, ct); break;
            case "pagos": await new OrderPaymentReport(services).ReadAsync(start, end, ct); break;
            default: throw new InvalidOperationException("Escenario desconocido.");
        }
    }
}
