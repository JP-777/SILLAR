using Sillar.Core.Contracts;

namespace Sillar.Modules.Reports.Tests;

/// <summary>
/// Resoluciones de zona con la forma del contrato de CORE (<c>1d7c968</c>). Cada
/// llamada a <see cref="ResolveAsync"/> devuelve la siguiente y repite la última,
/// así que una secuencia simula que la zona cambia a mitad de la exportación.
/// </summary>
internal sealed class ZonaDeCore(params Func<InstallationTimeZoneResolution>[] respuestas) : IInstallationTimeZoneResolver
{
    public static readonly Guid AtestacionA = Guid.Parse("0199a6f0-0000-7000-8000-00000000000a");
    public static readonly Guid AtestacionB = Guid.Parse("0199a6f0-0000-7000-8000-00000000000b");
    public static readonly DateTimeOffset Observado = new(2026, 10, 27, 15, 0, 0, TimeSpan.Zero);

    private int llamada;

    public int Llamadas => llamada;

    public Task<InstallationTimeZoneResolution> ResolveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(respuestas[Math.Min(llamada++, respuestas.Length - 1)]());
    }

    public static ZonaDeCore Siempre(InstallationTimeZoneResolution resolucion) => new(() => resolucion);

    public static ZonaDeCore EnOrden(params InstallationTimeZoneResolution[] resoluciones)
        => new([.. resoluciones.Select(r => (Func<InstallationTimeZoneResolution>)(() => r))]);

    /// <summary>Un resolvedor que falla con una avería, no con una cancelación.</summary>
    public static ZonaDeCore Averiada() => new(() => throw new InvalidOperationException("La base de configuración no respondió."));

    /// <summary>La zona inicial de JP: nunca verificada.</summary>
    public static InstallationTimeZoneResolution Inicial()
        => new(TimeZoneInfo.FindSystemTimeZoneById("America/Lima"), "America/Lima", InstallationTimeZoneOrigin.ApprovedInitial,
            TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.InitialZoneNotAttestable,
            new(IanaZoneOutcome.Missing, null, null), null, Observado);

    /// <summary>Configurada, con el estado y el motivo que se pidan; con atestación si se da.</summary>
    public static InstallationTimeZoneResolution Configurada(string id, TimeZoneVerificationStatus estado,
        TimeZoneVerificationReason motivo, Guid? atestacion = null, DateTimeOffset? observado = null, string? original = null)
        => new(TimeZoneInfo.FindSystemTimeZoneById(id), id, InstallationTimeZoneOrigin.ConfiguredSetting, estado, motivo,
            new(IanaZoneOutcome.RuntimeResolvedCandidate, original ?? id, id), atestacion, observado ?? Observado);

    /// <summary>
    /// Verificada. Hoy CORE no la produce: reservada a una autoridad ratificada. El
    /// contrato no define motivo para <c>Verified</c>; el doble usa uno cualquiera,
    /// que M10 no interpreta, solo compara entre lecturas.
    /// </summary>
    public static InstallationTimeZoneResolution Verificada(string id, Guid? atestacion = null, DateTimeOffset? observado = null, string? original = null)
        => Configurada(id, TimeZoneVerificationStatus.Verified, TimeZoneVerificationReason.AttestationAuthorityUnratified,
            atestacion ?? AtestacionA, observado, original);

    public static InstallationTimeZoneResolution Invalida(string valor)
        => new(null, null, null, TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.ConfiguredValueInvalid,
            new(IanaZoneOutcome.Invalid, valor, null), null, Observado);
}

/// <summary>Auditoría de exportaciones que responde lo que se le pide y anota lo que recibe.</summary>
internal sealed class AuditoriaDeCore(ExportAuditResult respuesta) : IExportAudit
{
    public static readonly ExportActor Actor = new(7, "admin@tienda.test", "nodo-1", "Administradora de prueba");

    public List<ExportAuditRequest> Pedidos { get; } = [];
    public Action? AlRegistrar { get; init; }

    public static AuditoriaDeCore Registra() => new(ExportAuditResult.Recorded(Guid.CreateVersion7(), Actor));

    public Task<ExportAuditResult> RecordAsync(ExportAuditRequest request, CancellationToken cancellationToken)
    {
        Pedidos.Add(request);
        AlRegistrar?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(respuesta);
    }
}
