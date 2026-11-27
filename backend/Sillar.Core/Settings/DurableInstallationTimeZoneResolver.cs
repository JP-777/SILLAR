using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sillar.Core.Contracts;
using Sillar.Core.Data;

namespace Sillar.Core.Settings;

/// <summary>Consulta directa: no acredita desde una caché, eventos ni datos del navegador.</summary>
internal sealed class DurableInstallationTimeZoneResolver(
    CoreDbContext database, ITzdbSource source, TimeProvider clock,
    ILogger<DurableInstallationTimeZoneResolver> logger) : IInstallationTimeZoneResolver
{
    public async Task<InstallationTimeZoneResolution> ResolveAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Una sentencia sobre la instalación y sus dos subconsultas: valor y evidencia.
            var rows = await database.Installations.AsNoTracking().Select(i => new
            {
                i.InstallationKey, i.IsSetupComplete,
                Value = database.SiteSettings.Where(s => s.SettingKey == IanaZoneCandidate.SettingKey && s.IsActive)
                    .Select(s => s.SettingValue).FirstOrDefault(),
                Proof = database.TimeZoneAttestations.OrderByDescending(a => a.Id).FirstOrDefault(),
            }).Take(2).ToListAsync(cancellationToken);
            if (rows.Count != 1 || !rows[0].IsSetupComplete)
                throw new InvalidOperationException("No hay una instalación única y terminada.");
            var row = rows[0];
            var setting = IanaZoneCandidate.Evaluate(row.Value);
            TimeZoneFacts? facts = null;
            if (setting.Outcome == IanaZoneOutcome.RuntimeResolvedCandidate)
            {
                var p = row.Proof;
                var proof = p is null ? null : new TimeZoneAttestation(p.AttestationId, p.InstallationKey, p.ZoneId,
                    p.SourceName, p.SourceVersion, p.AttestedByAdminUserLocalId,
                    p.AttestedByAdminUserHomeNode, p.AttestedAt, p.RevokedAt, p.AttestedByName, p.AuthorityVersion);
                facts = new(source.Observe(setting.RuntimeId!), p is null ? Lookup.NotFound : Lookup.Found,
                    proof, Lookup.Found, row.InstallationKey);
            }
            return InstallationTimeZoneEvaluator.Evaluate(setting, IInstallationTimeZoneResolver.InitialZoneId, facts, clock.GetUtcNow());
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(error, "No se pudo consultar la evidencia de zona de la instalación.");
            // La excepción diferencia un fallo de lectura de una zona configurada ausente.
            throw new InvalidOperationException("No se pudo verificar la zona de la instalación.", error);
        }
    }
}
