using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Portal;
using Sillar.Modules.Portal.Application;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Tracking.Contracts;

namespace Sillar.Modules.Portal.Tests;

public sealed class PortalReaderTests
{
    private static readonly Guid Customer = Guid.Parse("0199a113-6b18-7a11-a111-c02e00ea1b00");

    [Fact]
    public void Module_declara_schema_nulo_y_dependencias_ratificadas()
    {
        var module = new PortalModule();
        Assert.Equal("portal", module.Code);
        Assert.Null(module.Schema);
        Assert.Equal(["core", "crm"], module.HardDependencies);
        Assert.Equal(["sales", "tracking"], module.SoftDependencies);
    }

    [Fact]
    public async Task Sin_identidad_no_lee_datos_y_no_inventa_cliente()
    {
        using var providers = new ServiceCollection().BuildServiceProvider();
        var identity = new IdentityReader();
        var portal = Create(null, identity, providers);
        Assert.Null(await portal.GetOverviewAsync(20, CancellationToken.None));
        Assert.Equal(0, identity.Reads);
        var detail = await portal.GetWorkAsync("S-2026-0001", CancellationToken.None);
        Assert.Equal(PortalWorkOutcome.Unauthorized, detail.Outcome);
    }

    [Fact]
    public async Task Sin_proveedores_el_portal_muestra_perfil_y_ausencias_reales()
    {
        using var providers = new ServiceCollection().BuildServiceProvider();
        var portal = Create(Customer, new IdentityReader(), providers);
        var overview = await portal.GetOverviewAsync(null, CancellationToken.None);
        Assert.NotNull(overview);
        Assert.Equal("unavailable", overview.Orders.State);
        Assert.Equal("unavailable", overview.Work.State);
        Assert.Empty(overview.Orders.Items);
        Assert.Empty(overview.Work.Items);
        Assert.DoesNotContain("CustomerId", JsonSerializer.Serialize(overview), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Proveedores_activos_devuelven_sus_secciones_y_reciben_el_cliente_de_la_sesion()
    {
        var sales = new Orders();
        var tracking = new Tracking();
        using var providers = new ServiceCollection()
            .AddSingleton<ICustomerOrderHistory>(sales)
            .AddSingleton<ICustomerTrackingProgress>(tracking)
            .BuildServiceProvider();
        var portal = Create(Customer, new IdentityReader(), providers);
        var overview = await portal.GetOverviewAsync(100, CancellationToken.None);
        Assert.NotNull(overview);
        Assert.Equal("available", overview.Orders.State);
        Assert.Equal("available", overview.Work.State);
        Assert.Equal(Customer, sales.LastCustomer);
        Assert.Equal(Customer, tracking.LastCustomer);
        Assert.Equal(20, sales.LastLimit);
        Assert.Equal(20, tracking.LastLimit);
        var detail = await portal.GetWorkAsync("S-2026-0001", CancellationToken.None);
        Assert.Equal(PortalWorkOutcome.Found, detail.Outcome);
        Assert.NotNull(detail.Detail);
        Assert.Equal(Customer, tracking.LastCustomer);
        Assert.DoesNotContain("TrackingNotes", JsonSerializer.Serialize(detail.Detail), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Falla_solo_ventas_sin_ocultar_seguimiento()
    {
        using var providers = new ServiceCollection()
            .AddSingleton<ICustomerOrderHistory>(new BrokenOrders())
            .AddSingleton<ICustomerTrackingProgress>(new Tracking())
            .BuildServiceProvider();
        var overview = await Create(Customer, new IdentityReader(), providers)
            .GetOverviewAsync(20, CancellationToken.None);
        Assert.NotNull(overview);
        Assert.Equal("error", overview.Orders.State);
        Assert.Equal("available", overview.Work.State);
    }

    [Fact]
    public async Task Detalle_ajeno_es_indistinguible_del_inexistente_en_la_lectura()
    {
        using var providers = new ServiceCollection()
            .AddSingleton<ICustomerTrackingProgress>(new Tracking())
            .BuildServiceProvider();
        var portal = Create(Customer, new IdentityReader(), providers);
        var ajeno = await portal.GetWorkAsync("S-2026-AJENO", CancellationToken.None);
        var inexistente = await portal.GetWorkAsync("SIN-CODIGO", CancellationToken.None);
        Assert.Equal(PortalWorkOutcome.NotFound, ajeno.Outcome);
        Assert.Equal(ajeno.Outcome, inexistente.Outcome);
    }

    private static PortalReader Create(Guid? id, IdentityReader identity, IServiceProvider providers)
        => new(new Current(id), identity, providers, NullLogger<PortalReader>.Instance);

    private sealed record Current(Guid? CustomerId) : ICurrentCustomer
    {
        public string? Email => "cliente@example.test";
        public bool EmailVerified => true;
    }

    private sealed class IdentityReader : ICustomerIdentityReader
    {
        public int Reads { get; private set; }
        public Task<CustomerIdentity?> GetAsync(Guid customerId, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult<CustomerIdentity?>(customerId == Customer
                ? new CustomerIdentity(customerId, "Cliente de prueba", "cliente@example.test", null)
                : null);
        }

        public Task<IReadOnlyDictionary<Guid, CustomerIdentity>> GetManyAsync(
            IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<Guid, CustomerIdentity>>(
                new Dictionary<Guid, CustomerIdentity>());
    }

    private sealed class Orders : ICustomerOrderHistory
    {
        public Guid LastCustomer { get; private set; }
        public int LastLimit { get; private set; }
        public Task<IReadOnlyList<CustomerOrderSummary>> ObtenerPedidosDeAsync(
            Guid customerId, int limit, CancellationToken cancellationToken)
        {
            LastCustomer = customerId;
            LastLimit = limit;
            return Task.FromResult<IReadOnlyList<CustomerOrderSummary>>(
                [new CustomerOrderSummary("P-2026-0001", "pending_payment", 10m, 1,
                    DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)]);
        }
    }

    private sealed class BrokenOrders : ICustomerOrderHistory
    {
        public Task<IReadOnlyList<CustomerOrderSummary>> ObtenerPedidosDeAsync(
            Guid customerId, int limit, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Fallo simulado sin datos personales");
    }

    private sealed class Tracking : ICustomerTrackingProgress
    {
        public Guid LastCustomer { get; private set; }
        public int LastLimit { get; private set; }
        public Task<IReadOnlyList<CustomerTrackingSummary>> ListForCustomerAsync(
            Guid customerId, int limit, CancellationToken cancellationToken)
        {
            LastCustomer = customerId;
            LastLimit = limit;
            return Task.FromResult<IReadOnlyList<CustomerTrackingSummary>>(
                [new CustomerTrackingSummary("S-2026-0001", "received",
                    DateTimeOffset.UnixEpoch, null, DateTimeOffset.UnixEpoch)]);
        }

        public Task<CustomerTrackingDetail?> GetForCustomerAsync(
            Guid customerId, string visibleCode, CancellationToken cancellationToken)
        {
            LastCustomer = customerId;
            var result = customerId == Customer && visibleCode == "S-2026-0001"
                ? new CustomerTrackingDetail(
                    visibleCode, "received", DateTimeOffset.UnixEpoch, null,
                    DateTimeOffset.UnixEpoch, [])
                : null;
            return Task.FromResult(result);
        }
    }
}
