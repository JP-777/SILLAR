using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Sillar.Core.Contracts;
using Sillar.Modules.Payments;
using Sillar.Modules.Payments.Application;
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;
using Sillar.Modules.Payments.Tests;
using Sillar.Modules.Reports.Orders;
using Sillar.Modules.Sales;
using Sillar.Modules.Sales.Commercial;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Pedidos;
using Sillar.Shared.Replication;
using PaymentSource = Sillar.Modules.Sales.Contracts.PaymentSource;

namespace Sillar.Host.CommerceBridge.Tests;

/// <summary>
/// P1-POST-G6-R5 y R5.1 (Claude A): la composición vertical negativa M03–M10–M11 construida
/// por DI con los servicios reales, admitida por inventario cerrado. Solo se sustituye
/// infraestructura: PostgreSQL de M03 (por <see cref="BaseVentasSimulada"/>) y de M11 (por el
/// inbox en memoria y un almacén de lo ya persistido).
/// </summary>
public sealed class ComposicionVerticalTests
{
    private const string CadenaSinBase = "Host=127.0.0.1;Port=1;Database=sin_base;Username=x;Password=x;Timeout=1";
    private static readonly DateTimeOffset Inicio = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
    private static readonly DateTimeOffset Fin = Inicio.AddDays(1);
    private static readonly Guid Pedido = Guid.Parse("01999f3a-0000-7000-8000-0000000000f1");
    private const string Codigo = "P-2026-0501";

    internal static readonly string Salida =
        Environment.GetEnvironmentVariable("R5_SALIDA") ?? Path.Combine(AppContext.BaseDirectory, "r5-salida");

    internal sealed class Entorno(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;
        public string ApplicationName { get; set; } = "Sillar.Host.CommerceBridge.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    internal static IConfiguration Config(string? optIn = NegativeCommerceIntegration.OptInValue)
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = CadenaSinBase,
            [NegativeCommerceIntegration.OptInKey] = optIn,
        }).Build();

    internal static IServiceCollection Componer()
        => new ServiceCollection().AddNegativeCommerceIntegration(Config(), new Entorno(Environments.Development));

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
    /// La composición real con la infraestructura aislada, construida por la propia barrera:
    /// <see cref="NegativeCommerceIntegration.BuildNegativeServiceProvider"/> vuelve a verificar
    /// la colección, valida al construir y comprueba lo que resuelven los puertos guardados.
    /// </summary>
    private static Integracion Montar(PedidoSimulado[]? pedidos = null, PagoSimulado[]? pagos = null, bool persistido = false)
    {
        var s = Componer();

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

        var raiz = s.BuildNegativeServiceProvider();
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

    internal static void Escribir(string archivo, IEnumerable<string> lineas)
    {
        Directory.CreateDirectory(Salida);
        File.WriteAllLines(Path.Combine(Salida, archivo), lineas);
    }

    /// <summary>
    /// R10 (O-1): identificador de esta corrida de pruebas. La puerta lo entrega en
    /// <c>SILLAR_EVIDENCIA_EJECUCION</c> (el nombre de su base efímera, que también nombra el TRX);
    /// sin él, se genera uno por proceso. Va en la cabecera de cada evidencia y en el índice.
    /// </summary>
    internal static string Ejecucion { get; } = LeerEjecucion(Environment.GetEnvironmentVariable("SILLAR_EVIDENCIA_EJECUCION"));

    internal static string LeerEjecucion(string? valor)
        => valor is { Length: > 0 } v && System.Text.RegularExpressions.Regex.IsMatch(v, "^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$")
            ? v
            : $"local-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-p{Environment.ProcessId}";

    /// <summary>Primera línea de toda evidencia ligada a una fixture: ejecución, instancia y base.</summary>
    internal static string Cabecera(string instancia, string baseTemporal)
        => $"# ejecucion={Ejecucion} fixture={instancia} base={baseTemporal}";

    /// <summary>Nombre de archivo propio de una instancia de fixture: nunca lo comparte otra.</summary>
    internal static string ArchivoDe(string instancia, string nombre) => $"{instancia}-{nombre}";

    private static readonly List<string> Indice = [];

    /// <summary>Índice de la corrida: qué fixture, con qué base, escribió qué archivo.</summary>
    internal static void RegistrarEvidencia(string tipo, string instancia, string baseTemporal, string archivo)
    {
        lock (Indice)
        {
            var linea = $"ejecucion={Ejecucion} | tipo={tipo} | fixture={instancia} | base={baseTemporal} | archivo={archivo}";
            if (!Indice.Contains(linea))
            {
                Indice.Add(linea);
                Escribir("indice-de-evidencias.txt", Indice);
            }
        }
    }

    private static readonly List<string> Registro = [];

    private static void Anotar(string linea)
    {
        lock (Registro)
        {
            Registro.Add(linea);
            Escribir("flujos-ejecutados.txt", Registro.Order(StringComparer.Ordinal));
        }
    }

    internal static PedidoSimulado P(string estado = OrderStatus.PendingPayment, decimal total = 100m, string? moneda = null,
        bool activo = true, Guid? id = null, string codigo = Codigo, DateTimeOffset? creado = null)
        => new(id ?? Pedido, codigo, estado, total, moneda, activo, creado ?? Inicio.AddHours(1));

    internal static PagoSimulado Pago(decimal importe, string? origen, string? moneda = null, Guid? id = null, DateTimeOffset? cuando = null)
        => new(id ?? Pedido, importe, origen, moneda, PaymentMethod.Cash, false, cuando ?? Inicio.AddHours(2));

    // ---------------------------------------------------------------------
    // Construcción, barrera, inventario y alcance
    // ---------------------------------------------------------------------

    [Fact]
    public void R5_La_composicion_construye_y_resuelve_cada_servicio_admitido()
    {
        using var k = Montar();
        var s = Componer();
        var lineas = new List<string>
        {
            "# R5.1 · servicios admitidos en la composición, resueltos en un ámbito (BuildNegativeServiceProvider, ValidateOnBuild=true)",
            $"descriptores={s.Count} tipos={s.Select(d => d.ServiceType).Distinct().Count()}",
        };
        var fallos = 0;
        foreach (var d in s.DistinctBy(d => d.ServiceType).OrderBy(d => NegativeCompositionInventory.NameOf(d.ServiceType), StringComparer.Ordinal))
        {
            try
            {
                var instancia = k.Servicios.GetRequiredService(d.ServiceType);
                lineas.Add($"{NegativeCompositionInventory.NameOf(d.ServiceType)} | {d.Lifetime} | {NegativeCompositionInventory.NameOf(instancia.GetType())}");
            }
            catch (Exception e)
            {
                fallos++;
                lineas.Add($"{NegativeCompositionInventory.NameOf(d.ServiceType)} | {d.Lifetime} | FALLO: {e.Message}");
            }
        }

        lineas.Add($"fallos={fallos}");
        Escribir("servicios-resueltos.txt", lineas);
        Assert.Equal(0, fallos);
        Assert.Equal(NegativeCommerceIntegration.Inventory.Count(x => x.Entry.Admitted), s.Count);
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

        Assert.Equal("Sillar.Modules.Sales.Commercial.CandidateCommercialReadOnlyEvaluationAdapter",
            Assert.Single(sp.GetServices<ICommercialReadOnlyEvaluationCandidate>()).GetType().FullName);
        Assert.IsType<M03CommercialReadOnlyEvaluationPortAdapter>(sp.GetRequiredService<IM03CommercialReadOnlyEvaluationPort>());
        Assert.IsType<M03ReadOnlyNegativeEffectPortAdapter>(Assert.Single(sp.GetServices<IProviderCommercialEffectPort>()));
        Assert.IsType<ProviderInboxProcessor>(sp.GetRequiredService<IProviderInboxProcessingService>());
        Assert.IsType<HoldIndeterminateCommercialRetryPolicy>(sp.GetRequiredService<IProviderCommercialRetryPolicy>());
        Assert.IsType<UnavailableProviderAuthorizedOrderCorrelationSource>(sp.GetRequiredService<IProviderAuthorizedOrderCorrelationSource>());
        Assert.Equal("Sillar.Modules.Sales.Pedidos.LectorAgregadoDePedidos", sp.GetRequiredService<IOrderAggregateReader>().GetType().FullName);
        Assert.Equal("Sillar.Modules.Sales.Pedidos.LectorAgregadoDePagos", sp.GetRequiredService<IOrderPaymentAggregateReader>().GetType().FullName);
        Assert.IsType<OrderIndicatorsReport>(sp.GetRequiredService<IOrderIndicatorsReport>());

        // Nada de aplicación comercial, autoridades, recepción, I02, escritores de M03 ni lo no alcanzable.
        Assert.Null(sp.GetService<IExternalSettlementCandidate>());
        Assert.Null(sp.GetService<ISettlementCandidateJournal>());
        Assert.Null(sp.GetService<IPayableOrderCandidate>());
        Assert.Null(sp.GetService<IOrderValueAggregateReader>());
        Assert.Null(sp.GetService<IProviderAuthenticatedOrderDeclarationSource>());
        Assert.Null(sp.GetService<IProviderEvidenceVerifier>());
        Assert.Null(sp.GetService<IProviderReceptionService>());
        Assert.Null(sp.GetService<IProviderReceptionLedger>());
        Assert.Null(sp.GetService<IProviderSettlementFactRepository>());
        Assert.Null(sp.GetService<ProviderCommercialRecoveryService>());
        Assert.Null(sp.GetService<IOrderCorrelationCandidate>());
        Assert.Null(sp.GetService<IM03OrderCorrelationCandidatePort>());
        Assert.Null(sp.GetService<ICustomerOrderHistory>());
        Assert.Null(sp.GetService<IMonetaryRecordsProbe>());
        var s = Componer();
        Assert.DoesNotContain(s, d => d.ServiceType.Name is "OrderCodeAllocator" or "CongeladorDeCliente" or "CreadorDePedidos"
            or "VencimientoDePlazos" or "IAutoridadDeMoneda" or "IMonetaryRecordsProbe");
        Assert.DoesNotContain(s, d => d.ServiceType == typeof(DbContextOptions));
        Assert.Single(s, d => d.ServiceType.Name == "ServiceProviderAccessor");
    }

    /// <summary>
    /// Registrado frente a alcanzable: el grafo de constructores desde las entradas de los flujos
    /// de CommerceBridge. Lo registrado que puede escribir se declara; lo alcanzable no incluye
    /// ningún escritor comercial de M03.
    /// </summary>
    [Fact]
    public void R5_Lo_alcanzable_desde_los_flujos_se_distingue_de_lo_registrado_y_no_incluye_escritores_comerciales()
    {
        var s = Componer();
        var porServicio = s.GroupBy(d => d.ServiceType).ToDictionary(g => g.Key, g => g.Single());
        // Las factorías no exponen dependencias: se declaran sus reenvíos (PaymentsModule.cs:66-87,
        // CommercialReadOnlyEvaluationContract.cs:208-214) y los informes M10 que leen del contenedor
        // (OrderValueReport.cs:67-71, OrderPaymentReport.cs:112-118, MonetarySources.cs:16-33).
        var reenvios = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["IProviderCommercialCandidateSource"] = ["ProviderCommercialCandidateSource"],
            ["IProviderInboxProcessingService"] = ["ProviderInboxProcessor"],
            ["IProviderInboxRepository"] = ["EfProviderInboxRepository"],
            ["IProviderSettlementFactsSource"] = ["EfProviderInboxRepository"],
            ["IProviderVerifiedEvidenceReferenceSource"] = ["EfProviderInboxRepository"],
            ["ICommercialReadOnlyEvaluationCandidate"] = ["SalesDbContext"],
            ["OrderValueReport"] = ["IOrderAggregateReader", "IOrderValueAggregateReader"],
            ["OrderPaymentReport"] = ["IOrderAggregateReader", "IOrderPaymentAggregateReader"],
        };
        var entradas = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["M03→M11 evaluación"] = ["IM03CommercialReadOnlyEvaluationPort"],
            ["inbox→procesador"] = ["IProviderInboxProcessingService"],
            ["M03→M10 indicadores"] = ["IOrderIndicatorsReport", "OrderValueReport", "OrderPaymentReport"],
        };
        IEnumerable<string> Dependencias(string nombre)
        {
            if (reenvios.TryGetValue(nombre, out var r)) return r;
            var d = porServicio.Values.FirstOrDefault(x => x.ServiceType.Name == nombre);
            var impl = d?.ImplementationType;
            if (impl is null) return [];
            var ctor = impl.GetConstructors(BindingFlags.Public | BindingFlags.Instance).OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            return ctor?.GetParameters().Select(p => p.ParameterType.Name) ?? [];
        }

        var alcanzables = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var (flujo, raices) in entradas)
        {
            var pila = new Stack<string>(raices);
            while (pila.Count > 0)
            {
                var n = pila.Pop();
                if (!alcanzables.TryGetValue(n, out var flujos)) alcanzables[n] = flujos = new(StringComparer.Ordinal);
                if (!flujos.Add(flujo)) continue;
                foreach (var dep in Dependencias(n)) pila.Push(dep);
            }
        }

        var lineas = new List<string> { "# R5.1 · registrado frente a alcanzable (fuente | servicio | admitido | escribe | alcanzable desde | papel)" };
        foreach (var (fuente, e) in NegativeCommerceIntegration.Inventory)
        {
            var corto = e.Service.Contains('<') ? e.Service : e.Service[(e.Service.LastIndexOf('.') + 1)..];
            var desde = alcanzables.TryGetValue(corto, out var f) ? string.Join(", ", f) : "—";
            lineas.Add($"{fuente} | {e.Service} | {(e.Admitted ? "sí" : "no")} | {e.Writes} | {desde} | {e.Role}");
        }

        lineas.Add("== alcanzables sin descriptor propio (resueltos por reenvío o por los informes M10)");
        lineas.AddRange(alcanzables.Keys.Where(n => NegativeCommerceIntegration.Inventory.All(x => !x.Entry.Service.EndsWith("." + n, StringComparison.Ordinal)))
            .Select(n => $"{n} | {string.Join(", ", alcanzables[n])}"));
        Escribir("registrado-frente-a-alcanzable.txt", lineas);

        // Ningún escritor comercial de M03 está admitido ni es alcanzable.
        foreach (var (_, e) in NegativeCommerceIntegration.Inventory.Where(x => x.Entry.Role.StartsWith("escritor comercial", StringComparison.Ordinal)))
        {
            Assert.False(e.Admitted);
            Assert.False(alcanzables.ContainsKey(e.Service[(e.Service.LastIndexOf('.') + 1)..]));
        }

        // Lo admitido que puede escribir es el inbox de M11 y el contexto técnico de M03; se declara.
        var escriben = NegativeCommerceIntegration.Inventory.Where(x => x.Entry.Admitted && x.Entry.Writes != "no").Select(x => x.Entry.Service).ToArray();
        Assert.Contains("Sillar.Modules.Payments.Infrastructure.Persistence.EfProviderInboxRepository", escriben);
        Assert.Contains("Sillar.Modules.Sales.Data.SalesDbContext", escriben);
        Assert.DoesNotContain(escriben, n => n.Contains("Sales.Pedidos", StringComparison.Ordinal) || n.Contains("Sales.Commercial", StringComparison.Ordinal));
        // El valor I02 es alcanzable desde M10, pero no tiene descriptor: OrderValueReport lo encuentra ausente.
        Assert.True(alcanzables.ContainsKey("IOrderValueAggregateReader"));
        Assert.DoesNotContain(s, d => d.ServiceType == typeof(IOrderValueAggregateReader));
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

    private static (string, (string, object)[]) PorCodigo(string codigo) => (SqlEsperado.CorrelacionPorCodigo, [("@codigo", codigo)]);
    private static (string, (string, object)[]) PorId(Guid id) => (SqlEsperado.CorrelacionPorId, [("@plan_DeclaredOrderId_Value", id)]);
    private static (string, (string, object)[]) Releer(Guid id) => (SqlEsperado.Relectura, [("@orderId", id)]);
    private static (string, (string, object)[]) PagosDe(Guid id) => (SqlEsperado.PagosDelPedido, [("@orderId", id)]);

    /// <summary>Lo que el evaluador real de M03 lee en cada escenario (CommercialReadOnlyEvaluation.cs:478-517).</summary>
    private static readonly Dictionary<string, (string, (string, object)[])[]> ConsultasEsperadas = new()
    {
        ["pendiente sin moneda"] = [PorCodigo(Codigo), Releer(Pedido), PagosDe(Pedido)],
        ["inactivo con total negativo (H-1)"] = [PorCodigo(Codigo), Releer(Pedido), PagosDe(Pedido)],
        ["pago de proveedor no ratificado"] = [PorCodigo(Codigo), Releer(Pedido), PagosDe(Pedido)],
        ["pedido desconocido"] = [PorCodigo("P-2026-0599")],
        ["código en blanco por id (B5)"] = [PorId(Pedido), Releer(Pedido), PagosDe(Pedido)],
        ["uuid nulo por código (B5)"] = [PorCodigo(Codigo), Releer(Guid.Empty), PagosDe(Guid.Empty)],
        // La fila desaparece tras la correlación: la relectura no la encuentra y no se leen pagos.
        ["código en blanco por id, desaparece entre lecturas (H-3)"] = [PorId(Pedido), Releer(Pedido)],
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
        // C09: exactamente las consultas del evaluador, con sus filtros y valores.
        SqlEsperado.Afirmar(k.Base, ConsultasEsperadas[caso]);
        SinEfectos(k);
        Anotar($"M03→M11 · {caso} | {r.Outcome} {r.SourceVerdict}/{r.SourceReason} | código={razon} | consultas={k.Base.Consultas.Count}");
    }

    // ---------------------------------------------------------------------
    // Ruta inbox → procesador real
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
        Assert.NotEqual(ProviderInboxProcessOutcome.Applied, r.Outcome);
        Assert.NotEqual(ProviderInboxProcessOutcome.AlreadyApplied, r.Outcome);
        Assert.Empty(k.Base.Consultas);

        var otra = await procesador.ProcessAsync(evento, PaymentTestData.VerifiedAt.AddMinutes(30),
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), CancellationToken.None);
        Assert.Equal(ProviderInboxProcessOutcome.NotReady, otra.Outcome);
        Assert.Equal(st, k.Inbox.Status);
        SinEfectos(k);
        Anotar($"inbox→procesador · persistido={persistido} | {r.Outcome} | inbox={st.ProcessingState}/{st.CommercialState} | razón={st.HoldReason} | consultas_M03=0 | reproceso={otra.Outcome}");
    }

    // ---------------------------------------------------------------------
    // Flujo M03 → M10
    // ---------------------------------------------------------------------

    internal static PedidoSimulado[] Cohorte() =>
    [
        P(OrderStatus.PendingPayment, 100m, "PEN", true, Guid.Parse("01999f3a-0000-7000-8000-000000000101"), "P-2026-0601"),
        P(OrderStatus.PendingPayment, 50m, null, true, Guid.Parse("01999f3a-0000-7000-8000-000000000102"), "P-2026-0602"),
        P(OrderStatus.Delivered, 80m, "PEN", true, Guid.Parse("01999f3a-0000-7000-8000-000000000103"), "P-2026-0603"),
        P(OrderStatus.Cancelled, 30m, null, false, Guid.Parse("01999f3a-0000-7000-8000-000000000104"), "P-2026-0604"),
        P(OrderStatus.Expired, 20m, "PEN", true, Guid.Parse("01999f3a-0000-7000-8000-000000000105"), "P-2026-0605"),
        P(OrderStatus.PendingPayment, 999m, "PEN", true, Guid.Parse("01999f3a-0000-7000-8000-000000000106"), "P-2026-0606", Fin.AddHours(1)),
    ];

    internal static PagoSimulado[] PagosDeLaVentana() =>
    [
        Pago(30m, PaymentSource.Staff, "PEN", Guid.Parse("01999f3a-0000-7000-8000-000000000101")),
        Pago(20m, null, null, Guid.Parse("01999f3a-0000-7000-8000-000000000103")),
        Pago(5m, PaymentSource.Staff, "PEN", Guid.Parse("01999f3a-0000-7000-8000-000000000101"), Fin.AddHours(2)),
    ];

    /// <summary>
    /// La cohorte R6: la de R5 más el caso adversarial de C11, un pedido vencido y dado de
    /// baja, sin moneda, que I04 tiene que contar (LectorAgregadoDePedidos.cs:51-66).
    /// </summary>
    internal static PedidoSimulado[] CohorteR6() =>
    [
        .. Cohorte(),
        P(OrderStatus.Expired, 15m, null, false, Guid.Parse("01999f3a-0000-7000-8000-000000000107"), "P-2026-0607"),
    ];

    internal static PagoSimulado[] PagosR6() => PagosDeLaVentana();

    /// <summary>I03 completo, en el orden de OrderStatus.All, escrito a mano para CohorteR6.</summary>
    internal static (string, long)[] I03Esperado =>
    [
        .. OrderStatus.All.Select(estado => (estado, estado switch
        {
            OrderStatus.PendingPayment => 2L,
            OrderStatus.Delivered => 1L,
            OrderStatus.Expired => 2L,
            OrderStatus.Cancelled => 1L,
            _ => 0L,
        })),
    ];

    [Fact]
    public async Task R5_M10_lee_a_M03_real_I01_I03_I04_medidos_I02_retenido_e_I05_I07_no_medibles()
    {
        using var k = Montar(CohorteR6(), PagosR6());
        var sp = k.Servicios;

        var indicadores = await sp.GetRequiredService<IOrderIndicatorsReport>().ReadAsync(Inicio, Fin, CancellationToken.None);
        Assert.Equal(OrderIndicatorsSourceStatus.Available, indicadores.SourceStatus);
        Assert.Equal(6, indicadores.I01CreatedOrders);
        Assert.Equal((4L, 2L), (indicadores.ActiveOrders!.Value, indicadores.InactiveOrders!.Value));
        // C11: los siete estados, con los cuatro presentes; I04 cuenta también el vencido de baja.
        Assert.Equal(I03Esperado, indicadores.I03CurrentStatusCounts!.Select(x => (x.Status, x.Count)));
        Assert.Equal(2, indicadores.I04CurrentlyExpiredOrders);

        var valor = await new OrderValueReport(sp).ReadAsync(Inicio, Fin, CancellationToken.None);
        Assert.Equal(IndicatorStatus.NotMeasurable, valor.Status);
        Assert.Null(valor.ByCurrency);
        Assert.Null(valor.CreatedCount);

        // En M03 «escritor no acreditado» precede al histórico sin origen (LectoresMonetarios.cs:126-128).
        var pagos = await new OrderPaymentReport(sp).ReadAsync(Inicio, Fin, CancellationToken.None);
        Assert.Equal(IndicatorStatus.NotMeasurable, pagos.I05.Status);
        Assert.Equal(IndicatorStatus.NotMeasurable, pagos.I06.Status);
        Assert.Equal(IndicatorStatus.NotMeasurable, pagos.I07.Status);
        Assert.Null(pagos.I05.Count);
        Assert.Null(pagos.I06.ByCurrency);
        Assert.Null(pagos.I07.Count);
        Assert.Equal(NotMeasurableReasons.WriterNotAccredited, pagos.NotMeasurableReason);
        Assert.Equal(1, pagos.HistoricalWithoutSource!.Count);

        // C08: la secuencia exacta. Una agregada de pedidos, ninguna para I02 y una agregada de pagos.
        var inicio = Inicio.UtcDateTime;
        var fin = Fin.UtcDateTime;
        SqlEsperado.Afirmar(k.Base,
            (SqlEsperado.I01I03I04, [("@startUtc", inicio), ("@endUtc", fin)]),
            (SqlEsperado.I05I07, [("@inicio", inicio), ("@fin", fin)]));
        SinEfectos(k);
        Anotar($"M03→M10 · I01={indicadores.I01CreatedOrders} activos={indicadores.ActiveOrders} bajas={indicadores.InactiveOrders} "
            + $"I03={string.Join(",", indicadores.I03CurrentStatusCounts!.Where(x => x.Count > 0).Select(x => $"{x.Status}:{x.Count}"))} I04={indicadores.I04CurrentlyExpiredOrders}");
        Anotar($"M03→M10 · I02 status={valor.Status} importes={(valor.ByCurrency is null ? "ninguno" : "PUBLICADOS")}");
        Anotar($"M03→M10 · I05={pagos.I05.Status} I06={pagos.I06.Status} I07={pagos.I07.Status} motivo={pagos.NotMeasurableReason} históricos_sin_origen={pagos.HistoricalWithoutSource!.Count}");
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

    /// <summary>Caracterización, fuera de la composición: sin la barrera, I02 publicaría importes sin autoridad monetaria.</summary>
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
        Anotar($"CARACTERIZACIÓN sin barrera I02 · status={valor.Status} PEN activos={valor.ByCurrency!.Single().ActiveAmount} (por esto no se admite el lector)");
    }
}
