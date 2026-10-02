using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Domain;

namespace Sillar.Modules.Sales.Pedidos;

/// <summary>
/// Cuántos pedidos vencieron y qué pasó con ellos.
/// </summary>
/// <param name="Vencidos">Cuántos pasaron a Vencido.</param>
/// <param name="Codigos">Sus códigos visibles, para el aviso al personal.</param>
public sealed record ResultadoDeVencimiento(int Vencidos, IReadOnlyList<string> Codigos);

/// <summary>
/// Pasa a <b>Vencido</b> los pedidos cuyo plazo para pagar expiró.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es una actuación del sistema, y se nota en la fila:</b> el asiento del historial
/// lleva los <b>tres</b> datos de atribución en <c>NULL</c>. No hay trabajador
/// «Sistema», ni guion, ni identificador cero — cualquiera de los tres convertiría una
/// ausencia honesta en una persona que no existe, y dentro de un año nadie la
/// distinguiría de una real (R-14).
/// </para>
/// <para>
/// <b>Vencer no es cancelar y no libera nada.</b> El pedido sigue existiendo, deja de
/// estar garantizado y <b>no se cancela por este hecho</b>. Y nada se libera porque
/// nada se apartó: SILLAR WEB v1 no reserva existencias, así que las 48 horas eran
/// plazo para pagar.
/// </para>
/// <para>
/// <b>Qué decide y qué no.</b> A quién le toca vencer lo decide
/// <see cref="OrderTransitionPolicy.VencerPlazo"/>, que es pura; esta clase solo lo
/// aplica. Por eso la regla se puede provocar sin base de datos y aquí no se repite.
/// </para>
/// </remarks>
internal sealed class VencimientoDePlazos(SalesDbContext database, TimeProvider clock)
{
    /// <summary>
    /// Vence los plazos cumplidos. Idempotente: correrlo dos veces no cambia nada.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La consulta trae solo los que <b>están pendientes de pago</b> y cuyo plazo ya
    /// pasó. Que el filtro de estado esté en la consulta y además en la política no es
    /// redundancia inútil: la consulta evita traer lo que no toca, y la política es la
    /// que decide — si algún día discrepan, manda la política y la prueba lo dice.
    /// </para>
    /// <para>
    /// Todo en una transacción: o vencen todos los de esta pasada con su asiento, o
    /// ninguno. Un pedido que cambiara de estado sin dejar asiento sería un pedido sin
    /// historia.
    /// </para>
    /// <para>
    /// <b>Y se une a la transacción de quien llama si ya hay una abierta</b>, en vez
    /// de abrir la suya a ciegas. La primera versión la abría siempre y por eso no se
    /// podía componer: PostgreSQL no admite transacciones anidadas, así que una
    /// operación que ya viniera dentro de otra reventaba. <b>Lo destapó la propia
    /// prueba</b>, que envuelve cada caso en una transacción para no dejar filas.
    /// </para>
    /// <para>
    /// El criterio es el que corresponde: <b>la atomicidad la garantiza quien abre la
    /// transacción</b>. Si la abrió el llamador, es suya y aquí no se confirma; si no
    /// hay ninguna, esta operación la abre y la confirma. Nótese que es lo contrario
    /// de <c>OrderCodeAllocator</c>, que <b>exige</b> una abierta — y la diferencia no
    /// es un descuido: numerar fuera de la transacción del pedido dejaría un hueco en
    /// la serie, mientras vencer plazos es una operación completa por sí misma.
    /// </para>
    /// </remarks>
    public async Task<ResultadoDeVencimiento> VencerLoCumplidoAsync(
        CancellationToken cancellationToken)
    {
        var ahora = clock.GetUtcNow();

        var pendientes = await database.Orders
            .Where(o => o.IsActive
                        && o.Status == OrderStatus.PendingPayment
                        && o.PaymentDueAt <= ahora)
            .ToListAsync(cancellationToken);

        if (pendientes.Count == 0)
        {
            return new ResultadoDeVencimiento(0, []);
        }

        // Si el llamador ya tiene una transacción, es suya: no se abre otra ni se
        // confirma la ajena.
        var propia = database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var codigos = new List<string>(pendientes.Count);

        foreach (var pedido in pendientes)
        {
            var transicion = OrderTransitionPolicy.VencerPlazo(pedido.Status);

            // La política es la autoridad. Si dijera que este no vence, no vence.
            if (transicion.EstadoDestino is not { } destino)
            {
                continue;
            }

            database.OrderStatusChanges.Add(new OrderStatusChange
            {
                OrderId = pedido.OrderId,
                FromStatus = pedido.Status,
                ToStatus = destino,
                ChangedAt = ahora,

                // Los tres en NULL: lo hizo el sistema. El CHECK
                // ck_order_status_changes_atribucion_completa lo exige junto.
                ChangedBy = null,
                ChangedByAdminUserLocalId = null,
                ChangedByAdminUserHomeNode = null
            });

            pedido.Status = destino;
            codigos.Add(pedido.OrderCode);
        }

        await database.SaveChangesAsync(cancellationToken);

        if (propia is not null)
        {
            await propia.CommitAsync(cancellationToken);
            await propia.DisposeAsync();
        }

        return new ResultadoDeVencimiento(codigos.Count, codigos);
    }
}
