using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
using Sillar.Host.AssistantBridge;
using Sillar.Modules.Assistant;
using Sillar.Modules.Assistant.Authorization;
using Sillar.Modules.Assistant.Data.Repositories;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Host.AssistantBridge.Tests;

public sealed class AssistantM12NegativeCompositionTests
{
    private static readonly Guid InstallationId =
        Guid.Parse("642f3350-310b-49f5-a720-27a3ad9f09d0");

    [Fact]
    public void Host_expone_los_contratos_reales_de_M12_solo_en_modo_denegado()
    {
        var services = new ServiceCollection();
        services.AddAssistantHostCandidates();
        services.AddAssistantHostCandidates();

        Assert.Single(services, x => x.ServiceType == typeof(ITrustedAssistantHostEvidence));
        Assert.Single(services, x => x.ServiceType == typeof(ITrustedAssistantInstallationScope));
        Assert.Single(services, x => x.ServiceType == typeof(ITrustedAssistantQuotaPolicyWriter));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var evidence = scope.ServiceProvider.GetRequiredService<ITrustedAssistantHostEvidence>();
        var adapter = scope.ServiceProvider.GetRequiredService<AssistantHostEvidenceAdapter>();

        Assert.Same(adapter, evidence);
        Assert.False(evidence.IsInstallationConfigured);
        Assert.False(evidence.IsAssistantActive);
        Assert.False(evidence.IsAssistantLicensed);
        Assert.Null(evidence.InstallationId);

        var installation = scope.ServiceProvider
            .GetRequiredService<ITrustedAssistantInstallationScope>();
        Assert.IsType<HostAttestedAssistantInstallationScope>(installation);
        Assert.False(installation.TryGetEnabledInstallation(out var installationId));
        Assert.Equal(Guid.Empty, installationId);

        var writer = scope.ServiceProvider
            .GetRequiredService<ITrustedAssistantQuotaPolicyWriter>();
        Assert.IsType<DenyAllAssistantQuotaPolicyWriter>(writer);
        Assert.False(writer.CanWritePolicy(Guid.Empty));
        Assert.False(writer.CanWritePolicy(Guid.NewGuid()));
    }

    [Fact]
    public void Host_no_fabrica_autoridad_actor_commit_gate_politica_ni_fachada()
    {
        var services = new ServiceCollection();
        services.AddAssistantHostCandidates();

        Assert.DoesNotContain(services, x =>
            x.ServiceType == typeof(IAssistantEditorialAuthority));
        Assert.DoesNotContain(services, x =>
            x.ServiceType == typeof(IRevocableAssistantEditorialAuthority));
        Assert.DoesNotContain(services, x =>
            x.ServiceType == typeof(IEditorialCommitGate));
        Assert.DoesNotContain(services, x =>
            x.ServiceType == typeof(ITrustedEditorialAdminScope));
        Assert.DoesNotContain(services, x =>
            x.ServiceType == typeof(EditorialPersistencePolicy));
        Assert.DoesNotContain(services, x =>
            x.ServiceType == typeof(IAssistantEditorialAdministration));
    }

    [Fact]
    public void Politica_ausente_o_duplicada_no_habilita_la_fachada()
    {
        var module = new AssistantModule();

        var missing = new ServiceCollection();
        missing.AddAssistantHostCandidates();
        Assert.Throws<InvalidOperationException>(() =>
            module.RegisterServices(missing, Configuration()));
        Assert.DoesNotContain(missing, x =>
            x.ServiceType == typeof(IAssistantEditorialAdministration));

        var duplicated = new ServiceCollection();
        duplicated.AddAssistantHostCandidates();
        var policy = Policy();
        duplicated.AddSingleton(policy);
        duplicated.AddSingleton(policy);
        Assert.Throws<InvalidOperationException>(() =>
            module.RegisterServices(duplicated, Configuration()));
        Assert.DoesNotContain(duplicated, x =>
            x.ServiceType == typeof(IAssistantEditorialAdministration));
    }

    [Fact]
    public async Task M12_real_sin_autoridades_deniega_y_no_expone_el_adaptador_EF()
    {
        var services = new ServiceCollection();
        services.AddAssistantHostCandidates();
        var policy = Policy();
        services.AddSingleton(policy);
        new AssistantModule().RegisterServices(services, Configuration());

        Assert.Single(services, x =>
            x.ServiceType == typeof(EditorialPersistencePolicy));
        Assert.Single(services, x =>
            x.ServiceType == typeof(IAssistantEditorialAdministration));
        Assert.DoesNotContain(services, x =>
            x.ServiceType == typeof(IRevocableAssistantEditorialAuthority));
        Assert.DoesNotContain(services, x =>
            x.ServiceType == typeof(IEditorialCommitGate));
        Assert.DoesNotContain(services, x =>
            x.ServiceType == typeof(ITrustedEditorialAdminScope));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var editorial = scope.ServiceProvider
            .GetRequiredService<IAssistantEditorialAdministration>();
        Assert.IsType<RevocableAssistantEditorialAdministration>(editorial);

        var efType = typeof(RevocableAssistantEditorialAdministration).Assembly.GetType(
            "Sillar.Modules.Assistant.Services.EfAssistantEditorialAdministration",
            throwOnError: true)!;
        Assert.Null(scope.ServiceProvider.GetService(efType));
        Assert.Null(scope.ServiceProvider.GetService<EfEditorialArticleStore>());

        var actor = Actor();
        var articleId = Guid.CreateVersion7();
        Assert.Equal(EditorialResult.Forbidden,
            await editorial.CreateAsync(actor, articleId,
                TestContext.Current.CancellationToken));
        Assert.Equal(EditorialLookupStatus.Forbidden,
            (await editorial.InspectAsync(actor, articleId,
                TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task CORE_Denied_prevalece_y_no_invoca_la_autoridad_posterior()
    {
        var inner = new CountingEditorialAdministration();
        var core = new DeniedCoreAuthority();
        var revocable = new MustNotRunRevocableAuthority();
        var facade = new RevocableAssistantEditorialAdministration(inner, core, revocable);

        var result = await facade.CreateAsync(Actor(), Guid.CreateVersion7(),
            TestContext.Current.CancellationToken);

        Assert.Equal(EditorialResult.Forbidden, result);
        Assert.Equal(1, core.Calls);
        Assert.Equal(0, revocable.Calls);
        Assert.Equal(0, inner.Calls);
    }

    [Fact]
    public async Task Sin_autoridad_revocable_o_sin_commit_gate_no_hay_mutacion()
    {
        var actor = Actor();
        var inner = new CountingEditorialAdministration();
        var unusedCore = new ThrowingCoreAuthority();
        var withoutRevocation = new RevocableAssistantEditorialAdministration(
            inner, unusedCore, editorialAuthority: null);

        Assert.Equal(EditorialResult.Forbidden,
            await withoutRevocation.CreateAsync(actor, Guid.CreateVersion7(),
                TestContext.Current.CancellationToken));
        Assert.Equal(0, unusedCore.Calls);
        Assert.Equal(0, inner.Calls);

        var core = new AllowedCoreAuthority();
        var withoutCommitGate = new CallbackWithoutCommitGate(actor);
        var facade = new RevocableAssistantEditorialAdministration(
            inner, core, withoutCommitGate);

        Assert.Equal(EditorialResult.Forbidden,
            await facade.CreateAsync(actor, Guid.CreateVersion7(),
                TestContext.Current.CancellationToken));
        Assert.Equal(1, core.Calls);
        Assert.Equal(1, withoutCommitGate.Calls);
        Assert.Equal(0, inner.Calls);
        Assert.NotEqual(actor.InstallationId,
            core.LastDecision!.Installation!.InstallationKey);
    }

    [Fact]
    public async Task Inspect_permanece_Forbidden_sin_consultar_autoridades()
    {
        var inner = new CountingEditorialAdministration();
        var core = new ThrowingCoreAuthority();
        var revocable = new MustNotRunRevocableAuthority();
        var facade = new RevocableAssistantEditorialAdministration(inner, core, revocable);

        var result = await facade.InspectAsync(Actor(), Guid.CreateVersion7(),
            TestContext.Current.CancellationToken);

        Assert.Equal(EditorialLookupStatus.Forbidden, result.Status);
        Assert.Null(result.Article);
        Assert.Equal(0, core.Calls);
        Assert.Equal(0, revocable.Calls);
        Assert.Equal(0, inner.Calls);
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] =
                "Host=127.0.0.1;Database=no_abrir;Username=no_abrir;Password=no_abrir"
        })
        .Build();

    private static EditorialPersistencePolicy Policy() => new(
        new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [InstallationId] = ["ejemplo.test"]
        });

    private static EditorialPrincipal Actor() => new(
        InstallationId, EditorialRoles.Admin, "writer-node", HasArticleWriteAuthority: true);

    private sealed class DeniedCoreAuthority : IAssistantEditorialAuthority
    {
        public int Calls { get; private set; }

        public Task<EditorialAuthorityDecision> EvaluateAsync(
            EditorialAuthorityRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new EditorialAuthorityDecision(
                EditorialAuthorityOutcome.Denied,
                EditorialDenialReason.ContextualPolicyUnratified,
                null,
                null,
                DateTimeOffset.Parse("2026-10-28T12:00:00Z")));
        }
    }

    private sealed class AllowedCoreAuthority : IAssistantEditorialAuthority
    {
        public int Calls { get; private set; }
        public EditorialAuthorityDecision? LastDecision { get; private set; }

        public Task<EditorialAuthorityDecision> EvaluateAsync(
            EditorialAuthorityRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            LastDecision = new EditorialAuthorityDecision(
                EditorialAuthorityOutcome.Allowed,
                EditorialDenialReason.ContextualPolicyUnratified,
                new EditorialActor(7, "home-node", EditorialRoles.Admin,
                    Guid.Parse("d933e393-9344-4d8c-92ae-bd1838b6ddd4")),
                new EditorialInstallation(
                    Guid.Parse("187bbb04-b20b-42b3-ad11-63a2e39d22ee"),
                    "trial",
                    DateTimeOffset.Parse("2026-11-28T12:00:00Z")),
                DateTimeOffset.Parse("2026-10-28T12:00:00Z"));
            return Task.FromResult(LastDecision);
        }
    }

    private sealed class ThrowingCoreAuthority : IAssistantEditorialAuthority
    {
        public int Calls { get; private set; }

        public Task<EditorialAuthorityDecision> EvaluateAsync(
            EditorialAuthorityRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("No debía evaluarse CORE.");
        }
    }

    private sealed class MustNotRunRevocableAuthority : IRevocableAssistantEditorialAuthority
    {
        public int Calls { get; private set; }

        public Task<AssistantAuthorizedExecutionOutcome> ExecuteAsync(
            AssistantEditorialAuthorizationRequest request,
            Func<EditorialPrincipal, IEditorialCommitGate?, CancellationToken, Task> operation,
            CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("CORE Denied debía prevalecer.");
        }
    }

    private sealed class CallbackWithoutCommitGate(EditorialPrincipal actor)
        : IRevocableAssistantEditorialAuthority
    {
        public int Calls { get; private set; }

        public async Task<AssistantAuthorizedExecutionOutcome> ExecuteAsync(
            AssistantEditorialAuthorizationRequest request,
            Func<EditorialPrincipal, IEditorialCommitGate?, CancellationToken, Task> operation,
            CancellationToken cancellationToken)
        {
            Calls++;
            using var revocation = new CancellationTokenSource();
            await operation(actor, null, revocation.Token);
            return AssistantAuthorizedExecutionOutcome.Completed;
        }
    }

    private sealed class CountingEditorialAdministration : IAssistantEditorialAdministration
    {
        public int Calls { get; private set; }

        public Task<EditorialResult> CreateAsync(
            EditorialPrincipal actor, Guid articleId, CancellationToken ct)
            => Called(EditorialResult.Saved);

        public Task<EditorialResult> SaveDraftAsync(
            EditorialPrincipal actor, Guid articleId, KnowledgeDraft draft,
            long expectedVersion, CancellationToken ct)
            => Called(EditorialResult.Saved);

        public Task<EditorialResult> PublishAsync(
            EditorialPrincipal actor, Guid articleId, Guid revisionId,
            long expectedVersion, CancellationToken ct)
            => Called(EditorialResult.Published);

        public Task<EditorialResult> WithdrawAsync(
            EditorialPrincipal actor, Guid articleId, Guid revisionId,
            long expectedVersion, CancellationToken ct)
            => Called(EditorialResult.Withdrawn);

        public Task<EditorialArticleLookup> InspectAsync(
            EditorialPrincipal actor, Guid articleId, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new EditorialArticleLookup(
                EditorialLookupStatus.Found, null));
        }

        private Task<EditorialResult> Called(EditorialResult result)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }
}
