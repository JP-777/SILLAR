using Sillar.Modules.Assistant.Domain;

namespace Sillar.Modules.Assistant.Services;

/// <summary>Configuración de una sola instalación, suministrada por una autoridad confiable.</summary>
public sealed record LocalQuotaConfiguration(
    Guid InstallationId, AssistantMode Mode, bool EngineConfigured,
    long LimitUnits, int ConcurrencyLimit);

/// <summary>El estado reservado no supone autorización para llamar un motor sin MarkDispatched.</summary>
public enum QuotaOperation
{
    Reserved,
    FaqOnly,
    FaqFallback,
    Invalid,
    NotFound,
    Dispatched,
    AlreadyDispatched,
    Completed,
    AlreadyCompleted,
    Cancelled,
    AlreadyCancelled,
    Conflict
}

public sealed record QuotaReservation(QuotaOperation Outcome, Guid? ReservationId, AssistantMode? Mode);

public sealed record LocalQuotaSnapshot(
    long LimitUnits, long ConsumedUnits, long ReservedUnits, int RunningRequests,
    AssistantMode Mode);

/// <summary>
/// Reservas atómicas estrictamente VOLÁTILES, para comprobar concurrencia, consumo
/// y ausencia de cambio automático de motor. No son una autoridad durable de gasto.
/// En caso de timeout/caída después de Dispatched, conservar reserva: ni expirarla
/// ni llamar otra vez al modelo sin un protocolo externo de reconciliación.
/// </summary>
public sealed class LocalAssistantQuotaStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, State> _states = new();

    private sealed class State(LocalQuotaConfiguration configuration)
    {
        public LocalQuotaConfiguration Configuration { get; } = configuration;
        public long Consumed;
        public long Reserved;
        public int Running;
        public Dictionary<Guid, Lease> Leases { get; } = new();
    }

    private enum LeasePhase { Reserved, Dispatched, Completed, Cancelled }

    private sealed class Lease(long units)
    {
        public long Units { get; } = units;
        public LeasePhase Phase { get; set; } = LeasePhase.Reserved;
        public long? ActualUnits { get; set; }
    }

    public LocalAssistantQuotaStore(IEnumerable<LocalQuotaConfiguration> configurations)
    {
        ArgumentNullException.ThrowIfNull(configurations);
        foreach (var c in configurations)
        {
            if (c is null || c.InstallationId == Guid.Empty || !Enum.IsDefined(c.Mode) ||
                c.LimitUnits < 0 || c.ConcurrencyLimit < 0 ||
                !_states.TryAdd(c.InstallationId, new State(c)))
                throw new ArgumentException("Configuración de cuota inválida o duplicada.", nameof(configurations));
        }
        if (_states.Count == 0)
            throw new ArgumentException("Se requiere al menos una instalación.", nameof(configurations));
    }

    public QuotaReservation TryReserve(Guid installationId, long worstCaseUnits)
    {
        if (worstCaseUnits <= 0) return new(QuotaOperation.Invalid, null, null);
        lock (_gate)
        {
            if (!_states.TryGetValue(installationId, out var s))
                return new(QuotaOperation.NotFound, null, null);
            var c = s.Configuration;
            var decision = AssistantUsePolicy.Evaluate(installationId,
                new ResourceBudgetSnapshot(installationId, c.Mode, c.EngineConfigured,
                    c.LimitUnits, s.Consumed, s.Reserved, worstCaseUnits,
                    c.ConcurrencyLimit, s.Running));
            if (decision == AdmissionPlan.FaqOnly)
                return new(QuotaOperation.FaqOnly, null, AssistantMode.Faq);
            if (decision == AdmissionPlan.FaqFallback)
                return new(QuotaOperation.FaqFallback, null, AssistantMode.Faq);
            if (decision == AdmissionPlan.InvalidConfiguration)
                return new(QuotaOperation.Invalid, null, null);
            if (s.Running == int.MaxValue) return new(QuotaOperation.FaqFallback, null, AssistantMode.Faq);
            var token = Guid.NewGuid();
            s.Leases.Add(token, new Lease(worstCaseUnits));
            s.Reserved += worstCaseUnits;
            s.Running++;
            return new(QuotaOperation.Reserved, token, c.Mode);
        }
    }

    /// <summary>Solo la primera transición Dispatched autoriza una invocación; duplicados no autorizan otra.</summary>
    public QuotaOperation MarkDispatched(Guid installationId, Guid reservationId)
    {
        lock (_gate)
        {
            if (!Find(installationId, reservationId, out _, out var lease))
                return QuotaOperation.NotFound;
            if (lease!.Phase == LeasePhase.Dispatched) return QuotaOperation.AlreadyDispatched;
            if (lease.Phase != LeasePhase.Reserved) return QuotaOperation.Conflict;
            lease.Phase = LeasePhase.Dispatched;
            return QuotaOperation.Dispatched;
        }
    }

    /// <summary>Asienta el consumo SOLO tras una respuesta fiable del motor, nunca tras timeout incierto.</summary>
    public QuotaOperation Complete(Guid installationId, Guid reservationId, long actualUnits)
    {
        lock (_gate)
        {
            if (!Find(installationId, reservationId, out var s, out var lease))
                return QuotaOperation.NotFound;
            if (lease!.Phase == LeasePhase.Completed)
                return lease.ActualUnits == actualUnits
                    ? QuotaOperation.AlreadyCompleted : QuotaOperation.Conflict;
            if (lease.Phase != LeasePhase.Dispatched || actualUnits < 0 || actualUnits > lease.Units)
                return QuotaOperation.Conflict;
            s!.Reserved -= lease.Units;
            s.Consumed += actualUnits;
            s.Running--;
            lease.ActualUnits = actualUnits;
            lease.Phase = LeasePhase.Completed;
            return QuotaOperation.Completed;
        }
    }

    /// <summary>Cancelar solamente antes de MarkDispatched; nunca liberar consumos inciertos.</summary>
    public QuotaOperation CancelBeforeDispatch(Guid installationId, Guid reservationId)
    {
        lock (_gate)
        {
            if (!Find(installationId, reservationId, out var s, out var lease))
                return QuotaOperation.NotFound;
            if (lease!.Phase == LeasePhase.Cancelled) return QuotaOperation.AlreadyCancelled;
            if (lease.Phase != LeasePhase.Reserved) return QuotaOperation.Conflict;
            s!.Reserved -= lease.Units;
            s.Running--;
            lease.Phase = LeasePhase.Cancelled;
            return QuotaOperation.Cancelled;
        }
    }

    public LocalQuotaSnapshot? Snapshot(Guid installationId)
    {
        lock (_gate)
        {
            if (!_states.TryGetValue(installationId, out var s)) return null;
            return new(s.Configuration.LimitUnits, s.Consumed, s.Reserved,
                s.Running, s.Configuration.Mode);
        }
    }

    private bool Find(Guid installationId, Guid reservationId, out State? state, out Lease? lease)
    {
        state = null;
        lease = null;
        if (installationId == Guid.Empty || reservationId == Guid.Empty ||
            !_states.TryGetValue(installationId, out state)) return false;
        return state!.Leases.TryGetValue(reservationId, out lease);
    }
}
