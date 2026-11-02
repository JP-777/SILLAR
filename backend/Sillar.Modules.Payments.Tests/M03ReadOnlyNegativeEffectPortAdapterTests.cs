using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Payments.Application;
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;
using Sillar.Modules.Sales.Commercial;
using Sillar.Modules.Sales.Contracts;
using PaymentSource = Sillar.Modules.Sales.Contracts.PaymentSource;

namespace Sillar.Modules.Payments.Tests;

/// <summary>Dobles de P1-POST-G6-R4: solo fuentes de datos y contadores, nunca una autoridad.</summary>
internal static class R4Dobles
{
    internal sealed class Hechos(ProviderSettlementFacts hechos) : IProviderSettlementFactsSource
    {
        public ValueTask<ProviderSettlementFacts?> LoadCandidateFactsAsync(Guid inboxEventId, CancellationToken cancellationToken)
            => ValueTask.FromResult<ProviderSettlementFacts?>(hechos);
    }

    internal sealed class Referencias(ProviderVerifiedEvidenceReference referencia) : IProviderVerifiedEvidenceReferenceSource
    {
        public ValueTask<IReadOnlyList<ProviderVerifiedEvidenceReference>> LoadAsync(Guid inboxEventId, CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<ProviderVerifiedEvidenceReference>>([referencia]);
    }

    /// <summary>Correlación fija; sin código usa la única fábrica que no lo exige.</summary>
    internal sealed class CorrelacionFija(Guid orderId, string? codigo) : IProviderAuthorizedOrderCorrelationSource
    {
        public ValueTask<ProviderAuthorizedOrderCorrelation?> LoadAsync(Guid inboxEventId, PaymentIdempotencyKeys identity, CancellationToken cancellationToken)
            => ValueTask.FromResult<ProviderAuthorizedOrderCorrelation?>(codigo is null
                ? ProviderAuthorizedOrderCorrelation.Create(orderId, identity, "correlacion-de-prueba-r4")
                : ProviderAuthorizedOrderCorrelation.CreateResolvedOrder(orderId, codigo, identity, CandidateSources.CurrentDatabase));
    }

    internal sealed class Declaraciones(Func<IReadOnlyList<ProviderAuthenticatedOrderDeclaration>> declaraciones)
        : IProviderAuthenticatedOrderDeclarationSource
    {
        public ValueTask<IReadOnlyList<ProviderAuthenticatedOrderDeclaration>> LoadAsync(Guid inboxEventId, CancellationToken cancellationToken)
            => ValueTask.FromResult(declaraciones());
    }

    /// <summary>Cuenta las llamadas legítimas al adaptador negativo, sin cambiarlas.</summary>
    internal sealed class PuertoContado(IProviderCommercialEffectPort interior) : IProviderCommercialEffectPort
    {
        internal int Llamadas { get; private set; }

        public ValueTask<ProviderCommercialEffectResult> TryApplyAsync(ProviderCommercialEffectCandidate candidate, CancellationToken cancellationToken)
        {
            Llamadas++;
            return interior.TryApplyAsync(candidate, cancellationToken);
        }
    }

    /// <summary>Cuenta y guarda cada evaluación que el adaptador pide a M11.</summary>
    internal sealed class EvaluacionContada(IM03CommercialReadOnlyEvaluationPort interior) : IM03CommercialReadOnlyEvaluationPort
    {
        internal List<(M03OrderCorrelationRequestSnapshot Peticion, M03CommercialReadOnlyEvaluationResult Resultado)> Vistas { get; } = [];

        public async ValueTask<M03CommercialReadOnlyEvaluationResult> EvaluateAsync(M03OrderCorrelationRequestSnapshot request, CancellationToken cancellationToken)
        {
            var r = await interior.EvaluateAsync(request, cancellationToken);
            Vistas.Add((request, r));
            return r;
        }
    }

    /// <summary>Puerto de evaluación programable: devuelve, lanza o devuelve nulo.</summary>
    internal sealed class EvaluacionFija(Func<M03CommercialReadOnlyEvaluationResult?> respuesta) : IM03CommercialReadOnlyEvaluationPort
    {
        internal List<M03OrderCorrelationRequestSnapshot> Peticiones { get; } = [];

        public ValueTask<M03CommercialReadOnlyEvaluationResult> EvaluateAsync(M03OrderCorrelationRequestSnapshot request, CancellationToken cancellationToken)
        {
            Peticiones.Add(request);
            return ValueTask.FromResult(respuesta()!);
        }
    }
}

/// <summary>Contrato del adaptador y del formateador cerrado, con un puerto de evaluación programable.</summary>
public sealed class M03ReadOnlyNegativeEffectPortAdapterTests
{
    private static readonly Guid Pedido = Guid.Parse("01999f3a-0000-7000-8000-0000000000e1");

    /// <summary>El sufijo que añade la política Hold registrada, leído de la propia política.</summary>
    private static string SufijoHold => new HoldIndeterminateCommercialRetryPolicy()
        .Decide(1, PaymentTestData.VerifiedAt, TimeSpan.FromMinutes(1), "x").Reason;

    private static ProviderCommercialEffectCandidate Candidato(string? codigo = null)
    {
        var evidencia = PaymentTestData.Evidence();
        var identidad = PaymentIdempotencyKeys.From(evidencia);
        return ProviderCommercialEffectCandidate.From(
            Guid.CreateVersion7(), evidencia, PaymentTestData.Facts(evidencia),
            codigo is null
                ? ProviderAuthorizedOrderCorrelation.Create(Pedido, identidad, "correlacion-de-prueba-r4")
                : ProviderAuthorizedOrderCorrelation.CreateResolvedOrder(Pedido, codigo, identidad, CandidateSources.CurrentDatabase),
            ProviderVerifiedEvidenceReference.Create(Guid.CreateVersion7(), evidencia, "verifier-ref-001"));
    }

    private static async Task<ProviderCommercialEffectResult> Adaptar(M03CommercialReadOnlyEvaluationResult? r)
        => await new M03ReadOnlyNegativeEffectPortAdapter(new R4Dobles.EvaluacionFija(() => r))
            .TryApplyAsync(Candidato(), CancellationToken.None);

    private static M03CommercialReadOnlyEvaluationResult Evaluacion(
        M03CommercialReadOnlyEvaluationOutcome desenlace, CommercialReadOnlyEvaluationVerdict? veredicto, CommercialReadOnlyEvaluationReason? razon)
        => new(desenlace, veredicto, razon, null, null, null);

    private static void AfirmarNegativoSinReferencia(ProviderCommercialEffectResult r)
    {
        Assert.Contains(r.Outcome, new[]
        {
            ProviderCommercialEffectOutcome.Rejected, ProviderCommercialEffectOutcome.DecisionRequired,
            ProviderCommercialEffectOutcome.Conflict, ProviderCommercialEffectOutcome.Indeterminate,
        });
        Assert.Null(r.Reference);
        Assert.Null(r.OrderWasExpired);
        Assert.Null(r.ConfirmedFacts);
        Assert.True(M03ReadOnlyNegativeReason.IsM03ReadOnly(r.Reason), r.Reason);
    }

    // Tabla escrita a mano: las doce parejas del contrato, su desenlace y su código literal.
    [Theory]
    [InlineData(CommercialReadOnlyEvaluationVerdict.Rejected, CommercialReadOnlyEvaluationReason.NotAuthoritative, (int)ProviderCommercialEffectOutcome.Rejected, "m03ro:rejected/not_authoritative")]
    [InlineData(CommercialReadOnlyEvaluationVerdict.Rejected, CommercialReadOnlyEvaluationReason.MalformedReference, (int)ProviderCommercialEffectOutcome.Rejected, "m03ro:rejected/malformed_reference")]
    [InlineData(CommercialReadOnlyEvaluationVerdict.Rejected, CommercialReadOnlyEvaluationReason.UnknownReference, (int)ProviderCommercialEffectOutcome.Rejected, "m03ro:rejected/unknown_reference")]
    [InlineData(CommercialReadOnlyEvaluationVerdict.Rejected, CommercialReadOnlyEvaluationReason.DivergentDeclaration, (int)ProviderCommercialEffectOutcome.Rejected, "m03ro:rejected/divergent_declaration")]
    [InlineData(CommercialReadOnlyEvaluationVerdict.Rejected, CommercialReadOnlyEvaluationReason.InactiveOrder, (int)ProviderCommercialEffectOutcome.Rejected, "m03ro:rejected/inactive_order")]
    [InlineData(CommercialReadOnlyEvaluationVerdict.Rejected, CommercialReadOnlyEvaluationReason.InvalidAmount, (int)ProviderCommercialEffectOutcome.Rejected, "m03ro:rejected/invalid_amount")]
    [InlineData(CommercialReadOnlyEvaluationVerdict.PendingDecision, CommercialReadOnlyEvaluationReason.OrderLifecycleUnratified, (int)ProviderCommercialEffectOutcome.DecisionRequired, "m03ro:decision_required/order_lifecycle_unratified")]
    [InlineData(CommercialReadOnlyEvaluationVerdict.PendingDecision, CommercialReadOnlyEvaluationReason.AlreadySettledManually, (int)ProviderCommercialEffectOutcome.DecisionRequired, "m03ro:decision_required/already_settled_manually")]
    [InlineData(CommercialReadOnlyEvaluationVerdict.PendingDecision, CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing, (int)ProviderCommercialEffectOutcome.DecisionRequired, "m03ro:decision_required/currency_authority_missing")]
    [InlineData(CommercialReadOnlyEvaluationVerdict.Inconsistent, CommercialReadOnlyEvaluationReason.ContradictoryFacts, (int)ProviderCommercialEffectOutcome.Conflict, "m03ro:conflict/contradictory_facts")]
    [InlineData(CommercialReadOnlyEvaluationVerdict.Inconsistent, CommercialReadOnlyEvaluationReason.OrderChangedBetweenReads, (int)ProviderCommercialEffectOutcome.Indeterminate, "m03ro:indeterminate/order_changed_between_reads")]
    [InlineData(CommercialReadOnlyEvaluationVerdict.Inconsistent, CommercialReadOnlyEvaluationReason.UnexpectedPolicyAnswer, (int)ProviderCommercialEffectOutcome.Indeterminate, "m03ro:indeterminate/unexpected_policy_answer")]
    public async Task R4_Las_doce_parejas_del_contrato_se_traducen_a_su_negativo_con_codigo_cerrado(
        CommercialReadOnlyEvaluationVerdict veredicto, CommercialReadOnlyEvaluationReason razon, int desenlaceEsperado, string codigo)
    {
        var esperado = (ProviderCommercialEffectOutcome)desenlaceEsperado;
        var desenlace = esperado switch
        {
            ProviderCommercialEffectOutcome.Rejected => M03CommercialReadOnlyEvaluationOutcome.Rejected,
            ProviderCommercialEffectOutcome.DecisionRequired => M03CommercialReadOnlyEvaluationOutcome.DecisionRequired,
            ProviderCommercialEffectOutcome.Conflict => M03CommercialReadOnlyEvaluationOutcome.Conflict,
            _ => M03CommercialReadOnlyEvaluationOutcome.Indeterminate,
        };

        var r = await Adaptar(Evaluacion(desenlace, veredicto, razon));

        Assert.Equal(esperado, r.Outcome);
        Assert.Equal(codigo, r.Reason);
        AfirmarNegativoSinReferencia(r);
    }

    [Fact]
    public async Task R4_Fuera_de_las_doce_parejas_con_su_desenlace_todo_es_Indeterminate_tecnico_cerrado()
    {
        var aceptadas = 0;
        foreach (var veredicto in Enum.GetValues<CommercialReadOnlyEvaluationVerdict>())
        foreach (var razon in Enum.GetValues<CommercialReadOnlyEvaluationReason>())
        foreach (var desenlace in Enum.GetValues<M03CommercialReadOnlyEvaluationOutcome>())
        {
            var evaluacion = Evaluacion(desenlace, veredicto, razon);
            if (M03ReadOnlyNegativeReason.TryFormat(evaluacion, out _))
            {
                aceptadas++;
                continue;
            }

            var r = await Adaptar(evaluacion);
            Assert.Equal(ProviderCommercialEffectOutcome.Indeterminate, r.Outcome);
            Assert.Equal(M03ReadOnlyNegativeReason.InconsistentEvaluation, r.Reason);
        }

        // 3 × 12 × 4 = 144 combinaciones; solo las doce del contrato pasan.
        Assert.Equal(12, aceptadas);
    }

    [Theory]
    [InlineData("m03_readonly_evaluation_null_result", "m03ro:technical/null_result")]
    [InlineData("m03_readonly_evaluation_unknown_verdict", "m03ro:technical/unknown_verdict")]
    [InlineData("m03_readonly_evaluation_unknown_reason", "m03ro:technical/unknown_reason")]
    [InlineData("m03_readonly_evaluation_invalid_correlation", "m03ro:technical/invalid_correlation")]
    [InlineData("m03_readonly_evaluation_invalid_verdict_reason_pair", "m03ro:technical/invalid_verdict_reason_pair")]
    [InlineData("m03_readonly_evaluation_invalid_facts", "m03ro:technical/invalid_facts")]
    [InlineData("m03_readonly_evaluation_call_failed", "m03ro:technical/call_failed")]
    public async Task R4_Las_causas_tecnicas_de_M11_se_traducen_a_Indeterminate_con_codigo_cerrado(string causa, string codigo)
    {
        var r = await Adaptar(M03CommercialReadOnlyEvaluationResult.TechnicalFailure(causa));

        Assert.Equal(ProviderCommercialEffectOutcome.Indeterminate, r.Outcome);
        Assert.Equal(codigo, r.Reason);
        AfirmarNegativoSinReferencia(r);
    }

    public static TheoryData<string> CausasAjenas => new(
        "", " ", "otra_causa", "m03ro:rejected/inactive_order", "m03ro:technical/call_failed",
        "m03_readonly_evaluation_call_failed ", "M03_READONLY_EVALUATION_CALL_FAILED", "m03_readonly_evaluation_call_failed|x");

    [Theory]
    [MemberData(nameof(CausasAjenas))]
    public async Task R4_Una_causa_tecnica_ajena_no_entra_al_vocabulario_ni_con_el_prefijo(string causa)
    {
        var r = await Adaptar(M03CommercialReadOnlyEvaluationResult.TechnicalFailure(causa));

        Assert.Equal(ProviderCommercialEffectOutcome.Indeterminate, r.Outcome);
        Assert.Equal(M03ReadOnlyNegativeReason.InconsistentEvaluation, r.Reason);
        Assert.DoesNotContain(causa.Trim() is "" ? "\u0000" : causa, r.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R4_Causa_tecnica_mezclada_con_veredicto_correlacion_o_desenlace_ajeno_es_inconsistente()
    {
        var tecnica = M03CommercialReadOnlyEvaluationResult.TechnicalFailure("m03_readonly_evaluation_call_failed");
        M03CommercialReadOnlyEvaluationResult[] mezclas =
        [
            tecnica with { SourceVerdict = CommercialReadOnlyEvaluationVerdict.Inconsistent },
            tecnica with { SourceReason = CommercialReadOnlyEvaluationReason.OrderChangedBetweenReads },
            tecnica with { Correlation = new(M03OrderCorrelationOutcome.Resolved, Pedido, "", CandidateSources.CurrentDatabase) },
            tecnica with { Facts = new("preparing", 100m, true, null, M03CommercialReadOnlyCurrencyObservation.Absent, 0m, 0, 0m, 0, 0m, 0, 0) },
            tecnica with { Outcome = M03CommercialReadOnlyEvaluationOutcome.Rejected },
            tecnica with { Outcome = (M03CommercialReadOnlyEvaluationOutcome)9 },
        ];

        foreach (var mezcla in mezclas)
        {
            var r = await Adaptar(mezcla);
            Assert.Equal(ProviderCommercialEffectOutcome.Indeterminate, r.Outcome);
            Assert.Equal(M03ReadOnlyNegativeReason.InconsistentEvaluation, r.Reason);
        }
    }

    [Fact]
    public async Task R4_Enums_desconocidos_y_veredicto_o_razon_ausentes_son_inconsistentes()
    {
        M03CommercialReadOnlyEvaluationResult[] malas =
        [
            Evaluacion((M03CommercialReadOnlyEvaluationOutcome)9, CommercialReadOnlyEvaluationVerdict.Rejected, CommercialReadOnlyEvaluationReason.InactiveOrder),
            Evaluacion(M03CommercialReadOnlyEvaluationOutcome.Rejected, (CommercialReadOnlyEvaluationVerdict)7, CommercialReadOnlyEvaluationReason.InactiveOrder),
            Evaluacion(M03CommercialReadOnlyEvaluationOutcome.Rejected, CommercialReadOnlyEvaluationVerdict.Rejected, (CommercialReadOnlyEvaluationReason)99),
            Evaluacion(M03CommercialReadOnlyEvaluationOutcome.Rejected, null, CommercialReadOnlyEvaluationReason.InactiveOrder),
            Evaluacion(M03CommercialReadOnlyEvaluationOutcome.Rejected, CommercialReadOnlyEvaluationVerdict.Rejected, null),
            Evaluacion(M03CommercialReadOnlyEvaluationOutcome.Indeterminate, null, null),
        ];

        foreach (var mala in malas)
        {
            var r = await Adaptar(mala);
            Assert.Equal(ProviderCommercialEffectOutcome.Indeterminate, r.Outcome);
            Assert.Equal(M03ReadOnlyNegativeReason.InconsistentEvaluation, r.Reason);
        }
    }

    [Fact]
    public async Task R4_Ninguna_entrada_posible_produce_Applied_AlreadyApplied_ni_referencia()
    {
        // Barrido de las 6 × 14 × 6 × 3 = 1512 combinaciones, incluidos valores fuera de los enums.
        string?[] causas = [null, "m03_readonly_evaluation_call_failed", "otra"];
        var vistas = 0;
        for (var v = -1; v <= 4; v++)
        for (var z = -1; z <= 12; z++)
        for (var o = -1; o <= 4; o++)
        foreach (var causa in causas)
        {
            var r = await Adaptar(new((M03CommercialReadOnlyEvaluationOutcome)o, (CommercialReadOnlyEvaluationVerdict)v,
                (CommercialReadOnlyEvaluationReason)z, null, null, causa));
            AfirmarNegativoSinReferencia(r);
            Assert.NotEqual(ProviderCommercialEffectOutcome.Applied, r.Outcome);
            Assert.NotEqual(ProviderCommercialEffectOutcome.AlreadyApplied, r.Outcome);
            vistas++;
        }

        Assert.Equal(1512, vistas);
    }

    [Fact]
    public async Task R4_Resultado_nulo_o_excepcion_del_puerto_son_tecnicos_y_la_cancelacion_se_propaga()
    {
        var nulo = new R4Dobles.EvaluacionFija(() => null);
        var r = await new M03ReadOnlyNegativeEffectPortAdapter(nulo).TryApplyAsync(Candidato(), CancellationToken.None);
        Assert.Equal((ProviderCommercialEffectOutcome.Indeterminate, M03ReadOnlyNegativeReason.EvaluationUnavailable), (r.Outcome, r.Reason));
        Assert.Single(nulo.Peticiones);

        var lanza = new R4Dobles.EvaluacionFija(() => throw new InvalidOperationException("Host=db;Password=secreto"));
        r = await new M03ReadOnlyNegativeEffectPortAdapter(lanza).TryApplyAsync(Candidato(), CancellationToken.None);
        Assert.Equal((ProviderCommercialEffectOutcome.Indeterminate, M03ReadOnlyNegativeReason.EvaluationUnavailable), (r.Outcome, r.Reason));
        Assert.DoesNotContain("secreto", r.Reason!, StringComparison.Ordinal);
        Assert.Single(lanza.Peticiones);

        var cancela = new R4Dobles.EvaluacionFija(() => throw new OperationCanceledException());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new M03ReadOnlyNegativeEffectPortAdapter(cancela).TryApplyAsync(Candidato(), CancellationToken.None));
        Assert.Single(cancela.Peticiones);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await new M03ReadOnlyNegativeEffectPortAdapter(nulo).TryApplyAsync(null!, CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("P-2026-0401")]
    public async Task R4_La_peticion_a_M11_sale_tal_cual_de_la_correlacion_del_candidato(string? codigo)
    {
        var puerto = new R4Dobles.EvaluacionFija(() => M03CommercialReadOnlyEvaluationResult.TechnicalFailure("m03_readonly_evaluation_call_failed"));

        await new M03ReadOnlyNegativeEffectPortAdapter(puerto).TryApplyAsync(Candidato(codigo), CancellationToken.None);

        Assert.Equal(new M03OrderCorrelationRequestSnapshot(codigo, Pedido), Assert.Single(puerto.Peticiones));
    }

    [Fact]
    public void R4_El_vocabulario_es_cerrado_y_cabe_en_las_columnas_con_el_sufijo_Hold()
    {
        // hold_reason varchar(512); last_failure_code y failure_code varchar(256):
        // Migrations/20261021061912_PaymentsInitialCandidate20261021.cs:33, :40, :118.
        const int LimiteMasEstrecho = 256;
        var codigos = M03ReadOnlyNegativeReason.AllCodes;
        var forma = new Regex("^m03ro:(rejected|decision_required|conflict|indeterminate|technical)/[a-z_]+$", RegexOptions.CultureInvariant);

        Assert.Equal(21, codigos.Count);
        Assert.Equal(codigos.Count, codigos.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("automatic_retry_policy_not_ratified", SufijoHold);
        Assert.All(codigos, c =>
        {
            Assert.Matches(forma, c);
            Assert.True(M03ReadOnlyNegativeReason.IsM03ReadOnly(c));
            Assert.InRange($"{c}|{SufijoHold}".Length, 1, LimiteMasEstrecho);
        });
        Assert.InRange(codigos.Max(c => $"{c}|{SufijoHold}".Length), 1, 128);
    }

    [Fact]
    public void R4_Las_barreras_de_preparacion_y_del_procesador_no_llevan_el_prefijo()
    {
        // Literales de ProviderCommercialCandidateSource.cs:33, :42, :52, :58, :69, :75
        // y de ProviderInboxProcessor.cs:74, :94, :194.
        string[] barreras =
        [
            "verified_settlement_facts_missing", "verified_evidence_reference_source_invalid",
            "verified_evidence_reference_missing", "verified_evidence_reference_ambiguous",
            "authorized_order_correlation_missing", "authorized_order_correlation_identity_mismatch",
            "commercial_candidate_identity_mismatch", "already_applied_facts_divergent", "lease_expired_before_confirmation",
        ];

        Assert.All(barreras, b => Assert.False(M03ReadOnlyNegativeReason.IsM03ReadOnly(b), b));
        Assert.DoesNotContain(M03ReadOnlyNegativeReason.AllCodes, c => barreras.Any(b => c.Contains(b, StringComparison.Ordinal)));
    }

    [Fact]
    public void R4_El_adaptador_es_interno_y_PaymentsModule_no_lo_registra()
    {
        var tipo = typeof(M03ReadOnlyNegativeEffectPortAdapter);
        Assert.False(tipo.IsPublic);
        Assert.True(tipo.IsSealed);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=127.0.0.1;Port=1;Database=sin_base;Username=x;Password=x;Timeout=1",
            })
            .Build();
        var servicios = new ServiceCollection();
        new PaymentsModule().RegisterServices(servicios, config);

        Assert.DoesNotContain(servicios, d => d.ServiceType == typeof(IProviderCommercialEffectPort));
        Assert.DoesNotContain(servicios, d => d.ImplementationType == tipo || d.ServiceType == tipo);
        Assert.DoesNotContain(servicios, d => d.ServiceType == typeof(IM03CommercialReadOnlyEvaluationPort));
    }
}

/// <summary>R4 por la composición real: inbox en memoria → procesador → adaptador negativo → M11 → M03.</summary>
public sealed partial class M03CommercialReadOnlyEvaluationFidelityTests
{
    private const string HoldR4 = "automatic_retry_policy_not_ratified";

    private sealed record FlujoR4(
        ProviderInboxProcessor Procesador,
        ProviderInboxProcessorTests.MemoryInboxRepository Inbox,
        R4Dobles.PuertoContado Adaptador,
        R4Dobles.EvaluacionContada Evaluacion,
        Composicion K);

    private static FlujoR4 MontarR4(
        PedidoSimulado[] pedidos,
        PagoSimulado[] pagos,
        Func<Guid, ProviderVerifiedEvidenceReference, Composicion, IProviderAuthorizedOrderCorrelationSource> correlacion)
    {
        var k = Componer(pedidos, pagos);
        var estado = new ProviderInboxStatus(Guid.CreateVersion7(), PaymentTestData.Evidence(), ProviderInboxProcessingState.Ready,
            ProviderInboxCommercialState.NotRequested, null, 0, null, null, null, null, null, 1);
        var inbox = new ProviderInboxProcessorTests.MemoryInboxRepository(estado);
        var referencia = ProviderVerifiedEvidenceReference.Create(Guid.CreateVersion7(), estado.Evidence, "verifier-ref-001");
        var fuente = new ProviderCommercialCandidateSource(
            new R4Dobles.Hechos(PaymentTestData.Facts(estado.Evidence)),
            new R4Dobles.Referencias(referencia),
            correlacion(estado.InboxEventId, referencia, k));
        var evaluacion = new R4Dobles.EvaluacionContada(k.Puerto);
        var adaptador = new R4Dobles.PuertoContado(new M03ReadOnlyNegativeEffectPortAdapter(evaluacion));
        var procesador = new ProviderInboxProcessor(inbox, fuente, adaptador, new HoldIndeterminateCommercialRetryPolicy());
        return new(procesador, inbox, adaptador, evaluacion, k);
    }

    private static Task<ProviderInboxProcessResult> ProcesarR4(FlujoR4 f, int minuto = 1)
        => f.Procesador.ProcessAsync(f.Inbox.Status.InboxEventId, PaymentTestData.VerifiedAt.AddMinutes(minuto),
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), CancellationToken.None).AsTask();

    /// <summary>
    /// Cada campo contra su propio esperado. En estos flujos la razón de la finalización va a
    /// hold_reason, last_failure_code y failure_code (ProviderInboxEvent.cs:146-148, :200-202;
    /// EfProviderInboxRepository.cs:556-600); el estado comercial va aparte.
    /// </summary>
    private static async Task AfirmarRetenidoR4(FlujoR4 f, ProviderInboxProcessResult r, ProviderInboxProcessOutcome proceso,
        ProviderInboxCommercialState comercial, string holdReason, string lastFailureCode, string intento, string failureCode,
        int llamadasAdaptador)
    {
        var st = f.Inbox.Status;
        Assert.Equal(proceso, r.Outcome);
        Assert.Equal(ProviderInboxProcessingState.Held, st.ProcessingState);
        Assert.Equal(comercial, st.CommercialState);
        Assert.Equal(holdReason, st.HoldReason);
        Assert.Equal(lastFailureCode, st.LastFailureCode);
        Assert.Null(st.CommercialEffectReference);
        Assert.Null(st.NextAttemptAt);
        Assert.Null(st.LeaseToken);
        Assert.Equal(1, st.AttemptCount);
        Assert.NotEqual(ProviderInboxCommercialState.Applied, st.CommercialState);
        Assert.NotEqual(ProviderInboxCommercialState.AlreadyApplied, st.CommercialState);
        var a = Assert.Single(await f.Inbox.LoadAttemptsAsync(st.InboxEventId, CancellationToken.None));
        Assert.Equal(intento, a.Outcome);
        Assert.Equal(failureCode, a.FailureCode);
        Assert.Equal(llamadasAdaptador, f.Adaptador.Llamadas);
        Assert.Equal(llamadasAdaptador, f.Evaluacion.Vistas.Count);
        Assert.All(new[] { st.HoldReason!, st.LastFailureCode!, a.FailureCode! }, t => Assert.InRange(t.Length, 1, 256));

        // Sin reintento bajo Hold: más tarde el mismo evento ni se reclama ni vuelve a evaluar.
        var consultas = f.K.Base.Consultas.Count;
        var otra = await ProcesarR4(f, 30);
        Assert.Equal(ProviderInboxProcessOutcome.NotReady, otra.Outcome);
        Assert.Equal(llamadasAdaptador, f.Adaptador.Llamadas);
        Assert.Equal(consultas, f.K.Base.Consultas.Count);
        Assert.Equal(st, f.Inbox.Status);
    }

    /// <summary>Datos de M03, código esperado y destino en el inbox, escritos a mano.</summary>
    private static readonly Dictionary<string, (PedidoSimulado Pedido, PagoSimulado[] Pagos, Guid Correlacion, string? CodigoCorrelacion,
        string Codigo, ProviderInboxProcessOutcome Proceso, ProviderInboxCommercialState Inbox)> EscenariosR4 = new()
    {
        ["pendiente sin moneda"] = (P(), [], Pedido, Codigo, "m03ro:decision_required/currency_authority_missing",
            ProviderInboxProcessOutcome.DecisionRequired, ProviderInboxCommercialState.DecisionRequired),
        ["entregado con moneda"] = (P(OrderStatus.Delivered, moneda: "PEN"), [], Pedido, Codigo, "m03ro:decision_required/order_lifecycle_unratified",
            ProviderInboxProcessOutcome.DecisionRequired, ProviderInboxCommercialState.DecisionRequired),
        ["cubierto por staff e históricos"] = (P(), [Staff(30m), Historico(70m)], Pedido, Codigo, "m03ro:decision_required/already_settled_manually",
            ProviderInboxProcessOutcome.DecisionRequired, ProviderInboxCommercialState.DecisionRequired),
        ["inactivo con total negativo (H-1)"] = (P(total: -5m, activo: false), [], Pedido, Codigo, "m03ro:rejected/inactive_order",
            ProviderInboxProcessOutcome.Rejected, ProviderInboxCommercialState.Rejected),
        ["pendiente con total negativo y sin moneda (H-1)"] = (P(total: -5m), [], Pedido, Codigo, "m03ro:rejected/invalid_amount",
            ProviderInboxProcessOutcome.Rejected, ProviderInboxCommercialState.Rejected),
        ["pedido desconocido"] = (P(), [], Otro, "P-2026-0299", "m03ro:rejected/unknown_reference",
            ProviderInboxProcessOutcome.Rejected, ProviderInboxCommercialState.Rejected),
        ["pago de proveedor no ratificado"] = (P(), [Proveedor(10m)], Pedido, Codigo, "m03ro:conflict/contradictory_facts",
            ProviderInboxProcessOutcome.Conflict, ProviderInboxCommercialState.Conflict),
        ["estado y moneda vacíos con total negativo"] = (P("", -5m, "", false), [], Pedido, Codigo, "m03ro:conflict/contradictory_facts",
            ProviderInboxProcessOutcome.Conflict, ProviderInboxCommercialState.Conflict),
    };

    public static TheoryData<string> NombresEscenariosR4 => new(EscenariosR4.Keys);

    [Theory]
    [MemberData(nameof(NombresEscenariosR4))]
    public async Task R4_Cada_negativo_comercial_de_M03_queda_retenido_en_el_inbox_con_su_codigo_cerrado(string caso)
    {
        var (pedido, pagos, correlacion, codigoCorrelacion, codigo, proceso, inbox) = EscenariosR4[caso];
        var f = MontarR4([pedido, ElOtro], pagos, (_, _, _) => new R4Dobles.CorrelacionFija(correlacion, codigoCorrelacion));

        var r = await ProcesarR4(f);

        Assert.Null(Assert.Single(f.Evaluacion.Vistas).Resultado.TechnicalReason);
        await AfirmarRetenidoR4(f, r, proceso, inbox, codigo, codigo, "held", codigo, 1);
        SinEfectos(f.K);
    }

    [Theory]
    [InlineData("desaparece tras la correlación")]
    [InlineData("cambia de código entre lecturas")]
    public async Task R4_La_carrera_con_identidad_sana_queda_Indeterminate_retenida_con_sufijo_Hold(string carrera)
    {
        var fila = P();
        var f = MontarR4([fila, ElOtro], [], (_, _, _) => new R4Dobles.CorrelacionFija(Pedido, Codigo));
        CarrerasH3[carrera](f.K.Base, fila);

        var r = await ProcesarR4(f);

        var esperado = $"m03ro:indeterminate/order_changed_between_reads|{HoldR4}";
        await AfirmarRetenidoR4(f, r, ProviderInboxProcessOutcome.Indeterminate, ProviderInboxCommercialState.Indeterminate,
            esperado, esperado, "indeterminate", esperado, 1);
        SinEfectosSalvoLaCarrera(f.K);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task R4_Un_fallo_de_lectura_de_M03_queda_Indeterminate_tecnico_retenido(int consulta)
    {
        var f = MontarR4([P(), ElOtro], [], (_, _, _) => new R4Dobles.CorrelacionFija(Pedido, Codigo));
        f.K.Base.FalloEnConsulta = consulta;

        var r = await ProcesarR4(f);

        var esperado = $"m03ro:technical/call_failed|{HoldR4}";
        await AfirmarRetenidoR4(f, r, ProviderInboxProcessOutcome.Indeterminate, ProviderInboxCommercialState.Indeterminate,
            esperado, esperado, "indeterminate", esperado, 1);
        SinEfectos(f.K);
    }

    public static TheoryData<string, string> DegeneradasR4()
    {
        var d = new TheoryData<string, string>();
        foreach (var familia in FamiliasH3.Where(x => x.Value.Id != Guid.Empty).Select(x => x.Key))
        {
            foreach (var forma in new[] { "B5", "desaparece tras la correlación", "cambia de código entre lecturas" })
            {
                d.Add(familia, forma);
            }
        }

        return d;
    }

    [Theory]
    [MemberData(nameof(DegeneradasR4))]
    public async Task R4_B5_y_H3_con_identidad_degenerada_quedan_retenidos_y_no_son_accionables(string familia, string forma)
    {
        var (id, codigo, _) = FamiliasH3[familia];
        var fila = new PedidoSimulado(id, codigo, OrderStatus.Preparing, 100m, null, true);
        // Solo una correlación sin código (ProviderAuthorizedOrderCorrelation.Create) alcanza esta fila.
        var f = MontarR4([fila, ElOtro], [], (_, _, _) => new R4Dobles.CorrelacionFija(id, null));
        if (forma != "B5")
        {
            CarrerasH3[forma](f.K.Base, fila);
        }

        var r = await ProcesarR4(f);

        var (peticion, evaluacion) = Assert.Single(f.Evaluacion.Vistas);
        Assert.Equal(new M03OrderCorrelationRequestSnapshot(null, id), peticion);
        Assert.Equal((id, codigo), (evaluacion.Correlation!.OrderId, evaluacion.Correlation.OrderCode));
        if (forma == "B5")
        {
            Assert.NotNull(evaluacion.Facts);
            const string esperado = "m03ro:conflict/contradictory_facts";
            await AfirmarRetenidoR4(f, r, ProviderInboxProcessOutcome.Conflict, ProviderInboxCommercialState.Conflict,
                esperado, esperado, "held", esperado, 1);
            SinEfectos(f.K);
        }
        else
        {
            Assert.Null(evaluacion.Facts);
            var esperado = $"m03ro:indeterminate/order_changed_between_reads|{HoldR4}";
            await AfirmarRetenidoR4(f, r, ProviderInboxProcessOutcome.Indeterminate, ProviderInboxCommercialState.Indeterminate,
                esperado, esperado, "indeterminate", esperado, 1);
            SinEfectosSalvoLaCarrera(f.K);
        }

        // La identidad degenerada no viaja a ninguna razón persistible.
        Assert.DoesNotContain(id.ToString(), f.Inbox.Status.HoldReason!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("sana por código")]
    [InlineData("sana por id")]
    [InlineData("código vacío por id")]
    [InlineData("código U+0085 por id")]
    [InlineData("uuid nulo por código")]
    public async Task R4_Con_la_correlacion_real_la_barrera_de_preparacion_se_distingue_del_codigo_M03(string caso)
    {
        var (fila, codigoDeclarado, idDeclarado) = caso switch
        {
            "sana por código" => (P(), Codigo, (Guid?)null),
            "sana por id" => (P(), null, Pedido),
            "código vacío por id" => (P() with { OrderCode = "" }, null, Pedido),
            "código U+0085 por id" => (P() with { OrderCode = "\u0085" }, null, Pedido),
            "uuid nulo por código" => (P() with { OrderId = Guid.Empty }, Codigo, null),
            _ => throw new ArgumentOutOfRangeException(nameof(caso)),
        };
        var f = MontarR4([fila, ElOtro], [], (inbox, referencia, k) => new ProviderAuthorizedOrderCorrelationAdapter(
            new R4Dobles.Declaraciones(() => [ProviderAuthenticatedOrderDeclaration.Create(inbox, referencia, codigoDeclarado, idDeclarado)]),
            new R4Dobles.Referencias(referencia),
            new M03OrderCorrelationCandidatePortAdapter(ResolverCorrelacion(k))));

        var r = await ProcesarR4(f);

        if (caso.StartsWith("sana", StringComparison.Ordinal))
        {
            const string esperado = "m03ro:decision_required/currency_authority_missing";
            await AfirmarRetenidoR4(f, r, ProviderInboxProcessOutcome.DecisionRequired, ProviderInboxCommercialState.DecisionRequired,
                esperado, esperado, "held", esperado, 1);
            Assert.True(M03ReadOnlyNegativeReason.IsM03ReadOnly(f.Inbox.Status.HoldReason));
        }
        else
        {
            const string barrera = "authorized_order_correlation_missing";
            await AfirmarRetenidoR4(f, r, ProviderInboxProcessOutcome.DecisionRequired, ProviderInboxCommercialState.DecisionRequired,
                barrera, barrera, "held", barrera, 0);
            Assert.False(M03ReadOnlyNegativeReason.IsM03ReadOnly(f.Inbox.Status.HoldReason));
        }

        SinEfectos(f.K);
    }

    /// <summary>La correlación real de M03 (opt-in de G5-B) sobre el mismo contexto simulado de la composición.</summary>
    private static IOrderCorrelationCandidate ResolverCorrelacion(Composicion k)
    {
        var s = new ServiceCollection();
        s.AddSingleton(k.Contexto);
        s.AddCandidateSalesOrderCorrelation();
        return s.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true })
            .CreateScope().ServiceProvider.GetRequiredService<IOrderCorrelationCandidate>();
    }

    [Fact]
    public void R4_El_uuid_nulo_no_cabe_en_la_correlacion_del_inbox()
    {
        var identidad = PaymentIdempotencyKeys.From(PaymentTestData.Evidence());
        Assert.Throws<ArgumentException>(() => ProviderAuthorizedOrderCorrelation.Create(Guid.Empty, identidad, "x"));
        Assert.Throws<ArgumentException>(() =>
            ProviderAuthorizedOrderCorrelation.CreateResolvedOrder(Guid.Empty, Codigo, identidad, CandidateSources.CurrentDatabase));
    }
}
