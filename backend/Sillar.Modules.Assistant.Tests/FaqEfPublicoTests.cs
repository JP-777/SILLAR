using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Sillar.Modules.Assistant.Data;
using Sillar.Modules.Assistant.Data.Entities;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

public sealed class FaqEfPublicoTests
{
    private static readonly Guid Instalacion = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset Ahora = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private sealed class Ambito(Guid id, bool activo = true, bool romper = false)
        : ITrustedAssistantInstallationScope
    {
        public bool TryGetEnabledInstallation(out Guid installationId)
        {
            if (romper) throw new InvalidOperationException("autoridad inaccesible");
            installationId = id;
            return activo;
        }
    }

    private sealed class Reloj : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    private sealed class Fuente : IAsyncPublishedFaqReader
    {
        public int Calls { get; private set; }
        public Guid Installation { get; private set; }
        public string? Question { get; private set; }
        public FaqResult Result { get; set; } = FaqResult.Abstain(FaqOutcome.Insufficient);
        public bool Fail { get; set; }

        public Task<FaqResult> AnswerAsync(Guid installationId, string? question,
            DateTimeOffset now, CancellationToken ct)
        {
            Calls++;
            Installation = installationId;
            Question = question;
            if (Fail) throw new InvalidOperationException("fuente rota");
            return Task.FromResult(Result);
        }
    }

    private static Task<Microsoft.AspNetCore.Http.IResult> Run(AssistantFaqRequest? request,
        ITrustedAssistantInstallationScope scope, Fuente source)
        => AssistantFaqEndpoints.HandleFaqAsync(request, scope, source, new Reloj(),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task FuentePublicaAutoritativaRespondeConCita()
    {
        var src = new Fuente { Result = new(FaqOutcome.Answered, "Atendemos lunes.",
            Guid.NewGuid(), Guid.NewGuid(), "Horario", "https://empresa.example/horario") };
        var reply = Assert.IsType<Ok<AssistantFaqReply>>(await Run(new("¿Horario?"),
            new Ambito(Instalacion), src));
        Assert.Equal("answered", reply.Value!.Status);
        Assert.Equal("Horario", reply.Value.SourceTitle);
        Assert.Equal(Instalacion, src.Installation);
    }

    [Fact]
    public async Task SinFuentesPublicadasNoInventaRespuesta()
    {
        var reply = Assert.IsType<Ok<AssistantFaqReply>>(await Run(new("¿Horario?"),
            new Ambito(Instalacion), new Fuente()));
        Assert.Equal("insufficient", reply.Value!.Status);
        Assert.Null(reply.Value.SourceUrl);
    }

    [Fact]
    public async Task FuenteIndeterminadaNoAseguraRespuesta()
    {
        var src = new Fuente { Result = FaqResult.Abstain(FaqOutcome.Unavailable) };
        var reply = Assert.IsType<ProblemHttpResult>(await Run(new("¿Horario?"),
            new Ambito(Instalacion), src));
        Assert.Equal(503, reply.StatusCode);
    }

    [Fact]
    public async Task InstalacionDeshabilitadaNiAccedeALaFuente()
    {
        var src = new Fuente();
        Assert.IsType<NotFound>(await Run(new("¿Horario?"), new Ambito(Instalacion, false), src));
        Assert.Equal(0, src.Calls);
    }

    [Fact]
    public async Task ErrorEnAutoridadNoAbreAccesoALaFuente()
    {
        var src = new Fuente();
        Assert.IsType<NotFound>(await Run(new("¿Horario?"), new Ambito(Instalacion, romper: true), src));
        Assert.Equal(0, src.Calls);
    }

    [Fact]
    public async Task CuerpoNuloNoConsultaBase()
    {
        var src = new Fuente();
        Assert.IsType<ValidationProblem>(await Run(null, new Ambito(Instalacion), src));
        Assert.Equal(0, src.Calls);
    }

    [Fact]
    public async Task PreguntaDemasiadoLargaNoConsultaBase()
    {
        var src = new Fuente();
        Assert.IsType<ValidationProblem>(await Run(new(new string('x', 501)),
            new Ambito(Instalacion), src));
        Assert.Equal(0, src.Calls);
    }

    [Fact]
    public async Task PreguntaSoloEspaciosNoConsultaBase()
    {
        var src = new Fuente();
        Assert.IsType<ValidationProblem>(await Run(new("   "), new Ambito(Instalacion), src));
        Assert.Equal(0, src.Calls);
    }

    [Fact]
    public async Task FalloDelProveedorDeFuenteProduce503()
    {
        var src = new Fuente { Fail = true };
        var reply = Assert.IsType<ProblemHttpResult>(await Run(new("¿Horario?"),
            new Ambito(Instalacion), src));
        Assert.Equal(503, reply.StatusCode);
    }

    [Fact]
    public async Task FuenteQueMienteSobreEnlaceNoSeCita()
    {
        var src = new Fuente { Result = new(FaqOutcome.Answered, "Respuesta",
            Guid.NewGuid(), Guid.NewGuid(), "Fuente", "http://inseguro.example") };
        var reply = Assert.IsType<ProblemHttpResult>(await Run(new("¿Horario?"),
            new Ambito(Instalacion), src));
        Assert.Equal(503, reply.StatusCode);
    }

    private static AssistantDbContext Modelo() => new(
        new DbContextOptionsBuilder<AssistantDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=unused;Password=unused")
            .Options);

    [Fact]
    public void ClaveDePreguntaExigeColacionDeterminista()
    {
        using var db = Modelo();
        var designModel = db.GetService<IDesignTimeModel>().Model;
        var entity = designModel.FindEntityType(typeof(EditorialRevisionRow))!;
        var prop = entity.FindProperty(nameof(EditorialRevisionRow.QuestionKey))!;
        Assert.Equal("C", prop.GetCollation());
        Assert.False(prop.IsNullable);
    }

    [Fact]
    public void LaBusquedaIncluyeInstalacionYClave()
    {
        using var db = Modelo();
        var entity = db.Model.FindEntityType(typeof(EditorialRevisionRow))!;
        Assert.Contains(entity.GetIndexes(), ix =>
            ix.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { "InstallationId", "QuestionKey" }));
    }

    [Fact]
    public void ClaveNormalizadaPreservaReglaDeFaqExistente()
    {
        Assert.Equal("¿cuál es el horario?", FaqService.Normalize("  ¿CUÁL   ES  EL HORARIO?  "));
    }
}
