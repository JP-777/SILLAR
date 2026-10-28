namespace Sillar.Modules.Sales.Pedidos;

/// <summary>
/// <b>El único punto por el que entra la moneda de venta de un pedido nuevo.</b>
/// </summary>
/// <remarks>
/// <para>
/// Existe para que la moneda auténtica tenga un sitio donde suministrarse, y uno solo.
/// <c>CreadorDePedidos</c> no la deduce del catálogo, del nodo ni de la configuración
/// regional: la pide aquí, y si aquí no hay respuesta, <b>no crea el pedido</b>.
/// </para>
/// <para>
/// Hoy no hay autoridad de moneda ratificada, así que la única implementación
/// registrada es <see cref="SinAutoridadDeMoneda"/>. Cuando JP la ratifique, se
/// sustituye esa implementación y nada más.
/// </para>
/// </remarks>
internal interface IAutoridadDeMoneda
{
    /// <summary>
    /// La moneda ISO 4217 en que se venden los pedidos nuevos, o <c>null</c> si no hay
    /// autoridad que la afirme. <b>Nunca un valor por defecto.</b>
    /// </summary>
    string? MonedaDeVenta();
}

/// <summary>
/// Lo que hay hoy: ninguna autoridad. Con ella registrada, ningún pedido monetario nace.
/// </summary>
/// <remarks>
/// <b>Cerrar en falso es el comportamiento buscado.</b> Un pedido sin moneda se replica
/// y queda para siempre como un importe sin unidad; un pedido que no se crea no deja
/// nada que corregir.
/// </remarks>
internal sealed class SinAutoridadDeMoneda : IAutoridadDeMoneda
{
    public string? MonedaDeVenta() => null;
}
