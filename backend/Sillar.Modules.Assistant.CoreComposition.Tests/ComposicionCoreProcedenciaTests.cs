using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core;
using Sillar.Core.Authorization;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Assistant.CoreComposition.Tests;

/// <summary>
/// Composición real del productor editorial CORE canónico, sin host ni servidor.
/// El interceptor aborta antes de abrir una conexión y ninguna decisión habilita M12.
/// </summary>
public sealed class ComposicionCoreProcedenciaTests
{
    private static readonly Guid SessionId =
        Guid.Parse("01920000-0000-7000-8000-0000000000c1");

    private static ServiceCollection Services(NoConnections blocker, bool registerCandidate = true)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton(new NodeIdentity("principal"));
        services.AddDbContext<CoreDbContext>(options => options
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=no_connection;Username=unused;Password=unused;Pooling=false")
            .AddInterceptors(blocker));
        services.AddHttpContextAccessor();
        services.AddCoreAuthentication();
        if (registerCandidate) services.AddCandidateAssistantEditorialAuthority();
        return services;
    }

    [Fact]
    public async Task ProductorCoreSinSesionDeniegaSinIntentarConexion()
    {
        var blocker = new NoConnections();
        await using var provider = Services(blocker).BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IAssistantEditorialAuthority>()
            .EvaluateAsync(new("article-1", EditorialOperation.Edit),
                TestContext.Current.CancellationToken);

        Assert.Equal(EditorialAuthorityOutcome.Denied, result.Outcome);
        Assert.Equal(EditorialDenialReason.SessionMissing, result.Reason);
        Assert.False(result.IsAllowed);
        Assert.Null(result.Actor);
        Assert.Null(result.Installation);
        Assert.Equal(0, blocker.Blocked);
    }

    [Fact]
    public async Task ProductorCoreConSesionYLecturaBloqueadaDeniegaAutoridadNoDisponible()
    {
        var blocker = new NoConnections();
        await using var provider = Services(blocker).BuildServiceProvider();
        SetSession(provider);
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IAssistantEditorialAuthority>()
            .EvaluateAsync(new("article-1", EditorialOperation.Publish),
                TestContext.Current.CancellationToken);

        Assert.Equal(EditorialAuthorityOutcome.Denied, result.Outcome);
        Assert.Equal(EditorialDenialReason.AuthorityUnavailable, result.Reason);
        Assert.False(result.IsAllowed);
        Assert.Null(result.Actor);
        Assert.Null(result.Installation);
        Assert.Equal(1, blocker.Blocked);
    }

    [Fact]
    public void CoreNoRegistraAutoridadEditorialCandidataImplicitamente()
    {
        var services = Services(new NoConnections(), registerCandidate: false);

        Assert.DoesNotContain(services,
            descriptor => descriptor.ServiceType == typeof(IAssistantEditorialAuthority));
    }

    [Fact]
    public async Task CancelacionPreviaNoIntentaAbrirConexion()
    {
        var blocker = new NoConnections();
        await using var provider = Services(blocker).BuildServiceProvider();
        SetSession(provider);
        await using var scope = provider.CreateAsyncScope();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.ServiceProvider
            .GetRequiredService<IAssistantEditorialAuthority>()
            .EvaluateAsync(new("article-1", EditorialOperation.Retire), cancellation.Token));

        Assert.Equal(0, blocker.Blocked);
    }

    private static void SetSession(IServiceProvider provider)
    {
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "7"),
                new Claim("sillar:admin:session_id", SessionId.ToString("D"))
            ], "test"))
        };
    }

    private sealed class NoConnections : DbConnectionInterceptor
    {
        public int Blocked { get; private set; }

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            Blocked++;
            throw new InvalidOperationException("Lectura física prohibida en esta prueba de composición.");
        }

        public override InterceptionResult ConnectionOpening(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result)
        {
            Blocked++;
            throw new InvalidOperationException("Lectura física prohibida en esta prueba de composición.");
        }
    }
}
