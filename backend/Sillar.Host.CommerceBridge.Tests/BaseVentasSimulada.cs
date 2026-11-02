using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Sillar.Host.CommerceBridge.Tests;

/// <summary>Un pedido tal como lo vería la base.</summary>
public sealed record PedidoSimulado(
    Guid OrderId, string OrderCode, string Status, decimal TotalAmount, string? CurrencyCode, bool IsActive, DateTimeOffset CreatedAt);

/// <summary>Un pago tal como lo vería la base.</summary>
public sealed record PagoSimulado(
    Guid OrderId, decimal Amount, string? Source, string? CurrencyCode, string Method, bool WasLate, DateTimeOffset RegisteredAt);

/// <summary>
/// Sustituye a PostgreSQL —infraestructura externa, no autorizada— sin sustituir a M03:
/// el SQL que llega aquí lo genera EF desde las consultas reales de los lectores y del
/// evaluador. Responde solo a las formas que esos productores emiten; cualquier otra,
/// o cualquier escritura, transacción o SaveChanges, falla y se cuenta.
/// </summary>
/// <remarks>
/// Las lecturas por fila siguen el simulador acreditado de G6-1B-A (Claude B), copiado en
/// <c>Sillar.Modules.Payments.Tests/M03CommercialReadOnlyEvaluationConsumerTests.cs</c>.
/// Las tres agregadas son nuevas en R5 y reproducen el SQL capturado de
/// <c>LectorAgregadoDePedidos</c>, <c>LectorDeValorDePedidos</c> y <c>LectorAgregadoDePagos</c>.
/// </remarks>
public sealed class BaseVentasSimulada
    : DbConnectionInterceptor, IDbCommandInterceptor, ISaveChangesInterceptor, IDbTransactionInterceptor
{
    private static readonly Regex Mutante = new(
        @"\b(INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|TRUNCATE|GRANT|REVOKE|COPY|CALL|DO|LOCK|VACUUM)\b"
            + @"|\bnextval\b|\bsetval\b|pg_advisory|\bFOR\s+(UPDATE|SHARE|NO\s+KEY|KEY)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, Type> Tipos = new(StringComparer.Ordinal)
    {
        ["order_id"] = typeof(Guid),
        ["order_code"] = typeof(string),
        ["status"] = typeof(string),
        ["total_amount"] = typeof(decimal),
        ["currency_code"] = typeof(string),
        ["is_active"] = typeof(bool),
        ["amount"] = typeof(decimal),
        ["source"] = typeof(string),
    };

    public List<PedidoSimulado> Pedidos { get; } = [];

    public List<PagoSimulado> Pagos { get; } = [];

    public List<string> Consultas { get; } = [];

    public int ComandosProhibidos { get; private set; }

    public int Guardados { get; private set; }

    public int Transacciones { get; private set; }

    /// <summary>Se llama tras responder la consulta n-ésima: simula un cambio entre lecturas.</summary>
    public Action<int>? TrasConsulta { get; set; }

    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        => InterceptionResult.Suppress();

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(InterceptionResult.Suppress());

    public override InterceptionResult ConnectionClosing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        => InterceptionResult.Suppress();

    public override ValueTask<InterceptionResult> ConnectionClosingAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        => ValueTask.FromResult(InterceptionResult.Suppress());

    public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(Responder(command)));

    public InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        => InterceptionResult<DbDataReader>.SuppressWithResult(Responder(command));

    public ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        => throw Prohibido(command);

    public InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        => throw Prohibido(command);

    public ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        => throw Prohibido(command);

    public InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        => throw Prohibido(command);

    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guardados++;
        throw new InvalidOperationException("SaveChanges inesperado.");
    }

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Guardados++;
        throw new InvalidOperationException("SaveChanges inesperado.");
    }

    public InterceptionResult<DbTransaction> TransactionStarting(
        DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result)
    {
        Transacciones++;
        throw new InvalidOperationException("Transacción inesperada.");
    }

    public ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
        DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result,
        CancellationToken cancellationToken = default)
    {
        Transacciones++;
        throw new InvalidOperationException("Transacción inesperada.");
    }

    private InvalidOperationException Prohibido(DbCommand command)
    {
        ComandosProhibidos++;
        return new InvalidOperationException($"Comando no permitido: {command.CommandText}");
    }

    private static (DateTimeOffset Inicio, DateTimeOffset Fin) Ventana(DbCommand command)
    {
        var valores = command.Parameters.Cast<DbParameter>().Select(p => p.Value switch
        {
            DateTimeOffset d => d,
            DateTime d => new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)),
            _ => throw new InvalidOperationException("Parámetro de ventana inesperado."),
        }).ToArray();
        return valores is [var inicio, var fin] ? (inicio, fin) : throw new InvalidOperationException("Ventana sin dos límites.");
    }

    private DbDataReader Responder(DbCommand command)
    {
        var sql = command.CommandText;
        if (!sql.TrimStart().StartsWith("SELECT", StringComparison.Ordinal) || Mutante.IsMatch(sql))
        {
            throw Prohibido(command);
        }

        Consultas.Add(sql);
        var tabla = sql.Contains("GROUP BY", StringComparison.Ordinal) ? Agregada(command, sql) : PorFila(command, sql);
        var lector = tabla.CreateDataReader();
        TrasConsulta?.Invoke(Consultas.Count);
        return lector;
    }

    /// <summary>Las tres agregadas de M03, con el mismo orden de columnas que el SQL generado.</summary>
    private DataTable Agregada(DbCommand command, string sql)
    {
        var (inicio, fin) = Ventana(command);
        var tabla = new DataTable();
        if (sql.Contains("FROM sales.orders", StringComparison.Ordinal)
            && sql.Contains("GROUP BY o.status, o.is_active", StringComparison.Ordinal))
        {
            tabla.Columns.Add("status", typeof(string));
            tabla.Columns.Add("is_active", typeof(bool));
            tabla.Columns.Add("count", typeof(long));
            foreach (var g in Pedidos.Where(p => p.CreatedAt >= inicio && p.CreatedAt < fin).GroupBy(p => (p.Status, p.IsActive)))
            {
                tabla.Rows.Add(g.Key.Status, g.Key.IsActive, (long)g.Count());
            }
        }
        else if (sql.Contains("FROM sales.orders", StringComparison.Ordinal)
            && sql.Contains("GROUP BY o.currency_code, o.is_active", StringComparison.Ordinal))
        {
            tabla.Columns.Add("currency_code", typeof(string));
            tabla.Columns.Add("is_active", typeof(bool));
            tabla.Columns.Add("count", typeof(long));
            tabla.Columns.Add("sum", typeof(decimal));
            foreach (var g in Pedidos.Where(p => p.CreatedAt >= inicio && p.CreatedAt < fin).GroupBy(p => (p.CurrencyCode, p.IsActive)))
            {
                tabla.Rows.Add((object?)g.Key.CurrencyCode ?? DBNull.Value, g.Key.IsActive, (long)g.Count(), g.Sum(p => p.TotalAmount));
            }
        }
        else if (sql.Contains("FROM sales.order_payments", StringComparison.Ordinal)
            && sql.Contains("GROUP BY o.source, o.method, o.currency_code, o.was_late", StringComparison.Ordinal))
        {
            tabla.Columns.Add("source", typeof(string));
            tabla.Columns.Add("method", typeof(string));
            tabla.Columns.Add("currency_code", typeof(string));
            tabla.Columns.Add("was_late", typeof(bool));
            tabla.Columns.Add("count", typeof(long));
            tabla.Columns.Add("sum", typeof(decimal));
            foreach (var g in Pagos.Where(p => p.RegisteredAt >= inicio && p.RegisteredAt < fin)
                         .GroupBy(p => (p.Source, p.Method, p.CurrencyCode, p.WasLate)))
            {
                tabla.Rows.Add((object?)g.Key.Source ?? DBNull.Value, g.Key.Method, (object?)g.Key.CurrencyCode ?? DBNull.Value,
                    g.Key.WasLate, (long)g.Count(), g.Sum(p => p.Amount));
            }
        }
        else
        {
            throw Prohibido(command);
        }

        return tabla;
    }

    /// <summary>Las lecturas por fila de la correlación y del evaluador, como el simulador acreditado.</summary>
    private DataTable PorFila(DbCommand command, string sql)
    {
        var valor = command.Parameters.Cast<DbParameter>().Single().Value;
        var columnas = Columnas(sql);
        var tabla = new DataTable();
        if (sql.Contains("FROM sales.order_payments", StringComparison.Ordinal))
        {
            Llenar(tabla, columnas, Pagos.Where(p => valor is Guid id && p.OrderId == id).Select(p => new Dictionary<string, object?>
            {
                ["amount"] = p.Amount,
                ["source"] = p.Source,
                ["currency_code"] = p.CurrencyCode,
            }));
        }
        else if (sql.Contains("FROM sales.orders", StringComparison.Ordinal))
        {
            Llenar(tabla, columnas, Pedidos.Where(p => valor switch
            {
                string codigo => string.Equals(p.OrderCode, codigo, StringComparison.Ordinal),
                Guid id => p.OrderId == id,
                _ => false,
            }).Select(p => new Dictionary<string, object?>
            {
                ["order_id"] = p.OrderId,
                ["order_code"] = p.OrderCode,
                ["status"] = p.Status,
                ["total_amount"] = p.TotalAmount,
                ["currency_code"] = p.CurrencyCode,
                ["is_active"] = p.IsActive,
            }));
        }
        else
        {
            throw Prohibido(command);
        }

        return tabla;
    }

    private static string[] Columnas(string sql)
    {
        var inicio = sql.IndexOf("SELECT", StringComparison.Ordinal) + "SELECT".Length;
        var fin = sql.IndexOf("FROM", inicio, StringComparison.Ordinal);
        return sql[inicio..fin]
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Split(" AS ", StringSplitOptions.TrimEntries)[0])
            .Select(c => c[(c.LastIndexOf('.') + 1)..])
            .ToArray();
    }

    private static void Llenar(DataTable tabla, string[] columnas, IEnumerable<Dictionary<string, object?>> filas)
    {
        foreach (var columna in columnas)
        {
            tabla.Columns.Add(columna, Tipos[columna]);
        }

        foreach (var fila in filas)
        {
            tabla.Rows.Add(columnas.Select(c => fila[c] ?? DBNull.Value).ToArray());
        }
    }
}
