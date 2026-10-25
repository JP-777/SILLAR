using Sillar.Core.Contracts;
using Sillar.Modules.Assistant.Authorization;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.CoreComposition.Tests;

/// <summary>
/// Dobles explícitos del FUTURO productor autorizado: no son atestación ni
/// entitlement reales, no se registran en Host y no certifican commits físicos.
/// </summary>
public sealed class AutorizacionEditorialRevocableTests
{
    [Theory]
    [InlineData(AssistantAccessOutcome.Denied)]
    [InlineData(AssistantAccessOutcome.PendingDecision)]
    public async Task CoreNoAutorizadoImpideConsultarPermisosYRepositorio(AssistantAccessOutcome outcome)
    {
        using var rig = new Rig();
        rig.Access.Decision = rig.Access.Decision with { Outcome = outcome };
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
            missing == 0 || missing == 2 ? null : rig.Access,
            missing == 1 || missing == 2 ? null : rig.Authority);
        Assert.Equal(EditorialResult.Forbidden,
            await service.CreateAsync(rig.Actor, rig.Article, TestContext.Current.CancellationToken));
        Assert.Equal(0, rig.Inner.Calls);
    }

    [Fact]
    public async Task AllowedIncoherenteNoEsPermiso()
    {
        using var rig = new Rig();
        rig.Access.Decision = rig.Access.Decision with { Reason = AssistantAccessReason.EntitlementPolicyUnratified };
        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        rig.Access.Decision = rig.Access.Decision with
        {
            Reason = AssistantAccessReason.AuthorizedByRatifiedPolicy, InstallationKey = Guid.Empty
        };
        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(0, rig.Authority.Calls);
    }

    [Theory]
    [InlineData(AssistantAuthorizedExecutionOutcome.Denied)]
    [InlineData(AssistantAuthorizedExecutionOutcome.Revoked)]
    public async Task AutoridadDeniegaSinEjecutar(AssistantAuthorizedExecutionOutcome outcome)
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
    public async Task PrincipalDevueltoDeOtroAmbitoONodoNoAccede(int mismatch)
    {
        using var rig = new Rig();
        rig.Authority.Principal = mismatch switch
        {
            0 => rig.Actor with { InstallationId = Guid.NewGuid() },
            1 => rig.Actor with { WriterNode = "otro" },
            _ => rig.Actor with { HasArticleWriteAuthority = false }
        };
        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(0, rig.Inner.Calls);
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
    public async Task RevocacionDuranteLecturaSuprimeContenidoAunqueRepositorioIgnoreCancelacion()
    {
        using var rig = new Rig();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Inner.During = async _ =>
        {
            started.SetResult();
            await release.Task; // Deliberadamente ignora cancelación: el adaptador debe suprimir el resultado.
        };
        var pending = rig.Service.InspectAsync(rig.Actor, rig.Article, TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        rig.Revoked.Cancel();
        release.SetResult();
        var result = await pending;
        Assert.Equal(EditorialLookupStatus.Forbidden, result.Status);
        Assert.Null(result.Article);
        Assert.Equal(1, rig.Inner.Calls);
    }

    [Fact]
    public async Task RevocacionDespuesDelCallbackSuprimeResultadoAntesDeRetornar()
    {
        using var rig = new Rig();
        rig.Authority.Execution = async (operation, _) =>
        {
            await operation(rig.Actor, rig.Revoked.Token);
            rig.Revoked.Cancel();
            return AssistantAuthorizedExecutionOutcome.Completed;
        };
        var result = await rig.Service.InspectAsync(rig.Actor, rig.Article, TestContext.Current.CancellationToken);
        Assert.Equal(EditorialLookupStatus.Forbidden, result.Status);
        Assert.Null(result.Article);
    }

    [Fact]
    public async Task MismoAdaptadorReevaluaCoreYPermisoSinCachePositiva()
    {
        using var rig = new Rig();
        Assert.Equal(EditorialResult.Saved, await rig.Save()); // Solo autorización sintética del doble.
        rig.Access.Decision = rig.Access.Decision with { Outcome = AssistantAccessOutcome.Denied };
        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(2, rig.Access.Calls);
        Assert.Equal(1, rig.Authority.Calls);
        Assert.Equal(1, rig.Inner.Calls);
        Assert.NotEqual(rig.Actor.InstallationId, rig.Authority.Last!.InstallationKey);
        Assert.Equal(rig.Article, rig.Authority.Last.ArticleId);
        Assert.Equal(AssistantEditorialPermission.SaveDraft, rig.Authority.Last.Permission);
    }

    [Fact]
    public async Task MismoAdaptadorDetectaRevocacionEntreOperaciones()
    {
        using var rig = new Rig();
        Assert.Equal(EditorialResult.Saved, await rig.Save());
        rig.Revoked.Cancel();
        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(2, rig.Access.Calls);
        Assert.Equal(2, rig.Authority.Calls);
        Assert.Equal(1, rig.Inner.Calls);
    }

    [Fact]
    public async Task CompletedSinCallbackYCallbackTardioNoDanAcceso()
    {
        using var rig = new Rig();
        Func<EditorialPrincipal, CancellationToken, Task>? saved = null;
        rig.Authority.Execution = (operation, _) =>
        {
            saved = operation;
            return Task.FromResult(AssistantAuthorizedExecutionOutcome.Completed);
        };
        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        await saved!(rig.Actor, rig.Revoked.Token);
        Assert.Equal(0, rig.Inner.Calls);
    }

    [Fact]
    public async Task CallbackDuplicadoNoRepiteEscrituraNiConfirmaResultado()
    {
        using var rig = new Rig();
        rig.Authority.Execution = async (operation, _) =>
        {
            await operation(rig.Actor, rig.Revoked.Token);
            await operation(rig.Actor, rig.Revoked.Token);
            return AssistantAuthorizedExecutionOutcome.Completed;
        };
        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(1, rig.Inner.Calls); // No afirma rollback de esa ejecución sintética.
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
        rig.Access.Error = new InvalidOperationException("detalle privado");
        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(0, rig.Authority.Calls);
        rig.Access.Error = null;
        rig.Authority.Execution = (_, _) => throw new OperationCanceledException("cancelación interna");
        Assert.Equal(EditorialResult.Forbidden, await rig.Save());
        Assert.Equal(0, rig.Inner.Calls);
    }

    [Theory]
    [InlineData(AssistantEditorialPermission.Inspect)]
    [InlineData(AssistantEditorialPermission.Create)]
    [InlineData(AssistantEditorialPermission.SaveDraft)]
    [InlineData(AssistantEditorialPermission.Publish)]
    [InlineData(AssistantEditorialPermission.Withdraw)]
    public async Task CadaOperacionPideSuPermisoYArticuloSinConfundirRoles(AssistantEditorialPermission permission)
    {
        using var rig = new Rig();
        rig.Authority.Execution = (_, _) => Task.FromResult(AssistantAuthorizedExecutionOutcome.Denied);
        var ct = TestContext.Current.CancellationToken;
        switch (permission)
        {
            case AssistantEditorialPermission.Inspect:
                Assert.Null((await rig.Service.InspectAsync(rig.Actor, rig.Article, ct)).Article);
                break;
            case AssistantEditorialPermission.Create:
                Assert.Equal(EditorialResult.Forbidden, await rig.Service.CreateAsync(rig.Actor, rig.Article, ct));
                break;
            case AssistantEditorialPermission.SaveDraft:
                Assert.Equal(EditorialResult.Forbidden, await rig.Save());
                break;
            case AssistantEditorialPermission.Publish:
                Assert.Equal(EditorialResult.Forbidden,
                    await rig.Service.PublishAsync(rig.Actor, rig.Article, Guid.NewGuid(), 0, ct));
                break;
            case AssistantEditorialPermission.Withdraw:
                Assert.Equal(EditorialResult.Forbidden,
                    await rig.Service.WithdrawAsync(rig.Actor, rig.Article, Guid.NewGuid(), 0, ct));
                break;
        }
        Assert.Equal(permission, rig.Authority.Last!.Permission);
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
        Assert.Equal(0, rig.Access.Calls);
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
        Assert.Equal(0, rig.Access.Calls);
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
        public EditorialPrincipal Actor { get; } = new(Guid.NewGuid(), EditorialRoles.Admin, "principal", true);
        public Guid Article { get; } = Guid.NewGuid();
        public CancellationTokenSource Revoked { get; } = new();
        public Access Access { get; } = new();
        public Inner Inner { get; } = new();
        public Authority Authority { get; }
        public RevocableAssistantEditorialAdministration Service { get; }
        public Rig()
        {
            Authority = new Authority { Principal = Actor, Token = Revoked.Token };
            Service = new(Inner, Access, Authority);
        }
        public Task<EditorialResult> Save(CancellationToken? ct = null) => Service.SaveDraftAsync(Actor, Article,
            new(Guid.NewGuid(), "Pregunta", "Respuesta", "Título", new Uri("https://example.com/faq")), 0,
            ct ?? TestContext.Current.CancellationToken);
        public void Dispose() => Revoked.Dispose();
    }

    private sealed class Access : IAssistantAccessAuthority
    {
        public int Calls { get; private set; }
        public Exception? Error { get; set; }
        public AssistantAccessDecision Decision { get; set; } = new(AssistantAccessOutcome.Allowed,
            AssistantAccessReason.AuthorizedByRatifiedPolicy, Guid.NewGuid(), "test:future-authority",
            DateTimeOffset.UtcNow, null, null);
        public Task<AssistantAccessDecision> EvaluateCurrentInstallationAsync(CancellationToken ct)
        {
            Calls++;
            if (Error is not null) throw Error;
            return Task.FromResult(Decision);
        }
    }

    private sealed class Authority : IRevocableAssistantEditorialAuthority
    {
        public EditorialPrincipal Principal { get; set; } = null!;
        public CancellationToken Token { get; set; }
        public int Calls { get; private set; }
        public AssistantEditorialAuthorizationRequest? Last { get; private set; }
        public Func<Func<EditorialPrincipal, CancellationToken, Task>, CancellationToken,
            Task<AssistantAuthorizedExecutionOutcome>>? Execution { get; set; }
        public async Task<AssistantAuthorizedExecutionOutcome> ExecuteAsync(AssistantEditorialAuthorizationRequest request,
            Func<EditorialPrincipal, CancellationToken, Task> operation, CancellationToken ct)
        {
            Calls++;
            Last = request;
            if (Execution is not null) return await Execution(operation, ct);
            await operation(Principal, Token);
            return AssistantAuthorizedExecutionOutcome.Completed;
        }
    }

    private sealed class Inner : IAssistantEditorialAdministration
    {
        public int Calls { get; private set; }
        public Func<CancellationToken, Task>? During { get; set; }
        private async Task<EditorialResult> Write(CancellationToken ct)
        {
            Calls++;
            if (During is not null) await During(ct);
            return EditorialResult.Saved;
        }
        public Task<EditorialResult> CreateAsync(EditorialPrincipal actor, Guid id, CancellationToken ct) => Write(ct);
        public Task<EditorialResult> SaveDraftAsync(EditorialPrincipal actor, Guid id, KnowledgeDraft draft,
            long version, CancellationToken ct) => Write(ct);
        public Task<EditorialResult> PublishAsync(EditorialPrincipal actor, Guid id, Guid revision,
            long version, CancellationToken ct) => Write(ct);
        public Task<EditorialResult> WithdrawAsync(EditorialPrincipal actor, Guid id, Guid revision,
            long version, CancellationToken ct) => Write(ct);
        public async Task<EditorialArticleLookup> InspectAsync(EditorialPrincipal actor, Guid id, CancellationToken ct)
        {
            Calls++;
            if (During is not null) await During(ct);
            return new(EditorialLookupStatus.Found, new(id, actor.InstallationId, actor.WriterNode, 0,
                new(Guid.NewGuid(), "Pregunta privada", "Contenido privado", "Borrador", new Uri("https://example.com/faq")), null));
        }
    }
}
