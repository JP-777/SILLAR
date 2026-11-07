using Microsoft.EntityFrameworkCore;
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
/// 500 genérico. Dentro de la transacción llega sin envolver. El guardado de EF lo
/// envuelve en <see cref="DbUpdateException"/>, y la estrategia de ejecución vuelve a
/// envolver ese <see cref="DbUpdateException"/> en el <see cref="InvalidOperationException"/>
/// «transient failure», también dentro de la transacción: así llega un corte durante el
/// <c>INSERT</c> (R10-E13-G, D-4). Se aceptan las cuatro formas.
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

    /// <summary>
    /// Un fallo que <b>rompe la conexión</b>: el servidor cerró la sesión o se perdió el
    /// canal. Con la sesión cerrada, PostgreSQL ya deshizo la transacción abierta (R10-E13-G).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Más estrecho que <see cref="EsTransitorio"/> a propósito: <c>55P03</c> o
    /// <c>40001</c> son transitorios pero dejan la conexión viva, y ahí deshacer sí es
    /// posible y necesario.
    /// </para>
    /// <list type="bullet">
    /// <item>Del servidor: <c>57P01</c>, <c>57P02</c>, <c>57P03</c>, clase <c>08</c>, o
    /// gravedad <c>FATAL</c> o <c>PANIC</c>, con la que el servidor cierra la sesión.</item>
    /// <item>Del cliente, sin respuesta del servidor (E/S, socket o tiempo de espera): las
    /// <see cref="NpgsqlException"/> transitorias, que rompen el conector.</item>
    /// </list>
    /// </remarks>
    public static bool EsConexionRota(Exception? excepcion) => excepcion is not null && Desenvuelta(excepcion) switch
    {
        PostgresException p => p.SqlState is PostgresErrorCodes.AdminShutdown or PostgresErrorCodes.CrashShutdown or PostgresErrorCodes.CannotConnectNow
            || p.SqlState.StartsWith("08", StringComparison.Ordinal)
            || p.InvariantSeverity is "FATAL" or "PANIC",
        NpgsqlException { IsTransient: true } => true,
        _ => false,
    };

    private static Exception Desenvuelta(Exception excepcion) => excepcion switch
    {
        InvalidOperationException { InnerException: DbUpdateException { InnerException: NpgsqlException interna } } => interna,
        InvalidOperationException { InnerException: NpgsqlException interna } => interna,
        DbUpdateException { InnerException: NpgsqlException interna } => interna,
        _ => excepcion,
    };
}
