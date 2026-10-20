using Microsoft.AspNetCore.Http.HttpResults;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

public sealed class HostAttestedAssistantInstallationScopeTests
{
    private static readonly Guid InstallationA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid InstallationB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    private sealed class ServerEvidence : ITrustedAssistantHostEvidence
    {
        public bool IsInstallationConfigured { get; set; } = true;
        public bool IsAssistantActive { get; set; } = true;
        public bool IsAssistantLicensed { get; set; } = true;
        public Guid? InstallationId { get; set; } = InstallationA;
        public bool Fail { get; set; }
        bool ITrustedAssistantHostEvidence.IsInstallationConfigured => Read(IsInstallationConfigured);
        bool ITrustedAssistantHostEvidence.IsAssistantActive => Read(IsAssistantActive);
        bool ITrustedAssistantHostEvidence.IsAssistantLicensed => Read(IsAssistantLicensed);
        Guid? ITrustedAssistantHostEvidence.InstallationId => Fail
            ? throw new InvalidOperationException("Autoridad CORE no disponible") : InstallationId;
        private bool Read(bool value) => Fail
            ? throw new InvalidOperationException("Autoridad CORE no disponible") : value;
    }

    private sealed class FaqReader : IAsyncPublishedFaqReader
    {
        public int Calls { get; private set; }
        public Guid LastInstallation { get; private set; }
        public Task<FaqResult> AnswerAsync(Guid installationId, string? question,
            DateTimeOffset now, CancellationToken ct)
        {
            Calls++;
            LastInstallation = installationId;
            return Task.FromResult(FaqResult.Abstain(FaqOutcome.Insufficient));
        }
    }

    [Fact]
    public void AutoridadConfiguradaActivaLicenciadaDevuelveIdentidad()
    {
        var gate = new HostAttestedAssistantInstallationScope(new ServerEvidence());
        Assert.True(gate.TryGetEnabledInstallation(out var id));
        Assert.Equal(InstallationA, id);
    }

    [Fact]
    public void InstalacionNoConfiguradaCierraAcceso()
    {
        var gate = new HostAttestedAssistantInstallationScope(new ServerEvidence
            { IsInstallationConfigured = false });
        Assert.False(gate.TryGetEnabledInstallation(out var id));
        Assert.Equal(Guid.Empty, id);
    }

    [Fact]
    public void ModuloInactivoCierraAcceso()
    {
        var gate = new HostAttestedAssistantInstallationScope(new ServerEvidence
            { IsAssistantActive = false });
        Assert.False(gate.TryGetEnabledInstallation(out var id));
        Assert.Equal(Guid.Empty, id);
    }

    [Fact]
    public void LicenciaAusenteCierraAcceso()
    {
        var gate = new HostAttestedAssistantInstallationScope(new ServerEvidence
            { IsAssistantLicensed = false });
        Assert.False(gate.TryGetEnabledInstallation(out var id));
        Assert.Equal(Guid.Empty, id);
    }

    [Fact]
    public void IdentidadNulaOVaciaCierraAcceso()
    {
        foreach (var candidate in new Guid?[] { null, Guid.Empty })
        {
            var gate = new HostAttestedAssistantInstallationScope(new ServerEvidence
                { InstallationId = candidate });
            Assert.False(gate.TryGetEnabledInstallation(out var id));
            Assert.Equal(Guid.Empty, id);
        }
    }

    [Fact]
    public void ExcepcionDeAutoridadCierraAcceso()
    {
        var gate = new HostAttestedAssistantInstallationScope(new ServerEvidence { Fail = true });
        Assert.False(gate.TryGetEnabledInstallation(out var id));
        Assert.Equal(Guid.Empty, id);
    }

    [Fact]
    public async Task PeticionNoPuedeForzarOtraInstalacionYNoSeConsultaSiFaltaLicencia()
    {
        var evidence = new ServerEvidence { IsAssistantLicensed = false };
        var reader = new FaqReader();
        var result = await AssistantFaqEndpoints.HandleFaqAsync(
            new AssistantFaqRequest($"¿Horario? installationId={InstallationB}"),
            new HostAttestedAssistantInstallationScope(evidence), reader,
            TimeProvider.System, TestContext.Current.CancellationToken);
        Assert.IsType<NotFound>(result);
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task CuerpoQueNombraOtraInstalacionNoCambiaElAmbitoServidor()
    {
        var reader = new FaqReader();
        var result = await AssistantFaqEndpoints.HandleFaqAsync(
            new AssistantFaqRequest($"¿Horario? installationId={InstallationB}"),
            new HostAttestedAssistantInstallationScope(new ServerEvidence()), reader,
            TimeProvider.System, TestContext.Current.CancellationToken);
        Assert.IsType<Ok<AssistantFaqReply>>(result);
        Assert.Equal(1, reader.Calls);
        Assert.Equal(InstallationA, reader.LastInstallation);
    }
}
