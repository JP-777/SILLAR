using Microsoft.Extensions.Logging;
using Sillar.Core.Contracts;

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
/// La implementación registrada es <see cref="AutoridadDeMonedaDeCore"/>, que solo
/// responde con la moneda que CORE da por <b>acreditada</b>. Es asíncrona porque la
/// lectura de CORE lo es: va a la base en cada llamada, no a la caché de configuración.
/// </para>
/// </remarks>
internal interface IAutoridadDeMoneda
{
    /// <summary>
    /// La moneda ISO 4217 en que se venden los pedidos nuevos, o <c>null</c> si no hay
    /// autoridad que la afirme. <b>Nunca un valor por defecto.</b>
    /// </summary>
    /// <remarks>La cancelación del llamador se propaga.</remarks>
    Task<string?> MonedaDeVentaAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Ninguna autoridad: con ella, ningún pedido monetario nace.
/// </summary>
/// <remarks>
/// <b>Cerrar en falso es el comportamiento buscado.</b> Un pedido sin moneda se replica
/// y queda para siempre como un importe sin unidad; un pedido que no se crea no deja
/// nada que corregir. Ya no es la implementación que registra M03; queda como la
/// ausencia explícita de autoridad.
/// </remarks>
internal sealed class SinAutoridadDeMoneda : IAutoridadDeMoneda
{
    public Task<string?> MonedaDeVentaAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<string?>(null);
    }
}

/// <summary>
/// La moneda de venta según CORE: solo la acreditada.
/// </summary>
/// <remarks>
/// <para>
/// <b>Consume únicamente <see cref="IInstallationCurrencyAuthority"/>.</b> No lee
/// <c>currency_code</c>, ni <c>currency_symbol</c>, ni ningún valor sembrado, ni la
/// caché de configuración. Si CORE no dice <see cref="InstallationCurrencyStatus.Accredited"/>
/// con una moneda, la respuesta es <c>null</c>, y el creador rechaza el pedido con el
/// mismo motivo de siempre (D20-A): no preparada, sin establecer, no admitida, sin
/// comprobante o ilegible terminan igual para el cliente.
/// </para>
/// <para>
/// <b>Por petición, no única.</b> El lector de CORE vive en el ámbito de la petición
/// sobre su propio contexto; registrarlo dentro de un servicio único lo capturaría.
/// </para>
/// </remarks>
internal sealed class AutoridadDeMonedaDeCore(
    IInstallationCurrencyAuthority core,
    ILogger<AutoridadDeMonedaDeCore> logger) : IAutoridadDeMoneda
{
    public async Task<string?> MonedaDeVentaAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // La cancelación sale tal cual: el lector de CORE la propaga y aquí no se traga.
        var lectura = await core.ReadAsync(cancellationToken);

        if (lectura.IsAccredited && lectura.Currency is { Code: { } codigo })
        {
            return codigo;
        }

        logger.LogInformation(
            "Sin moneda de venta acreditada por CORE ({Estado}, lista {Lista}); no nace ningún pedido.",
            lectura.Status,
            lectura.ListVersion);
        return null;
    }
}
