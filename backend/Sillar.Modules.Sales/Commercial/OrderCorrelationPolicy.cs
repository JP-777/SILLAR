using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;

namespace Sillar.Modules.Sales.Commercial;

/// <summary>
/// La identidad de un pedido, y nada más.
/// </summary>
/// <remarks>
/// <b>Deliberadamente no lleva estado, total ni baja lógica.</b> Correlacionar es
/// decir a qué pedido se refiere algo; si esta fotografía trajera el estado, la
/// política de correlación podría empezar a opinar sobre si el pedido puede cobrarse,
/// que es la pregunta de <see cref="ExternalCommercePolicy.EvaluarPagabilidad"/> y
/// tiene su propia respuesta.
/// </remarks>
/// <param name="OrderId">El pedido.</param>
/// <param name="OrderCode">Su código visible, tal como está guardado.</param>
public sealed record OrderIdentityRecord(Guid OrderId, string OrderCode);

/// <summary>
/// Qué hay que buscar, o por qué no hay nada que buscar.
/// </summary>
/// <remarks>
/// Separar el plan de la lectura es lo que permite probar sin base <b>las dos cosas
/// que de verdad deciden</b>: qué declaraciones no merecen ni una consulta, y cómo se
/// juzga lo que vuelve.
/// </remarks>
/// <param name="CanonicalOrderCode">El código ya recortado, si se buscará por código.</param>
/// <param name="DeclaredOrderId">El identificador declarado, si vino.</param>
/// <param name="Rejection">La respuesta ya decidida, si no hay nada que buscar.</param>
public sealed record OrderCorrelationPlan(
    string? CanonicalOrderCode,
    Guid? DeclaredOrderId,
    OrderCorrelationCandidate? Rejection)
{
    /// <summary>Si merece una lectura de la base.</summary>
    public bool DebeBuscar => Rejection is null;
}

/// <summary>
/// Reglas puras de correlación entre una declaración externa y un pedido de M03.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sin base y sin reloj</b>, como la política de transiciones: la decisión se puede
/// provocar en milisegundos y la lectura queda reducida a traer una fila.
/// </para>
/// <para>
/// <b>La correlación auténtica es el código visible.</b> Es único
/// (<c>uq_orders_order_code</c>), está guardado sin colación —así que la base compara
/// byte a byte— y lo compone un formato ratificado. No hay otra.
/// </para>
/// </remarks>
public static class OrderCorrelationPolicy
{
    /// <summary>
    /// Decide si la declaración da para buscar algo, y qué.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Rechaza antes de leer</b> lo que no puede existir: una forma que el formato
    /// ratificado no produce nunca no está en la base, y preguntarlo solo añade una
    /// consulta y la duda de si faltaba la fila o sobraba el texto.
    /// </para>
    /// <para>
    /// <b>La referencia sin verificar no entra en el plan.</b> Es texto que escribió
    /// una persona; mientras no haya una fuente que la verifique, usarla para decidir
    /// a qué pedido pertenece un cobro sería adivinar.
    /// </para>
    /// <para>
    /// <b>Se recorta la envoltura y no se pliegan las mayúsculas.</b> Lo primero es
    /// inocuo —<c>Componer</c> no deja espacios en los extremos, así que ningún código
    /// guardado los tiene—; lo segundo cambiaría el conjunto de pedidos que una
    /// declaración puede alcanzar, porque la columna distingue mayúsculas.
    /// </para>
    /// </remarks>
    public static OrderCorrelationPlan Planear(OrderCorrelationRequest solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var declarado = solicitud.DeclaredOrderCode?.Trim();

        if (!string.IsNullOrEmpty(declarado))
        {
            return OrderCode.ReconocerForma(declarado)
                ? new OrderCorrelationPlan(declarado, solicitud.DeclaredOrderId, null)
                : SinBuscar(OrderCorrelationOutcome.MalformedReference);
        }

        if (solicitud.DeclaredOrderId is { } identificador)
        {
            // Guid.Empty no es un uuid v7 y ninguna fila lo lleva (ADR-016): es una
            // declaración mal formada, no un pedido que falte.
            return identificador == Guid.Empty
                ? SinBuscar(OrderCorrelationOutcome.MalformedReference)
                : new OrderCorrelationPlan(null, identificador, null);
        }

        return SinBuscar(OrderCorrelationOutcome.NotAuthoritative);
    }

    /// <summary>
    /// Juzga lo que volvió de la base.
    /// </summary>
    /// <remarks>
    /// <b>Si vinieron código e identificador, los compara.</b> Devolver el pedido del
    /// código y callar que el identificador declaraba otro es exactamente cómo un cobro
    /// termina atribuido al pedido equivocado sin que nadie vea nada raro.
    /// </remarks>
    /// <param name="plan">Lo que se planeó buscar.</param>
    /// <param name="fila">La identidad encontrada, o <c>null</c> si no había.</param>
    public static OrderCorrelationCandidate Juzgar(OrderCorrelationPlan plan, OrderIdentityRecord? fila)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.Rejection is { } yaDecidido)
        {
            // Defensivo: un plan que no debía buscar conserva su respuesta aunque
            // alguien haya ido a la base de todas formas.
            return yaDecidido;
        }

        if (fila is null)
        {
            return new OrderCorrelationCandidate(
                OrderCorrelationOutcome.UnknownReference, null, null,
                CandidateSources.CurrentDatabase);
        }

        if (plan.CanonicalOrderCode is { } codigo
            && !string.Equals(fila.OrderCode, codigo, StringComparison.Ordinal))
        {
            // La fila no es la que se pidió. Puede ser una lectura plegada por una
            // colación inesperada, y en ese caso no se resuelve nada.
            return new OrderCorrelationCandidate(
                OrderCorrelationOutcome.DivergentDeclaration, null, null,
                CandidateSources.CurrentDatabase);
        }

        if (plan.DeclaredOrderId is { } declarado && declarado != fila.OrderId)
        {
            return new OrderCorrelationCandidate(
                OrderCorrelationOutcome.DivergentDeclaration, null, null,
                CandidateSources.CurrentDatabase);
        }

        return new OrderCorrelationCandidate(
            OrderCorrelationOutcome.Resolved, fila.OrderId, fila.OrderCode,
            CandidateSources.CurrentDatabase);
    }

    /// <remarks>
    /// <b>La fuente es la regla pura</b>, no la base: nadie leyó nada para responder
    /// esto, y decir lo contrario atribuiría a <c>sales</c> una lectura que no ocurrió.
    /// </remarks>
    private static OrderCorrelationPlan SinBuscar(OrderCorrelationOutcome desenlace)
        => new(null, null, new OrderCorrelationCandidate(
            desenlace, null, null, CandidateSources.RatifiedRules));
}
