using Sillar.Core.Contracts;
using Sillar.Core.Settings;

namespace Sillar.Core.Tests;

public sealed class ZonaAcreditadaTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-26T18:00:00Z");
    private static readonly Guid Installation = Guid.NewGuid();
    private static readonly TzdbObservation Source = new(true, "system-tzdata.zi", "2026f:huella", true);
    private static TimeZoneAttestation Proof() => new(Guid.CreateVersion7(), Installation, "America/Lima",
        Source.SourceName!, Source.SourceVersion!, 7, "principal", Now, null,
        "Persona acreditante", InstallationTimeZoneService.AuthorityVersion);

    private static InstallationTimeZoneResolution Evaluate(TimeZoneAttestation proof, TzdbObservation? source = null)
        => InstallationTimeZoneEvaluator.Evaluate(IanaZoneCandidate.Evaluate("America/Lima"), "America/Lima",
            new(source ?? Source, Lookup.Found, proof, Lookup.Found, Installation), Now);

    [Fact]
    public void Solo_la_evidencia_completa_vigente_acredita_la_zona_configurada()
    {
        var proof = Proof();
        var result = Evaluate(proof);
        Assert.Equal(TimeZoneVerificationStatus.Verified, result.Status);
        Assert.Equal(TimeZoneVerificationReason.AttestationAccepted, result.Reason);
        Assert.Equal(proof.AttestationId, result.AttestationId);
    }

    [Theory]
    [InlineData("nombre")]
    [InlineData("nodo")]
    [InlineData("id")]
    [InlineData("autoridad")]
    [InlineData("futuro")]
    [InlineData("uuid")]
    [InlineData("instalacion")]
    [InlineData("revocada")]
    [InlineData("fuente")]
    [InlineData("zona")]
    public void Una_evidencia_incompleta_o_alterada_no_acredita(string corruption)
    {
        var proof = Proof();
        proof = corruption switch
        {
            "nombre" => proof with { AttestedByName = " " },
            "nodo" => proof with { AttestedByHomeNode = " " },
            "id" => proof with { AttestedByAdminUserId = 0 },
            "autoridad" => proof with { AuthorityVersion = null },
            "futuro" => proof with { AttestedAt = Now.AddSeconds(1) },
            "uuid" => proof with { AttestationId = Guid.NewGuid() },
            "instalacion" => proof with { InstallationKey = Guid.NewGuid() },
            "revocada" => proof with { RevokedAt = Now },
            "fuente" => proof with { SourceVersion = "antigua" },
            "zona" => proof with { ZoneId = "America/Bogota" },
            _ => throw new InvalidOperationException(),
        };
        Assert.NotEqual(TimeZoneVerificationStatus.Verified, Evaluate(proof).Status);
    }

    [Fact]
    public void La_fuente_distingue_alias_y_detecta_un_cambio_aun_con_igual_version_declarada()
    {
        const string data = "# version 2026f\nZ America/Lima -5 - -05\nL America/Lima America/Alias\n";
        var original = SystemTzdbSource.ObserveText("America/Lima", data);
        Assert.True(original.IsAvailable);
        Assert.True(original.IsCanonical);
        Assert.False(SystemTzdbSource.ObserveText("America/Alias", data).IsCanonical);
        Assert.NotEqual(original.SourceVersion, SystemTzdbSource.ObserveText("America/Lima", data + "# cambio\n").SourceVersion);
        Assert.False(SystemTzdbSource.ObserveText("America/Lima", "Z America/Lima -5 - -05").IsAvailable);
        Assert.NotEqual(TimeZoneVerificationStatus.Verified, Evaluate(Proof(), TzdbObservation.Unavailable).Status);
    }
}
