using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Application.Contracts;

/// <summary>
/// Vista local y provisional de los desenlaces candidatos de M03. Esta capa se
/// elimina o adapta cuando el contrato público sea integrado y ratificado.
/// </summary>
internal enum M03CandidateOutcome
{
    AppliedDurable,
    AlreadyAppliedEquivalent,
    Rejected,
    PendingDecision
}

/// <summary>
/// Vista local de las razones candidatas publicadas por M03. Mantener la razón
/// separada del desenlace impide aceptar combinaciones semánticamente inválidas.
/// </summary>
internal enum M03CandidateReason
{
    InvalidIdentity,
    InvalidAmount,
    MissingCurrency,
    UnauthenticatedEvent,
    CommercialPolicyUnratified,
    DurableIdentityPolicyUnratified,
    DurableWriteUnavailable,
    InconsistentReplay,
    RatifiedAndPersisted,
    EquivalentReplayWithoutDurableEffect
}

/// <summary>
/// Identidad exacta de la propuesta observada por la futura costura con M03.
/// Los literales son opacos y nunca se normalizan.
/// </summary>
internal sealed record M03CandidateIdentity(
    string? Provider,
    string? ProviderEventId,
    string? ExternalSettlementId);

/// <summary>
/// Respuesta mínima que la costura futura entregará al mapper puro. No es una
/// copia del contrato M03 ni habilita su registro productivo.
/// </summary>
/// <param name="Outcome">Desenlace candidato informado por M03.</param>
/// <param name="Reason">Causa original informada por M03.</param>
/// <param name="Identity">
/// Identidad de la propuesta efectivamente enviada a M03; el resultado candidato
/// actual no la devuelve y el futuro adaptador deberá conservarla como contexto.
/// </param>
/// <param name="DurableEffectReference">
/// Referencia candidata; su presencia no autoriza un efecto en esta fase.
/// </param>
internal sealed record M03CandidateResultSnapshot(
    M03CandidateOutcome Outcome,
    M03CandidateReason Reason,
    M03CandidateIdentity? Identity,
    string? DurableEffectReference = null);

internal enum M03CandidateMappingDisposition
{
    MappedWithoutCommercialEffect,
    Blocked
}

/// <summary>
/// Resultado fail-closed del mapper. Un bloqueo no contiene resultado comercial
/// y un mapeo jamás puede contener Applied ni AlreadyApplied.
/// </summary>
internal sealed record M03CandidateMapping
{
    private M03CandidateMapping(
        M03CandidateMappingDisposition disposition,
        ProviderCommercialEffectResult? result,
        string? blockReason,
        M03CandidateResultSnapshot? source,
        string? sourceFailure,
        ProviderSettlementFacts? contradictoryFacts)
    {
        Disposition = disposition;
        Result = result;
        BlockReason = blockReason;
        Source = source;
        SourceFailure = sourceFailure;
        ContradictoryFacts = contradictoryFacts;
    }

    internal M03CandidateMappingDisposition Disposition { get; }
    internal ProviderCommercialEffectResult? Result { get; }
    internal string? BlockReason { get; }
    internal M03CandidateResultSnapshot? Source { get; }
    internal string? SourceFailure { get; }
    internal ProviderSettlementFacts? ContradictoryFacts { get; }

    internal static M03CandidateMapping WithoutCommercialEffect(
        ProviderCommercialEffectResult result,
        M03CandidateResultSnapshot? source,
        string? sourceFailure = null,
        ProviderSettlementFacts? contradictoryFacts = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Outcome is ProviderCommercialEffectOutcome.Applied
            or ProviderCommercialEffectOutcome.AlreadyApplied)
        {
            throw new ArgumentException(
                "El mapper candidato no puede acreditar efectos comerciales.",
                nameof(result));
        }

        return new(
            M03CandidateMappingDisposition.MappedWithoutCommercialEffect,
            result,
            null,
            source,
            sourceFailure,
            contradictoryFacts);
    }

    internal static M03CandidateMapping Blocked(
        string? reason,
        M03CandidateResultSnapshot source)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Un bloqueo exige razón.",
                nameof(reason));
        }

        ArgumentNullException.ThrowIfNull(source);
        return new(
            M03CandidateMappingDisposition.Blocked,
            null,
            reason,
            source,
            null,
            null);
    }
}
