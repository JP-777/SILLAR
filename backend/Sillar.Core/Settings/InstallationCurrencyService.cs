using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sillar.Core.Auditing;
using Sillar.Core.Contracts;
using Sillar.Core.Contracts.Events;
using Sillar.Core.Data;
using Sillar.Core.Domain;
using Sillar.Shared.Events;

namespace Sillar.Core.Settings;

/// <summary>
/// El texto del registro de auditoría de la moneda. Es también el comprobante que
/// lee <see cref="InstallationCurrencyAuthority"/>, así que su forma es fija.
/// </summary>
internal static partial class InstallationCurrencyAuditText
{
    /// <summary>Lo que se escribe como valor anterior cuando el guardado no es reconocible.</summary>
    internal const string UnrecognizedPrevious = "valor-no-admitido";

    public static string Compose(string previous, string next)
    {
        var shown = InstallationCurrency.Check(previous).Status is CurrencyValueStatus.Supported or CurrencyValueStatus.Pending
            ? previous
            : UnrecognizedPrevious;
        return $"Moneda de la instalación: anterior={shown}; nueva={next}; lista={SupportedCurrencies.Version}.";
    }

    /// <summary>La moneda nueva que dice el registro, o <c>null</c> si no tiene la forma exacta.</summary>
    public static string? NewValue(string? summary)
    {
        if (summary is null)
        {
            return null;
        }

        var match = Pattern().Match(summary);
        return match.Success ? match.Groups["new"].Value : null;
    }

    // El valor anterior solo puede ser lo que escribe Compose: un código de tres
    // letras, el marcador de pendiente o el de no admitido. Nada más.
    [GeneratedRegex(@"^Moneda de la instalación: anterior=(?<old>[A-Z]{3}|PENDIENTE_DEFINIR|valor-no-admitido); nueva=(?<new>[A-Z]{3}); lista=(?<list>[a-z0-9\-]{1,40})\.$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}

/// <summary>
/// La regla de lectura: cuándo hay moneda acreditada. Sin base ni reloj propios.
/// </summary>
internal static class InstallationCurrencyEvaluator
{
    public static InstallationCurrencyReading Evaluate(bool prepared, string? value, string? lastAuditSummary, DateTimeOffset observedAt)
    {
        InstallationCurrencyReading Read(InstallationCurrencyStatus status, SupportedCurrency? currency = null)
            => new(status, currency, SupportedCurrencies.Version, observedAt);

        if (!prepared)
        {
            return Read(InstallationCurrencyStatus.NotPrepared);
        }

        var check = InstallationCurrency.Check(value);
        switch (check.Status)
        {
            case CurrencyValueStatus.Supported:
                break;
            case CurrencyValueStatus.Malformed or CurrencyValueStatus.Unsupported:
                return Read(InstallationCurrencyStatus.Unsupported);
            default:
                return Read(InstallationCurrencyStatus.NotEstablished);
        }

        // Solo la operación administrativa escribe este valor, y lo hace en la
        // misma transacción que su registro. Si el último registro no dice este
        // mismo valor, el valor no salió de ahí.
        return string.Equals(InstallationCurrencyAuditText.NewValue(lastAuditSummary), value, StringComparison.Ordinal)
            ? Read(InstallationCurrencyStatus.Accredited, check.Currency)
            : Read(InstallationCurrencyStatus.NotAttested);
    }
}

/// <summary>Qué hacer con un valor válido, dado lo que hay guardado.</summary>
internal enum CurrencyWriteDecision
{
    /// <summary>Ya es ese valor y está acreditado: no se escribe nada.</summary>
    Unchanged,

    /// <summary>Es otro valor: se cambia, con su registro.</summary>
    Change,

    /// <summary>
    /// Ya es ese valor pero sin comprobante (escrito por fuera): se registra el acto
    /// explícito de super_admin que lo establece. No es automático: es esta operación.
    /// </summary>
    Reattest,
}

/// <summary>La regla de escritura, sin base.</summary>
internal static class InstallationCurrencyWriteRule
{
    public static CurrencyWriteDecision Decide(string previous, string requested, string? latestAuditSummary)
    {
        if (!string.Equals(previous, requested, StringComparison.Ordinal))
        {
            return CurrencyWriteDecision.Change;
        }

        return string.Equals(InstallationCurrencyAuditText.NewValue(latestAuditSummary), previous, StringComparison.Ordinal)
            ? CurrencyWriteDecision.Unchanged
            : CurrencyWriteDecision.Reattest;
    }
}

/// <summary>
/// El último registro de auditoría de la clave de moneda, para la escritura. El lector
/// lleva el mismo filtro en línea; una prueba compara los dos.
/// </summary>
internal static class InstallationCurrencyAuditQuery
{
    public static IQueryable<string?> LatestSummary(CoreDbContext database)
        => database.AuditLogs
            .Where(log => log.EntityType == InstallationCurrencyService.AuditEntityType
                && log.EntityId == InstallationCurrency.SettingKey
                && log.Action == AuditAction.Update)
            .OrderByDescending(log => log.AuditLogId)
            .Select(log => log.Summary);
}

/// <summary>Una fila de hechos: el valor guardado y el último registro de auditoría de la clave.</summary>
internal sealed record InstallationCurrencyFacts(string SettingValue, string? LastAuditSummary);

/// <summary>Productor de <see cref="IInstallationCurrencyAuthority"/>: lee la base en cada llamada.</summary>
internal sealed class InstallationCurrencyAuthority(
    CoreDbContext database,
    TimeProvider clock,
    ILogger<InstallationCurrencyAuthority> logger) : IInstallationCurrencyAuthority
{
    /// <inheritdoc />
    public Task<InstallationCurrencyReading> ReadAsync(CancellationToken cancellationToken)
        => InstallationCurrencyRead.ReadAsync(database, clock, logger, cancellationToken);

    /// <summary>La consulta de la lectura, sobre el contexto de CORE.</summary>
    internal IQueryable<InstallationCurrencyFacts> FactsQuery() => InstallationCurrencyRead.FactsQuery(database);
}

/// <summary>
/// La lectura acreditada, escrita una sola vez: la usan el lector de CORE sobre su
/// propio contexto y el lector transaccional sobre la conexión del llamador.
/// </summary>
internal static class InstallationCurrencyRead
{
    /// <summary>Lee y evalúa. La cancelación se propaga; cualquier otro fallo es <c>Unavailable</c>.</summary>
    public static async Task<InstallationCurrencyReading> ReadAsync(
        CoreDbContext database,
        TimeProvider clock,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        InstallationCurrencyFacts? facts;
        try
        {
            facts = await FactsQuery(database).SingleOrDefaultAsync(cancellationToken);
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            throw exception as OperationCanceledException
                ?? new OperationCanceledException("Lectura cancelada por el llamador.", exception, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "No se pudo leer la moneda de la instalación.");
            return new(InstallationCurrencyStatus.Unavailable, null, SupportedCurrencies.Version, clock.GetUtcNow());
        }

        return InstallationCurrencyEvaluator.Evaluate(facts is not null, facts?.SettingValue, facts?.LastAuditSummary, clock.GetUtcNow());
    }

    /// <summary>Una sola sentencia: la fila de la clave y, como subconsulta, su último registro de auditoría.</summary>
    public static IQueryable<InstallationCurrencyFacts> FactsQuery(CoreDbContext database)
        => database.SiteSettings
            .AsNoTracking()
            .Where(setting => setting.SettingKey == InstallationCurrency.SettingKey)
            .Select(setting => new InstallationCurrencyFacts(
                setting.SettingValue,
                // En línea a propósito: un método estático aquí EF lo ejecutaría aparte,
                // como consulta propia. Mismo filtro que InstallationCurrencyAuditQuery.
                database.AuditLogs
                    .Where(log => log.EntityType == InstallationCurrencyService.AuditEntityType
                        && log.EntityId == InstallationCurrency.SettingKey
                        && log.Action == AuditAction.Update)
                    .OrderByDescending(log => log.AuditLogId)
                    .Select(log => log.Summary)
                    .FirstOrDefault()));
}

/// <summary>Cómo terminó un intento de establecer la moneda.</summary>
internal enum CurrencyChangeOutcome
{
    /// <summary>Guardada, con su auditoría.</summary>
    Changed,

    /// <summary>Ya tenía ese valor sin comprobante; ahora tiene el registro de este acto.</summary>
    Reattested,

    /// <summary>Ya era esa y estaba acreditada. No se escribió nada.</summary>
    Unchanged,

    /// <summary>Hace falta <c>super_admin</c>.</summary>
    Forbidden,

    /// <summary>El valor no es una moneda de la lista.</summary>
    Invalid,

    /// <summary>No hay forma de comprobar si existen pedidos.</summary>
    RecordsUnverifiable,

    /// <summary>Ya hay pedidos.</summary>
    RecordsPresent,

    /// <summary>La clave no está preparada en esta instalación.</summary>
    NotPrepared,

    /// <summary>El escritor de auditoría no trabaja sobre el mismo contexto: no sería atómico.</summary>
    AuditNotAtomic,

    /// <summary>No se pudo guardar el cambio con su auditoría. No quedó nada.</summary>
    NotSaved,

    /// <summary>Hay una operación de venta en curso con el bloqueo de moneda; no se esperó más.</summary>
    MonetaryLockBusy,
}

/// <summary>Resultado del intento.</summary>
internal sealed record CurrencyChangeResult(CurrencyChangeOutcome Outcome, string? Message, SupportedCurrency? Currency = null);

/// <summary>
/// Operación administrativa: establecer o cambiar la moneda de la instalación.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cambio y auditoría, o nada.</b> La fila se bloquea (<c>FOR UPDATE</c>), se lee
/// el valor anterior, se cambia, y el registro con los dos valores se prepara en el
/// mismo contexto con <see cref="AuditWriter.Stage"/>: un solo <c>SaveChanges</c>
/// dentro de una transacción explícita. Si falla, se deshace todo.
/// </para>
/// <para>
/// <b>No cambia la moneda si no puede comprobar que no hay pedidos.</b> Eso lo sabe
/// el dueño de los pedidos, no CORE: se pregunta a <see cref="IMonetaryRecordsProbe"/>,
/// y sin ninguno registrado, o sin respuesta firme, la respuesta es no.
/// </para>
/// <para>
/// <b>Pedir la moneda que ya hay no es cambiarla</b> (O-2, ratificada). Si el valor
/// guardado es exactamente el pedido y el último comprobante de auditoría lo acredita,
/// el resultado es <see cref="CurrencyChangeOutcome.Unchanged"/> aunque existan pedidos:
/// no se escribe, no se acredita de nuevo y no se avisa. Se decide bajo el bloqueo
/// exclusivo, con la fila releída tras <c>FOR UPDATE</c>. Con pedidos sigue rechazándose
/// todo lo que escribiría: cambiar de moneda y acreditar un valor sin comprobante.
/// </para>
/// <para>
/// <b>Barrera con Ventas.</b> Orden fijo dentro de la transacción: tiempo de espera de
/// bloqueos, <see cref="MonetaryAuthorityLock.AcquireExclusive"/>, la sonda, y solo
/// entonces <c>FOR UPDATE</c> de la fila. Un pedido que tenga el bloqueo compartido
/// termina antes de que CORE lo obtenga, y la sonda ya lo ve; uno que llegue después
/// espera a que CORE confirme o deshaga. El bloqueo se suelta al terminar la transacción.
/// </para>
/// </remarks>
internal sealed class InstallationCurrencyService(
    CoreDbContext database,
    AuditWriter auditWriter,
    SettingsCache cache,
    IEventPublisher events,
    IServiceProvider services,
    TimeProvider clock,
    ILogger<InstallationCurrencyService> logger)
{
    /// <summary>Tipo de entidad en <c>core.audit_log</c>, el mismo que el resto de la configuración.</summary>
    internal const string AuditEntityType = "setting";

    /// <summary>
    /// Cuánto espera CORE el bloqueo de moneda (y cualquier otro de su transacción).
    /// Si una venta lo retiene más, la operación se rechaza y se puede reintentar.
    /// </summary>
    internal const string LockTimeoutSql = "SET LOCAL lock_timeout = '5s'";

    public async Task<CurrencyChangeResult> SetAsync(
        string? value,
        bool actorIsSuperAdmin,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!actorIsSuperAdmin)
        {
            return new(CurrencyChangeOutcome.Forbidden, "Establecer la moneda de la instalación exige el rol super_admin.");
        }

        var check = InstallationCurrency.Check(value);
        if (!check.IsSupported)
        {
            return new(CurrencyChangeOutcome.Invalid, InvalidMessage(check.Status));
        }

        if (!ReferenceEquals(auditWriter.Context, database))
        {
            return new(CurrencyChangeOutcome.AuditNotAtomic,
                "No se guardó la moneda: su auditoría no iría en la misma transacción que el cambio.");
        }

        // Dependencia blanda: sin nadie que informe de los pedidos no se puede saber,
        // y no hace falta ni tocar la base para responder que no.
        if (services.GetService<IMonetaryRecordsProbe>() is not { } probe)
        {
            return Unverifiable();
        }

        SiteSetting? setting = null;
        AuditLog? staged = null;
        string? previous = null;
        var decision = CurrencyWriteDecision.Change;
        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

            // 1. Barrera con Ventas, antes de mirar nada.
            await database.Database.ExecuteSqlRawAsync(LockTimeoutSql, cancellationToken);
            await database.Database.ExecuteSqlAsync(MonetaryAuthorityLock.AcquireExclusive(), cancellationToken);

            // 2. Con el bloqueo puesto: ningún pedido puede estar a medio crear. Sin una
            //    respuesta firme de Ventas no se sigue. Con ella, el veredicto sobre los
            //    pedidos se aplica después de mirar la fila (O-2, ratificada por JP).
            var records = await ProbeRecordsAsync(probe, cancellationToken);
            if (records is not (MonetaryRecordsState.None or MonetaryRecordsState.Present))
            {
                return Unverifiable();
            }

            // 3. La fila, siempre después de la barrera.
            await database.Database.ExecuteSqlAsync(
                $"SELECT 1 FROM core.site_settings WHERE setting_key = {InstallationCurrency.SettingKey} FOR UPDATE",
                cancellationToken);

            setting = await database.SiteSettings
                .FirstOrDefaultAsync(candidate => candidate.SettingKey == InstallationCurrency.SettingKey, cancellationToken);

            if (setting is not null)
            {
                // Si la fila ya estaba en el contexto, se relee: bajo el bloqueo manda la base.
                await database.Entry(setting).ReloadAsync(cancellationToken);
                previous = setting.SettingValue;

                // Igual al pedido no basta: tiene que estar acreditado. Si no lo está, se
                // registra este acto en vez de responder que ya estaba.
                var latest = await InstallationCurrencyAuditQuery.LatestSummary(database).FirstOrDefaultAsync(cancellationToken);
                decision = InstallationCurrencyWriteRule.Decide(previous, value!, latest);

                // 4. Ya es esa moneda y su comprobante lo dice: no hay nada que escribir,
                //    ni acreditación nueva, ni aviso. Vale también con pedidos, porque no
                //    cambia lo que esos pedidos dieron por moneda.
                if (decision == CurrencyWriteDecision.Unchanged)
                {
                    return new(CurrencyChangeOutcome.Unchanged, null, check.Currency);
                }
            }

            // 5. Todo lo demás escribe —cambiar, o acreditar un valor sin comprobante— y
            //    con pedidos no se escribe.
            if (records == MonetaryRecordsState.Present)
            {
                return new(CurrencyChangeOutcome.RecordsPresent,
                    "Ya hay pedidos registrados con importe: la moneda de la instalación no se cambia.");
            }

            if (setting is null)
            {
                return new(CurrencyChangeOutcome.NotPrepared,
                    "La clave de la moneda no está preparada en esta instalación: falta aplicar la migración CoreInstallationCurrency.");
            }

            setting.SettingValue = value!;
            setting.IsPublic = false;
            staged = auditWriter.Stage(new AuditEntry(AuditAction.Update)
            {
                AdminUserId = actingUserId,
                AdminUserEmail = actingEmail,
                ModuleCode = CoreModule.ModuleCode,
                EntityType = AuditEntityType,
                EntityId = InstallationCurrency.SettingKey,
                // Hay fila, así que el valor anterior se leyó arriba, bajo el bloqueo.
                Summary = InstallationCurrencyAuditText.Compose(previous!, value!),
            });

            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            Forget(setting, previous, staged);
            throw exception as OperationCanceledException
                ?? new OperationCanceledException("Cambio de moneda cancelado por el llamador.", exception, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            Forget(setting, previous, staged);
            logger.LogInformation(exception, "El bloqueo de moneda no quedó libre a tiempo; no se cambió nada.");
            return new(CurrencyChangeOutcome.MonetaryLockBusy,
                "Hay una venta en curso usando la moneda: no se cambió nada. Inténtalo de nuevo en unos segundos.");
        }
        catch (Exception exception)
        {
            Forget(setting, previous, staged);
            logger.LogWarning(exception, "No se pudo guardar la moneda de la instalación con su auditoría; no quedó nada.");
            return new(CurrencyChangeOutcome.NotSaved,
                "No se guardó la moneda: no se pudo registrar su auditoría junto al cambio. Inténtalo de nuevo.");
        }

        await AnnounceAsync(cancellationToken);
        return new(
            decision == CurrencyWriteDecision.Reattest ? CurrencyChangeOutcome.Reattested : CurrencyChangeOutcome.Changed,
            null,
            check.Currency);
    }

    /// <summary>
    /// Lo que viene después del commit. <b>No lanza:</b> la moneda ya está guardada, y
    /// un fallo al avisar —o una cancelación que llega tarde— no puede hacer creer
    /// que no se guardó. El aviso se intenta una vez con el token del llamador, como
    /// en el resto de la configuración; si no llega, queda en el registro.
    /// </summary>
    internal async Task AnnounceAsync(CancellationToken cancellationToken)
    {
        cache.Invalidate();

        try
        {
            await events.PublishAsync(new SettingChanged(InstallationCurrency.SettingKey, false, clock.GetUtcNow()), cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "La moneda de la instalación se guardó, pero no se pudo avisar de {Evento}.", nameof(SettingChanged));
        }
    }

    private static CurrencyChangeResult Unverifiable()
        => new(CurrencyChangeOutcome.RecordsUnverifiable,
            "No se puede establecer la moneda: todavía no hay forma de comprobar si existen pedidos. " +
            "Hace falta que el módulo de ventas lo informe.");

    private async Task<MonetaryRecordsState> ProbeRecordsAsync(IMonetaryRecordsProbe probe, CancellationToken cancellationToken)
    {
        try
        {
            return await probe.ProbeAsync(cancellationToken);
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            throw exception as OperationCanceledException
                ?? new OperationCanceledException("Cambio de moneda cancelado por el llamador.", exception, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "El informe de pedidos falló; la moneda no se cambia.");
            return MonetaryRecordsState.Unknown;
        }
    }

    /// <summary>
    /// Deja el contexto como estaba: ningún guardado posterior de la misma petición
    /// debe reintentar a escondidas el cambio o su registro.
    /// </summary>
    private void Forget(SiteSetting? setting, string? previous, AuditLog? staged)
    {
        if (staged is not null)
        {
            database.Entry(staged).State = EntityState.Detached;
        }

        if (setting is not null)
        {
            if (previous is not null)
            {
                setting.SettingValue = previous;
            }

            database.Entry(setting).State = EntityState.Detached;
        }
    }

    internal static string InvalidMessage(CurrencyValueStatus status) => status switch
    {
        CurrencyValueStatus.Missing or CurrencyValueStatus.Empty =>
            "Falta la moneda: escribe su código ISO 4217, por ejemplo PEN.",
        CurrencyValueStatus.Pending =>
            "Ese valor marca la moneda como pendiente; escribe el código ISO 4217 de la moneda.",
        CurrencyValueStatus.Malformed =>
            "El código de moneda son tres letras mayúsculas, sin espacios, por ejemplo PEN.",
        _ =>
            $"Esa moneda no está entre las que el producto soporta ({string.Join(", ", SupportedCurrencies.All.Select(c => c.Code))}).",
    };
}

/// <summary>Registro de la operación y del lector. Lo llama <c>CoreServices</c>.</summary>
internal static class InstallationCurrencyRegistration
{
    public static IServiceCollection AddInstallationCurrency(this IServiceCollection services)
    {
        services.AddScoped<InstallationCurrencyService>();
        services.AddScoped<IInstallationCurrencyAuthority, InstallationCurrencyAuthority>();
        services.AddScoped<IInstallationCurrencyTransactionalAuthority, InstallationCurrencyTransactionalAuthority>();
        return services;
    }
}
