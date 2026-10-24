using Sillar.Modules.Sales.Commercial;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// La correlación entre una declaración externa y el pedido comercial.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ninguna toca la base.</b> Lo que se prueba aquí es qué declaraciones no merecen
/// ni una consulta y cómo se juzga lo que vuelve: las dos cosas que de verdad deciden.
/// </para>
/// <para>
/// <b>Lo que estas pruebas NO prueban:</b> que la consulta real compare byte a byte.
/// Eso lo decide la colación de <c>order_code</c> en PostgreSQL y no se puede
/// comprobar sin base; aquí se prueba que M03 no pliegue nada por su cuenta.
/// </para>
/// </remarks>
public sealed class CorrelacionDePedidoCandidataTests
{
    private static readonly Guid Pedido = Guid.Parse("01999f3a-0000-7000-8000-000000000001");
    private static readonly Guid Otro = Guid.Parse("01999f3a-0000-7000-8000-000000000002");

    private const string Codigo = "P-2026-0147";

    // ---------- la forma del código, en la función pura que la compone ----------

    [Fact]
    public void Todo_codigo_que_el_formato_ratificado_compone_se_reconoce_despues()
    {
        foreach (var etiqueta in new[] { "P", "W", "p", "SUC-NORTE", "Ñ" })
        {
            foreach (var anio in new[] { 2026, 2027, 3001 })
            {
                foreach (var correlativo in new[] { 1, 99, 147, 9999, 10000 })
                {
                    var codigo = OrderCode.Componer(etiqueta, anio, correlativo);
                    Assert.True(OrderCode.ReconocerForma(codigo), codigo);
                }
            }
        }
    }

    [Fact]
    public void Una_etiqueta_con_guiones_no_rompe_el_reconocimiento()
    {
        Assert.True(OrderCode.ReconocerForma(OrderCode.Componer("SUC-NORTE-2", 2026, 1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("P-2026")]
    [InlineData("P20260147")]
    [InlineData("-2026-0147")]
    [InlineData("P-2026-147")]
    [InlineData("P-2026-0147 ")]
    [InlineData(" P-2026-0147")]
    [InlineData("P-2026-0000")]
    [InlineData("P-dosmil-0147")]
    [InlineData("P-2026-014X")]
    public void Lo_que_el_formato_no_compone_no_se_reconoce(string? texto)
    {
        Assert.False(OrderCode.ReconocerForma(texto));
    }

    [Fact]
    public void Los_digitos_indicos_no_son_el_correlativo_de_ninguna_serie()
    {
        // char.IsDigit los aceptaría; la serie los emite jamás, porque se compone
        // con InvariantCulture. Si esto pasara, un código imposible llegaría a la base.
        Assert.False(OrderCode.ReconocerForma("P-2026-٠١٢٣"));
    }

    // ---------- qué no merece ni una consulta ----------

    [Fact]
    public void Una_declaracion_sin_nada_autoritativo_no_llega_a_ser_una_busqueda()
    {
        var plan = OrderCorrelationPolicy.Planear(new OrderCorrelationRequest(null, null));

        Assert.False(plan.DebeBuscar);
        var respuesta = OrderCorrelationPolicy.Juzgar(plan, null);
        Assert.Equal(OrderCorrelationOutcome.NotAuthoritative, respuesta.Outcome);
        Assert.Equal(CandidateSources.RatifiedRules, respuesta.Source);
    }

    [Fact]
    public void La_referencia_del_proveedor_sin_verificar_no_correlaciona_nada()
    {
        // Es el hueco registrado: M03 no tiene columna de proveedor y la referencia de
        // un pago manual es lo que el cliente dictó. No se usa para decidir.
        var plan = OrderCorrelationPolicy.Planear(
            new OrderCorrelationRequest(null, null) { UnverifiedProviderReference = Codigo });

        Assert.False(plan.DebeBuscar);
        Assert.Equal(
            OrderCorrelationOutcome.NotAuthoritative,
            OrderCorrelationPolicy.Juzgar(plan, null).Outcome);
    }

    [Fact]
    public void Una_forma_que_el_formato_no_produce_se_rechaza_sin_leer_la_base()
    {
        var plan = OrderCorrelationPolicy.Planear(new OrderCorrelationRequest("pedido 147", null));

        Assert.False(plan.DebeBuscar);
        var respuesta = OrderCorrelationPolicy.Juzgar(plan, null);
        Assert.Equal(OrderCorrelationOutcome.MalformedReference, respuesta.Outcome);
        Assert.Equal(CandidateSources.RatifiedRules, respuesta.Source);
    }

    [Fact]
    public void Un_identificador_vacio_es_una_declaracion_mal_formada_no_un_pedido_que_falta()
    {
        var plan = OrderCorrelationPolicy.Planear(new OrderCorrelationRequest(null, Guid.Empty));

        Assert.False(plan.DebeBuscar);
        Assert.Equal(
            OrderCorrelationOutcome.MalformedReference,
            OrderCorrelationPolicy.Juzgar(plan, null).Outcome);
    }

    [Fact]
    public void Un_plan_que_no_debia_buscar_conserva_su_respuesta_aunque_vuelva_una_fila()
    {
        var plan = OrderCorrelationPolicy.Planear(new OrderCorrelationRequest(null, null));

        var respuesta = OrderCorrelationPolicy.Juzgar(plan, new OrderIdentityRecord(Pedido, Codigo));

        Assert.Equal(OrderCorrelationOutcome.NotAuthoritative, respuesta.Outcome);
        Assert.Null(respuesta.OrderId);
    }

    // ---------- el recorte sí, el plegado de mayúsculas no ----------

    [Fact]
    public void La_envoltura_en_blanco_se_recorta_porque_ningun_codigo_guardado_la_tiene()
    {
        var plan = OrderCorrelationPolicy.Planear(new OrderCorrelationRequest($"  {Codigo}  ", null));

        Assert.True(plan.DebeBuscar);
        Assert.Equal(Codigo, plan.CanonicalOrderCode);
    }

    [Fact]
    public void Las_mayusculas_no_se_pliegan_porque_la_columna_no_las_pliega()
    {
        var plan = OrderCorrelationPolicy.Planear(new OrderCorrelationRequest("p-2026-0147", null));

        Assert.True(plan.DebeBuscar);
        Assert.Equal("p-2026-0147", plan.CanonicalOrderCode);
    }

    [Fact]
    public void Una_fila_que_solo_coincide_en_minusculas_no_resuelve_el_pedido()
    {
        // Si la base devolviera una fila plegada por una colación inesperada, no se
        // resuelve nada: es justo el caso en que un cobro se atribuye a otro pedido.
        var plan = OrderCorrelationPolicy.Planear(new OrderCorrelationRequest("p-2026-0147", null));

        var respuesta = OrderCorrelationPolicy.Juzgar(plan, new OrderIdentityRecord(Pedido, Codigo));

        Assert.Equal(OrderCorrelationOutcome.DivergentDeclaration, respuesta.Outcome);
        Assert.Null(respuesta.OrderId);
    }

    // ---------- lo que vuelve de la base ----------

    [Fact]
    public void Un_codigo_valido_sin_fila_es_una_referencia_desconocida_no_un_fallo()
    {
        var plan = OrderCorrelationPolicy.Planear(new OrderCorrelationRequest(Codigo, null));

        var respuesta = OrderCorrelationPolicy.Juzgar(plan, null);

        Assert.Equal(OrderCorrelationOutcome.UnknownReference, respuesta.Outcome);
        Assert.Equal(CandidateSources.CurrentDatabase, respuesta.Source);
        Assert.Null(respuesta.OrderId);
    }

    [Fact]
    public void Un_codigo_que_existe_resuelve_el_pedido_y_devuelve_su_codigo_guardado()
    {
        var plan = OrderCorrelationPolicy.Planear(new OrderCorrelationRequest(Codigo, null));

        var respuesta = OrderCorrelationPolicy.Juzgar(plan, new OrderIdentityRecord(Pedido, Codigo));

        Assert.Equal(OrderCorrelationOutcome.Resolved, respuesta.Outcome);
        Assert.Equal(Pedido, respuesta.OrderId);
        Assert.Equal(Codigo, respuesta.OrderCode);
        Assert.Equal(CandidateSources.CurrentDatabase, respuesta.Source);
    }

    [Fact]
    public void Un_identificador_que_existe_resuelve_sin_necesitar_el_codigo()
    {
        var plan = OrderCorrelationPolicy.Planear(new OrderCorrelationRequest(null, Pedido));

        Assert.True(plan.DebeBuscar);
        Assert.Null(plan.CanonicalOrderCode);

        var respuesta = OrderCorrelationPolicy.Juzgar(plan, new OrderIdentityRecord(Pedido, Codigo));

        Assert.Equal(OrderCorrelationOutcome.Resolved, respuesta.Outcome);
        Assert.Equal(Pedido, respuesta.OrderId);
    }

    [Fact]
    public void Codigo_e_identificador_que_senalan_a_pedidos_distintos_no_se_desempatan()
    {
        var plan = OrderCorrelationPolicy.Planear(new OrderCorrelationRequest(Codigo, Otro));

        var respuesta = OrderCorrelationPolicy.Juzgar(plan, new OrderIdentityRecord(Pedido, Codigo));

        Assert.Equal(OrderCorrelationOutcome.DivergentDeclaration, respuesta.Outcome);
        Assert.Null(respuesta.OrderId);
        Assert.Null(respuesta.OrderCode);
    }

    [Fact]
    public void Codigo_e_identificador_concordantes_resuelven_el_mismo_pedido()
    {
        var plan = OrderCorrelationPolicy.Planear(new OrderCorrelationRequest(Codigo, Pedido));

        var respuesta = OrderCorrelationPolicy.Juzgar(plan, new OrderIdentityRecord(Pedido, Codigo));

        Assert.Equal(OrderCorrelationOutcome.Resolved, respuesta.Outcome);
        Assert.Equal(Pedido, respuesta.OrderId);
    }

    // ---------- la frontera con las otras dos preguntas ----------

    [Fact]
    public void Resolver_un_pedido_no_dice_que_pueda_cobrarse()
    {
        // Correlacionar es identidad. La pagabilidad la responde la otra política, y
        // hoy no devuelve Eligible para ningún pedido.
        var correlacion = OrderCorrelationPolicy.Juzgar(
            OrderCorrelationPolicy.Planear(new OrderCorrelationRequest(Codigo, null)),
            new OrderIdentityRecord(Pedido, Codigo));

        Assert.Equal(OrderCorrelationOutcome.Resolved, correlacion.Outcome);

        var pagabilidad = ExternalCommercePolicy.EvaluarPagabilidad(
            Pedido,
            new OrderCommercialSnapshot(Pedido, Codigo, OrderStatus.PendingPayment, 100m, 0m, true));

        Assert.NotEqual(PayableOutcome.Eligible, pagabilidad.Outcome);
    }

    [Fact]
    public void La_identidad_del_pedido_y_la_de_la_liquidacion_siguen_siendo_dos_cosas()
    {
        // El resultado de correlacionar no lleva identidad de liquidación, y el de
        // liquidar no lleva pedido: fundirlos haría que resolver pareciera cobrar.
        var correlacion = OrderCorrelationPolicy.Juzgar(
            OrderCorrelationPolicy.Planear(new OrderCorrelationRequest(Codigo, null)),
            new OrderIdentityRecord(Pedido, Codigo));

        var liquidacion = ExternalCommercePolicy.EvaluarLiquidacion(
            new ExternalSettlementCandidateEvent(
                "proveedor", "evt-1", "liq-1", Pedido, 100m, "PEN", "evidencia"),
            null);

        Assert.Equal(Pedido, correlacion.OrderId);
        Assert.Null(liquidacion.DurableEffectReference);
        Assert.NotEqual(ExternalSettlementOutcome.AppliedDurable, liquidacion.Outcome);
    }
}
