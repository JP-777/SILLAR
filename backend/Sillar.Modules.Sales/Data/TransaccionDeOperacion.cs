using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sillar.Modules.Sales.Pedidos;

namespace Sillar.Modules.Sales.Data;

/// <summary>
/// Una transacción que se abre solo si no hay ninguna, y que solo confirma la que
/// abrió.
/// </summary>
/// <remarks>
/// <para>
/// <b>Existe porque el mismo defecto apareció dos veces.</b> Primero
/// <c>VencimientoDePlazos</c> y después <c>CreadorDePedidos</c> abrían su transacción
/// sin mirar si ya había una: PostgreSQL no las anida, así que las dos reventaban
/// cuando se las llamaba desde dentro de otra. Las dos veces lo destapó la prueba, que
/// envuelve cada caso para no dejar filas.
/// </para>
/// <para>
/// <b>Se generaliza ahora y no antes</b> porque ahora hay un segundo caso real. Con uno
/// solo habría sido una abstracción «por si acaso».
/// </para>
/// <para>
/// <b>La regla que encapsula:</b> la atomicidad la garantiza quien abre la
/// transacción. Si la abrió el llamador, es suya y aquí no se confirma ni se deshace;
/// si no había ninguna, esta la abre y la confirma al completar.
/// </para>
/// <para>
/// <b>No sirve para <c>OrderCodeAllocator</c>, y es deliberado:</b> aquél <b>exige</b>
/// una transacción abierta y se niega a trabajar sin ella, porque numerar fuera dejaría
/// un hueco permanente en la serie. Son dos necesidades distintas y no se confunden.
/// </para>
/// <para>
/// <b>Deshacer una transacción cuya conexión ya se rompió</b> (R10-E13-G, D-3). Si el
/// servidor cierra la sesión antes del <c>COMMIT</c>, Npgsql desecha la transacción, y
/// deshacerla lanza <see cref="ObjectDisposedException"/>. Esa excepción secundaria
/// sustituía a la causa original: el corte ya traducido a «servicio no disponible» salía
/// como un 500. Solo se tolera ese caso, y solo cuando el motivo que Npgsql guardó al
/// desechar es una conexión rota (<see cref="FallosDeInfraestructura.EsConexionRota"/>):
/// con la sesión cerrada, PostgreSQL ya deshizo la transacción y soltó sus bloqueos. Queda
/// en <see cref="RollbackImposible"/> y en el registro. Cualquier otro fallo al deshacer se
/// propaga.
/// </para>
/// </remarks>
internal sealed class TransaccionDeOperacion : IAsyncDisposable
{
    /// <summary>Identifica en los registros un rollback que no se pudo hacer porque la conexión ya se había roto.</summary>
    internal static readonly EventId RollbackTrasConexionRota = new(31303, "RollbackImposibleTrasConexionRota");

    private readonly IDbContextTransaction? propia;
    private readonly ILogger? registro;
    private bool completada;

    private TransaccionDeOperacion(IDbContextTransaction? propia, ILogger? registro = null)
    {
        this.propia = propia;
        this.registro = registro;
    }

    /// <summary>
    /// Si al deshacer se encontró la transacción ya desechada por una conexión rota, la
    /// excepción de la limpieza; si no, <c>null</c>. Es diagnóstico, no un resultado.
    /// </summary>
    public ObjectDisposedException? RollbackImposible { get; private set; }

    /// <summary>Se une a la transacción del llamador, o abre una propia.</summary>
    public static async Task<TransaccionDeOperacion> AbrirSiHaceFaltaAsync(
        SalesDbContext database,
        CancellationToken cancellationToken)
        => new(database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(cancellationToken)
            : null);

    /// <summary>
    /// Igual, pero la propia se abre con el aislamiento indicado, explícito en el
    /// <c>BEGIN</c>: no depende de <c>default_transaction_isolation</c> del servidor.
    /// </summary>
    /// <remarks>
    /// La del llamador no se toca, tampoco su aislamiento: comprobar si sirve le toca a
    /// quien la necesita (R10-E13-B, <c>CreadorDePedidos</c>). <paramref name="registro"/>
    /// recibe el aviso si al deshacer la conexión ya estaba rota.
    /// </remarks>
    public static async Task<TransaccionDeOperacion> AbrirSiHaceFaltaAsync(
        SalesDbContext database,
        IsolationLevel aislamiento,
        ILogger? registro,
        CancellationToken cancellationToken)
        => new(database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(aislamiento, cancellationToken)
            : null, registro);

    /// <summary>
    /// Confirma, si la transacción es propia. Si es del llamador, no hace nada.
    /// </summary>
    public async Task CompletarAsync(CancellationToken cancellationToken)
    {
        if (propia is not null)
        {
            await propia.CommitAsync(cancellationToken);
        }

        completada = true;
    }

    /// <summary>
    /// Deshace la propia si nadie la completó. La del llamador no se toca nunca.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sin esto, una excepción a mitad dejaría la transacción propia abierta hasta que
    /// el contexto muriera, y entre tanto la fila del contador seguiría bloqueada.
    /// </para>
    /// <para>
    /// <b>Lo único que no se propaga</b> es el <see cref="ObjectDisposedException"/> de una
    /// transacción que Npgsql desechó porque la conexión se rompió: su
    /// <see cref="Exception.InnerException"/> es el motivo que Npgsql guardó, y tiene que ser
    /// una conexión rota. Sin motivo, con otro motivo, u otra excepción —un fallo genuino al
    /// deshacer o un uso indebido de la transacción— se propagan como siempre.
    /// </para>
    /// <para>
    /// Un <c>COMMIT</c> que falla no cambia de semántica: su excepción sigue siendo la que
    /// sale. Esto solo impide que la limpieza la sustituya.
    /// </para>
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (propia is null)
        {
            return;
        }

        if (!completada)
        {
            try
            {
                await propia.RollbackAsync();
            }
            catch (ObjectDisposedException secundaria) when (FallosDeInfraestructura.EsConexionRota(secundaria.InnerException))
            {
                // La sesión ya no existe: PostgreSQL deshizo la transacción y soltó sus
                // bloqueos al cerrarla. No hay nada que deshacer aquí, y la causa original
                // —la que ya se está devolviendo o propagando— no se sustituye.
                RollbackImposible = secundaria;
                registro?.LogWarning(RollbackTrasConexionRota, secundaria,
                    "La transacción ya estaba desechada porque la conexión se rompió ({Motivo}); PostgreSQL la deshizo al cerrar la sesión. Se conserva el fallo original.",
                    (secundaria.InnerException as PostgresException)?.SqlState ?? secundaria.InnerException!.GetType().Name);
            }
        }

        await propia.DisposeAsync();
    }
}
