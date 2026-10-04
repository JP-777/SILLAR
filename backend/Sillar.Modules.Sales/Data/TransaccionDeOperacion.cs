using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
/// </remarks>
internal sealed class TransaccionDeOperacion : IAsyncDisposable
{
    private readonly IDbContextTransaction? propia;
    private bool completada;

    private TransaccionDeOperacion(IDbContextTransaction? propia) => this.propia = propia;

    /// <summary>Se une a la transacción del llamador, o abre una propia.</summary>
    public static async Task<TransaccionDeOperacion> AbrirSiHaceFaltaAsync(
        SalesDbContext database,
        CancellationToken cancellationToken)
        => new(database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(cancellationToken)
            : null);

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
    /// Sin esto, una excepción a mitad dejaría la transacción propia abierta hasta que
    /// el contexto muriera, y entre tanto la fila del contador seguiría bloqueada.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (propia is null)
        {
            return;
        }

        if (!completada)
        {
            await propia.RollbackAsync();
        }

        await propia.DisposeAsync();
    }
}
