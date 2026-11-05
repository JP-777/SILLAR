namespace Sillar.Core.Contracts;

/// <summary>
/// Bloqueo consultivo de PostgreSQL que serializa la moneda de la instalación con
/// el uso que hace de ella quien cobra. Es la barrera común entre CORE y Ventas.
/// </summary>
/// <remarks>
/// <para>
/// <b>Identidad.</b> Forma de dos claves <c>int4</c>: <see cref="Namespace"/> y
/// <see cref="CurrencyAuthority"/>. Todos los bloqueos consultivos que ya existen
/// en el producto usan la forma de una clave <c>bigint</c>
/// (<c>ModuleActivationService.ActivationLockKey</c> y los <c>hashtextextended</c>
/// de órdenes de servicio y seguimiento), y PostgreSQL no mezcla los dos espacios:
/// esta identidad no puede chocar con ellos, ni por valor ni por hash.
/// </para>
/// <para>
/// <b>Por instalación sin codificar la instalación.</b> Los bloqueos consultivos
/// pertenecen a una base de datos, y en una base cabe una sola instalación
/// (<c>ck_installation_singleton</c>). Así, la misma identidad en dos
/// instalaciones son dos bloqueos distintos, y Ventas no necesita leer
/// <c>core.installation</c> para calcularla. CORE y Ventas usan la misma base porque
/// los dos se conectan con la cadena <c>Default</c>.
/// </para>
/// <para>
/// <b>Quién lo adquiere, en qué modo y en qué orden.</b>
/// </para>
/// <list type="number">
/// <item><b>CORE, al establecer o cambiar la moneda:</b> <see cref="AcquireExclusive"/>,
/// primera sentencia de su transacción tras fijar su tiempo de espera; antes de
/// consultar <see cref="IMonetaryRecordsProbe"/> y antes de bloquear la fila de
/// configuración.</item>
/// <item><b>Ventas, al crear un pedido (pendiente de implementar):</b>
/// <see cref="AcquireShared"/>, primer bloqueo de su transacción: antes de leer la
/// moneda con <see cref="IInstallationCurrencyAuthority"/>, antes de numerar y antes
/// de insertar. Se mantiene hasta confirmar o deshacer el pedido.</item>
/// </list>
/// <para>
/// <b>Solo las variantes de transacción</b> (<c>_xact_</c>): se liberan solas al
/// confirmar o deshacer. Nunca <c>pg_advisory_lock</c> de sesión con esta identidad.
/// </para>
/// <para>
/// <b>Quien implementa <see cref="IMonetaryRecordsProbe"/> no lo adquiere.</b> CORE
/// llama a la sonda teniéndolo ya en exclusivo, en otra conexión: si la sonda lo
/// pidiera, esperaría a CORE y CORE a ella, y PostgreSQL no lo vería como
/// interbloqueo porque CORE no espera en la base sino en la aplicación. La sonda
/// solo lee, sin <c>FOR SHARE</c> ni <c>FOR UPDATE</c>.
/// </para>
/// </remarks>
public static class MonetaryAuthorityLock
{
    /// <summary>Espacio de bloqueos consultivos de SILLAR: <c>'SILL'</c> en ASCII.</summary>
    public const int Namespace = 0x53494C4C;

    /// <summary>Bloqueo de la moneda de la instalación dentro de <see cref="Namespace"/>.</summary>
    public const int CurrencyAuthority = 1;

    /// <summary>Exclusivo, hasta el fin de la transacción. Solo CORE, al escribir la moneda.</summary>
    public static FormattableString AcquireExclusive()
        => $"SELECT pg_advisory_xact_lock({Namespace}, {CurrencyAuthority})";

    /// <summary>Compartido, hasta el fin de la transacción. Para quien crea pedidos con la moneda.</summary>
    public static FormattableString AcquireShared()
        => $"SELECT pg_advisory_xact_lock_shared({Namespace}, {CurrencyAuthority})";
}
