using Sillar.Modules.Crm.Contracts;

namespace Sillar.Modules.Sales.Pedidos;

/// <summary>
/// Obtiene de M04 los datos del cliente y decide si puede comprar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la mitad del checkout que no habla de artículos ni de precios</b>, y por eso
/// se puede construir y probar sin la costura de Catálogo.
/// </para>
/// <para>
/// <b>La guarda va aquí, en la operación.</b> El carrito o el endpoint pueden
/// anticiparla para responder rápido, pero ninguno la constituye: ponerla en el
/// llamador protegería de ese llamador, y aquí protege de todos.
/// </para>
/// <para>
/// <b>Solo referencia <c>Sillar.Modules.Crm.Contracts</c></b>, nunca
/// <c>Sillar.Modules.Crm</c> ni su <c>Data</c> o <c>Domain</c>. No hay ninguna
/// consulta a tablas de CRM, y no puede haberla: el compilador no alcanza sus
/// entidades.
/// </para>
/// </remarks>
internal sealed class CongeladorDeCliente(ICustomerSnapshotReader lector)
{
    /// <summary>
    /// Los datos del cliente para un pedido de recojo, si puede comprar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Dos rechazos, en este orden y no en otro.</b> Primero el <c>null</c> de
    /// M04, porque sin ficha no hay nada que mirar; después el correo sin verificar,
    /// que es regla de M03 (R-07) y no de M04 — su contrato entrega el hecho y deja
    /// la decisión aquí.
    /// </para>
    /// <para>
    /// <b>El <c>null</c> no se interpreta.</b> M04 lo devuelve sin distinguir si la
    /// ficha no existe, está de baja, está bloqueada o no tiene cuenta, y <b>eso es
    /// deliberado de su parte</b>. M03 no deduce el motivo ni se lo dice al cliente:
    /// nombrar una causa que nadie garantiza es inventarla.
    /// </para>
    /// </remarks>
    public async Task<ResultadoDeInstantanea> ObtenerParaRecojoAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        // La sobrecarga SIN dirección: un pedido de recojo no tiene a dónde enviarse,
        // y un cliente sin dirección guardada compra igual.
        var cliente = await lector.GetForOrderAsync(customerId, cancellationToken);

        if (cliente is null)
        {
            return new ResultadoDeInstantanea(null, MotivoDeRechazo.LaCuentaNoPuedeComprar);
        }

        // R-07: sin verificar se puede entrar y mirar; comprar, no. Lo exige M03
        // porque si el correo no está verificado, el aviso del pedido no llega.
        if (!cliente.EmailVerified)
        {
            return new ResultadoDeInstantanea(null, MotivoDeRechazo.CorreoSinVerificar);
        }

        return new ResultadoDeInstantanea(
            new InstantaneaDelCliente(
                cliente.CustomerId,
                cliente.FullName,
                cliente.Email,
                cliente.Phone,
                cliente.DocumentType,
                cliente.DocumentNumber),
            MotivoDeRechazo.Ninguno);
    }
}
