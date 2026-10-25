using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Auditing;
using Sillar.Core.Settings;

namespace Sillar.Host.ReportsBridge;

/// <summary>
/// Composición candidata y opt-in de los productores CORE que consume M10.
/// Ninguna entrada productiva la invoca durante FASE A.
/// </summary>
public static class ReportsHostServiceCollectionExtensions
{
    /// <summary>
    /// Conecta los puertos reales de zona y auditoría. No registra una autoridad
    /// de verificación de zona ni sustituye identidad, almacenamiento o auditoría.
    /// </summary>
    public static IServiceCollection AddReportsCoreCandidates(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCandidateInstallationTimeZone();
        services.AddCandidateExportAudit();
        return services;
    }
}
