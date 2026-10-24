using Sillar.Modules.Assistant.Data.Repositories;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

public sealed class DominiosEditorialesInternacionalizadosTests
{
    private static readonly Guid Installation = Guid.NewGuid();
    private static readonly Guid OtherInstallation = Guid.NewGuid();

    private static Dictionary<Guid, IReadOnlyCollection<string>> Hosts(string host) => new()
    {
        [Installation] = [host],
        [OtherInstallation] = ["example.org"]
    };

    [Theory]
    [InlineData("españa.example", "https://españa.example/horario")]
    [InlineData("españa.example", "https://xn--espaa-rta.example/horario")]
    [InlineData("xn--espaa-rta.example", "https://españa.example/horario")]
    [InlineData("ESPAÑA.example", "https://españa.example/horario")]
    public void DominioAutorizadoAceptaAmbasRepresentacionesDns(string host, string url)
    {
        var policy = new EditorialPersistencePolicy(Hosts(host));
        var local = new LocalEditorialKnowledgeStore(Hosts(host));
        Assert.True(policy.IsAllowedPublicUri(Installation, new Uri(url)));
        Assert.True(local.IsAllowedPublicUri(Installation, new Uri(url)));
        Assert.False(policy.IsAllowedPublicUri(OtherInstallation, new Uri(url)));
        Assert.False(local.IsAllowedPublicUri(OtherInstallation, new Uri(url)));
    }

    [Theory]
    [InlineData("https://españa.example.impostor.example/horario")]
    [InlineData("https://sub.españa.example/horario")]
    [InlineData("https://espana.example/horario")]
    [InlineData("https://usuario@españa.example/horario")]
    [InlineData("https://españa.example:444/horario")]
    [InlineData("http://españa.example/horario")]
    public void NormalizarNoAmpliaDominiosNiProtocolosPermitidos(string url)
    {
        Assert.False(new EditorialPersistencePolicy(Hosts("españa.example"))
            .IsAllowedPublicUri(Installation, new Uri(url)));
        Assert.False(new LocalEditorialKnowledgeStore(Hosts("españa.example"))
            .IsAllowedPublicUri(Installation, new Uri(url)));
    }

    [Fact]
    public void ArticuloConDominioInternacionalizadoCompletaCicloLocalDeFaq()
    {
        var store = new LocalEditorialKnowledgeStore(Hosts("españa.example"));
        var actor = new EditorialPrincipal(Installation, EditorialRoles.Admin, "principal", true);
        var article = Guid.NewGuid();
        var draft = new KnowledgeDraft(Guid.NewGuid(), "¿Horario?", "De lunes a viernes.",
            "Horario", new Uri("https://españa.example/horario"));
        var now = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(EditorialResult.Saved, store.Create(actor, article));
        Assert.Equal(EditorialResult.Saved, store.SaveDraft(actor, article, draft, 0));
        Assert.Equal(EditorialResult.Published, store.Publish(actor, article, draft.RevisionId, 1, now));
        var answer = new FaqService(store).Answer(Installation, draft.Question, now);
        Assert.Equal(FaqOutcome.Answered, answer.Outcome);
        Assert.Equal(draft.Answer, answer.Answer);
        Assert.Equal(draft.RevisionId, answer.RevisionId);
        Assert.Equal(EditorialResult.Withdrawn, store.Withdraw(actor, article, draft.RevisionId, 2));
        Assert.Equal(FaqOutcome.Insufficient, new FaqService(store).Answer(Installation, draft.Question, now).Outcome);
    }
}
