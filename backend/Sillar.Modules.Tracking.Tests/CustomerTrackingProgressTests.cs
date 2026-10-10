using System.Text.Json;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.Tracking.Application;
using Sillar.Modules.Tracking.Contracts;

namespace Sillar.Modules.Tracking.Tests;

public sealed class CustomerTrackingProgressTests
{
    [Fact]
    public async Task Revalida_al_dueno_en_cada_detalle_y_retorna_nulo_si_no_pertenece()
    {
        var fake = new OrdenesFiltradas();
        var service = new CustomerTrackingProgressService(fake);
        var clienteA = OrdenesFiltradas.Cliente;
        var clienteB = Guid.CreateVersion7();

        Assert.NotNull(await service.GetForCustomerAsync(
            clienteA, "S-2026-0101", CancellationToken.None));
        Assert.Null(await service.GetForCustomerAsync(
            clienteB, "S-2026-0101", CancellationToken.None));
        Assert.Equal(clienteB, fake.UltimoClienteConsultado);
    }

    [Fact]
    public async Task La_proyeccion_publica_no_contiene_notas_personal_ni_plazos_internos()
    {
        var service = new CustomerTrackingProgressService(new OrdenesFiltradas());
        var respuesta = await service.GetForCustomerAsync(
            OrdenesFiltradas.Cliente, "S-2026-0101", CancellationToken.None);
        Assert.NotNull(respuesta);

        var nombres = typeof(CustomerTrackingDetail).GetProperties()
            .Select(property => property.Name).ToHashSet();
        Assert.True(nombres.SetEquals([
            "VisibleCode", "CurrentStatus", "ReceivedAt", "PromisedAt",
            "LastStatusChangedAt", "Items"]));

        var json = JsonSerializer.Serialize(respuesta);
        foreach (var prohibido in new[] {
            "CustomerId", "BoardPriority", "InternalDueAt", "TrackingNotes",
            "ReceivedNotes", "Staff", "AdminUser", "OriginNode", "RequestedDetails" })
        {
            Assert.DoesNotContain(prohibido, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class OrdenesFiltradas : ICustomerServiceOrderReader
    {
        internal static readonly Guid Cliente = Guid.Parse("0199a113-6b18-7a11-a111-c02e00ea1b00");
        public Guid UltimoClienteConsultado { get; private set; }

        public Task<IReadOnlyList<CustomerServiceOrderSummary>> ListForCustomerAsync(
            Guid customerId, int limit, CancellationToken cancellationToken)
        {
            var result = customerId == Cliente
                ? new[] { new CustomerServiceOrderSummary("S-2026-0101", "received",
                    DateTimeOffset.UnixEpoch, null, DateTimeOffset.UnixEpoch) }
                : [];
            return Task.FromResult<IReadOnlyList<CustomerServiceOrderSummary>>(result);
        }

        public Task<CustomerServiceOrderDetail?> GetForCustomerAsync(
            Guid customerId, string visibleCode, CancellationToken cancellationToken)
        {
            UltimoClienteConsultado = customerId;
            CustomerServiceOrderDetail? result = customerId == Cliente && visibleCode == "S-2026-0101"
                ? new CustomerServiceOrderDetail(
                    "S-2026-0101", "received", DateTimeOffset.UnixEpoch,
                    null, DateTimeOffset.UnixEpoch,
                    [new CustomerServiceWorkItem("Anillado", "Trabajo de anillado", 2m, "unidad")])
                : null;
            return Task.FromResult(result);
        }
    }
}
