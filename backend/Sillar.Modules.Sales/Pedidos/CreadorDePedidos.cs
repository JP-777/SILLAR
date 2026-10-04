using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Catalog.Contracts;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Domain;
using Sillar.Modules.Sales.Dtos;

namespace Sillar.Modules.Sales.Pedidos;

/// <summary>
/// Crea un pedido: la operación donde viven todas las guardas de la compra.
/// </summary>
/// <remarks>
/// <para>
/// <b>La guarda está aquí, no en quien llama.</b> El carrito, la pantalla o el
/// endpoint pueden anticipar estas comprobaciones para responder rápido, pero
/// <b>ninguno las constituye</b>: ponerlas en el llamador protegería de ese llamador,
/// y aquí protegen de todos, incluidos los que todavía no existen.
/// </para>
/// <para>
/// <b>Se recomprueba todo en la operación</b>, aunque el carrito ya lo mirara: el
/// cliente sigue siendo válido, cada variante sigue siendo vendible, y el precio es el
/// que M01 dice <b>ahora</b>. Entre ver el carrito y confirmar puede pasar un día.
/// </para>
/// <para>
/// <b>El orden de los pasos no es casual.</b> Primero el cliente, porque si no puede
/// comprar no hace falta preguntar por ningún artículo; después los artículos, uno a
/// uno y parando en el primero que falle; y <b>el número al final</b>, dentro de la
/// transacción, cuando ya nada puede impedir el pedido.
/// </para>
/// </remarks>
internal sealed class CreadorDePedidos(
    SalesDbContext database,
    CongeladorDeCliente clientes,
    ICatalogService catalogo,
    OrderCodeAllocator numerador,
    ISettingsReader ajustes,
    TimeProvider reloj)
{
    /// <summary>Crea un pedido de recojo en tienda para el cliente indicado.</summary>
    public async Task<ResultadoDeCreacion> CrearAsync(
        Guid customerId,
        IReadOnlyList<LineaPedida> lineas,
        CancellationToken cancellationToken)
    {
        if (lineas.Count == 0)
        {
            return ResultadoDeCreacion.Rechaza(MotivoDeNoCreacion.SinLineas);
        }

        // 1 · El cliente. D1: instantánea sin dirección y R-07.
        var cliente = await clientes.ObtenerParaRecojoAsync(customerId, cancellationToken);

        if (!cliente.Permitido)
        {
            return ResultadoDeCreacion.Rechaza(cliente.Motivo switch
            {
                MotivoDeRechazo.CorreoSinVerificar => MotivoDeNoCreacion.CorreoSinVerificar,
                _ => MotivoDeNoCreacion.LaCuentaNoPuedeComprar
            });
        }

        // 2 · Los artículos. M01 es la autoridad de existencia y de precio.
        var congeladas = new List<(ItemSnapshot Item, int Cantidad)>(lineas.Count);

        foreach (var pedida in lineas)
        {
            if (pedida.Quantity < 1)
            {
                return ResultadoDeCreacion.Rechaza(
                    MotivoDeNoCreacion.CantidadNoPositiva, pedida.ItemId);
            }

            // Se pregunta por la vendibilidad ANTES de por el precio, y con el método
            // que existe para eso: ObtenerItemAsync devuelve también las variantes de
            // baja —su propio contrato lo dice— así que el snapshot por sí solo no
            // acredita que se pueda vender.
            if (!await catalogo.ItemExisteYEstaActivoAsync(pedida.ItemId, cancellationToken))
            {
                return ResultadoDeCreacion.Rechaza(
                    MotivoDeNoCreacion.ItemNoVendible, pedida.ItemId);
            }

            var item = await catalogo.ObtenerItemAsync(pedida.ItemId, cancellationToken);

            if (item is null)
            {
                return ResultadoDeCreacion.Rechaza(
                    MotivoDeNoCreacion.ItemNoVendible, pedida.ItemId);
            }

            // Nulo es «a consultar» y queda fuera del carrito y del pago (R-11).
            // CERO NO: cero es gratis y se vende. La comprobación es «is null» y no
            // una de «falsy», porque confundirlas ya mordió una vez en la tarjeta
            // pública del catálogo.
            if (item.Price is null)
            {
                return ResultadoDeCreacion.Rechaza(
                    MotivoDeNoCreacion.ItemAConsultar, pedida.ItemId);
            }

            congeladas.Add((item, pedida.Quantity));
        }

        // 3 · El total lo calcula el servidor con los precios de M01. R-15: la suma de
        // las líneas y nada más — sin costo de entrega, sin tarifas.
        var total = congeladas.Sum(l => l.Item.Price!.Value * l.Cantidad);

        var ahora = reloj.GetUtcNow();
        var horas = ajustes.Get<int>(SalesSettingsKeys.PaymentDueHours);
        var plazo = horas > 0 ? horas : SalesSettingsKeys.PaymentDueHoursDefault;

        // 4 · La transacción. Dentro van el pedido, sus líneas, su primer asiento de
        // estado y el número: o está todo o no está nada. Se une a la del llamador si
        // ya hay una — ver TransaccionDeOperacion, que existe porque este defecto
        // apareció dos veces.
        await using var transaccion =
            await TransaccionDeOperacion.AbrirSiHaceFaltaAsync(database, cancellationToken);

        var pedido = new Order
        {
            OrderCode = string.Empty,   // lo pone el numerador, al final
            CustomerFullName = cliente.Instantanea!.FullName,
            CustomerEmail = cliente.Instantanea.Email,
            Status = OrderStatus.PendingPayment,
            PaymentDueAt = ahora.AddHours(plazo),
            TotalAmount = total
        };

        cliente.Instantanea.Congelar(pedido);

        foreach (var (item, cantidad) in congeladas)
        {
            database.OrderLines.Add(new OrderLine
            {
                OrderId = pedido.OrderId,
                ItemId = item.ItemId,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                VariantValue = item.VariantValue,
                SaleUnit = item.SaleUnit,
                Quantity = cantidad,
                UnitPrice = item.Price!.Value
            });
        }

        // El primer asiento: el pedido nace. from_status nulo porque no venía de
        // ningún estado, y los tres datos de atribución también nulos — lo creó el
        // cliente, que no es personal, y R-14 solo atribuye actuaciones del personal.
        database.OrderStatusChanges.Add(new OrderStatusChange
        {
            OrderId = pedido.OrderId,
            FromStatus = null,
            ToStatus = OrderStatus.PendingPayment,
            ChangedAt = ahora,
            ChangedBy = null,
            ChangedByAdminUserLocalId = null,
            ChangedByAdminUserHomeNode = null
        });

        // 5 · El número, AL FINAL y dentro de esta transacción. Pedirlo antes gastaría
        // uno en cada intento que no llega a confirmarse; el numerador además se niega
        // a trabajar fuera de transacción, así que esto no es una convención.
        pedido.OrderCode = await numerador.SiguienteAsync(cancellationToken);
        database.Orders.Add(pedido);

        await database.SaveChangesAsync(cancellationToken);
        await transaccion.CompletarAsync(cancellationToken);

        return new ResultadoDeCreacion(
            pedido.OrderCode, pedido.TotalAmount, pedido.PaymentDueAt, MotivoDeNoCreacion.Ninguno);
    }
}
