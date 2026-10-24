using System.Collections;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

public sealed class FaqEvidenciaInvalidaTests
{
    private static readonly Guid Installation = Guid.NewGuid();
    private static readonly Guid Article = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
    private static PublishedKnowledge Publication => new(Article, Guid.NewGuid(), Installation,
        "¿Horario?", "De lunes a viernes.", "Horario", new Uri("https://example.com/horario"), 1, Now);

    [Fact]
    public void PublicacionFuturaNoAutorizaRespuesta()
    {
        var result = new FaqService(new Source(Publication with { PublishedAt = Now.AddTicks(1) }))
            .Answer(Installation, "¿Horario?", Now);
        Assert.Equal(FaqOutcome.Insufficient, result.Outcome);
        Assert.Null(result.PublicUrl);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LecturaNulaSeAbstieneInclusoEnRevalidacion(bool lastRead)
    {
        var result = new FaqService(new Source(Publication) { NullReadAt = lastRead ? 2 : 1 })
            .Answer(Installation, "¿Horario?", Now);
        Assert.Equal(FaqOutcome.Unavailable, result.Outcome);
        Assert.Null(result.ArticleId);
        Assert.Null(result.SourceTitle);
    }

    [Fact]
    public void ColeccionQueFallaAlEnumerarNoFiltraExcepcion()
    {
        var result = new FaqService(new Source(Publication) { BrokenCandidates = true })
            .Answer(Installation, "¿Horario?", Now);
        Assert.Equal(FaqOutcome.Unavailable, result.Outcome);
        Assert.Null(result.PublicUrl);
    }

    [Fact]
    public void PublicacionEnInstanteActualSigueRespondiendo()
    {
        var result = new FaqService(new Source(Publication)).Answer(Installation, "¿Horario?", Now);
        Assert.Equal(FaqOutcome.Answered, result.Outcome);
        Assert.Equal("De lunes a viernes.", result.Answer);
    }

    private sealed class Source(PublishedKnowledge publication) : ILocalKnowledgeAuthority
    {
        public int NullReadAt { get; init; }
        public bool BrokenCandidates { get; init; }
        private int reads;
        public IReadOnlyList<Guid> FindCandidateArticleIds(Guid installationId, string question, int max)
            => BrokenCandidates ? new BrokenList() : new[] { Article };
        public KnowledgeRead ReadCurrent(Guid installationId, Guid articleId)
            => ++reads == NullReadAt ? null! : new(KnowledgeReadStatus.Authoritative,
                publication, true, true, false, null);
        public bool IsAllowedPublicUri(Guid installationId, Uri uri) => true;
    }

    private sealed class BrokenList : IReadOnlyList<Guid>
    {
        public int Count => 1;
        public Guid this[int index] => Article;
        public IEnumerator<Guid> GetEnumerator() => throw new InvalidOperationException("Índice no disponible.");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
