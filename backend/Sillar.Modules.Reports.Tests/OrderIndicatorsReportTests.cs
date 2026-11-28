using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Reports;
using Sillar.Modules.Reports.Orders;
using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Reports.Tests;

/// <summary>
/// Focales del consumidor M10 de los agregados de M03. Sin base de datos: el
/// lector de M03 se sustituye por uno en memoria que registra cómo lo llaman.
/// </summary>
public sealed class OrderIndicatorsReportTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset Fin = new(2026, 10, 8, 0, 0, 0, TimeSpan.FromHours(-5));

    [Fact]
    public void El_modulo_declara_codigo_y_schema_reporting_y_ventas_como_dependencia_blanda()
    {
        var module = new ReportsModule();
        Assert.Equal("reporting", module.Code);
        Assert.Equal("reporting", module.Schema);
        Assert.IsAssignableFrom<Sillar.Shared.Data.Modularity.IModuleMigrations>(module);
        Assert.Equal(["core"], module.HardDependencies);
        Assert.Equal(["sales"], module.SoftDependencies);
    }

    [Fact]
    public async Task Sin_el_modulo_de_ventas_la_fuente_no_esta_disponible_y_ninguna_metrica_es_cero()
    {
        using var services = new ServiceCollection().ConPermisoDePrueba().BuildServiceProvider();

        var result = await new OrderIndicatorsReport(services).ReadAsync(Inicio, Fin, CancellationToken.None);

        Assert.Equal(OrderIndicatorsSourceStatus.SourceUnavailable, result.SourceStatus);
        Assert.Null(result.ObservedAtUtc);
        Assert.Null(result.I01CreatedOrders);
        Assert.Null(result.I03CurrentStatusCounts);
        Assert.Null(result.I04CurrentlyExpiredOrders);
        Assert.Null(result.ActiveOrders);
        Assert.Null(result.InactiveOrders);
    }

    [Fact]
    public async Task Con_ventas_copia_solo_recuentos_e_instante_del_propietario()
    {
        var reader = new LectorEnMemoria();
        using var services = Proveedor(reader);

        var result = await new OrderIndicatorsReport(services).ReadAsync(Inicio, Fin, CancellationToken.None);

        Assert.Equal(OrderIndicatorsSourceStatus.Available, result.SourceStatus);
        Assert.Equal(5, result.I01CreatedOrders);
        Assert.Equal(2, result.I04CurrentlyExpiredOrders);
        Assert.Equal(4, result.ActiveOrders);
        Assert.Equal(1, result.InactiveOrders);
        Assert.Equal(LectorEnMemoria.Observado, result.ObservedAtUtc);
        Assert.Equal(OrderStatus.All, result.I03CurrentStatusCounts!.Select(x => x.Status));
    }

    [Fact]
    public async Task El_periodo_viaja_a_ventas_en_utc_y_semiabierto()
    {
        var reader = new LectorEnMemoria();
        using var services = Proveedor(reader);

        var result = await new OrderIndicatorsReport(services).ReadAsync(Inicio, Fin, CancellationToken.None);

        Assert.Equal(TimeSpan.Zero, reader.Inicio!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, reader.Fin!.Value.Offset);
        Assert.Equal(Inicio, reader.Inicio);
        Assert.Equal(Fin, reader.Fin);
        Assert.Equal(TimeSpan.Zero, result.StartInclusive.Offset);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Un_fin_que_no_es_posterior_al_inicio_se_rechaza_sin_consultar_ventas(int horas)
    {
        var reader = new LectorEnMemoria();
        using var services = Proveedor(reader);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new OrderIndicatorsReport(services).ReadAsync(Inicio, Inicio.AddHours(horas), CancellationToken.None));
        Assert.Equal(0, reader.Llamadas);
    }

    [Fact]
    public async Task Un_fallo_real_de_ventas_se_propaga_y_no_se_disfraza_de_fuente_ausente()
    {
        using var services = Proveedor(new LectorEnMemoria { Falla = true });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new OrderIndicatorsReport(services).ReadAsync(Inicio, Fin, CancellationToken.None));
    }

    private static ServiceProvider Proveedor(IOrderAggregateReader reader) =>
        new ServiceCollection().ConPermisoDePrueba().AddSingleton(reader).BuildServiceProvider();

    private sealed class LectorEnMemoria : IOrderAggregateReader
    {
        public static readonly DateTimeOffset Observado = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);
        public bool Falla { get; init; }
        public int Llamadas { get; private set; }
        public DateTimeOffset? Inicio { get; private set; }
        public DateTimeOffset? Fin { get; private set; }

        public Task<OrderAggregateSnapshot> ReadCreatedBetweenAsync(
            DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
        {
            Llamadas++;
            Inicio = startInclusive;
            Fin = endExclusive;
            if (Falla) throw new InvalidOperationException("Ventas no respondió.");
            var porEstado = OrderStatus.All
                .Select(status => new OrderStatusCount(status,
                    status == OrderStatus.PendingPayment ? 3 : status == OrderStatus.Expired ? 2 : 0))
                .ToArray();
            return Task.FromResult(new OrderAggregateSnapshot(
                startInclusive, endExclusive, Observado, 5, porEstado, 2, new OrderActivityCounts(4, 1)));
        }
    }
}
