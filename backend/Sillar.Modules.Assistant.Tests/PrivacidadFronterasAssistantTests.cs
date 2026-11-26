using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

public sealed class PrivacidadFronterasAssistantTests
{
    private const string Sensitive = "pregunta-privada-prueba; Password=secreto-prueba; prompt-interno-prueba";
    private static readonly Guid Installation = Guid.NewGuid();
    private static readonly Guid Article = Guid.NewGuid();
    private static readonly Guid Revision = Guid.NewGuid();

    [Theory]
    [InlineData("create", false)]
    [InlineData("inspect", false)]
    [InlineData("draft", false)]
    [InlineData("publish", false)]
    [InlineData("withdraw", false)]
    [InlineData("create", true)]
    [InlineData("inspect", true)]
    [InlineData("draft", true)]
    [InlineData("publish", true)]
    [InlineData("withdraw", true)]
    public async Task FalloEditorialNoExponeContenidoNiDetallesTecnicos(string operation, bool asynchronous)
    {
        var service = new BrokenEditorial(() => new InvalidOperationException(Sensitive,
            new Exception(Sensitive)), asynchronous);
        var result = await Run(operation, service, CancellationToken.None);
        AssertPrivateFailure(result);
        Assert.Equal(1, service.Calls); // No reintentar una escritura de resultado incierto.
    }

    [Theory]
    [InlineData("create")]
    [InlineData("inspect")]
    [InlineData("draft")]
    [InlineData("publish")]
    [InlineData("withdraw")]
    public async Task CancelacionEditorialPreservaTokenSinContenidoDelProveedor(string operation)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new BrokenEditorial(() => SensitiveCancellation(cancellation.Token), true);
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Run(operation, service, cancellation.Token));
        AssertPrivateCancellation(error, cancellation.Token);
        Assert.Equal(1, service.Calls);
    }

    [Fact]
    public async Task CancelacionAjenaALaPeticionEditorialDevuelveErrorMinimo()
    {
        var service = new BrokenEditorial(() => SensitiveCancellation(CancellationToken.None), true);
        AssertPrivateFailure(await Run("draft", service, CancellationToken.None));
    }

    [Fact]
    public async Task CancelacionFaqNoPropagaPreguntaNiPromptEnExcepcion()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            AssistantFaqEndpoints.HandleFaqAsync(new(Sensitive), new FaqScope(),
                new BrokenFaq(() => SensitiveCancellation(cancellation.Token)),
                TimeProvider.System, cancellation.Token));
        AssertPrivateCancellation(error, cancellation.Token);
    }

    [Fact]
    public async Task ErrorFaqNoReflejaPreguntaSecretoNiIdentidadInterna()
    {
        var result = await AssistantFaqEndpoints.HandleFaqAsync(new(Sensitive), new FaqScope(),
            new BrokenFaq(() => new Exception(Sensitive)), TimeProvider.System, CancellationToken.None);
        AssertPrivateFailure(result);
    }

    private static void AssertPrivateFailure(IResult result)
    {
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(503, problem.StatusCode);
        Assert.Null(problem.ProblemDetails.Detail);
        Assert.Empty(problem.ProblemDetails.Extensions);
        var json = JsonSerializer.Serialize(problem.ProblemDetails);
        Assert.DoesNotContain(Sensitive, json);
        Assert.DoesNotContain("secreto-prueba", json);
        Assert.DoesNotContain("prompt-interno-prueba", json);
        Assert.DoesNotContain(Installation.ToString(), json);
        Assert.DoesNotContain(Article.ToString(), json);
    }

    private static void AssertPrivateCancellation(OperationCanceledException error, CancellationToken token)
    {
        Assert.Equal(token, error.CancellationToken);
        Assert.Null(error.InnerException);
        Assert.Empty(error.Data);
        Assert.DoesNotContain(Sensitive, error.ToString());
        Assert.DoesNotContain("secreto-prueba", error.ToString());
    }

    private static OperationCanceledException SensitiveCancellation(CancellationToken ct)
    {
        var error = new OperationCanceledException(Sensitive, new Exception(Sensitive), ct);
        error.Data["pregunta"] = Sensitive;
        return error;
    }

    private static Task<IResult> Run(string operation, IAssistantEditorialAdministration service,
        CancellationToken ct) => operation switch
    {
        "create" => AssistantEditorialEndpoints.Create(new EditorialScope(), service, ct),
        "inspect" => AssistantEditorialEndpoints.Inspect(Article, new EditorialScope(), service, ct),
        "draft" => AssistantEditorialEndpoints.SaveDraft(Article,
            new(Sensitive, Sensitive, "Título", "https://example.com/faq", 0),
            new EditorialScope(), service, ct),
        "publish" => AssistantEditorialEndpoints.Publish(Article, new(Revision, 0), new EditorialScope(), service, ct),
        "withdraw" => AssistantEditorialEndpoints.Withdraw(Article, new(Revision, 0), new EditorialScope(), service, ct),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private sealed class EditorialScope : ITrustedEditorialAdminScope
    {
        public bool TryGetPrincipal(out EditorialPrincipal? actor)
        {
            actor = new(Installation, EditorialRoles.Admin, "principal", true);
            return true;
        }
    }

    private sealed class FaqScope : ITrustedAssistantInstallationScope
    {
        public bool TryGetEnabledInstallation(out Guid installationId)
        {
            installationId = Installation;
            return true;
        }
    }

    private sealed class BrokenFaq(Func<Exception> error) : IAsyncPublishedFaqReader
    {
        public Task<FaqResult> AnswerAsync(Guid installationId, string? question,
            DateTimeOffset now, CancellationToken ct) => Task.FromException<FaqResult>(error());
    }

    private sealed class BrokenEditorial(Func<Exception> error, bool asynchronous) : IAssistantEditorialAdministration
    {
        public int Calls { get; private set; }
        private Task<T> Fail<T>()
        {
            Calls++;
            if (asynchronous) return Task.FromException<T>(error());
            throw error();
        }
        public Task<EditorialResult> CreateAsync(EditorialPrincipal actor, Guid id, CancellationToken ct)
            => Fail<EditorialResult>();
        public Task<EditorialArticleLookup> InspectAsync(EditorialPrincipal actor, Guid id, CancellationToken ct)
            => Fail<EditorialArticleLookup>();
        public Task<EditorialResult> SaveDraftAsync(EditorialPrincipal actor, Guid id, KnowledgeDraft draft,
            long version, CancellationToken ct) => Fail<EditorialResult>();
        public Task<EditorialResult> PublishAsync(EditorialPrincipal actor, Guid id, Guid revision,
            long version, CancellationToken ct) => Fail<EditorialResult>();
        public Task<EditorialResult> WithdrawAsync(EditorialPrincipal actor, Guid id, Guid revision,
            long version, CancellationToken ct) => Fail<EditorialResult>();
    }
}
