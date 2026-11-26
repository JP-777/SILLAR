using System.Data.Common;

namespace Sillar.Core.Contracts;

/// <summary>
/// La moneda acreditada, leída <b>dentro de la transacción comercial del llamador</b>:
/// sobre su misma conexión, sin abrir otra.
/// </summary>
/// <remarks>
/// <para>
/// <b>Para qué existe.</b> Quien crea un pedido toma
/// <see cref="MonetaryAuthorityLock.AcquireShared"/> en su transacción y después
/// necesita la moneda. <see cref="IInstallationCurrencyAuthority"/> lee con la conexión
/// de CORE: bajo el bloqueo, cada pedido ocuparía dos conexiones del pool a la vez, y
/// con un pool corto las peticiones se quedan esperando unas a otras (R10-E12, D-1).
/// Este contrato hace la misma lectura en la conexión que ya tiene el bloqueo.
/// </para>
/// <para>
/// <b>Mismas garantías que <see cref="IInstallationCurrencyAuthority"/>.</b> La consulta
/// y la evaluación son las de CORE, no una copia: solo <see cref="InstallationCurrencyStatus.Accredited"/>
/// con una moneda de <see cref="SupportedCurrencies"/> y su comprobante de auditoría;
/// nunca <c>currency_code</c> ni la caché de configuración. Quien llama no recibe ni
/// necesita acceso a las tablas de CORE.
/// </para>
/// <para>
/// <b>Lo que CORE comprueba antes de leer</b>, en la propia transacción:
/// </para>
/// <list type="bullet">
/// <item>que la conexión esté abierta y la transacción sea suya y siga viva;</item>
/// <item>que el aislamiento sea <c>read committed</c>: con un aislamiento mayor la
/// instantánea se tomaría antes del bloqueo y se leería la moneda anterior a un cambio
/// que el bloqueo ya esperó;</item>
/// <item>que esta transacción tenga concedido el bloqueo de moneda (compartido o
/// exclusivo): sin él, nada impide que la moneda cambie después de leerla;</item>
/// <item>que la base sea la de CORE.</item>
/// </list>
/// <para>
/// Si algo de eso falla es un error de uso, no una moneda ausente: se lanza
/// <see cref="InvalidOperationException"/> y no se lee nada.
/// </para>
/// <para>
/// <b>Lo que CORE no hace con la transacción ajena:</b> no la confirma, no la deshace,
/// no la cierra, no abre ni cierra su conexión, no escribe y no toma ningún bloqueo.
/// Solo ejecuta lecturas. Si una falla, la transacción del llamador puede quedar
/// abortada (así es PostgreSQL); deshacerla le corresponde al llamador.
/// </para>
/// </remarks>
public interface IInstallationCurrencyTransactionalAuthority
{
    /// <summary>Lee la moneda acreditada en la transacción dada.</summary>
    /// <param name="transaction">
    /// La transacción abierta del llamador, con el bloqueo de moneda ya concedido. Su
    /// <see cref="DbTransaction.Connection"/> es la conexión que se usa.
    /// </param>
    /// <param name="cancellationToken">Se propaga como <see cref="OperationCanceledException"/>.</param>
    /// <returns>
    /// La lectura, con las mismas reglas que <see cref="IInstallationCurrencyAuthority.ReadAsync"/>:
    /// un fallo al leer es <see cref="InstallationCurrencyStatus.Unavailable"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Sin transacción.</exception>
    /// <exception cref="InvalidOperationException">
    /// Conexión cerrada, transacción terminada o ajena, aislamiento distinto de
    /// <c>read committed</c>, bloqueo de moneda no concedido o base distinta de la de CORE.
    /// </exception>
    Task<InstallationCurrencyReading> ReadInTransactionAsync(DbTransaction transaction, CancellationToken cancellationToken);
}
