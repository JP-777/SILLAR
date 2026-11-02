using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Sillar.Modules.Payments.Application;
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;
using Sillar.Modules.Payments.Tests;
using Sillar.Modules.Reports.Orders;
using Sillar.Modules.Sales.Commercial;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Pedidos;
using Sillar.Shared.Replication;
using PaymentSource = Sillar.Modules.Sales.Contracts.PaymentSource;

namespace Sillar.Host.CommerceBridge.Tests;

/// <summary>
/// P1-POST-G6-R5 (Claude A): la composición vertical negativa M03–M10–M11 construida por
/// DI con los servicios reales de los tres módulos. Solo se sustituye infraestructura:
/// PostgreSQL de M03 (por <see cref="BaseVentasSimulada"/>) y de M11 (por el inbox en
/// memoria y un almacén de lo ya persistido). Ninguna autoridad, ningún productor de
/// módulo y ningún puerto entre módulos se sustituye.
/// </summary>
public sealed class ComposicionVerticalTests
{
    private const string CadenaSinBase = "Host=127.0.0.1;Port=1;Database=sin_base;Username=x;Password=x;Timeout=1";
    private static readonly DateTimeOffset Inicio = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
    private static readonly DateTimeOffset Fin = Inicio.AddDays(1);
    private static readonly Guid Pedido = Guid.Parse("01999f3a-0000-7000-8000-0000000000f1");
    private static readonly Guid Otro = Guid.Parse("01999f3a-0000-7000-8000-0000000000f2");
    private const string Codigo = "P-2026-0501";

    internal static readonly string Salida =
        Environment.GetEnvironmentVariable("R5_SALIDA") ?? Path.Combine(AppContext.BaseDirectory, "r5-salida");

    private sealed class Entorno(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;
        public string ApplicationName { get; set; } = "Sillar.Host.CommerceBridge.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Config(string? optIn = NegativeCommerceIntegration.OptInValue)
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = CadenaSinBase,
            [NegativeCommerceIntegration.OptInKey] = optIn,
        }).Build();

    /// <summary>Lo que M11 ya tendría persistido para un evento; vacío si no hay nada.</summary>
    private sealed class HechosPersistidos(ProviderSettlementFacts? hechos) : IProviderSettlementFactsSource
    {
        public ValueTask<ProviderSettlementFacts?> LoadCandidateFactsAsync(Guid inboxEventId, CancellationToken cancellationToken)
            => ValueTask.FromResult(hechos);
    }

    private sealed class ReferenciasPersistidas(ProviderVerifiedEvidenceReference? referencia) : IProviderVerifiedEvidenceReferenceSource
    {
        public ValueTask<IReadOnlyList<ProviderVerifiedEvidenceReference>> LoadAsync(Guid inboxEventId, CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<ProviderVerifiedEvidenceReference>>(referencia is null ? [] : [referencia]);
    }

    private sealed record Integracion(
        ServiceProvider Raiz, IServiceScope Ambito, BaseVentasSimulada Base, SalesDbContext Ctx, MemoryInboxRepository Inbox) : IDisposable
    {
        public IServiceProvider Servicios => Ambito.ServiceProvider;

        public void Dispose()
        {
            Ambito.Dispose();
            Raiz.Dispose();
        }
    }

    private static ProviderInboxStatus Listo() => new(
        Guid.CreateVersion7(), PaymentTestData.Evidence(), ProviderInboxProcessingState.Ready,
        ProviderInboxCommercialState.NotRequested, null, 0, null, null, null, null, null, 1);

    /// <summary>
    /// La composición real, con la infraestructura aislada y validada al construir: si falta
    /// una dependencia de cualquier servicio registrado, esto lanza.
    /// </summary>
    private static Integracion Montar(
        PedidoSimulado[]? pedidos = null, PagoSimulado[]? pagos = null, bool persistido = false)
    {
        var s = new ServiceCollection().AddNegativeCommerceIntegration(Config(), new Entorno(Environments.Development));

        // Aislamiento de infraestructura, solo en la prueba.
        var b = new BaseVentasSimulada();
        b.Pedidos.AddRange(pedidos ?? []);
        b.Pagos.AddRange(pagos ?? []);
        var c = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>()
            .UseNpgsql(CadenaSinBase).AddInterceptors(b).Options, new NodeIdentity("prueba"), TimeProvider.System);
        s.RemoveAll<SalesDbContext>();
        s.AddSingleton(c);

        var estado = Listo();
        var inbox = new MemoryInboxRepository(estado);
        s.RemoveAll<IProviderInboxRepository>();
        s.AddSingleton<IProviderInboxRepository>(inbox);
        ProviderVerifiedEvidenceReference? referencia = persistido
            ? ProviderVerifiedEvidenceReference.Create(Guid.CreateVersion7(), estado.Evidence, "verifier-ref-001")
            : null;
        s.RemoveAll<IProviderSettlementFactsSource>();
        s.AddSingleton<IProviderSettlementFactsSource>(new HechosPersistidos(persistido ? PaymentTestData.Facts(estado.Evidence) : null));
        s.RemoveAll<IProviderVerifiedEvidenceReferenceSource>();
        s.AddSingleton<IProviderVerifiedEvidenceReferenceSource>(new ReferenciasPersistidas(referencia));

        var raiz = s.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        return new(raiz, raiz.CreateScope(), b, c, inbox);
    }

    private static void SinEfectos(Integracion k)
    {
        Assert.Equal(0, k.Base.Guardados);
        Assert.Equal(0, k.Base.ComandosProhibidos);
        Assert.Equal(0, k.Base.Transacciones);
        Assert.All(k.Base.Consultas, sql => Assert.StartsWith("SELECT", sql.TrimStart(), StringComparison.Ordinal));
        Assert.Empty(k.Ctx.ChangeTracker.Entries());
    }

    private static void Escribir(string archivo, IEnumerable<string> lineas)
    {
        Directory.CreateDirectory(Salida);
        File.WriteAllLines(Path.Combine(Salida, archivo), lineas);
    }

    private static PedidoSimulado P(string estado = OrderStatus.PendingPayment, decimal total = 100m, string? moneda = null,
        bool activo = true, Guid? id = null, string codigo = Codigo, DateTimeOffset? creado = null)
        => new(id ?? Pedido, codigo, estado, total, moneda, activo, creado ?? Inicio.AddHours(1));

    private static PagoSimulado Pago(decimal importe, string? origen, string? moneda = null, Guid? id = null, DateTimeOffset? cuando = null)
        => new(id ?? Pedido, importe, origen, moneda, PaymentMethod.Cash, false, cuando ?? Inicio.AddHours(2));

    // ---------------------------------------------------------------------
    // Construcción, barrera y servicios resueltos
    // ---------------------------------------------------------------------

    [Fact]
    public void R5_La_composicion_construye_y_valida_todas_sus_dependencias()
    {
        using var k = Montar();
        var s = new ServiceCollection().AddNegativeCommerceIntegration(Config(), new Entorno(Environments.Development));
        var lineas = new List<string>
        {
            "# R5 · servicios de la composición AddNegativeCommerceIntegration, resueltos en un ámbito (ValidateOnBuild=true)",
            $"descriptores={s.Count}",
        };
        var fallos = 0;
        foreach (var d in s.Where(d => !d.ServiceType.IsGenericTypeDefinition).DistinctBy(d => d.ServiceType)
                     .OrderBy(d => d.ServiceType.FullName, StringComparer.Ordinal))
        {
            try
            {
                var instancia = k.Servicios.GetRequiredService(d.ServiceType);
                lineas.Add($"{d.ServiceType.FullName} | {d.Lifetime} | {instancia.GetType().FullName}");
            }
            catch (Exception e)
            {
                fallos++;
                lineas.Add($"{d.ServiceType.FullName} | {d.Lifetime} | FALLO: {e.Message}");
            }
        }

        lineas.Add($"fallos={fallos}");
        lineas.Add("== excluidos a propósito (servicio | razón)");
        lineas.AddRange(NegativeCommerceIntegration.Excluded.Select(x => $"{x.Service} | {x.Reason}"));
        Escribir("servicios-resueltos.txt", lineas);
        Assert.Equal(0, fallos);
    }

    [Fact]
    public void R5_La_barrera_impide_montar_en_Production_o_sin_el_opt_in_exacto()
    {
        foreach (var (entorno, optIn) in new (string, string?)[]
                 {
                     (Environments.Production, NegativeCommerceIntegration.OptInValue),
                     (Environments.Development, null),
                     (Environments.Development, ""),
                     (Environments.Development, "isolated-non-production "),
                     (Environments.Development, "ISOLATED-NON-PRODUCTION"),
                     (Environments.Staging, "true"),
                 })
        {
            var s = new ServiceCollection();
            Assert.Throws<InvalidOperationException>(() => s.AddNegativeCommerceIntegration(Config(optIn), new Entorno(entorno)));
            Assert.Empty(s);
        }
    }

    [Fact]
    public void R5_La_composicion_conecta_los_productores_reales_y_nada_mas()
    {
        using var k = Montar();
        var sp = k.Servicios;

        // M03 → M11: productores reales.
        Assert.Equal("Sillar.Modules.Sales.Commercial.CandidateCommercialReadOnlyEvaluationAdapter",
            sp.GetRequiredService<ICommercialReadOnlyEvaluationCandidate>().GetType().FullName);
        Assert.Equal("Sillar.Modules.Sales.Commercial.CandidateOrderCorrelationReader",
            sp.GetRequiredService<IOrderCorrelationCandidate>().GetType().FullName);
        Assert.IsType<M03CommercialReadOnlyEvaluationPortAdapter>(sp.GetRequiredService<IM03CommercialReadOnlyEvaluationPort>());
        Assert.IsType<M03OrderCorrelationCandidatePortAdapter>(sp.GetRequiredService<IM03OrderCorrelationCandidatePort>());
        Assert.IsType<M03ReadOnlyNegativeEffectPortAdapter>(sp.GetRequiredService<IProviderCommercialEffectPort>());
        Assert.IsType<ProviderInboxProcessor>(sp.GetRequiredService<IProviderInboxProcessingService>());
        Assert.IsType<HoldIndeterminateCommercialRetryPolicy>(sp.GetRequiredService<IProviderCommercialRetryPolicy>());
        // La correlación autorizada del inbox sigue siendo la barrera de FASE A: no hay productor real.
        Assert.IsType<UnavailableProviderAuthorizedOrderCorrelationSource>(sp.GetRequiredService<IProviderAuthorizedOrderCorrelationSource>());

        // M03 → M10: lectores reales; I02 retirado por la barrera monetaria.
        Assert.Equal("Sillar.Modules.Sales.Pedidos.LectorAgregadoDePedidos", sp.GetRequiredService<IOrderAggregateReader>().GetType().FullName);
        Assert.Equal("Sillar.Modules.Sales.Pedidos.LectorAgregadoDePagos", sp.GetRequiredService<IOrderPaymentAggregateReader>().GetType().FullName);
        Assert.Null(sp.GetService<IOrderValueAggregateReader>());
        Assert.IsType<OrderIndicatorsReport>(sp.GetRequiredService<IOrderIndicatorsReport>());

        // Lo que no se conecta: aplicador, journal, lector pagable, autoridades y recepción.
        Assert.Null(sp.GetService<IExternalSettlementCandidate>());
        Assert.Null(sp.GetService<ISettlementCandidateJournal>());
        Assert.Null(sp.GetService<IPayableOrderCandidate>());
        Assert.Null(sp.GetService<IProviderAuthenticatedOrderDeclarationSource>());
        Assert.Null(sp.GetService<IProviderEvidenceVerifier>());
        Assert.Null(sp.GetService<IProviderReceptionService>());
        Assert.Equal(
            new[]
            {
                nameof(IOrderValueAggregateReader), "IProviderReceptionService", "ProviderReceptionOrchestrator",
                "OrderCodeAllocator", "CongeladorDeCliente", "CreadorDePedidos", "VencimientoDePlazos",
            },
            NegativeCommerceIntegration.Excluded.Select(x => x.Service));
        // Ningún escritor de M03 queda registrado.
        var s = new ServiceCollection().AddNegativeCommerceIntegration(Config(), new Entorno(Environments.Development));
        Assert.DoesNotContain(s, d => d.ServiceType.Namespace is "Sillar.Modules.Sales.Pedidos" or "Sillar.Modules.Sales.Data"
            && d.ServiceType.Name is "OrderCodeAllocator" or "CongeladorDeCliente" or "CreadorDePedidos" or "VencimientoDePlazos");
    }

    // ---------------------------------------------------------------------
    // Flujo M03 → M11: evaluación real por el puerto resuelto
    // ---------------------------------------------------------------------

    public static TheoryData<string> EscenariosM03M11 => new(Escenarios.Keys);

    private static readonly Dictionary<string, (PedidoSimulado[] Pedidos, PagoSimulado[] Pagos, M03OrderCorrelationRequestSnapshot Peticion,
        bool Carrera, M03CommercialReadOnlyEvaluationOutcome Desenlace, string Codigo)> Escenarios = new()
    {
        ["pendiente sin moneda"] = ([P()], [], new(Codigo, null), false,
            M03CommercialReadOnlyEvaluationOutcome.DecisionRequired, "m03ro:decision_required/currency_authority_missing"),
        ["inactivo con total negativo (H-1)"] = ([P(total: -5m, activo: false)], [], new(Codigo, null), false,
            M03CommercialReadOnlyEvaluationOutcome.Rejected, "m03ro:rejected/inactive_order"),
        ["pago de proveedor no ratificado"] = ([P()], [Pago(10m, PaymentSource.Provider)], new(Codigo, null), false,
            M03CommercialReadOnlyEvaluationOutcome.Conflict, "m03ro:conflict/contradictory_facts"),
        ["pedido desconocido"] = ([P()], [], new("P-2026-0599", null), false,
            M03CommercialReadOnlyEvaluationOutcome.Rejected, "m03ro:rejected/unknown_reference"),
        ["código en blanco por id (B5)"] = ([P(codigo: "")], [], new(null, Pedido), false,
            M03CommercialReadOnlyEvaluationOutcome.Conflict, "m03ro:conflict/contradictory_facts"),
        ["uuid nulo por código (B5)"] = ([P(id: Guid.Empty)], [], new(Codigo, null), false,
            M03CommercialReadOnlyEvaluationOutcome.Conflict, "m03ro:conflict/contradictory_facts"),
        ["código en blanco por id, desaparece entre lecturas (H-3)"] = ([P(codigo: "")], [], new(null, Pedido), true,
            M03CommercialReadOnlyEvaluationOutcome.Indeterminate, "m03ro:indeterminate/order_changed_between_reads"),
    };

    [Theory]
    [MemberData(nameof(EscenariosM03M11))]
    public async Task R5_El_puerto_M11_resuelto_por_DI_evalua_con_M03_real_y_solo_da_negativos(string caso)
    {
        var (pedidos, pagos, peticion, carrera, desenlace, codigo) = Escenarios[caso];
        using var k = Montar(pedidos, pagos);
        if (carrera)
        {
            k.Base.TrasConsulta = n => { if (n == 1) { k.Base.Pedidos.Clear(); } };
        }

        var r = await k.Servicios.GetRequiredService<IM03CommercialReadOnlyEvaluationPort>().EvaluateAsync(peticion, CancellationToken.None);

        Assert.Equal(desenlace, r.Outcome);
        Assert.Null(r.TechnicalReason);
        Assert.True(M03ReadOnlyNegativeReason.TryFormat(r, out var razon));
        Assert.Equal(codigo, razon);
        Assert.NotEmpty(k.Base.Consultas);
        SinEfectos(k);
        lock (Registro) { Registro.Add($"M03→M11 · {caso} | {r.Outcome} {r.SourceVerdict}/{r.SourceReason} | código={razon} | consultas={k.Base.Consultas.Count}"); }
        Escribir("flujos-ejecutados.txt", Registro.Order(StringComparer.Ordinal));
    }

    private static readonly List<string> Registro = [];

    // ---------------------------------------------------------------------
    // Ruta inbox → procesador real, con los servicios reales disponibles
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(false, "verified_settlement_facts_missing")]
    [InlineData(true, "authorized_order_correlation_missing")]
    public async Task R5_El_procesador_real_del_inbox_termina_en_su_barrera_negativa_sin_evaluar_ni_aplicar(bool persistido, string barrera)
    {
        using var k = Montar([P()], [], persistido);
        var procesador = k.Servicios.GetRequiredService<IProviderInboxProcessingService>();
        var evento = k.Inbox.Status.InboxEventId;

        var r = await procesador.ProcessAsync(evento, PaymentTestData.VerifiedAt.AddMinutes(1),
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), CancellationToken.None);

        var st = k.Inbox.Status;
        Assert.Equal(ProviderInboxProcessOutcome.DecisionRequired, r.Outcome);
        Assert.Equal(ProviderInboxProcessingState.Held, st.ProcessingState);
        Assert.Equal(ProviderInboxCommercialState.DecisionRequired, st.CommercialState);
        Assert.Equal(barrera, st.HoldReason);
        Assert.Equal(barrera, st.LastFailureCode);
        Assert.False(M03ReadOnlyNegativeReason.IsM03ReadOnly(st.HoldReason));
        Assert.Null(st.CommercialEffectReference);
        Assert.Null(st.NextAttemptAt);
        // La barrera está antes del puerto: ni el adaptador negativo ni M03 llegan a leer.
        Assert.Empty(k.Base.Consultas);

        var otra = await procesador.ProcessAsync(evento, PaymentTestData.VerifiedAt.AddMinutes(30),
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), CancellationToken.None);
        Assert.Equal(ProviderInboxProcessOutcome.NotReady, otra.Outcome);
        Assert.Equal(st, k.Inbox.Status);
        SinEfectos(k);
        lock (Registro) { Registro.Add($"inbox→procesador · persistido={persistido} | {r.Outcome} | inbox={st.ProcessingState}/{st.CommercialState} | razón={st.HoldReason} | consultas_M03=0 | reproceso={otra.Outcome}"); }
        Escribir("flujos-ejecutados.txt", Registro.Order(StringComparer.Ordinal));
    }

    // ---------------------------------------------------------------------
    // Flujo M03 → M10: lectores reales, I02 retenido, I05–I07 no medibles
    // ---------------------------------------------------------------------

    private static PedidoSimulado[] Cohorte() =>
    [
        P(OrderStatus.PendingPayment, 100m, "PEN", true, Guid.Parse("01999f3a-0000-7000-8000-000000000101"), "P-2026-0601"),
        P(OrderStatus.PendingPayment, 50m, null, true, Guid.Parse("01999f3a-0000-7000-8000-000000000102"), "P-2026-0602"),
        P(OrderStatus.Delivered, 80m, "PEN", true, Guid.Parse("01999f3a-0000-7000-8000-000000000103"), "P-2026-0603"),
        P(OrderStatus.Cancelled, 30m, null, false, Guid.Parse("01999f3a-0000-7000-8000-000000000104"), "P-2026-0604"),
        P(OrderStatus.Expired, 20m, "PEN", true, Guid.Parse("01999f3a-0000-7000-8000-000000000105"), "P-2026-0605"),
        // Fuera de la ventana: no cuenta.
        P(OrderStatus.PendingPayment, 999m, "PEN", true, Guid.Parse("01999f3a-0000-7000-8000-000000000106"), "P-2026-0606", Fin.AddHours(1)),
    ];

    private static PagoSimulado[] PagosDeLaVentana() =>
    [
        Pago(30m, PaymentSource.Staff, "PEN", Guid.Parse("01999f3a-0000-7000-8000-000000000101")),
        Pago(20m, null, null, Guid.Parse("01999f3a-0000-7000-8000-000000000103")),
        Pago(5m, PaymentSource.Staff, "PEN", Guid.Parse("01999f3a-0000-7000-8000-000000000101"), Fin.AddHours(2)),
    ];

    [Fact]
    public async Task R5_M10_lee_a_M03_real_I01_I03_medidos_I02_retenido_e_I05_I07_no_medibles()
    {
        using var k = Montar(Cohorte(), PagosDeLaVentana());
        var sp = k.Servicios;

        var indicadores = await sp.GetRequiredService<IOrderIndicatorsReport>().ReadAsync(Inicio, Fin, CancellationToken.None);
        Assert.Equal(OrderIndicatorsSourceStatus.Available, indicadores.SourceStatus);
        Assert.Equal(5, indicadores.I01CreatedOrders);
        Assert.Equal((4L, 1L), (indicadores.ActiveOrders!.Value, indicadores.InactiveOrders!.Value));
        Assert.Equal(2, indicadores.I03CurrentStatusCounts!.Single(x => x.Status == OrderStatus.PendingPayment).Count);
        Assert.Equal(1, indicadores.I03CurrentStatusCounts!.Single(x => x.Status == OrderStatus.Expired).Count);

        // I02: hay pedidos con moneda en la ventana, y aun así ningún importe se publica.
        var valor = await new OrderValueReport(sp).ReadAsync(Inicio, Fin, CancellationToken.None);
        Assert.Equal(IndicatorStatus.NotMeasurable, valor.Status);
        Assert.Null(valor.ByCurrency);
        Assert.Null(valor.CreatedCount);

        // I05–I07: el lector real dice que ningún escritor está acreditado. En M03 ese motivo
        // precede al histórico sin origen (LectoresMonetarios.cs:126-128); el histórico se cuenta aparte.
        var pagos = await new OrderPaymentReport(sp).ReadAsync(Inicio, Fin, CancellationToken.None);
        Assert.Equal(IndicatorStatus.NotMeasurable, pagos.I05.Status);
        Assert.Equal(IndicatorStatus.NotMeasurable, pagos.I06.Status);
        Assert.Equal(IndicatorStatus.NotMeasurable, pagos.I07.Status);
        Assert.Null(pagos.I05.Count);
        Assert.Null(pagos.I06.ByCurrency);
        Assert.Null(pagos.I07.Count);
        Assert.Equal(NotMeasurableReasons.WriterNotAccredited, pagos.NotMeasurableReason);
        Assert.Equal(1, pagos.HistoricalWithoutSource!.Count);

        Assert.Equal(2, k.Base.Consultas.Count(sql => sql.Contains("GROUP BY", StringComparison.Ordinal)));
        SinEfectos(k);
        lock (Registro)
        {
            Registro.Add($"M03→M10 · I01={indicadores.I01CreatedOrders} activos={indicadores.ActiveOrders} bajas={indicadores.InactiveOrders} "
                + $"I03={string.Join(",", indicadores.I03CurrentStatusCounts!.Where(x => x.Count > 0).Select(x => $"{x.Status}:{x.Count}"))} I04={indicadores.I04CurrentlyExpiredOrders}");
            Registro.Add($"M03→M10 · I02 status={valor.Status} importes={(valor.ByCurrency is null ? "ninguno" : "PUBLICADOS")}");
            Registro.Add($"M03→M10 · I05={pagos.I05.Status} I06={pagos.I06.Status} I07={pagos.I07.Status} motivo={pagos.NotMeasurableReason}");
        }

        Escribir("flujos-ejecutados.txt", Registro.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task R5_Sin_historicos_el_motivo_de_I05_I07_es_escritor_no_acreditado()
    {
        using var k = Montar(Cohorte(), [Pago(30m, PaymentSource.Staff, "PEN", Guid.Parse("01999f3a-0000-7000-8000-000000000101"))]);

        var pagos = await new OrderPaymentReport(k.Servicios).ReadAsync(Inicio, Fin, CancellationToken.None);

        Assert.Equal(IndicatorStatus.NotMeasurable, pagos.I05.Status);
        Assert.Equal(NotMeasurableReasons.WriterNotAccredited, pagos.NotMeasurableReason);
        Assert.Null(pagos.I06.ByCurrency);
        SinEfectos(k);
    }

    /// <summary>
    /// Caracterización, fuera de la composición: si el lector de valor estuviera registrado,
    /// M10 publicaría importes I02 sin ninguna autoridad monetaria. Es la razón de la barrera.
    /// </summary>
    [Fact]
    public async Task R5_Caracterizacion_sin_la_barrera_I02_publicaria_importes_sin_autoridad_monetaria()
    {
        using var k = Montar(Cohorte(), []);
        var s = new ServiceCollection();
        s.AddSingleton(k.Ctx);
        s.AddSingleton(TimeProvider.System);
        s.AddCandidateSalesMonetaryReaders();
        s.AddScoped(_ => k.Servicios.GetRequiredService<IOrderAggregateReader>());
        using var sinBarrera = s.BuildServiceProvider();

        var valor = await new OrderValueReport(sinBarrera.CreateScope().ServiceProvider).ReadAsync(Inicio, Fin, CancellationToken.None);

        Assert.Equal(IndicatorStatus.WithData, valor.Status);
        Assert.Equal(MonetaryFacts.Format(100m + 80m + 20m), Assert.Single(valor.ByCurrency!).ActiveAmount);
        lock (Registro) { Registro.Add($"CARACTERIZACIÓN sin barrera I02 · status={valor.Status} PEN activos={valor.ByCurrency!.Single().ActiveAmount} (por esto se retira el lector)"); }
        Escribir("flujos-ejecutados.txt", Registro.Order(StringComparer.Ordinal));
    }
}
