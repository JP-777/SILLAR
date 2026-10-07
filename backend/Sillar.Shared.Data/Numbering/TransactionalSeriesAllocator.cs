using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Sillar.Shared.Data.Numbering;

/// <summary>
/// Reserva correlativos anuales dentro de la transacción del documento que los consume.
/// </summary>
/// <remarks>
/// Esta es la primitiva común de M03, M05b y M07. No conoce códigos visibles ni
/// dominio: conserva únicamente la guarda transaccional, el año America/Lima y el
/// <c>INSERT ... ON CONFLICT ... DO UPDATE ... RETURNING</c> que impide duplicados
/// y permite que un rollback devuelva el número.
/// </remarks>
public static partial class TransactionalSeriesAllocator
{
    public const string BusinessTimeZone = "America/Lima";

    private static readonly TimeZoneInfo Lima =
        TimeZoneInfo.FindSystemTimeZoneById(BusinessTimeZone);

    public static int YearInLima(DateTimeOffset instant)
        => TimeZoneInfo.ConvertTime(instant, Lima).Year;

    public static async Task<int> ReserveNextAsync(
        DbContext database,
        TransactionalSeriesDefinition definition,
        string scope,
        int year,
        CancellationToken cancellationToken)
    {
        var transaction = database.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "El correlativo debe reservarse dentro de la misma transacción que persiste el documento; fuera de ella un rollback dejaría un hueco.");

        definition.Validate();

        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("El ámbito de la serie no puede estar vacío.", nameof(scope));
        }

        if (year is < 2000 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, "El año de serie debe tener cuatro cifras.");
        }

        var sql = $"""
            INSERT INTO {Quote(definition.Schema)}.{Quote(definition.Table)} AS serie
                ({Quote(definition.ScopeColumn)}, {Quote(definition.YearColumn)}, {Quote(definition.ValueColumn)})
            VALUES (@scope, @year, 1)
            ON CONFLICT ({Quote(definition.ScopeColumn)}, {Quote(definition.YearColumn)})
            DO UPDATE SET {Quote(definition.ValueColumn)} = serie.{Quote(definition.ValueColumn)} + 1
            RETURNING {Quote(definition.ValueColumn)}
            """;

        var connection = database.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction.GetDbTransaction();
        AddParameter(command, "scope", scope);
        AddParameter(command, "year", year);

        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string Quote(string identifier) => $"\"{identifier}\"";

    [GeneratedRegex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    internal static partial Regex SafeIdentifier();
}

/// <summary>Ubicación y columnas de una tabla local de correlativos.</summary>
public sealed record TransactionalSeriesDefinition(
    string Schema,
    string Table,
    string ScopeColumn,
    string YearColumn,
    string ValueColumn)
{
    internal void Validate()
    {
        foreach (var identifier in new[] { Schema, Table, ScopeColumn, YearColumn, ValueColumn })
        {
            if (!TransactionalSeriesAllocator.SafeIdentifier().IsMatch(identifier))
            {
                throw new ArgumentException(
                    $"'{identifier}' no es un identificador PostgreSQL permitido para una serie transaccional.");
            }
        }
    }
}
