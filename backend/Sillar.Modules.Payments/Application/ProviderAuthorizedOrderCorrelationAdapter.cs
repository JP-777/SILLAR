using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Application;

/// <summary>
/// Consume la correlación candidata M03 sólo después de acreditar la
/// procedencia de la declaración dentro de M11.
/// </summary>
internal sealed class ProviderAuthorizedOrderCorrelationAdapter(
    IProviderAuthenticatedOrderDeclarationSource declarationSource,
    IProviderVerifiedEvidenceReferenceSource evidenceReferenceSource,
    IM03OrderCorrelationCandidatePort correlationPort)
    : IProviderAuthorizedOrderCorrelationSource
{
    private const string CurrentDatabaseSource = "sales.current_database";

    public async ValueTask<ProviderAuthorizedOrderCorrelation?> LoadAsync(
        Guid inboxEventId,
        PaymentIdempotencyKeys identity,
        CancellationToken cancellationToken)
    {
        if (inboxEventId == Guid.Empty)
        {
            throw new ArgumentException(
                "La correlación exige inbox.",
                nameof(inboxEventId));
        }

        ArgumentNullException.ThrowIfNull(identity);
        var evidenceReferences = await evidenceReferenceSource.LoadAsync(
            inboxEventId,
            cancellationToken);
        var declarations = await declarationSource.LoadAsync(
            inboxEventId,
            cancellationToken);
        if (evidenceReferences is null || declarations is null)
        {
            return null;
        }

        var authenticated = (
            from candidateDeclaration in declarations
            where candidateDeclaration is not null
                && candidateDeclaration.Identity == identity
            from evidenceReference in evidenceReferences
            where evidenceReference is not null
                && evidenceReference.Identity == identity
                && candidateDeclaration.Matches(
                    inboxEventId,
                    evidenceReference)
            select candidateDeclaration)
            .ToArray();
        if (authenticated.Length != 1)
        {
            return null;
        }

        var declaration = authenticated[0];
        var resolution = await correlationPort.ResolveAsync(
            new(
                declaration.DeclaredOrderCode,
                declaration.DeclaredOrderId),
            cancellationToken);
        if (!IsUsableResolution(declaration, resolution))
        {
            return null;
        }

        return ProviderAuthorizedOrderCorrelation.CreateResolvedOrder(
            resolution!.OrderId!.Value,
            resolution.OrderCode!,
            identity,
            resolution.Source!);
    }

    private static bool IsUsableResolution(
        ProviderAuthenticatedOrderDeclaration declaration,
        M03OrderCorrelationResultSnapshot? resolution)
    {
        if (resolution is null
            || !Enum.IsDefined(resolution.Outcome)
            || resolution.Outcome != M03OrderCorrelationOutcome.Resolved
            || resolution.OrderId is null
            || resolution.OrderId == Guid.Empty
            || string.IsNullOrWhiteSpace(resolution.OrderCode)
            || !StringComparer.Ordinal.Equals(
                resolution.Source,
                CurrentDatabaseSource))
        {
            return false;
        }

        if (declaration.DeclaredOrderId is { } declaredOrderId
            && declaredOrderId != resolution.OrderId)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(declaration.DeclaredOrderCode)
            || StringComparer.Ordinal.Equals(
                declaration.DeclaredOrderCode.Trim(),
                resolution.OrderCode);
    }
}
