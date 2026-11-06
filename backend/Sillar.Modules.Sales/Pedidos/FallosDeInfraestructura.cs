using Npgsql;

namespace Sillar.Modules.Sales.Pedidos;

/// <summary>
/// Qué fallos de la base son «no se puede ahora» y cuál es «la moneda está cambiando»,
/// vengan como los lanza Npgsql o envueltos por EF Core (R10-E13-B).
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué hace falta desenvolver.</b> Fuera de una transacción, la estrategia de
/// ejecución de EF Core con Npgsql envuelve los fallos transitorios en un
/// <see cref="InvalidOperationException"/> («An exception has been raised that is likely
/// due to a transient failure»), con la <see cref="NpgsqlException"/> dentro. Así llegó
/// el pool agotado al abrir la transacción del pedido en R10-E12, y por eso acabó en un
/// 500 genérico. Dentro de la transacción llega sin envolver. Se aceptan las dos formas.
/// </para>
/// <para>
/// <b>El orden importa.</b> Para Npgsql, <c>55P03</c> (<c>lock_timeout</c> agotado) también
/// es transitorio; quien clasifica pregunta antes por <see cref="EsEsperaDelBloqueoAgotada"/>.
/// </para>
/// </remarks>
internal static class FallosDeInfraestructura
{
    /// <summary>El bloqueo no llegó a tiempo: <c>55P03</c>.</summary>
    public static bool EsEsperaDelBloqueoAgotada(Exception excepcion)
        => Desenvuelta(excepcion) is PostgresException { SqlState: PostgresErrorCodes.LockNotAvailable };

    /// <summary>Un fallo que Npgsql da por transitorio: conexión, pool, red o servidor que no atiende.</summary>
    public static bool EsTransitorio(Exception excepcion)
        => Desenvuelta(excepcion) is NpgsqlException { IsTransient: true };

    private static Exception Desenvuelta(Exception excepcion)
        => excepcion is InvalidOperationException { InnerException: NpgsqlException interna } ? interna : excepcion;
}
