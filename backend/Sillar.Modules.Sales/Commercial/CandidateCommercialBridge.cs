using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;

namespace Sillar.Modules.Sales.Commercial;

/// <summary>
/// Lee la verdad comercial que M03 ya tiene y consulta la política.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí solo hay entrada y salida.</b> Las decisiones viven en
/// <see cref="ExternalCommercePolicy"/>, que no toca la base: es lo que permite
/// probar las reglas sin PostgreSQL, como ya se hace con las transiciones.
/// </para>
/// <para>
/// <b>No inventa la moneda</b> —M03 no tiene columna para ella— y <b>no deduce una
/// liquidación externa de un pago manual</b>: son dos hechos distintos y el segundo
/// no implica al primero.
/// </para>
/// </remarks>
internal sealed class CandidatePayableOrderReader(SalesDbContext database) : IPayableOrderCandidate
{
    /// <inheritdoc />
    public async Task<PayableOrderCandidate> EvaluateAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        // Proyección, no entidad: nada de cliente, líneas ni contacto sale de aquí.
        var pedido = await database.Orders
            .AsNoTracking()
            .Where(o => o.OrderId == orderId)
            .Select(o => new
            {
                o.OrderId,
                o.OrderCode,
                o.Status,
                o.TotalAmount,
                o.IsActive,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (pedido is null)
        {
            return ExternalCommercePolicy.EvaluarPagabilidad(orderId, null);
        }

        // Lo ya cobrado a mano. Va aparte del total porque el pendiente no es el
        // total, y confundirlos fraccionaría un cobro sin que nadie lo decidiera.
        //
        // La suma se proyecta a decimal? y el cero se pone aquí, sin depender de que
        // la traducción envuelva SUM en COALESCE: un pedido sin pagos debe dar cero,
        // y eso no se puede comprobar sin base, así que no se da por supuesto.
        var cobradoAMano = await database.OrderPayments
            .AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        return ExternalCommercePolicy.EvaluarPagabilidad(
            orderId,
            new OrderCommercialSnapshot(
                pedido.OrderId,
                pedido.OrderCode,
                pedido.Status,
                pedido.TotalAmount,
                cobradoAMano,
                pedido.IsActive));
    }
}

/// <summary>
/// Memoria de propuestas vistas, para reconocer un reintento.
/// </summary>
/// <remarks>
/// <b>No es durabilidad y no debe confundirse con ella.</b> Lo que recuerda vive en
/// este proceso y desaparece al reiniciar, así que jamás puede fundamentar
/// <see cref="ExternalSettlementOutcome.AppliedDurable"/>. Existe para que el replay
/// divergente se pueda detectar y probar sin base de datos.
/// </remarks>
public interface ISettlementCandidateJournal
{
    /// <summary>Lo ya visto con esa identidad de evento, o <c>null</c>.</summary>
    SettlementCandidateRecord? Find(string provider, string providerEventId);

    /// <summary>Recuerda la propuesta. Recordar no es aplicar.</summary>
    void Remember(SettlementCandidateRecord record);
}

/// <summary>
/// Memoria por proceso, explícitamente volátil.
/// </summary>
/// <remarks>
/// La clave es el par proveedor + identidad de evento, comparado de forma
/// <b>ordinal</b>: dos identidades que solo difieren en mayúsculas son distintas, y
/// fundirlas convertiría un hecho real en un duplicado.
/// </remarks>
internal sealed class InProcessSettlementCandidateJournal : ISettlementCandidateJournal
{
    private readonly Dictionary<(string Provider, string ProviderEventId), SettlementCandidateRecord> vistos
        = new();

    private readonly Lock cerrojo = new();

    /// <inheritdoc />
    public SettlementCandidateRecord? Find(string provider, string providerEventId)
    {
        lock (cerrojo)
        {
            return vistos.GetValueOrDefault((provider, providerEventId));
        }
    }

    /// <inheritdoc />
    public void Remember(SettlementCandidateRecord record)
    {
        lock (cerrojo)
        {
            vistos[(record.Provider, record.ProviderEventId)] = record;
        }
    }
}

/// <summary>
/// Recibe la propuesta de liquidación y responde lo que M03 puede responder.
/// </summary>
/// <remarks>
/// <para>
/// <b>No materializa ningún efecto en FASE A.</b> No abre transacción, no escribe y
/// no devuelve referencia de efecto durable. Un ACK técnico de bandeja <b>no</b> es
/// una aplicación, y este adaptador está escrito para que no puedan confundirse.
/// </para>
/// <para>
/// <b>No fabrica a la persona que falta.</b> La atribución humana de un pago son tres
/// datos congelados de una cuenta real; un pago de proveedor no los tiene y escribir
/// un nombre de sistema inventaría un trabajador. Mientras la nulidad condicionada no
/// esté ratificada, la propuesta se queda en pendiente.
/// </para>
/// </remarks>
internal sealed class CandidateExternalSettlementApplicator(
    ISettlementCandidateJournal journal) : IExternalSettlementCandidate
{
    /// <inheritdoc />
    public Task<ExternalSettlementCandidateResult> TryApplyAsync(
        ExternalSettlementCandidateEvent candidate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var previo = string.IsNullOrWhiteSpace(candidate.Provider)
            || string.IsNullOrWhiteSpace(candidate.ProviderEventId)
                ? null
                : journal.Find(candidate.Provider, candidate.ProviderEventId);

        var resultado = ExternalCommercePolicy.EvaluarLiquidacion(candidate, previo);

        // Solo se recuerda lo que pasó la autenticación y las comprobaciones de
        // forma: recordar un evento rechazado haría que el siguiente intento
        // legítimo con esa identidad pareciera un reintento divergente.
        if (previo is null && resultado.Outcome is not ExternalSettlementOutcome.Rejected)
        {
            journal.Remember(new SettlementCandidateRecord(
                candidate.Provider,
                candidate.ProviderEventId,
                candidate.ExternalSettlementId,
                candidate.OrderId,
                candidate.Amount,
                candidate.Currency));
        }

        return Task.FromResult(resultado);
    }
}

/// <summary>
/// Resuelve a qué pedido se refiere una declaración externa, leyendo la única
/// correlación auténtica que M03 tiene.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí solo hay entrada y salida.</b> Qué merece una consulta y cómo se juzga lo
/// que vuelve está en <see cref="OrderCorrelationPolicy"/>, que no toca la base.
/// </para>
/// <para>
/// <b>La comparación la hace la base, y es exacta</b> porque <c>order_code</c> se
/// declaró sin colación. No se usa <c>ILIKE</c> ni se normaliza en SQL: plegar
/// mayúsculas ampliaría en silencio el conjunto de pedidos alcanzables por una
/// declaración ajena.
/// </para>
/// </remarks>
internal sealed class CandidateOrderCorrelationReader(SalesDbContext database) : IOrderCorrelationCandidate
{
    /// <inheritdoc />
    public async Task<OrderCorrelationCandidate> ResolveAsync(
        OrderCorrelationRequest request,
        CancellationToken cancellationToken)
    {
        var plan = OrderCorrelationPolicy.Planear(request);

        if (!plan.DebeBuscar)
        {
            // Ni una consulta: la respuesta ya estaba decidida por la forma del dato.
            return OrderCorrelationPolicy.Juzgar(plan, null);
        }

        // Proyección a la identidad y nada más. Lo que no se trae no puede influir.
        var consulta = database.Orders.AsNoTracking();

        consulta = plan.CanonicalOrderCode is { } codigo
            ? consulta.Where(o => o.OrderCode == codigo)
            : consulta.Where(o => o.OrderId == plan.DeclaredOrderId!.Value);

        // SingleOrDefault y no First: order_code es único por uq_orders_order_code y
        // order_id es la clave primaria. Si alguna vez vuelven dos filas, eso es una
        // avería del esquema y debe doler, no quedar disimulada en la primera.
        var fila = await consulta
            .Select(o => new OrderIdentityRecord(o.OrderId, o.OrderCode))
            .SingleOrDefaultAsync(cancellationToken);

        return OrderCorrelationPolicy.Juzgar(plan, fila);
    }
}

/// <summary>
/// Registro opcional. <b>Deliberadamente no se llama desde <c>SalesModule</c>.</b>
/// </summary>
/// <remarks>
/// Mientras sea candidato, quien lo quiera lo pide; si se registrara en el módulo,
/// un contrato sin ratificar quedaría disponible para cualquiera por descuido.
/// </remarks>
public static class CandidateSalesCommercialRegistration
{
    /// <summary>Registra los tres candidatos y la memoria volátil de reintentos.</summary>
    public static IServiceCollection AddCandidateSalesCommerce(this IServiceCollection services)
    {
        services.AddScoped<IPayableOrderCandidate, CandidatePayableOrderReader>();
        services.AddScoped<IExternalSettlementCandidate, CandidateExternalSettlementApplicator>();
        services.AddScoped<IOrderCorrelationCandidate, CandidateOrderCorrelationReader>();

        // Singleton porque una memoria de reintentos por petición no reconocería
        // ninguno. Sigue siendo volátil, y eso no la convierte en durable.
        services.AddSingleton<ISettlementCandidateJournal, InProcessSettlementCandidateJournal>();
        return services;
    }
}
