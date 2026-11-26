using System.Data.Common;
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
/// regional: la pide aquí, y si aquí no hay respuesta acreditada, <b>no crea el pedido</b>.
/// </para>
/// <para>
/// <b>Dos lecturas con dos papeles distintos</b> (R10-E13-B):
/// </para>
/// <list type="bullet">
/// <item><see cref="MonedaDeVentaAsync"/>, <b>antes</b> de abrir la transacción del pedido:
/// solo sirve para rechazar pronto (D20-A). Usa la conexión de CORE, y en ese momento
/// Ventas todavía no retiene ninguna.</item>
/// <item><see cref="LeerEnTransaccionAsync"/>, <b>dentro</b> de la transacción del pedido y
/// con el bloqueo de moneda ya concedido: es la que se congela. Usa la misma conexión y la
/// misma transacción de Ventas; no pide otra al pool mientras se retiene el bloqueo (D-1).</item>
/// </list>
/// <para>
/// Las dos distinguen tres cosas que para el cliente no son lo mismo: no hay moneda
/// acreditada, no se pudo leer ahora, y la costura se usó mal.
/// </para>
/// </remarks>
internal interface IAutoridadDeMoneda
{
    /// <summary>
    /// La moneda acreditada, leída con la conexión de CORE. <b>Solo para rechazar</b>: no
    /// se llama con una transacción de Ventas abierta, y lo que devuelve no se congela.
    /// </summary>
    /// <remarks>La cancelación del llamador se propaga.</remarks>
    Task<LecturaDeMonedaDeVenta> MonedaDeVentaAsync(CancellationToken cancellationToken);

    /// <summary>
    /// La moneda acreditada, leída en <paramref name="transaccion"/>: la transacción viva
    /// del pedido, que ya tiene el bloqueo compartido de moneda.
    /// </summary>
    /// <remarks>
    /// No confirma, no deshace y no cierra la transacción. Si la lectura falla, la
    /// transacción puede quedar abortada; deshacerla le toca a su dueño. La cancelación
    /// del llamador se propaga.
    /// </remarks>
    Task<LecturaDeMonedaDeVenta> LeerEnTransaccionAsync(DbTransaction transaccion, CancellationToken cancellationToken);
}

/// <summary>Qué se sabe de la moneda de venta.</summary>
internal enum EstadoDeMonedaDeVenta
{
    /// <summary>CORE la da por acreditada, con su código.</summary>
    Acreditada,

    /// <summary>
    /// No hay moneda acreditada: sin preparar, sin establecer, no admitida o sin
    /// comprobante. Lo arregla la tienda configurándola.
    /// </summary>
    SinAutoridad,

    /// <summary>
    /// No se pudo leer ahora: la base, la conexión o el pool fallaron. No dice nada sobre
    /// si la tienda tiene moneda. Se arregla solo o reintentando.
    /// </summary>
    NoDisponible,

    /// <summary>
    /// La costura con CORE se usó fuera de su contrato (aislamiento, bloqueo, base o
    /// transacción). Es un defecto de integración, no un estado de la tienda.
    /// </summary>
    UsoIndebido,
}

/// <summary>Lo que respondió la autoridad.</summary>
/// <param name="Estado">Qué se sabe.</param>
/// <param name="Codigo">El código ISO 4217, solo si está acreditada.</param>
internal readonly record struct LecturaDeMonedaDeVenta(EstadoDeMonedaDeVenta Estado, string? Codigo)
{
    public static LecturaDeMonedaDeVenta Acreditada(string codigo) => new(EstadoDeMonedaDeVenta.Acreditada, codigo);

    public static LecturaDeMonedaDeVenta SinAutoridad { get; } = new(EstadoDeMonedaDeVenta.SinAutoridad, null);

    public static LecturaDeMonedaDeVenta NoDisponible { get; } = new(EstadoDeMonedaDeVenta.NoDisponible, null);

    public static LecturaDeMonedaDeVenta UsoIndebido { get; } = new(EstadoDeMonedaDeVenta.UsoIndebido, null);
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
    public Task<LecturaDeMonedaDeVenta> MonedaDeVentaAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LecturaDeMonedaDeVenta.SinAutoridad);
    }

    public Task<LecturaDeMonedaDeVenta> LeerEnTransaccionAsync(DbTransaction transaccion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LecturaDeMonedaDeVenta.SinAutoridad);
    }
}

/// <summary>
/// La moneda de venta según CORE: solo la acreditada.
/// </summary>
/// <remarks>
/// <para>
/// <b>Consume únicamente los dos lectores acreditados de CORE</b>:
/// <see cref="IInstallationCurrencyAuthority"/> para el rechazo temprano e
/// <see cref="IInstallationCurrencyTransactionalAuthority"/> para la lectura que se
/// congela. No lee <c>currency_code</c>, ni <c>currency_symbol</c>, ni ningún valor
/// sembrado, ni la caché de configuración, ni las tablas de CORE: la consulta y la
/// evaluación son las de CORE.
/// </para>
/// <para>
/// <b>Cómo traduce.</b> <see cref="InstallationCurrencyStatus.Accredited"/> con moneda es
/// <see cref="EstadoDeMonedaDeVenta.Acreditada"/>; <see cref="InstallationCurrencyStatus.Unavailable"/>
/// o un fallo al leer es <see cref="EstadoDeMonedaDeVenta.NoDisponible"/>; cualquier otro
/// estado es <see cref="EstadoDeMonedaDeVenta.SinAutoridad"/>. En la lectura transaccional,
/// el <see cref="InvalidOperationException"/> con que CORE señala un uso fuera de contrato
/// es <see cref="EstadoDeMonedaDeVenta.UsoIndebido"/> y se registra como error con
/// <see cref="CosturaFueraDeContrato"/>.
/// </para>
/// <para>
/// <b>Por petición, no única.</b> Los lectores de CORE viven en el ámbito de la petición;
/// registrarlos dentro de un servicio único los capturaría.
/// </para>
/// </remarks>
internal sealed class AutoridadDeMonedaDeCore(
    IInstallationCurrencyAuthority core,
    IInstallationCurrencyTransactionalAuthority coreEnTransaccion,
    ILogger<AutoridadDeMonedaDeCore> logger) : IAutoridadDeMoneda
{
    /// <summary>Identifica en los registros un uso de la costura fuera de contrato.</summary>
    internal static readonly EventId CosturaFueraDeContrato = new(31301, "CosturaMonetariaFueraDeContrato");

    /// <summary>Identifica en los registros una lectura de moneda que no se pudo hacer.</summary>
    internal static readonly EventId MonedaIlegible = new(31302, "MonedaDeVentaNoDisponible");

    public async Task<LecturaDeMonedaDeVenta> MonedaDeVentaAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        InstallationCurrencyReading lectura;
        try
        {
            lectura = await core.ReadAsync(cancellationToken);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // El contrato dice que un fallo de lectura es Unavailable; si aun así algo
            // sale como excepción, tampoco afirma que falte la moneda.
            logger.LogWarning(MonedaIlegible, excepcion, "No se pudo leer la moneda de venta en CORE antes de abrir el pedido.");
            return LecturaDeMonedaDeVenta.NoDisponible;
        }

        return Traducir(lectura, "antes de abrir el pedido");
    }

    public async Task<LecturaDeMonedaDeVenta> LeerEnTransaccionAsync(DbTransaction transaccion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaccion);
        cancellationToken.ThrowIfCancellationRequested();

        InstallationCurrencyReading lectura;
        try
        {
            lectura = await coreEnTransaccion.ReadInTransactionAsync(transaccion, cancellationToken);
        }
        catch (Exception excepcion) when (FallosDeInfraestructura.EsTransitorio(excepcion) && !cancellationToken.IsCancellationRequested)
        {
            // Antes que el uso indebido: EF envuelve lo transitorio en InvalidOperationException.
            logger.LogWarning(MonedaIlegible, excepcion, "No se pudo leer la moneda de venta en la transacción del pedido.");
            return LecturaDeMonedaDeVenta.NoDisponible;
        }
        catch (InvalidOperationException excepcion) when (!cancellationToken.IsCancellationRequested)
        {
            // Aislamiento, bloqueo, base o transacción fuera de contrato: un defecto que
            // tiene que verse en los registros. Al cliente no le llega este detalle.
            logger.LogError(CosturaFueraDeContrato, excepcion,
                "La lectura transaccional de moneda de CORE rechazó la transacción del pedido; no nace ningún pedido.");
            return LecturaDeMonedaDeVenta.UsoIndebido;
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(MonedaIlegible, excepcion, "No se pudo leer la moneda de venta en la transacción del pedido.");
            return LecturaDeMonedaDeVenta.NoDisponible;
        }

        return Traducir(lectura, "en la transacción del pedido");
    }

    private LecturaDeMonedaDeVenta Traducir(InstallationCurrencyReading lectura, string momento)
    {
        if (lectura.IsAccredited && lectura.Currency is { Code: { } codigo })
        {
            return LecturaDeMonedaDeVenta.Acreditada(codigo);
        }

        if (lectura.Status == InstallationCurrencyStatus.Unavailable)
        {
            logger.LogWarning(MonedaIlegible,
                "CORE no pudo leer la moneda de venta {Momento} (lista {Lista}); no nace ningún pedido.",
                momento,
                lectura.ListVersion);
            return LecturaDeMonedaDeVenta.NoDisponible;
        }

        logger.LogInformation(
            "Sin moneda de venta acreditada por CORE {Momento} ({Estado}, lista {Lista}); no nace ningún pedido.",
            momento,
            lectura.Status,
            lectura.ListVersion);
        return LecturaDeMonedaDeVenta.SinAutoridad;
    }
}
