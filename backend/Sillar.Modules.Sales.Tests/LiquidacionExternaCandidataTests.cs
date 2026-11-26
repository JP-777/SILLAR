using Sillar.Modules.Sales.Commercial;
using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// Las reglas del pago externo candidato, como reglas comprobables.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ninguna toca la base.</b> La política es pura, así que estas pruebas se
/// provocan en milisegundos y no necesitan PostgreSQL ni migraciones.
/// </para>
/// <para>
/// <b>No se prueba ninguna regla sin ratificar.</b> Lo que se comprueba es que donde
/// JP no ha decidido el resultado queda pendiente <b>y dice qué falta decidir</b>, no
/// que la decisión sea una u otra.
/// </para>
/// </remarks>
public sealed class LiquidacionExternaCandidataTests
{
    private static readonly Guid Pedido = Guid.CreateVersion7();

    private static OrderCommercialSnapshot Sano(
        string estado = OrderStatus.PendingPayment,
        decimal total = 120.50m,
        decimal cobrado = 0m,
        bool activo = true)
        => new(Pedido, "V-PRI-000123", estado, total, cobrado, activo);

    private static ExternalSettlementCandidateEvent Evento(
        string proveedor = "proveedor-candidato",
        string eventoId = "evt-0001",
        string? liquidacion = "LIQ-AB12",
        Guid? pedido = null,
        decimal importe = 120.50m,
        string? moneda = "PEN",
        string? evidencia = "evidencia-verificada-0001")
        => new(proveedor, eventoId, liquidacion, pedido ?? Pedido, importe, moneda, evidencia);

    private static SettlementCandidateRecord Previo(ExternalSettlementCandidateEvent e)
        => new(e.Provider, e.ProviderEventId, e.ExternalSettlementId, e.OrderId, e.Amount, e.Currency);

    // ================================================================
    // Pagabilidad.
    // ================================================================

    [Fact]
    public void Un_pedido_que_no_existe_se_responde_y_no_falla()
    {
        var r = ExternalCommercePolicy.EvaluarPagabilidad(Pedido, null);

        Assert.Equal(PayableOutcome.NotFound, r.Outcome);
        Assert.Equal(PayableReason.UnknownOrder, r.Reason);
        Assert.Null(r.FrozenTotal);
        Assert.Equal(CandidateSources.CurrentDatabase, r.Source);
    }

    [Fact]
    public void Un_pedido_dado_de_baja_no_es_elegible()
    {
        var r = ExternalCommercePolicy.EvaluarPagabilidad(Pedido, Sano(activo: false));

        Assert.Equal(PayableOutcome.NotEligible, r.Outcome);
        Assert.Equal(PayableReason.InactiveOrder, r.Reason);
    }

    [Fact]
    public void Un_pedido_cancelado_o_entregado_queda_pendiente_por_su_ciclo_de_vida()
    {
        foreach (var estado in new[] { OrderStatus.Cancelled, OrderStatus.Delivered })
        {
            var r = ExternalCommercePolicy.EvaluarPagabilidad(Pedido, Sano(estado));

            Assert.Equal(PayableOutcome.PendingDecision, r.Outcome);

            // Y no por la moneda: las dos cosas faltan, pero el motivo que bloquea
            // primero es el que el consumidor necesita para saber a quién preguntar.
            Assert.Equal(PayableReason.OrderLifecycleUnratified, r.Reason);
        }
    }

    [Fact]
    public void Un_pedido_sano_queda_pendiente_porque_no_hay_autoridad_de_moneda()
    {
        var r = ExternalCommercePolicy.EvaluarPagabilidad(Pedido, Sano());

        Assert.Equal(PayableOutcome.PendingDecision, r.Outcome);
        Assert.Equal(PayableReason.CurrencyAuthorityMissing, r.Reason);
    }

    [Fact]
    public void La_moneda_nunca_se_inventa()
    {
        foreach (var estado in OrderStatus.All)
        {
            var r = ExternalCommercePolicy.EvaluarPagabilidad(Pedido, Sano(estado));

            Assert.Null(r.Currency);
        }
    }

    [Fact]
    public void El_total_congelado_viaja_aparte_de_lo_ya_cobrado_a_mano()
    {
        var r = ExternalCommercePolicy.EvaluarPagabilidad(Pedido, Sano(total: 200m, cobrado: 50m));

        Assert.Equal(200m, r.FrozenTotal);
        Assert.Equal(50m, r.ManuallySettledTotal);
        Assert.Equal("V-PRI-000123", r.OrderCode);
        Assert.Equal(OrderStatus.PendingPayment, r.Status);
    }

    [Fact]
    public void Lo_ya_cobrado_a_mano_que_cubre_el_total_queda_pendiente_de_decision()
    {
        var r = ExternalCommercePolicy.EvaluarPagabilidad(Pedido, Sano(total: 200m, cobrado: 200m));

        Assert.Equal(PayableOutcome.PendingDecision, r.Outcome);
        Assert.Equal(PayableReason.AlreadySettledManually, r.Reason);
    }

    [Fact]
    public void Un_pedido_sin_importe_y_sin_cobros_no_se_toma_por_ya_cobrado()
    {
        var r = ExternalCommercePolicy.EvaluarPagabilidad(Pedido, Sano(total: 0m, cobrado: 0m));

        Assert.Equal(PayableReason.CurrencyAuthorityMissing, r.Reason);
    }

    [Fact]
    public void Ningun_pedido_puede_responder_Eligible_en_fase_A()
    {
        foreach (var estado in OrderStatus.All)
        {
            foreach (var activo in new[] { true, false })
            {
                foreach (var total in new[] { -1m, 0m, 120.50m })
                {
                    foreach (var cobrado in new[] { 0m, 60m, 500m })
                    {
                        var r = ExternalCommercePolicy.EvaluarPagabilidad(
                            Pedido, Sano(estado, total, cobrado, activo));

                        Assert.NotEqual(PayableOutcome.Eligible, r.Outcome);
                        Assert.NotEqual(PayableReason.RatifiedAndVerified, r.Reason);
                    }
                }
            }
        }
    }

    // ================================================================
    // Rechazos de la liquidación.
    // ================================================================

    [Fact]
    public void Una_liquidacion_sin_pedido_proveedor_o_evento_se_rechaza()
    {
        ExternalSettlementCandidateEvent[] malos =
        [
            Evento(pedido: Guid.Empty),
            Evento(proveedor: ""),
            Evento(proveedor: "   "),
            Evento(eventoId: ""),
            Evento(eventoId: "   "),
        ];

        foreach (var malo in malos)
        {
            var r = ExternalCommercePolicy.EvaluarLiquidacion(malo, null);

            Assert.Equal(ExternalSettlementOutcome.Rejected, r.Outcome);
            Assert.Equal(ExternalSettlementReason.InvalidIdentity, r.Reason);
        }
    }

    [Fact]
    public void Una_liquidacion_sin_evidencia_verificada_se_rechaza_antes_que_por_la_moneda()
    {
        // Le faltan las dos cosas. Un evento sin autenticar no llega a ser una
        // pregunta comercial, así que no puede salir como pendiente de decisión:
        // parecería que espera a JP cuando lo que espera es autenticarse.
        var r = ExternalCommercePolicy.EvaluarLiquidacion(
            Evento(evidencia: null, moneda: null), null);

        Assert.Equal(ExternalSettlementOutcome.Rejected, r.Outcome);
        Assert.Equal(ExternalSettlementReason.UnauthenticatedEvent, r.Reason);
    }

    [Fact]
    public void Un_importe_no_positivo_se_rechaza()
    {
        foreach (var importe in new[] { -10m, 0m })
        {
            var r = ExternalCommercePolicy.EvaluarLiquidacion(Evento(importe: importe), null);

            Assert.Equal(ExternalSettlementOutcome.Rejected, r.Outcome);
            Assert.Equal(ExternalSettlementReason.InvalidAmount, r.Reason);
        }
    }

    [Fact]
    public void Una_liquidacion_sin_moneda_queda_pendiente_y_no_se_le_supone_ninguna()
    {
        foreach (var moneda in new string?[] { null, "", "   " })
        {
            var r = ExternalCommercePolicy.EvaluarLiquidacion(Evento(moneda: moneda), null);

            Assert.Equal(ExternalSettlementOutcome.PendingDecision, r.Outcome);
            Assert.Equal(ExternalSettlementReason.MissingCurrency, r.Reason);
        }
    }

    [Fact]
    public void Sin_identidad_durable_de_liquidacion_queda_pendiente_por_esa_politica()
    {
        var r = ExternalCommercePolicy.EvaluarLiquidacion(Evento(liquidacion: null), null);

        Assert.Equal(ExternalSettlementOutcome.PendingDecision, r.Outcome);
        Assert.Equal(ExternalSettlementReason.DurableIdentityPolicyUnratified, r.Reason);
    }

    [Fact]
    public void Con_todo_lo_comprobable_comprobado_lo_que_falta_es_politica_comercial()
    {
        var r = ExternalCommercePolicy.EvaluarLiquidacion(Evento(), null);

        Assert.Equal(ExternalSettlementOutcome.PendingDecision, r.Outcome);
        Assert.Equal(ExternalSettlementReason.CommercialPolicyUnratified, r.Reason);
    }

    // ================================================================
    // Reintentos: equivalente y divergente.
    // ================================================================

    [Fact]
    public void Un_reintento_identico_queda_pendiente_sin_afirmar_efecto_aplicado()
    {
        var primero = Evento();

        var r = ExternalCommercePolicy.EvaluarLiquidacion(primero, Previo(primero));

        Assert.Equal(ExternalSettlementOutcome.PendingDecision, r.Outcome);

        // No es AlreadyAppliedEquivalent: no hay efecto del que esto sea duplicado.
        Assert.Equal(ExternalSettlementReason.EquivalentReplayWithoutDurableEffect, r.Reason);
        Assert.Null(r.DurableEffectReference);
    }

    [Fact]
    public void Un_reintento_con_hechos_distintos_es_replay_divergente()
    {
        var primero = Evento();

        ExternalSettlementCandidateEvent[] divergentes =
        [
            Evento(importe: 999m),
            Evento(pedido: Guid.CreateVersion7()),
            Evento(moneda: "USD"),
            Evento(liquidacion: "LIQ-OTRA"),
        ];

        foreach (var divergente in divergentes)
        {
            var r = ExternalCommercePolicy.EvaluarLiquidacion(divergente, Previo(primero));

            Assert.Equal(ExternalSettlementOutcome.PendingDecision, r.Outcome);
            Assert.Equal(ExternalSettlementReason.InconsistentReplay, r.Reason);
        }
    }

    [Fact]
    public void Dos_liquidaciones_que_solo_difieren_en_mayusculas_no_son_el_mismo_hecho()
    {
        // Si la comparación las fundiera, un segundo pago real pasaría por repetición
        // del primero y no se registraría nunca. Por eso es ordinal aquí, y por eso
        // su colación tendrá que ser determinista cuando llegue a tener columna.
        var primero = Evento(liquidacion: "LIQ-AB12");

        var r = ExternalCommercePolicy.EvaluarLiquidacion(
            Evento(liquidacion: "liq-ab12"), Previo(primero));

        Assert.Equal(ExternalSettlementReason.InconsistentReplay, r.Reason);
    }

    [Fact]
    public void Nunca_se_devuelve_efecto_aplicado_ni_duplicado_aplicado()
    {
        ExternalSettlementCandidateEvent[] bateria =
        [
            Evento(),
            Evento(pedido: Guid.Empty),
            Evento(evidencia: null),
            Evento(moneda: null),
            Evento(importe: -1m),
            Evento(liquidacion: null),
        ];

        foreach (var e in bateria)
        {
            foreach (var previo in new SettlementCandidateRecord?[] { null, Previo(Evento()) })
            {
                var r = ExternalCommercePolicy.EvaluarLiquidacion(e, previo);

                Assert.NotEqual(ExternalSettlementOutcome.AppliedDurable, r.Outcome);
                Assert.NotEqual(ExternalSettlementOutcome.AlreadyAppliedEquivalent, r.Outcome);
                Assert.NotEqual(ExternalSettlementReason.RatifiedAndPersisted, r.Reason);
                Assert.Null(r.DurableEffectReference);
            }
        }
    }

    // ================================================================
    // El adaptador, con su memoria volátil. Tampoco toca la base.
    // ================================================================

    [Fact]
    public async Task Un_evento_rechazado_no_se_recuerda_y_no_envenena_el_siguiente_intento()
    {
        var aplicador = new CandidateExternalSettlementApplicator(
            new InProcessSettlementCandidateJournal());

        // Mismo proveedor y mismo evento: primero sin autenticar, luego correcto.
        var rechazado = await aplicador.TryApplyAsync(
            Evento(evidencia: null), TestContext.Current.CancellationToken);

        var legitimo = await aplicador.TryApplyAsync(
            Evento(), TestContext.Current.CancellationToken);

        Assert.Equal(ExternalSettlementReason.UnauthenticatedEvent, rechazado.Reason);

        // Si el rechazo se hubiera recordado, esto saldría como reintento.
        Assert.Equal(ExternalSettlementReason.CommercialPolicyUnratified, legitimo.Reason);
    }

    [Fact]
    public async Task El_aplicador_reconoce_el_reintento_divergente_del_que_ya_vio()
    {
        var aplicador = new CandidateExternalSettlementApplicator(
            new InProcessSettlementCandidateJournal());

        var primero = await aplicador.TryApplyAsync(
            Evento(), TestContext.Current.CancellationToken);

        var segundo = await aplicador.TryApplyAsync(
            Evento(importe: 999m), TestContext.Current.CancellationToken);

        Assert.Equal(ExternalSettlementReason.CommercialPolicyUnratified, primero.Reason);
        Assert.Equal(ExternalSettlementOutcome.PendingDecision, segundo.Outcome);
        Assert.Equal(ExternalSettlementReason.InconsistentReplay, segundo.Reason);
        Assert.Null(segundo.DurableEffectReference);
    }

    [Fact]
    public async Task Una_cancelacion_solicitada_se_propaga_y_no_se_disfraza_de_pendiente()
    {
        var aplicador = new CandidateExternalSettlementApplicator(
            new InProcessSettlementCandidateJournal());

        using var cancelado = new CancellationTokenSource();
        await cancelado.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => aplicador.TryApplyAsync(Evento(), cancelado.Token));
    }
}
