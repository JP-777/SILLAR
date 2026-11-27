using Microsoft.EntityFrameworkCore;
using Sillar.Core.Data;

namespace Sillar.Core.Settings;

/// <summary>Lee la última evidencia persistida; no la reconstruye desde cuentas actuales.</summary>
internal sealed class PersistedTimeZoneAttestationStore(CoreDbContext database) : ITimeZoneAttestationStore
{
    public async Task<TimeZoneAttestation?> ReadCurrentAsync(CancellationToken cancellationToken)
    {
        var row = await database.TimeZoneAttestations.AsNoTracking()
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : new(row.AttestationId, row.InstallationKey, row.ZoneId,
            row.SourceName, row.SourceVersion, row.AttestedByAdminUserLocalId,
            row.AttestedByAdminUserHomeNode, row.AttestedAt, row.RevokedAt,
            row.AttestedByName, row.AuthorityVersion);
    }
}
