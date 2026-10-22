using System.Security.Cryptography;
using System.Text;

namespace Sillar.Modules.Payments.Infrastructure.Persistence.Entities;

/// <summary>
/// Exclusión técnica para observaciones que comparten literalmente un
/// <c>external_settlement_id</c>. La valla no declara que el identificador sea
/// global ni que represente la identidad comercial de M03: sólo obliga a que
/// M11 observe y clasifique esas recepciones en secuencia.
/// </summary>
internal sealed class ProviderSettlementConcurrencyFence
{
    private ProviderSettlementConcurrencyFence()
    {
    }

    internal string FenceSha256 { get; private set; } = null!;
    internal string ExternalSettlementId { get; private set; } = null!;
    internal long Revision { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }

    internal static string CreateFenceSha256(string? externalSettlementId)
    {
        var exactValue = ProviderPersistenceGuard.OpaqueRequired(
            externalSettlementId,
            nameof(externalSettlementId));
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(exactValue)))
            .ToLowerInvariant();
    }

    internal static ProviderSettlementConcurrencyFence Create(
        string externalSettlementId,
        DateTimeOffset createdAt)
    {
        var exactValue = ProviderPersistenceGuard.OpaqueRequired(
            externalSettlementId,
            nameof(externalSettlementId));
        var instant = ProviderPersistenceGuard.RequiredInstant(
            createdAt,
            nameof(createdAt));
        return new()
        {
            FenceSha256 = CreateFenceSha256(exactValue),
            ExternalSettlementId = exactValue,
            Revision = 1,
            CreatedAt = instant,
            UpdatedAt = instant
        };
    }

    internal void RegisterObservation(
        string externalSettlementId,
        DateTimeOffset changedAt)
    {
        if (!StringComparer.Ordinal.Equals(
                ExternalSettlementId,
                externalSettlementId))
        {
            throw new InvalidOperationException(
                "Una colisión de la clave técnica no puede mezclar liquidaciones.");
        }

        var instant = ProviderPersistenceGuard.RequiredInstant(
            changedAt,
            nameof(changedAt));
        if (instant < UpdatedAt)
        {
            instant = UpdatedAt;
        }

        Revision = checked(Revision + 1);
        UpdatedAt = instant;
    }
}
