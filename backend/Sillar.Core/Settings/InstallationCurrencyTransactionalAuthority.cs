using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Shared.Replication;

namespace Sillar.Core.Settings;

/// <summary>
/// Productor de <see cref="IInstallationCurrencyTransactionalAuthority"/>: la lectura
/// acreditada de CORE, ejecutada en la conexión y la transacción del llamador.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cómo evita la segunda conexión.</b> Construye un <see cref="CoreDbContext"/> de vida
/// corta cuyas opciones solo conocen la conexión de la transacción recibida
/// (<see cref="CoreDataServiceExtensions.BuildOptions(DbConnection)"/>), y se une a esa
/// transacción con <c>UseTransaction</c>. El contexto de CORE del ámbito no se usa
/// para leer: solo se consulta su cadena, sin abrirla, para saber cuál es la base de CORE.
/// </para>
/// <para>
/// <b>Misma lectura, no una copia.</b> La consulta y la evaluación son las de
/// <see cref="InstallationCurrencyRead"/>, las mismas que usa
/// <see cref="InstallationCurrencyAuthority"/>.
/// </para>
/// <para>
/// <b>Sin efectos sobre lo ajeno.</b> Ni <c>Commit</c>, ni <c>Rollback</c>, ni <c>Open</c>,
/// ni <c>Close</c>, ni <c>SaveChanges</c>, ni ningún bloqueo: dos sentencias de lectura
/// —la comprobación de la transacción y la lectura acreditada— y nada más.
/// </para>
/// </remarks>
internal sealed class InstallationCurrencyTransactionalAuthority(
    CoreDbContext core,
    NodeIdentity node,
    TimeProvider clock,
    ILogger<InstallationCurrencyTransactionalAuthority> logger) : IInstallationCurrencyTransactionalAuthority
{
    /// <summary>El único aislamiento en el que la lectura ve lo confirmado después del bloqueo.</summary>
    internal const string RequiredIsolation = "read committed";

    /// <inheritdoc />
    public async Task<InstallationCurrencyReading> ReadInTransactionAsync(DbTransaction transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();

        // Una transacción confirmada o deshecha ya no tiene conexión.
        var connection = transaction.Connection
            ?? throw new InvalidOperationException(
                "La transacción comercial ya terminó: la moneda se lee dentro de la transacción viva que tiene el bloqueo.");

        // CORE no abre la conexión de otro: si la abriera, no sería la del bloqueo.
        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException(
                $"La conexión de la transacción comercial no está abierta ({connection.State}); CORE no la abre.");
        }

        var coreDatabase = CoreDatabaseName();

        await using var borrowed = new CoreDbContext(CoreDataServiceExtensions.BuildOptions(connection), node, clock);
        borrowed.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        borrowed.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        await borrowed.Database.UseTransactionAsync(transaction, cancellationToken);

        TransactionCheck check;
        try
        {
            check = (await borrowed.Database.SqlQuery<TransactionCheck>(CheckSql()).ToListAsync(cancellationToken)).Single();
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            throw exception as OperationCanceledException
                ?? new OperationCanceledException("Lectura cancelada por el llamador.", exception, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "No se pudo comprobar la transacción comercial; la moneda no está disponible.");
            return new(InstallationCurrencyStatus.Unavailable, null, SupportedCurrencies.Version, clock.GetUtcNow());
        }

        Require(check, coreDatabase);

        return await InstallationCurrencyRead.ReadAsync(borrowed, clock, logger, cancellationToken);
    }

    /// <summary>
    /// Aislamiento, base y bloqueo de moneda concedido a esta misma sesión, en una sola
    /// lectura. Solo mira <c>pg_locks</c>: no toma ni espera ningún bloqueo.
    /// </summary>
    /// <remarks>
    /// Forma de dos claves <c>int4</c>: PostgreSQL la muestra con <c>classid</c> = primera
    /// clave, <c>objid</c> = segunda y <c>objsubid</c> = 2.
    /// </remarks>
    internal static FormattableString CheckSql()
        => $"""
            SELECT current_setting('transaction_isolation') AS "Isolation",
                   current_database() AS "Database",
                   EXISTS (
                       SELECT 1
                       FROM pg_catalog.pg_locks AS l
                       WHERE l.locktype = 'advisory'
                         AND l.pid = pg_catalog.pg_backend_pid()
                         AND l.granted
                         AND l.database = (SELECT d.oid FROM pg_catalog.pg_database AS d WHERE d.datname = current_database())
                         AND l.classid = CAST({MonetaryAuthorityLock.Namespace} AS oid)
                         AND l.objid = CAST({MonetaryAuthorityLock.CurrencyAuthority} AS oid)
                         AND l.objsubid = 2
                         AND l.mode IN ('ShareLock', 'ExclusiveLock')
                   ) AS "LockHeld"
            """;

    /// <summary>Lo que exige el contrato a la transacción. Falla con un error de uso, nunca con «sin moneda».</summary>
    internal static void Require(TransactionCheck check, string? coreDatabase)
    {
        if (!string.Equals(check.Isolation, RequiredIsolation, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"La transacción comercial está en '{check.Isolation}': la moneda solo se lee en '{RequiredIsolation}', " +
                "porque con más aislamiento la instantánea es anterior al bloqueo de moneda.");
        }

        if (coreDatabase is null || !string.Equals(check.Database, coreDatabase, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "La transacción comercial no está en la base de CORE: su moneda no es la de esta instalación.");
        }

        if (!check.LockHeld)
        {
            throw new InvalidOperationException(
                "La transacción comercial no tiene el bloqueo de moneda: tómalo con MonetaryAuthorityLock.AcquireShared " +
                "antes de leer la moneda.");
        }
    }

    /// <summary>La base de CORE según su cadena. No abre nada.</summary>
    private string? CoreDatabaseName()
    {
        var cadena = core.Database.GetConnectionString();
        return string.IsNullOrEmpty(cadena) ? null : new NpgsqlConnectionStringBuilder(cadena).Database;
    }
}

/// <summary>Una fila de <see cref="InstallationCurrencyTransactionalAuthority.CheckSql"/>.</summary>
internal sealed class TransactionCheck
{
    public string Isolation { get; set; } = "";

    public string Database { get; set; } = "";

    public bool LockHeld { get; set; }
}
