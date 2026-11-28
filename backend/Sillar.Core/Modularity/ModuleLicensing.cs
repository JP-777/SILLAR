using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain;
using Sillar.Core.Services;
using Sillar.Shared.Modularity;

namespace Sillar.Core.Modularity;

/// <summary>
/// Provisionamiento manual de despliegue, separado del interruptor del negocio.
/// La firma de licencias y las condiciones comerciales siguen fuera de WEB v1 (ADR-004).
/// </summary>
internal static class ModuleLicensing
{
    internal const string ConfigurationKey = "Sillar:License:Modules";
    internal const string SourceName = "provisionamiento-servidor:v1";

    /// <summary>Vacío conserva lo existente; una lista explícita reemplaza los derechos locales.</summary>
    internal static HashSet<string>? Parse(string? value, IReadOnlyList<IModule> declared)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var result = new HashSet<string>(StringComparer.Ordinal) { ModuleGraph.CoreCode };
        var byCode = declared.ToDictionary(m => m.Code, StringComparer.Ordinal);
        foreach (var code in value.Split(',').Select(c => c.Trim()))
        {
            if (!byCode.ContainsKey(code))
                throw new InvalidOperationException($"La licencia de despliegue declara un módulo ausente: '{code}'.");
            result.Add(code);
        }
        foreach (var code in result)
            foreach (var dependency in byCode[code].HardDependencies)
                if (!result.Contains(dependency))
                    throw new InvalidOperationException($"La licencia de '{code}' necesita también la licencia de '{dependency}'. No se aplicó el cambio.");
        return result;
    }

    internal static async Task ApplyProvisioningAsync(CoreDbContext database, IReadOnlyList<IModule> declared,
        IConfiguration? configuration, CancellationToken ct)
    {
        var grants = Parse(configuration?[ConfigurationKey], declared);
        if (grants is null) return;
        await using var tx = await database.Database.BeginTransactionAsync(ct);
        // El mismo espacio que activaciones: una activación concurrente no puede
        // basarse en una concesión que el despliegue está retirando.
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})",
            [ModuleActivationService.ActivationLockKey], ct);
        var current = await database.ModuleLicenses.ToDictionaryAsync(l => l.ModuleCode, ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var code in current.Keys.Union(grants).Order(StringComparer.Ordinal))
        {
            var licensed = grants.Contains(code);
            if (current.TryGetValue(code, out var license) && license.IsLicensed == licensed) continue;
            if (license is null)
            {
                license = new ModuleLicense { ModuleCode = code, Source = SourceName };
                database.ModuleLicenses.Add(license);
            }
            license.IsLicensed = licensed;
            license.UpdatedAt = now;
            license.Source = SourceName;
            // Acción del host al cargar el provisionamiento; los tres datos humanos
            // son nulos. No se atribuye a la primera cuenta ni se escribe «Sistema».
            database.AuditLogs.Add(new AuditLog
            {
                OccurredAt = now, Action = AuditAction.Update, ModuleCode = "core",
                EntityType = "module_license", EntityId = code,
                Summary = $"Licencia de '{code}' {(licensed ? "concedida" : "retirada")} por el provisionamiento del despliegue.",
            });
        }
        await database.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    internal static Task<bool> IsLicensedAsync(CoreDbContext database, string code, CancellationToken ct)
        => code == ModuleGraph.CoreCode ? Task.FromResult(true)
            : database.ModuleLicenses.AsNoTracking().AnyAsync(l => l.ModuleCode == code && l.IsLicensed, ct);
}
