using Npgsql;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;
using Xunit;

namespace Sillar.Modules.Assistant.Tests;

/// <summary>
/// P8: pruebas focales de denegación previa a cualquier operación de base.
/// El NpgsqlDataSource apunta a un puerto local no destinado a una base real:
/// no se llama OpenConnectionAsync mientras la puerta rechaza la operación.
/// No acredita revalidación de permisos durante una transacción ni CAS físico.
/// </summary>
public sealed class DurableQuotaPolicySecurityP8Tests
{
    private static readonly Guid InstallationA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid InstallationB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    private sealed class InstallationScope(Guid installation, bool success = true, bool throws = false)
        : ITrustedAssistantInstallationScope
    {
        public int Calls { get; private set; }

        public bool TryGetEnabledInstallation(out Guid installationId)
        {
            Calls++;
            if (throws) throw new InvalidOperationException("CORE no disponible");
            installationId = installation;
            return success;
        }
    }

    private sealed class PolicyWriter(Func<Guid, bool> permission) : ITrustedAssistantQuotaPolicyWriter
    {
        public int Calls { get; private set; }
        public Guid LastInstallation { get; private set; }

        public bool CanWritePolicy(Guid installationId)
        {
            Calls++;
            LastInstallation = installationId;
            return permission(installationId);
        }
    }

    private static NpgsqlDataSource NoDatabase() => NpgsqlDataSource.Create(
        "Host=127.0.0.1;Port=1;Database=sillar_m12_p8_no_db;Username=nadie;Password=nadie;Timeout=1;Pooling=false");

    private static DurableQuotaPolicyChange ValidChange() =>
        new(AssistantMode.ExternalApi, true, 100, 2);

    [Fact]
    public async Task LecturaRechazaScopeNegadoAunqueRetorneGuid()
    {
        using var source = NoDatabase();
        var scope = new InstallationScope(InstallationA, success: false);
        var writer = new PolicyWriter(_ => true);
        var store = new DurableAssistantQuotaPolicyStore(source, scope, writer);
        var result = await store.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DurableQuotaPolicyOutcome.Denied, result.Outcome);
        Assert.Null(result.Snapshot);
        Assert.Equal(0, writer.Calls);
    }

    [Fact]
    public async Task EscrituraRechazaScopeNegadoSinConsultarWriter()
    {
        using var source = NoDatabase();
        var writer = new PolicyWriter(_ => true);
        var store = new DurableAssistantQuotaPolicyStore(
            source, new InstallationScope(InstallationA, success: false), writer);
        var result = await store.TryUpdateAsync(0, ValidChange(), TestContext.Current.CancellationToken);
        Assert.Equal(DurableQuotaPolicyOutcome.Denied, result.Outcome);
        Assert.Equal(0, writer.Calls);
    }

    [Fact]
    public async Task LecturaNoAceptaGuidVacioAunqueScopeDevuelvaTrue()
    {
        using var source = NoDatabase();
        var store = new DurableAssistantQuotaPolicyStore(
            source, new InstallationScope(Guid.Empty), new PolicyWriter(_ => true));
        var result = await store.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DurableQuotaPolicyOutcome.Denied, result.Outcome);
    }

    [Fact]
    public async Task EscrituraNoAceptaGuidVacioYNoPreguntaPorPermiso()
    {
        using var source = NoDatabase();
        var writer = new PolicyWriter(_ => true);
        var store = new DurableAssistantQuotaPolicyStore(source, new InstallationScope(Guid.Empty), writer);
        var result = await store.TryUpdateAsync(0, ValidChange(), TestContext.Current.CancellationToken);
        Assert.Equal(DurableQuotaPolicyOutcome.Denied, result.Outcome);
        Assert.Equal(0, writer.Calls);
    }

    [Fact]
    public async Task LecturaDenegadaCuandoAutoridadScopeFalla()
    {
        using var source = NoDatabase();
        var store = new DurableAssistantQuotaPolicyStore(
            source, new InstallationScope(InstallationA, throws: true), new PolicyWriter(_ => true));
        var result = await store.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DurableQuotaPolicyOutcome.Denied, result.Outcome);
    }

    [Fact]
    public async Task EscrituraDenegadaCuandoAutoridadScopeFalla()
    {
        using var source = NoDatabase();
        var writer = new PolicyWriter(_ => true);
        var store = new DurableAssistantQuotaPolicyStore(
            source, new InstallationScope(InstallationA, throws: true), writer);
        var result = await store.TryUpdateAsync(0, ValidChange(), TestContext.Current.CancellationToken);
        Assert.Equal(DurableQuotaPolicyOutcome.Denied, result.Outcome);
        Assert.Equal(0, writer.Calls);
    }

    [Fact]
    public async Task PermisoExplicitoNegadoImpideEscritura()
    {
        using var source = NoDatabase();
        var writer = new PolicyWriter(_ => false);
        var store = new DurableAssistantQuotaPolicyStore(source, new InstallationScope(InstallationA), writer);
        var result = await store.TryUpdateAsync(0, ValidChange(), TestContext.Current.CancellationToken);
        Assert.Equal(DurableQuotaPolicyOutcome.Denied, result.Outcome);
        Assert.Equal(1, writer.Calls);
    }

    [Fact]
    public async Task ExcepcionDelWriterImpideEscritura()
    {
        using var source = NoDatabase();
        var writer = new PolicyWriter(_ => throw new InvalidOperationException("permiso no disponible"));
        var store = new DurableAssistantQuotaPolicyStore(source, new InstallationScope(InstallationA), writer);
        var result = await store.TryUpdateAsync(0, ValidChange(), TestContext.Current.CancellationToken);
        Assert.Equal(DurableQuotaPolicyOutcome.Denied, result.Outcome);
        Assert.Equal(1, writer.Calls);
    }

    [Fact]
    public async Task NoSeAutorizaOtraInstalacionAlConsultarWriter()
    {
        using var source = NoDatabase();
        var writer = new PolicyWriter(id => id == InstallationB);
        var store = new DurableAssistantQuotaPolicyStore(source, new InstallationScope(InstallationA), writer);
        var result = await store.TryUpdateAsync(0, ValidChange(), TestContext.Current.CancellationToken);
        Assert.Equal(DurableQuotaPolicyOutcome.Denied, result.Outcome);
        Assert.Equal(InstallationA, writer.LastInstallation);
        Assert.Equal(1, writer.Calls);
    }

    [Fact]
    public async Task PoliticaInvalidaNoAbreBaseInclusoConPermisoValido()
    {
        using var source = NoDatabase();
        var writer = new PolicyWriter(_ => true);
        var store = new DurableAssistantQuotaPolicyStore(source, new InstallationScope(InstallationA), writer);
        var bad = new DurableQuotaPolicyChange((AssistantMode)65536, false, 100, 2);
        var result = await store.TryUpdateAsync(0, bad, TestContext.Current.CancellationToken);
        Assert.Equal(DurableQuotaPolicyOutcome.Invalid, result.Outcome);
        Assert.Equal(1, writer.Calls);
    }

    [Fact]
    public async Task SinPermisoPriorizaDeniedSobreParametrosInvalidos()
    {
        using var source = NoDatabase();
        var store = new DurableAssistantQuotaPolicyStore(
            source, new InstallationScope(InstallationA), new PolicyWriter(_ => false));
        var result = await store.TryUpdateAsync(-1, null, TestContext.Current.CancellationToken);
        Assert.Equal(DurableQuotaPolicyOutcome.Denied, result.Outcome);
    }
}
