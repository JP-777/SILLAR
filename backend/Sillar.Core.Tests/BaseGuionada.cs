using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Sillar.Core.Data;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

/// <summary>
/// Una base de datos de mentira para comprobar <b>el orden</b> de lo que un servicio
/// hace dentro de su transacción, sin PostgreSQL.
/// </summary>
/// <remarks>
/// <para>
/// La conexión es falsa y se declara abierta —si no, EF daría la transacción por
/// perdida en el primer comando y seguiría sin ella—; la transacción y los comandos
/// también son falsos, y cada paso queda anotado en <see cref="Pasos"/> en el orden en que
/// ocurre: <c>BEGIN</c>, el texto de cada comando, lo que añada la prueba (la
/// sonda, por ejemplo) y <c>COMMIT</c> o <c>ROLLBACK</c>. Toda lectura devuelve cero
/// filas.
/// </para>
/// <para>
/// <b>No prueba exclusión mutua.</b> Prueba que las sentencias salen en el orden
/// debido y que la transacción se confirma o se deshace cuando toca. Que dos
/// conexiones se excluyan de verdad solo lo demuestra PostgreSQL.
/// </para>
/// </remarks>
internal sealed class BaseGuionada
{
    /// <summary>Lo que pasó, en orden.</summary>
    public List<string> Pasos { get; } = [];

    /// <summary>Si devuelve una excepción para un comando, el comando la lanza en vez de ejecutarse.</summary>
    public Func<string, Exception?> FallaAlEjecutar { get; set; } = _ => null;

    public CoreDbContext Contexto()
    {
        var opciones = new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql(new ConexionFalsa())
            .AddInterceptors(new TransaccionFalsaAlEmpezar(this), new ComandoFalsoAlCrear(this));
        return new CoreDbContext(opciones.Options, Nodo, TimeProvider.System);
    }

    /// <summary>Siempre abierta, sin servidor detrás.</summary>
    private sealed class ConexionFalsa : DbConnection
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = "";

        public override string Database => "guionada";

        public override string DataSource => "guionada";

        public override string ServerVersion => "16.0";

        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName)
        {
        }

        public override void Open()
        {
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public override void Close()
        {
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new InvalidOperationException("La transacción la da el interceptor.");

        protected override DbCommand CreateDbCommand()
            => throw new InvalidOperationException("Los comandos los da el interceptor.");
    }

    private sealed class TransaccionFalsaAlEmpezar(BaseGuionada base_) : DbTransactionInterceptor
    {
        public override InterceptionResult<DbTransaction> TransactionStarting(
            DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result)
        {
            base_.Pasos.Add("BEGIN");
            return InterceptionResult<DbTransaction>.SuppressWithResult(new TransaccionFalsa(base_, connection));
        }

        public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(TransactionStarting(connection, eventData, result));
    }

    private sealed class ComandoFalsoAlCrear(BaseGuionada base_) : DbCommandInterceptor
    {
        public override InterceptionResult<DbCommand> CommandCreating(CommandCorrelatedEventData eventData, InterceptionResult<DbCommand> result)
            => InterceptionResult<DbCommand>.SuppressWithResult(new ComandoFalso(base_));
    }

    private sealed class TransaccionFalsa(BaseGuionada base_, DbConnection connection) : DbTransaction
    {
        private bool terminada;

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        protected override DbConnection DbConnection => connection;

        public override void Commit() => Terminar("COMMIT");

        public override void Rollback() => Terminar("ROLLBACK");

        public override Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Commit();
            return Task.CompletedTask;
        }

        public override Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Rollback();
            return Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            // Como en PostgreSQL: cerrar sin confirmar es deshacer.
            Terminar("ROLLBACK");
            base.Dispose(disposing);
        }

        private void Terminar(string paso)
        {
            if (!terminada)
            {
                terminada = true;
                base_.Pasos.Add(paso);
            }
        }
    }

    private sealed class ComandoFalso(BaseGuionada base_) : DbCommand
    {
        private readonly NpgsqlCommand parametros = new();

        public override string CommandText { get; set; } = "";

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection? DbConnection { get; set; }

        protected override DbParameterCollection DbParameterCollection => parametros.Parameters;

        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel()
        {
        }

        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter() => new NpgsqlParameter();

        public override int ExecuteNonQuery()
        {
            Anotar();
            return -1;
        }

        public override object? ExecuteScalar()
        {
            Anotar();
            return null;
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            Anotar();
            return new DataTable().CreateDataReader();
        }

        private void Anotar()
        {
            base_.Pasos.Add(CommandText.Trim());
            if (base_.FallaAlEjecutar(CommandText) is { } excepcion)
            {
                throw excepcion;
            }
        }
    }
}
