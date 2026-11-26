using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;

namespace Sillar.Modules.Sales.Data;

/// <summary>
/// La mitad de Ventas de la barrera de moneda: el bloqueo compartido de
/// <see cref="MonetaryAuthorityLock"/>, dentro de la transacción del pedido.
/// </summary>
/// <remarks>
/// <para>
/// <b>Qué garantiza.</b> CORE toma el mismo bloqueo en exclusivo para cambiar la moneda.
/// Un pedido que lo tiene en compartido termina antes de que CORE lo obtenga, y la sonda
/// de CORE ya lo ve; un pedido que llega mientras CORE lo tiene espera a que CORE confirme
/// o deshaga, y lee la moneda que quedó. Los pedidos entre sí no se esperan por esto.
/// </para>
/// <para>
/// <b>Exige transacción abierta</b>, como el numerador: fuera de ella la variante
/// <c>_xact_</c> se soltaría al terminar la propia sentencia y no protegería nada.
/// </para>
/// <para>
/// <b>Espera acotada solo para el bloqueo.</b> Se fija <c>lock_timeout</c> para esta
/// sentencia y se restaura el valor que tuviera la transacción, para no cambiar cuánto
/// espera la fila del numerador ni la transacción de un llamador. Si se agota, PostgreSQL
/// lanza <c>55P03</c> y la transacción queda abortada: el pedido no nace.
/// </para>
/// </remarks>
internal static class BarreraMonetariaDeVentas
{
    /// <summary>Cuánto espera un pedido a que CORE termine de cambiar la moneda.</summary>
    internal const string TiempoDeEspera = "5s";

    /// <summary>Toma el bloqueo compartido de moneda en la transacción abierta.</summary>
    /// <exception cref="InvalidOperationException">Si no hay transacción abierta.</exception>
    public static async Task AdquirirCompartidaAsync(SalesDbContext database, CancellationToken cancellationToken)
    {
        if (database.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "El bloqueo de moneda se toma dentro de la transacción que persiste el pedido, " +
                "y no hay ninguna abierta: fuera de ella se soltaría al instante.");
        }

        var anterior = (await database.Database
                .SqlQueryRaw<string>("SELECT current_setting('lock_timeout') AS \"Value\"")
                .ToListAsync(cancellationToken))
            .Single();

        await database.Database.ExecuteSqlRawAsync(
            "SELECT set_config('lock_timeout', {0}, true)", [TiempoDeEspera], cancellationToken);

        await database.Database.ExecuteSqlAsync(MonetaryAuthorityLock.AcquireShared(), cancellationToken);

        await database.Database.ExecuteSqlRawAsync(
            "SELECT set_config('lock_timeout', {0}, true)", [anterior], cancellationToken);
    }
}
