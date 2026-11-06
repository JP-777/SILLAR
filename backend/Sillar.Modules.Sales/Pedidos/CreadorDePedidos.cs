using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
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
/// <para>
/// <b>Una sola conexión mientras se retiene el bloqueo de moneda</b> (R10-E13-B, D-1).
/// Desde que se toma el compartido hasta que se confirma o deshace, todo —la lectura
/// acreditada de CORE, el número y la inserción— va por la conexión y la transacción de
/// <see cref="SalesDbContext"/>. Ninguna llamada de ese tramo pide otra conexión al pool:
/// con un pool corto, cada pedido retendría una y esperaría otra, y se bloquearían entre
/// ellos.
/// </para>
/// <para>
/// <b>Nada de la configuración se lee en ese tramo</b> (R10-E13-D, R-1). El plazo de pago
/// y la etiqueta de serie se leen en el paso 3, antes de abrir la transacción: la caché de
/// configuración, si está invalidada, recarga con su propio ámbito y su propia conexión.
/// La etiqueta capturada se congela para esta operación y se le pasa al numerador.
/// </para>
/// </remarks>
internal sealed class CreadorDePedidos(
    SalesDbContext database,
    CongeladorDeCliente clientes,
    ICatalogService catalogo,
    OrderCodeAllocator numerador,
    ISettingsReader ajustes,
    IAutoridadDeMoneda monedas,
    TimeProvider reloj,
    ILogger<CreadorDePedidos> registro)
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

        // 0 · La moneda, antes que nada que salga de M03. Sin autoridad que la afirme
        // —o si lo que afirma no tiene forma ISO— no nace ningún pedido: un importe sin
        // unidad se replica y ya no se puede corregir. Cerrar en falso, a propósito.
        //
        // ESTA LECTURA SOLO SIRVE PARA RECHAZAR. Va antes de tocar la base de Ventas para
        // que una tienda sin moneda no abra transacciones ni bloqueos por nada (D20-A).
        // Usa la conexión de CORE, y Ventas todavía no retiene ninguna. La moneda que se
        // congela es la del paso 4b, leída en la transacción del pedido bajo el bloqueo:
        // entre esta lectura y aquella, CORE pudo cambiarla. «No se pudo leer» no se
        // presenta como «falta configurar la moneda».
        var previa = await monedas.MonedaDeVentaAsync(cancellationToken);

        if (previa.Estado != EstadoDeMonedaDeVenta.Acreditada || !CurrencyCode.TieneFormaIso(previa.Codigo))
        {
            return ResultadoDeCreacion.Rechaza(MotivoPor(previa.Estado));
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

        // 3b · La etiqueta de serie, AQUÍ y no al numerar (R-1). El numerador corre con el
        // bloqueo de moneda puesto, y leerla allí podía recargar la caché de configuración,
        // que abre su propio ámbito y pide otra conexión al pool mientras la del pedido
        // retiene el bloqueo. Se valida con la regla del numerador: sin etiqueta no se abre
        // transacción, no se toma el bloqueo y no se consume número. Queda congelada para
        // este pedido; un cambio de configuración posterior sirve al siguiente.
        var etiquetaDeSerie = numerador.EtiquetaValida(ajustes.Get(SalesSettingsKeys.OrderSeriesLabel));

        // 4 · La transacción. Dentro van la barrera y la lectura de moneda, el pedido,
        // sus líneas, su primer asiento de estado y el número: o está todo o no está
        // nada. Se une a la del llamador si ya hay una — ver TransaccionDeOperacion, que
        // existe porque este defecto apareció dos veces. Unida a la del llamador, la
        // barrera dura lo que dure esa transacción, y si algo de lo que sigue falla en
        // la base la deja abortada: el llamador tiene que deshacerla. Aquí nunca se
        // confirma ni se deshace una transacción ajena.
        //
        // La propia se abre en READ COMMITTED explícito: es el único aislamiento en que
        // la lectura de moneda ve lo que CORE confirmó mientras el bloqueo esperaba. Una
        // prestada con otro aislamiento declarado no sirve, y se rechaza sin tomar el
        // bloqueo. El aislamiento real lo vuelve a comprobar CORE en el servidor (4b).
        if (database.Database.CurrentTransaction is { } prestada
            && prestada.GetDbTransaction().IsolationLevel is not (IsolationLevel.ReadCommitted or IsolationLevel.Unspecified))
        {
            registro.LogError(AutoridadDeMonedaDeCore.CosturaFueraDeContrato,
                "La transacción del llamador está en {Aislamiento}: el pedido solo se crea en ReadCommitted. No nace ningún pedido.",
                prestada.GetDbTransaction().IsolationLevel);
            return ResultadoDeCreacion.Rechaza(MotivoDeNoCreacion.CosturaMonetariaDefectuosa);
        }

        TransaccionDeOperacion transaccion;
        try
        {
            transaccion = await TransaccionDeOperacion.AbrirSiHaceFaltaAsync(
                database, IsolationLevel.ReadCommitted, cancellationToken);
        }
        catch (Exception excepcion) when (FallosDeInfraestructura.EsTransitorio(excepcion) && !cancellationToken.IsCancellationRequested)
        {
            // Sin conexión, pool agotado o servidor que se cae: no se abrió nada. EF lo
            // entrega envuelto; ver FallosDeInfraestructura.
            registro.LogWarning(AutoridadDeMonedaDeCore.MonedaIlegible, excepcion,
                "No se pudo abrir la transacción del pedido; no nace ningún pedido.");
            return ResultadoDeCreacion.Rechaza(MotivoDeNoCreacion.ServicioNoDisponible);
        }

        await using var alSalir = transaccion;

        // 4a · La barrera de moneda, PRIMER bloqueo de la transacción: antes de leer la
        // moneda, antes de numerar y antes de insertar. Mientras CORE la cambia, esto
        // espera; mientras esto la tiene, CORE espera. Se suelta sola al confirmar o
        // deshacer. Ver BarreraMonetariaDeVentas y MonetaryAuthorityLock.
        try
        {
            await BarreraMonetariaDeVentas.AdquirirCompartidaAsync(database, cancellationToken);
        }
        catch (Exception excepcion) when (FallosDeInfraestructura.EsEsperaDelBloqueoAgotada(excepcion))
        {
            // La transacción quedó abortada; la propia se deshace al salir. El número
            // no se pidió, así que la serie no tiene hueco.
            return ResultadoDeCreacion.Rechaza(MotivoDeNoCreacion.MonedaEnCambio);
        }
        catch (Exception excepcion) when (FallosDeInfraestructura.EsTransitorio(excepcion) && !cancellationToken.IsCancellationRequested)
        {
            // La conexión se cortó o el servidor no puede atender: no es un cambio de
            // moneda, y no se sabe nada de la moneda.
            registro.LogWarning(AutoridadDeMonedaDeCore.MonedaIlegible, excepcion,
                "No se pudo tomar el bloqueo de moneda del pedido; no nace ningún pedido.");
            return ResultadoDeCreacion.Rechaza(MotivoDeNoCreacion.ServicioNoDisponible);
        }

        // 4b · La moneda AUTÉNTICA: la que CORE da por acreditada ahora, con el bloqueo
        // puesto, leída EN ESTA MISMA TRANSACCIÓN Y CONEXIÓN. No hay una segunda lectura
        // con la conexión de CORE: mientras esta retiene el bloqueo, pedir otra al pool es
        // el defecto D-1. Si dejó de estar acreditada desde el paso 0, no nace el pedido.
        var lectura = await monedas.LeerEnTransaccionAsync(
            database.Database.CurrentTransaction!.GetDbTransaction(), cancellationToken);

        if (lectura.Estado != EstadoDeMonedaDeVenta.Acreditada || !CurrencyCode.TieneFormaIso(lectura.Codigo))
        {
            // Si la lectura falló en la base, la transacción quedó abortada: la propia se
            // deshace al salir y la prestada la deshace su dueño. Sin número ni inserción.
            return ResultadoDeCreacion.Rechaza(MotivoPor(lectura.Estado));
        }

        var moneda = lectura.Codigo!;

        var pedido = new Order
        {
            OrderCode = string.Empty,   // lo pone el numerador, al final
            CustomerFullName = cliente.Instantanea!.FullName,
            CustomerEmail = cliente.Instantanea.Email,
            Status = OrderStatus.PendingPayment,
            PaymentDueAt = ahora.AddHours(plazo),
            TotalAmount = total,
            // La que afirmó la autoridad, congelada como el total.
            CurrencyCode = moneda
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
        // a trabajar fuera de transacción, así que esto no es una convención. Recibe la
        // etiqueta capturada en 3b: no consulta la configuración bajo el bloqueo.
        //
        // Si la conexión se corta aquí, todavía no hay COMMIT: el pedido no nació y se
        // puede decir. El COMMIT queda fuera a propósito: si falla, no se sabe si el
        // pedido quedó confirmado, y pedir «vuelve a confirmarlo» podría duplicarlo.
        try
        {
            pedido.OrderCode = await numerador.SiguienteAsync(etiquetaDeSerie, cancellationToken);
            database.Orders.Add(pedido);

            await database.SaveChangesAsync(cancellationToken);
        }
        catch (Exception excepcion) when (FallosDeInfraestructura.EsTransitorio(excepcion) && !cancellationToken.IsCancellationRequested)
        {
            registro.LogWarning(AutoridadDeMonedaDeCore.MonedaIlegible, excepcion,
                "Se perdió la conexión del pedido antes de confirmarlo; no nace ningún pedido.");
            return ResultadoDeCreacion.Rechaza(MotivoDeNoCreacion.ServicioNoDisponible);
        }

        await transaccion.CompletarAsync(cancellationToken);

        return new ResultadoDeCreacion(
            pedido.OrderCode, pedido.TotalAmount, pedido.PaymentDueAt, MotivoDeNoCreacion.Ninguno);
    }

    /// <summary>
    /// Por qué no nace el pedido, según lo que se supo de la moneda. Acreditada con un
    /// código sin forma ISO tampoco vale: es «sin autoridad».
    /// </summary>
    internal static MotivoDeNoCreacion MotivoPor(EstadoDeMonedaDeVenta estado) => estado switch
    {
        EstadoDeMonedaDeVenta.NoDisponible => MotivoDeNoCreacion.ServicioNoDisponible,
        EstadoDeMonedaDeVenta.UsoIndebido => MotivoDeNoCreacion.CosturaMonetariaDefectuosa,
        _ => MotivoDeNoCreacion.MonedaSinAutoridad,
    };
}
