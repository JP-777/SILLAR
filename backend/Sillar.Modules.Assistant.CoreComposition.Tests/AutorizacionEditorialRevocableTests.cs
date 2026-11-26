using Sillar.Core.Contracts;
using Sillar.Modules.Assistant.Authorization;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.CoreComposition.Tests;

/// <summary>
/// Dobles explícitos del FUTURO productor autorizado: no son atestación ni
/// entitlement reales, no se registran en Host y no certifican commits físicos.
/// El productor CORE canónico nunca devuelve Allowed.
/// </summary>
public sealed class AutorizacionEditorialRevocableTests
{
    [Theory]
    [InlineData(EditorialDenialReason.SessionRevoked)]
    [InlineData(EditorialDenialReason.InstallationIncomplete)]
    [InlineData(EditorialDenialReason.ContextualPolicyUnratified)]
    [InlineData(EditorialDenialReason.AuthorityUnavailable)]
    public async Task CoreDenegadoImpideConsultarPermisosAunqueTraigaHechosCompletos(
        EditorialDenialReason reason)
    {
        using var rig = new Rig();
        rig.Core.Decision = rig.Core.Decision with
        {
            Outcome = EditorialAuthorityOutcome.Denied,
            Reason = reason
        };

        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(0, rig.Authority.Calls);
        Assert.Equal(0, rig.Inner.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SinAmbasAutoridadesNoHayFallbackAlPrincipal(int missing)
    {
        using var rig = new Rig();
        var service = new RevocableAssistantEditorialAdministration(rig.Inner,
            missing == 0 || missing == 2 ? null : rig.Core,
            missing == 1 || missing == 2 ? null : rig.Authority);

        Assert.Equal(EditorialResult.Forbidden,
            await service.CreateAsync(rig.Actor, rig.Article, TestContext.Current.CancellationToken));
        Assert.Equal(0, rig.Inner.Calls);
    }

    [Fact]
    public async Task SinGateDeCommitNingunaMutacionLlegaAlRepositorio()
    {
        using var rig = new Rig();
        rig.Authority.WithholdGate = true;

        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(0, rig.Inner.Calls);
        Assert.Equal(0, rig.Authority.Commits);
    }

    [Fact]
    public async Task EscrituraSinteticaPasaUnaVezPorGateYConservaDecisionCoreCompleta()
    {
        using var rig = new Rig();
        var decision = rig.Core.Decision;

        Assert.Equal(EditorialResult.Saved, await rig.Save());
        Assert.Equal(1, rig.Authority.Commits);
        Assert.Same(rig.Authority.Last, rig.Authority.LastCommit);
        Assert.Same(decision, rig.Authority.Last!.CoreDecision);
        Assert.Equal(EditorialOperation.Edit, rig.Authority.Last.CoreOperation);
        Assert.Equal(decision.Actor!.SessionId, rig.Authority.Last.CoreDecision.Actor!.SessionId);
        Assert.Equal(decision.Installation, rig.Authority.Last.CoreDecision.Installation);
        Assert.Equal(decision.ObservedAt, rig.Authority.Last.CoreDecision.ObservedAt);
        Assert.Equal(decision.Outcome, rig.Authority.Last.CoreDecision.Outcome);
        Assert.Equal(decision.Reason, rig.Authority.Last.CoreDecision.Reason);
    }

    [Fact]
    public async Task InspectDeniegaSinConsultarCoreAutoridadRevocableNiRepositorio()
    {
        using var rig = new Rig();

        var result = await rig.Service.InspectAsync(
            rig.Actor, rig.Article, new CancellationToken(canceled: true));

        Assert.Equal(EditorialLookupStatus.Forbidden, result.Status);
        Assert.Null(result.Article);
        Assert.Equal(0, rig.Core.Calls);
        Assert.Equal(0, rig.Authority.Calls);
        Assert.Equal(0, rig.Inner.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task AllowedSinteticoConHechosIncompletosNoEsPermiso(int defect)
    {
        using var rig = new Rig();
        var decision = rig.Core.Decision;
        rig.Core.Decision = defect switch
        {
            0 => decision with { Actor = null },
            1 => decision with { Installation = null },
            2 => decision with { Actor = decision.Actor! with { SessionId = Guid.Empty } },
            3 => decision with { Actor = decision.Actor! with { AdminUserId = 0 } },
            4 => decision with { Actor = decision.Actor! with { HomeNode = " " } },
            5 => decision with { Actor = decision.Actor! with { Role = "owner" } },
            6 => decision with { Installation = decision.Installation! with { InstallationKey = Guid.Empty } },
            7 => decision with { Installation = decision.Installation! with { LicenseType = " " } },
            _ => decision with { ObservedAt = default }
        };

        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(0, rig.Authority.Calls);
        Assert.Equal(0, rig.Inner.Calls);
    }

    [Theory]
    [InlineData(AssistantAuthorizedExecutionOutcome.Denied)]
    [InlineData(AssistantAuthorizedExecutionOutcome.Revoked)]
    public async Task AutoridadRevocableDeniegaSinEjecutar(AssistantAuthorizedExecutionOutcome outcome)
    {
        using var rig = new Rig();
        rig.Authority.Execution = (_, _) => Task.FromResult(outcome);

        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(0, rig.Inner.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task PrincipalDevueltoDeOtroAmbitoNoAccede(int mismatch)
    {
        using var rig = new Rig();
        rig.Authority.Principal = mismatch switch
        {
            0 => rig.Actor with { InstallationId = Guid.Parse("01920000-0000-7000-8000-000000000099") },
            1 => rig.Actor with { WriterNode = "otro-writer" },
            2 => rig.Actor with { HasArticleWriteAuthority = false },
            _ => rig.Actor with { Role = EditorialRoles.SuperAdmin }
        };

        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(0, rig.Inner.Calls);
    }

    [Fact]
    public async Task HomeNodeYWriterNodeDistintosNoSeEquiparan()
    {
        using var rig = new Rig();

        Assert.NotEqual(rig.Core.Decision.Actor!.HomeNode, rig.Actor.WriterNode);
        Assert.Equal(EditorialResult.Saved, await rig.Save());
        Assert.Equal(1, rig.Authority.Commits);
    }

    [Fact]
    public async Task InstallationKeyYInstallationIdDistintosSeConservanSinConversion()
    {
        using var rig = new Rig();

        Assert.Equal(EditorialResult.Saved, await rig.Save());
        Assert.NotEqual(rig.Actor.InstallationId,
            rig.Authority.Last!.CoreDecision.Installation!.InstallationKey);
        Assert.Equal(rig.Actor.InstallationId, rig.Authority.Principal.InstallationId);
    }

    [Fact]
    public async Task TokenNoRevocableORevocadoAntesDeEjecutarNoAbreRepositorio()
    {
        using var rig = new Rig();
        rig.Authority.Token = CancellationToken.None;
        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        rig.Authority.Token = rig.Revoked.Token;
        rig.Revoked.Cancel();
        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(0, rig.Inner.Calls);
    }

    [Fact]
    public async Task RevocacionDuranteMutacionSuprimeResultadoAunqueRepositorioIgnoreCancelacion()
    {
        using var rig = new Rig();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Inner.During = async _ =>
        {
            started.SetResult();
            await release.Task;
        };

        var pending = rig.Save();
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        rig.Revoked.Cancel();
        release.SetResult();

        Assert.Equal(EditorialResult.Forbidden, await pending);
        Assert.Equal(1, rig.Inner.Calls);
    }

    [Fact]
    public async Task RevocacionDespuesDelCallbackSuprimeResultadoAntesDeRetornar()
    {
        using var rig = new Rig();
        rig.Authority.Execution = async (operation, _) =>
        {
            await operation(rig.Actor, rig.Authority, rig.Revoked.Token);
            rig.Revoked.Cancel();
            return AssistantAuthorizedExecutionOutcome.Completed;
        };

        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
    }

    [Fact]
    public async Task MismoAdaptadorReevaluaCoreYPermisoSinCachePositiva()
    {
        using var rig = new Rig();
        Assert.Equal(EditorialResult.Saved, await rig.Save());
        rig.Core.Decision = rig.Core.Decision with
        {
            Outcome = EditorialAuthorityOutcome.Denied,
            Reason = EditorialDenialReason.SessionRevoked
        };

        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(2, rig.Core.Calls);
        Assert.Equal(1, rig.Authority.Calls);
        Assert.Equal(1, rig.Inner.Calls);
    }

    [Fact]
    public async Task MismoAdaptadorDetectaRevocacionEntreOperaciones()
    {
        using var rig = new Rig();
        Assert.Equal(EditorialResult.Saved, await rig.Save());
        rig.Revoked.Cancel();

        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(2, rig.Core.Calls);
        Assert.Equal(2, rig.Authority.Calls);
        Assert.Equal(1, rig.Inner.Calls);
    }

    [Fact]
    public async Task CompletedSinCallbackYCallbackTardioNoDanAcceso()
    {
        using var rig = new Rig();
        Func<EditorialPrincipal, IEditorialCommitGate?, CancellationToken, Task>? saved = null;
        rig.Authority.Execution = (operation, _) =>
        {
            saved = operation;
            return Task.FromResult(AssistantAuthorizedExecutionOutcome.Completed);
        };

        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        await saved!(rig.Actor, rig.Authority, rig.Revoked.Token);
        Assert.Equal(0, rig.Inner.Calls);
    }

    [Fact]
    public async Task CallbackDuplicadoNoRepiteEscrituraNiConfirmaResultado()
    {
        using var rig = new Rig();
        rig.Authority.Execution = async (operation, _) =>
        {
            await operation(rig.Actor, rig.Authority, rig.Revoked.Token);
            await operation(rig.Actor, rig.Authority, rig.Revoked.Token);
            return AssistantAuthorizedExecutionOutcome.Completed;
        };

        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(1, rig.Inner.Calls);
    }

    [Fact]
    public async Task ErrorInternoNoFiltraDetalleNiReintenta()
    {
        using var rig = new Rig();
        rig.Inner.During = _ => throw new InvalidOperationException("secreto de prueba");

        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(1, rig.Inner.Calls);
    }

    [Fact]
    public async Task FallosDeCoreYDeAutoridadEditorialNoAbrenRepositorio()
    {
        using var rig = new Rig();
        rig.Core.Error = new InvalidOperationException("detalle privado");
        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(0, rig.Authority.Calls);
        rig.Core.Error = null;
        rig.Authority.Execution = (_, _) => throw new OperationCanceledException("cancelación interna");
        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(0, rig.Inner.Calls);
    }

    [Theory]
    [InlineData(AssistantEditorialPermission.Create, EditorialOperation.Create)]
    [InlineData(AssistantEditorialPermission.SaveDraft, EditorialOperation.Edit)]
    [InlineData(AssistantEditorialPermission.Publish, EditorialOperation.Publish)]
    [InlineData(AssistantEditorialPermission.Withdraw, EditorialOperation.Retire)]
    public async Task CadaMutacionPideOperacionCoreYArticuloCorrectos(
        AssistantEditorialPermission permission, EditorialOperation coreOperation)
    {
        using var rig = new Rig();
        rig.Authority.Execution = (_, _) => Task.FromResult(AssistantAuthorizedExecutionOutcome.Denied);
        var ct = TestContext.Current.CancellationToken;
        switch (permission)
        {
            case AssistantEditorialPermission.Create:
                await rig.Service.CreateAsync(rig.Actor, rig.Article, ct);
                break;
            case AssistantEditorialPermission.SaveDraft:
                await rig.Save();
                break;
            case AssistantEditorialPermission.Publish:
                await rig.Service.PublishAsync(rig.Actor, rig.Article, Guid.NewGuid(), 0, ct);
                break;
            case AssistantEditorialPermission.Withdraw:
                await rig.Service.WithdrawAsync(rig.Actor, rig.Article, Guid.NewGuid(), 0, ct);
                break;
        }

        Assert.Equal(rig.Article.ToString("D"), rig.Core.Last!.ArticleId);
        Assert.Equal(coreOperation, rig.Core.Last.Operation);
        Assert.Equal(permission, rig.Authority.Last!.Permission);
        Assert.Equal(coreOperation, rig.Authority.Last.CoreOperation);
        Assert.Equal(rig.Article, rig.Authority.Last.ArticleId);
        Assert.Equal(0, rig.Inner.Calls);
    }

    [Fact]
    public async Task CancelacionPreviaNoConsultaNingunaAutoridad()
    {
        using var rig = new Rig();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rig.Save(cancellation.Token));
        Assert.Equal(0, rig.Core.Calls);
        Assert.Equal(0, rig.Authority.Calls);
        Assert.Equal(0, rig.Inner.Calls);
    }

    [Fact]
    public async Task EditorNoPuedePublicarAunqueElDobleDeAutoridadLoIntentara()
    {
        using var rig = new Rig();
        var actor = rig.Actor with { Role = EditorialRoles.Editor };

        Assert.Equal(EditorialResult.Forbidden, await rig.Service.PublishAsync(actor, rig.Article,
            Guid.NewGuid(), 0, TestContext.Current.CancellationToken));
        Assert.Equal(0, rig.Core.Calls);
        Assert.Equal(0, rig.Inner.Calls);
    }

    [Fact]
    public async Task CancelacionDelLlamadorNoArrastraDetallesDeProveedor()
    {
        using var rig = new Rig();
        using var cancellation = new CancellationTokenSource();
        rig.Inner.During = _ =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException("secreto de prueba", new Exception("privado"), cancellation.Token);
        };

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rig.Save(cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("secreto", error.ToString());
    }

    private sealed class Rig : IDisposable
    {
        public EditorialPrincipal Actor { get; } = new(
            Guid.Parse("01920000-0000-7000-8000-0000000000a1"),
            EditorialRoles.Admin, "writer-node", true);
        public Guid Article { get; } = Guid.Parse("01920000-0000-7000-8000-0000000000b1");
        public CancellationTokenSource Revoked { get; } = new();
        public CoreAuthority Core { get; } = new();
        public Inner Inner { get; } = new();
        public Authority Authority { get; }
        public RevocableAssistantEditorialAdministration Service { get; }

        public Rig()
        {
            Authority = new Authority { Principal = Actor, Token = Revoked.Token };
            Service = new(Inner, Core, Authority);
        }

        public Task<EditorialResult> Save(CancellationToken? ct = null) => Service.SaveDraftAsync(Actor, Article,
            new(Guid.NewGuid(), "Pregunta", "Respuesta", "Título", new Uri("https://example.com/faq")), 0,
            ct ?? TestContext.Current.CancellationToken);

        public void Dispose() => Revoked.Dispose();
    }

    private sealed class CoreAuthority : IAssistantEditorialAuthority
    {
        private static readonly EditorialActor Actor = new(7, "core-home", EditorialRoles.Admin,
            Guid.Parse("01920000-0000-7000-8000-0000000000c1"));
        private static readonly EditorialInstallation Installation = new(
            Guid.Parse("01920000-0000-7000-8000-000000000001"), "subscription",
            new DateTimeOffset(2027, 10, 28, 12, 0, 0, TimeSpan.Zero));

        public int Calls { get; private set; }
        public Exception? Error { get; set; }
        public EditorialAuthorityRequest? Last { get; private set; }
        public EditorialAuthorityDecision Decision { get; set; } = new(
            EditorialAuthorityOutcome.Allowed,
            // El contrato actual no define una razón positiva. Este valor solo
            // permite probar la costura futura y nunca se registra en producción.
            EditorialDenialReason.ContextualPolicyUnratified,
            Actor, Installation,
            new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero));

        public Task<EditorialAuthorityDecision> EvaluateAsync(
            EditorialAuthorityRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            Last = request;
            if (Error is not null) throw Error;
            return Task.FromResult(Decision);
        }
    }

    private sealed class Authority : IRevocableAssistantEditorialAuthority, IEditorialCommitGate
    {
        public EditorialPrincipal Principal { get; set; } = null!;
        public CancellationToken Token { get; set; }
        public int Calls { get; private set; }
        public AssistantEditorialAuthorizationRequest? Last { get; private set; }
        public AssistantEditorialAuthorizationRequest? LastCommit { get; private set; }
        public int Commits { get; private set; }
        public bool WithholdGate { get; set; }
        public Func<Func<EditorialPrincipal, IEditorialCommitGate?, CancellationToken, Task>, CancellationToken,
            Task<AssistantAuthorizedExecutionOutcome>>? Execution { get; set; }

        public async Task<AssistantAuthorizedExecutionOutcome> ExecuteAsync(
            AssistantEditorialAuthorizationRequest request,
            Func<EditorialPrincipal, IEditorialCommitGate?, CancellationToken, Task> operation,
            CancellationToken cancellationToken)
        {
            Calls++;
            Last = request;
            if (Execution is not null) return await Execution(operation, cancellationToken);
            await operation(Principal, WithholdGate ? null : this, Token);
            return AssistantAuthorizedExecutionOutcome.Completed;
        }

        public async Task<T> CommitAsync<T>(AssistantEditorialAuthorizationRequest request,
            EditorialPrincipal principal, Func<CancellationToken, Task<T>> mutation,
            CancellationToken cancellationToken)
        {
            Commits++;
            LastCommit = request;
            cancellationToken.ThrowIfCancellationRequested();
            var result = await mutation(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
    }

    private sealed class Inner : IAssistantEditorialAdministration
    {
        public int Calls { get; private set; }
        public Func<CancellationToken, Task>? During { get; set; }

        private async Task<EditorialResult> Write(CancellationToken cancellationToken)
        {
            Calls++;
            if (During is not null) await During(cancellationToken);
            return EditorialResult.Saved;
        }

        public Task<EditorialResult> CreateAsync(EditorialPrincipal actor, Guid id, CancellationToken ct) => Write(ct);
        public Task<EditorialResult> SaveDraftAsync(EditorialPrincipal actor, Guid id, KnowledgeDraft draft,
            long version, CancellationToken ct) => Write(ct);
        public Task<EditorialResult> PublishAsync(EditorialPrincipal actor, Guid id, Guid revision,
            long version, CancellationToken ct) => Write(ct);
        public Task<EditorialResult> WithdrawAsync(EditorialPrincipal actor, Guid id, Guid revision,
            long version, CancellationToken ct) => Write(ct);
        public Task<EditorialArticleLookup> InspectAsync(EditorialPrincipal actor, Guid id, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new EditorialArticleLookup(EditorialLookupStatus.Found, null));
        }
    }
}
