using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    public async Task<InstallationCurrencyReading> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        InstallationCurrencyFacts? facts;
        try
        {
            facts = await FactsQuery().SingleOrDefaultAsync(cancellationToken);
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
    internal IQueryable<InstallationCurrencyFacts> FactsQuery()
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
/// y sin ninguno registrado la respuesta es no.
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

        var records = await ProbeRecordsAsync(cancellationToken);
        if (records == MonetaryRecordsState.Present)
        {
            return new(CurrencyChangeOutcome.RecordsPresent,
                "Ya hay pedidos registrados con importe: la moneda de la instalación no se cambia.");
        }

        if (records != MonetaryRecordsState.None)
        {
            return new(CurrencyChangeOutcome.RecordsUnverifiable,
                "No se puede establecer la moneda: todavía no hay forma de comprobar si existen pedidos. " +
                "Hace falta que el módulo de ventas lo informe.");
        }

        SiteSetting? setting = null;
        AuditLog? staged = null;
        string? previous = null;
        var decision = CurrencyWriteDecision.Change;
        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

            await database.Database.ExecuteSqlAsync(
                $"SELECT 1 FROM core.site_settings WHERE setting_key = {InstallationCurrency.SettingKey} FOR UPDATE",
                cancellationToken);

            setting = await database.SiteSettings
                .FirstOrDefaultAsync(candidate => candidate.SettingKey == InstallationCurrency.SettingKey, cancellationToken);

            if (setting is null)
            {
                return new(CurrencyChangeOutcome.NotPrepared,
                    "La clave de la moneda no está preparada en esta instalación: falta aplicar la migración CoreInstallationCurrency.");
            }

            // Si la fila ya estaba en el contexto, se relee: bajo el bloqueo manda la base.
            await database.Entry(setting).ReloadAsync(cancellationToken);
            previous = setting.SettingValue;

            // Igual al pedido no basta: tiene que estar acreditado. Si no lo está, se
            // registra este acto en vez de responder que ya estaba.
            var latest = await InstallationCurrencyAuditQuery.LatestSummary(database).FirstOrDefaultAsync(cancellationToken);
            decision = InstallationCurrencyWriteRule.Decide(previous, value!, latest);
            if (decision == CurrencyWriteDecision.Unchanged)
            {
                return new(CurrencyChangeOutcome.Unchanged, null, check.Currency);
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
                Summary = InstallationCurrencyAuditText.Compose(previous, value!),
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

    private async Task<MonetaryRecordsState> ProbeRecordsAsync(CancellationToken cancellationToken)
    {
        // Dependencia blanda: si nadie lo informa, no se puede saber.
        if (services.GetService<IMonetaryRecordsProbe>() is not { } probe)
        {
            return MonetaryRecordsState.Unknown;
        }

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
        return services;
    }
}
